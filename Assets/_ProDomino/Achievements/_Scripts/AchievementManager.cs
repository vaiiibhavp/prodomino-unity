using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.MissionSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Manages player achievements, including progress tracking, reward claiming, data synchronization with the
    /// backend, and integration with UI and analytics systems.
    /// </summary>
    public partial class AchievementManager : SingleInstanceMonoBehaviour<AchievementManager>, IService
    {
        private GameManager gameManager;
        private AuthManager authManager;
        private MissionManager missionManager;
        private AnalyticsManager analyticsManager;
        private DictionaryService dictionaryService;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        private Dictionary<GameAchievementData, PlayerAchievementData> _playerAchievementsDataCollection;
        public Dictionary<GameAchievementData, PlayerAchievementData> PlayerAchievementsDataCollection => _playerAchievementsDataCollection ??= new();

        public bool IsAlreadyInitialized { get; private set; }
        public bool IsAuthenticated => gameManager?.IsAuthenticated ?? false;
        public DateTime? EstimatedServerTime => missionManager?.EstimatedServerTime;

        private AchievementUI _achievementUI;
        internal AchievementUI AchievementUI
        {
            get
            {
                if (_achievementUI == null)
                {
                    _achievementUI = FindFirstObjectByType<AchievementUI>();
                    if (_achievementUI == null)
                        Debug.LogWarning($"{nameof(AchievementSystem.AchievementUI)} not found in the scene");
                }

                return _achievementUI;
            }
        }

        public bool WasUpdate { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
            missionManager = ServiceLocator.Instance.GetService<MissionManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            if (!gameManager || !authManager || !analyticsManager || !missionManager || !dictionaryService)
            {
                Debug.LogError("AchievementManager: One or more required services are not available. Please ensure AuthManager, AnalyticsManager, MissionManager, and DictionaryService are initialized.");
                return;
            }

            _playerAchievementsDataCollection = new();
            AchievementUI?.Awake_AchievementUI();
        }

        private async void Start()
        {
            AchievementUI?.Start_AchievementUI();

            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => IsAuthenticated);

            analyticsManager.Subscribe(UpdatePlayerAchievementData);

            // Initialize the AchievementUI with the necessary parameters
            AchievementUI?.Initialize
                (() => IsAuthenticated,
                _tuple => dictionaryService?.GetSprite(Consts.CollectionKeys.Achievements, _tuple.achievement.ToString()) ?? dictionaryService?.GetSprite(Consts.CollectionKeys.Achievements, _tuple.rank.ToString()),
                _cosmeticID =>
                { 
                    var cosmeticData = gameManager?.GameCosmeticData?.FirstOrDefault(x => x.id == _cosmeticID);
                    return dictionaryService?.GetSprite(cosmeticData?.type.ToString() ?? Consts.CollectionKeys.Cosmetics, _cosmeticID);
                },
                () => PlayerAchievementsDataCollection,
                ClaimReward);

            await UpdateProgress(true);

            // Refresh the achievement data when the game starts
            await RefreshData(false);
        }


        // Unsubscribe from analytics updates when the object is destroyed
        private void OnDestroy()
        {
            // Unsubscribe from the analytics manager to avoid memory leaks
            if (analyticsManager)
                analyticsManager.Unsubscribe(UpdatePlayerAchievementData);
        }

        // Try to update progress when the application quits
        private void OnApplicationQuit()
        {
            UpdateProgress().Forget();
        }

        /// <summary>
        /// Refreshes player achievement data from the backend, updates internal collections, and refreshes the
        /// achievement UI.
        /// </summary>
        /// <param name="shouldRefreshdata">Indicates whether to refresh player data from the backend before updating achievement data.</param>
        /// <returns>A task representing the asynchronous refresh operation.</returns>
        private async UniTask RefreshData(bool shouldRefreshdata = true)
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot validate achievements. User is not authenticated.");
                return;
            }

            // Refresh player data from the backend
            if (shouldRefreshdata)
                await gameManager.RefreshProtectedPlayerData();

            // Get the player achievement data from the game manager
            var playerAchievementDatas = gameManager.PlayerAchievementDatas
                ?.Select(x => x.Clone() as PlayerAchievementData)
                ?.ToArray() ?? new PlayerAchievementData[0];

            if (playerAchievementDatas is null or { Length: 0 })
                Debug.LogWarning("No player achievement data found in the response. Cannot validate achievements.");

            // Clear existing collections
            _playerAchievementsDataCollection?.Clear();

            // Initialize collections
            UpdateAchievementDataCollection
                (ref _playerAchievementsDataCollection,
                playerAchievementDatas);

            // Concat daily and weekly achievement data collections and configure the UI
            var allAchievementsDataCollection = _playerAchievementsDataCollection?.ToDictionary(x => x.Key, x => x.Value);

            // Refresh the AchievementUI elements with the combined achievement data collection
            AchievementUI.RefreshElements();

            // Configure the AchievementUI
            AchievementUI.ConfigureUI();

            void UpdateAchievementDataCollection
                (ref Dictionary<GameAchievementData, PlayerAchievementData> achievementDataCollection,
                PlayerAchievementData[] playerAchievementDatas)
            {
                if (gameManager.GameAchievementData is null or { Length: 0 })
                {
                    Debug.LogWarning("Game achievement data lists is null or empty.");
                    return;
                }

                // Iterate for each game achievement and get its corresponding player achievement data; later add it to the collection
                foreach (var gameAchievementData in gameManager.GameAchievementData)
                {
                    if (gameAchievementData is null or { achievement: Achievement.None })
                        continue;

                    var playerAchievementData = playerAchievementDatas?.FirstOrDefault(m => m.achievement == gameAchievementData.achievement);
                    achievementDataCollection?.Add(gameAchievementData, playerAchievementData);
                }
            }
        }

        /// <summary>
        /// Updates the player's achievement progress by sending the latest data to the backend if the user is
        /// authenticated.
        /// </summary>
        /// <param name="isForced">If true, forces the update regardless of local achievement data state.</param>
        /// <returns>A UniTask representing the asynchronous update operation.</returns>
        private async UniTask UpdateProgress(bool isForced = false)
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot update achievement progress. User is not authenticated.");
                return;
            }

            var playerAchievements = default(List<PlayerAchievementData>);
            if (!isForced)
            { 
                playerAchievements = PlayerAchievementsDataCollection?.Values
                    ?.Where(x => x is not null)
                    ?.ToList() ?? new();

                if (playerAchievements.Count == 0 || !WasUpdate)
                { 
                    Debug.LogWarning("No player achievements to update.");
                    return;
                }
            }

            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["updatesFromFrontend"] = playerAchievements?.ToArray(),
            });

            // Update achievement progress in the backend
            // Uses the GameManager proxy to handle loading and errors uniformly
            await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.UpdateAchievementProgress(dataEncrypted).AsUniTask(),
                taskId: nameof(module.UpdateAchievementProgress),
                showLoading: false);

            await RefreshData();

            // Reset the update flag after processing
            WasUpdate = false;
        }

        /// <summary>
        /// Claims the reward for a specified achievement if the user is authenticated and the achievement is eligible.
        /// </summary>
        /// <param name="achievement">The achievement for which to claim the reward.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask ClaimReward(Achievement achievement)
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot claim achievement reward. User is not authenticated.");
                return;
            }

            if (achievement is Achievement.None)
            {
                Debug.LogWarning("Player achievement is not defined (none). Cannot claim reward.");
                return;
            }

            // Find the achievement data in either daily or weekly collections
            var (gameAchievementData, playerAchievementData) = (KeyValuePair<GameAchievementData, PlayerAchievementData>)PlayerAchievementsDataCollection?.FirstOrDefault(x => x.Key.achievement == achievement);
            if (playerAchievementData is null || gameAchievementData is null)
            {
                Debug.LogWarning($"No achievement data found for type: {achievement.ToString()}");
                return;
            }

            // Check if the achievement is already claimed
            if (playerAchievementData.claimed)
            {
                Debug.LogWarning($"Achievement {achievement.ToString()} is already claimed.");
                return;
            }

            // Check if the achievement is not completed (only check for achievements that require progress)
            if (gameAchievementData.achievementType is AchievementType.PlaceTiles && playerAchievementData.progress < gameAchievementData.goalAmount)
            {
                Debug.LogWarning($"Achievement {achievement.ToString()} progress is not sufficient to claim reward. Current progress: {playerAchievementData.progress}, Goal: {gameAchievementData.goalAmount}");
                return;
            }

            // Try to update progress before claiming the reward
            await UpdateProgress();

            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["completedPlayerAchievement"] = achievement,
            });

            // Claim the achievement reward
            var configEncryptedData = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.ClaimAchievementReward(dataEncrypted).AsUniTask(),
                taskId: nameof(module.ClaimAchievementReward),
                showLoading: false);

            // Deserialize and decrypt the data
            var claimRewardResponse = authManager.DeserializeAndDecryptData<AchievementClaimRewardResponse>(configEncryptedData);

            // Once the reward is claimed, update the player achievement data
            await RefreshData();

            // If there are cosmetics in the reward, send an analytic for obtaining a cosmetic
            if (gameAchievementData.rewardCosmeticsIDs.Any(x => !string.IsNullOrEmpty(x)))
                analyticsManager?.SendAnalytic(AnalyticType.ObtainCosmetic);
        }

        /// <summary>
        /// Splits the input string into a list of lowercase tokens, removing diacritics and using spaces, hyphens, and
        /// underscores as delimiters.
        /// </summary>
        /// <param name="input">The string to be tokenized.</param>
        /// <returns>A list of tokens extracted from the input string.</returns>
        static internal List<string> Tokenize(string input)
        {
            var normalized = RemoveDiacritics(input.ToLowerInvariant());
            return normalized.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        /// <summary>
        /// Removes diacritic marks from the specified string.
        /// </summary>
        /// <param name="text">The input string from which diacritics will be removed.</param>
        /// <returns>A new string with diacritics removed from the input.</returns>
        static internal string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
