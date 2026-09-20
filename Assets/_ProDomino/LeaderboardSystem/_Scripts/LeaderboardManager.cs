using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.NationalitySystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms.Impl;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Leaderboard
{
    public class LeaderboardManager : SingleInstanceMonoBehaviour<LeaderboardManager>, IService
    {
        [SerializeField] private bool isSavingInCache = false;
        [SerializeField] private bool isTestingDummyNextUpdateTime = false;

        private AuthManager authManager;
        private GameManager gameManager;
        private DictionaryService dictionaryService;
        
        /// <summary>
        /// Array containing data for players on the leaderboard.
        /// </summary>
        private PlayerLeaderboardData[] leaderboardPlayers;

        /// <summary>
        /// Array containing leaderboard data for players by nationality.
        /// </summary>
        private PlayerLeaderboardData[] nationalityPlayers;

        /// <summary>
        /// Stores the current leaderboard entries for this client
        /// </summary>
        private Dictionary<string, LeaderboardEntry> currentClientLeaderboards;
        
        private DateTime nextUpdateTime;
        private List<string> leaderboardsIds;


        private BackendBindings _module;
        internal BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        public LeaderboardCache CurrentLeaderboardCache { get; private set; }

        private NationalityController _nationalityController;
        internal NationalityController NationalityController
        {
            get
            {
                if (_nationalityController == null)
                {
                    _nationalityController = FindFirstObjectByType<NationalityController>(findObjectsInactive: FindObjectsInactive.Exclude);
                    if (_nationalityController == null)
                        Debug.LogWarning($"{nameof(NationalityController)} not found in the scene");
                }
                return _nationalityController;
            }
        }



        private AbstractLeaderboardUI _leaderboardUI;

        internal AbstractLeaderboardUI LeaderboardUI
        {
            get
            {
                if (_leaderboardUI == null)
                {
                    _leaderboardUI = FindFirstObjectByType<AbstractLeaderboardUI>(findObjectsInactive: FindObjectsInactive.Exclude);
                    if (_leaderboardUI == null)
                        Debug.LogWarning($"{nameof(AbstractLeaderboardUI)} not found in the scene");
                }

                return _leaderboardUI;
            }
        }

        public bool IsAlreadyInitialized { get; private set; }

        private string[] _gameModes;
        public string[] GameModes => _gameModes ??= Enum.GetNames(typeof(GameMode))
            ?.Where(x => x != GameMode.none.ToString())
            ?.Select(x => x.CapitalizeFirstLetter())
            ?.ToArray();

        private string[] _leaderboardTiers;
        public string[] LeaderboardTiers => _leaderboardTiers ??= Enum.GetNames(typeof(LeaderboardTier))
            ?.Where(x => x != LeaderboardTier.None.ToString())
            ?.ToArray();

        private string[] _numberOfPlayers;

        public string[] NumberOfPlayers => _numberOfPlayers ??= new string[] { "1v1", "1v3" };

        protected override void Awake()
        {
            base.Awake();

            // Iterate through all combinations of game modes, number of players, and leaderboard tiers to get the leaderboard scores tasks
            
            leaderboardsIds = new List<string>();
            foreach (var number in NumberOfPlayers)
                foreach (var gameMode in GameModes)
                {
                    var leaderboardID = $"{gameMode}{number}";
                    leaderboardsIds.Add(leaderboardID);
                }

            // Set next UTC midnight at start
            nextUpdateTime = !isTestingDummyNextUpdateTime ? DateTime.UtcNow.Date.AddDays(1) : DateTime.UtcNow.AddMinutes(5);

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            if (!gameManager || !authManager || !dictionaryService)
            {
                Debug.LogError("Required services are not initialized. Cannot proceed with LeaderboardMAnager initialization.");
                return;
            }

            if (gameManager)
            { 
                // Add listener for sign in event
                gameManager.HandleOnSignIn(OnSignIn);

                // Add listener for sign out event
                gameManager.HandleOnSignOut(OnSignOut);
            }
            else
                Debug.LogError("GameManager is not initialized. Cannot add listeners for sign in and sign out events.");

            if (LeaderboardUI)
                LeaderboardUI.Awake_LeaderboardUI();
            else
                Debug.LogError("Leaderboard UI is not initialized. Cannot call Awake_LeaderboardUI.");

            // Add listener for nationality
            if (NationalityController)
                NationalityController.HandleOnNationalitySelected(TryToGetLeaderboardsFromRTDB_Externally);
            else
                Debug.LogError("Nationality Controller is not assigned in the scene or could not be found. National leaderboards will not be updated on nationality change.");


            // Wait until the AuthManager is initialized
            if (IsAlreadyInitialized)
                OnSignIn();
            else
                OnSignOut();
        }

        private async void Start()
        {
            LeaderboardUI?.Start_LeaderboardUI();

            // Wait until the GameManager is initialized and authenticated
            await UniTask.WaitUntil(() => gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticatedAndVerified: true });

            // Check if tha data will be load|save from player prefs
            if (isSavingInCache)
            { 
                // Then, check if the leaderboard has registered data in cache. If so, try to load it.
                TryToLoadLeaderboardCachedData();

                // If the leaderboard cache is not null, try to get the leaderboards from the cache
                if (CurrentLeaderboardCache is not null)
                    await TryToGetLeaderboardsFromCache();
            }

            // If the leaderboard players still null or empty, try to get the leaderboards from the service
            if (leaderboardPlayers is null || leaderboardPlayers.Length == 0)
            {
                // Call Cloud Code module to refresh leaderboard metadata
                await TryToGetLeaderboardsFromUGS();

                // Register the leaderboard in cache
                if (isSavingInCache)
                    RegisterLeaderboardCachedData();
            }

            // Start the validation loop
            ScheduleLeaderboardDailyReset().Forget();

            IsAlreadyInitialized = true;

            // Once the leaderboardPlayers array is populated, initialize the UI
            if (LeaderboardUI is LeaderboardUI_New leaderboardUI_New)
                leaderboardUI_New.Initialize
                    (checkIfIsSavingInCache: () => isSavingInCache,
                    getNextUpdateTime: () => nextUpdateTime,
                    currentClientLeaderboards: () => currentClientLeaderboards,
                    getLeaderboardPlayers: () => leaderboardPlayers,
                    getLeaderboardID: GetLeaderboardID,
                    getSprite: gameManager.GetSprite,
                    getNationalityPlayers: () => nationalityPlayers,
                    tryToGetLeaderboardsFromRTDB: TryToGetLeaderboardsFromRTDB);

            // But, if is an old leaderboard UI, initialize it differently
            else
                LeaderboardUI?.Initialize
                    (() => isSavingInCache,
                    () => nextUpdateTime,
                    () => currentClientLeaderboards,
                    () => leaderboardPlayers,
                    GetLeaderboardID,
                    gameManager.GetSprite);
        }

        private void Update()
        {
            // Check if the GameManager is initialized and if the leaderboard UI is ready to be updated
            if (gameManager is null or { IsAlreadyInitialized: false} || !IsAlreadyInitialized)
                return;

            // Update the leaderboard UI if it is initialized
            LeaderboardUI?.Update_LeaderboardUI();
        }

        private void OnDestroy()
        {
            if (gameManager)
            { 
                // Remove listener for sign in event
                gameManager.UnHandleOnSignIn(OnSignIn);

                // Remove listener for sign out event
                gameManager.UnHandleOnSignOut(OnSignOut);
            }

            if (NationalityController)
                NationalityController.UnHandleOnNationalitySelected(TryToGetLeaderboardsFromRTDB_Externally);
        }

        internal void AddListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            LeaderboardUI.AddListener_OnPlayerLeaderboardDataUpdated(listener);
        }
        internal void RemoveListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            LeaderboardUI.RemoveListener_OnPlayerLeaderboardDataUpdated(listener);
        }

        /// <summary>
        /// Retrieves the leaderboard data from the cache if available.
        /// </summary>
        private async UniTask TryToGetLeaderboardsFromCache()
        {
            if (CurrentLeaderboardCache is null or { playerLeaderboardData: null or { Length: 0 } })
            {
                Debug.LogWarning("CurrentLeaderboardCache is null or empty. Cannot get leaderboard from cache.");
                return;
            }

            // Convert the cached leaderboard models to LeaderboardPlayer array
            leaderboardPlayers = CurrentLeaderboardCache.playerLeaderboardData;

            // Update the general leaderboard players list with the latest
            UpdateNationalityList(ref nationalityPlayers, leaderboardPlayers);

            // Try to register the lefting entries in the cache collection
            await TryToCachePlayersProfileIcons(leaderboardPlayers);
        }

        private async UniTask TryToLoadOwnLeadearboadData()
        {
            var tasks = new List<UniTask<(string leadeboardID, LeaderboardEntry leaderboardEntry)>>();

            // Iterate through all combinations of game modes, number of players, and leaderboard tiers to get the leaderboard scores tasks
            foreach (var number in NumberOfPlayers)
                foreach (var gameMode in GameModes)
                {
                    var leaderboardID = $"{gameMode}{number}";
                    tasks.Add(GetPlayerLeaderboardEntryWithContext(leaderboardID));
                }

            // Check if there are any tasks to process
            if (tasks.Count == 0)
            {
                Debug.LogWarning("No leaderboard tasks were created. Check your game modes, number of players, and leaderboard tiers.");
                return;
            }

            // Wait for all tasks to complete and collect the results
            var results = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => UniTask.WhenAll(tasks),
                taskId: nameof(LeaderboardsService.Instance.GetPlayerScoreAsync),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true,
                shouldRetrySomeTimes: false,
                returnExceptionOnError: true);

            currentClientLeaderboards = results
                ?.Where(x => !string.IsNullOrEmpty(x.leadeboardID) && x.leaderboardEntry is not null)
                ?.ToDictionary(x => x.leadeboardID, x => x.leaderboardEntry);

            Debug.Log($"Loaded {currentClientLeaderboards?.Count ?? 0} player leaderboard entries.");

            async UniTask<(string leaderboardId, LeaderboardEntry entry)> GetPlayerLeaderboardEntryWithContext(string leaderboardId)
            {
                try
                {
                    var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId);
                    return (leaderboardId, entry);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error loading leaderboard:{leaderboardId}.\n\nError: \n{ex?.Message ?? string.Empty}\n");
                    return default;
                }
            }
        }

        /// <summary>
        /// Retrieves leaderboards from the real-time database for the specified nationality type.
        /// </summary>
        /// <param name="nationalityType">The nationality type for which to retrieve leaderboards.</param>
        private async void TryToGetLeaderboardsFromRTDB_Externally(NationalityType nationalityType)
        {
            await TryToGetLeaderboardsFromRTDB(nationalityType);

            // Once we have the latest data from the RTDB, we can configure the filters in the UI based on the nationalities that we have data for.
            // This is because the RTDB will return data for specific nationalities, and we want to ensure the UI reflects the available data.
            if (LeaderboardUI)
                LeaderboardUI.ConfigureFilters();
            else
                Debug.LogWarning("Leaderboard UI is not initialized. Cannot configure filters after getting leaderboards from RTDB.");
        }

        /// <summary>
        /// Retrieves the leaderboard data from the Unity Services Leaderboards.
        /// </summary>
        /// <returns></returns>
        private async UniTask TryToGetLeaderboardsFromRTDB(NationalityType nationalityType)
        {
            if (!gameManager)
            { 
                Debug.LogError("GameManager is not initialized. Cannot get leaderboards from RTDB.");
                return;
            }

            // Wait for all tasks to complete and collect the results
            await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => gameManager.GetTopLeadeboard(leaderboardsIds?.ToArray(), nationalityType),
                taskId: nameof(gameManager.GetTopLeadeboard),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true,
                shouldRetrySomeTimes: false,
                returnExceptionOnError: true);

            // According the data got from RTDB, try to get the player data for each leaderboard entry, since the RTDB will only return
            // the player ID, username, score and updated time, but we need to get the profile icon and the achievements data for each player
            // to be able to display it in the UI. So we need to call a Cloud Code function that will return this data for us based on the player ID.
            var leaderboardPlayersDataResponses = await TryToGetLeaderboardEntriesPlayerData((nationalityType, gameManager.TopNationalityRanking));

            // Process the results to create leaderboard models
            nationalityPlayers = leaderboardPlayersDataResponses
                ?.Select(x => x.playerLeaderboardData)
                ?.ToArray();

            // Update the general leaderboard players list with the latest data from the nationality-specific leaderboard.
            UpdateNationalityList(ref leaderboardPlayers, nationalityPlayers);

            Debug.Log($"Loaded {leaderboardPlayers?.Length ?? 0} leaderboard entries.");
        }

        /// <summary>
        /// Retrieves the leaderboard data from the Unity Services Leaderboards.
        /// </summary>
        /// <returns></returns>
        private async UniTask TryToGetLeaderboardsFromUGS()
        {
            var tasks = new List<UniTask<(string leadeboardID, LeaderboardTierScoresPage leaderboardTierScorePage)?>>();
            var getScoreOptions = new GetScoresByTierOptions()
            {
                Limit = gameManager.GameBackendConfigData.leaderboardConfig.maxPlayers, // Set a limit for the number of scores to retrieve
            };

            // Iterate through all combinations of game modes, number of players, and leaderboard tiers to get the leaderboard scores tasks
            foreach (var number in NumberOfPlayers)
                foreach (var gameMode in GameModes)
                {
                    var leaderboardID = $"{gameMode}{number}";
                    tasks.AddRange(LeaderboardTiers.Select(tier =>
                       GetLeaderboardTierPageWithContext(leaderboardID, tier, getScoreOptions)
                    ));
                }

            // Check if there are any tasks to process
            if (tasks.Count == 0)
            {
                Debug.LogWarning("No leaderboard tasks were created. Check your game modes, number of players, and leaderboard tiers.");
                return;
            }

            // Try to get the player leaderboard data for each player in the leaderboard using a Cloud Code function, since the leaderboard service will only return the
            // player ID, username, score and updated time, but we need to get the profile icon and the achievements data for each player
            var leaderboardPlayersData = await TryToGetLeaderboardEntriesPlayerData();

            // Process the results to create leaderboard models
            leaderboardPlayers = leaderboardPlayersData
                ?.Select(x => x.playerLeaderboardData)
                ?.ToArray();

            // If the leaderboard players is still null or empty, try to get the player leaderboard data for the players in the leaderboard using the client SDK,
            // since maybe there was an error getting the data from the cloud code function, but we can still try to get the data for the players in the leaderboard using the client SDK. 
            if (leaderboardPlayers is null or { Length: 0 })
                await SetPlayerLeaderboardUsingClientSDK();

            // Update the general leaderboard players list with the latest
            UpdateNationalityList(ref nationalityPlayers, leaderboardPlayers);

            // Try to register the lefting entries in the cache collection
            await TryToCachePlayersProfileIcons(leaderboardPlayers);

            Debug.Log($"Loaded {leaderboardPlayers?.Length ?? 0} leaderboard entries.");

            async UniTask SetPlayerLeaderboardUsingClientSDK()
            {
                // Wait for all tasks to complete and collect the results
                var results = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => UniTask.WhenAll(tasks),
                    taskId: nameof(LeaderboardsService.Instance.GetScoresByTierAsync),
                    showLoading: false,
                    shouldIgnoreTryAgainProcess: true,
                    shouldRetrySomeTimes: false,
                    returnExceptionOnError: true);

                // Process the results to create leaderboard models
                leaderboardPlayers = results
                    ?.Where(x => x is not null and { leaderboardTierScorePage: not null and { Results: not null } })
                    ?.SelectMany(x => x.Value.leaderboardTierScorePage.Results.Select(y => (x.Value.leadeboardID, leaderboardEntry: y)))
                    ?.Select(tuple =>
                    {
                        // Tuple that contains the leaderboard ID and the leaderboard entry for each player in the results obtained from the leaderboard service
                        var (leaderboardID, leaderboardEntry) = tuple;

                        // For each leaderboard entry, try to find the corresponding player data in the leaderboardPlayersData collection,
                        // which contains the profile icon and achievements data for each player.
                        var specificPlayerDataResult = leaderboardPlayersData?.FirstOrDefault(x => x.playerLeaderboardData.playerId == leaderboardEntry.PlayerId);

                        // If the player data is not found or if there is an error, log a warning and skip this entry.
                        if (specificPlayerDataResult is null or { playerLeaderboardData: null } or { error: not null and not "" })
                            Debug.LogWarning($"No player data found for player {leaderboardEntry.PlayerId} in leaderboard {leaderboardID}. Skipping this entry\nError: {specificPlayerDataResult?.error ?? "No error message"}");

                        return specificPlayerDataResult?.playerLeaderboardData;
                    })
                    ?.Where(x => x is not null)
                    ?.ToArray();
            }

            async UniTask<(string leaderboardId, LeaderboardTierScoresPage page)?> GetLeaderboardTierPageWithContext(string leaderboardId, string tier, GetScoresByTierOptions options)
            {
                try
                {
                    var page = await LeaderboardsService.Instance.GetScoresByTierAsync(leaderboardId, tier, options);
                    return (leaderboardId, page);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error loading leaderboard:{leaderboardId}, Tier <b>{tier}</b>.\n\nError: \n{ex.Message}\n");
                    return null;
                }
            }
        }

        /// <summary>
        /// Tries to load the cached leaderboard data from PlayerPrefs.
        /// </summary>
        private void TryToLoadLeaderboardCachedData()
        {
            // Try to load cached leaderboard data from PlayerPrefs
            var cacheJson = PlayerPrefs.GetString(Consts.PlayerPrefs.LeaderboardCache, null);
            if (string.IsNullOrEmpty(cacheJson))
            { 
                Debug.LogWarning("No cached leaderboard data found");
                return;
            }

            // Try to deserialize the cached data; if the data is not in the expected format, log a warning and return
            if (JsonConvert.DeserializeObject<LeaderboardCache>(cacheJson) is not LeaderboardCache cachedData)
            {
                Debug.LogWarning("Failed to deserialize cached leaderboard data");
                return;
            }

            // Check if the cached data is valid
            if (cachedData.playerLeaderboardData is null or { Length: 0 })
            {
                Debug.LogWarning("Cached leaderboard data is empty or null");
                return;
            }

            // Check if the cached data is expired (e.g., older than 30 minutes)
            if (cachedData.timeToUpdate < DateTime.UtcNow)
            {
                Debug.LogWarning("Cached leaderboard data is expired");
                return;
            }

            Debug.Log($"Loaded leaderboard from cache with {cachedData.playerLeaderboardData.Length} entries.");
            CurrentLeaderboardCache = cachedData;
        }

        /// <summary>
        /// Try to refresh the data registered in each leaderboard entry
        /// </summary>
        /// <returns></returns>
        private async UniTask<PlayerLeaderboardDataResult[]> TryToGetLeaderboardEntriesPlayerData(
            (NationalityType nationality, Dictionary<string, List<RTDBPlayerLeaderboardData>> topByLeaderboards)? optionalLeaderboardData = null)
        {
            // Iterate through all combinations of game modes, number of players, and leaderboard tiers to get the leaderboard scores tasks
            var leaderboardsIDs = new List<string>();
            foreach (var number in NumberOfPlayers)
                foreach (var gameMode in GameModes)
                {
                    var leaderboardID = $"{gameMode}{number}";
                    leaderboardsIDs.Add(leaderboardID);
                }

            // Serialize the leaderboard id to be use from cloud code
            var payload = new Dictionary<string, object>()
            {
                ["leaderboardIds"] = leaderboardsIDs
            };

            // If there are optional player scores to be sent, add them to the payload
            // This is to get specific player data for those scores, since the cloud code function can handle to get data for specific scores
            // if they are sent from the client, or get data for the player scores in the leaderboard if no specific scores are sent.
            if (optionalLeaderboardData is not null and { topByLeaderboards: not null and { Count: > 0 } })
                payload["optionalLeaderboardData"] = new 
                {
                    nationality = optionalLeaderboardData.Value.nationality,
                    topByLeaderboards = optionalLeaderboardData.Value.topByLeaderboards
                };

            // Serialize the payload to be used in the cloud code function
            var encryptedJsonData = authManager.SerializeAndEncryptData(payload);

            var saveMetadataEncriptedData = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.GetLeaderboardEntriesPlayerData(encryptedJsonData).AsUniTask(),
                taskId: nameof(module.GetLeaderboardEntriesPlayerData),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true);

            // Deserialize and decrypt the data
            var leaderboardsPlayerDataResponse = authManager.DeserializeAndDecryptData<GetLeaderboardPlayerDataResponse>(saveMetadataEncriptedData);
            if (leaderboardsPlayerDataResponse == null)
            {
                Debug.LogError("Failed to deserialize or decrypt the leaderboard entries player data response.");
                return null;
            }

            if (leaderboardsPlayerDataResponse.results is null or { Count: 0 })
            {
                Debug.LogWarning("No results found in the leaderboard entries player data response.");
                return null;
            }

            // Register the leaderboard errors if any
            var errorMessages = leaderboardsPlayerDataResponse.results
                .Where(x => !string.IsNullOrEmpty(x.error))
                .Select(x => x.error)
                .ToList();

            // Show the error messages in the console if there are any
            if (errorMessages is not null and { Count: > 0 })
            {
                var errorMessageCollection = string.Join("\n* ", errorMessages);
                Debug.LogWarning($"Error getting leaderboard player data: \n\n* {errorMessageCollection}");
            }

            var successfulResults = leaderboardsPlayerDataResponse.results
                .Where(x => string.IsNullOrEmpty(x.error))
                .ToList();

            if (successfulResults is null or { Count: 0 })
            {
                Debug.LogWarning("No successful results found in the leaderboard entries player data response.");
                return null;
            }

            return successfulResults?.ToArray();
        }

        /// <summary>
        /// Registers the current leaderboard data in the cache.
        /// </summary>
        private void RegisterLeaderboardCachedData()
        {
            if (leaderboardPlayers is null or { Length: 0 })
            { 
                Debug.LogWarning("Could not register leaderboard in cache: leaderboardModels is null or empty.");
                return;
            }

            // Create the cache that will registered
            var leaderboardCacheData = new LeaderboardCache(leaderboardPlayers, DateTime.UtcNow.Date.AddDays(1));
            var json = JsonConvert.SerializeObject(leaderboardCacheData);

            PlayerPrefs.SetString(Consts.PlayerPrefs.LeaderboardCache, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Updates casual match analytics and leaderboard score based on the player's victory status.
        /// </summary>
        /// <param name="isVictory">Indicates whether the player won the match.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        public async UniTask UpdateCasualAnalytics(bool isVictory)
        {
            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(new Dictionary<string, object>
            {
                ["isPlayerWinner"] = isVictory,
            });

            // Use the backedn binding of the corresponding player to update the leaderboard score
            await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.UpdateCasualMatchAnalytics(encryptedJsonData).AsUniTask(), 
                taskId: nameof(module.UpdateCasualMatchAnalytics), 
                showLoading: false);
        }

        /// <summary>
        /// Updates the leaderboard result for a match, encrypts and sends the data to the backend, and refreshes the
        /// player's leaderboard UI.
        /// </summary>
        /// <param name="gameMode">The game mode of the match.</param>
        /// <param name="numberPlayers">The number of players in the match.</param>
        /// <param name="isVictory">Indicates whether the player won the match.</param>
        /// <param name="matchEMC">The EMC value associated with the match.</param>
        /// <returns>A UniTask representing the asynchronous leaderboard update operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the numberPlayers parameter is not a supported value.</exception>
        public async UniTask UpdateLeadeboardResult
            (GameMode gameMode, 
            NumberPlayers numberPlayers, 
            int resultPosition,
            float matchEMC)
        {
            // Use the capitalized game mode and number of players to create the leaderboard ID
            var capitalizedGameMode = gameMode.ToString().CapitalizeFirstLetter();
            var number = numberPlayers switch
            {
                NumberPlayers.oneVsOne => "1v1",
                NumberPlayers.oneVsThree => "1v3",
                _ => throw new ArgumentOutOfRangeException(nameof(numberPlayers), numberPlayers, null)
            };

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var leaderboardID = $"{capitalizedGameMode}{number}";
            var encryptedJsonData = authManager.SerializeAndEncryptData(new Dictionary<string, object>
            {
                ["leaderboardId"] = leaderboardID,
                ["resultPosition"] = resultPosition,
                ["matchEMC"] = matchEMC
            });

            // Use the backedn binding of the corresponding player to update the leaderboard score
            await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.UpdateLeaderboardScoreByMatchResult(encryptedJsonData).AsUniTask(), 
                taskId: nameof(module.UpdateLeaderboardScoreByMatchResult), 
                showLoading: false);

            // Try to get the player leaderboard data, if any. This will be used to show the player's own leaderboard entry in the UI.
            if (isSavingInCache)
                await TryToLoadOwnLeadearboadData();

            // Else, get everything from UGS
            else
                await UniTask.WhenAll(
                    TryToLoadOwnLeadearboadData(),
                    TryToGetLeaderboardsFromUGS());
        }

        /// <summary>
        /// Generates a leaderboard ID based on the specified game mode and number of players.
        /// </summary>
        /// <param name="gameMode">The game mode to include in the leaderboard ID.</param>
        /// <param name="numberPlayers">The number of players to include in the leaderboard ID.</param>
        /// <returns>A string representing the leaderboard ID for the given game mode and number of players.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the numberPlayers value is not supported.</exception>
        public static string GetLeaderboardID(GameMode gameMode, NumberPlayers numberPlayers)
        {
            var capitalizedGameMode = gameMode.ToString().CapitalizeFirstLetter();
            var number = numberPlayers switch
            {
                NumberPlayers.oneVsOne => "1v1",
                NumberPlayers.oneVsThree => "1v3",
                NumberPlayers.twoVsTwo => "2v2",
                _ => throw new ArgumentOutOfRangeException(nameof(numberPlayers), numberPlayers, null)
            };
            return $"{capitalizedGameMode}{number}";
        }
        
        public static (GameMode gameMode, NumberPlayers numberPlayers)? GetDataFromLeaderboardID(string leaderboardId)
        {
            if (string.IsNullOrEmpty(leaderboardId))
                return null;

            // Split at first digit: group 1 = text before, group 2 = number + rest
            var match = Regex.Match(leaderboardId, @"^([^\d]+)(\d.*)$");
            if (!match.Success)
                return null;

            string modePart = match.Groups[1].Value;  // "Block"
            string numberPart = "_" + match.Groups[2].Value; // "_1v3"

            // Try to parse both enums safely (case-insensitive)
            if (Enum.TryParse(modePart, true, out GameMode mode)
                && Enum.TryParse(numberPart, true, out NumberPlayers num))
            {
                return (mode, num);
            }

            return null;
        }


        /// <summary>
        /// Updates the specified player leaderboard array with the latest data from the source array based on matching
        /// player IDs.
        /// </summary>
        /// <param name="arrayToModify">The array of player leaderboard data to be updated.</param>
        /// <param name="source">The source array containing the most recent player leaderboard data.</param>
        private void UpdateNationalityList(ref PlayerLeaderboardData[] arrayToModify, PlayerLeaderboardData[] source)
        {
            // Check if the source and the array to modify are not null and have data, then iterate through the source data to update the corresponding entries in the array to modify with the latest data from the source.
            if (source is not null and { Length: > 0 } && arrayToModify is not null and { Length: > 0 })
                foreach (var nationalPlayer in source)
                {
                    // Try to find the index of the player in the general leaderboard players collection to update its data with the latest data from the national leaderboard. This is because the national leaderboard
                    // will have the most updated data for each player, since it is updated more frequently than the general leaderboard.
                    var matchingLeaderboardPlayerIndex = Array.FindIndex(arrayToModify, x => x.playerId == nationalPlayer.playerId);

                    // If a matching player is found in the general leaderboard collection, update its data with the latest data from the national leaderboard.
                    // This will ensure that the general leaderboard is updated with the latest data for each player, even if the general leaderboard data is not updated as frequently as the national leaderboard.
                    if (matchingLeaderboardPlayerIndex != -1)
                        arrayToModify[matchingLeaderboardPlayerIndex] = nationalPlayer.Clone() as PlayerLeaderboardData;
                }
        }

        /// <summary>
        /// Retrieves the leaderboard entry associated with the specified leaderboard ID.
        /// </summary>
        /// <param name="leaderboardID">The unique identifier of the leaderboard to retrieve.</param>
        /// <returns>The corresponding LeaderboardEntry if found; otherwise, null.</returns>
        public LeaderboardEntry GetCurrentClientLeaderboardEntry(string leaderboardID)
        { 
            if (string.IsNullOrEmpty(leaderboardID))
            {
                Debug.LogWarning("Leaderboard ID is null or empty. Cannot get leaderboard entry.");
                return null;
            }

            return (currentClientLeaderboards?.TryGetValue(leaderboardID, out var leaderboardEntry) ?? false) ? leaderboardEntry : null;
        }

        /// <summary>
        /// Asynchronously caches profile icon sprites for players in the provided leaderboard data.
        /// </summary>
        /// <param name="playerLeaderboardDatas">An array of player leaderboard data containing profile icon information.</param>
        /// <returns>A UniTask representing the asynchronous caching operation.</returns>
        private async UniTask TryToCachePlayersProfileIcons(PlayerLeaderboardData[] playerLeaderboardDatas)
        {
            if (playerLeaderboardDatas is null or { Length: 0 })
            {
                Debug.LogWarning("No player leaderboard data provided for caching icons.");
                return;
            }

            var cacheIconTasks = playerLeaderboardDatas
                .Where(data => data is not null and { playerProfileData: not null } && !string.IsNullOrEmpty(data.playerProfileData.profileIconID))
                .Select(data => gameManager.GetSpriteAsync(data.playerProfileData.profileIconID, Consts.CollectionKeys.Icons))
                .ToArray();

            await UniTask.WhenAll(cacheIconTasks);
        }

        /// <summary>
        /// Schedules an action to be executed at the next UTC midnight (00:00:00).
        /// </summary>
        private async UniTask ScheduleLeaderboardDailyReset()
        {
            if (!(gameManager?.IsAuthenticated ?? true))
            {
                Debug.LogWarning("Cannot schedule leaderboard daily reset. User is not authenticated.");
                return;
            }

            // Get current estimated server time
            DateTime currentUtcTime = DateTime.UtcNow;

            // If already past the target (e.g. due to device clock skew), trigger immediately
            if (currentUtcTime >= nextUpdateTime)
            {
                Debug.Log("Detected that current time is past next UTC midnight. Executing leaderboard daily reset immediately.");
                await OnDailyLeaderboardReset(); // Placeholder method — implement your logic here
                return;
            }

            // Calculate delay until next reset
            TimeSpan delay = nextUpdateTime - currentUtcTime;

            Debug.Log($"Scheduling next leaderboard daily reset in {delay.TotalMinutes:F1} minutes ({delay.Hours:D2}h:{delay.Minutes:D2}m:{delay.Seconds:D2}s).");

            // Wait asynchronously until the next reset time
            await UniTask.Delay(delay, DelayType.DeltaTime, PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());

            // Call the reset logic (implement your logic inside this method)
            await OnDailyLeaderboardReset();
        }

        private async void OnSignIn()
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticatedAndVerified: true })
            {
                // Try to get the player leaderboard data, if any. This will be used to show the player's own leaderboard entry in the UI.
                if (isSavingInCache)
                    await TryToLoadOwnLeadearboadData();

                // Else, get everything from UGS
                else
                    await UniTask.WhenAll(
                        TryToLoadOwnLeadearboadData(),
                        TryToGetLeaderboardsFromUGS());

                LeaderboardUI?.OnSignIn();
            }
        }

        private void OnSignOut()
        {
            currentClientLeaderboards = null;
            LeaderboardUI?.OnSignOut();
        }

        /// <summary>
        /// Placeholder for logic to run when leaderboard resets daily.
        /// </summary>
        private async UniTask OnDailyLeaderboardReset()
        {
            Debug.Log("Executing daily leaderboard reset...");

            // Call Cloud Code module to refresh leaderboard metadata
            await TryToGetLeaderboardsFromUGS();

            // Register the leaderboard in cache
            if (isSavingInCache)
                RegisterLeaderboardCachedData();

            // Update next update time to the next UTC midnight
            nextUpdateTime = DateTime.UtcNow.Date.AddDays(1);

            // Reset the scheduled task when it's done (don't use the 'await' keyword here to avoid blocking)
            ScheduleLeaderboardDailyReset().Forget();
        }

        [Serializable]
        public class LeaderboardCache
        {
            /// <summary>
            /// Collection that contains the data of the player. Key: player id, value: tuple
            /// </summary>
            public PlayerLeaderboardData[] playerLeaderboardData;
            public DateTime timeToUpdate;

            public LeaderboardCache(PlayerLeaderboardData[] playerLeaderboardData, DateTime timeToUpdate)
            {
                this.playerLeaderboardData = playerLeaderboardData;
                this.timeToUpdate = timeToUpdate;
            }
        }
    }
}
