using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Authentication;
using ProDomino.HandleProcessesSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal.Http;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;
using UnityEngine.Localization.Settings;

namespace ProDomino.GameSystem
{
    public partial class GameManager : SingleInstanceMonoBehaviour<GameManager>, IService
    {
        [SerializeField] private bool isShowingLogs;
        [SerializeField] private float minutesToFetchLeaderboard = 5f;

        private UnityEvent onSignedIn, onSignedOut;
        private Dictionary<string, bool> waitingDictionary;
        private AuthManager authManager;
        private DictionaryService dictionaryService;
        private HandleProcessesController handleProcessesController;
        private string lastNormalizedSearchTerm = string.Empty;
        private DateTime lastLeaderboardFetchTime = DateTime.MinValue;
        private DateTime? startMatchDateTime;
        private readonly Dictionary<string, Sprite> cachedSprites = new();

        private bool areBundlesLoadadedSuccessfully = false;
        private bool areProtectedDataLoadedSuccessfully = false;
        private string lastUGSPlayerIDRegistered = null;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        // Public Player Data
        public Dictionary<string, object> PublicPlayerData { get; private set; }
        public List<PlayerNotificationData> PlayerNotificationDatas { get; private set; }
        public List<PlayerRecentlyPlayedData> RecentlyPlayedDatas { get; private set; }

        // Protected Player Data
        public AnalyticsData AnalyticsData { get; private set; }
        public NationalityData NationalityData { get; private set; }
        public PlayerProfileData PlayerProfileData { get; private set; }
        public PlayerMatchData PlayerMatchData { get; private set; }
        public PlayerAdData PlayerAdData { get; private set; }
        public PlayerCosmeticData[] PlayerCosmeticDatas { get; private set; }
        public PlayerAchievementData[] PlayerAchievementDatas { get; private set; }
        public PlayerPurchasesData PlayerPurchasesData { get; private set; }
        public Dictionary<Currency, uint> PlayerCurrencyCollection { get; private set; }

        // Game Data
        public GameConfig CurrentGameConfig { get; private set; }
        public ConfigData GameBackendConfigData { get; private set; }
        public GameCosmeticData[] GameCosmeticData { get; private set; }
        public GameAchievementData[] GameAchievementData { get; private set; }

        // Profile Data
        public Sprite ProfilePicture => GetProfilePicture().icon;
        public Sprite Board => GetBoard().icon;
        public Sprite BoardFund => GetBoardFund().icon;
        public Sprite TilePreview => GetTilePreview().icon;
        public Sprite[] Tiles => GetTiles().icons;
        public Sprite BackTile => GetBackTile().icon;
        public Sprite[] Badges => GetBadges()?.Select(x => x?.icon)?.Where(x => x != null)?.ToArray();


        private const string clubsCollectionIdKey = "clubs";
        private const string clubsChatsCollectionIdKey = "clubChats";
        private const string purchasesCollectionsIdKey = "purchases";

        public bool IsAlreadyInitialized => areBundlesLoadadedSuccessfully && areProtectedDataLoadedSuccessfully;
        public bool IsAuthenticated => 
            authManager is not null 
            and { IsAlreadyInitialized: true, IsAuthenticated: true }
            && sessionActive;

        public bool IsAuthenticatedAndVerified => 
            authManager is not null
            and { IsAlreadyInitialized: true, IsAuthenticatedAndVerified: true }
            && sessionActive;

        protected override async void Awake()
        {
            base.Awake();

            onSignedIn = new();
            onSignedOut = new();
            waitingDictionary = new();
            SearchedClubsCollection = new();
            SearchedPlayersCache = new();
            lastPlayerGameModeCollection = new();
            onUpdateGlobaAnalyticsData = new();
            onGetTopLeaderboardData = new();

            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            handleProcessesController = ServiceLocator.Instance.GetService<HandleProcessesController>();

            Awake_SessionSystem();

#if !UNITY_EDITOR && DEVELOPMENT_BUILD
            if (!isShowingLogs)
                Debug.unityLogger.logEnabled = false;
#elif UNITY_EDITOR
            Debug.unityLogger.logEnabled = true;
#else
            Debug.unityLogger.logEnabled = false;
#endif
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            // Wait until the authentication manager is initialized due ugs needs to be initialized before we can access the backend bindings
            // The secuence is: GameManager.Awake -> AuthManager.Awake (initialize UGS) -> GameManager.Awake (continue) -> AuthManager Cached Login
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true });

            // Subscribe to the sign-in event to refresh all data when the player signs in
            authManager?.HandleOnSignIn(OnSignInProxy);
            authManager?.HandleOnFacebookLogin(OnSignInProxy);
            authManager?.HandleOnGoogleLogin(OnSignInProxy);

            // Subscribe to the sign-out event to clear all data when the player signs out
            authManager.HandleOnSignOut(OnSignOutProxy);
            authManager.HandleOnExpired(OnSignOutProxy);

            // Wait until localization is initialized
            await HandleProcess_GameManagerProxy
                (uniTask: () => LocalizationSettings.InitializationOperation.ToUniTask(),
                taskId: $"{nameof(LocalizationSettings)}_{nameof(LocalizationSettings.InitializationOperation)}",
                showLoading: true,
                shouldIgnoreTryAgainProcess: false,
                returnExceptionOnError: false,
                shouldRetrySomeTimes: true);

