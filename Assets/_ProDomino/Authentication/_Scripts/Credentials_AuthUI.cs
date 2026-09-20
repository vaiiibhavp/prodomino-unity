using System;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static HelperSharedLibrary.CredentialsValidator;
using static ProDomino.Authentication.AuthManager;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Manages the authentication user interface, including sign-in, sign-up, and password recovery screens, input
    /// fields, feedback messages, and related event handling.
    /// </summary>
    internal partial class AuthUI
    {
        [SerializeField, Header("Crendential Auth")] private Button openSignUpScreenButton;
        [SerializeField] private Button openRecoveryScreenButton;

        [SerializeField] private Button closeSignUpScreenButton;
        [SerializeField] private Button backSignUpScreenButton;

        [SerializeField] private Button closeRecoveryScreenButton;
        [SerializeField] private Button backRecoveryScreenButton;

        [SerializeField, Header("Sign In")] private CanvasGroup signInCanvasGroup;
        [SerializeField] private Button signInButton;
        [SerializeField] private Toggle rememberMeToggle;
        [SerializeField] private TMP_InputField signInCredentialsInputField;
        [SerializeField] private TMP_InputField signInPasswordInputField;
        [SerializeField] private TMP_Text signInDenyFeedback;

        [SerializeField, Header("Sign Up")] private CanvasGroup signUpCanvasGroup;
        [SerializeField] private Button signUpButton;
        [SerializeField] private Toggle termsAndConditionsToggle;
        [SerializeField] private Toggle dataTreatmentToggle;
        [SerializeField] private TMP_InputField signUpUserNameInputField;
        [SerializeField] private TMP_InputField signUpEmailInputField;
        [SerializeField] private TMP_InputField signUpPasswordInputField;
        [SerializeField] private TMP_InputField signUpConfirmPasswordInputField;
        [SerializeField] private TMP_Text 
            signUpUsernameDenyFeedback,
            signUpEmailDenyFeedback,
            signUpPasswordDenyFeedback,
            signUpConfirmPasswordDenyFeedback;

        [SerializeField, Header("Recovery")] private CanvasGroup recoveryCanvasGroup;
        [SerializeField] private TMP_InputField recoveryEmailInputField;
        [SerializeField] private Button recoveryButton;

        [SerializeField, Header("Crendential's Events")] private UnityEvent<bool> onCredentialsSignUp;
        [SerializeField] private UnityEvent<bool> onCredentialsSignIn;
        [SerializeField, Space(5)] private UnityEvent<string> onSignUpCredentialsValueChanged;
        [SerializeField] private UnityEvent<string> onSignInCredentialsValueChanged;
        [SerializeField, Space(5)] private UnityEvent<bool> onCheckEmailVerification;
        [SerializeField, Space(5)] private UnityEvent<bool> onRecoveryPassword;

        private PromptFadeController promptFadeController;
        private Func<bool> 
            isValidToSignIn, 
            isValidToSignUp;

        internal AuthUIActiveState CurrentAuthUIActiveState { get; private set; }

        internal string UserName_SignUp => signUpUserNameInputField?.text ?? string.Empty;
        internal string Email_SignUp => signUpEmailInputField?.text ?? string.Empty;
        internal string Password_SignUp => signUpPasswordInputField?.text ?? string.Empty;
        internal string ConfirmPassword_SignUp => signUpConfirmPasswordInputField?.text ?? string.Empty;
        internal bool WasTermsAndConditionsCheck_SignUp => termsAndConditionsToggle?.isOn ?? false;
        internal bool WasDataTreatmentCheck_SignUp => dataTreatmentToggle?.isOn ?? false;

        internal string Credentials_SignIn => signInCredentialsInputField?.text ?? string.Empty;
        internal string Password_SignIn => signInPasswordInputField?.text ?? string.Empty;

        internal string RecoveryEmail => recoveryEmailInputField?.text ?? string.Empty;

        /// <summary>
        /// Initializes input fields, toggles, and buttons for sign-up, sign-in, and recovery screens by attaching
        /// relevant event listeners.
        /// </summary>
        private void OnAwake_Crendentials()
        {
            signUpUserNameInputField?.onValueChanged.AddListener(OnSignUpValueChanged);
            signUpEmailInputField?.onValueChanged.AddListener(OnSignUpValueChanged);
            signUpPasswordInputField?.onValueChanged.AddListener(OnSignUpValueChanged);
            signUpConfirmPasswordInputField?.onValueChanged.AddListener(OnSignUpValueChanged);

            rememberMeToggle?.onValueChanged.AddListener(OnPressRememberMeToggle);
            signInCredentialsInputField?.onValueChanged.AddListener(OnSignInValueChanged);
            signInPasswordInputField?.onValueChanged.AddListener(OnSignInValueChanged);

            openSignUpScreenButton?.onClick.AddListener(OnOpenSignUpScreen);
            openRecoveryScreenButton?.onClick.AddListener(OnOpenRecoveryScreen);

            closeSignUpScreenButton?.onClick.AddListener(OnPressCloseButton);
            backSignUpScreenButton?.onClick.AddListener(OnOpenSignInScreen);

            closeRecoveryScreenButton?.onClick.AddListener(OnPressCloseButton);
            backRecoveryScreenButton?.onClick.AddListener(OnOpenSignInScreen);

            HandleOnRecoveryPassword(OnSentRecoveryPasswordMail);
        }

        /// <summary>
        /// Updates the 'Remember Me' preference and sets the toggle state without triggering listeners.
        /// </summary>
        /// <param name="isRemember">Indicates whether the 'Remember Me' option should be enabled.</param>
        private void OnPressRememberMeToggle(bool isRemember)
        {
            PlayerPrefs.SetInt("IsRememberPassword", isRemember ? 1 : 0);

            // Set the toggle state without notifying listeners to avoid recursive calls
            rememberMeToggle.SetIsOnWithoutNotify(isRemember);
        }

        /// <summary>
        /// Initializes the promptFadeController by retrieving it from the service locator.
        /// </summary>
        private void OnStart_Credentials()
        { 
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
        }

        /// <summary>
        /// Displays a prompt indicating whether the recovery password email was sent successfully.
        /// </summary>
        /// <param name="wasMailSentSuccessfully">True if the recovery email was sent successfully; otherwise, false.</param>
        private void OnSentRecoveryPasswordMail(bool wasMailSentSuccessfully)
        {
            if (promptFadeController)
                promptFadeController.Fade(wasMailSentSuccessfully 
                    ? $"Recovery mail was sent successfully to \"{RecoveryEmail}\""
                    : $"Failed to send recovery mail to \"{RecoveryEmail}\". Try again later"
                    , 3, .25f, 1);
        }

        /// <summary>
        /// Updates the interactable state of sign-in, sign-up, and recovery buttons based on credential validation and
        /// input.
        /// </summary>
        private void OnUpdate_Credentials()
        { 
            if (signInButton)
                signInButton.interactable = isValidToSignIn?.Invoke() ?? false;

            if (signUpButton)
                signUpButton.interactable = isValidToSignUp?.Invoke() ?? false;

            if (recoveryButton)
                recoveryButton.interactable = !string.IsNullOrEmpty(recoveryEmailInputField?.text);
        }

        /// <summary>
        /// Removes value change listeners from sign-up and sign-in input fields to clean up event subscriptions.
        /// </summary>
        private void OnDestroy_Credentials()
        {
            signUpUserNameInputField?.onValueChanged.RemoveListener(OnSignUpValueChanged);
            signUpEmailInputField?.onValueChanged.RemoveListener(OnSignUpValueChanged);
            signUpPasswordInputField?.onValueChanged.RemoveListener(OnSignUpValueChanged);
            signUpConfirmPasswordInputField?.onValueChanged.RemoveListener(OnSignUpValueChanged);

            signInCredentialsInputField?.onValueChanged.RemoveListener(OnSignInValueChanged);
            signInPasswordInputField?.onValueChanged.RemoveListener(OnSignInValueChanged);
        }

        /// <summary>
        /// Initializes validation delegates and resets input fields and feedback messages for sign-in and sign-up
        /// forms.
        /// </summary>
        /// <param name="isValidToSignIn">Delegate used to validate sign-in input.</param>
        /// <param name="isValidToSignUp">Delegate used to validate sign-up input.</param>
        internal void Initialize(Func<bool> isValidToSignIn, Func<bool> isValidToSignUp)
        {
            this.isValidToSignIn = isValidToSignIn;
            this.isValidToSignUp = isValidToSignUp;

            // Set the default values
            signInCredentialsInputField.text = string.Empty;
            signInPasswordInputField.text = string.Empty;

            signUpUserNameInputField.text = string.Empty;
            signUpEmailInputField.text = string.Empty;
            signUpPasswordInputField.text = string.Empty;
            signUpConfirmPasswordInputField.text = string.Empty;

            // Clear the feedback messages
            signInDenyFeedback.text = string.Empty;

            signUpUsernameDenyFeedback.text = string.Empty;
            signUpEmailDenyFeedback.text = string.Empty;
            signUpPasswordDenyFeedback.text = string.Empty;
            signUpConfirmPasswordDenyFeedback.text = string.Empty;
        }

        /// <summary>
        /// This method only sets the active state of the Sign-In credentials UI (but turns off the Sign-Up and Recovery UI).
        /// </summary>
        /// <param name="isActive">True to activate the Sign-In UI; false to deactivate it.</param>
        internal void SetActive_Credentials(bool isActive)
        {
            CurrentAuthUIActiveState = isActive ? AuthUIActiveState.SignIn : AuthUIActiveState.None;

            SetActiveCanvasGroup(signInCanvasGroup, isActive);
            SetActiveCanvasGroup(signUpCanvasGroup, false);
            SetActiveCanvasGroup(recoveryCanvasGroup, false);

            if (isActive)
            {
                // Refresh the layout groups immediately to ensure the UI is updated correctly
                transform.RefreshLayoutGroupsImmediateAndRecursive();

                // By default, if the AuthUI is open, then reset the remember me toggle to false
                OnPressRememberMeToggle(false);
            }
        }

        /// <summary>
        /// This method sets the active state of the Sign-In, Sign-Up and Recovery credentials UI.
        /// </summary>
        /// <param name="authUIActiveState">The desired active state for the credentials UI.</param>
        private void SetActive_Credentials(AuthUIActiveState authUIActiveState)
        {
            CurrentAuthUIActiveState = authUIActiveState;

            SetActiveCanvasGroup(signInCanvasGroup, CurrentAuthUIActiveState == AuthUIActiveState.SignIn);
            SetActiveCanvasGroup(signUpCanvasGroup, CurrentAuthUIActiveState == AuthUIActiveState.SignUp);
            SetActiveCanvasGroup(recoveryCanvasGroup, CurrentAuthUIActiveState == AuthUIActiveState.Recovery);

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Clears all the input fields related to credentials authentication
        /// </summary>
        private void ClearCredentialsInputFields()
        {
            signInCredentialsInputField.text = string.Empty;
            signInPasswordInputField.text = string.Empty;

            signUpUserNameInputField.text = string.Empty;
            signUpEmailInputField.text = string.Empty;
            signUpPasswordInputField.text = string.Empty;
            signUpConfirmPasswordInputField.text = string.Empty;

            recoveryEmailInputField.text = string.Empty;
        }

        /// <summary>
        /// Direct call to the onCredentialsSignIn event
        /// </summary>
        /// <param name="wasSignInSuccessfully">True if the sign-in was successful; otherwise, false.</param>
        internal void CallOnCredentialsSignInEvent(bool wasSignInSuccessfully) => onCredentialsSignIn?.Invoke(wasSignInSuccessfully);

        /// <summary>
        /// Direct call to the onCredentialsSignUp event
        /// </summary>
        /// <param name="wasSignUpSuccessfully">True if the sign-up was successful; otherwise, false.</param>
        internal void CallOnCredentialsSignUpEvent(bool wasSignUpSuccessfully) => onCredentialsSignUp?.Invoke(wasSignUpSuccessfully);

        /// <summary>
        /// Direct call to the onCheckEmailVerification event
        /// </summary>
        /// <param name="wasVerifiedSuccessfully">True if the email verification was successful; otherwise, false.</param>
        internal void CallOnCheckEmailVerificationEvent(bool wasVerifiedSuccessfully) => onCheckEmailVerification?.Invoke(wasVerifiedSuccessfully);

        /// <summary>
        /// Direct call to the onRecoveryPassword event
        /// </summary>
        /// <param name="wasRecoverySuccessfully">True if the password recovery was successful; otherwise, false.</param>
        internal void CallOnRecoveryPasswordEvent(bool wasRecoverySuccessfully) => onRecoveryPassword?.Invoke(wasRecoverySuccessfully);

        /// <summary>
        /// Clears feedback messages for sign-in or sign-up fields.
        /// </summary>
        /// <param name="isSignIn">True to clear sign-in feedback; false to clear sign-up feedback.</param>
        internal void FeedbackClear(bool isSignIn)
        {
            if (isSignIn)
            { 
                if (signInDenyFeedback)
                    signInDenyFeedback.text = string.Empty;
            } 
            else 
            {
                if (signUpUsernameDenyFeedback)
                    signUpUsernameDenyFeedback.text = string.Empty;

                if (signUpEmailDenyFeedback)
                    signUpEmailDenyFeedback.text = string.Empty;

                if (signUpPasswordDenyFeedback)
                    signUpPasswordDenyFeedback.text = string.Empty;

                if (signUpConfirmPasswordDenyFeedback)
                    signUpConfirmPasswordDenyFeedback.text = string.Empty;
            }
        }

        /// <summary>
        /// Generates and updates feedback messages based on the state of user credentials during sign-in or sign-up.
        /// </summary>
        /// <param name="isSignIn">Indicates whether the operation is sign-in or sign-up.</param>
        /// <param name="credentialStateModel">Tuple containing the credentials deny state and the input field type.</param>
        internal void FeedbackCredentialsState(bool isSignIn, (CredentialsDenyState state, InputfieldType inputFieldType) credentialStateModel)
        {
            var denyMessage = string.Empty;
            var (state, InputFieldType) = (credentialStateModel.state, credentialStateModel.inputFieldType);

            // Override the InputFieldType according the value of the credentials if the user is Sign-In
            if (isSignIn && InputFieldType is InputfieldType.Credentials)
                InputFieldType = IsCredentialUsername(Credentials_SignIn) 
                    ? InputfieldType.UserName 
                    : InputfieldType.Email;

            // Determine the target of the feedback message
            var target = InputFieldType.ToString();

            // TODO: Comented to save space in the feedback
            //// Check if the state is empty
            //if (state.HasFlag(CredentialsDenyState.IsEmpty))
            //    denyMessage += $"{target} is empty";

            // Check if the state contains too short
            if (state.HasFlag(CredentialsDenyState.IsTooShort))
            { 
                TryToAddLineJump();
                denyMessage += $"<b>*</b> {target} is too short";
            }

            // Check if the state contains too long
            if (state.HasFlag(CredentialsDenyState.IsTooLong))
            { 
                TryToAddLineJump();
                denyMessage += $"<b>*</b> {target} is too long";
            }

            // Check if the Username contains invalid characters
            if (InputFieldType is InputfieldType.UserName && state.HasFlag(CredentialsDenyState.IsContainingInvalidChars))
            {
                var invalidChars = (isSignIn ? Credentials_SignIn : UserName_SignUp)
                    ?.Where(x => !char.IsLetter(x) && !char.IsNumber(x) && !UsernameAllowedSymbols.Contains(x))
                    ?.Distinct()
                    ?.ToArray();

                var invalidCharsString = string.Join(", ", invalidChars);

                TryToAddLineJump();
                denyMessage += $"<b>*</b> {target} has invalid symbol: {invalidCharsString}";
            }

            if (InputFieldType is InputfieldType.Email)
            {
                // Check if the email doesn't has '@' to split between localPart and domain
                if (state.HasFlag(CredentialsDenyState.IsLeftingRequiredChars))
                { 
                    TryToAddLineJump();
                    denyMessage += $"<b>*</b> {target} must contains '@'";
                }

                // Check if the email local part is invalid
                if (state.HasFlag(CredentialsDenyState.IsNotUsingEmailStructure))
                { 
                    TryToAddLineJump();
                    var invalidLocalPart = (isSignIn ? Credentials_SignIn : Email_SignUp)
                        ?.Split('@')
                        ?.FirstOrDefault();

                    denyMessage += $"<b>*</b> {target} must use an valid email struture: [localPart]@[domain].[domainExtension]";
                }
                    
                // Check if the email domain is not one of the blocked domains
                if (state.HasFlag(CredentialsDenyState.IsNotValidDomain))
                { 
                    TryToAddLineJump();
                    var invalidDomain = (isSignIn ? Credentials_SignIn : Email_SignUp)
                        ?.Split('@')
                        ?.LastOrDefault();

                    denyMessage += $"<b>*</b> {target} must not use an invalid domain: {invalidDomain}";
                }
            }
                
            // Check if the Password contains the required characters
            if (InputFieldType is InputfieldType.Password && state.HasFlag(CredentialsDenyState.IsLeftingRequiredChars))
            {
                var passwordToUse = (isSignIn ? Password_SignIn : Password_SignUp);

                var hasLower = passwordToUse.Any(char.IsLower);
                var hasUpper = passwordToUse.Any(char.IsUpper);
                var hasNumber = passwordToUse.Any(char.IsNumber);
                var hasSymbol = passwordToUse.Any(char.IsSymbol);

                if (!hasLower)
                {
                    TryToAddLineJump();
                    denyMessage += $"<b>*</b> {target} must contains at least <b>1</b> lowercase letter";
                }
                if (!hasUpper)
                {
                    TryToAddLineJump();
                    denyMessage += $"<b>*</b> {target} must contains at least <b>1</b> uppercase letter";
                }
                if (!hasNumber) 
                {
                    TryToAddLineJump();
                    denyMessage += $"<b>*</b> {target} must contains at least <b>1</b> number";
                }
                if (!hasSymbol)
                {
                    TryToAddLineJump();
                    denyMessage += $"<b>*</b> {target} must contains at least <b>1</b> symbol";
                }
            }

            // Check if the Password is not matching
            if (InputFieldType is InputfieldType.ConfirmPassword && state.HasFlag(CredentialsDenyState.IsNotMatching))
            {
                TryToAddLineJump();
                denyMessage += $"<b>*</b> Password must match";
            }

            // If the feedback already exists, add a line jump
            var feedback = isSignIn 
                ? signInDenyFeedback 
                : InputFieldType switch {
                    InputfieldType.Email => signUpEmailDenyFeedback,
                    InputfieldType.Password => signUpPasswordDenyFeedback,
                    InputfieldType.ConfirmPassword => signUpConfirmPasswordDenyFeedback,
                    InputfieldType.UserName or _ => signUpUsernameDenyFeedback,
                };

            if (feedback.text is not "")
                TryToAddLineJump(true);

            // Once the deny message is set, set the feedback text
            if (!string.IsNullOrEmpty(denyMessage) && feedback)
                feedback.text += denyMessage;

            void TryToAddLineJump(bool isInsertingAtFirst = false)
            {
                if (!string.IsNullOrEmpty(denyMessage))
                {
                    if (isInsertingAtFirst)
                        denyMessage = "\n" + denyMessage;
                    else
                        denyMessage += "\n";
                }
            }
        }

        /// <summary>
        /// Sets the feedback message for either sign-in or sign-up and refreshes the layout.
        /// </summary>
        /// <param name="isSignIn">Indicates whether the feedback is for sign-in (true) or sign-up (false).</param>
        /// <param name="feedback">The feedback message to display.</param>
        internal void SetFeedbackDirectly(bool isSignIn, string feedback)
        {
            var feedbackLabel = isSignIn
                ? signInDenyFeedback
                : signUpUsernameDenyFeedback;

            if (feedbackLabel)
                feedbackLabel.text = feedback;

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Determines whether the provided credentials string is likely a username rather than an email address.
        /// </summary>
        /// <param name="credentials">The credentials string to evaluate.</param>
        /// <returns>True if the credentials string does not contain both '@' and '.' characters; otherwise, false.</returns>
        private bool IsCredentialUsername(string credentials)
        {
            return !credentials.Contains('@') || !credentials.Contains('.');
        }

        /// <summary>
        /// Sets the active state of the specified CanvasGroup.
        /// </summary>
        /// <param name="canvasGroup">The CanvasGroup to modify.</param>
        /// <param name="isActive">Whether the CanvasGroup should be active.</param>
        private void SetActiveCanvasGroup(CanvasGroup canvasGroup, bool isActive)
        {
            canvasGroup.SetActive(isActive);
        }

        #region Handlers - Credentials
        #region OnPressCredentialsSignInButton
        /// <summary>
        /// Registers a listener for the sign-in button's click event using the provided UnityAction.
        /// </summary>
        /// <param name="onPressCredentialsSignInButton">The action to invoke when the sign-in button is pressed.</param>
        internal void HandleOnPressCredentialsSignInButton(UnityAction onPressCredentialsSignInButton)
        {
            if (signInButton)
                signInButton.onClick.AddListener(onPressCredentialsSignInButton);
        }

        /// <summary>
        /// Removes the specified listener from the sign-in button's onClick event.
        /// </summary>
        /// <param name="onPressCredentialsSignInButton">The UnityAction to remove from the sign-in button's onClick event.</param>
        internal void UnHandleOnPressCredentialsSignInButton(UnityAction onPressCredentialsSignInButton)
        {
            if (signInButton)
                signInButton.onClick.RemoveListener(onPressCredentialsSignInButton);
        }
        #endregion

        #region OnPressCredentialsSignUpButton
        /// <summary>
        /// Registers a listener for the sign-up button's click event using the provided UnityAction.
        /// </summary>
        /// <param name="onPressCredentialsSignUpButton">The UnityAction to invoke when the sign-up button is pressed.</param>
        internal void HandleOnPressCredentialsSignUpButton(UnityAction onPressCredentialsSignUpButton)
        {
            if (signUpButton)
                signUpButton.onClick.AddListener(onPressCredentialsSignUpButton);
        }

        /// <summary>
        /// Removes the specified listener from the sign-up button's onClick event.
        /// </summary>
        /// <param name="onPressCredentialsSignUpButton">The UnityAction to remove from the sign-up button's onClick event.</param>
        internal void UnHandleOnPressCredentialsSignUpButton(UnityAction onPressCredentialsSignUpButton)
        {
            if (signUpButton)
                signUpButton.onClick.RemoveListener(onPressCredentialsSignUpButton);
        }
        #endregion

        #region OnPressCredentialsRecoveryButton
        /// <summary>
        /// Adds the specified action as a listener to the recovery button's click event.
        /// </summary>
        /// <param name="onPressCredentialsRecoveryButton">The action to invoke when the recovery button is pressed.</param>
        internal void HandleOnPressCredentialsRecoveryButton(UnityAction onPressCredentialsRecoveryButton)
        {
            if (recoveryButton)
                recoveryButton.onClick.AddListener(onPressCredentialsRecoveryButton);
        }

        /// <summary>
        /// Removes the specified listener from the credentials recovery button's onClick event.
        /// </summary>
        /// <param name="onPressCredentialsRecoveryButton">The UnityAction to remove from the recovery button's onClick event.</param>
        internal void UnHandleOnPressCredentialsRecoveryButton(UnityAction onPressCredentialsRecoveryButton)
        {
            if (recoveryButton)
                recoveryButton.onClick.RemoveListener(onPressCredentialsRecoveryButton);
        }
        #endregion

        #region OnSignUpCredentialsUpdate
        /// <summary>
        /// Registers a listener for sign-up credentials value changes.
        /// </summary>
        /// <param name="onSignUpCredentialsValueChanged">The callback to invoke when the sign-up credentials value changes.</param>
        internal void HandleOnSignUpCredentialsUpdate(UnityAction<string> onSignUpCredentialsValueChanged)
        {
            if (this.onSignUpCredentialsValueChanged is not null && onSignUpCredentialsValueChanged != null)
                this.onSignUpCredentialsValueChanged.AddListener(onSignUpCredentialsValueChanged);
        }

        /// <summary>
        /// Removes the specified listener from the sign-up credentials value changed event.
        /// </summary>
        /// <param name="onSignUpCredentialsValueChanged">The listener to remove from the event.</param>
        internal void UnHandleOnSignUpCredentialsUpdate(UnityAction<string> onSignUpCredentialsValueChanged)
        {
            if (this.onSignUpCredentialsValueChanged is not null && onSignUpCredentialsValueChanged != null)
                this.onSignUpCredentialsValueChanged.RemoveListener(onSignUpCredentialsValueChanged);
        }
        #endregion

        #region OnSignInCredentialsUpdate
        /// <summary>
        /// Registers a listener for sign-in credentials value changes.
        /// </summary>
        /// <param name="onSignInCredentialsValueChanged">The callback to invoke when the sign-in credentials value changes.</param>
        internal void HandleOnSignInCredentialsUpdate(UnityAction<string> onSignInCredentialsValueChanged)
        {
            if (this.onSignInCredentialsValueChanged is not null && onSignInCredentialsValueChanged != null)
                this.onSignInCredentialsValueChanged.AddListener(onSignInCredentialsValueChanged);
        }

        /// <summary>
        /// Removes the specified listener from the sign-in credentials value changed event.
        /// </summary>
        /// <param name="onSignInCredentialsValueChanged">The listener to remove from the sign-in credentials value changed event.</param>
        internal void UnHandleOnSignInCredentialsUpdate(UnityAction<string> onSignInCredentialsValueChanged)
        {
            if (this.onSignInCredentialsValueChanged is not null && onSignInCredentialsValueChanged != null)
                this.onSignInCredentialsValueChanged.RemoveListener(onSignInCredentialsValueChanged);
        }
        #endregion

        #region OnCredentialsSignIn
        /// <summary>
        /// Registers a listener for the credentials sign-in event.
        /// </summary>
        /// <param name="onCredentialsSignIn">The action to invoke when the credentials sign-in event occurs.</param>
        internal void HandleOnCredentialsSignIn(UnityAction<bool> onCredentialsSignIn)
        {
            if (this.onCredentialsSignIn is not null && onCredentialsSignIn != null)
                this.onCredentialsSignIn.AddListener(onCredentialsSignIn);
        }

        /// <summary>
        /// Removes the specified listener from the credentials sign-in event.
        /// </summary>
        /// <param name="onCredentialsSignIn">The listener to remove from the credentials sign-in event.</param>
        internal void UnHandleOnCredentialsSignIn(UnityAction<bool> onCredentialsSignIn)
        {
            if (this.onCredentialsSignIn is not null && onCredentialsSignIn != null)
                this.onCredentialsSignIn.RemoveListener(onCredentialsSignIn);
        }
        #endregion

        #region OnCredentialsSignUp
        /// <summary>
        /// Registers a listener for the credentials sign-up event.
        /// </summary>
        /// <param name="onCredentialsSignUp">The callback to invoke when the credentials sign-up event occurs.</param>
        internal void HandleOnCredentialsSignUp(UnityAction<bool> onCredentialsSignUp)
        {
            if (this.onCredentialsSignUp is not null && onCredentialsSignUp != null)
                this.onCredentialsSignUp.AddListener(onCredentialsSignUp);
        }

        /// <summary>
        /// Removes the specified listener from the credentials sign-up event.
        /// </summary>
        /// <param name="onCredentialsSignUp">The listener to remove from the credentials sign-up event.</param>
        internal void UnHandleOnCredentialsSignUp(UnityAction<bool> onCredentialsSignUp)
        {
            if (this.onCredentialsSignUp is not null && onCredentialsSignUp != null)
                this.onCredentialsSignUp.RemoveListener(onCredentialsSignUp);
        }
        #endregion

        #region OnCheckEmailVerification
        /// <summary>
        /// Registers a listener for email verification check events.
        /// </summary>
        /// <param name="onCheckEmailVerification">The callback to invoke when checking email verification.</param>
        internal void HandleOnCheckEmailVerification(UnityAction<bool> onCheckEmailVerification)
        {
            if (this.onCheckEmailVerification is not null && onCheckEmailVerification != null)
                this.onCheckEmailVerification.AddListener(onCheckEmailVerification);
        }

        /// <summary>
        /// Removes the specified listener from the email verification check event.
        /// </summary>
        /// <param name="onCheckEmailVerification">The listener to remove from the email verification check event.</param>
        internal void UnHandleOnCheckEmailVerification(UnityAction<bool> onCheckEmailVerification)
        {
            if (this.onCheckEmailVerification is not null && onCheckEmailVerification != null)
                this.onCheckEmailVerification.RemoveListener(onCheckEmailVerification);
        }
        #endregion

        #region OnRecoveryPassword
        /// <summary>
        /// Registers a listener for the recovery password event.
        /// </summary>
        /// <param name="onRecoveryPassword">The callback to invoke when the recovery password event occurs.</param>
        internal void HandleOnRecoveryPassword(UnityAction<bool> onRecoveryPassword)
        {
            if (this.onRecoveryPassword is not null && onRecoveryPassword != null)
                this.onRecoveryPassword.AddListener(onRecoveryPassword);
        }

        /// <summary>
        /// Removes the specified recovery password listener.
        /// </summary>
        /// <param name="onRecoveryPassword">The recovery password listener to remove.</param>
        internal void UnHandleOnRecoveryPassword(UnityAction<bool> onRecoveryPassword)
        {
            if (this.onRecoveryPassword is not null && onRecoveryPassword != null)
                this.onRecoveryPassword.RemoveListener(onRecoveryPassword);
        }
        #endregion
        #endregion

        #region Events
        /// <summary>
        /// Invokes the sign-up credentials value changed event and refreshes layout groups.
        /// </summary>
        /// <param name="newValue">The updated sign-up value.</param>
        private void OnSignUpValueChanged(string newValue)
        {
            onSignUpCredentialsValueChanged?.Invoke(newValue);
            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Invokes the sign-in credentials value changed event and refreshes layout groups recursively.
        /// </summary>
        /// <param name="newValue">The updated sign-in value.</param>
        private void OnSignInValueChanged(string newValue)
        {
            onSignInCredentialsValueChanged?.Invoke(newValue);
            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Activates the sign-in credentials screen in the authentication UI.
        /// </summary>
        private void OnOpenSignInScreen() => SetActive_Credentials(AuthUIActiveState.SignIn);

        /// <summary>
        /// Activates the sign-up screen in the authentication UI.
        /// </summary>
        private void OnOpenSignUpScreen() => SetActive_Credentials(AuthUIActiveState.SignUp);

        /// <summary>
        /// Activates the recovery credentials screen in the authentication UI.
        /// </summary>
        private void OnOpenRecoveryScreen() => SetActive_Credentials(AuthUIActiveState.Recovery);
        #endregion

        /// <summary>
        /// Represents the active state of the authentication UI, such as sign-in, sign-up, or recovery.
        /// </summary>
        internal enum AuthUIActiveState
        {
            None = 0,
            SignIn = 1,
            SignUp = 2,
            Recovery = 3
        }
    }
}
