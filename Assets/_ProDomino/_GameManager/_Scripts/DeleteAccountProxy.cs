using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.GameSystem
{
    [RequireComponent(typeof(Button))]
    public class DeleteAccountProxy : MonoBehaviour
    {
        private GameManager gameManager;
        private DeleteAccountController deleteAccountController;
        private DeleteAccountController DeleteAccountController => deleteAccountController = deleteAccountController != null 
            ? deleteAccountController 
            : FindFirstObjectByType<DeleteAccountController>();
        private Button button;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            button = GetComponent<Button>();
            button.onClick.AddListener(OnPressDeleteButton);

            if (button)
                button.interactable = false;
            else
                Debug.LogError("DeleteAccountProxy: button reference is missing");

            if (gameManager)
            {
                gameManager.HandleOnSignIn(OnSignedIn);
                gameManager.HandleOnSignOut(OnSignedOut);
            } 
            else
                Debug.LogError("DeleteAccountProxy: GameManager reference is missing");
        }

        private void OnPressDeleteButton()
        {
            // Get the refence once
            var deleteAccountController = DeleteAccountController;
            if (!deleteAccountController)
            {
                Debug.LogWarning($"DeleteAccountProxy: Missing reference: {nameof(deleteAccountController)}");
                return;
            }

            deleteAccountController.ShowDeleteAccountPopUp();
        }

        private void OnSignedIn()
        {
            if (!button)
            {
                Debug.LogError($"DeleteAccountProxy: Button reference is missing");
                return;
            }

            button.interactable = gameManager.IsAuthenticated;
        }
        private void OnSignedOut()
        {
            if (!button)
            {
                Debug.LogError($"DeleteAccountProxy: Button reference is missing");
                return;
            }

            button.interactable = false;
        }
    }
}
