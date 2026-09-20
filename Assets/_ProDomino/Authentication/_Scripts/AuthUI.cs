using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Manages the authentication user interface, including initialization, activation, credential handling, and event
    /// invocation for sign-in and sign-out actions.
    /// </summary>
    internal partial class AuthUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup authUICanvasGroup;
        [SerializeField] private Button closeAuthUIButton;
        [SerializeField] private UnityEvent<bool> onSignIn;
        [SerializeField] private UnityEvent<bool> onSignOut;

        /// <summary>
        /// Initializes the authentication UI by setting up the close button listener and credentials.
        /// </summary>
        public void Awake_AuthUI()
        {
            closeAuthUIButton?.onClick.AddListener(OnPressCloseButton);
            OnAwake_Crendentials();
        }

        /// <summary>
        /// Initializes authentication UI by starting credential handling, deactivating the UI, and clearing sign-in
        /// denial feedback.
        /// </summary>
        public void Start_AuthUI()
        {
            OnStart_Credentials();
            SetActive(false);

            signInDenyFeedback.text = string.Empty;
        }

        /// <summary>
        /// Updates the authentication user interface by invoking credential update logic.
        /// </summary>
        public void Update_AuthUI()
        {
            OnUpdate_Credentials();
        }

        /// <summary>
        /// Cleans up authentication UI resources by destroying credentials.
        /// </summary>
        public void OnDestroy_AuthUI()
        {
            OnDestroy_Credentials();
        }

        /// <summary>
        /// Sets the active state of the entire Auth UI
        /// </summary>
        /// <param name="isActive"></param>
        internal void SetActive(bool isActive)
        {
            SetActive(authUICanvasGroup, isActive);
            SetActive_Credentials(isActive);

            // To avoid preserving input data when the Auth UI is closed
            ClearCredentialsInputFields();
        }

        /// <summary>
        /// Sets the active state of a specific CanvasGroup
        /// </summary>
        /// <param name="canvasGroup">The CanvasGroup to set the active state for.</param>
        /// <param name="isActive">Whether the CanvasGroup should be active or not.</param>
        internal void SetActive(CanvasGroup canvasGroup, bool isActive)
        {
            if (canvasGroup == null)
            {
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }
            canvasGroup.SetActive(isActive);
        }

        /// <summary>
        /// Closes the sign-in panel and clears the input fields and feedback message.
        /// </summary>
        private void OnPressCloseButton()
        {
            SetActive(false);

            signInCredentialsInputField.text = string.Empty;
            signInPasswordInputField.text = string.Empty;
            signInDenyFeedback.text = string.Empty;
        }

        /// <summary>
        /// Direct call to the SignIn event
        /// </summary>
        /// <param name="wasSignInSuccessfully">Indicates whether the sign-in was successful.</param>
        internal void CallOnSignInEvent(bool wasSignInSuccessfully) => onSignIn?.Invoke(wasSignInSuccessfully);
        
        /// <summary>
        /// Direct call to the SignOut event
        /// </summary>
        /// <param name="wasSignOutSuccessfully">Indicates whether the sign-out was successful.</param>
        internal void CallOnSignOutEvent(bool wasSignOutSuccessfully) => onSignOut?.Invoke(wasSignOutSuccessfully);

    }
}