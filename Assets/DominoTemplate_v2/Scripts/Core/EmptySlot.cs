using Cysharp.Threading.Tasks;
using DominoTemplate.Controllers;
using DominoTemplate.DragAndDrop;
using ProDomino.AnalyticsSystem;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace DominoTemplate.Core
{
    /// <summary>
    /// Represents an interactive empty slot in the UI that handles pointer click events, manages its visual state, and
    /// coordinates drag-and-drop operations within the game.
    /// </summary>
    public class EmptySlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform _ownTransform = null;
        [SerializeField] private Image _ownImage = null;
        [HideInInspector] public GameControler gameScript;
        public string sideInfo = "none";
        public bool arePointerEventsBlocked;

        private static AnalyticsManager analyticsManager;

        private void Awake()
        {
            analyticsManager = analyticsManager != null 
                ? analyticsManager 
                : ServiceLocator.Instance.GetService<AnalyticsManager>();
        }

        /// <summary>
        /// Gets the RectTransform associated with this instance.
        /// </summary>
        /// <returns>The RectTransform of this object.</returns>
        public RectTransform GetOwnRectTransform()
        {
            return _ownTransform;
        }

        /// <summary>
        /// Makes the object's image fully transparent.
        /// </summary>
        public void BecomeInvisible()
        {
            _ownImage.color = Color.clear;
        }

        /// <summary>
        /// Blocks pointer events by setting the internal flag to true.
        /// </summary>
        public void BlockPointerEvents()
        {
            arePointerEventsBlocked = true;
        }

        /// <summary>
        /// Handles pointer click events by initiating a drag operation if pointer events are not blocked and a drag is
        /// in progress.
        /// </summary>
        /// <param name="eventData">Pointer event data associated with the click.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            // Avoid click events when the pointer events are blocked (e.g., during an animation or when the slot is not interactable)
            if (arePointerEventsBlocked)
                return;

            Debug.Log($"Object {gameObject.name} was clicked with button: {eventData.button}");

            if (gameScript != null && gameScript.currentDrag != null)
            {
                // Beacause the empty slot could be delete meanwhile the the coroutine still alive, is better to confer that autority to another monobehaviour
                gameScript.StartCoroutine(gameScript.currentDrag.GetComponent<DragHandler>().LerpMove
                    (_ownTransform, 
                    isAI: false, 
                    notifyToHost: gameScript.GameTypeSelectedID != ProDomino.Shared.GameType.singlePlayerIA,
                    onFinishDrag: OnFinishDrag));
            }
        }

        /// <summary>
        /// Handles logic to execute when a drag operation finishes, updating hand controls and sending analytics if
        /// pointer events are not blocked.
        /// </summary>
        /// <returns>A completed UniTask representing the asynchronous operation.</returns>
        private UniTask OnFinishDrag()
        {
            // Avoid click events when the pointer events are blocked (e.g., during an animation or when the slot is not interactable)
            if (arePointerEventsBlocked)
                return UniTask.CompletedTask;

            gameScript.DeckController.ControlAllHands();
            analyticsManager?.SendAnalytic(AnalyticType.PlaceTilesOnBoard, 1);
            return UniTask.CompletedTask;
        }
    }
}