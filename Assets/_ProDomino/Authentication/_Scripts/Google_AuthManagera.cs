using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Events;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Manages authentication processes, primarily handling Google sign-in, session management, and integration with
    /// Unity and Firebase services.
    /// </summary>
    public partial class AuthManager
    {
        // Exclusive Google Auth Manager Firebase consts.
        // Those are use to preserve intact the token made by google.
        // With those the login in firebase from cloud code could be made (iniatialice some cloud code function since there)
        private const string google_id_token = "GoogleIDToken";
        private const string google_token_type = "GoogleTokenType";
        private const string google_token_expiration = "GoogleTokenExpiration";

        private AuthProviderErrorPopUp authProviderErrorPopUp;
        internal AuthProviderErrorPopUp AuthProviderErrorPopUp => authProviderErrorPopUp = authProviderErrorPopUp != null 
            ? authProviderErrorPopUp 
            : FindFirstObjectByType<AuthProviderErrorPopUp>();

        protected bool IsTryingToAuthenticateWithGoogle { get; private set; }


        public string GoogleId 
        {
            get 
            {
                if (!IsAlreadyInitialized)
                    Debug.LogWarning("Could not check Google ID because Unity Services are not initialized yet");

                return IsAlreadyInitialized ? AuthenticationService.Instance.PlayerInfo?.GetGoogleId() : default;
            }
        }

        #region Unity Events - Google
        /// <summary>
        /// Specific Awake method for Google Authentication <br></br>
        /// Try to initialize request for iOS authotization tracking at first,
        /// then check to validate Unity and Google services
        /// </summary>
        private async UniTask OnAwake_Google()
        {
            HandleOnPressGoogleLoginButton(OpenLoginWithGoogleInterface);

            // [Only iOS] Check if the player has authorized tracking
            var wasTrackingAuthorized = await TryToAuthorizeTracking();

            // Just in case, try to initialize Unity services if not already initialized
            if (!IsAlreadyInitialized)
            {
                Debug.Log($"<b>[{nameof(OnAwake_Google)}]</b>  Unity Services not initialized, trying to initialize...");
                await TryToInitializeUnityServices();
            }

            // If Unity services are not initialized, do not continue
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(OnAwake_Google)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }
        }

        /// <summary>
        /// Attempts to automatically sign in the user with Google if a valid session exists and the platform supports
        /// Google authentication.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous sign-in operation.</returns>
        private async UniTask OnStart_Google()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(OnStart_Google)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // Check if the current platform supports Google authentication via JSlib
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning("Current platform does not support Google authentication via JSlib, skipping automatic sign-in.");
                return;
            }

            // Check if the user is not already authenticated anonymously or with other providers
            if (!IsUGSAuthenticated || !string.IsNullOrEmpty(FacebookId) || IsUserAuthenticatedWithCredentials)
            { 
                Debug.LogWarning("User is not authenticated or already authenticated with another provider, skipping Google automatic sign-in.");
                return;
            }

            Debug.Log("Attempting automatic sign-in with Google if valid session exists.");
            var preLoginGoogleID = GoogleId;

            // Check if Google is already authenticated 
            var isGoogleAuthenticated = !string.IsNullOrEmpty(preLoginGoogleID);

            // Define the data we want to retrieve from the module
            var dataEncrypted = SerializeAndEncryptData(new()
            {
                ["keys"] = new string[]
                {
                    google_id_token,
                    google_token_type,
                    google_token_expiration
                }
            });

            // Call the function within the module and provide the parameters we defined in there
            var googleDataJson = await HandleProcess_AuthManagerProxy
                (uniTask: () => module.LoadProtectedData(dataEncrypted).AsUniTask(), 
                taskId: nameof(module.LoadProtectedData));

            // Deserialize and decrypt the data
            var dataResponse = DeserializeAndDecryptData(googleDataJson);
            if (dataResponse == null)
            {
                Debug.LogWarning("Failed to deserialize Google data response. Signing out if already authenticated.");
                await SignOut();

                return;
            }

            // If the response contains data, check if the Google ID token is still valid
            if (dataResponse.Length > 0)
            {
                Debug.Log("Google data found, checking token validity.");

                var googleIdToken = dataResponse.FirstOrDefault(x => x.key is google_id_token)?.value.ToString();
                var googleTokenType = dataResponse.FirstOrDefault(x => x.key is google_token_type)?.value.ToString();
                var googleTokenExpirationString = dataResponse.FirstOrDefault(x => x.key is google_token_expiration)?.value.ToString();
                var googleTokenExpiration = DateTime.Parse(googleTokenExpirationString);

                // Check if the current session is still active
                if (!string.IsNullOrEmpty(googleIdToken) && googleTokenExpiration > DateTime.UtcNow)
                {
                    // If Google is not authenticated, sign in with the cached token
                    if (isGoogleAuthenticated)
                    {
                        Debug.Log($"Unity already authenticated with Google ID: {preLoginGoogleID}, no need to sign in again.");

                        IsTryingToAuthenticateWithGoogle = true;

                        var signInCacheGoogleWrapper = new Func<UniTask>(async () =>
                        {
                            // Get the name of the current game object to be used as a callback target
                            FirebaseAuth.GetUserProfile(googleIdToken,
                               name,
                               nameof(OnSignUsingGoogleInterfaceSuccessfully),
                               nameof(OnSignUsingGoogleInterfaceFailed));

                            // Wait until the authentication process is completed
                            await UniTask.WaitUntil(() => !IsTryingToAuthenticateWithGoogle);
                        });

                        // Sign in with cached Google token
                        await HandleProcess_AuthManagerProxy(
                            uniTask: signInCacheGoogleWrapper,
                            taskId: nameof(FirebaseAuth.GetUserProfile),
                            showLoading: true,
                            shouldIgnoreTryAgainProcess: true,
                            returnExceptionOnError: false);

                        Debug.Log("Unity Google profile fetched successfully using cached token.");
                    }

                    isGoogleAuthenticated = true;
                } 
                else
                { 
                    Debug.Log("Google token is missing or expired, cannot sign in automatically.");
                    await SignOut();
                }
            } 
            else
            { 
                Debug.Log("No Google data found for automatic sign-in.");
                await SignOut();
            }
        }

        /// <summary>
        /// Specific OnDestroy method for Google Authentication <br></br>
        /// </summary>
        private void OnDestroy_Google()
        {
            UnHandleOnPressGoogleLoginButton(OpenLoginWithGoogleInterface);
        }
        #endregion

        /// <summary>
        /// Opens the Google login interface for the user to sign in.
        /// </summary>
        public async void OpenLoginWithGoogleInterface()
        {
            // If the session has expired or does not exist, start login with Google
            Debug.Log("Google session expired or missing, check to logging again.");
            if (IsValidPlatformToUseJSlib() && FirebaseAuth.IsUserAuthenticated() is 0)
            {
                Debug.Log("User is not authenticated, starting Google login.");

                IsTryingToAuthenticateWithGoogle = true;

                var loginWithGoogleWrapper = new Func<UniTask>(async () => 
                {
                    // Get the name of the current game object to be used as a callback target
                    FirebaseAuth.SignInWithGoogle
                        (name,
                        nameof(OnSignUsingGoogleInterfaceSuccessfully),
                        nameof(OnSignUsingGoogleInterfaceFailed));

                    // Wait until the authentication process is completed
                    await UniTask.WaitUntil(() => !IsTryingToAuthenticateWithGoogle);
                });

                // Open a loading screen until the emergent window is closed. Once the window is closed, the loading screen should be closed as well after a second (waiting until the data is cleared).
                await HandleProcess_AuthManagerProxy(
                    uniTask: loginWithGoogleWrapper,
                    taskId: nameof(FirebaseAuth.SignInWithGoogle),
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true,
                    returnExceptionOnError: false,
                    shouldRetrySomeTimes: false,
                    isUsingTimeOut: false);
            }
            else
                Debug.LogError("User is already authenticated, cannot start Google login.");
        }

        /// <summary>
        /// Attempts to sign in to Firebase using Google credentials, handling encryption, provider data, and
        /// platform-specific authentication.
        /// </summary>
        /// <param name="token">The Google ID token used for authentication.</param>
        /// <param name="tokenType">The type of the token provided for authentication.</param>
        /// <returns>A UniTask representing the asynchronous sign-in operation.</returns>
        private async UniTask TryToSignInFirebase_Google(string token, string tokenType)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[TryToSignInFirebase_Google]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(tokenType))
            { 
                Debug.LogWarning($"<b>[TryToSignInFirebase_Google]</b> Couldn't be called. Token or tokentype doesn't have value");
                return;
            }

            var dataToEncrypt = new Dictionary<string, object>()
            {
                ["providerid"] = "google",
                ["providerIDToken"] = token,
                ["tokenType"] = tokenType
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
                Debug.LogError($"<b>[TryToSignInFirebase_Google]</b>Couldn't sign in with provider in firebase\n\n{ex.Message}</color>");
            }

            // Check if the user is authenticated with Firebase in the jslib
            if (IsValidPlatformToUseJSlib() && FirebaseAuth.IsUserAuthenticated() is 0)
            {
                Debug.Log($"<b>[TryToSignInFirebase_Google]</b>Trying to authenticate the jslib</color>");

                try
                {
                    // Once the player is signed in, call the event
                    Firebase_JSLibSignInWithIdp(token, tokenType, "google");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"<b>[TryToSignInFirebase_Google]</b>Failed to authenticate the jslib</color>\n\nError: {ex.Message}");
                }
            }

            // Update the player info after signing in
            await AuthenticationService.Instance.GetPlayerInfoAsync();
        }

        #region Handlers
        /// <summary>
        /// Invokes the Google login button press handler on the AuthUI component.
        /// </summary>
        /// <param name="onPressGoogleLoginButton">Callback to execute when the Google login button is pressed.</param>
        public void HandleOnPressGoogleLoginButton(UnityAction onPressGoogleLoginButton) => AuthUI?.HandleOnPressGoogleLoginButton(onPressGoogleLoginButton);
       
        /// <summary>
        /// Removes a handler for the Google login button press event.
        /// </summary>
        /// <param name="onPressGoogleLoginButton">The UnityAction delegate to remove from the event.</param>
        public void UnHandleOnPressGoogleLoginButton(UnityAction onPressGoogleLoginButton) => AuthUI?.UnHandleOnPressGoogleLoginButton(onPressGoogleLoginButton);

        /// <summary>
        /// Initiates the Google login process and invokes a callback upon completion.
        /// </summary>
        /// <param name="onGoogleLogin">Callback invoked with the result of the Google login attempt.</param>
        public void HandleOnGoogleLogin(UnityAction<bool> onGoogleLogin) => AuthUI?.HandleOnGoogleLogin(onGoogleLogin);
        
        /// <summary>
        /// Unsubscribes the specified callback from the Google login event.
        /// </summary>
        /// <param name="onGoogleLogin">The callback to remove from the Google login event handler.</param>
        public void UnHandleOnGoogleLogin(UnityAction<bool> onGoogleLogin) => AuthUI?.UnHandleOnGoogleLogin(onGoogleLogin);
        #endregion

        #region Events
        /// <summary>
        /// Handles successful Google sign-in by deserializing the provided data, authenticating with Unity and Firebase
        /// services, updating user profile information, and saving authentication data securely.
        /// </summary>
        /// <param name="dataJson">A JSON string containing Google sign-in data such as tokens, display name, and photo URL.</param>
        public async void OnSignUsingGoogleInterfaceSuccessfully(string dataJson)
        { 
            if (string.IsNullOrEmpty(dataJson))
            {
                Debug.LogError("Google sign-in interface returned empty data.");
                IsTryingToAuthenticateWithGoogle = false;
                return;
            }

            Debug.Log("Google sign-in interface completed successfully.");

            var dataCollection = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
            if (dataCollection is null or { Count: 0 })
            {
                Debug.LogError("Failed to deserialize Google data collection.");
                IsTryingToAuthenticateWithGoogle = false;
                return;
            }

            // Get the relevant data from the collection, using default values if keys are missing
            var idToken = dataCollection.TryGetValue("idToken", out var googleTokenObj)
                ? googleTokenObj.ToString()
                : default;
            
            var refreshToken = dataCollection.TryGetValue("refreshToken", out var googleRefreshTokenObj)
                ? googleRefreshTokenObj.ToString()
                : default;
            
            var tokenType = dataCollection.TryGetValue("tokenType", out var googleTokenTypeObj)
                ? googleTokenTypeObj.ToString()
                : default;

            var displayName = dataCollection.TryGetValue("displayName", out var googleDisplayNameObj)
                ? googleDisplayNameObj.ToString()
                : default;
            
            var photoURL = dataCollection.TryGetValue("photoURL", out var googlePhotoURLObj)
                ? googlePhotoURLObj.ToString()
                : default;

            var tokenExpiration = default(DateTime);
            if (dataCollection.TryGetValue("expToken", out var googleTokenExpirationObj))
            {
                if (long.TryParse(googleTokenExpirationObj.ToString(), out long unixTimestamp))
                {
                    tokenExpiration = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp).UtcDateTime;
                    Debug.Log($"Expiration date parsed successfully: {tokenExpiration}");
                } 
                else
                    Debug.LogError($"Invalid timestamp format: {googleTokenExpirationObj}");
            }

            // Check if the ID token and expiration date are valid
            if (!string.IsNullOrEmpty(idToken) && tokenExpiration > DateTime.UtcNow)
            {
                Debug.Log("Token is not null. Proceding to configur cloud code");

                // If the user is not signed in, sign in with google
                if (IsUGSAuthenticated)
                    await SignOut(isSignOutFromFirebaseToo: false, isSignInAnonymouslyOnSignOut: false);

                try
                {
                    // Sign in with Google in Unity Authentication
                    await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
                    Debug.Log($"<b>[SignInWithGoogleAsync]</b> Unity SignIn is successful.");

                    await PrepareProviderData();

                    //  Try to sign up/in in Firebase with Google token
                    await TryToSignInFirebase_Google(idToken, tokenType);
                    Debug.Log($"<b>[SignInWithGoogleAsync]</b> Firebase SignIn is successful.");

                    SetActiveAuthUI(false);

                    // Once the player is signed in, call the event
                    if (IsUGSAuthenticated)
                        onCompletelySignedIn?.Invoke();
                    else
                        Debug.LogWarning($"<b>[SignInWithGoogleAsync]</b> Sign-in completed but user is not authenticated.");

                    AuthUI?.CallOnGoogleLoginEvent(true);
                }
                catch (Exception ex)
                {
                    Exception(ex);

                    // IF the user is still authenticated, sign out and authenticate anonymously
                    if (IsUGSAuthenticated)
                        await SignOut();

                    throw;
                }

                // Save the expiration date in the module
                var dataToSave = SerializeAndEncryptData(new()
                {
                    [google_id_token] = idToken,
                    [google_token_type] = tokenType,
                    [google_token_expiration] = tokenExpiration.ToString()
                });

                await module.SaveProtectedData(dataToSave);
                Debug.Log("Google data saved successfully.");
            } 
            
            else
                Debug.LogWarning($"Google data is null or expired.");

            IsTryingToAuthenticateWithGoogle = false;

            async UniTask PrepareProviderData()
            {
                // Download the avatar if the URL is available
                if (!string.IsNullOrEmpty(photoURL))
                { 
                    var avatar = await DownloadAvatar(photoURL);

                    // If the avatar was downloaded successfully, set the provider icon and URL. Otherwise, use default values.
                    ProviderURL = avatar ? photoURL : default;
                    ProviderIcon = avatar;
                }

                // Generate a valid username based on the display name and provider UID
                if (!string.IsNullOrEmpty(displayName) && !IsValidUGSName(displayName))
                    displayName = GenerateUsername(displayName, UUID);

                // Try to get the current username
                providerUsername = await AuthenticationService.Instance.GetPlayerNameAsync();

                // First at all, check if the Google name is valid before use it. If not, use UGS cached name.
                // If the username is not valid or doesn't match the target expected, update it with the Google display name
                if (!string.IsNullOrEmpty(displayName)
                    && (!IsValidUGSName(providerUsername) || !CheckForSameName(providerUsername, displayName)))
                {
                    Debug.Log($"Saving UGS name update from {providerUsername} to {displayName}");
                    providerUsername = await UpdateUnityPlayerName(displayName);

                    Debug.Log($"Google new display name configured: {providerUsername}");
                } else if (HasExtraId(providerUsername))
                {
                    Debug.LogWarning($"Google - username \"{providerUsername}\" shares root with \"{displayName}\", then is not necessary register the changes in ugs. " +
                        $"But, the first one has extra id that is no necessary to show");

                    providerUsername = displayName;
                }

                Debug.Log($"Google display name configuration process finished. Current username: {providerUsername}");
            }
        }

        /// <summary>
        /// Handles failures during Google sign-in by parsing the error response, displaying appropriate error messages,
        /// and performing sign-out if necessary.
        /// </summary>
        /// <param name="response">The JSON-formatted error response received from the Google sign-in attempt.</param>
        public async void OnSignUsingGoogleInterfaceFailed(string response)
        {
            Debug.LogError($"Failed to sign in with Google: {response}");

            // Try parse the JSON coming from JS
            Dictionary<string, object> data = null;

            try
            {
                data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not parse JSON fallback response: {ex.Message}");
            }

            string errorCode = data != null && data.ContainsKey("error")
                ? data["error"]?.ToString()
                : null;

            // CASE 1: Google sign-in Å® email == null Å® provider conflict
            if (errorCode == "ACCOUNT_EXISTS_WITH_DIFFERENT_PROVIDER")
            {
                Debug.LogWarning("This Google account is linked to another auth provider.");

                SetActiveAuthUI(false);
                AuthProviderErrorPopUp.SetActive(true, AuthProviderErrorPopUp.GoogleAuthErrorType.AccountExistsWithDifferentProvider);

                if (IsUGSAuthenticated && !string.IsNullOrEmpty(GoogleId))
                    await SignOut();

                IsTryingToAuthenticateWithGoogle = false;
                return;
            }

            // CASE 2: Firebase's native "different credential" error
            if (errorCode == "ACCOUNT_EXISTS_WITH_DIFFERENT_CREDENTIAL")
            {
                Debug.LogWarning("Google login blocked: account exists with another provider.");

                SetActiveAuthUI(false);
                AuthProviderErrorPopUp.SetActive(true, AuthProviderErrorPopUp.GoogleAuthErrorType.AccountExistsWithDifferentCredential);

                if (IsUGSAuthenticated && !string.IsNullOrEmpty(GoogleId))
                    await SignOut();

                IsTryingToAuthenticateWithGoogle = false;
                return;
            }

            // CASE 3: Duplicate password account
            if (errorCode == "DUPLICATE_ACCOUNT")
            {
                Debug.LogWarning("Google login cancelled (duplicate account).");

                SetActiveAuthUI(false);
                AuthProviderErrorPopUp.SetActive(true, AuthProviderErrorPopUp.GoogleAuthErrorType.DuplicatedAccount);

                if (IsUGSAuthenticated && !string.IsNullOrEmpty(GoogleId))
                    await SignOut();

                IsTryingToAuthenticateWithGoogle = false;
                return;
            }

            // CASE 4: Unhandled errors
            Debug.LogError("Unhandled Google login error.");

            if (IsUGSAuthenticated && !string.IsNullOrEmpty(GoogleId))
                await SignOut();

            IsTryingToAuthenticateWithGoogle = false;
        }
        #endregion
    }
}
