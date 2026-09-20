using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.CredentialsValidator;
using static HelperSharedLibrary.ExceptionHelper;

namespace ProDomino.Authentication
{
    // Credentials_AuthManager.cs

    /// <summary>
    /// Manages user authentication using credentials, including sign-up, sign-in, password recovery, email
    /// verification, and UI event handling with Unity Gaming Services and Firebase integration.
    /// </summary>
    public partial class AuthManager
    {
        [Header("Credentials Module")]
        [SerializeField] private bool isEmailVerificated_onlyEditor;
        private Coroutine checkEmailVerificationCoroutine;

        private CredentialsDenyState
            _signUp_UsernameState,
            _signUp_EmailState,
            _signUp_PasswordState,
            _signUp_ConfirmPasswordState;

        private CredentialsDenyState
            _signIn_CredentialsState,
            _signIn_PasswordState;

        /// <summary>
        /// This property is used to check if the player is authenticated only with Credentials <br></br>
        /// </summary>
        public bool IsUserAuthenticatedWithCredentials => IsAlreadyInitialized
            && IsUGSAuthenticated is true
            && AuthenticationService.Instance.PlayerInfo is PlayerInfo playerInfo 
            && playerInfo is not null
            && !string.IsNullOrEmpty(playerInfo.Username); // If the username is not empty, it means the player is authenticated, at least, with credentials 

        public bool IsEmailVerified => Firebase_WasEmailVerified();

        #region Unity Events - Credentials

        /// <summary>
        /// Specific Awake method for Facebook Authentication <br></br>
        /// Try to initialize request for iOS authotization tracking at first,
        /// then check to validate Unity and FB services
        /// </summary>
        private async UniTask OnAwake_Credentials()
        {
            HandleOnPressCredentialsLoginButton(TryToSignIn);
            HandleOnPressCredentialsCreateAccountButton(TryToCreateAccount);
            HandleOnPressCredentialsRecoveryButton(TryToRecoverPassword);

            HandleOnSignUpCredentialsUpdate(OnSignUpCredentialsUpdate);
            HandleOnSignInCredentialsUpdate(OnSignInCredentialsUpdate);

            // [Only iOS] Check if the player has authorized tracking
            var wasTrackingAuthorized = await TryToAuthorizeTracking();

            // Just in case, try to initialize Unity services if not already initialized
            await HandleProcess_AuthManagerProxy(TryToInitializeUnityServices, nameof(TryToInitializeUnityServices));
            await HandleProcess_AuthManagerProxy
                (uniTask: () => new UnityCredentialsValidator().Initialize().AsUniTask(), 
                taskId: $"{nameof(UnityCredentialsValidator)}_{nameof(UnityCredentialsValidator.Initialize)}",
                showLoading: true);
        }

        /// <summary>
        /// Initializes authentication UI, resets sign-in and sign-up credentials, and checks email verification if the
        /// user is authenticated.
        /// </summary>
        private void OnStart_Credentials()
        {
            if (AuthUI)
                AuthUI.Initialize(IsValidToSignIn, IsValidToSignUp);

            OnSignUpCredentialsUpdate(string.Empty);
            OnSignInCredentialsUpdate(string.Empty);

            // Check for once if the email is verified
            if (IsUserAuthenticatedWithCredentials)
                Firebase_CheckEmailVerification();
        }

        /// <summary>
        /// Removes event handlers related to credentials login, account creation, and credentials updates.
        /// </summary>
        private void OnDestroy_Credentials()
        {
            UnHandleOnPressCredentialsLoginButton(TryToSignIn);
            UnHandleOnPressCredentialsCreateAccountButton(TryToCreateAccount);

            UnHandleOnSignUpCredentialsUpdate(OnSignUpCredentialsUpdate);
            UnHandleOnSignInCredentialsUpdate(OnSignInCredentialsUpdate);
        }
        #endregion

        /// <summary>
        /// Determines whether the sign-in credentials and password states are valid for signing in.
        /// </summary>
        /// <returns>True if both credentials and password states are valid; otherwise, false.</returns>
        private bool IsValidToSignIn() =>
            _signIn_CredentialsState is CredentialsDenyState.IsValid
            && _signIn_PasswordState is CredentialsDenyState.IsValid;

