using Cysharp.Threading.Tasks;
using Facebook.Unity;
using FirebaseWebGL.Scripts.FirebaseBridge;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Events;


#if UNITY_IOS || UNITY_EDITOR
#endif

namespace ProDomino.Authentication
{
    // Facebook_AuthManager.cs

    /// <summary>
    /// Manages Facebook authentication within a Unity application, handling initialization, sign-in, session
    /// management, and user profile integration with Unity and Firebase services.
    /// </summary>
    public partial class AuthManager
    {
        public string FacebookId
        {
            get
            {
                if (!IsAlreadyInitialized)
                    Debug.LogWarning("Could not check facebook id because Unity Services are not initialized yet");

                return IsAlreadyInitialized ? AuthenticationService.Instance.PlayerInfo?.GetFacebookId() : default;
            }
        }

        #region Unity Events - Facebook

        /// <summary>
        /// Specific Awake method for Facebook Authentication
        /// Initializes Unity Services, then Facebook SDK,
        /// and finally queries Facebook session status (getLoginStatus)
        /// </summary>
        private async UniTask OnAwake_Facebook()
        {
            // Register the Facebook login button callback
            HandleOnPressFacebookLoginButton(OpenLoginWithFacebookInterface);

            // [iOS only] Ask for tracking authorization if required
            var wasTrackingAuthorized = await TryToAuthorizeTracking();

            // Ensure Unity Services are initialized
            if (!IsAlreadyInitialized)
            {
                Debug.Log($"<b>[{nameof(OnAwake_Facebook)}]</b> Unity Services not initialized, trying to initialize...");
                await TryToInitializeUnityServices();
            }

            // Abort if Unity Services could not be initialized
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(OnAwake_Facebook)}]</b> Unity Services are not initialized, aborting Facebook setup.");
                return;
            }

            // Initialize Facebook SDK if needed
            if (!FB.IsInitialized)
            {
                Debug.Log($"<b>[{nameof(OnAwake_Facebook)}]</b> Initializing Facebook SDK...");

                FB.Init(
                    onInitComplete: OnFacebookInitComplete,
                    onHideUnity: OnHideUnity
                );

                // Wait until Facebook SDK is initialized
                await UniTask.WaitUntil(() => FB.IsInitialized).TimeoutWithoutException(TimeSpan.FromSeconds(5));
            } 
            else
            {
                Debug.Log($"<b>[{nameof(OnAwake_Facebook)}]</b> Facebook SDK already initialized.");
                OnFacebookInitComplete();
            }
        }

        /// <summary>
        /// Attempts to automatically sign in the user with Facebook on application start if a valid session exists and
        /// the platform supports it.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous sign-in operation.</returns>
        private async UniTask OnStart_Facebook()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"[OnStart_Facebook] Couldn't be called. Unity services is not initialized");
                return;
            }

            // Check if the platform is valid to use the Firebase WebGL JSlib
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning($"[OnStart_Facebook] Firebase WebGL is not supported on this platform, skipping Facebook automatic sign-in on Start.");
                return;
            }

            // Check if the user is not already authenticated anonymously or with other providers
            if (!IsUGSAuthenticated || !string.IsNullOrEmpty(GoogleId) || IsUserAuthenticatedWithCredentials)
            { 
                Debug.LogWarning($"[OnStart_Facebook] User is already authenticated, skipping Facebook automatic sign-in on Start.");
                return;
            }

            Debug.Log($"[OnStart_Facebook] Attempting automatic sign-in with Facebook if valid session exists.");
            var preLoginFacebookID = FacebookId;

            // Check if the user is already authenticated with Facebook
            var isFacebookAuthenticated = !string.IsNullOrEmpty(preLoginFacebookID);

            var hasAccessToken = !string.IsNullOrEmpty(AccessToken.CurrentAccessToken?.TokenString ?? string.Empty);
            var isTokenExpired = AccessToken.CurrentAccessToken?.ExpirationTime < DateTime.UtcNow;

            // Check if the Facebook access token exists
            if (!hasAccessToken)
                Debug.Log($"[OnStart_Facebook] No Facebook access token found.");
            else
                Debug.Log($"[OnStart_Facebook] Facebook access token found, expires at \"{AccessToken.CurrentAccessToken?.ExpirationTime}\" UTC.");

            if (isTokenExpired)
                Debug.Log($"[OnStart_Facebook] Facebook access token has expired.");

            // Check if the current session is still active
            if (hasAccessToken && !isTokenExpired)
            {
                Debug.Log($"[OnStart_Facebook] Facebook session still active, proceeding with sign-in.");

                // If the user is not already authenticated with Facebook, sign in with Facebook
                if (!isFacebookAuthenticated)
                {
                    Debug.Log($"[OnStart_Facebook] User not authenticated with Facebook, signing in...");

                    // If the user is not signed in, sign in with Facebook
                    await HandleProcess_AuthManagerProxy
                        (uniTask: () => SignInWithFacebookAsync(AccessToken.CurrentAccessToken.TokenString, "access_token"),
                        taskId: nameof(SignInWithFacebookAsync),
                        shouldIgnoreTryAgainProcess: true);

                    Debug.Log($"[OnStart_Facebook] Facebook sign-in on Start completed successfully.");
                }

                else
                {
                    Debug.Log($"[OnStart_Facebook] User already authenticated with Facebook, skipping sign-in.");

                    // Try to configure the display name using the Facebook display name and request the Facebook profile picture
                    await HandleProcess_AuthManagerProxy(
                        uniTask: () => UniTask.WhenAll
                            (TryToConfigureFacebookDisplayName(), 
                            RequestFacebookProfilePicture()),
                        taskId: "Configure Facebook display name and profile picture at start",
                        shouldIgnoreTryAgainProcess: true);

                    // Once the player is signed in, call the event
                    if (IsUGSAuthenticated)
                        onCompletelySignedIn?.Invoke();
                    else
                        Debug.LogWarning($"[OnStart_Facebook] Sign-In completed but user is not authenticated.");

                    AuthUI?.CallOnFacebookLoginEvent(true);
                    Debug.Log($"[OnStart_Facebook] Facebook display name configuration on Start completed successfully.");
                }

                Debug.Log($"[OnStart_Facebook] Facebook profile picture request on Start completed successfully.");
            }
            else
            { 
                Debug.Log($"[OnStart_Facebook] No active Facebook session found on Start.");
                await SignOut();
            }
        }

        /// <summary>
        /// Specific OnDestroy method for Facebook Authentication <br></br>
        /// </summary>
        private void OnDestroy_Facebook()
        {
            UnHandleOnPressFacebookLoginButton(OpenLoginWithFacebookInterface);
        }
        #endregion

        /// <summary>
        /// Called once Facebook SDK initialization completes
        /// </summary>
        private void OnFacebookInitComplete()
        {
            if (!FB.IsInitialized)
            {
                Debug.LogError("[Facebook] Facebook SDK failed to initialize.");
                return;
            }

            Debug.Log("[Facebook] Facebook SDK initialized successfully.");

            FB.ActivateApp();

            // This is the earliest safe moment to query Facebook session state
            QueryFacebookLoginStatus();
        }

        /// <summary>
        /// Queries Facebook for an existing login session
        /// This call hydrates AccessToken.CurrentAccessToken if a session exists
        /// </summary>
        private void QueryFacebookLoginStatus()
        {
            Debug.Log("[Facebook] Querying Facebook login status...");

            if (!FB.IsInitialized)
            {
                Debug.LogWarning("[Facebook] SDK not initialized while querying login status.");
                return;
            }

            if (AccessToken.CurrentAccessToken != null)
            {
                Debug.Log(
                    $"[Facebook] Active Facebook session found. " +
                    $"Expires at {AccessToken.CurrentAccessToken.ExpirationTime} UTC."
                );
            } 
            else
                Debug.Log("[Facebook] No active Facebook session found.");
        }

        /// <summary>
        /// Opens the Facebook login interface, allowing the user to sign in with their Facebook account.
        /// </summary>
        public async void OpenLoginWithFacebookInterface()
        {
            var preLoginFacebookID = FacebookId;

            // Check if facebook account is already linked to the user account
            if (!string.IsNullOrEmpty(preLoginFacebookID))
            { 
                Debug.Log($"Facebook is already linked");
                return;
            }

            // Check if the current session is still active
            if (AccessToken.CurrentAccessToken != null
                && !string.IsNullOrEmpty(AccessToken.CurrentAccessToken.TokenString)
                && AccessToken.CurrentAccessToken.ExpirationTime > DateTime.UtcNow)
            {
                Debug.Log("Facebook session still active, proceeding with sign-in.");

                // If the user is not signed in, sign in with Facebook
                await HandleProcess_AuthManagerProxy
                    (uniTask: () => SignInWithFacebookAsync(AccessToken.CurrentAccessToken.TokenString, "access_token"), 
                    taskId: nameof(SignInWithFacebookAsync));
            } 
            
            // If the session has expired or does not exist, start login with Facebook
            else
            { 
                Debug.Log("Facebook session expired or missing, logging in again.");

                var isTryingToLogin = true;
                try
                {
                    var loginWithFacebookWrapper = new Func<UniTask>(async () =>
                    {
                        // Start the Facebook login process
                        FB.LogInWithReadPermissions(
                            new List<string> { "public_profile" },
                            LoginWithReadPermission_Callback);

                        // Wait until the login process is finished
                        await UniTask.WaitUntil(() => !isTryingToLogin);
                    });

                    // Use the AuthManagerProxy to handle the Facebook login process
                    await HandleProcess_AuthManagerProxy
                        (uniTask: loginWithFacebookWrapper,
                        taskId: nameof(FB.LogInWithReadPermissions),
                        showLoading: true,
                        shouldIgnoreTryAgainProcess: true,
                        returnExceptionOnError: false,
                        shouldRetrySomeTimes: false,
                        isUsingTimeOut: false);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Facebook Login] Exception during login: {ex.Message}");
                }

                async void LoginWithReadPermission_Callback(ILoginResult result)
                {
                    Debug.Log($"[Facebook Login] Result: {result.RawResult}");
                    if (FB.IsLoggedIn && result.AccessToken != null && !string.IsNullOrEmpty(result.AccessToken.TokenString))
                    {
                        // [Only iOS] Check if the player has authorized tracking
#if UNITY_IOS
                        var wasAuthorized = GetAuthorizationTrackingStatus() is AuthorizationTrackingStatus.AUTHORIZED;
                        Debug.Log($"[Facebook Login] {(wasAuthorized ? "Using standard access token" : "Using Limited Login authentication token")}");
#endif
                        string token;
                        string tokenType;

                        if (result.AuthenticationToken != null)
                        {
                            // Limited Login / ID token
                            token = result.AuthenticationToken.TokenString;
                            tokenType = "id_token";
                        } 
                        else
                        {
                            // Classic Facebook login
                            token = result.AccessToken.TokenString;
                            tokenType = "access_token";
                        }

                        // Proceed with sign-in using the obtained access token
                        await SignInWithFacebookAsync(token, tokenType);
                    } 
                    else
                        Debug.Log("[Facebook Login] User cancelled login from within Facebook flow");

                    isTryingToLogin = false;
                }
            }
        }

        /// <summary>
        /// Directly sign in with Facebook<br></br>
        /// It needs being called from <see cref="HandleProcessesController"/> to handle the possible exceptions
        /// </summary>
        /// <param name="token">The access token obtained from the Facebook login process.</param>
        /// <param name="tokenType">The type of token, either "access_token" or "id_token".</param>
        /// <returns>A UniTask representing the asynchronous sign-in operation.</returns>
        private async UniTask SignInWithFacebookAsync(string token, string tokenType)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
                throw new Exception($"<b>[{nameof(SignInWithFacebookAsync)}]</b> Couldn't be called. Unity services is not initialized");

            // If the user is not signed in, sign in with Facebook
            if (IsUGSAuthenticated)
                await SignOut
                    (isSignOutFromFirebaseToo: false, 
                    isSignInAnonymouslyOnSignOut: false, 
                    isSignOutFromFacebookToo: false);

            try
            {
                // Sign in with Facebook using the token got from the Facebook interface
                await AuthenticationService.Instance.SignInWithFacebookAsync(token);
                Debug.Log("Unity Facebook was Sign-In successfully.");

                // Turn off the authentication UI
                SetActiveAuthUI(false);

                // Try to configure the display name using the Facebook display name and request the Facebook profile picture
                await UniTask.WhenAll
                    (TryToConfigureFacebookDisplayName(), 
                    RequestFacebookProfilePicture());

                // Check if the user is authenticated with Firebase
                await TryToSignUpInFirebase_Facebook(token, tokenType);

                // Once the player is signed in, call the event
                if (IsUGSAuthenticated)
                    onCompletelySignedIn?.Invoke();
                else
                    Debug.LogWarning($"<b>[{nameof(SignInWithFacebookAsync)}]</b> Sign-In completed but user is not authenticated.");

                AuthUI?.CallOnFacebookLoginEvent(true);
            }
            catch (Exception ex) 
            { 
                Exception(ex);

                // IF the user is still authenticated, sign out and authenticate anonymously
                if (IsUGSAuthenticated)
                    await SignOut();

                throw ex;
            }
        }

        /// <summary>
        /// Attempts to sign up or sign in a user to Firebase using Facebook authentication, handling Unity service
        /// initialization, data encryption, and platform-specific authentication steps.
        /// </summary>
        /// <param name="token">The Facebook authentication token.</param>
        /// <param name="tokenType">The type of the Facebook authentication token.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask TryToSignUpInFirebase_Facebook(string token, string tokenType)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(TryToSignUpInFirebase_Facebook)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            var dataToEncrypt = new Dictionary<string, object>()
            {
                ["providerid"] = "facebook",
                ["providerIDToken"] = token,
                ["tokenType"] = tokenType,
            };

            // If the provider url has value, register it to hte data to encrypt
            if (!string.IsNullOrEmpty(ProviderURL))
                dataToEncrypt.Add("optionalIconID", ProviderURL);

            // In some cases is possible that Firebase responses don't contain displayName asigned. Then we send to backend the one that we got from Google
            if (!string.IsNullOrEmpty(providerUsername))
                dataToEncrypt.Add("optionalDisplayName", providerUsername);

            var dataEncrypted = SerializeAndEncryptData(dataToEncrypt);

            try
            {
                // Call the function within the module and provide the parameters we defined in there
                await module.SignInProviderInFirebase(dataEncrypted);
            }
            catch (Exception ex)
            {
                Debug.LogError($"<b>[{nameof(TryToSignUpInFirebase_Facebook)}]</b>Couldn't sign in with provider in firebase\n\n{ex.Message}</color>");
            }

            // Check if the user is authenticated with Firebase in the jslib
            if (IsValidPlatformToUseJSlib() && FirebaseAuth.IsUserAuthenticated() is 0)
            {
                Debug.Log($"<b>[{nameof(TryToSignUpInFirebase_Facebook)}]</b>Trying to authenticate the jslib</color>");

                try
                {
                    // Once the player is signed in, call the event
                    Firebase_JSLibSignInWithIdp(token, tokenType, "facebook");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"<b>[TryToSignInFirebase_Google]</b>Failed to authenticate the jslib</color>\n\nError: {ex.Message}");
                }
            }

            // Update the player info after signing in
            await AuthenticationService.Instance.GetPlayerInfoAsync();
        }

        /// <summary>
        /// Attempts to retrieve and configure the Facebook display name for the authenticated user, updating the Unity
        /// player name if necessary.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask TryToConfigureFacebookDisplayName()
        {
            Debug.Log("[Facebook_AuthManager] Trying to configure Facebook display name...");

            var isGettingDisplayName = true;
            try
            {
                // If the user is not configured yet, configure the user using the Facebook Dis
                FB.API("/me?fields=name", HttpMethod.GET, async result =>
                {
                    if (result.Error == null)
                    {
                        if (string.IsNullOrEmpty(result.RawResult))
                        {
                            Debug.LogError("Failed to get Facebook profile data.");
                            return;
                        }

                        var profileDataCollection = JsonConvert.DeserializeObject<Dictionary<string, object>>(result.RawResult);
                        var displayName = string.Empty;

                        // Check if the profile data collection request obtained somethind
                        if (profileDataCollection != null)
                        { 
                            displayName = profileDataCollection.TryGetValue("name", out var fbDisplayNameObj)
                                ? fbDisplayNameObj.ToString()
                                : default;

                            // Generate a valid username based on the display name and provider UID
                            if (!string.IsNullOrEmpty(displayName) && !IsValidUGSName(displayName))
                                displayName = GenerateUsername(displayName, UUID);
                        }

                        // Try to get the current username
                        providerUsername = await AuthenticationService.Instance.GetPlayerNameAsync();

                        // First at all, check if the facebook name is valid before use it. If not, use UGS cached name.
                        // If the username is not valid or doesn't match the target expected, update it with the Facebook display name
                        if (!string.IsNullOrEmpty(displayName)
                            && (!IsValidUGSName(providerUsername) || !CheckForSameName(providerUsername, displayName)))
                        {
                            Debug.Log($"Facebook - Saving UGS name update from {providerUsername} to {displayName}");
                            providerUsername = await UpdateUnityPlayerName(displayName);

                            Debug.Log($"Facebook new display name configured: {providerUsername}");
                        } 
                        else if (HasExtraId(providerUsername))
                        {
                            Debug.LogWarning($"Facebook - username \"{providerUsername}\" shares root with \"{displayName}\", then is not necessary register the changes in ugs. " +
                                $"But, the first one has extra id that is no necessary to show");

                            providerUsername = displayName;
                        }

                        Debug.Log($"Facebook display name configuration process finished. Current username: {providerUsername}");
                    } 
                    else
                        Debug.LogError("Error getting Facebook profile data: " + result.Error);
                });

                // Wait until the display name request is finished
                await UniTask
                    .WaitUntil(() => !isGettingDisplayName)
                    .TimeoutWithoutException(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Facebook DisplayName] Exception during name request: {ex.Message}");
            }
            finally
            {
                isGettingDisplayName = false;
            }
        }

        /// <summary>
        /// Requests the user's Facebook profile picture, downloads it, and sets the provider icon and URL accordingly.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask RequestFacebookProfilePicture()
        {
            var isGettingPicture = true;

            try
            {
                FB.API("/me/picture?type=large&redirect=false", HttpMethod.GET, async result =>
                {
                    if (result.Error == null && result.ResultDictionary != null &&
                        result.ResultDictionary.TryGetValue("data", out var dataObj))
                    {
                        if (dataObj is Dictionary<string, object> data &&
                            data.TryGetValue("url", out var urlObj))
                        {
                            string photoUrl = urlObj.ToString();
                            Debug.Log($"[Facebook Avatar] URL: {photoUrl}");

                            // Download and set the avatar from the URL
                            var avatar = await DownloadAvatar(photoUrl);

                            // If the avatar was downloaded successfully, set the provider icon and URL. Otherwise, use default values.
                            ProviderURL = avatar ? photoUrl : default;
                            ProviderIcon = avatar;

                            // Mark the picture request as finished
                            isGettingPicture = false;
                        }
                    } else
                        Debug.LogWarning("[Facebook Avatar] Error getting picture: " + result.Error);
                });

                // Wait until the picture request is finished
                await UniTask
                    .WaitUntil(() => !isGettingPicture)
                    .TimeoutWithoutException(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Facebook Avatar] Exception during picture request: {ex.Message}");
            }
            finally
            {
                isGettingPicture = false;
            }
        }

        #region Handlers
        /// <summary>
        /// Invokes the Facebook login button handler on the authentication UI.
        /// </summary>
        /// <param name="onPressFacebookLoginButton">Callback to execute when the Facebook login button is pressed.</param>
        public void HandleOnPressFacebookLoginButton(UnityAction onPressFacebookLoginButton) => AuthUI?.HandleOnPressFacebookLoginButton(onPressFacebookLoginButton);
        
        /// <summary>
        /// Removes a handler for the Facebook login button press event.
        /// </summary>
        /// <param name="onPressFacebookLoginButton">The UnityAction delegate to remove from the Facebook login button press event.</param>
        public void UnHandleOnPressFacebookLoginButton(UnityAction onPressFacebookLoginButton) => AuthUI?.UnHandleOnPressFacebookLoginButton(onPressFacebookLoginButton);

        /// <summary>
        /// Initiates the Facebook login process and invokes a callback upon completion.
        /// </summary>
        /// <param name="onFacebookLogin">Callback invoked with the result of the Facebook login attempt.</param>
        public void HandleOnFacebookLogin(UnityAction<bool> onFacebookLogin) => AuthUI?.HandleOnFacebookLogin(onFacebookLogin);
        
        /// <summary>
        /// Unsubscribes the specified callback from the Facebook login event.
        /// </summary>
        /// <param name="onFacebookLogin">The callback to remove from the Facebook login event.</param>
        public void UnHandleOnFacebookLogin(UnityAction<bool> onFacebookLogin) => AuthUI?.UnHandleOnFacebookLogin(onFacebookLogin);
        #endregion

        #region Events
        /// <summary>
        /// Pauses or resumes the game based on whether the game window is shown.
        /// </summary>
        /// <param name="isGameShown">True if the game is shown; false if it is hidden.</param>
        private void OnHideUnity(bool isGameShown)
        {
            // Pause or resume the game depending on focus
            Time.timeScale = isGameShown ? 1 : 0;
        }
        #endregion
    }
}
