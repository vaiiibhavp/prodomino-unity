using Cysharp.Threading.Tasks;
using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.View;
using ProDomino.AnalyticsSystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Timba.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace DominoTemplate.DragAndDrop
{
    public class DragHandler : MonoBehaviour
    {
        public static AsyncActionHandler CreateSlot;

        [HideInInspector] public GameControler gameScript;

        [SerializeField] private Animator glowAnimator;
        [SerializeField] private GameObject _invisibleDominoPrefab;
        [SerializeField] private RectTransform _dragObject = null;
        public RectTransform DragObject => _dragObject;
        [SerializeField] private DominoView _dominoView = null;
        [SerializeField] private Button _currentButton = null;
        [SerializeField] private GameObject _selectedTileFeedbackObj = null;

        [Space(15)]
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float rotationSpeed = 1f;
        [SerializeField] private float selectTileDistance = 1f;

        private List<RectTransform> _slots = new List<RectTransform>();
        private Dictionary<RectTransform, float> _slotDictionary;

        private Vector2 _startPosition;
        private Image _glowImage;
        private float timeOfTravel = 0.5f;
        private float currentTime;
        private float normalizedValue;
        private bool changeParent;
        private float _yOffset;
        private bool isClicked = false;
        private bool isDragging = false;

        private AnalyticsManager analyticsManager;
        private RectTransformPanZoomController rectTransformPanZoomController;

        private static DragHandler lastSelectedDragHandler;
        private static UnityEvent onDragHandlerSelected;
        private static Func<bool> _isTimeout;

        public RectTransformPanZoomController RectTransformPanZoomController => rectTransformPanZoomController;

        private void Awake()
        {
            onDragHandlerSelected ??= new();
            onDragHandlerSelected.AddListener(OnDragHandlerSelected);

            // Try to get the Image component from the _selectedTileFeedbackObj's children and assign it to _glowImage
            if (_selectedTileFeedbackObj)
                _glowImage = _selectedTileFeedbackObj.GetComponentInChildren<Image>();
        }

        private void Start()
        {
            _yOffset = Screen.height / 3f;

            isClicked = false;
            isDragging = false;

            ShowSelectedTileFeedback(false);

            GameControler.SetSlotPoosition += SetSlotPosition;
        }

        private void OnDestroy()
        {
            GameControler.SetSlotPoosition -= SetSlotPosition;
            onDragHandlerSelected?.RemoveListener(OnDragHandlerSelected);
        }

        public void Initialize(RectTransformPanZoomController rectTransformPanZoomController, AnalyticsManager analyticsManager)
        {
            this.rectTransformPanZoomController = rectTransformPanZoomController;
            this.analyticsManager = analyticsManager;
        }

        public static void Static_Initialize(Func<bool> isTimeout)
        {
            _isTimeout = isTimeout;
        }


        private void SetSlotPosition(List<RectTransform> slots)
        {
            _slots = slots;
        }

        public DominoView GetDominoView()
        {
            return _dominoView;
        }

        /// <summary>
        /// Enables the glow animation when the pointer enters the tile, if the button is interactable and the domino is
        /// available.
        /// </summary>
        /// <param name="eventData">Pointer event data associated with the pointer enter event.</param>
        public void OnPointerEnterProxy(BaseEventData eventData)
        {
            // Check if the event data is of type PointerEventData, if not, return early
            if (eventData is not PointerEventData pointerEventData)
                return;

            // Check if the button is interactable and the domino is available before showing the glow animation
            if (!_currentButton.interactable || !_dominoView.GetDomino().Available)
                return;
        }

        public async void OnPointerClickProxy(BaseEventData eventData)
        {
            if (eventData is not PointerEventData pointerEventData)
                return;

            if (!_currentButton.interactable || !_dominoView.GetDomino().Available)
                return;

            Debug.Log($"Object {gameObject.name} was clicked with button: {pointerEventData.button}");

            // Show the glow animation when the pointer enters the tile
            SetAnimatorEnabled(true);

            // Once the manual glow is disabled (because the animator takes control), we TRY to destroy the hint slot if it exists
            gameScript.SlotPosScript.DestroyBestMoveSlot();

            if (!isDragging)
            {
                if (!isClicked)
                {
                    isClicked = true;
                    lastSelectedDragHandler = this;
                    onDragHandlerSelected?.Invoke();

                    SoundManager.Instance.PlaySFX(IDAudioClip.selectPiece);

                    if (gameScript.currentDrag != null)
                        gameScript.currentDrag.GetComponent<DragHandler>().SelectTile();

                    gameScript.currentDrag = _dominoView;
                    _slotDictionary = new Dictionary<RectTransform, float>(_slots.Count);
                    _startPosition = _dragObject.anchoredPosition;

                    ShowSelectedTileFeedback(true);
                    await (CreateSlot?.Invoke() ?? default);
                    //SetDraggedPosition(eventData);   
                }
                else
                {
                    isClicked = false;
                    ShowSelectedTileFeedback(false);

                    if (lastSelectedDragHandler == this)
                    { 
                        gameScript.currentDrag = null;
                        StartCoroutine(LerpMove(CheckTheNearestSlot(), isAI: false, onFinishDrag: OnFinishDrag));
                    }
                }
            }
        }


        private void OnDragHandlerSelected()
        {
            if (lastSelectedDragHandler == this)
                return;

            // Deselect the previous drag handler
            isClicked = false;
            ShowSelectedTileFeedback(false);
        }

        // -> IBeginDragHandler, IDragHandler, IEndDragHandler all this for that functions <- //
        public async void OnBeginDragProxy(BaseEventData eventData)
        {
            if (eventData is not PointerEventData pointerEventData)
                return;

            if (!_currentButton.interactable)
                return;
            if (!_dominoView.GetDomino().Available)
                return;

            isClicked = false;
            isDragging = true;
            ShowSelectedTileFeedback(false);

            SoundManager.Instance.PlaySFX(IDAudioClip.selectPiece);

            if (gameScript.currentDrag != null)
                gameScript.currentDrag.GetComponent<DragHandler>().SelectTile();

            gameScript.currentDrag = _dominoView;
            _slotDictionary = new Dictionary<RectTransform, float>(_slots.Count);
            _startPosition = _dragObject.anchoredPosition;
            
            SetDraggedPosition(pointerEventData);
             await (CreateSlot?.Invoke() ?? default);
        }

        public void OnDragProxy(BaseEventData eventData)
        {
            if (eventData is not PointerEventData pointerEventData)
                return;

            if (!_currentButton.interactable)
                return;
            if (!_dominoView.GetDomino().Available)
                return;

            SetDraggedPosition(pointerEventData);
        }

        public void OnEndDragProxy(BaseEventData eventData)
        {
            if (eventData is not PointerEventData pointerEventData)
                return;

            if (!_currentButton.interactable)
                return;
            if (!_dominoView.GetDomino().Available)
                return;

            gameScript.currentDrag = null;

            StartCoroutine(LerpMove
                (CheckTheNearestSlot(), 
                isAI: false, 
                notifyToHost: gameScript.GameTypeSelectedID != ProDomino.Shared.GameType.singlePlayerIA, 
                onFinishDrag: OnFinishDrag));

            isDragging = false;
        }

        private UniTask OnFinishDrag()
        {
            // Once the tile is dropped, we need to update the game state
            gameScript.DeckController.ControlAllHands();

            analyticsManager?.SendAnalytic(AnalyticType.PlaceTilesOnBoard, 1);
            return UniTask.CompletedTask;
        }

        private void SetDraggedPosition(PointerEventData data)
        {
            // Function for setting this pos to mouse pos
            Vector3 globalMousePos;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_dragObject, data.position,
                    data.pressEventCamera,
                    out globalMousePos))
            {
                _dragObject.position = globalMousePos;
            }
        }

        public void SelectTile()
        {
            _dragObject.anchoredPosition = _startPosition;
            ShowSelectedTileFeedback(false);
        }
        
        /// <summary>
        /// Resets the drag state, returns the dragged object to its original position, removes all slots from the game
        /// script, and clears the current drag reference.
        /// </summary>
        public void DiscardTile()
        {
            isClicked = false;
            isDragging = false;

            _dragObject.anchoredPosition = _startPosition;
            ShowSelectedTileFeedback(false);
        }

        /// <summary>
        /// Displays or hides visual feedback for the selected tile, optionally disabling the glow animation.
        /// </summary>
        /// <param name="show">True to show the selected tile feedback; false to hide it.</param>
        /// <param name="isDisablingGlowAnimator">True to disable the glow animation when showing feedback; otherwise, false.</param>
        public void ShowSelectedTileFeedback(bool show, bool isDisablingGlowAnimator = false)
        {
            // Disable the glow animation when showing the feedback to avoid conflicts between the animation and the static feedback state
            if (isDisablingGlowAnimator)
                SetAnimatorEnabled(false);

            // Set the glow image color to white when showing feedback, or clear when hiding feedback
            if (_glowImage)
                _glowImage.color = show ? Color.white : Color.clear;

            // Set the feedback object active state based on the show parameter
            if (_selectedTileFeedbackObj)
                _selectedTileFeedbackObj.SetActive(show);
        }

        /// <summary>
        /// Enables or disables the glow animator component.
        /// </summary>
        /// <param name="enabled">True to enable the animator; false to disable it.</param>
        public void SetAnimatorEnabled(bool enabled)
        {
            if (glowAnimator)
                glowAnimator.enabled = enabled;
        }

        public IEnumerator LerpMove
            (RectTransform nearestSlot,
            bool isAI = false, // This is to check if the timeout wil lbe checked or not
            bool notifyToHost = false,
            bool isNotificationFromHost = false,
            AsyncActionHandler onFinishDrag = null)
        {
            if (_isTimeout != null)
            {
                if (!isAI && _isTimeout())
                { 
                    Debug.LogWarning("[Drag Handler] The player is timeout. Play will not done. The tile will wait until the correspondly manager handles the situation");
                    yield break;
                }
            }
            else
                Debug.LogError("[Drag Handler] Couldn't check if the turn is not timeout because func reference is null");

            //isNotificationFromHost = false;

            Debug.Log("++-- 03 LerpMove: notifyHost=" + notifyToHost + ", isNotificationFromHost=" + isNotificationFromHost);

            bool isHostNotification = notifyToHost || isNotificationFromHost; //Whether the notification must be communicated to the host or received from the host
            var originPosition = _dragObject.position;

            currentTime = 0;
            normalizedValue = 0;
            changeParent = false;

            ShowSelectedTileFeedback(false);
            _dominoView.ChangeBackState(false);

            // Pre-reset the RectTransformPanZoomController
            rectTransformPanZoomController.StopCoroutines();

            // This will drag to slot|
            var hasNearestSlotOrClicked = new Func<bool>(() => (gameScript.GetInfoSlots() && nearestSlot != null) || isClicked);
            if (hasNearestSlotOrClicked())
            {
                if (nearestSlot)
                { 
                    _dragObject.sizeDelta = nearestSlot.sizeDelta;
                    _dragObject.rotation = nearestSlot.rotation;
                }

                // Move the drag object to the target canvas (change parents preserving world position)
                if (rectTransformPanZoomController is not null and { TargetRect: not null })
                {
                    _dragObject.DeepParenting(rectTransformPanZoomController.TargetRect);
                    yield return new WaitForEndOfFrame();
                }
                else
                    Debug.LogWarning($"Reference missing: {nameof(rectTransformPanZoomController)} or {nameof(rectTransformPanZoomController.TargetRect)}");

            }

            // If there is a nearest slot or the tile was clicked, start the lerp movement
            if (nearestSlot)
            {
                Debug.Log("Testing nearest slot");
                // Called when the tile is starting to move
                string auxSideInfo = nearestSlot.GetComponent<EmptySlot>().sideInfo;
                if (notifyToHost && ((gameScript.GetInfoSlots() && nearestSlot != null) || isClicked))
                {
                    gameScript.NotifyPlayerStartsMovement_ToHost(_dominoView, auxSideInfo);
                    Debug.Log("*** Slot Name: " + nearestSlot.GetComponent<EmptySlot>().sideInfo);
                }

                var tileTargetPosition = new Func<Vector2>(() => nearestSlot?.position ?? _dragObject.position);

                // Wait until the end of the frame to ensure the position is updated or, if the tile was clicked, wait until it's at the target position
                while (currentTime < timeOfTravel
                    || (isClicked && Mathf.Abs(_dragObject.position.sqrMagnitude - tileTargetPosition().sqrMagnitude) > 0.01f))
                {
                    currentTime += Time.deltaTime;
                    normalizedValue = currentTime / timeOfTravel;

                    if (hasNearestSlotOrClicked())
                    {
                        _dominoView.LockTile();
                        _dragObject.position = Vector3.Lerp(
                            _dragObject.position, 
                            tileTargetPosition(), 
                            normalizedValue);

                        yield return new WaitForEndOfFrame();

                        var distanceToSlot = Vector2.Distance(_dragObject.position, tileTargetPosition());
                        if (distanceToSlot <= .1f)
                        {
                            isClicked = false;
                            gameScript.currentDrag = null;
                            _dragObject.position = tileTargetPosition();

                            _dominoView.OnTable();

                            gameScript.ConfirmSlotOccupation(ref nearestSlot, _dominoView);

                            SoundManager.Instance.PlaySFX(IDAudioClip.placePiece);

                            // Invoke the callback if provided
                            if (onFinishDrag is not null)
                                yield return onFinishDrag().ToCoroutine();

                            gameScript.FinishTurn(ref nearestSlot, _dominoView.GetDomino(), isHostNotification, wasUsed: true);
                            gameScript.RemoveAllSlots();

                            // Wait until the slots are destroyed
                            yield return new WaitForSeconds(.1f);

                            // Configure the RectTransformPanZoomController with the new rect
                            rectTransformPanZoomController.UpdateTilesBounds(true, true);

                            // Wait a moment to ensure the bounds are updated
                            yield return new WaitForSeconds(.15f);

                            // Unblock the pan zoom after dragging
                            rectTransformPanZoomController.SetPanZoomBlockStatus(false);

                            // If client is informing, proceed to inform the host that the movement ends 
                            if (notifyToHost)
                                gameScript.NotifyPlayerEndsMovement_ToHost(_dominoView, auxSideInfo);

                            yield break;
                        }
                    }

                    yield return null;
                }

                // Once the tile is dropped, we need to update the game state
                gameScript.UpdateDominoPositionsAccordingRecords_Proxy();
            }

            // But if the slot is null or the drag was not completed, return to hand
            yield return ReturnToHand();

            // Once the tile is dropped, we need to update the game state
            gameScript.UpdateDominoPositionsAccordingRecords_Proxy();

            // Return to hand if the drag was not completed
            IEnumerator ReturnToHand()
            {
                // Deselect the tile
                isClicked = false;

                // Reset the drag object position to the start position and the gameScript current drag to null
                gameScript.currentDrag = null;

                var timeElapsed = 0f;
                while (Vector2.Distance(_dragObject.anchoredPosition, _startPosition) > 0.1f && timeElapsed < 1)
                {
                    currentTime += Time.deltaTime;
                    normalizedValue = currentTime / timeOfTravel;

                    // Lerp the position back to the start position
                    _dragObject.anchoredPosition = Vector3.Lerp(_dragObject.anchoredPosition, _startPosition, normalizedValue);
                    yield return new WaitForEndOfFrame();

                    timeElapsed += Time.deltaTime;
                }

                // Refresh parent layout groups to automatically recalculate gameobjects positions (return to hand)
                _dragObject.transform.parent.RefreshLayoutGroupsImmediateAndRecursive();

                // Delete every slot
                gameScript.RemoveAllSlots();

                // Wait until the slots are destroyed
                yield return new WaitForSeconds(.1f);

                // Configure the RectTransformPanZoomController with the new rect
                rectTransformPanZoomController.UpdateTilesBounds(true, true);

                // Wait a moment to ensure the bounds are updated
                yield return new WaitForSeconds(.25f);

                // Unblock the pan zoom after dragging
                rectTransformPanZoomController.SetPanZoomBlockStatus(false);
            }
        }
        
        public void PPlaceInBoard (Vector3 position, Quaternion rotation, Vector2 sizeDelta)
        {
            _dominoView.ChangeBackState(false);
            _dominoView.OnTable();

            _dragObject.sizeDelta = sizeDelta;
            _dragObject.rotation = rotation;

            _dragObject.position = position;
        }

        public IEnumerator PlaceInBoard
            (Vector3 position, Quaternion rotation, Vector2 sizeDelta)
        {
            // Pre-reset the RectTransformPanZoomController

            ShowSelectedTileFeedback(false);
            _dominoView.ChangeBackState(false);
            _dominoView.OnTable();

            rectTransformPanZoomController.StopCoroutines();
            
            _dragObject.sizeDelta = sizeDelta;
            _dragObject.rotation = rotation;

            // Move the drag object to the target canvas (change parents preserving world position)
            if (rectTransformPanZoomController is not null and { TargetRect: not null })
            {
                Debug.Log("PPParent 01");
                _dragObject.DeepParenting(rectTransformPanZoomController.TargetRect);
                yield return new WaitForEndOfFrame();
            }
            else
            {
                Debug.Log("PPParent 02");
                Debug.LogWarning($"RRRReference missing: {nameof(rectTransformPanZoomController)} or {nameof(rectTransformPanZoomController.TargetRect)}");
            }

            _dragObject.position = position;
            yield return new WaitForEndOfFrame();

            //gameScript.ConfirmSlotOccupation(ref nearestSlot, _dominoView.GetDomino(), isHostNotification, wasUsed: true);
            //gameScript.RemoveAllSlots();

            // Wait until the slots are destroyed
            yield return new WaitForSeconds(.1f);

            // Configure the RectTransformPanZoomController with the new rect
            //rectTransformPanZoomController.UpdateTilesBounds(true, true);

            // Wait a moment to ensure the bounds are updated
            yield return new WaitForSeconds(.15f);

            // Unblock the pan zoom after dragging
            rectTransformPanZoomController.SetPanZoomBlockStatus(false);

            yield return null;
        }
        
        #region Replay Mode
        public IEnumerator PlacingInstant
            (RectTransform nearestSlot,
            bool notifyToHost = false,
            bool isNotificationFromHost = false,
            AsyncActionHandler onFinishDrag = null)
        {
            //isNotificationFromHost = false;

            Debug.Log("++-- 03 LerpMove: notifyHost=" + notifyToHost + ", isNotificationFromHost=" + isNotificationFromHost);

            bool isHostNotification = notifyToHost || isNotificationFromHost; //Whether the notification must be communicated to the host or received from the host
            var originPosition = _dragObject.position;

            currentTime = 0;
            normalizedValue = 0;
            changeParent = false;

            ShowSelectedTileFeedback(false);
            _dominoView.ChangeBackState(false);

            // Pre-reset the RectTransformPanZoomController
            rectTransformPanZoomController.StopCoroutines();

            // This will drag to slot|
            var hasNearestSlotOrClicked = new Func<bool>(() => (gameScript.GetInfoSlots() && nearestSlot != null) || isClicked);
            if (hasNearestSlotOrClicked())
            {
                _dragObject.sizeDelta = nearestSlot.sizeDelta;
                _dragObject.rotation = nearestSlot.rotation;

                // Move the drag object to the target canvas (change parents preserving world position)
                if (rectTransformPanZoomController is not null and { TargetRect: not null })
                {
                    _dragObject.DeepParenting(rectTransformPanZoomController.TargetRect);
                    yield return new WaitForEndOfFrame();
                }
                else
                    Debug.LogWarning($"Reference missing: {nameof(rectTransformPanZoomController)} or {nameof(rectTransformPanZoomController.TargetRect)}");
            }

            // If there is a nearest slot or the tile was clicked, start the lerp movement
            if (nearestSlot)
            {
                // Called when the tile is starting to move
                string auxSideInfo = nearestSlot.GetComponent<EmptySlot>().sideInfo;
                if (notifyToHost && ((gameScript.GetInfoSlots() && nearestSlot != null) || isClicked))
                    gameScript.NotifyPlayerStartsMovement_ToHost(_dominoView, auxSideInfo);

                var tileTargetPosition = nearestSlot?.position ?? _dragObject.position;

                // Wait until the end of the frame to ensure the position is updated or, if the tile was clicked, wait until it's at the target position
                while (currentTime < timeOfTravel
                    || (isClicked && Mathf.Abs(_dragObject.position.sqrMagnitude - tileTargetPosition.sqrMagnitude) > 0.01f))
                {
                    currentTime += Time.deltaTime;
                    normalizedValue = currentTime / timeOfTravel;

                    if (hasNearestSlotOrClicked())
                    {
                        _dragObject.position = tileTargetPosition;
                        _dominoView.LockTile();

                        yield return new WaitForEndOfFrame();

                        var distanceToSlot = Vector2.Distance(_dragObject.position, tileTargetPosition);
                        if (distanceToSlot <= .1f)
                        {
                            isClicked = false;
                            gameScript.currentDrag = null;
                            _dragObject.position = tileTargetPosition;

                            _dominoView.OnTable();

                            // Confirm the slot occupation in the game script
                            gameScript.ConfirmSlotOccupation(ref nearestSlot, _dominoView);

                            // Invoke the callback if provided
                            if (onFinishDrag is not null)
                                yield return onFinishDrag().ToCoroutine();

                            gameScript.RemoveAllSlots();

                            // Wait until the slots are destroyed
                            yield return new WaitForSeconds(.1f);

                            // Configure the RectTransformPanZoomController with the new rect
                            rectTransformPanZoomController.UpdateTilesBounds(true, true);

                            // Wait a moment to ensure the bounds are updated
                            yield return new WaitForSeconds(.15f);

                            // Unblock the pan zoom after dragging
                            rectTransformPanZoomController.SetPanZoomBlockStatus(false);

                            // If client is informing, proceed to inform the host that the movement ends 
                            if (notifyToHost)
                                gameScript.NotifyPlayerEndsMovement_ToHost(_dominoView, auxSideInfo);

                            yield break;
                        }
                    }

                    yield return null;
                }

                // Once the tile is dropped, we need to update the game state
                gameScript.UpdateDominoPositionsAccordingRecords_Proxy();
            }

            // But if the slot is null or the drag was not completed, return to hand
            yield return ReturnToHand();

            // Once the tile is dropped, we need to update the game state
            gameScript.UpdateDominoPositionsAccordingRecords_Proxy();

            // Return to hand if the drag was not completed
            IEnumerator ReturnToHand()
            {
                // Deselect the tile
                isClicked = false;

                // Reset the drag object position to the start position and the gameScript current drag to null
                gameScript.currentDrag = null;

                var timeElapsed = 0f;
                while (Vector2.Distance(_dragObject.anchoredPosition, _startPosition) > 0.1f && timeElapsed < 1)
                {
                    currentTime += Time.deltaTime;
                    normalizedValue = currentTime / timeOfTravel;

                    // Lerp the position back to the start position
                    _dragObject.anchoredPosition = Vector3.Lerp(_dragObject.anchoredPosition, _startPosition, normalizedValue);
                    yield return new WaitForEndOfFrame();

                    timeElapsed += Time.deltaTime;
                }

                // Refresh parent layout groups to automatically recalculate gameobjects positions (return to hand)
                _dragObject.transform.parent.RefreshLayoutGroupsImmediateAndRecursive();

                // Delete every slot
                gameScript.RemoveAllSlots();

                // Wait until the slots are destroyed
                yield return new WaitForSeconds(.1f);

                // Configure the RectTransformPanZoomController with the new rect
                rectTransformPanZoomController.UpdateTilesBounds(true, true);

                // Wait a moment to ensure the bounds are updated
                yield return new WaitForSeconds(.25f);

                // Unblock the pan zoom after dragging
                rectTransformPanZoomController.SetPanZoomBlockStatus(false);
            }
        }
        #endregion

        private RectTransform CheckTheNearestSlot()
        {
            _slotDictionary.Clear();

            RectTransform result = null;

            // Iterate for each slot and check the distance to the drag object
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null && !_slotDictionary.ContainsKey(_slots[i]))
                {
                    var distance = Vector2.Distance(_dragObject.position, _slots[i].position);

                    // Check if the slot is clicked or if the distance is less than the selectTileDistance when dragging
                    if (isClicked || (isDragging && distance <= selectTileDistance))
                        _slotDictionary.Add(_slots[i], Vector2.Distance(_dragObject.position, _slots[i].position));
                }

            var ordered = _slotDictionary.OrderBy(x => x.Value);

            // Find the nearest slot
            foreach (var value in ordered)
                if (result == null)
                {
                    result = value.Key;
                    break;
                }

            return result;
        }


        private RectTransform _nextParent;
        private bool _standing;
        private bool _onAI;

        public void SendToNextHand(RectTransform nextHand, bool stand, bool onAI, bool instantMove = false)
        {
            _nextParent = nextHand;
            _standing = stand;
            _onAI = onAI;

            if(!instantMove)
            {
                StartCoroutine(FlyToHand());
                SoundManager.Instance.PlaySFX(IDAudioClip.shuffle);
            }
            else
            {
                InstantToHand();
            }
        }

        private void InstantToHand()
        {
            currentTime = 0;
            normalizedValue = 0;
            changeParent = false;

            Vector2 targetSize = _nextParent.rect.size;
            Vector3 targetScale = Vector3.one;

            Vector3 targetWorldPosition = _nextParent.position;

            // If parent is a layout group, use a dummy to compute the future position
            LayoutGroup layoutGroup = _nextParent.GetComponent<LayoutGroup>();
            GameObject dummy = null;

            if (layoutGroup != null)
            {
                dummy = Instantiate(_invisibleDominoPrefab, _nextParent);
                dummy.name = "[DummySlot]";
                dummy.transform.localScale = Vector3.one;

                var dummyRT = dummy.GetComponent<RectTransform>();
                dummyRT.sizeDelta = _dragObject.sizeDelta;

                // Make dummy invisible without deactivating
                if (dummy.TryGetComponent(out CanvasGroup cg))
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }

                // Force layout and wait 1 frame to ensure positioning is complete
                LayoutRebuilder.ForceRebuildLayoutImmediate(_nextParent);

                // Capture final position after layout updates
                targetWorldPosition = dummyRT.position;

                if (dummy != null)
                    Destroy(dummy);
            }

            _dragObject.position = targetWorldPosition;
            _dragObject.sizeDelta = targetSize;
            _dragObject.localScale = targetScale;

            _dragObject.SetParent(_nextParent, false); // Change parent without preserving world transform
            _dragObject.localRotation = Quaternion.identity; // Force local rotation to zero

            if (_onAI)
                _dominoView.OnAIHands();

            gameScript.transform.RefreshContentSizeFitterImmediateAndRecursive(this);
            gameScript.transform.RefreshLayoutGroupsImmediateAndRecursive();

            if (dummy != null)
                Destroy(dummy);
        }

        private IEnumerator FlyToHand()
        {
            float duration = 1f;
            currentTime = 0;
            normalizedValue = 0;
            changeParent = false;

            // Save initial transform state
            Vector3 startPosition = _dragObject.position;
            Vector2 startSize = _dragObject.sizeDelta;
            Vector3 startScale = _dragObject.localScale;
            Quaternion startRotation = _dragObject.rotation;

            Vector2 targetSize = _nextParent.rect.size;
            Vector3 targetScale = Vector3.one;
            Quaternion targetRotation = _nextParent.rotation;

            Vector3 targetWorldPosition = _nextParent.position;

            // If parent is a layout group, use a dummy to compute the future position
            LayoutGroup layoutGroup = _nextParent.GetComponent<LayoutGroup>();
            GameObject dummy = null;

            if (layoutGroup != null)
            {
                dummy = Instantiate(_invisibleDominoPrefab, _nextParent);
                dummy.name = "[DummySlot]";
                dummy.transform.localScale = Vector3.one;

                var dummyRT = dummy.GetComponent<RectTransform>();
                dummyRT.sizeDelta = _dragObject.sizeDelta;

                // Make dummy invisible without deactivating
                if (dummy.TryGetComponent(out CanvasGroup cg))
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }

                // Force layout and wait 1 frame to ensure positioning is complete
                LayoutRebuilder.ForceRebuildLayoutImmediate(_nextParent);
                yield return null;

                // Capture final position after layout updates
                targetWorldPosition = dummyRT.position;
                targetRotation = dummyRT.rotation;

                if (dummy != null)
                    Destroy(dummy);
            }

            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                normalizedValue = Mathf.Clamp01(currentTime / duration);

                _dragObject.position = Vector3.Lerp(startPosition, targetWorldPosition, normalizedValue * moveSpeed);
                _dragObject.sizeDelta = Vector2.Lerp(startSize, targetSize, normalizedValue * moveSpeed);
                _dragObject.localScale = Vector3.Lerp(startScale, targetScale, normalizedValue * moveSpeed);
                _dragObject.rotation = Quaternion.Lerp(startRotation, targetRotation, normalizedValue * rotationSpeed);

                if (Vector3.Distance(_dragObject.position, targetWorldPosition) <= 1f)
                {
                    _dragObject.position = targetWorldPosition;
                    _dragObject.sizeDelta = targetSize;
                    _dragObject.localScale = targetScale;

                    _dragObject.SetParent(_nextParent, false); // Change parent without preserving world transform
                    _dragObject.localRotation = Quaternion.identity; // Force local rotation to zero

                    if (_onAI)
                        _dominoView.OnAIHands();

                    gameScript.transform.RefreshContentSizeFitterImmediateAndRecursive(this);
                    gameScript.transform.RefreshLayoutGroupsImmediateAndRecursive();

                    yield break;
                }

                yield return null;
            }

            if (dummy != null)
                Destroy(dummy);
        }

        public void SetToParent(RectTransform nextHand)
        {

            Vector2 targetSize = nextHand.rect.size;
            Vector3 targetScale = Vector3.one;

            _dragObject.sizeDelta = targetSize;
            _dragObject.localScale = targetScale;

            _dragObject.SetParent(nextHand, false);
            _dragObject.localRotation = Quaternion.identity; // Force local rotation to zero
        }
    }
}