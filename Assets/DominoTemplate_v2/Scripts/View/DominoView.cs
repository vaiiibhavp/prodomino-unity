using DominoTemplate.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DominoTemplate.View
{
    public class DominoView : MonoBehaviour
    {
        [SerializeField] private Image _dominoImage = null;
        [SerializeField] private Image _backImage;
        [SerializeField] private Button _currentButton = null;
        [SerializeField] private Domino _currentDomino = null;
        [SerializeField] private RectTransform _dominoTransform = null;

        private bool _playerMove = false;
        private bool _onTable;

        /// <summary>
        /// Indicates whether the object is currently on the table.
        /// </summary>
        /// <returns>True if the object is on the table; otherwise, false.</returns>
        public bool IsOnTable() => _onTable;

        /// <summary>
        /// Marks the current tile as placed on the table, disables its associated button, sets it as unavailable, and
        /// updates its visual appearance.
        /// </summary>
        public void OnTable()
        {
            // Disable the button and mark the tile as unavailable
            SetButtonInteractivity(false);

            // Mark the tile as unavailable
            if (_currentDomino is not null)
                _currentDomino.Available = false;

            // Mark the tile as on the table
            _onTable = true;

            // Alterate the alpha of the tile
            if (TryGetComponent(out CanvasGroup cg))
                cg.alpha = 1;
        }

        public void RemoveTable()
        {
            _onTable = false;
        }

        public void ChangeBackState(bool state)
        {
            if (_backImage)
                _backImage.gameObject.SetActive(state);
            else
                Debug.LogWarning("Back image not assigned in DominoView.");
        }

        public void OnAIHands()
        {
            //_dominoTransform.localScale = new Vector3(0.5f, 0.5f, 0);
            _currentDomino.Available = false;
        }

        public void UnLockTile(bool isPlayer)
        {
            // Enable the button and mark the tile as available if it's not on the table, otherwise keep it disabled and unavailable
            SetButtonInteractivity(_onTable ? false : isPlayer);

            _currentDomino.Available = _onTable ? false : isPlayer;

            // Alterate the alpha of the tile
            if (TryGetComponent(out CanvasGroup cg))
                cg.alpha = 1;
        }

        /// <summary>
        /// Locks the tile by disabling its button, marking it as unavailable, and setting its alpha to fully opaque if
        /// not already on the table.
        /// </summary>
        public void LockTile()
        {
            if (!_onTable)
            {
                // Disable the button and mark the tile as unavailable
                SetButtonInteractivity(false);

                _currentDomino.Available = false;
            }

            // Alterate the alpha of the tile
            if (TryGetComponent(out CanvasGroup cg))
                cg.alpha = 1;
        }


        public Domino GetDomino()
        {
            return _currentDomino;
        }

        public void SetDomino(Domino domino, Sprite dominoSprite, Sprite backSprite)
        {
            _currentDomino = domino;
            OverrideSkin(dominoSprite, backSprite);
        }

        public void OverrideSkin(Sprite dominoSprite, Sprite backSprite)
        {
            _dominoImage.sprite = dominoSprite;

            if (_backImage)
                _backImage.sprite = backSprite;
        }

        public void SetDefaultTile(Sprite dominoSprite)
        {
            _currentDomino.id = -1;
            _currentDomino.TopIndex = 0;
            _currentDomino.BottomIndex = 0;
            _dominoImage.sprite = dominoSprite;
        }

        private void UpdateState()
        {
            SetButtonInteractivity(_playerMove && _currentDomino.Available);
        }

        public void StartPosition()
        {
            _dominoTransform.anchoredPosition = Vector2.zero;
            _dominoTransform.localScale = Vector3.one;
            _dominoTransform.sizeDelta = new Vector2(60f, 120f);
        }

        public void UpdateRotation()
        {
            _dominoTransform.localRotation =
                _currentDomino.PortraitOrientation ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 0, 90f);
        }

        public void DisableInConcentrataGameMode(float? optionalAlfa = null)
        {
            // Disable the button and mark the tile as unavailable
            SetButtonInteractivity(false);

            // If there is an alfa value, set it
            if (optionalAlfa.HasValue && TryGetComponent(out CanvasGroup cg))
                cg.alpha = optionalAlfa.Value;
        }

        // Public method to force a visual state update
        public void SetButtonInteractivity(bool isInteractable)
        {
            if (_currentButton)
            { 
                _currentButton.interactable = isInteractable;
                _currentButton.animator.SetTrigger(isInteractable ? "Normal" :"Disabled");
            }
        }
    }
}