using HelperSharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.ConfigData;

namespace ProDomino.MissionSystem
{
    internal class MissionUI : MonoBehaviour
    {
        [SerializeField] private Button openMissionInterfaceButton;
        [SerializeField] private CanvasGroup missionUICanvasGroup;

        [Space(15), Header("Bonus Pop Up")]
        [SerializeField] private bool isShowinBonusPopUp = false;
        [SerializeField] private CanvasGroupVisibilityController dailyMissionBonusPopUp;
        [SerializeField] private TMP_Text dailyMissionBonusPopUpHeader;
        [SerializeField] private TMP_Text dailyMissionBonusPopUpDescription;

        [Space(15), Header("Instantiate elements")]
        [SerializeField] private MissionElement missionElementPrefab;
        [SerializeField] private Transform dailyMissionElementParent;
        [SerializeField] private Transform weeklyMissionElementParent;
        [SerializeField] private Transform bonusMissionElementParent;

        private List<MissionElement> dailyMissionInstances;
        private List<MissionElement> weeklyMissionInstances;
        private List<MissionElement> bonusMissionInstances;

        private Func<bool> checkIfIsAuthenticated;
        private AsyncActionHandler<string> claimReward;
        private Func<DateTime> getEstimateServerTime;
        private Func<(DateTime daily, DateTime weekly)> getMissionsResetTime;

        internal bool IsAuthenticated => checkIfIsAuthenticated?.Invoke() ?? false;
        internal DateTime EstimatedServerTime => getEstimateServerTime?.Invoke() ?? DateTime.UtcNow;
        internal DateTime NextDailyReset => getMissionsResetTime?.Invoke().daily ?? DateTime.MinValue;
        internal DateTime NextWeeklyReset => getMissionsResetTime?.Invoke().weekly ?? DateTime.MinValue;

        private void Awake()
        {
            dailyMissionInstances = dailyMissionElementParent?.GetComponentsInChildren<MissionElement>(true)?.ToList() ?? new();
            weeklyMissionInstances = weeklyMissionElementParent?.GetComponentsInChildren<MissionElement>(true)?.ToList() ?? new();
            bonusMissionInstances = bonusMissionElementParent?.GetComponentsInChildren<MissionElement>(true)?.ToList() ?? new();

            openMissionInterfaceButton?.onClick.AddListener(OnPressMissionButton);
        }

        private void Start()
        {
            SetActive(false);
        }

        private void Update()
        {
            // Ensure the button is only interactable if the user is authenticated
            if (openMissionInterfaceButton != null)
                openMissionInterfaceButton.interactable = IsAuthenticated;
        }

        internal void Initialize(Func<bool> checkIfIsAuthenticated, AsyncActionHandler<string> claimReward, Func<DateTime> getEstimateServerTime, Func<(DateTime daily, DateTime weekly)> getMissionsResetTime)
        {
            this.checkIfIsAuthenticated = checkIfIsAuthenticated;
            this.claimReward = claimReward;
            this.getEstimateServerTime = getEstimateServerTime;
            this.getMissionsResetTime = getMissionsResetTime;

            // If there are instances already, we need to initialize them
            InitializeInstances(dailyMissionInstances);
            InitializeInstances(weeklyMissionInstances);
            InitializeInstances(bonusMissionInstances);

            // Set the initial state of the mission UI
            void InitializeInstances(List<MissionElement> missionElements)
            {
                if (missionElements is not null and { Count: > 0 })
                    foreach (var instance in missionElements)
                        instance.Initialize(checkIfIsAuthenticated, this.claimReward, this.getEstimateServerTime, this.getMissionsResetTime);
            }
        }

