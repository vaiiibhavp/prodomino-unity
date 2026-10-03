using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This partial class was create to content all info related to GlobalAnalyticSystem into the GameManager
    /// </summary>
    public partial class GameManager
    {
        private Dictionary<GameMode, bool> lastPlayerGameModeCollection;
        private UnityEvent onUpdateGlobaAnalyticsData;
        private bool wereInitializedGlobalAnalytics = false;

        // Must match the session TIMEOUT used by CheckIfSessionActive in firebasedatabase.jslib
        private const int ActivePlayersWindowSeconds = 60;
        [SerializeField] private float activePlayersRefreshInterval = 30f;
        private float activePlayersTimer;
        private bool isGlobalAnalyticsLive;

        public GlobalAnalyticsData GlobalAnalyticsData { get; private set; }

        /// <summary>
        /// Players with an online session (heartbeat inside the session timeout). -1 until the first count arrives.
        /// </summary>
        public int ActivePlayers { get; private set; } = -1;

        /// <summary>
        /// Initializes daily analytics when the player starts the game.
        /// Ensures the data structure exists and resets if the date changed.
        /// </summary>
        public async UniTask InitializeAnalyticsOnStartup()
        {
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning("Firebase Realtime Database client is only available on WebGL platform");
                return;
            }

            FirebaseDatabase.InitializeDailyAnalytics(
                gameObject.name,
                nameof(OnAnalyticsInitialized),
                nameof(OnAnalyticsInitFailed)
            );

            // Wait until session is active or sign out occurs
            await UniTask.WaitUntil(() => wereInitializedGlobalAnalytics || !authManager.IsAuthenticated)
                .TimeoutWithoutException(TimeSpan.FromSeconds(10));

            if (authManager.IsAuthenticated)
                StartGlobalAnalyticsLive();
        }

        /// <summary>
        /// Starts the realtime subscription to the global counters and the active players polling.
        /// </summary>
        private void StartGlobalAnalyticsLive()
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            FirebaseDatabase.SubscribeGlobalAnalytics(
                gameObject.name,
                nameof(OnAnalyticsUpdated),
                nameof(OnAnalyticsUpdateFailed)
            );

            isGlobalAnalyticsLive = true;
            activePlayersTimer = 0f;
            RequestActivePlayersCount();
        }

        /// <summary>
        /// Stops the realtime subscription, used on sign out
        /// </summary>
        private void StopGlobalAnalyticsLive()
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            if (isGlobalAnalyticsLive)
                FirebaseDatabase.UnsubscribeGlobalAnalytics();

            isGlobalAnalyticsLive = false;
            wereInitializedGlobalAnalytics = false;
            ActivePlayers = -1;
            GlobalAnalyticsData = null;
        }

        /// <summary>
        /// Active players come from session heartbeats, which change every few seconds, so they are polled instead of listened
        /// </summary>
        private void Update_GlobalAnalyticsSystem()
        {
            if (!isGlobalAnalyticsLive || !IsAuthenticated)
                return;

            activePlayersTimer += Time.unscaledDeltaTime;
            if (activePlayersTimer >= activePlayersRefreshInterval)
            {
                activePlayersTimer = 0f;
                RequestActivePlayersCount();
            }
        }

        private void RequestActivePlayersCount()
        {
            FirebaseDatabase.CountActivePlayers(
                ActivePlayersWindowSeconds,
                gameObject.name,
                nameof(OnActivePlayersCounted),
                nameof(OnActivePlayersCountFailed)
            );
        }

        public void OnActivePlayersCounted(string count)
        {
            if (!int.TryParse(count, out var value))
            {
                Debug.LogWarning($"[Analytics] Invalid active players count: {count}");
                return;
            }

            ActivePlayers = value;
            onUpdateGlobaAnalyticsData?.Invoke();
        }

        public void OnActivePlayersCountFailed(string error)
        {
            Debug.LogWarning($"[Analytics] Active players count failed: {error}");
        }


        public void AddListenerWhenUpdateGlobalAnalytics(UnityAction eventToListen)
        {
            if (onUpdateGlobaAnalyticsData is null)
            {
                Debug.LogWarning($"The event {nameof(onUpdateGlobaAnalyticsData)} is not initialized properly");
                return;
            }

            onUpdateGlobaAnalyticsData.AddListener(eventToListen);
        }
        
        public void RemoveListenerWhenUpdateGlobalAnalytics(UnityAction eventToListen)
        {
            if (onUpdateGlobaAnalyticsData is null)
            {
                Debug.LogWarning($"The event {nameof(onUpdateGlobaAnalyticsData)} is not initialized properly");
                return;
            }

            onUpdateGlobaAnalyticsData.RemoveListener(eventToListen);
        }

        private void OnAnalyticsInitialized(string jsonData)
        {
            wereInitializedGlobalAnalytics = true;

            if (string.IsNullOrEmpty(jsonData))
            {
                Debug.LogWarning("Couldn't register the global analytic data after initialization because its json si null or empty");
                return;
            }

            try
            {
                // Parse JSON to a simple structure if you want to log details
                GlobalAnalyticsData = JsonConvert.DeserializeObject<GlobalAnalyticsData>(jsonData);
                Debug.Log($"[Analytics Init] Initialization complete. Date: {GlobalAnalyticsData.lastUpdatedDate}, Games today: {GlobalAnalyticsData.gamesPlayedToday}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics Init] Failed to parse analytics data: {ex.Message}");
            }
        }

        private void OnAnalyticsInitFailed(string error)
        {
            wereInitializedGlobalAnalytics = true;

            Debug.LogError($"[Analytics Init] Initialization failed: {error}");
        }


        /// <summary>
        /// Updates analytics counters in batch mode.
        /// Each tuple defines one analytic change: (isAdding, globalType, gameMode).
        /// Prevents registering new game modes while one is already active.
        /// </summary>
        public void UpdateAnalyticsValue(params (bool isAdding, GlobalAnalyticType globalAnalyticType, GameMode globalAnalytic_GameMode)[] data)
        {
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning("Firebase Realtime Database client is only available on WebGL platform");
                return;
            }

            if (!IsAuthenticated)
            {
                Debug.LogWarning("User must be authenticated to update analytics.");
                return;
            }

            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("No analytic operations provided.");
                return;
            }

            // Ensure dictionary is initialized
            lastPlayerGameModeCollection ??= new();

            var validOperations = new List<(bool isAdding, GlobalAnalyticType globalAnalyticType, GameMode globalAnalytic_GameMode)>();

            foreach (var (isAdding, globalType, mode) in data)
            {
                // Skip replay mode entirely
                if (mode == GameMode.replay)
                {
                    Debug.Log("[Analytics] Replay mode ignored");
                    continue;
                }

                // Skip modes not tied to gameplay (like GamesPlayedToday)
                if (mode == GameMode.none)
                {
                    validOperations.Add((isAdding, globalType, mode));
                    continue;
                }

                // Get previous state
                lastPlayerGameModeCollection.TryGetValue(mode, out bool wasPlaying);

                // If adding, check if already playing another mode
                if (isAdding)
                {
                    // Check if player already playing another mode
                    var alreadyInGame = lastPlayerGameModeCollection.Any(x => x.Value);

                    // Prevent starting a new mode if already in another
                    if (alreadyInGame && !wasPlaying)
                    {
                        Debug.LogWarning($"Cannot start '{mode}' while another mode is active.");
                        continue;
                    }

                    // Register as active
                    lastPlayerGameModeCollection[mode] = true;
                } 
                else
                {
                    // Only allow exit if it was previously active
                    if (!wasPlaying)
                    {
                        Debug.LogWarning($"Cannot exit '{mode}' because it wasn't active.");
                        continue;
                    }

                    lastPlayerGameModeCollection[mode] = false;
                }

                // Register valid operation
                validOperations.Add((isAdding, globalType, mode));
            }

            if (validOperations.Count == 0)
            {
                Debug.Log("No valid analytic operations to send.");
                return;
            }

            // Convert tuple list into array-of-arrays (for JS compatibility)
            var arrayForm = validOperations
                .Select(x => new object[] { x.isAdding, (int)x.globalAnalyticType, (int)x.globalAnalytic_GameMode })
                .ToArray();

            var json = JsonConvert.SerializeObject(arrayForm);

            // Debug the JSON being sent to JS
            Debug.Log($"[Analytics] Sending JSON: {json}");

            // Send to JS library
            FirebaseDatabase.ModifyAnalyticsBatch(
                json,
                gameObject.name,
                nameof(OnAnalyticsUpdated),
                nameof(OnAnalyticsUpdateFailed)
            );
        }


        public void OnAnalyticsUpdated(string jsonData)
        {
            try
            {
                GlobalAnalyticsData = JsonConvert.DeserializeObject<GlobalAnalyticsData>(jsonData);
                onUpdateGlobaAnalyticsData?.Invoke();

                Debug.Log($"[Analytics Update] Analytics successfully updated. GamesToday={GlobalAnalyticsData.gamesPlayedToday}, UsersNow={GlobalAnalyticsData.usersPlayingNow}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics Update] Could not parse returned data: {ex.Message}");
            }
        }

        public void OnAnalyticsUpdateFailed(string error)
        {
            Debug.LogError($"[Analytics Update] Update failed: {error}");
        }
    }
}
