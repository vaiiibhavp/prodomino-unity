using ProDomino.HandleProcessesSystem;
using ProDomino.Shared;
using System;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Displays a pop-up for Google authentication errors and manages its visibility and localized error messages.
    /// </summary>
    public class AuthProviderErrorPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Button denyButton;

        private HandleProcessesController handleProcessesController;
        private bool isPurchaseInProgress = false;

        private void Awake()
        {
            handleProcessesController = ServiceLocator.Instance.GetService<HandleProcessesController>();

            // Assign button listeners
            if (denyButton)
                denyButton.onClick.AddListener(OnConfirmPressed);
            else
                Debug.LogWarning("Deny Button is not assigned.");
        }

        /// <summary>
        /// Sets the active state of the pop-up.
        /// </summary>
        /// <param name="isActive">Whether the pop-up should be active or not.</param>
        /// <param name="googleAuthErrorType">The type of Google authentication error, if applicable.</param>
        internal async void SetActive(bool isActive, GoogleAuthErrorType? googleAuthErrorType = null)
        {
            // Ensure the GameObject is active
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // Check for null CanvasGroup
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }

            // Configure the description label with localized text
            if (descriptionLabel && isActive)
            {
                var localizationKey = googleAuthErrorType switch
                {
                    GoogleAuthErrorType.AccountExistsWithDifferentProvider => Consts.LocalizationKeys.GoogleAccountExistWithDifferentProvider,
                    GoogleAuthErrorType.AccountExistsWithDifferentCredential => Consts.LocalizationKeys.GoogleAccountExistWithDifferentCredential,
                    GoogleAuthErrorType.DuplicatedAccount => Consts.LocalizationKeys.GoogleDuplicatedAccount,
                    _ => throw new NotImplementedException(),
                };

                try
                {
                    // Localize the text of the button label
                    var localizedText = await LocalizationHelper.Get(localizationKey);
                    ConfigureDescriptionLabel(localizedText);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error fetching localized text: " + ex);
                }
            }

            canvasGroup.SetActive(isActive);
        }

        /// <summary>
        /// Configures the description label with localized text.
        /// </summary>
        /// <param name="localizedText">The localized text to display in the description label.</param>
        private void ConfigureDescriptionLabel(string localizedText)
        {
            // Check for null references
            if (descriptionLabel is null)
            {
                Debug.LogError($"{nameof(AuthProviderErrorPopUp)}: {nameof(descriptionLabel)} is null");
                return;
            }

            // Check for null or empty localized text
            if (string.IsNullOrEmpty(localizedText))
            {
                Debug.LogError($"{nameof(AuthProviderErrorPopUp)}: localizedText is null or empty");
                return;
            }

            // Set the button label text
            descriptionLabel.text = localizedText;
        }

        /// <summary>
        /// Handles the deny button click event.
        /// </summary>
        private void OnConfirmPressed()
        {
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }

            canvasGroup.SetActive(false);
        }

        /// <summary>
        /// Represents error types encountered during Google authentication.
        /// </summary>
        internal enum GoogleAuthErrorType
        {
            AccountExistsWithDifferentProvider,
            AccountExistsWithDifferentCredential,
            DuplicatedAccount
        }
    }
}
