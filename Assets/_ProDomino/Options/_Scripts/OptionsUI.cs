using ProDomino.AccountSystem;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.CustomizationSystem;
using ProDomino.FriendSystem;
using ProDomino.GameSystem;
using System;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Options
{
    public class OptionsUI : MonoBehaviour
    {
        [SerializeField] private Image profileImage;
        [SerializeField] private Button openAuthInterfaceButton;
        [SerializeField] private Button sendEmailVerificationButton;
        [SerializeField] private Button signOutButton;
        [SerializeField] private Button profileMenuButton;

        [SerializeField] private CustomButtonUI openFriendListPopUp;
        [SerializeField] private CustomButtonUI openCustomizationPopUp;
        [SerializeField] private CustomButtonUI openAccountPopUp;

        [SerializeField] private CanvasGroup optionsInterface;
        [SerializeField] private CanvasGroup notLoginButtonInterface;
        [SerializeField] private CanvasGroup loginButtonInterface;
        [SerializeField]
        private TMP_Text
            usernameLabel,
            uuidLabel;
        [SerializeField] private CustomizationController customizationController;
        [SerializeField] private AccountDataController accountDataController;
        [SerializeField] private PartyController friendListController;

        [SerializeField] private Image profileAlertIcon;
        [SerializeField] private Image sendEmailAlertIcon;
        [SerializeField] private CanvasGroup alertEmailVerificationPopup_cg;

        private AuthManager authManager;
        private AnalyticsManager analyticsManager;
        private GameManager gameManager;
        private PromptFadeController promptFadeController;

        public bool IsOptionInterfaceActive => optionsInterface?.alpha > 0;

        private void Start()
        {
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            if (!authManager || !analyticsManager || !gameManager || !promptFadeController)
            {
                Debug.LogError("One or more required services are not initialized.");
                return;
            }

            openAuthInterfaceButton?.onClick.AddListener(OnPressAuthButton);
            profileMenuButton?.onClick.AddListener(OnPressProfileButton);
            openFriendListPopUp?.onClick.AddListener(OnPressOpenFriendListPopUpButton);
            openCustomizationPopUp?.onClick.AddListener(OnPressOpenCustomizationPopUpButton);
            openAccountPopUp?.onClick.AddListener(OnPressOpenAccountPopUpButton);
            sendEmailVerificationButton?.onClick.AddListener(OnPressSendEmailVerificationButton);
            signOutButton?.onClick.AddListener(OnPressSignOutButton);

            // Set the initial state of the profile menu toggle
            if (profileMenuButton && optionsInterface)
                SetActiveInterface(optionsInterface, false);

            // Subscribe to analytics events
            analyticsManager.Subscribe(UpdatePlayerIcon);

            gameManager?.HandleOnSignIn(Configure);
            gameManager?.HandleOnSignIn(ValidateAndNotifyUnverifiedEmailAccounts);

            authManager?.HandleOnCheckEmailVerification(Configure);
            authManager?.HandleOnCheckEmailVerification(ValidateAndNotifyUnverifiedEmailAccounts);

            gameManager?.HandleOnSignOut(ResetShowEmailVerificationAlertProperty);

            // Configure the UI based on the current authentication state
            Configure();
        }

        private void OnDestroy()
        {
            gameManager?.UnHandleOnSignIn(Configure);
            gameManager?.UnHandleOnSignIn(ValidateAndNotifyUnverifiedEmailAccounts);

            authManager?.UnHandleOnCheckEmailVerification(Configure);
            authManager?.UnHandleOnCheckEmailVerification(ValidateAndNotifyUnverifiedEmailAccounts);

            gameManager?.UnHandleOnSignOut(ResetShowEmailVerificationAlertProperty);

            analyticsManager?.Unsubscribe(UpdatePlayerIcon);
        }

        private void Configure(bool isSuccessfully) => Configure();
        private void ValidateAndNotifyUnverifiedEmailAccounts(bool isSuccessfully) => ValidateAndNotifyUnverifiedEmailAccounts();

        private void Configure()
        {
            SetButtonInteractivity(openFriendListPopUp);
            SetButtonInteractivity(openCustomizationPopUp);
            SetButtonInteractivity(openAccountPopUp);

            if (!gameManager.IsAuthenticated)
            {
                Debug.Log("The player is not authenticated yet");
                DetermineLoggedInterface(false);
                SetActiveInterface(optionsInterface, false);
                return;
            }

            if (!string.IsNullOrEmpty(authManager.Username))
                usernameLabel.text = authManager.Username;
            else
                Debug.Log("Username is null or empty.");
            
            if (!string.IsNullOrEmpty(authManager.UUID))
                uuidLabel.text = authManager.UUID;
            else
                Debug.Log("UUID is null or empty.");

            if (sendEmailVerificationButton)
            { 
                var isUserAuthWithCredentials = authManager.IsUserAuthenticatedWithCredentials;
                var isEmailVerified = authManager.IsEmailVerified;
                var showShowEmailVerificationButton = isUserAuthWithCredentials && !isEmailVerified;

                sendEmailVerificationButton.interactable = showShowEmailVerificationButton;
                sendEmailVerificationButton.gameObject.SetActive(showShowEmailVerificationButton);
            }

            if (profileImage)
                profileImage.sprite = gameManager.ProfilePicture;

            DetermineLoggedInterface(true);
            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        private void ValidateAndNotifyUnverifiedEmailAccounts()
        {
            if (!gameManager.IsAuthenticated)
            { 
                Debug.LogWarning("The player is not authenticated with email and password nor with provider.");
                return;
            }

            bool isUserAuthWithCredentials = authManager.IsUserAuthenticatedWithCredentials;
            bool isEmailVerified = authManager.IsEmailVerified;
            bool showEmailVerificationPopup = isUserAuthWithCredentials && !isEmailVerified;
            ActiveEmailVerificationAlerts(showEmailVerificationPopup);
        }

        private void ResetShowEmailVerificationAlertProperty() => showEmailVerificationAlert = true;

        private bool showEmailVerificationAlert = true;
        private void ActiveEmailVerificationAlerts(bool active)
        {
            profileAlertIcon.enabled = active;
            sendEmailAlertIcon.enabled = active;

            alertEmailVerificationPopup_cg.SetActive(showEmailVerificationAlert && active);

            if (showEmailVerificationAlert)
                showEmailVerificationAlert = false;
        }

        private void DetermineLoggedInterface(bool isLogin)
        {
            if (notLoginButtonInterface is null || loginButtonInterface is null)
            {
                Debug.LogError("NotLoginInterface or LoginInterface is not assigned.");
                return;
            }
            SetActiveInterface(loginButtonInterface, isLogin);
            SetActiveInterface(notLoginButtonInterface, !isLogin);
        }

        private void SetActiveInterface(CanvasGroup canvasGroup, bool isActive)
        {
            canvasGroup.SetActive(isActive);
        }

        private void TurnOffPopUps()
        { 
            customizationController.SetVisibility(false);
            friendListController.SetVisibility(false);
            accountDataController.SetVisibility(false);
        }

        private void UpdatePlayerIcon(AnalyticType type, object data)
        {
            if (type is not AnalyticType.ObtainCosmetic and not AnalyticType.SelectCosmetic)
                return;

            if (profileImage)
                profileImage.sprite = gameManager.ProfilePicture;
            else
                Debug.LogWarning("Profile image is not assigned in OptionsUI.");
        }

        private void SetButtonInteractivity(CustomButtonUI button)
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
        private void OnPressAuthButton()
        {
            if (authManager is null)
            {
                Debug.LogError("AuthManager is not initialized.");
                return;
            }

            // Open the Auth menu
            authManager.SetActiveAuthUI(true);
        }
        
        private void OnPressProfileButton()
        {
            if (!gameManager.IsAuthenticated)
            {
                Debug.LogWarning("[OnPressProfileButton] The player is not authenticated yet");
                Configure();
                return;
            }

            // Toggle the profile menu
            SetActiveInterface(optionsInterface, optionsInterface.alpha is 0);
        }
        
        private void OnPressOpenFriendListPopUpButton()
        {
            if (!friendListController)
            {
                Debug.LogError("friendListController; is not assigned.");
                return;
            }

            // Toggle the profile menu
            SetActiveInterface(optionsInterface, false);
            TurnOffPopUps();

            // Show the customization controller
            friendListController.SetVisibility(true);
        }
        
        private void OnPressOpenCustomizationPopUpButton()
        {
            if (!customizationController)
            {
                Debug.LogError("CustomizationController is not assigned.");
                return;
            }

            // Toggle the profile menu
            SetActiveInterface(optionsInterface, false);
            TurnOffPopUps();

            // Show the customization controller
            customizationController.SetVisibility(true);
        }
        
        private void OnPressOpenAccountPopUpButton()
        {
            if (!accountDataController)
            {
                Debug.LogError("AccountDataController is not assigned.");
                return;
            }

            // Toggle the profile menu
            SetActiveInterface(optionsInterface, false);
            TurnOffPopUps();

            // Show the account controller
            accountDataController.SetVisibility(true);
        }

        private async void OnPressSendEmailVerificationButton()
        {
            if (!gameManager.IsAuthenticated)
            {
                Debug.LogWarning("[OnPressSendEmailVerificationButton] The player is not authenticated yet");
                Configure();
                return;
            }

            // Check if the email is already verified
            if (authManager.IsEmailVerified || authManager.IsUserAuthenticatedWithProvider)
            {
                Debug.LogWarning("The email is already verified or the user is authenticated with a provider.");
                return;
            }

            SetActiveInterface(optionsInterface, false);

            var promptMessage = "";
            try
            {
                // Try to send the email verification
                var emailWhoSentVerifcation = await authManager.SendEmailVerification();

                // If the response contains the email, show success message
                if (!string.IsNullOrEmpty(emailWhoSentVerifcation))
                    promptMessage = $"Email verification sent to <b>{emailWhoSentVerifcation}</b>";
                else
                    promptMessage = "Failed to send email verification. Try again later.";
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to send email verification: " + ex.Message);
                promptMessage = "An error occurred while sending email verification. Please try again later.";
            }
            finally
            {
                // Show the prompt message and configure the UI again
                promptFadeController?.Fade(promptMessage);
                Configure();
            }
        }
        
        private async void OnPressSignOutButton()
        {
            if (!gameManager.IsAuthenticated)
            {
                Debug.LogWarning("[OnPressSignOutButton] The player is not authenticated yet");
                Configure();
                return;
            }

            SetActiveInterface(optionsInterface, false);

            // Sign out the player
            await authManager.SignOut();
            Configure();
        }
    }
}
