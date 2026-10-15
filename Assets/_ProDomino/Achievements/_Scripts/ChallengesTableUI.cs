using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.MissionSystem;
using Timba.Patterns;
using Timba.Utils;
using UnityEngine;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Fills the Challenges tab table of Achievements_Screen with the signed in player's daily, weekly and bonus
    /// missions from <see cref="MissionManager"/>. Each row is an <see cref="AchievementElement"/> in challenge mode,
    /// so completed missions show the claim button and claiming collects the mission reward.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChallengesTableUI : MonoBehaviour
    {
        [Tooltip("Row cloned when there are more missions than rows. Defaults to the first row under this object.")]
        [SerializeField] private AchievementElement rowTemplate;

        [Header("Copy")]
        [SerializeField] private string dailyCategory = "Daily Challenge";
        [SerializeField] private string weeklyCategory = "Weekly Challenge";
        [SerializeField] private string bonusCategory = "Bonus Challenge";

        [Tooltip("Seconds between checks of the mission collections. Missions are rebuilt after every validation or claim.")]
        [SerializeField] private float refreshInterval = 0.5f;

        private readonly List<AchievementElement> rows = new();
        private readonly List<(PlayerMissionData player, GameMissionData game, string category)> missions = new();
        private readonly List<PlayerMissionData> boundMissions = new();
        private MissionManager missionManager;
        private float refreshTimer;

        private void Awake()
        {
            rows.AddRange(GetComponentsInChildren<AchievementElement>(true));

            if (!rowTemplate && rows.Count > 0)
                rowTemplate = rows[0];
        }

        private void OnEnable()
        {
            refreshTimer = 0f;
            Refresh();
        }

        private void Update()
        {
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < refreshInterval)
                return;

            refreshTimer = 0f;
            Refresh();
        }

        /// <summary>
        /// Rebinds the rows when the mission collections were replaced. Progress changes inside a mission are picked
        /// up by each row on its own.
        /// </summary>
        private void Refresh()
        {
            CollectMissions();

            if (IsBoundTo(missions))
                return;

            Bind();
        }

        private void CollectMissions()
        {
            missions.Clear();

            if (!missionManager)
                missionManager = ServiceLocator.Instance.GetService<MissionManager>();

            if (missionManager is not { IsAuthenticated: true, IsAlreadyInitialized: true })
                return;

            AddMissions(missionManager.DailyMissionsDataCollection, isBonus: false, dailyCategory);
            AddMissions(missionManager.WeeklyMissionsDataCollection, isBonus: false, weeklyCategory);
            AddMissions(missionManager.DailyMissionsDataCollection, isBonus: true, bonusCategory);
            AddMissions(missionManager.WeeklyMissionsDataCollection, isBonus: true, bonusCategory);
        }

        private void AddMissions(Dictionary<PlayerMissionData, GameMissionData> collection, bool isBonus, string category)
        {
            if (collection is null)
                return;

            foreach (var (player, game) in collection)
                if (player is not null && game is not null && player.isBonus == isBonus)
                    missions.Add((player, game, category));
        }

        // Reference check: ValidateMissions creates new mission objects, so a new reference means new data to bind
        private bool IsBoundTo(List<(PlayerMissionData player, GameMissionData game, string category)> current)
        {
            if (current.Count != boundMissions.Count)
                return false;

            for (var i = 0; i < current.Count; i++)
                if (!ReferenceEquals(current[i].player, boundMissions[i]))
                    return false;

            return true;
        }

        private void Bind()
        {
            while (rows.Count < missions.Count && rowTemplate)
                rows.Add(Instantiate(rowTemplate, transform));

            boundMissions.Clear();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var hasMission = i < missions.Count;

                if (hasMission)
                {
                    var (player, game, category) = missions[i];
                    row.ConfigureChallenge(player, game, category, () => missionManager is { IsAuthenticated: true }, ClaimMission);
                    boundMissions.Add(player);
                }

                if (row.gameObject.activeSelf != hasMission)
                    row.gameObject.SetActive(hasMission);
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        private UniTask ClaimMission(string missionID)
            => missionManager ? missionManager.ClaimMissionReward(missionID) : UniTask.CompletedTask;
    }
}
