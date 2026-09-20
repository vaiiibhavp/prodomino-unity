using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.MissionSystem
{
    public partial class MissionManager : SingleInstanceMonoBehaviour<MissionManager>, IService
    {
        private GameManager gameManager;
        private AuthManager authManager;
        private AnalyticsManager analyticsManager;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        private Dictionary<PlayerMissionData, GameMissionData> _dailyMissionsDataCollection;
        public Dictionary<PlayerMissionData, GameMissionData> DailyMissionsDataCollection => _dailyMissionsDataCollection ??= new();

        private Dictionary<PlayerMissionData, GameMissionData> _weeklyMissionsDataCollection;
        public Dictionary<PlayerMissionData, GameMissionData> WeeklyMissionsDataCollection => _weeklyMissionsDataCollection ??= new();
        
        public bool IsAlreadyInitialized { get; private set; }
        public bool IsAuthenticated => gameManager is not null and { IsAuthenticated: true };
        public TimeSpan ServerOffset { get; private set; } = TimeSpan.Zero;
        public DateTime EstimatedServerTime => DateTime.UtcNow + ServerOffset;
        public DateTime NextDailyReset { get; private set; }
        public DateTime NextWeeklyReset { get; private set; }

        private MissionUI _missionUI;
        internal MissionUI MissionUI
        {
            get
            {
                if (_missionUI == null)
                {
                    _missionUI = FindFirstObjectByType<MissionUI>();
                    if (_missionUI == null)
                        Debug.LogWarning($"{nameof(MissionSystem.MissionUI)} not found in the scene");
                }

                return _missionUI;
            }
        }

        public bool WasUpdate { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();

            if (!authManager || !analyticsManager)
            {
                Debug.LogError("MissionManager: One or more required services are not available. Please ensure AuthManager and AnalyticsManager are initialized.");
                return;
            }

            _dailyMissionsDataCollection = new();
            _weeklyMissionsDataCollection = new();
        }

        private async void Start()
        {
            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } && IsAuthenticated);

            analyticsManager.Subscribe(UpdatePlayerMissionData);

            MissionUI?.Initialize
                (() => authManager.IsAlreadyInitialized && IsAuthenticated,
                ClaimReward,
                () => EstimatedServerTime, 
                () => (NextDailyReset, NextWeeklyReset));
            await ValidateMissions();
        }

        // Unsubscribe from analytics updates when the object is destroyed
        private void OnDestroy()
        {
            if (analyticsManager)
                analyticsManager.Unsubscribe(UpdatePlayerMissionData);
        }

        // Try to update progress when the application quits
        private void OnApplicationQuit()
        {
            UpdateProgress().Forget();
        }

        private async UniTask ValidateMissions()
        {
            IsAlreadyInitialized = false;

            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot validate missions. User is not authenticated.");
                return;
            }

            var missionEncriptedData = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.ValidateMissions().AsUniTask(),
                taskId: nameof(module.ValidateMissions),
                showLoading: false,
                shouldIgnoreTryAgainProcess: true);

            // Deserialize and decrypt the data
            var missionDataResponse = authManager.DeserializeAndDecryptData<MissionResponse>(missionEncriptedData);
            if (missionDataResponse == null)
            {
                Debug.LogWarning("Failed to deserialize Mission data response.");
                return;
            }

            var serverTime = DateTime.Parse(missionDataResponse.serverTime, null, DateTimeStyles.RoundtripKind);
            var localTimeAtSync = DateTime.UtcNow;

            ServerOffset = serverTime - localTimeAtSync;
            NextDailyReset = DateTime.Parse(missionDataResponse.nextDailyReset, null, DateTimeStyles.RoundtripKind);
            NextWeeklyReset = DateTime.Parse(missionDataResponse.nextWeeklyReset, null, DateTimeStyles.RoundtripKind);

            // Clear existing collections
            _dailyMissionsDataCollection?.Clear();
            _weeklyMissionsDataCollection?.Clear();

            // Initialize collections
            UpdateMissionDataCollection
                (ref _dailyMissionsDataCollection,
                missionDataResponse.dailyPlayerMissions,
                missionDataResponse.dailyGameMissions);

            UpdateMissionDataCollection
                (ref _weeklyMissionsDataCollection,
                missionDataResponse.weeklyPlayerMissions,
                missionDataResponse.weeklyGameMissions);

            IsAlreadyInitialized = true;

            // Concat daily and weekly mission data collections and configure the UI
            var allMissionsDataCollection = _dailyMissionsDataCollection
                ?.Concat(_weeklyMissionsDataCollection)
                ?.ToDictionary(x => x.Key, x => x.Value);

            // Configure the MissionUI with the combined mission data collection
            MissionUI.Configure(allMissionsDataCollection);

            // Start the validation loop
            ScheduleNextValidation().Forget();

            void UpdateMissionDataCollection
                (ref Dictionary<PlayerMissionData, GameMissionData> missionDataCollection,
                List<PlayerMissionData> playerMissionDatas, List<GameMissionData> gameMissionDatas)
            {
                if (playerMissionDatas is null || gameMissionDatas is null)
                {
                    Debug.LogWarning("Player or game mission data lists are null.");
                    return;
                }

                foreach (var playerMissionData in playerMissionDatas)
                {
                    if (playerMissionData is null or { id: null })
                        continue;

                    var gameMissionData = gameMissionDatas.FirstOrDefault(m => m.id == playerMissionData.id);
                    if (gameMissionData is null)
                    {
                        Debug.LogWarning($"No matching game mission found for player mission ID: {playerMissionData.id}");
                        continue;
                    }

                    missionDataCollection?.Add(playerMissionData, gameMissionData);
                }
            }
        }

        private async UniTask UpdateProgress()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot update mission progress. User is not authenticated.");
                return;
            }

            var playerMissions = DailyMissionsDataCollection?.Keys?.ToList() ?? new();
            if (WeeklyMissionsDataCollection is not null and { Count: > 0 })
                playerMissions.AddRange(WeeklyMissionsDataCollection?.Keys);

            if (playerMissions.Count == 0 || !WasUpdate)
            { 
                Debug.LogWarning("No player missions to update.");
                return;
            }

            var encryptedJsonData = authManager.SerializeAndEncryptData(new()
            {
                ["updatesFromFrontend"] = playerMissions.ToArray()
            });

            await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.UpdateMissionProgress(encryptedJsonData).AsUniTask(),
                taskId: nameof(module.UpdateMissionProgress),
                showLoading: false);

            // Reset the update flag after processing
            WasUpdate = false;
        }

        private async UniTask ScheduleNextValidation()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot schedule next validation. User is not authenticated.");
                return;
            }

            var estimatedServerTime = EstimatedServerTime;
            var nextResetTime = new[] { NextDailyReset, NextWeeklyReset }.Min();

            if (estimatedServerTime >= nextResetTime)
            {
                Debug.Log("Detected past reset time. Validating immediately.");
                await ValidateMissions();
                return;
            }

            var delay = nextResetTime - estimatedServerTime;
            Debug.Log($"Scheduling next mission validation in {delay.TotalMinutes:F1} minutes.");

            await UniTask.Delay(delay, DelayType.DeltaTime, PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            await ValidateMissions();
        }

        private async UniTask ClaimReward(string missionID)
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Cannot claim mission reward. User is not authenticated.");
                return;
            }

            if (string.IsNullOrEmpty(missionID))
            {
                Debug.LogWarning("Player mission ID is null or empty. Cannot claim reward.");
                return;
            }

            // Find the mission data in either daily or weekly collections
            var kvp = DailyMissionsDataCollection?.FirstOrDefault(x => x.Key.id == missionID) is KeyValuePair<PlayerMissionData, GameMissionData> kvpDaily and { Key: not null } 
                ? kvpDaily
                : WeeklyMissionsDataCollection?.FirstOrDefault(x => x.Key.id == missionID);

            // If no mission data found, log a warning and return
            var (playerMissionData, gameMissionData) = (kvp.Value.Key, kvp.Value.Value);
            if (playerMissionData is null || gameMissionData is null)
            {
                Debug.LogWarning($"No mission data found for ID: {missionID}");
                return;
            }

            // Check if the mission is already claimed
            if (playerMissionData.claimed)
            {
                Debug.LogWarning($"Mission {missionID} is already claimed.");
                return;
            }

            // Check if the mission is not completed
            if (playerMissionData.progress < gameMissionData.goalAmount)
            {
                Debug.LogWarning($"Mission {missionID} progress is not sufficient to claim reward. Current progress: {playerMissionData.progress}, Goal: {gameMissionData.goalAmount}");
                return;
            }

            // Try to update progress before claiming the reward
            await UpdateProgress();

            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["completedPlayerMissionID"] = missionID,
            });

            // Claim the mission reward
            var configEncryptedData = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.ClaimMissionReward(dataEncrypted).AsUniTask(),
                taskId: nameof(module.ClaimMissionReward),
                showLoading: true);

            // Deserialize and decrypt the data
            var claimRewardResponse = authManager.DeserializeAndDecryptData<MissionClaimRewardResponse>(configEncryptedData);
            if (claimRewardResponse != null && claimRewardResponse.isRetrievingDailyBonus && DailyMissionsDataCollection.All(x => x.Key.completed))
                MissionUI?.ShowBonusPopUp(claimRewardResponse.missionConfigData);

            // Once the reward is claimed, update the player mission data
            await ValidateMissions();

            // Try to update bonus progress after claiming the reward
            analyticsManager.SendAnalytic(AnalyticType.Bonus);

            // Send analytic for the token modification
            analyticsManager?.SendAnalytic(AnalyticType.OnTokenModified);
        }
    }
}
