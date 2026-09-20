using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.Shared;
using System;
using System.Runtime.InteropServices;
using Timba.Patterns;
using TMPro;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static HelperSharedLibrary.AccountDeletionResponse;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// Frontend controller responsible for orchestrating the user-facing
    /// account deletion flow.
    ///
    /// Responsibilities:
    /// - Collect explicit user confirmation (email input)
    /// - Trigger Cloud Code deletion request
    /// - Handle encrypted backend responses
    /// - Provide user feedback (success or failure)
    /// - Clean up local state and force a hard reload
    ///
    /// This controller intentionally contains NO business logic.
    /// All validation and irreversible decisions are delegated to the backend.
    /// </summary>
    public class DeleteAccountController : MonoBehaviour
    {
        [SerializeField] private float timerToDeleteAccount = 10;
        [SerializeField] private bool isValidatingEmail;

        // UI references
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_InputField confirmationInputfield;
        [SerializeField] private Button confirmButton, cancelButton;
        [SerializeField] private TMP_Text confirmButtonLabel;

        // Service dependencies
        private GameManager gameManager;
        private AuthManager authManager;
        private PromptFadeController promptFadeController;
        private double timeElapse;
        private string defaultConfirmButtonText;

        private const string localize_tablet = "DeleteAccountTable";

        // Lazy-loaded Cloud Code bindings
        private BackendBindings _module;
        private BackendBindings module =>
            _module ??= CloudCodeService.Instance is not null
                ? new BackendBindings(CloudCodeService.Instance)
                : null;

        // JS interop for WebGL hard reload
        // This bypasses Unity scene state entirely
        [DllImport("__Internal")]
        private static extern void ReloadPage();

        /// <summary>
        /// Unity lifecycle method.
        /// Resolves service dependencies and binds UI events.
        /// </summary>
        private async void Awake()
        {
            // Resolve global services using service locator
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            // Bind delete action to confirm button
            if (confirmButton)
                confirmButton.onClick.AddListener(() => DeleteAccount().Forget());
            else
                Debug.LogError("DeleteAccountController: Confirm button reference is missing");

            // Validate required services
            if (!gameManager || !authManager || !promptFadeController)
                Debug.LogError("DeleteAccountController: services references are missing");

            // Store default confirm button text to later
            if (confirmButtonLabel)
            {
                defaultConfirmButtonText = await LocalizationHelper.Get("confirm_delete_button", localize_tablet);

                if (string.IsNullOrEmpty(defaultConfirmButtonText))
                    defaultConfirmButtonText = confirmButtonLabel.text;
            }
            else
                Debug.LogError("DeleteAccountController: confirmButtonLabel reference are missing");
        }

        /// <summary>
        /// Unity update loop.
        /// Enables or disables the confirm button depending on user input.
        /// </summary>
        private void Update()
        {
            if (!confirmationInputfield || !confirmButton)
                return;

            // Each frame the popUp is not closed, decrease the timer elapse value
            if (canvasGroup is not null and { alpha: not 0 } && timeElapse is not 0)
                timeElapse = Math.Clamp(timeElapse - Time.deltaTime, 0, double.MaxValue);

            // Modify the timer label
            if (confirmButtonLabel)
            {
                var isTimerVisible = timeElapse is not 0;
                confirmButtonLabel.text = $"{defaultConfirmButtonText}{(!isTimerVisible ? string.Empty : $" ({Mathf.RoundToInt((float)timeElapse)})")}";
            }

            // Prevent empty confirmation submissions
            confirmButton.interactable =
                (!isValidatingEmail || !string.IsNullOrEmpty(confirmationInputfield.text))
                && timeElapse <= 0;
        }

        /// <summary>
        /// Entry point triggered by the user when confirming account deletion.
        /// This method:
        /// - Encrypts confirmation data
        /// - Calls Cloud Code backend
        /// - Handles success or failure responses
        /// - Performs local cleanup and reload on success
        ///
        /// Uses UniTaskVoid because this is a UI-triggered fire-and-forget action.
        /// </summary>
        private async UniTaskVoid DeleteAccount()
        {
            if (!confirmationInputfield)
            {
                Debug.LogError("DeleteAccountController: confirm inputfield missing");
                return;
            }

            // Disable UI interaction to avoid double submission
            DetermineBlockInteractivity(false);

            // Encrypt confirmation email before sending to backend
            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["confirmationEmail"] = confirmationInputfield.text
            });

            // Call Cloud Code delete account function
            // All retries and logic are handled by the backend
            var encryptedResponse = await gameManager.HandleProcess_GameManagerProxy(
                uniTask: () => module.DeleteAccount(dataEncrypted).AsUniTask(),
                taskId: nameof(module.DeleteAccount),
                showLoading: true,
                shouldIgnoreTryAgainProcess: false);

            // Transport-level failure
            if (string.IsNullOrEmpty(encryptedResponse))
            {
                Debug.LogError("DeleteAccountController: Empty response from backend");
                ShowFeedbackToClient("Failed to delete account. Try again later");
                return;
            }

            // Decrypt and deserialize backend response
            var accountDeletionResponse =
                authManager.DeserializeAndDecryptData<AccountDeletionResponse>(encryptedResponse);

            if (accountDeletionResponse is null)
            {
                Debug.LogError("DeleteAccountController: Failed to deserialize response");
                ShowFeedbackToClient("A problem occurred. Try again later");
                return;
            }

            // Backend explicitly denied deletion
            if (accountDeletionResponse.reason is not DenyDeletionReason.None ||
                !string.IsNullOrEmpty(accountDeletionResponse.message))
            {
                Debug.Log(
                    $"DeleteAccountController: Backend denied deletion. " +
                    $"Reason={accountDeletionResponse.reason}, " +
                    $"Message={accountDeletionResponse.message}");

                ShowFeedbackToClient(accountDeletionResponse.message);
                return;
            }

            // At this point deletion is confirmed successful
            // Sign out before clearing local data
            await gameManager.HandleProcess_GameManagerProxy(
                uniTask: () => authManager.SignOut(true, true, true, true),
                taskId: $"{nameof(authManager.SignOut)}_DeletingAccount",
                showLoading: true,
                shouldIgnoreTryAgainProcess: false);

            ShowFeedbackToClient("Account was deleted successfully");

            // Clear all local persisted data
            PlayerPrefs.DeleteAll();

            // Force full reload to prevent zombie sessions
            HardReload();

            #region Nested Helpers

            /// <summary>
            /// Displays feedback to the user and restores UI interaction.
            /// </summary>
            void ShowFeedbackToClient(string message)
            {
                if (!promptFadeController)
                {
                    Debug.LogError(
                        "DeleteAccountController: Cannot show feedback, prompt reference missing");
                    return;
                }

                DetermineBlockInteractivity(true);
                HideDeleteAccountPopUp();
                promptFadeController.Fade(message, 4);
            }

            /// <summary>
            /// Enables or disables user interaction for confirmation controls.
            /// Used to prevent multiple submissions.
            /// </summary>
            void DetermineBlockInteractivity(bool isInteractable)
            {
                if (!confirmButton || !cancelButton)
                {
                    Debug.LogError("DeleteAccountController: Buttons references are missing");
                    return;
                }

                confirmButton.interactable = isInteractable;
                cancelButton.interactable = isInteractable;
            }

            #endregion
        }

        /// <summary>
        /// Shows the delete account confirmation popup.
        /// Resets the confirmation input field.
        /// </summary>
        public void ShowDeleteAccountPopUp()
        {
            if (!canvasGroup)
            {
                Debug.LogError("DeleteAccountController: Canvas group reference is missing");
                return;
            }

            if (confirmationInputfield)
                confirmationInputfield.text = string.Empty;
            else
                Debug.LogError("DeleteAccountController: confirm inputfield missing");

            // Reset the timer needed to make available the confirm button
            timeElapse = timerToDeleteAccount;
            canvasGroup.SetActive(true);
        }

        /// <summary>
        /// Hides the delete account confirmation popup.
        /// Clears sensitive input.
        /// </summary>
        public void HideDeleteAccountPopUp()
        {
            if (!canvasGroup)
            {
                Debug.LogError("DeleteAccountController: Canvas group reference is missing");
                return;
            }

            if (confirmationInputfield)
                confirmationInputfield.text = string.Empty;
            else
                Debug.LogError("DeleteAccountController: confirm inputfield missing");

            // Reset the timer needed to make available the confirm button
            timeElapse = timerToDeleteAccount;
            canvasGroup.SetActive(false);
        }

        /// <summary>
        /// Forces a full application reload.
        /// In WebGL, uses a JavaScript reload.
        /// In other platforms, reloads the active scene.
        /// </summary>
        public void HardReload()
        {
            if (!GameManager.IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning(
                    "DeleteAccountController: JS reload not supported on this platform. Reloading scene.");

                var activeScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
                return;
            }

            ReloadPage();
        }
    }
}