        /// <summary>
        /// Determines whether all sign-up fields are valid and required checkboxes are selected.
        /// </summary>
        /// <returns>True if username, email, password, and confirm password states are valid and both terms and data treatment
        /// checkboxes are checked; otherwise, false.</returns>
        private bool IsValidToSignUp() =>
            _signUp_UsernameState is CredentialsDenyState.IsValid
            && _signUp_EmailState is CredentialsDenyState.IsValid
            && _signUp_PasswordState is CredentialsDenyState.IsValid
            && _signUp_ConfirmPasswordState is CredentialsDenyState.IsValid
            && AuthUI.WasTermsAndConditionsCheck_SignUp
            && AuthUI.WasDataTreatmentCheck_SignUp;

        /// <summary>
        /// Attempts to create a new user account by validating input and performing sign-up operations with Unity
        /// Gaming Services and Firebase.
        /// </summary>
        private async void TryToCreateAccount()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(TryToCreateAccount)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // Check if the username and password are valid, then try to create an account
            if (IsValidToSignUp())
                await HandleProcess_AuthManagerProxy
                    (uniTask: () => CustomSignUp(AuthUI.UserName_SignUp, AuthUI.Email_SignUp, AuthUI.Password_SignUp), 
                    taskId: nameof(CustomSignUp),
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true);

