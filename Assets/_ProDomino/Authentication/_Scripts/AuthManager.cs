using Cysharp.Threading.Tasks;
using Facebook.Unity;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.HandleProcessesSystem;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Timba.Patterns;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using static HelperSharedLibrary.ExceptionHelper;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Manages authentication for Unity Gaming Services and Firebase, handling sign-in, sign-out, session management,
    /// username validation, and provider integration. Supports anonymous and provider-based authentication, event
    /// handling, and secure data operations.
    /// </summary>
    public partial class AuthManager : SingleInstanceMonoBehaviour<AuthManager>, IService
    {
        [Tooltip("Is necessary that Unity Services are fully initialized before trying to usign them. " +
            "Due that, enable this testing to wait for 10s if there are process no waiting for this initialization be done")]
        [SerializeField] private bool isTestingUnityServicesCalls;

        [Tooltip("If enabled, an extra ID will be generated and appended to the username obtained from the provider to ensure its uniqueness")]
        [SerializeField] private bool generateExtraIDForUsername = true;

        [Tooltip("Event called when the player is completely signed in (UGS + Firebase if applicable)")]
        [SerializeField] private UnityEvent onCompletelySignedIn;

        private HandleProcessesController handleProcessesController;

        // Exclusive Auth Manager Firebase consts
        // With this you could login in firebase from cloud code (iniatialice some cloud code function since there)
        private const string firebase_refresh_token = "FirebaseRefreshToken";

        /// <summary>
        /// This field is used to store the Facebook or google provider displayName
        /// </summary>
        private string providerUsername;

        /// <summary>
        /// This property is used to check if the Unity services are initialized <br></br>
        /// </summary>
        public bool IsAlreadyInitialized { get; private set; }

        /// <summary>
        /// This property is used to check if the player is authenticated with Unity Gaming Services (UGS) <br></br>
        /// Usually, this means that the player is authenticated anonimously <br></br>
        /// </summary>
        public bool IsUGSAuthenticated
        {
            get
            {
                try
                {
                    if (!IsAlreadyInitialized || AuthenticationService.Instance == null)
                        Debug.LogWarning("Could not check login status because Unity Services are not initialized yet");

                    return IsAlreadyInitialized && AuthenticationService.Instance.IsSignedIn;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not check login status because Unity Services are not initialized yet: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// This property is used to definy if the player will be signed in automatically when the player signs out <br></br>
        /// When the player is signed out, its value will set as false to prevent the player to be signed in again automatically <br></br>
        /// </summary>
        public bool WillSignInAnonymouslyOnSignOut { get; private set; }

        /// <summary>
        /// This property is used to check if the player will sign out from Firebase when he is signing out from UGS<br></br>
        /// </summary>
        public bool WillSignOutFromFirebaseToo { get; private set; }

        /// <summary>
        /// This property is used to check if the player is authenticated with Credentials or a provider (Facebook, Google, Apple, etc.) <br></br>
        /// </summary>
        public bool IsUserAuthenticatedWithProvider => IsAlreadyInitialized 
            && IsUGSAuthenticated is true
            && (!string.IsNullOrEmpty(FacebookId) || !string.IsNullOrEmpty(GoogleId));

        /// <summary>
        /// This property is used to check if the player is authenticated with Credentials (email and password)
        /// </summary>
        public bool IsAuthenticated => IsUGSAuthenticated is true
            && (IsUserAuthenticatedWithCredentials || IsUserAuthenticatedWithProvider);
        
        /// <summary>
        /// This property is used to check if the player is authenticated with Credentials (email and password)
        /// </summary>
        public bool IsAuthenticatedAndVerified => IsAuthenticated
            && (!IsUserAuthenticatedWithCredentials || IsEmailVerified);

        /// <summary>
        /// This is the username of the player auth account
        /// </summary>
        public PlayerInfo PlayerInfo => IsAlreadyInitialized ? AuthenticationService.Instance?.PlayerInfo : default;
        public string Username => IsAlreadyInitialized 
            ? IsUserAuthenticatedWithProvider 
                ? providerUsername
                : AuthenticationService.Instance?.PlayerInfo?.Username 
            : default;
        public string UUID => IsAlreadyInitialized ? AuthenticationService.Instance?.PlayerInfo?.Id : default;
        public Sprite ProviderIcon { get; private set; }
        public string ProviderURL { get; private set; }


        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        private AuthUI _authUI;
        internal AuthUI AuthUI
        {
            get
            {
                if (_authUI == null)
                {
                    _authUI = FindFirstObjectByType<AuthUI>();
                    if (_authUI == null)
                        Debug.LogWarning($"{nameof(Authentication.AuthUI)} not found in the scene");
                }

                return _authUI;
            }
        }

        protected async override void Awake()
        {
            base.Awake();

            handleProcessesController = ServiceLocator.Instance.GetService<HandleProcessesController>();

            // First, try to initialize Unity services
            await HandleProcess_AuthManagerProxy(TryToInitializeUnityServices, nameof(TryToInitializeUnityServices));

            // Wait until the game manager has finished its Awake process
            // The secuence is: GameManager.Awake -> AuthManager.Awake (initialize UGS) -> GameManager.Awake (continue) -> AuthManager Cached Login
            await HandleProcess_AuthManagerProxy(() => UniTask.WaitForSeconds(1), "Waiting", shouldIgnoreTryAgainProcess: true);

            // If Unity services are not initialized, do not continue
            if (!IsAlreadyInitialized)
            {
                Debug.LogError("Unity Services could not be initialized, AuthManager will not work properly");
                return;
            }

            // Register simply events that notify the player about the authentication state
            HandleCommonAuthHandlers();

            // Call specific OnAwake methods for each authentication method
            await HandleProcess_AuthManagerProxy(OnAwake_Credentials, nameof(OnAwake_Credentials));
            await HandleProcess_AuthManagerProxy(OnAwake_Facebook, nameof(OnAwake_Facebook));
            await HandleProcess_AuthManagerProxy(OnAwake_Google, nameof(OnAwake_Google));

            // Try to sign in using the cached user
            var isSignedInUsingCachedUser = await HandleProcess_AuthManagerProxy
                (uniTask: TryToSignInCachedUserAsync,
                taskId: nameof(TryToSignInCachedUserAsync),
                showLoading: true,
                shouldIgnoreTryAgainProcess: true,
                resultValidator: _result => IsAuthenticatedAndVerified);

            // Check if the user is already signed in using the cached user; if not, sign in anonimously
            if (!isSignedInUsingCachedUser)
            {
                Debug.LogWarning("The player is not signed in using cached user, signing in anonimously");
                await HandleProcess_AuthManagerProxy
                    (uniTask: SignInAnonmously, 
                    taskId: nameof(SignInAnonmously),
                    showLoading: true,
                    resultValidator: () => IsUGSAuthenticated);
            }

            AuthUI?.Awake_AuthUI();
        }

        protected async virtual void Start()
        {
            await UniTask.WaitUntil(() => IsAlreadyInitialized && IsUGSAuthenticated);
            Debug.Log("Starting AuthManager...");

            OnStart_Credentials();

            // Try to sign in with Google and Facebook if necessary usign cached data
            await UniTask.WhenAll(
                OnStart_Google(), 
                OnStart_Facebook());

            AuthUI?.Start_AuthUI();
        }

        protected virtual void Update()
        {
            if (!IsAlreadyInitialized)
                return;

            AuthUI?.Update_AuthUI();
        }

        private void OnDestroy()
        {
            // Specific OnDestroy method for Authentication sub-classes
            OnDestroy_Facebook();
            OnDestroy_Credentials();
            OnDestroy_Google();

            AuthUI?.OnDestroy_AuthUI();

            // Try to sign out from Firebase if the player is authenticated with it
            if (PlayerPrefs.GetInt("IsRememberPassword", 0) is 0)
                SignOut(isSignOutFromFirebaseToo: true, isSignInAnonymouslyOnSignOut: false).Forget();
        }

        /// <summary>
        /// This is the Auth version of the same used in GameManager that could't be used due to the dependency of Unity Services
        /// </summary>
        /// <returns></returns>
        private bool IsValidPlatformToUseJSlib() => Application.platform is RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// Activates or deactivates the authentication UI.
        /// </summary>
        /// <param name="isActive">True to activate the authentication UI; false to deactivate it.</param>
        public void SetActiveAuthUI(bool isActive) => AuthUI?.SetActive(isActive);

        /// <summary>
        /// Attempts to authorize tracking on iOS, prompting the user if necessary.
        /// </summary>
        /// <returns>True if tracking authorization was denied on iOS; otherwise, false on iOS or true on other platforms.</returns>
        private async UniTask<bool> TryToAuthorizeTracking()
        {
#if UNITY_IOS
            // This will prompt the player to consent to tracking when the app is first launched.
            // Only include this line here if it is not otherwise requested already.
            var atStatus = GetAuthorizationTrackingStatus();
            if ((atStatus = GetAuthorizationTrackingStatus()) is not AuthorizationTrackingStatus.AUTHORIZED)
            {
                RequestAuthorizationTracking();
                await UniTask.WaitUntil(() => (atStatus = GetAuthorizationTrackingStatus())
                    is AuthorizationTrackingStatus.AUTHORIZED
                    or AuthorizationTrackingStatus.DENIED);

                var wasAuthorized = atStatus is AuthorizationTrackingStatus.AUTHORIZED;
                var wasDenied = atStatus is AuthorizationTrackingStatus.DENIED;

                Debug.LogWarning($"Authorization to Tracking iOS data was <color={(wasAuthorized ? "green" : wasDenied ? "red" : "yellow")}>{atStatus}</color>");

                // If the user denied tracking, do not initialize Unity services
                return wasDenied;
            }
            return false;
#else
            await UniTask.CompletedTask;
            return true;
#endif
        }

        /// <summary>
        /// It needs being called from <see cref="HandleProcessesController"/> to handle the possible exceptions
        /// </summary>
        /// <returns></returns>
        private async UniTask TryToInitializeUnityServices()
        {
            // If Unity services are not initialized, initialize them
            if (UnityServices.Instance is null or { State: not ServicesInitializationState.Initialized })
            {
                // Wait for a while to make sure there aren't process called before the unity services are completely initialized
                if (isTestingUnityServicesCalls)
                    await UniTask.WaitForSeconds(10);

                // Initialize Unity services
                await UnityServices.InitializeAsync();
                var unityStatus = UnityServices.Instance.State;
                Debug.Log($"Unity services initialization: {unityStatus}");

                var wasSuccess = unityStatus is ServicesInitializationState.Initialized;
                var wasFail = unityStatus is ServicesInitializationState.Uninitialized;

                IsAlreadyInitialized = wasSuccess;
                Debug.Log($"<b>Unity</b> is <color={(wasSuccess ? "green" : wasFail ? "red" : "yellow")}>{unityStatus}</color>");
            }
        }

        /// <summary>
        /// It needs being called from <see cref="HandleProcessesController"/> to handle the possible exceptions
        /// </summary>
        private async UniTask<bool> TryToSignInCachedUserAsync()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(TryToSignInCachedUserAsync)}]</b> Couldn't be called. Unity services is not initialized");
                return false;
            }

            // Check if a cached player already exists by checking if the session token exists
            if (!AuthenticationService.Instance.SessionTokenExists)
            {
                // if not, then do nothing
                return false;
            }
            Debug.Log("Player has already sign-in session token");

            // Try to sign in the cached player
            try
            {
                await SignInAnonmously();
            }
            catch (AuthenticationException ex)
            {
                // If the error code refers to an invalid session token, throw an exception; Otherwise, just return
                if (ex.ErrorCode != AuthenticationErrorCodes.InvalidSessionToken)
                    throw;
                else
                    return false;
            }

            // Remove the session token if the user is not remembering the password
            if (IsUGSAuthenticated && IsUserAuthenticatedWithCredentials && PlayerPrefs.GetInt("IsRememberPassword", 0) is 0)
            { 
                Debug.LogWarning("Removing session token because the user is not remembering the password");
                await SignOut(isSignOutFromFirebaseToo: true, isSignInAnonymouslyOnSignOut: false);
            }

            // Check if the user is authenticated but no yet with Firebase
            if (IsAuthenticated
                && IsValidPlatformToUseJSlib()
                && FirebaseAuth.IsUserAuthenticated() is 0)
            {
                // Check if the user is authenticated with Firebase in the jslib
                Debug.Log($"<b>[{nameof(TryToSignInCachedUserAsync)}]</b>Trying to authenticate the jslib</color>");

                // Try to handle some exception that client could see
                try
                {
                    // Call the function within the module and provide the parameters we defined in there
                    await module.SignInIDTokenInFirebase();
                    var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();
                }
                catch (FirebaseException ex)
                {
                    var possibleErrorMessage = new string[]
                    {
                        "INVALID_ID_TOKEN",
                        "TOKEN_EXPIRED",
                        "USER_DISABLED",
                        "EMAIL_NOT_FOUND",
                        "INVALID_PASSWORD"
                    };
                    if (ex.errorResponse is null or { code: not 400 }
                        or { message: not string }
                        || !possibleErrorMessage.Contains(ex.errorResponse.message))
                        throw ex;
                }

                try
                {
                    // Once the player is signed in, call the event
                    await Firebase_JSLibSignInWithRefreshToken();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"<b>[{nameof(TryToSignInCachedUserAsync)}]</b>Failed to authenticate the jslib</color>\n\nError: {ex.Message}");
                }
            }

            // If the user is authenticated, check if the user has logged in today to update the days streak
            if (IsAuthenticated)
            {
                Debug.Log("Checking if the player has logged in today to update the days streak");

                var dataEncrypted = SerializeAndEncryptData(new()
                {
                    ["keys"] = new string[]
                    {
                        "Analytics"
                    }
                });

                var loadDataRequest = await module.LoadProtectedData(dataEncrypted);
                if (loadDataRequest != null)
                {
                    var responseData = DeserializeAndDecryptData(loadDataRequest);
                    if (responseData != null && responseData.Length > 0)
                    {
                        var analyticsData = responseData.FirstOrDefault(x => x.key is "Analytics")?.value?.ToObject<AnalyticsData>();
                        if (analyticsData != null)
                        {
                            var currentDate = DateTime.UtcNow.Date;

                            // Check if the last login date is today
                            if (analyticsData.lastLoginDate.Date != currentDate)
                            { 
                                await module.UpdateDaysStreak();
                                Debug.Log("The player has logged in today, updating the last login date");
                            }
                            else
                                Debug.Log("The player has already logged in today");
                        } else
                            Debug.LogWarning("Could not find Analytics data");
                    } else
                        Debug.LogWarning("Could not deserialize and decrypt the generic data");
                } else
                    Debug.LogWarning("Could not load the protected data");
            }

            // Check if the player is signed in
            Debug.Log($"The player is {(IsUGSAuthenticated ? "Correctly" : "not")} signed in anonimously");

            // Once the player is signed in, call the event
            if (IsUGSAuthenticated)
                onCompletelySignedIn?.Invoke();
            else
                Debug.LogWarning("The player could not be signed in using the cached user");

            return IsUGSAuthenticated;
        }

        /// <summary>
        /// It needs being called from <see cref="HandleProcessesController"/> to handle the possible exceptions
        /// </summary>
        /// <returns></returns>
        private async UniTask SignInAnonmously()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[SignInAnonmously]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // This call will sign in the cached player.
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // Once the player is signed in, call the event
            if (IsUGSAuthenticated)
                onCompletelySignedIn?.Invoke();
            else
                Debug.LogWarning("<b>[SignInAnonmously]</b> The player could not be signed in anonimously");

            Debug.Log($"Sign in anonymously succeeded!\nPlayerID: {AuthenticationService.Instance.PlayerId}");
        }

        /// <summary>
        /// Updates the Unity player name using the provided display name and returns the new name if successful.
        /// </summary>
        /// <param name="currentDisplayName">The current display name of the player.</param>
        /// <returns>The updated player name, or the original display name if the update fails.</returns>
        private async UniTask<string> UpdateUnityPlayerName(string currentDisplayName)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(UpdateUnityPlayerName)}]</b> Couldn't be called. Unity services is not initialized");
                return currentDisplayName;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogError($"<b>[{nameof(UpdateUnityPlayerName)}]</b> User not authenticated in UGS.");
                return currentDisplayName;
            }

            try
            {
                // Generate a valid username based on the display name and provider UID
                var updatedName = GenerateUsername(currentDisplayName, UUID);

                // Register the new username in UGS
                var newName = await AuthenticationService.Instance.UpdatePlayerNameAsync(updatedName);
                if (string.IsNullOrEmpty(newName))
                {
                    Debug.LogError("Failed to update player name in UGS.");
                    return currentDisplayName;
                }

                // Removes possible extra id
                newName = GenerateUsername(newName, UUID);

                Debug.Log($"Player name updated from: {currentDisplayName}, to: {newName}");
                await AuthenticationService.Instance.GetPlayerInfoAsync();

                return newName;
            }
            catch (AuthenticationException e)
            {
                Debug.LogError($"Error to update the player name in UGS: {e.ErrorCode}\n\n{e.Message}");
                return currentDisplayName;
            }
        }

        /// <summary>
        /// Signs out the user from Unity Gaming Services, optionally from Firebase and Facebook, deletes anonymous
        /// accounts if necessary, and clears session tokens.
        /// </summary>
        /// <param name="isSignOutFromFirebaseToo">Indicates whether to sign out from Firebase as well.</param>
        /// <param name="isSignInAnonymouslyOnSignOut">Indicates whether to sign in anonymously after signing out.</param>
        /// <param name="isSignOutFromFacebookToo">Indicates whether to sign out from Facebook as well.</param>
        /// <param name="isForcingDeleteAccount">Specifies whether to force deletion of the account, particularly for anonymous users.</param>
        /// <returns>A UniTask representing the asynchronous sign-out operation.</returns>
        public async UniTask SignOut
            (bool isSignOutFromFirebaseToo = true, 
            bool isSignInAnonymouslyOnSignOut = true, 
            bool isSignOutFromFacebookToo = true,
            bool? isForcingDeleteAccount = null)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(SignOut)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (!IsUGSAuthenticated)
            {
                Debug.LogWarning("The player is not authenticated, so there is no need to sign out");
                return;
            }

            Debug.Log("Signing out...");

            WillSignInAnonymouslyOnSignOut = isSignInAnonymouslyOnSignOut;
            WillSignOutFromFirebaseToo = isSignOutFromFirebaseToo;

            var wasSignedUsingCredentials = IsUserAuthenticatedWithCredentials;
            var wasSignedUsingProvider = IsUserAuthenticatedWithProvider;

            // If the player is not authenticated with Credentials or Provider, delete the anonymous account to avoid orphaned accounts
            if (!IsUserAuthenticatedWithCredentials
                && !IsUserAuthenticatedWithProvider 
                && (!isForcingDeleteAccount.HasValue || isForcingDeleteAccount.Value))
            {
                Debug.Log("Deleting UGS account Deleting the anonymous account. Use this method to avoid orphaned accounts.");

                // TRy to call the delete account process
                try
                {
                    await HandleProcess_AuthManagerProxy
                        (uniTask: () => AuthenticationService.Instance.DeleteAccountAsync().AsUniTask(),
                        taskId: nameof(AuthenticationService.Instance.DeleteAccountAsync),
                        showLoading: true,
                        shouldIgnoreTryAgainProcess: false,
                        returnExceptionOnError: true,
                        shouldRetrySomeTimes: true,
                        isUsingTimeOut: true);

                    Debug.Log("Account deleted successfully from UGS.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error deleting account from UGS: \n\n{ex.Message}");
                    throw;
                }
            }

            // Simply sign out from UGS (when the player is authenticated with Credentials or Provider)
            try
            {
                Debug.Log("Signing out from UGS Clearing the session token.");
                AuthenticationService.Instance.SignOut(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error signing out from UGS: \n\n{ex.Message}");
                throw;
            }

            // For security reasons, clear the session token too
            if (AuthenticationService.Instance.SessionTokenExists)
            { 
                AuthenticationService.Instance.ClearSessionToken();
                Debug.Log("Session token cleared.");
            }

            // Sign out from Facebook too if applicable
            if (isSignOutFromFacebookToo && FB.IsLoggedIn)
            { 
                FB.LogOut();
                Debug.Log("Player signed out from Facebook.");
            }

            // Clear provider data
            ProviderIcon = default;
            ProviderURL = default;
            providerUsername = default;
        }

        /// <summary>
        /// Logs the provided exception and triggers a failed Facebook login event.
        /// </summary>
        /// <param name="ex">The exception to log and handle.</param>
        private void Exception(Exception ex)
        {
            Debug.LogException(ex);
            AuthUI?.CallOnFacebookLoginEvent(false);
        }

        #region Helpers
        /// <summary>
        /// Executes an authentication-related asynchronous task with optional loading UI, retry, timeout, and result
        /// validation features.
        /// </summary>
        /// <param name="uniTask">The asynchronous task to execute.</param>
        /// <param name="taskId">An optional identifier for the task.</param>
        /// <param name="showLoading">Indicates whether to display a loading screen during execution.</param>
        /// <param name="shouldIgnoreTryAgainProcess">Specifies whether to bypass the retry prompt on failure.</param>
        /// <param name="returnExceptionOnError">Determines if exceptions should be returned on error.</param>
        /// <param name="shouldRetrySomeTimes">Indicates whether the task should be retried on failure.</param>
        /// <param name="isUsingTimeOut">Specifies if a timeout should be applied to the task.</param>
        /// <param name="resultValidator">An optional function to validate the task result.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        public async UniTask HandleProcess_AuthManagerProxy
            (Func<UniTask> uniTask, 
            string taskId = null, 
            bool showLoading = true, 
            bool shouldIgnoreTryAgainProcess = false, 
            bool returnExceptionOnError = false,
            bool shouldRetrySomeTimes = true,
            bool isUsingTimeOut = true,
            Func<bool> resultValidator = null)
        {
            if (handleProcessesController)
                await handleProcessesController.HandleProcess
                    (taskFactory: uniTask, 
                    taskId: $"<color=#6dc79d>[AM]</color>::{taskId ?? "Not defined"}", 
                    showLoading: showLoading,
                    shouldIgnoreTryAgainProcess: shouldIgnoreTryAgainProcess,
                    returnExceptionOnError: returnExceptionOnError,
                    shouldRetrySomeTimes: shouldRetrySomeTimes,
                    isUsingTimeOut: isUsingTimeOut,
                    resultValidator: resultValidator);
            else
            {
                Debug.LogWarning("LoadingController service not found. Calling task without loading screen.");
                await uniTask();
            }
        }
        
        /// <summary>
        /// Executes an asynchronous task with optional loading screen, retry, timeout, and result validation, using the
        /// AuthManager proxy.
        /// </summary>
        /// <typeparam name="T">The type of the result returned by the asynchronous task.</typeparam>
        /// <param name="uniTask">The asynchronous task to execute.</param>
        /// <param name="taskId">An optional identifier for the task.</param>
        /// <param name="showLoading">Indicates whether to show a loading screen during execution.</param>
        /// <param name="shouldIgnoreTryAgainProcess">Indicates whether to ignore the retry process on failure.</param>
        /// <param name="returnExceptionOnError">Indicates whether to return an exception on error.</param>
        /// <param name="shouldRetrySomeTimes">Indicates whether to retry the task multiple times on failure.</param>
        /// <param name="isUsingTimeOut">Indicates whether to use a timeout for the task.</param>
        /// <param name="resultValidator">An optional function to validate the task result.</param>
        /// <returns>A UniTask containing the result of the asynchronous operation.</returns>
        public async UniTask<T> HandleProcess_AuthManagerProxy<T>
            (Func<UniTask<T>> uniTask, 
            string taskId = null, 
            bool showLoading = true, 
            bool shouldIgnoreTryAgainProcess = false, 
            bool returnExceptionOnError = false,
            bool shouldRetrySomeTimes = true,
            bool isUsingTimeOut = true,
            Func<T, bool> resultValidator = null)
        {
            if (handleProcessesController)
                return await handleProcessesController.HandleProcess
                    (taskFactory: uniTask, 
                    taskId: $"<color=#6dc79d>[AM]</color>::{taskId ?? "Not defined"}", 
                    showLoading: showLoading,
                    shouldIgnoreTryAgainProcess: shouldIgnoreTryAgainProcess,
                    returnExceptionOnError: returnExceptionOnError,
                    shouldRetrySomeTimes: shouldRetrySomeTimes,
                    isUsingTimeOut: isUsingTimeOut,
                    resultValidator: resultValidator);
            else
            {
                Debug.LogWarning("LoadingController service not found. Calling task without loading screen.");
                return await uniTask();
            }
        }

        /// <summary>
        /// Helper method to serialize and encrypt data using UGS authentication parameters
        /// </summary>
        /// <param name="data">The data to serialize and encrypt.</param>
        /// <returns>The serialized and encrypted data as a JSON string.</returns>
        public string SerializeAndEncryptData(Dictionary<string, object> data)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(SerializeAndEncryptData)}]</b> Couldn't be called. Unity services is not initialized");
                return null;
            }

            if (data is null or { Count: 0 })
            {
                Debug.LogWarning("Data is null or empty");
                return null;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("The player is not authenticated, so there is no way to serialize the data");
                return null;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            var accessToken = AuthenticationService.Instance.AccessToken;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

            var dataJson = JsonConvert.SerializeObject(data);
            var encriptedParameters = SecurityHelper.EncryptData(dataJson, derivedKey);
            
            return JsonConvert.SerializeObject(encriptedParameters);
        }

        /// <summary>
        /// Helper method to deserialize and decrypt data using UGS authentication parameters
        /// </summary>
        /// <param name="encryptedData">The encrypted data to deserialize and decrypt.</param>
        /// <returns>The deserialized and decrypted data as an array of ResponseData objects.</returns>
        public ResponseData[] DeserializeAndDecryptData(string encryptedData) 
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(DeserializeAndDecryptData)}]</b> Couldn't be called. Unity services is not initialized");
                return null;
            }

            if (string.IsNullOrEmpty(encryptedData))
            {
                Debug.LogWarning("Serialized data is null or empty");
                return default;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("The player is not authenticated, so there is no way to decrypt the data");
                return default;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            var accessToken = AuthenticationService.Instance.AccessToken;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

            try
            {
                var securityDataJson = JsonConvert.DeserializeObject<SecurityData>(encryptedData);
                var securityData = SecurityHelper.DecryptData(securityDataJson, derivedKey);

                return JsonConvert.DeserializeObject<ResponseData[]>(securityData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error to deserialize and decrypt the generic data: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Helper method to deserialize and decrypt generic data using UGS authentication parameters
        /// </summary>
        /// <typeparam name="T">The type of the result returned by the deserialized and decrypted data.</typeparam>
        /// <param name="encryptedData">The encrypted data to deserialize and decrypt.</param>
        /// <returns>The deserialized and decrypted data as an object of type T.</returns>
        public T DeserializeAndDecryptData<T>(string encryptedData)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(DeserializeAndDecryptData)}]</b> Couldn't be called. Unity services is not initialized");
                return default;
            }

            if (string.IsNullOrEmpty(encryptedData))
            {
                Debug.LogWarning("Serialized data is null or empty");
                return default;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("The player is not authenticated, so there is no way to decrypt the generic data");
                return default;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            var accessToken = AuthenticationService.Instance.AccessToken;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);

            try
            {
                var securityDataJson = JsonConvert.DeserializeObject<SecurityData>(encryptedData);
                var securityData = SecurityHelper.DecryptData(securityDataJson, derivedKey);
            
                return JsonConvert.DeserializeObject<T>(securityData);

            }
            catch (Exception ex)
            {
                Debug.LogError($"Error to deserialize and decrypt the generic data: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Downloads the avatar image from the given URL and creates a Sprite from it.
        /// </summary>
        /// <param name="imageUrl">The URL of the image to download.</param>
        /// <returns>The downloaded image as a Sprite, or null if the download fails.</returns>
        public async UniTask<Sprite> DownloadAvatar(string imageUrl)
        {
            // Check if the url is not malformed
            if (!IsValidHttpUrl(imageUrl))
            {
                Debug.LogWarning($"[Avatar] The string is not a valid url\n\n{imageUrl}");
                return default;
            }

            try
            {
                using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Avatar] Failed to download image: {request.error}");
                    return default;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                var rect = new Rect(0, 0, texture.width, texture.height);
                var pivot = new Vector2(0.5f, 0.5f);

                var providerIcon = Sprite.Create(texture, rect, pivot);
                Debug.Log("[Avatar] Sprite created successfully.");

                return providerIcon;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Avatar] Exception occurred while downloading image: {ex.Message}");
                return default;
            }

            /// <summary>
            /// Validates whether a string is a well-formed absolute HTTP or HTTPS URL
            /// </summary>
            bool IsValidHttpUrl(string url)
            {
                // Null or empty strings are automatically invalid
                if (string.IsNullOrWhiteSpace(url))
                    return false;

                // Try to create a URI using absolute rules
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                    return false;

                // Accept only HTTP and HTTPS schemes (Curl friendly)
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
        }
        
        /// <summary>
        /// Validates if a username meets all UGS and project-specific rules.
        /// This method does NOT modify the original username.
        /// </summary>
        /// <param name="username">Final username to validate (example: PlayerName.123)</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValidUGSName(string username)
        {
            Debug.Log($"IsValidUGSName: Checking for: {username}");

            // Null or empty usernames are invalid
            if (string.IsNullOrEmpty(username))
                return false;

            // Remove extra ID if present (after '#')
            var nameWithOutID = username.Split('#').FirstOrDefault();

            // Normalize username for validation without mutating original input
            var normalizedUsername = ParseSpecialCharacters(nameWithOutID);

            // Expected format: name.xxx
            var parts = normalizedUsername.Split('.');

            // Must contain exactly one dot
            if (parts.Length is 0 or > 2)
                return false;

            string namePart = parts[0];
            string uidPart = parts.ElementAtOrDefault(1);

            // Extra Idd rules
            if (!IsExtraIdValid(uidPart))
            {
                Debug.LogWarning("IsValidUGSName: Username doesn't march: extra id failed");
                return false;
            }

            // Name length rules (before dot)
            if (namePart.Length < 4 || namePart.Length > 26)
            {
                Debug.LogWarning("IsValidUGSName: Username doesn't march: character lenght failed");
                return false;
            }

            // Allowed characters according to UGS rules
            var allowedRegex = new Regex(@"^[a-zA-Z0-9_\-\.]+$");

            // Validate characters after normalization
            if (!allowedRegex.IsMatch(normalizedUsername))
            {
                Debug.LogWarning("IsValidUGSName: Username doesn't march: using no valid regex");
                return false;
            }

            // Ensure the name contains at least one lowercase letter
            if (!namePart.Any(char.IsLower))
            {
                Debug.LogWarning("IsValidUGSName: Username doesn't march: doesn't contains at least one lower letter");
                return false;
            }

            // Spaces are not allowed in final username
            if (normalizedUsername.Contains(" "))
            {
                Debug.LogWarning("IsValidUGSName: Username doesn't march: contains spaces");
                return false;
            }

            // All validations passed
            return true;

            // Local helper method to normalize accented characters
            // This method is intentionally nested to avoid external misuse
            string ParseSpecialCharacters(string input)
            {
                // Prevent null reference issues
                if (string.IsNullOrEmpty(input))
                    return string.Empty;

                // Decompose accented characters (FormD)
                string normalized = input.Normalize(NormalizationForm.FormD);

                var builder = new StringBuilder();

                foreach (char c in normalized)
                {
                    // Ignore diacritical marks (accents)
                    if (Char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    {
                        builder.Append(c);
                    }
                }

                // Recompose string to canonical form (FormC)
                return builder
                    .ToString()
                    .Normalize(NormalizationForm.FormC);
            }

            // Local helper method to validate if the extra id is limited to 3 digits and normal characters
            bool IsExtraIdValid(string uidPart)
            {
                // If the extra id generation is being ignore, return true
                if (!generateExtraIDForUsername)
                    return true;

                // UID suffix must be exactly 3 characters
                if (uidPart.Length != 3)
                    return false;

                // UID suffix must be alphanumeric
                if (!uidPart.All(char.IsLetterOrDigit))
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Simply check if the usernameA match the B's one<br></br>
        /// Ignore uppercase and additional ids
        /// </summary>
        /// <param name="usernameA">The first username to compare.</param>
        /// <param name="usernameB">The second username to compare.</param>
        /// <returns>True if the usernames match ignoring case and extra IDs, otherwise false.</returns>
        public bool CheckForSameName(string usernameA, string usernameB)
        {
            // Check if any of the usernames couldn't be compared
            if (string.IsNullOrEmpty(usernameA) || string.IsNullOrEmpty(usernameB))
                return false;

            var usernameAWithOutExtraCode = usernameA.Split('#')?.FirstOrDefault()?.ToLower();
            var usernameBWithOutExtraCode = usernameB.Split('#')?.FirstOrDefault()?.ToLower();

            return usernameAWithOutExtraCode == usernameBWithOutExtraCode;
        }

        /// <summary>
        /// Simply check if the username has an extra id (numbers after the '#'
        /// </summary>
        /// <param name="username">the username to check.</param>
        public bool HasExtraId(string username)
        {
            // Check if the username couldn't be check
            if (string.IsNullOrEmpty(username))
                return false;

            return username.Split('#')?.Length > 0;
        }

        /// <summary>
        /// Generates a valid UGS username based on the provided display name and UGS name parameters.
        /// </summary>
        /// <param name="displayName">The display name to base the username on.</param>
        /// <param name="uid">The UGS name to ensure uniqueness.</param>
        /// <returns>A valid UGS username.</returns>
        public string GenerateUsername(string displayName, string uid)
        {
            // Normalize input: convert null values to empty strings to avoid crashes
            displayName = displayName?.Split('#')?.FirstOrDefault() ?? string.Empty;
            uid = uid ?? "000";

            // Replace accented characters BEFORE removing invalid ones
            string normalizedDisplayName = ParseSpecialCharacters(displayName);

            // Remove all characters not allowed by Unity Authentication (UGS)
            // Allowed: A-Z, a-z, 0-9, underscore, hyphen, period, space
            var cleaned = Regex.Replace(normalizedDisplayName, @"[^a-zA-Z0-9_\-\. ]+", "");

            // Capitalize each word in the user's name
            // Splits by whitespace and converts each segment to Title Case manually
            string capitalized;

            // If the name contains spaces, apply Title Case per word
            // Otherwise, keep the original casing (important for CamelCase names)
            if (cleaned.Contains(" "))
            {
                capitalized = string.Concat(
                    Regex.Split(cleaned, @"\s+")
                        .Where(w => w.Length > 0)
                        .Select(w =>
                            char.ToUpper(w[0]) + w.Substring(1).ToLower()
                        )
                );
            } 
            
            // Preserve original casing when no spaces are present
            else
                capitalized = cleaned;


            // Limit the resulting name to a maximum of 26 characters
            if (capitalized.Length > 26)
                capitalized = capitalized.Substring(0, 26);

            // Ensure the name has a minimum length of 4 characters
            // If shorter, pad the end with underscores
            if (capitalized.Length < 4)
                capitalized = capitalized.PadRight(4, '_');

            // Base username without extra ID
            string username = capitalized;

            // If enabled, append extra ID to ensure uniqueness
            if (generateExtraIDForUsername)
            { 
                // Extract last 3 characters from the provider UID
                // If UID is too short, pad on the left with zeros
                string uidDigits = uid.Length >= 3
                    ? uid.Substring(uid.Length - 3)
                    : uid.PadLeft(3, '0');

                // Ensure the name contains at least one lowercase letter
                // If not, convert the entire name to lowercase
                if (!capitalized.Any(char.IsLower))
                    capitalized = capitalized.ToLower();

                // Construct the final username in the pattern: name.xxx
                username = $"{capitalized}.{uidDigits}";
            }

            // Debug print for developer visibility
            Debug.Log("Generated Username (UGS-safe): " + username);

            return username;

            // Local helper method to replace accented characters with ASCII equivalents
            // Nested here to keep the normalization logic close to its usage
            string ParseSpecialCharacters(string input)
            {
                // Prevent null reference issues
                if (string.IsNullOrEmpty(input))
                    return string.Empty;

                // Decompose accented characters (FormD)
                string normalized = input.Normalize(NormalizationForm.FormD);

                var builder = new StringBuilder();

                foreach (char c in normalized)
                {
                    // Skip diacritical marks (accents)
                    if (Char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    {
                        builder.Append(c);
                    }
                }

                // Recompose string to canonical form (FormC)
                return builder
                    .ToString()
                    .Normalize(NormalizationForm.FormC);
            }
        }
        #endregion

        #region Firebase Handlers
        /// <summary>
        /// Signs in to Firebase using a refresh token retrieved and decrypted from a JavaScript library module.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous sign-in operation.</returns>
        public async UniTask Firebase_JSLibSignInWithRefreshToken()
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            // Define the data we want to retrieve from the module
            var dataEncrypted = SerializeAndEncryptData(new()
            {
                ["keys"] = new string[]
                {
                    firebase_refresh_token,
                }
            });

            // Call the function within the module and provide the parameters we defined in there
            var refreshTokenEncrypted = await HandleProcess_AuthManagerProxy
                (uniTask: () => module.LoadProtectedData(dataEncrypted).AsUniTask(),
                taskId: "Load Firebase Refresh Token to login in firebase in jslib",
                resultValidator: _result => string.IsNullOrEmpty(_result));

            // Deserialize and decrypt the data
            var dataResponse = DeserializeAndDecryptData(refreshTokenEncrypted);
            if (dataResponse == null)
            {
                Debug.LogWarning("Firebase_JSLibSignInWithRefreshToken: Failed to deserialize firebase refresh token data response.");
                return;
            }

            // If the response contains data, check if the Google ID token is still valid
            if (dataResponse.Length > 0)
            { 
                var firebaseRefreshToken = dataResponse.FirstOrDefault(x => x.key is firebase_refresh_token)?.value.ToString();
           
                if (!string.IsNullOrEmpty(firebaseRefreshToken))
                {
                    Debug.Log("Firebase_JSLibSignInWithRefreshToken: Signing in to Firebase with custom token from UGS");

                    FirebaseAuth.FirebaseSignInWithRefreshToken(
                        refreshToken: firebaseRefreshToken,
                        gameObject.name,
                        nameof(OnFirebaseSignInWithCustomTokenSuccessfully),
                        nameof(OnFirebaseFailToSignInWithCustomToken));
                } 
                else
                    Debug.LogWarning("Firebase_JSLibSignInWithRefreshToken: Failed to sign in with custom id in jslib.");
            }
        }

        /// <summary>
        /// Signs in to Firebase using a custom token, token type, and provider ID via JavaScript library integration.
        /// </summary>
        /// <param name="token">The custom authentication token used for signing in.</param>
        /// <param name="tokenType">The type of the authentication token provided.</param>
        /// <param name="providerId">The identifier of the authentication provider.</param>
        public void Firebase_JSLibSignInWithIdp(string token, string tokenType, string providerId)
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            if (!string.IsNullOrEmpty(token))
            { 
                Debug.Log("Firebase_JSLibSignInWithIdp: Signing in to Firebase with custom token from UGS");

                FirebaseAuth.FirebaseSignInWithIdp(
                    token: token, 
                    tokenType: tokenType, 
                    providerId: providerId,
                    gameObject.name,
                    nameof(OnFirebaseSignInWithCustomTokenSuccessfully),
                    nameof(OnFirebaseFailToSignInWithCustomToken));
            }
            else
                Debug.LogWarning("Firebase_JSLibSignInWithIdp: Failed to sign in with custom id in jslib.");
        }

        /// <summary>
        /// Signs out the authenticated user from Firebase and triggers success or failure callbacks.
        /// </summary>
        public void Firebase_SignOut()
        {
            if (!IsValidPlatformToUseJSlib() || FirebaseAuth.IsUserAuthenticated() is 0)
                return;

            Debug.LogWarning("Logging out from Firebase");

            FirebaseAuth.SignOut(gameObject.name, nameof(OnFirebaseSignOutSuccessfully), nameof(OnFirebaseSignOutFailed));
        }
        #endregion

        #region Event Handles
        /// <summary>
        /// Registers event handlers for Unity AuthenticationService events, managing sign-in, token expiration, and
        /// sign-out actions.
        /// </summary>
        private void HandleCommonAuthHandlers()
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(HandleCommonAuthHandlers)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            AuthenticationService.Instance.SignedIn += () =>
            {
                if (IsAuthenticated)
                {
                    Debug.Log($"The player has successfully signed in");
                    AuthUI?.CallOnSignInEvent(true);
                }
            };

            AuthenticationService.Instance.Expired += () =>
            {
                Debug.Log($"The access token was not refreshed and has expired");

                AuthenticationService.Instance.ClearSessionToken();
                ResetFields();
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                Debug.Log($"The player has successfully signed out");

                AuthenticationService.Instance.ClearSessionToken();
                ResetFields();

                // Sign out from Firebase once the player has signed out from Unity services to avoid possible conflicts
                if (WillSignOutFromFirebaseToo)
                    Firebase_SignOut();

                // Once the user is signed out, we can sign in again anonimously to continue using the basic game services
                if (WillSignInAnonymouslyOnSignOut)
                    HandleProcess_AuthManagerProxy
                        (uniTask: SignInAnonmously,
                        taskId: nameof(SignInAnonmously),
                        showLoading: true,
                        resultValidator: () => IsUGSAuthenticated)
                    .Forget();

                AuthUI?.CallOnSignOutEvent(true);
            };
        }

        /// <summary>
        /// Clears the providerUsername and ProviderIcon fields.
        /// </summary>
        private void ResetFields()
        {
            providerUsername = null;
            ProviderIcon = null;
        }

        /// <summary>
        /// Registers a callback to be invoked when the player is fully signed in to Unity services and Firebase, if
        /// applicable.
        /// </summary>
        /// <param name="onSignIn">The action to execute upon complete sign-in.</param>
        public void HandleOnSignIn(UnityAction onSignIn) 
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(HandleOnSignIn)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // We use our own UnityEvent to notify when the player is completely signed in (UGS + Firebase if applicable)
            // The other possible way is to use AuthenticationService.Instance.SignedIn, but it will be called when the player is signed in UGS only
            if (onCompletelySignedIn is not null)
                onCompletelySignedIn.AddListener(onSignIn);
        }

        /// <summary>
        /// Removes the specified listener from the event triggered when the player is completely signed in.
        /// </summary>
        /// <param name="onSignIn">The UnityAction delegate to remove from the sign-in event.</param>
        public void UnHandleOnSignIn(UnityAction onSignIn) 
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(UnHandleOnSignIn)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            // We use our own UnityEvent to notify when the player is completely signed in (UGS + Firebase if applicable)
            // The other possible way is to use AuthenticationService.Instance.SignedIn, but it will be called when the player is signed in UGS only
            if (onCompletelySignedIn is not null)
                onCompletelySignedIn.RemoveListener(onSignIn);
        }

        /// <summary>
        /// Registers a callback to be invoked when the user signs out, if Unity services are initialized.
        /// </summary>
        /// <param name="onSignOut">The action to execute upon sign-out.</param>
        public void HandleOnSignOut(Action onSignOut)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(HandleOnSignOut)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (AuthenticationService.Instance != null && onSignOut != null)
                AuthenticationService.Instance.SignedOut += onSignOut;
        }
        
        /// <summary>
        /// Removes the specified action from the SignedOut event of the AuthenticationService instance.
        /// </summary>
        /// <param name="onSignOut">The action to remove from the SignedOut event.</param>
        public void UnHandleOnSignOut(Action onSignOut)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(UnHandleOnSignOut)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (AuthenticationService.Instance != null && onSignOut != null)
                AuthenticationService.Instance.SignedOut -= onSignOut;
        }

        /// <summary>
        /// Registers a callback to be invoked when the authentication session expires, if Unity services are
        /// initialized.
        /// </summary>
        /// <param name="onExpired">The action to execute when the authentication session expires.</param>
        public void HandleOnExpired(Action onExpired)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(HandleOnExpired)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (AuthenticationService.Instance != null && onExpired != null)
                AuthenticationService.Instance.Expired += onExpired;
        }

        /// <summary>
        /// Removes the specified handler from the authentication service's Expired event.
        /// </summary>
        /// <param name="onExpired">The event handler to remove from the Expired event.</param>
        public void UnHandleOnExpired(Action onExpired)
        {
            // Check if the Unity services are ready to be used
            if (!IsAlreadyInitialized)
            {
                Debug.LogWarning($"<b>[{nameof(UnHandleOnExpired)}]</b> Couldn't be called. Unity services is not initialized");
                return;
            }

            if (AuthenticationService.Instance != null && onExpired != null)
                AuthenticationService.Instance.Expired -= onExpired;
        }
        #endregion

        #region Events
        /// <summary>
        /// Logs a warning message indicating successful Firebase sign-in with a custom token.
        /// </summary>
        /// <param name="msg">The message describing the sign-in event.</param>
        private void OnFirebaseSignInWithCustomTokenSuccessfully(string msg)
        {
            Debug.LogWarning($"Signed in to Firebase with custom token successfully: {msg}");
        }

        /// <summary>
        /// Logs an error message when Firebase fails to sign in with a custom token.
        /// </summary>
        /// <param name="error">The error message describing the reason for the sign-in failure.</param>
        private void OnFirebaseFailToSignInWithCustomToken(string error)
        {
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"Failed to sign in to Firebase with custom token: {error}");
            else
                Debug.LogError("Failed to sign in to Firebase with custom token: Unknown error");
        }

        /// <summary>
        /// Logs a message indicating a successful Firebase sign-out.
        /// </summary>
        /// <param name="response">The response message from the Firebase sign-out operation.</param>
        private void OnFirebaseSignOutSuccessfully(string response)
        { 
            Debug.Log($"Firebase Sign out successfully: {response}");
        }

        /// <summary>
        /// Logs an error message when Firebase sign-out fails.
        /// </summary>
        /// <param name="response">The error message returned from the failed sign-out attempt.</param>
        private void OnFirebaseSignOutFailed(string response)
        {
            Debug.LogError($"Firebase Sign out failed: {response}");
        }
        #endregion
    }
}
