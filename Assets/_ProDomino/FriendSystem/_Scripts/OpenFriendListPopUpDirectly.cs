using ProDomino.GameSystem;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Handles the direct opening of the friend list popup and manages button interactivity based on authentication
    /// status.
    /// </summary>
    [RequireComponent(typeof(CustomButtonUI))]
    public class OpenFriendListPopUpDirectly : MonoBehaviour
    {
        [SerializeField] private PartyController friendListController;
        private GameManager gameManager;
        private CustomButtonUI button;

        private void Awake()
        {
            button = GetComponent<CustomButtonUI>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();

            gameManager.HandleOnSignIn(OnSignIn);
            gameManager.HandleOnSignOut(OnSignOut);

            if (button)
                button.onClick.AddListener(OnPressOpenFriendListPopUpButton);
            else
                Debug.LogError("Button component is missing on OpenFriendListPopUpDirectly script.");
        }

        private void Start()
        {
            SetButtonInteractivity();
        }

        /// <summary>
        /// Configures the button's interactivity and tooltip raycast target based on the authentication and
        /// initialization status of the game manager.
        /// </summary>
        private void SetButtonInteractivity()
        {
            if (!button)
            {
                Debug.LogError("Button component is missing on OpenFriendListPopUpDirectly script.");
                return;
            }

            // Enable or disable the button based on authentication and initialization status
            if (gameManager is not null)
            {
                var shouldButtonBeInteractable = gameManager.IsAuthenticatedAndVerified;
                button.SetButtonInteractable(shouldButtonBeInteractable);

                // Also manage the raycast target of the tooltip image if it exists
                if (button.TooltipContainer && button.TooltipContainer.TryGetComponent<Image>(out var image))
                    image.raycastTarget = !shouldButtonBeInteractable;
                else
                    Debug.LogWarning("TooltipContainer or Image component is missing on the button.");
            }
        }

        /// <summary>
        /// Opens the friend list popup by setting its visibility to true.
        /// </summary>
        private void OnPressOpenFriendListPopUpButton()
        {
            if (!friendListController)
            {
                Debug.LogError("friendListController is not assigned.");
                return;
            }

            // Show the customization controller
            friendListController.SetVisibility(true);
        }

        /// <summary>
        /// Updates the interactivity state of the sign-in button.
        /// </summary>
        private void OnSignIn()
        {
            SetButtonInteractivity();
        }

        /// <summary>
        /// Updates button interactivity after a sign-out event.
        /// </summary>
        private void OnSignOut()
        {
            SetButtonInteractivity();
        }
    }
}
