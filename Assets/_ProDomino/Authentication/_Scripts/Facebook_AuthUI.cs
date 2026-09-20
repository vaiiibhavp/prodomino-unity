using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Provides UI functionality for Facebook authentication, including handling login button interactions and login
    /// event callbacks.
    /// </summary>
    internal partial class AuthUI
    {
        [SerializeField, Header("Facebook Auth")] private Button facebookLoginButton;
        [SerializeField] private UnityEvent<bool> onFacebookLogin;

        /// <summary>
        /// Invokes the Facebook login event with the specified login result.
        /// </summary>
        /// <param name="wasLoginSuccessfully">Indicates whether the Facebook login was successful.</param>
        internal void CallOnFacebookLoginEvent(bool wasLoginSuccessfully) => onFacebookLogin?.Invoke(wasLoginSuccessfully);

        #region Handlers - Facebook
        /// <summary>
        /// Registers a callback to be invoked when the Facebook login button is pressed.
        /// </summary>
        /// <param name="onPressFacebookLoginButton">The action to execute when the Facebook login button is clicked.</param>
        internal void HandleOnPressFacebookLoginButton(UnityAction onPressFacebookLoginButton)
        {
            if (facebookLoginButton)
                facebookLoginButton.onClick.AddListener(onPressFacebookLoginButton);
        }

        /// <summary>
        /// Removes the specified listener from the Facebook login button's onClick event.
        /// </summary>
        /// <param name="onPressFacebookLoginButton">The UnityAction to remove from the Facebook login button's onClick event.</param>
        internal void UnHandleOnPressFacebookLoginButton(UnityAction onPressFacebookLoginButton)
        {
            if (facebookLoginButton)
                facebookLoginButton.onClick.RemoveListener(onPressFacebookLoginButton);
        }

        /// <summary>
        /// Registers a callback to be invoked when a Facebook login event occurs.
        /// </summary>
        /// <param name="onFacebookLogin">The callback to execute when the Facebook login event is triggered.</param>
        internal void HandleOnFacebookLogin(UnityAction<bool> onFacebookLogin)
        { 
            if (this.onFacebookLogin is not null && onFacebookLogin != null)
                this.onFacebookLogin.AddListener(onFacebookLogin);
        }

        /// <summary>
        /// Removes the specified listener from the Facebook login event handler.
        /// </summary>
        /// <param name="onFacebookLogin">The UnityAction<bool> delegate to remove from the Facebook login event.</param>
        internal void UnHandleOnFacebookLogin(UnityAction<bool> onFacebookLogin)
        {
            if (this.onFacebookLogin is not null && onFacebookLogin != null)
                this.onFacebookLogin.RemoveListener(onFacebookLogin);
        }
        #endregion
    }
}