            // Make an custom sign up that will sign-up the user in UGS as well as in Firebase
            async UniTask CustomSignUp(string username, string email, string password)
            {
                try
                {
                    if (IsUserAuthenticatedWithCredentials || IsUserAuthenticatedWithProvider)
                    { 
                        Debug.LogWarning("Already authenticated with provider");
                        return;
                    }

                    // If the user is not authenticated, sign in anonymously to be able to call the Cloud Code function
                    if (!IsUGSAuthenticated)
                    { 
                        await SignInAnonmously();

                        // If the user still is not authenticated, return
                        if (!IsUGSAuthenticated)
                            throw new Exception("Failed to sign in anonymously");
                    }

                    var playerId = AuthenticationService.Instance.PlayerId;
                    var accessToken = AuthenticationService.Instance.AccessToken;
                    var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

                    var parameters = new Dictionary<string, object>()
                    {
                        ["username"] = username,
                        ["email"] = email,
                        ["password"] = password
                    };

                    var parametersJson = JsonConvert.SerializeObject(parameters);
                    var encriptedParameters = SecurityHelper.EncryptData(parametersJson, derivedKey);
                    var encriptedParametersJson = JsonConvert.SerializeObject(encriptedParameters);

                    // Call the function within the module and provide the parameters we defined in there
                    await module.SignUpWithCredentials(encriptedParametersJson);

                    // Sign-out the anonymous user
                    await SignOut(isSignOutFromFirebaseToo: true, isSignInAnonymouslyOnSignOut: false, isForcingDeleteAccount: false);

                    // Once the user is signed up with UGS since Cloud Code, sign in locally with the credentials
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);

                    // Once the user is signed in with UGS, sign up with the local Firebase (to keep the Firebase session alive)
                    if (IsValidPlatformToUseJSlib())
                        FirebaseAuth.SignInWithEmailAndPassword
                            (email,
                            password,
                            gameObject.name,
                            nameof(OnSignUpWithCredentialsSuccessfully),
                            nameof(OnSignUpWithCredentialsFailed));
                    else
                        OnSignUpWithCredentialsSuccessfully();

                    Debug.Log("<color=cyan><b>SignUp</b> was completed successfully</color>");
                }
                catch (CloudCodeException exception)
                {
                    var message = $"Unknow exception. Error Code: {exception.ErrorCode}";

                    // Regular expression to capture "Exception type" and "Message"
                    var match = Regex.Match(exception.Message, @"Exception type: (\w+).*?Message: (.*)");
                    if (match.Success)
                    {
                        var exceptionType = match.Groups[1].Value;
                        var exceptionMessageExtracted = match.Groups[2].Value;

                        if (exceptionType is nameof(UGSException) or nameof(FirebaseException))
                            message = $"<b>*</b> {exceptionMessageExtracted}";
                    }

                    Debug.LogWarning($"SignUp failed Cloud Code: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignUpWithCredentialsFailed(message);
                }
                catch (UGSException exception)
                {
                    Debug.LogWarning($"SignUp failed UGS: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignUpWithCredentialsFailed($"<b>*</b> {exception.Message}");
                }
                catch (FirebaseException exception)
                {
                    Debug.LogWarning($"SignUp failed Firebase: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignUpWithCredentialsFailed($"<b>*</b> {exception.Message}");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"SignUp failed Basic Exception: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignUpWithCredentialsFailed($"<b>*</b> Unknow exception.");
                }
            }
        }

        /// <summary>
        /// Attempts to sign in the user by validating credentials and handling authentication with Unity services and
        /// Firebase.
        /// </summary>
        private async void TryToSignIn()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(TryToSignIn)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // Check if the username and password are valid, then try to sign in
            if (IsValidToSignIn())
                await HandleProcess_AuthManagerProxy
                    (uniTask: () => CustomSignIn(AuthUI.Credentials_SignIn, AuthUI.Password_SignIn), 
                    taskId: nameof(CustomSignIn),
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true,
                    resultValidator: () => IsUserAuthenticatedWithCredentials);

            // Make an custom sign up that will sign-up the user in UGS as well as in Firebase
            async UniTask CustomSignIn(string credentials, string password)
            {
                try
                {
                    if (IsUserAuthenticatedWithCredentials || IsUserAuthenticatedWithProvider)
                    {
                        Debug.Log("Already authenticated with provider");
                        return;
                    }

                    // If the user is not authenticated, sign in anonymously to be able to call the Cloud Code function
                    if (!IsUGSAuthenticated)
                    {
                        await SignInAnonmously();

                        // If the user still is not authenticated, return
                        if (!IsUGSAuthenticated)
                            throw new Exception("Failed to sign in anonymously");
                    }

                    var playerId = AuthenticationService.Instance.PlayerId;
                    var accessToken = AuthenticationService.Instance.AccessToken;
                    var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

                    var parameters = new Dictionary<string, object>()
                    {
                        ["credentials"] = credentials,
                        ["password"] = password
                    };

                    var parametersJson = JsonConvert.SerializeObject(parameters);
                    var encriptedParameters = SecurityHelper.EncryptData(parametersJson, derivedKey);
                    var encriptedParametersJson = JsonConvert.SerializeObject(encriptedParameters);

                    // Call the function within the module and provide the parameters we defined in there
                    // This function will sign-in the user in Firebase too
                    var responseEncryptedData = await module.SignInWithCredentials(encriptedParametersJson);

                    if (responseEncryptedData is null)
                    {
                        Debug.LogWarning("Response encrypted data is null");
                        return;
                    }

                    var securityData = JsonConvert.DeserializeObject<SecurityData>(responseEncryptedData);
                    var decryptedResponseJson = SecurityHelper.DecryptData(securityData, derivedKey);
                    var leftingCredential = JsonConvert.DeserializeObject<string>(decryptedResponseJson);

                    // If the lefting credential couldn't be got, thrown an exception
                    if (string.IsNullOrEmpty(leftingCredential))
                        throw new Exception("Lefting credential is null or empty");

                    // Sign-out the anonymous user
                    await SignOut(isSignOutFromFirebaseToo: true, isSignInAnonymouslyOnSignOut: false);
                    Debug.Log("Lefting credential obtained successfully");

                    // Usign the unity's SDK, sign in with the credentials
                    var isUsername = !credentials.Contains("@");
                    await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(isUsername ? credentials : leftingCredential, password);

                    // Once the user is signed in with UGS, sign in with the local Firebase (to keep the Firebase session alive)
                    if (IsValidPlatformToUseJSlib())
                        FirebaseAuth.SignInWithEmailAndPassword
                            (isUsername ? leftingCredential : credentials, 
                            password, 
                            gameObject.name, 
                            nameof(OnSignInWithCredentialsSuccessfully),
                            nameof(OnSignInWithCredentialsFailed));
                    else
                        OnSignInWithCredentialsSuccessfully();

                    var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
                    Debug.Log("<color=green><b>SignIn</b> was completed successfullu</color>");
                }
                catch (CloudCodeException exception)
                {
                    var message = $"<b>*</b> Unknow exception. Error Code: {exception.ErrorCode}";

                    // Regular expression to capture "Exception type" and "Message"
                    var match = Regex.Match(exception.Message, @"Exception type: (\w+).*?Message: (.*)");
                    if (match.Success)
                    {
                        var exceptionType = match.Groups[1].Value;
                        var exceptionMessageExtracted = match.Groups[2].Value;

                        if (exceptionType is nameof(UGSException) or nameof(FirebaseException))
                            message = $"<b>*</b> {exceptionMessageExtracted}";
                    }

                    Debug.LogWarning($"SignIn failed Cloud Code: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignInWithCredentialsFailed(message);
                }
                catch (UGSException exception)
                {
                    Debug.LogWarning($"SignIn failed UGS: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignInWithCredentialsFailed($"<b>*</b> {exception.Message}");
                }
                catch (FirebaseException exception)
                {
                    Debug.LogWarning($"SignIn failed Firebase: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignInWithCredentialsFailed($"<b>*</b> {exception.Message}");
                }
                catch (Exception exception)
                { 
                    Debug.LogWarning($"SignIn failed Basic Exception: {JsonConvert.SerializeObject(exception.Message, Formatting.Indented)}");
                    OnSignInWithCredentialsFailed($"<b>*</b> Unknow exception.");
                }
            }
        }

        /// <summary>
        /// Attempts to initiate the password recovery process by sending a recovery email if Unity services are
        /// initialized, the player is authenticated, and a valid recovery email is provided.
        /// </summary>
        private async void TryToRecoverPassword()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(TryToRecoverPassword)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (!IsUGSAuthenticated)
            {
                Debug.LogWarning("The player is not authenticated, so there is no possible send recovery mail");
                return;
            }

            if (string.IsNullOrEmpty(AuthUI.RecoveryEmail))
            {
                Debug.LogWarning("The recovery email is null or empty");
                return;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            var accessToken = AuthenticationService.Instance.AccessToken;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

            var parameters = new Dictionary<string, object>()
            {
                ["email"] = AuthUI.RecoveryEmail
            };

            var parametersJson = JsonConvert.SerializeObject(parameters);   
            var encriptedParameters = SecurityHelper.EncryptData(parametersJson, derivedKey);
            var encriptedParametersJson = JsonConvert.SerializeObject(encriptedParameters);

            var wasSentSuccessfully = await HandleProcess_AuthManagerProxy
                (uniTask: () => module.PasswordRecovery(encriptedParametersJson).AsUniTask(), 
                taskId: nameof(module.PasswordRecovery),
                showLoading: true,
                shouldIgnoreTryAgainProcess: true);

            // If the response is positive, call the success event; otherwise, the failed one
            if (wasSentSuccessfully)
            {
                SetActiveAuthUI(false);

                OnRecoveryPasswordSuccessfully();
                Debug.Log("<color=cyan><b>Password Recovery</b> was started successfully</color>");
            } 
            
            else
            { 
                Debug.LogWarning("Password Recovery failed");
                OnRecoveryPasswordFailed();
            }
        }

        /// <summary>
        /// Links the current user account with the specified username and password using Unity Authentication services.
        /// </summary>
        /// <param name="username">The username to associate with the user account.</param>
        /// <param name="password">The password to associate with the user account.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async UniTask LinkWithUserNameAndPasswordAsync(string username, string password)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(LinkWithUserNameAndPasswordAsync)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            try
            {
                await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);
                Debug.Log("Username and password added.");
            }
            catch (AuthenticationException ex)
            {
                // Compare error code to AuthenticationErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
            catch (RequestFailedException ex)
            {
                // Compare error code to CommonErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Updates the user's password asynchronously using Unity Authentication Service.
        /// </summary>
        /// <param name="currentPassword">The user's current password.</param>
        /// <param name="newPassword">The new password to set.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask UpdatePasswordAsync(string currentPassword, string newPassword)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(UpdatePasswordAsync)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            try
            {
                await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);
                Debug.Log("Password updated.");
            }
            catch (AuthenticationException ex)
            {
                // Compare error code to AuthenticationErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
            catch (RequestFailedException ex)
            {
                // Compare error code to CommonErrorCodes
                // Notify the player with the proper error message
                Debug.LogException(ex);
            }
        }


        /// <summary>
        /// Try to show the player the feedback of the username and password when it is invalid
        /// </summary>
        /// <param name="isSignIn">Indicates whether the feedback is for a sign-in attempt.</param>
        /// <param name="credentialsStateTuple">An array of tuples containing the credentials state and input field type.</param>
        private void FeedbackState(bool isSignIn, params (CredentialsDenyState state, InputfieldType inputFieldType)[] credentialsStateTuple)
        {
            AuthUI?.FeedbackClear(isSignIn);

            foreach (var stateTuple in credentialsStateTuple)
            {
                if (stateTuple.state == CredentialsDenyState.IsValid)
                    continue;

                // Show the feedback
                AuthUI?.FeedbackCredentialsState(isSignIn, stateTuple);
            }
        }

        /// <summary>
        /// Initiates a coroutine to periodically check if the user's email has been verified and updates the UI
        /// accordingly.
        /// </summary>
        private void StartCheckingForVerifiedEmail()
        {
            if (checkEmailVerificationCoroutine != null)
            {
                Debug.Log("[Credentials] Already checking for email verification");
                return;
            }
            
            Debug.Log("[Credentials] Starting to check for email verification...");
            checkEmailVerificationCoroutine = StartCoroutine(CheckEmailVerification());
            
            IEnumerator CheckEmailVerification()
            {
                for (int timesIterated = 0; timesIterated < 15; timesIterated++)
                {
                    yield return new WaitForSecondsRealtime(timesIterated is <= 10 ? 30 : 60); // 10 min in total
                    Firebase_CheckEmailVerification();

                    // If the emails is already verified, stops the iteration
                    if (IsEmailVerified)
                    {
                        Debug.Log("[Credentials] Email is verified, stopping the verification checks");
                        break;
                    }
                }

                Debug.Log("[Credentials] Stopped checking for email verification");
                checkEmailVerificationCoroutine = null;

                // Once the checking is done, call the event one last time to inform the UI
                AuthUI?.CallOnCheckEmailVerificationEvent(IsEmailVerified);
            }
        }

        /// <summary>
        /// Sends an email verification to the user's email address.
        /// </summary>
        /// <returns>The email address to which the verification was sent, or null if the operation failed.</returns>
        public async UniTask<string> SendEmailVerification()
        {
            if (!IsUGSAuthenticated)
            {
                Debug.LogWarning("The player is not authenticated, so there is no need to sign out");
                return default;
            }

            if (IsEmailVerified)
            { 
                Debug.Log("The email is already verified");
                return default;
            }

            Debug.Log("Sending email verification...");
            var emailVerificationResponseEncrypted = await HandleProcess_AuthManagerProxy
                (uniTask: () => module.EmailVerification().AsUniTask(),
                taskId: nameof(module.EmailVerification),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true);

            // Deserialize and decrypt the data
            var emailVerificationResponse = DeserializeAndDecryptData<FirebaseSendEmailResponse>(emailVerificationResponseEncrypted);
            if (emailVerificationResponse == null)
            {
                Debug.LogWarning("Failed to deserialize email verification response.");
                return default;
            }

            if (emailVerificationResponse is null or { email: null or "" })
            {
                Debug.LogWarning("Failed to send email verification.");
                return default;
            }

            Debug.Log($"Email verification sent to: {emailVerificationResponse.email}");
            return emailVerificationResponse.email;
        }

        #region Firebase Handlers
        /// <summary>
        /// Initiates an email verification check using Firebase authentication and handles the result with callback
        /// methods.
        /// </summary>
        public void Firebase_CheckEmailVerification()
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            Debug.Log("[Credentials] Checking email verification...");
            FirebaseAuth.CheckEmailVerification(gameObject.name, nameof(OnEmailVerifiedSuccessfully), nameof(OnEmailVerificationFailed));
        }

        /// <summary>
        /// Checks whether the user's email has been verified in Firebase.
        /// </summary>
        /// <returns>True if the email is verified; otherwise, false.</returns>
        public bool Firebase_WasEmailVerified()
        {
#if UNITY_EDITOR
            return isEmailVerificated_onlyEditor;
#else
            if (!IsValidPlatformToUseJSlib())
                return false;

            var result = FirebaseAuth.IsEmailVerified();
            return result is 1;
#endif
        }
#endregion

        #region Handlers
        #region OnPressCredentialsLoginButton
        /// <summary>
        /// Invokes the credentials sign-in button handler in the authentication UI.
        /// </summary>
        /// <param name="onPressCredentialsLoginButton">Action to execute when the credentials login button is pressed.</param>
        public void HandleOnPressCredentialsLoginButton(UnityAction onPressCredentialsLoginButton) => AuthUI?.HandleOnPressCredentialsSignInButton(onPressCredentialsLoginButton);
       
        /// <summary>
        /// Removes the handler for the credentials login button press event.
        /// </summary>
        /// <param name="onPressCredentialsLoginButton">The action to remove from the credentials login button press event.</param>
        public void UnHandleOnPressCredentialsLoginButton(UnityAction onPressCredentialsLoginButton) => AuthUI?.UnHandleOnPressCredentialsSignInButton(onPressCredentialsLoginButton);
        #endregion

        #region OnPressCredentialsCreateAccountButton
        /// <summary>
        /// Invokes the sign-up button handler in the authentication UI with the specified action.
        /// </summary>
        /// <param name="onPressCredentialsCreateAccountButton">Action to execute when the create account button is pressed.</param>
        public void HandleOnPressCredentialsCreateAccountButton(UnityAction onPressCredentialsCreateAccountButton) => AuthUI?.HandleOnPressCredentialsSignUpButton(onPressCredentialsCreateAccountButton);
        
        /// <summary>
        /// Removes the handler for the create account button press event in the credentials UI.
        /// </summary>
        /// <param name="onPressCredentialsCreateAccountButton">The action to remove from the create account button press event.</param>
        public void UnHandleOnPressCredentialsCreateAccountButton(UnityAction onPressCredentialsCreateAccountButton) => AuthUI?.UnHandleOnPressCredentialsSignUpButton(onPressCredentialsCreateAccountButton);
        #endregion

        #region OnPressCredentialsRecoveryButton
        /// <summary>
        /// Invokes the credentials recovery button handler in the authentication UI.
        /// </summary>
        /// <param name="onPressCredentialsRecoveryButton">Action to execute when the credentials recovery button is pressed.</param>
        public void HandleOnPressCredentialsRecoveryButton(UnityAction onPressCredentialsRecoveryButton) => AuthUI?.HandleOnPressCredentialsRecoveryButton(onPressCredentialsRecoveryButton);
       
        /// <summary>
        /// Removes the handler for the credentials recovery button press event.
        /// </summary>
        /// <param name="onPressCredentialsRecoveryButton">The action to remove from the credentials recovery button press event.</param>
        public void UnHandleOnPressCredentialsRecoveryButton(UnityAction onPressCredentialsRecoveryButton) => AuthUI?.UnHandleOnPressCredentialsRecoveryButton(onPressCredentialsRecoveryButton);
        #endregion

        #region OnSignUpCredentialsUpdate
        /// <summary>
        /// Registers a callback to handle updates to sign-up credentials.
        /// </summary>
        /// <param name="onSignUpCredentialsValueChanged">Callback invoked when the sign-up credentials value changes.</param>
        public void HandleOnSignUpCredentialsUpdate(UnityAction<string> onSignUpCredentialsValueChanged) => AuthUI?.HandleOnSignUpCredentialsUpdate(onSignUpCredentialsValueChanged);
        
        /// <summary>
        /// Removes the handler for sign-up credentials value changes.
        /// </summary>
        /// <param name="onSignUpCredentialsValueChanged">The callback to be removed from sign-up credentials value change notifications.</param>
        public void UnHandleOnSignUpCredentialsUpdate(UnityAction<string> onSignUpCredentialsValueChanged) => AuthUI?.UnHandleOnSignUpCredentialsUpdate(onSignUpCredentialsValueChanged);
        #endregion

        #region OnSignInCredentialsUpdate
        /// <summary>
        /// Registers a callback to handle updates to sign-in credentials.
        /// </summary>
        /// <param name="onSignInCredentialsValueChanged">Callback invoked when the sign-in credentials value changes.</param>
        public void HandleOnSignInCredentialsUpdate(UnityAction<string> onSignInCredentialsValueChanged) => AuthUI?.HandleOnSignInCredentialsUpdate(onSignInCredentialsValueChanged);
        
        /// <summary>
        /// Removes the handler for sign-in credentials value changes.
        /// </summary>
        /// <param name="onSignInCredentialsValueChanged">The callback to remove from sign-in credentials value change notifications.</param>
        public void UnHandleOnSignInCredentialsUpdate(UnityAction<string> onSignInCredentialsValueChanged) => AuthUI?.UnHandleOnSignInCredentialsUpdate(onSignInCredentialsValueChanged);
        #endregion

        #region OnCredentialsLogin
        /// <summary>
        /// Invokes the credentials sign-in handler with the specified callback.
        /// </summary>
        /// <param name="onCredentialsLogin">Callback action to execute upon credentials login, receiving a boolean indicating success.</param>
        public void HandleOnCredentialsLogin(UnityAction<bool> onCredentialsLogin) => AuthUI?.HandleOnCredentialsSignIn(onCredentialsLogin);
        
        /// <summary>
        /// Removes the handler for credential-based login events.
        /// </summary>
        /// <param name="onCredentialsLogin">The callback to be removed from credential login event handling.</param>
        public void UnHandleOnCredentialsLogin(UnityAction<bool> onCredentialsLogin) => AuthUI?.UnHandleOnCredentialsSignIn(onCredentialsLogin);
        #endregion

        #region OnCredentialsCreateAccount
        /// <summary>
        /// Invokes the account creation handler with the specified callback.
        /// </summary>
        /// <param name="onCredentialsCreateAccount">Callback invoked upon account creation, receiving a boolean indicating success.</param>
        public void HandleOnCredentialsCreateAccount(UnityAction<bool> onCredentialsCreateAccount) => AuthUI?.HandleOnCredentialsSignUp(onCredentialsCreateAccount);
        
        /// <summary>
        /// Removes the handler for account creation credentials from the authentication UI.
        /// </summary>
        /// <param name="onCredentialsCreateAccount">The callback to be removed from the credentials sign-up handler.</param>
        public void UnHandleOnCredentialsCreateAccount(UnityAction<bool> onCredentialsCreateAccount) => AuthUI?.UnHandleOnCredentialsSignUp(onCredentialsCreateAccount);
        #endregion

        #region OnCheckEmailVerification
        /// <summary>
        /// Invokes email verification check handling in the AuthUI component.
        /// </summary>
        /// <param name="onCheckEmailVerification">Callback action to receive the result of the email verification check.</param>
        public void HandleOnCheckEmailVerification(UnityAction<bool> onCheckEmailVerification) => AuthUI?.HandleOnCheckEmailVerification(onCheckEmailVerification);
        
        /// <summary>
        /// Removes the specified callback from the email verification check event handler.
        /// </summary>
        /// <param name="onCheckEmailVerification">The callback to be removed from the email verification check event.</param>
        public void UnHandleOnCheckEmailVerification(UnityAction<bool> onCheckEmailVerification) => AuthUI?.UnHandleOnCheckEmailVerification(onCheckEmailVerification);
        #endregion

        #region OnRecoveryPassword
        /// <summary>
        /// Invokes the recovery password handler on the AuthUI component.
        /// </summary>
        /// <param name="onRecoveryPassword">Callback action to handle the result of the recovery password process.</param>
        public void HandleOnRecoveryPassword(UnityAction<bool> onRecoveryPassword) => AuthUI?.HandleOnRecoveryPassword(onRecoveryPassword);
        
        /// <summary>
        /// Removes the specified recovery password handler from the authentication UI.
        /// </summary>
        /// <param name="onRecoveryPassword">The recovery password handler to remove.</param>
        public void UnHandleOnRecoveryPassword(UnityAction<bool> onRecoveryPassword) => AuthUI?.UnHandleOnRecoveryPassword(onRecoveryPassword);
        #endregion
        #endregion

        #region Events
        /// <summary>
        /// Handles post-sign-in actions after successful authentication with credentials, including updating UI,
        /// verifying email, and invoking completion events.
        /// </summary>
        private void OnSignInWithCredentialsSuccessfully()
        { 
            AuthUI?.CallOnCredentialsSignInEvent(true);

            // Turn off the sign-up UI
            SetActiveAuthUI(false);

            // Once the player sign-in, check if the firebase email is already verified; otherwise, start checking for it for a while.
            if (!IsEmailVerified)
                StartCheckingForVerifiedEmail();

            // Once the player is signed in, call the event
            if (IsUGSAuthenticated)
                onCompletelySignedIn?.Invoke();
            else
                Debug.LogWarning("Player is not completely signed in after credentials sign-in");
        }

        /// <summary>
        /// Handles failed sign-in attempts with credentials by updating the UI with feedback and logging the error.
        /// </summary>
        /// <param name="response">The error message received from the failed sign-in attempt.</param>
        private void OnSignInWithCredentialsFailed(string response)
        { 
            AuthUI?.CallOnCredentialsSignInEvent(false);
            AuthUI?.SetFeedbackDirectly(true, $"<color=red>{response}</color>");
            Debug.LogError($"Sign-In failed: {response}");
        }

        /// <summary>
        /// Handles post sign-up actions, including triggering sign-up events, updating UI, verifying email status, and
        /// invoking complete sign-in callbacks.
        /// </summary>
        private void OnSignUpWithCredentialsSuccessfully()
        { 
            AuthUI?.CallOnCredentialsSignUpEvent(true);

            // Turn off the sign-up UI
            SetActiveAuthUI(false);

            // Once the player sign-Up, check if the firebase email is already verified; otherwise, start checking for it for a while.
            if (!IsEmailVerified)
                StartCheckingForVerifiedEmail();

            // Once the player is signed in, call the event
            if (IsUGSAuthenticated)
                onCompletelySignedIn?.Invoke();
            else
                Debug.LogWarning("Player is not completely signed in after credentials sign-up");
        }

        /// <summary>
        /// Handles failed sign-up attempts by updating the UI with an error message and logging the failure.
        /// </summary>
        /// <param name="response">The error message received from the sign-up attempt.</param>
        private void OnSignUpWithCredentialsFailed(string response)
        { 
            AuthUI?.CallOnCredentialsSignUpEvent(false);
            AuthUI?.SetFeedbackDirectly(false, $"<color=red>{response}</color>");
            Debug.LogError($"Sign-Up failed: {response}");
        }

        /// <summary>
        /// Validates sign-up credentials and updates feedback based on the validity of username, email, password, and
        /// confirm password fields.
        /// </summary>
        /// <param name="newValue">The updated value of the sign-up credential input.</param>
        private void OnSignUpCredentialsUpdate(string newValue)
        {
            var wasUsernameValid = IsValidUsername(AuthUI.UserName_SignUp, ref _signUp_UsernameState);
            var wasEmailValid = IsValidEmail(AuthUI.Email_SignUp, ref _signUp_EmailState);
            var wasPasswordValid = IsValidPassword(AuthUI.Password_SignUp, ref _signUp_PasswordState);
            var wasConfirmPasswordValid = IsValidConfirmPassword(AuthUI.ConfirmPassword_SignUp, AuthUI.Password_SignUp, ref _signUp_ConfirmPasswordState);

            if (wasUsernameValid && wasEmailValid && wasPasswordValid && wasConfirmPasswordValid)
                AuthUI.FeedbackClear(false);
            else
                FeedbackState
                    (false, 
                    (_signUp_UsernameState, InputfieldType.UserName),
                    (_signUp_EmailState, InputfieldType.Email),
                    (_signUp_PasswordState, InputfieldType.Password),
                    (_signUp_ConfirmPasswordState, InputfieldType.ConfirmPassword));
        }

        /// <summary>
        /// Validates updated sign-in credentials and password, then updates feedback based on their validity.
        /// </summary>
        /// <param name="newValue">The new value entered for the sign-in credentials.</param>
        private void OnSignInCredentialsUpdate(string newValue)
        {
            var wasCredentialsValid = false;
            var wasPasswordValid = IsValidPassword(AuthUI.Password_SignIn, ref _signIn_PasswordState);

            if (AuthUI.Credentials_SignIn.Contains("@"))
                wasCredentialsValid = IsValidEmail(AuthUI.Credentials_SignIn, ref _signIn_CredentialsState);
            else
                wasCredentialsValid = IsValidUsername(AuthUI.Credentials_SignIn, ref _signIn_CredentialsState);

            if (wasCredentialsValid && wasPasswordValid)
                AuthUI.FeedbackClear(true);
            else
                FeedbackState
                    (true,
                    (_signIn_CredentialsState, InputfieldType.Credentials),
                    (_signIn_PasswordState, InputfieldType.Password));
        }

        /// <summary>
        /// Logs a successful email verification and notifies the UI of the verification status.
        /// </summary>
        private void OnEmailVerifiedSuccessfully()
        {
            Debug.Log("[Credentials] Email Verified successfully");
            AuthUI?.CallOnCheckEmailVerificationEvent(true);
        }

        /// <summary>
        /// Handles a failed email verification attempt by notifying the UI and logging a warning.
        /// </summary>
        /// <param name="response">The response message detailing the failure.</param>
        private void OnEmailVerificationFailed(string response)
        {
            AuthUI?.CallOnCheckEmailVerificationEvent(false);
            Debug.LogWarning($"[Credentials] Email Verification failed: {response}");
        }

        /// <summary>
        /// Notifies the authentication UI of a successful password recovery event.
        /// </summary>
        private void OnRecoveryPasswordSuccessfully()
        {
            AuthUI?.CallOnRecoveryPasswordEvent(true);
        }

        /// <summary>
        /// Handles a failed password recovery attempt by notifying the UI and logging a warning.
        /// </summary>
        private void OnRecoveryPasswordFailed()
        {
            AuthUI?.CallOnRecoveryPasswordEvent(false);
            Debug.LogWarning($"Password Recovery failed");
        }
        #endregion

        /// <summary>
        /// Specifies input field types for user authentication and registration forms.
        /// </summary>
        public enum InputfieldType
        {
            None = 0,
            UserName = 1,
            Email = 2,
            Credentials = 3,
            Password = 4,
            ConfirmPassword = 5
        }
    }
}