        internal void Configure(Dictionary<PlayerMissionData, GameMissionData> missionDataCollection)
        {
            if (missionDataCollection is null or { Count: 0 })
            { 
                Debug.LogWarning("Mission data collection is null or empty.");
                return;
            }

            var dailyMissionCount = missionDataCollection.Count(m => m.Key.isDaily && !m.Key.isBonus);
            var weeklyMissionCount = missionDataCollection.Count(m => !m.Key.isDaily && !m.Key.isBonus);
            var bonusMissionCount = missionDataCollection.Count(m => m.Key.isBonus);

            for (var i = 0; i < dailyMissionCount; i++)
                if (i >= dailyMissionInstances.Count)
                {
                    var newInstance = Instantiate(missionElementPrefab, dailyMissionElementParent);
                    newInstance.Initialize(checkIfIsAuthenticated, claimReward, getEstimateServerTime, getMissionsResetTime);
                    dailyMissionInstances.Add(newInstance);
                }
            
            for (var i = 0; i < weeklyMissionCount; i++)
                if (i >= weeklyMissionInstances.Count)
                {
                    var newInstance = Instantiate(missionElementPrefab, weeklyMissionElementParent);
                    newInstance.Initialize(checkIfIsAuthenticated, claimReward, getEstimateServerTime, getMissionsResetTime);
                    weeklyMissionInstances.Add(newInstance);
                }

            for (var i = 0; i < bonusMissionCount; i++)
                if (i >= bonusMissionInstances.Count)
                {
                    var newInstance = Instantiate(missionElementPrefab, bonusMissionElementParent);
                    newInstance.Initialize(checkIfIsAuthenticated, claimReward, getEstimateServerTime, getMissionsResetTime);
                    bonusMissionInstances.Add(newInstance);
                }

            // Deactivate unused daily mission instances
            dailyMissionInstances.ForEach(instance => instance.gameObject.SetActive(false));
            weeklyMissionInstances.ForEach(instance => instance.gameObject.SetActive(false));
            bonusMissionInstances.ForEach(instance => instance.gameObject.SetActive(false));

            // Iterate through the weekly missions and ensure we have enough instances
            foreach (var (playerMissionData, gameMissionData) in missionDataCollection)
            {
                var instance = (playerMissionData.isBonus 
                    ? bonusMissionInstances 
                    : playerMissionData.isDaily 
                        ? dailyMissionInstances 
                        : weeklyMissionInstances)
                    ?.FirstOrDefault(i => !i.gameObject.activeSelf);

                if (instance != null)
                {
                    // If the mission is bonus, set the goal amount to the daily mission count
                    if (gameMissionData.analyticType is Enums.AnalyticType.Bonus)
                    {
                        playerMissionData.progress = (uint)(missionDataCollection?.Count(x => x.Key is { isBonus : false, isDaily: true, claimed: true }) ?? 0);
                        gameMissionData.goalAmount = dailyMissionCount;
                    }

                    instance.Configure(playerMissionData, gameMissionData);
                    instance.gameObject.SetActive(true);
                }
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        internal void ShowBonusPopUp(MissionConfigData missionConfigData)
        {
            if (!isShowinBonusPopUp)
                return;

            if (dailyMissionBonusPopUp)
                dailyMissionBonusPopUp.ShowCanvasGroup();

            if (dailyMissionBonusPopUpHeader)
                dailyMissionBonusPopUpHeader.text = dailyMissionBonusPopUpHeader.text.Replace("{{dailyMissionsCount}}", missionConfigData.dailyMissionsCount.ToString());

            if (dailyMissionBonusPopUpDescription)
                dailyMissionBonusPopUpDescription.text = dailyMissionBonusPopUpDescription.text
                    ?.Replace("{{dailyMissionBonusAmout}}", missionConfigData.dailyMissionBonusAmout.ToString())
                    ?.Replace("{{dailyMissionBonusCurrency}}", missionConfigData.dailyMissionBonusCurrency.ToString());
        }

        internal void SetActive(bool isActive)
        {
            if (missionUICanvasGroup == null)
            {
                Debug.LogWarning("Mission UI CanvasGroup is not assigned.");
                return;
            }

            if (!IsAuthenticated)
            {
                Debug.Log("User is not authenticated. Cannot set mission UI active.");
                return;
            }

            missionUICanvasGroup.SetActive(isActive);
            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        private void OnPressMissionButton()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("User is not authenticated. Cannot open mission UI.");
                return;
            }

            SetActive(true);
        }
    }
}