            // Load the data from player preferences
            LoadGameConfig();

            

            // If the player is already authenticated, refresh all data. (this is necessary in case the user is already signed in when the game manager awakes)
            if (IsAuthenticated)
                await UniTask.WhenAll(PreloadBundles(), RefreshAllDataAsync());

            // Initialize the bad words checker
            await HandleProcess_GameManagerProxy
                (uniTask: () => new UnityProfanityValidator().Initialize().AsUniTask(),
                taskId: $"{nameof(UnityProfanityValidator)}_{nameof(UnityProfanityValidator.Initialize)}",
                showLoading: false);
        }

        private void Update()
        {
            Update_SessionSystem();
        }

        private void OnDestroy()
        {
            // Unsubscribe from the sign-in event
            authManager?.UnHandleOnSignIn(OnSignInProxy);
            authManager?.UnHandleOnFacebookLogin(OnSignInProxy);
            authManager?.UnHandleOnGoogleLogin(OnSignInProxy);

            // Unsubscribe from the sign-out event
            authManager?.UnHandleOnSignOut(OnSignOutProxy);
            authManager?.UnHandleOnExpired(OnSignOutProxy);
        }

        private void OnApplicationQuit()
        {
            OnApplicationQuit_SessionSystem();
        }

        /// <summary>
        /// Checks if the current platform is valid for using JSlib (WebGL).
        /// </summary>
        /// <returns></returns>
        public static bool IsValidPlatformToUseJSlib() => Application.platform is RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// Preloads the necessary bundles and game data from the backend.
        /// </summary>
        /// <returns></returns>
        private async UniTask PreloadBundles()
        {
            // Check if the player is authenticated at least anonymously
            // This process is MANDATORY
            if (authManager is null or { IsUGSAuthenticated: false })
                throw new Exception("To load protected game data the player must be authenticated at least anonymously");

            var keysEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["keys"] = new string[]
                {
                    Consts.CollectionKeys.Achievements,
                    Consts.CollectionKeys.Cosmetics,
                    Consts.CollectionKeys.Config,
                }
            });

            // Load from the backend the protected game data
            var dataResponseEncrypted = await HandleProcess_GameManagerProxy
                (uniTask: () => module.LoadProtectedGameData(keysEncrypted).AsUniTask(),
                taskId: nameof(module.LoadProtectedGameData),
                showLoading: true);

            // Deserialize and decrypt the data
            var gameDataResponse = authManager.DeserializeAndDecryptData(dataResponseEncrypted);
            if (gameDataResponse is null or { Length: 0 })
            {
                areBundlesLoadadedSuccessfully = false;
                throw new Exception("Failed to deserialize Protected Game data response.");
            }

            // Get the game backend config data
            var gameBackendConfigDataResultData = gameDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Config)?.value.ToObject<ConfigData>();
            if (gameBackendConfigDataResultData is null)
                Debug.LogWarning("No game cosmetic data found in the response. Cannot validate cosmetics.");
            else
                GameBackendConfigData = gameBackendConfigDataResultData;

            // Get the game cosmetic data
            var gameCosmeticResultDatas = gameDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Cosmetics)?.value.ToObject<GameCosmeticData[]>();
            if (gameCosmeticResultDatas is null or { Length: 0 })
                Debug.LogWarning("No game cosmetic data found in the response. Cannot validate cosmetics.");
            else
                GameCosmeticData = gameCosmeticResultDatas;

            // Get the game achievement data
            var gameAchievementDatas = gameDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Achievements)?.value.ToObject<GameAchievementData[]>();
            if (gameAchievementDatas is null or { Length: 0 })
                Debug.LogWarning("No game achievement data found in the response. Cannot validate achievements.");
            else
                GameAchievementData = gameAchievementDatas;

            areBundlesLoadadedSuccessfully = true;
        }

        /// <summary>
        /// Refreshes the public player data from the cloud save service, including notifications and recently played players.
        /// </summary>
        /// <returns></returns>
        public async UniTask RefreshPublicPlayerData()
        {
            // Check if the player is authenticated
            if (!IsAuthenticated)
            { 
                Debug.LogWarning("To load public data the player must be authenticated");
                return;
            }

            // Load all public player data from the cloud save service
            PublicPlayerData = 
                (await HandleProcess_GameManagerProxy
                    (uniTask: () => CloudSaveService.Instance.Data.Player.LoadAllAsync().AsUniTask(),
                    taskId: "LoadPublicPlayerData",
                    showLoading: false))
                ?.ToDictionary(x => x.Key, x => (object)x.Value.Value);

            // Deserialize the recently played and notifications data
            if (PublicPlayerData is not null and { Count: > 0 })
            {
                if (PublicPlayerData.TryGetValue(Consts.CollectionKeys.RecentlyPlayed, out var recentlyPlayerObject) 
                    && recentlyPlayerObject is IDeserializable recentlyPlayerDeserializable)
                    RecentlyPlayedDatas = recentlyPlayerDeserializable.GetAs<List<PlayerRecentlyPlayedData>>();
                else
                    RecentlyPlayedDatas ??= new();

                if (PublicPlayerData.TryGetValue(Consts.CollectionKeys.Notifications, out var notificationsObject) 
                    && notificationsObject is IDeserializable notificationsPlayerDeserializable)
                    PlayerNotificationDatas = notificationsPlayerDeserializable.GetAs<List<PlayerNotificationData>>();
                else
                    PlayerNotificationDatas ??= new();
            }
            else
            { 
                RecentlyPlayedDatas ??= new();
                PlayerNotificationDatas ??= new();
            }
        }

        /// <summary>
        /// Refreshes the protected player data from the backend, including profile, match, cosmetics, achievements, and currency collection.
        /// </summary>
        /// <returns></returns>
        public async UniTask RefreshProtectedPlayerData()
        {
            // Check if the player is authenticated
            if (!IsAuthenticated)
            {
                areProtectedDataLoadedSuccessfully = false;
                
                Debug.LogWarning("To load protected data the player must be authenticated");
                return;
            }

            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["keys"] = new string[]
                {
                    Consts.CollectionKeys.Analytics,
                    Consts.CollectionKeys.Achievements,
                    Consts.CollectionKeys.Cosmetics,
                    Consts.CollectionKeys.Currency,
                    Consts.CollectionKeys.Profile,
                    Consts.CollectionKeys.Match,
                    Consts.CollectionKeys.Club,
                    Consts.CollectionKeys.Ads,
                    Consts.CollectionKeys.Nationality,
                }
            });

            // Load the protected game and player data from the backend instead of using specific cloud code functions
            var playerDataResponseEncrypted = await HandleProcess_GameManagerProxy
                (uniTask: () => module.LoadProtectedData(dataEncrypted).AsUniTask(),
                taskId: nameof(module.LoadProtectedData),
                showLoading : false);

            // Check if the response contains data, if not, log a warning but not throw an error (this is expected if the game has no cosmetics yet)
            var playerDataResponse = authManager.DeserializeAndDecryptData(playerDataResponseEncrypted);
            if (playerDataResponse is null or { Length: 0 })
            { 
                Debug.LogWarning($"Failed to deserialize Protected Player data response.\n\nEncrypted data: {playerDataResponseEncrypted}");
                return;
            }

            AnalyticsData = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Analytics)?.value?.ToObject<AnalyticsData>();
            if (AnalyticsData is null)
                Debug.LogWarning("No player anlytics data found in the response. Cannot validate analytics.");

            PlayerAchievementDatas = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Achievements)?.value?.ToObject<PlayerAchievementData[]>();
            if (PlayerAchievementDatas is null or { Length: 0 })
                Debug.LogWarning("No player achievement data found in the response. Cannot validate achievements.");

            PlayerCosmeticDatas = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Cosmetics)?.value?.ToObject<PlayerCosmeticData[]>();
            if (PlayerCosmeticDatas is null or { Length: 0 })
                Debug.LogWarning("No player cosmetics data found in the response. Cannot validate cosmetics.");

            PlayerCurrencyCollection = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Currency)?.value?.ToObject<Dictionary<Currency, uint>>();
            if (PlayerCurrencyCollection is null or { Count: 0 })
                Debug.LogWarning("No player currency data found in the response. Cannot validate currency collection.");

            PlayerProfileData = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Profile)?.value?.ToObject<PlayerProfileData>();
            if (PlayerProfileData is null)
            {
                Debug.LogWarning("No player profile data found in the response. Cannot validate profile data. Creating One...");
                PlayerProfileData = new();
            }

            PlayerMatchData = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Match)?.value?.ToObject<PlayerMatchData>();
            if (PlayerMatchData is null)
                Debug.LogWarning("No player match data found in the response. Cannot validate match data. Creating One...");

            UGSPlayerClubName = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Club)?.value?.ToObject<string>();
            if (UGSPlayerClubName is null)
                Debug.LogWarning("No player club name found in the response. Cannot validate club name.");

            PlayerAdData = playerDataResponse.FirstOrDefault(x => x.key is Consts.CollectionKeys.Ads)?.value?.ToObject<PlayerAdData>();
            if (PlayerAdData is null)
                Debug.LogWarning("No player ad data found in the response. Cannot validate ad data.");

            NationalityData = playerDataResponse?.FirstOrDefault(x => x?.key == Consts.CollectionKeys.Nationality)?.value?.ToObject<NationalityData>();
            if (NationalityData is null)
                Debug.LogWarning("No player nationality data found in the response. Cannot validate nationality.");

            areProtectedDataLoadedSuccessfully = true;
        }

        public async UniTask UpdateTotalTimeMatch()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot update achievement progress. User is not authenticated.");
                return;
            }

            if (!startMatchDateTime.HasValue)
            { 
                Debug.LogWarning("Start match date time is not registered. Cannot update total time match.");
                return;
            }

            var totalMatchTicks = (DateTime.UtcNow - startMatchDateTime.Value).Ticks;
            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["matchTimeTicks"] = totalMatchTicks,
            });

            var analyticDataEncryptedJson = await HandleProcess_GameManagerProxy
                (uniTask: () => module.UpdateTotalTimeMatch(dataEncrypted).AsUniTask(),
                taskId: nameof(module.UpdateTotalTimeMatch),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true);

            if (string.IsNullOrEmpty(analyticDataEncryptedJson))
            {
                Debug.LogWarning("Failed to Update total time match");
                return;
            }

            // Deserialize and decrypt the purchase result
            var analyticDataResponse = authManager.DeserializeAndDecryptData<AnalyticsDataResponse>(analyticDataEncryptedJson);
            if (analyticDataResponse is null or { analyticsData: null })
            {
                Debug.LogWarning("Failed to deserialize analytics data response.");
                return;
            }

            AnalyticsData = analyticDataResponse.analyticsData;
        }

        public void RegisterStartMatchDateTime(bool isReseting = false)
        { 
            startMatchDateTime = isReseting ? null : DateTime.UtcNow;
        }

        /// <summary>
        /// Loads the game configuration from PlayerPrefs or initializes it with default values if not found.
        /// </summary>
        private void LoadGameConfig()
        { 
            var gameConfigJson = PlayerPrefs.GetString(Consts.PlayerPrefs.GameConfig, JsonConvert.SerializeObject(new GameConfig()));

            if (string.IsNullOrEmpty(gameConfigJson))
            { 
                Debug.LogWarning("GameConfig not found in PlayerPrefs, using default configuration.");
                CurrentGameConfig = new GameConfig();
            }
            else
                CurrentGameConfig = JsonConvert.DeserializeObject<GameConfig>(gameConfigJson);

            ChangeTargetFrame((TargetFrameRate)CurrentGameConfig.TargetFrameRate);
            ChangeBGMVolume(CurrentGameConfig.BGMVolume);
            ChangeSFXVolume(CurrentGameConfig.SFXVolume);
            ChangeLanguage(CurrentGameConfig.Language);
        }

        /// <summary>
        /// This method is calle only with the purpose of updating the player profile data locally. Remote updates should be handled by other services
        /// </summary>
        /// <param name="playerProfileData"></param>
        public void UpdatePlayerProfileData(PlayerProfileData playerProfileData)
        {
            PlayerProfileData = playerProfileData;
        }

        /// <summary>
        /// Updates the NationalityData property with the provided nationality information.
        /// </summary>
        /// <param name="nationalityData">The new nationality data to assign.</param>
        public void UpdateNationalityExternally(NationalityData nationalityData)
        {
            NationalityData = nationalityData;
        }

        /// <summary>
        /// Try to referesh the recently entry data
        /// </summary>
        /// <param name="playerRecentlyPlayedData"></param>
        public void RefreshRecentlyPlayedDatas(PlayerRecentlyPlayedData playerRecentlyPlayedData)
        {
            if (playerRecentlyPlayedData is null || string.IsNullOrEmpty(playerRecentlyPlayedData.id) || playerRecentlyPlayedData.id == authManager.UUID)
                return;

            RecentlyPlayedDatas ??= new();
            if (RecentlyPlayedDatas.Any(x => x.id == playerRecentlyPlayedData.id))
            {
                var index = RecentlyPlayedDatas.IndexOf(playerRecentlyPlayedData);

                // If the index is valid, update the played time of the existing entry with the new value.
                // This is necessary to properly order the recently played list based on the most recent played time.
                if (index is not -1 && RecentlyPlayedDatas.Count > index)
                    RecentlyPlayedDatas[index].playedTime = playerRecentlyPlayedData.playedTime;
            } 
            else
                RecentlyPlayedDatas.Add(playerRecentlyPlayedData);

            RecentlyPlayedDatas = RecentlyPlayedDatas
                .OrderByDescending(x => x.playedTime.GetValueOrDefault())
                .ToList();

            if (RecentlyPlayedDatas.Count > 20)
                RecentlyPlayedDatas = RecentlyPlayedDatas.Take(20).ToList();

            SavePublicGameData();
        }

        /// <summary>
        /// Saves the current game configuration to PlayerPrefs.
        /// </summary>
        public void SaveGameConfig()
        {
            var gameConfigJson = JsonConvert.SerializeObject(CurrentGameConfig);
            PlayerPrefs.SetString(Consts.PlayerPrefs.GameConfig, gameConfigJson);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Saves the public game data to the cloud save service, including notifications and recently played players.
        /// </summary>
        public void SavePublicGameData()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("To save public data the player must be authenticated");
                return;
            }

            if (PublicPlayerData is null)
            { 
                Debug.LogWarning("Public player data is null. Cannot save public game data.");
                return;
            }

            // Override the notification public player data with the most recent update
            if (PublicPlayerData.ContainsKey(Consts.CollectionKeys.Notifications))
                PublicPlayerData[Consts.CollectionKeys.Notifications] = PlayerNotificationDatas ?? new();

            else if (PlayerNotificationDatas is not null and { Count: > 0})
                PublicPlayerData.Add(Consts.CollectionKeys.Notifications, PlayerNotificationDatas);

            // Override the recent played players public player data with the most recent update
            if (PublicPlayerData.ContainsKey(Consts.CollectionKeys.RecentlyPlayed))
                PublicPlayerData[Consts.CollectionKeys.RecentlyPlayed] = RecentlyPlayedDatas ?? new();

            else if (RecentlyPlayedDatas is not null and { Count: > 0 })
                PublicPlayerData.Add(Consts.CollectionKeys.RecentlyPlayed, RecentlyPlayedDatas);

            CloudSaveService.Instance.Data.Player.SaveAsync(PublicPlayerData.ToDictionary(x => x.Key, x => x.Value));
        }

        /// <summary>
        /// Changes the target frame rate of the game and updates the Application's target frame rate accordingly.
        /// </summary>
        public void ChangeTargetFrame(TargetFrameRate targetFrameRate)
        {
            CurrentGameConfig.SetTargetFrameRate(targetFrameRate);
            Application.targetFrameRate = CurrentGameConfig.TargetFrameRate;

            SaveGameConfig();
        }

        /// <summary>
        /// Changes the background music (BGM) volume and updates the current game configuration.
        /// </summary>
        public void ChangeBGMVolume(float volume)
        {
            CurrentGameConfig.SetBGMVolume(Mathf.Clamp01(volume));

            // TODO: use AudioManager to set BGM volume

            SaveGameConfig();
        }

        /// <summary>
        /// Changes the sound effects (SFX) volume and updates the current game configuration.
        /// </summary>
        public void ChangeSFXVolume(float volume)
        {
            CurrentGameConfig.SetSFXVolume(Mathf.Clamp01(volume));

            // TODO: use AudioManager to set SFX volume

            SaveGameConfig();
        }


        /// <summary>
        /// Changes the language of the game and updates the current game configuration.
        /// </summary>
        public void ChangeLanguage(string language)
        {
            CurrentGameConfig.SetLanguage(language);

            if (LocalizationSettings.Instance is null)
            {
                Debug.LogWarning("LocalizationSettings instance is null. Cannot change language.");
                return;
            }

            // Save the game config
            var languageSettings = LocalizationSettings.AvailableLocales.GetLocale(language);
            LocalizationSettings.SelectedLocale = languageSettings;
        }

        /// <summary>
        /// Trims the notification list by removing duplicate PartyInvites 
        /// and ensuring it does not exceed the configured limit.
        /// </summary>
        public void TrimNotificationExcess()
        {
            // Deduplicate only PartyInvite notifications
            var partyInvites = PlayerNotificationDatas
                .Where(n => n.notificationType == NotificationType.PartyInvite)
                .GroupBy(n => new { n.senderID, n.targetID })
                .Select(g => g.OrderByDescending(n => n.timestamp ?? 0).First());

            // Keep all non-PartyInvite notifications
            var others = PlayerNotificationDatas
                .Where(n => n.notificationType != NotificationType.PartyInvite);

            // Merge and sort by recency
            PlayerNotificationDatas = partyInvites
                .Concat(others)
                .OrderByDescending(n => n.timestamp ?? 0)
                .ToList();

            // Trim excess (keep most recent only)
            int limit = GameBackendConfigData.notificationsConfig.notificationsLimit;
            if (PlayerNotificationDatas.Count > limit)
            {
                PlayerNotificationDatas = PlayerNotificationDatas
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Try to remove a notification by its hash code
        /// </summary>
        /// <param name="notificationHashCode"></param>
        public void RemoveNotification(int notificationHashCode)
        {
            if (PlayerNotificationDatas.Any(x => x.GetHashCode() == notificationHashCode))
            {
                var notificationsRemoved = PlayerNotificationDatas.RemoveAll(x => x.GetHashCode() == notificationHashCode);
                if (notificationsRemoved > 0)
                { 
                    Debug.Log($"Removed {notificationsRemoved} notifications with ID: {notificationHashCode}");
                    SavePublicGameData();
                }
            }
        }

        /// <summary>
        /// Refreshes all player and game data from the backend asynchronously.
        /// </summary>
        /// <returns></returns>
        private async UniTask RefreshAllDataAsync()
        {
            // Wait until the game and the player data are loaded from the backend for the first time
            await UniTask.WhenAll
                (RefreshPublicPlayerData(),
                RefreshProtectedPlayerData(),
                RefreshFirestoreClubData(),
                RefreshFirestoreClubChatData(),
                RefreshRealtimeDatabasePurchasesData());
        }

        /// <summary>
        /// This is an helper method to handle a UniTask process with the loading controller if available
        /// </summary>
        /// <param name="uniTask"></param>
        /// <returns></returns>
        public async UniTask HandleProcess_GameManagerProxy
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
                    taskId: $"<color=#ba89c7>[GM]</color>::{taskId ?? "Not defined"}", 
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
        /// This is an helper method to handle a UniTask process with the loading controller if available
        /// </summary>
        /// <param name="uniTask"></param>
        /// <returns></returns>
        public async UniTask<T> HandleProcess_GameManagerProxy<T>
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
                    taskId: $"<color=#ba89c7>[GM]</color>::{taskId ?? "Not defined"}", 
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
        /// Proxy method to match the UnityAction signature for sign-in events
        /// </summary>
        private void OnSignInProxy(bool isSuccessfully) => OnSignInProxy();

        /// <summary>
        /// Refreshes all player and game data from the backend.
        /// </summary>
        private async void OnSignInProxy()
        {
            if (authManager is null or { IsUGSAuthenticated: false })
            {
                Debug.LogWarning("Fail to proceed with OnSignInProxy. AuthManager is null or user is not authenticated with UGS.");
                return;
            }

            // Wait until the auth process is not running anymore (necessary for cases where the sign-in is triggered by a create account or credentials login process)
            if (authManager.IsUserAuthenticatedWithProvider || authManager.IsUserAuthenticatedWithCredentials)
            {
                Debug.Log("Try to start session system after sign-in...");

                await UniTask.WhenAll
                    (OnSignIn_SessionSystem(), // Try to start the session system
                    InitializeAnalyticsOnStartup()); // Initialize analytics data on startup

                // Wait until the username is properly set (necessary for cases where the sign-in is triggered by a create account or credentials login process)
                if (authManager.IsAuthenticated)
                    await UniTask
                        .WaitWhile(() => string.IsNullOrEmpty(authManager.Username))
                        .TimeoutWithoutException(TimeSpan.FromSeconds(5));
                else
                    Debug.LogWarning("User is not authenticated after sign-in process. Cannot wait for username to be set.");
            }
            else
                Debug.Log("User is not authenticated with provider or credentials. Skipping session system start.");

            // If the sign in was blocked or failed, do not proceed
            if (!authManager.IsAuthenticated)
            {
                Debug.LogWarning("User is not authenticated after sign-in process. Cannot proceed with OnSignInProxy.");
                return;
            }

            // Wait until the game and the player data are loaded from the backend
            await UniTask.WhenAll(PreloadBundles(), RefreshAllDataAsync());

            // Once everything is loaded, invoke the on signed in event
            onSignedIn?.Invoke();

            // Use when the player signs out and the uid refecerence changes
            lastUGSPlayerIDRegistered = authManager.UUID;
        }

        /// <summary>
        /// Locally removes all player data, used on sign out
        /// </summary>
        private void OnSignOutProxy()
        {
            RecentlyPlayedDatas?.Clear();
            UGSPlayerClubName = null;
            AnalyticsData = null;

            PublicPlayerData?.Clear();
            PlayerNotificationDatas?.Clear();
            PlayerProfileData = null;
            PlayerMatchData = null;
            PlayerCosmeticDatas = null;
            PlayerAchievementDatas = null;
            PlayerCurrencyCollection?.Clear();
            PlayerClubData = null;
            PlayerClubChatData = null;
            PlayerAdData = null;

            areProtectedDataLoadedSuccessfully = false;

            CleanPlayerSearchCache();
            CleanSearchCollection();

            OnSignOut_SessionSystem();

            onSignedOut?.Invoke();
        }

        // Get the profile picture based on the player's tile skin ID or default to the provider icon or default profile icon
        // The priority is: player's profile icon -> provider icon (useful for webgl where we can use the provider icon as profile picture) -> default profile icon
        public (string id, Sprite icon) GetProfilePicture()
        {
            // Try to get the profile picture based on the player's profile icon ID and if it's not the same as the provider URL
            // (to avoid using the provider icon as profile picture if the player has a custom profile icon set,
            // this is useful for webgl where we can use the provider icon as profile picture)
            if (PlayerProfileData?.profileIconID is not null or "" && !IsValidHttpUrl(PlayerProfileData.profileIconID))
                return (PlayerProfileData.profileIconID, dictionaryService.GetSprite(CosmeticType.Icons.ToString(), PlayerProfileData.profileIconID));

            // If the sprite is not found or the player has no profile icon ID, try to get the provider icon if the player is authenticated with a provider (this is useful for webgl where we can use the provider icon as profile picture)
            else if (authManager.ProviderIcon)
                return (authManager?.ProviderURL, authManager?.ProviderIcon);

            // If the player has no profile icon ID and is not authenticated with a provider or the provider icon is not found, return the default profile icon
            else
                return ("", dictionaryService.GetSprite(CosmeticType.Icons.ToString(), $"{CosmeticType.Icons}_{Consts.CollectionKeys.Default}_Male"));

            // Checks whether a string is a valid absolute HTTP/HTTPS URL
            bool IsValidHttpUrl(string value)
            {
                // Return false if string is null or empty
                if (string.IsNullOrWhiteSpace(value))
                    return false;

                // Try to create a URI from the string
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    return false;

                // Ensure scheme is HTTP or HTTPS only
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
        }

        // Get the profile board based on the player's board skin ID or default to the default board icon
        public (string id, Sprite icon) GetBoard() => PlayerProfileData?.boardSkinID is not null or ""
            ? (PlayerProfileData.boardSkinID, dictionaryService.GetSprite(CosmeticType.Boards.ToString(), PlayerProfileData.boardSkinID))
            : ("", dictionaryService.GetSprite(CosmeticType.Boards.ToString(), $"{CosmeticType.Boards}_{Consts.CollectionKeys.Default}"));

        // Get the profile fund icon based on the player's board fund skin ID or default to the default board fund icon
        public (string id, Sprite icon) GetBoardFund() => PlayerProfileData?.boardFundSkinID is not null or ""
            ? (PlayerProfileData.boardFundSkinID, dictionaryService.GetSprite(CosmeticType.Fund.ToString(), PlayerProfileData.boardFundSkinID))
            : ("", dictionaryService.GetSprite(CosmeticType.Fund.ToString(), $"{CosmeticType.Fund}_{Consts.CollectionKeys.Default}"));

        // Get the profile fund icon based on the player's board fund skin ID or default to the default board fund icon
        public (string id, Sprite icon) GetTilePreview() => PlayerProfileData?.tileSkinID is not null or ""
            ? (PlayerProfileData.tileSkinID, dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), PlayerProfileData.tileSkinID))
            : ("", dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), $"{CosmeticType.Tiles}_{Consts.CollectionKeys.Default}"));

        // Get the profile tiles based on the player's tile skin ID or default to the default tile skin
        public (string id, Sprite[] icons) GetTiles() => PlayerProfileData?.tileSkinID is not null or ""
            ? (PlayerProfileData.tileSkinID, dictionaryService.GetSpriteCollection($"{CosmeticType.Tiles}_{PlayerProfileData.tileSkinID}"))
            : ("", dictionaryService.GetSpriteCollection($"{CosmeticType.Tiles}_{CosmeticType.Tiles}_{Consts.CollectionKeys.Default}")); // It's redundant but it needs to be like this to match the naming convention in the dictionary service and the cosmetic default id without hardcoding the default values

        public (string id, Sprite icon) GetBackTile() => PlayerProfileData?.tileSkinID is not null or ""
            ? ($"{PlayerProfileData.tileSkinID}_{Consts.CollectionKeys.Back}", dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), $"{PlayerProfileData.tileSkinID}_{Consts.CollectionKeys.Back}"))
            : ("", dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), $"{CosmeticType.Tiles}_{Consts.CollectionKeys.Default}_{Consts.CollectionKeys.Back}"));

        public (string id, Sprite icon)?[] GetBadges() => PlayerProfileData?.badgesIDs
            ?.Select(badge => !string.IsNullOrEmpty(badge)
                ? (badge, dictionaryService.GetSprite(Consts.CollectionKeys.Achievements, badge))
                : default((string id, Sprite icon)?))
            ?.ToArray();

        /// <summary>
        /// Retrieves a sprite from the specified collection using the given icon ID, utilizing caching for performance.
        /// </summary>
        /// <param name="iconId">The identifier of the icon to retrieve.</param>
        /// <param name="collection">The name of the sprite collection to search.</param>
        /// <returns>The requested sprite if found; otherwise, the first sprite from the collection or null.</returns>
        public Sprite GetSprite(string iconId, string collection = null)
        {
            if (!dictionaryService)
            {
                Debug.LogError("DictionaryService reference is missing. Cannot retrieve sprite.");
                return null;
            }

            // Check if the icon ID is null
            if (string.IsNullOrEmpty(iconId))
            {
                // If the collection is not null or empty, try to return the first sprite from the collection as a default icon, otherwise return null
                if (!string.IsNullOrEmpty(collection))
                {
                    Debug.Log($"[LeaderboardManager] Icon ID is null or empty, attempting to use default icon from collection {collection}.");
                    return dictionaryService.GetSpriteCollection(collection)?.FirstOrDefault();
                } 
                else
                    return null;
            }

            if (!cachedSprites.TryGetValue(iconId, out var cachedSprite))
            {
                var iconToCache = default(Sprite);
                if (!string.IsNullOrEmpty(iconId) && !string.IsNullOrEmpty(collection))
                {
                    Debug.Log("[LeaderboardManager] Attempting to get sprite locally for icon ID {iconId} from collection {collection}.");
                    iconToCache = dictionaryService.GetSprite(collection, iconId); // Get the profile sprite using the provided function
                }

                if (!iconToCache && !string.IsNullOrEmpty(collection))
                {
                    Debug.Log($"[LeaderboardManager] Sprite not found remotely for icon ID {iconId}, attempting to use default icon from collection {collection}.");
                    iconToCache = dictionaryService.GetSpriteCollection(collection)?.FirstOrDefault();
                }

                // Cache the sprite for future use
                if (iconToCache)
                    lock (cachedSprites)
                    {
                        cachedSprites[iconId] = iconToCache;
                    }

                // Override the sprite to return with the obtained one (even if it's null) to avoid trying to get it again in the future if it was not found
                cachedSprite = iconToCache;
            }

            return cachedSprite;
        }
        

        /// <summary>
        /// Retrieves a sprite for the specified icon ID, optionally from a collection and with the option to download
        /// if not found locally.
        /// </summary>
        /// <param name="iconId">The unique identifier of the icon to retrieve.</param>
        /// <param name="collection">The name of the collection to search for the icon, or null to use the default.</param>
        /// <returns>A task that represents the asynchronous operation and returns the requested sprite, or null if not found.</returns>
        public async UniTask<Sprite> GetSpriteAsync(string iconId, string collection = null)
        {
            if (!dictionaryService)
            {
                Debug.LogError("DictionaryService reference is missing. Cannot retrieve sprite.");
                return null;
            }

            if (!cachedSprites.TryGetValue(iconId, out var cachedSprite))
            {
                var iconToCache = default(Sprite);
                if (!string.IsNullOrEmpty(iconId) && !string.IsNullOrEmpty(collection))
                {
                    Debug.Log("[LeaderboardManager] Attempting to get sprite locally for icon ID {iconId} from collection {collection}.");
                    iconToCache = dictionaryService.GetSprite(collection, iconId); // Get the profile sprite using the provided function
                }

                if (!iconToCache)
                {
                    Debug.Log($"[LeaderboardManager] Sprite not found locally for icon ID {iconId}, attempting to download provider icon.");
                    iconToCache = await authManager.DownloadAvatar(iconId); // Try to download provider icon if not found
                }

                if (!iconToCache && !string.IsNullOrEmpty(collection))
                {
                    Debug.Log($"[LeaderboardManager] Sprite not found remotely for icon ID {iconId}, attempting to use default icon from collection {collection}.");
                    iconToCache = dictionaryService.GetSpriteCollection(collection)?.FirstOrDefault();
                }

                // Cache the sprite for future use
                if (iconToCache)
                    lock (cachedSprites)
                    {
                        cachedSprites[iconId] = iconToCache;
                    }

                // Override the sprite to return with the obtained one (even if it's null) to avoid trying to get it again in the future if it was not found
                cachedSprite = iconToCache;
            }

            return cachedSprite;
        }

        /// <summary>
        /// This method allows other classes to subscribe to the sign-in event<br></br>
        /// The purpose is to be called after the player data is completely loaded
        /// </summary>
        public void HandleOnSignIn(UnityAction onSignIn)
        {
            // Check if the Unity services are ready to be used
            if (onSignIn is null)
            {
                Debug.LogWarning($"<b>[{nameof(HandleOnSignIn)}]</b> Couldn't be called. onSignIn action is null");
                return;
            }

            onSignedIn.AddListener(onSignIn);
        }

        /// <summary>
        /// This method allows other classes to unsubscribe from the sign-in event
        /// </summary>
        public void UnHandleOnSignIn(UnityAction onSignIn)
        {
            // Check if the Unity services are ready to be used
            if (onSignIn is null)
            {
                Debug.LogWarning($"<b>[{nameof(UnHandleOnSignIn)}]</b> Couldn't be called. onSignIn action is null");
                return;
            }

            onSignedIn.RemoveListener(onSignIn);
        }

        /// <summary>
        /// This method allows other classes to subscribe to the sign-out event<br></br>
        /// The purpose is to be called after the player data is completely cleared
        /// </summary>
        public void HandleOnSignOut(UnityAction onSignOut)
        {
            // Check if the Unity services are ready to be used
            if (onSignOut is null)
            {
                Debug.LogWarning($"<b>[{nameof(HandleOnSignOut)}]</b> Couldn't be called. onSignOut action is null");
                return;
            }

            onSignedOut.AddListener(onSignOut);
        }

        /// <summary>
        /// This method allows other classes to unsubscribe from the sign-out event
        /// </summary>
        public void UnHandleOnSignOut(UnityAction onSignOut)
        {
            // Check if the Unity services are ready to be used
            if (onSignOut is null)
            {
                Debug.LogWarning($"<b>[{nameof(UnHandleOnSignOut)}]</b> Couldn't be called. onSignOut action is null");
                return;
            }

            onSignedOut.RemoveListener(onSignOut);
        }

        [Serializable]
        public class GameConfig
        {

            [JsonProperty(nameof(TargetFrameRate))]
            public int TargetFrameRate { get; private set; }

            [JsonProperty(nameof(BGMVolume))]
            public float BGMVolume { get; private set; }

            [JsonProperty(nameof(SFXVolume))]
            public float SFXVolume { get; private set; }

            [JsonProperty(nameof(Language))]
            public string Language { get; set; }


            public GameConfig()
            {
                // Default constructor for JSON serialization
                TargetFrameRate = (int)GameManager.TargetFrameRate.Default;
                BGMVolume = .5f;
                SFXVolume = .75f;
                Language = "en";
            }

            public void SetTargetFrameRate(TargetFrameRate targetFrameRate) => TargetFrameRate = (int)targetFrameRate;
            public void SetBGMVolume(float volume) => BGMVolume = Mathf.Clamp01(volume);
            public void SetSFXVolume(float volume) => SFXVolume = Mathf.Clamp01(volume);
            public void SetLanguage(string language) => Language = language;
        }

        public enum TargetFrameRate
        {
            Default = 30,
            Low = 15,
            High = 60,
        }

        /// <summary>
        /// Model to store search results along with the search time for caching purposes.
        /// </summary>
        public class SearchDataModel
        {
            public List<FirestoreClubData> clubsFound;
            public DateTime searchTime;

            public SearchDataModel(List<FirestoreClubData> clubsFound, DateTime searchTime)
            {
                this.clubsFound = clubsFound;
                this.searchTime = searchTime;
            }
        }
    }
}
