using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// UI component for handling Google authentication interactions, including login button events and login result
    /// callbacks.
    /// </summary>
    internal partial class AuthUI
    {
        [SerializeField, Header("Google Auth")] private Button GoogleLoginButton;
        [SerializeField] private UnityEvent<bool> onGoogleLogin;

        /// <summary>
        /// Direct call to the Google login event
        /// </summary>
        /// <param name="wasLoginSuccessfully"></param>
        internal void CallOnGoogleLoginEvent(bool wasLoginSuccessfully) => onGoogleLogin?.Invoke(wasLoginSuccessfully);

        #region Handlers - Google
        /// <summary>
        /// Registers a listener for the Google login button's click event.
        /// </summary>
        /// <param name="onPressGoogleLoginButton">The action to invoke when the Google login button is pressed.</param>
        internal void HandleOnPressGoogleLoginButton(UnityAction onPressGoogleLoginButton)
        {
            if (GoogleLoginButton)
                GoogleLoginButton.onClick.AddListener(onPressGoogleLoginButton);
        }

        /// <summary>
        /// Removes the specified listener from the Google login button's onClick event.
        /// </summary>
        /// <param name="onPressGoogleLoginButton">The UnityAction to remove from the Google login button's onClick event.</param>
        internal void UnHandleOnPressGoogleLoginButton(UnityAction onPressGoogleLoginButton)
        {
            if (GoogleLoginButton)
                GoogleLoginButton.onClick.RemoveListener(onPressGoogleLoginButton);
        }

        /// <summary>
        /// Registers a callback to be invoked when a Google login event occurs.
        /// </summary>
        /// <param name="onGoogleLogin">The callback to execute when the Google login event is triggered.</param>
        internal void HandleOnGoogleLogin(UnityAction<bool> onGoogleLogin)
        { 
            if (this.onGoogleLogin is not null && onGoogleLogin != null)
                this.onGoogleLogin.AddListener(onGoogleLogin);
        }

        /// <summary>
        /// Unsubscribes the specified callback from the Google login event.
        /// </summary>
        /// <param name="onGoogleLogin">The callback to remove from the Google login event listeners.</param>
        internal void UnHandleOnGoogleLogin(UnityAction<bool> onGoogleLogin)
        {
            if (this.onGoogleLogin is not null && onGoogleLogin != null)
                this.onGoogleLogin.RemoveListener(onGoogleLogin);
        }
        #endregion
    }
}
