using ProDomino.AchievementSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Leaderboard;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.AccountSystem
{
    /// <summary>
    /// Manages the display and interaction of the account data UI, including user information, achievements, and
    /// leaderboard entries, with support for pagination and dynamic updates based on game, authentication, and
    /// achievement data.
    /// </summary>
    public class AccountDataController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField]
        private TMP_Text
            usernameLabel,
            userIDLabel,
            completedAchievementsLabel,
            achievementPointsLabel,
            totalMatchPlayedLabel,
            totalGameTimeLabel,
            currentPageLabel;
        [SerializeField] private Image profileImage;
        [SerializeField] private Transform leaderboardAccountParent;
        [SerializeField] private LeaderboardAccountEntry leaderboardAccountEntryPrefab;
        [SerializeField] private Button leftArrow, rightArrow;
        [SerializeField] private Button backButton;

        private GameManager gameManager;
        private AuthManager authManager;
        private AchievementManager achievementManager;
        private List<LeaderboardAccountEntry> leaderboardAccountEntries;
        private int currentPageIndex;

        private const int ITEMS_PER_PAGE = 4;
        public int TotalPages { get; private set; }

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            achievementManager = ServiceLocator.Instance.GetService<AchievementManager>();
            leaderboardAccountEntries = new();

            leftArrow?.onClick.AddListener(() => ChangePage(false));
            rightArrow?.onClick.AddListener(() => ChangePage(true));
            backButton?.onClick.AddListener(() => SetVisibility(false));
        }

        private void Start()
        {
            leaderboardAccountEntries = leaderboardAccountParent?.GetComponentsInChildren<LeaderboardAccountEntry>(true)
                ?.ToList() 
                ?? new();
        }

        /// <summary>
        /// Shows or hides the account UI and updates its displayed data based on the current game, authentication, and
        /// achievement information.
        /// </summary>
        /// <param name="isVisible">If true, displays and updates the account UI; if false, hides it.</param>
        public void SetVisibility(bool isVisible)
        {
            if (!isVisible)
            { 
                rootCanvasGroup.SetActive(false);
                return;
            }

            if (!gameManager || !authManager || !achievementManager)
            {
                Debug.LogError($"Missing reference: {nameof(gameManager)} (or its analytic data), {nameof(authManager)}, {nameof(achievementManager)}");
                return;
            }

            if (profileImage)
                profileImage.sprite = gameManager.ProfilePicture;
            else
                Debug.LogError($"Missing reference: {nameof(profileImage)}");

            if (usernameLabel)
                usernameLabel.text = authManager.Username;
            else
                Debug.LogError($"Missing reference: {nameof(usernameLabel)}");

            if (userIDLabel)
                userIDLabel.text = authManager.UUID;
            else
                Debug.LogError($"Missing reference: {nameof(userIDLabel)}");
            
            if (completedAchievementsLabel)
                completedAchievementsLabel.text = (gameManager.AnalyticsData?.achievementsClaimedCount ?? 0).ToString();
            else
                Debug.LogError($"Missing reference: {nameof(completedAchievementsLabel)}");

            if (achievementPointsLabel)
                achievementPointsLabel.text = (achievementManager.PlayerAchievementsDataCollection?.Keys?.Sum(x => x.points) ?? 0).ToString();
            else
                Debug.LogError($"Missing reference: {nameof(achievementPointsLabel)}");

            if (totalMatchPlayedLabel)
                totalMatchPlayedLabel.text = (gameManager.AnalyticsData?.TotalMatches ?? 0).ToString();
            else
                Debug.LogError($"Missing reference: {nameof(totalMatchPlayedLabel)}");

            if (totalGameTimeLabel)
            {
                var timeSpan = TimeSpan.FromTicks(gameManager.AnalyticsData?.totalMatchTimeTicks ?? 0);
                totalGameTimeLabel.text = ToCompactString(timeSpan);
            }
            else
                Debug.LogError($"Missing reference: {nameof(totalMatchPlayedLabel)}");

            var gameModes = Enum.GetValues(typeof(GameMode)).Cast<GameMode>()
                ?.Where(x => x is not GameMode.none)
                ?.ToArray();

            // Try to instantiate the lefting entries
            if (gameModes is not null and { Length: > 0 })
            {
                var difference = gameModes.Length - leaderboardAccountEntries.Count;
                if (difference > 0)
                    for (int i = 0; i < difference; i++)
                    {
                        var newEntry = Instantiate(leaderboardAccountEntryPrefab, leaderboardAccountParent);
                        leaderboardAccountEntries.Add(newEntry);
                    }
            }

            // Group leaderboard records by GameMode, preserving the NumberOfPlayers and record data
            var recordsByGameMode = gameManager.AnalyticsData?.leaderboardRecords
                ?.Select(record => // Extract game data from leaderboard ID
                {
                    var data = LeaderboardManager.GetDataFromLeaderboardID(record.leaderboardId);
                    return (record, data);
                })
                ?.Where(x => x.data.HasValue) // Keep only valid tuples
                ?.GroupBy(x => x.data.Value.gameMode) // Group by game mode
                ?.ToDictionary( // Convert to dictionary: GameMode -> array of (record, numberOfPlayers)
                    g => g.Key,
                    g => g.Select(x => (x.record, x.data.Value.numberPlayers)).ToArray()
                );

            // Configure the account entries with the data obtained
            if (leaderboardAccountEntries is not null and { Count: > 0 })
            {
                for (int i = 0; i < leaderboardAccountEntries.Count; i++)
                {
                    var currentGameMode = gameModes.ElementAtOrDefault(i);
                    var accountEntry = leaderboardAccountEntries[i];
                    var isInvalidGameMode = currentGameMode is GameMode.none;

                    // Set active the instance according the validity of the game mode
                    accountEntry.gameObject.SetActive(!isInvalidGameMode);

                    // Ommit the entry configuration if the game mode is invalid
                    if (isInvalidGameMode)
                    {
                        accountEntry.WasConfiguredProperly = false;
                        continue;
                    }

                    // Try to get the records of the player to configure the entry
                    var gameModeRecordData = recordsByGameMode?.ElementAtOrDefault(i).Value;
                    var oneVsOneRecordData = gameModeRecordData?.FirstOrDefault(x => x.numberPlayers is Shared.NumberPlayers.oneVsOne);
                    var oneVsThreeRecordData = gameModeRecordData?.FirstOrDefault(x => x.numberPlayers is Shared.NumberPlayers.oneVsThree);

                    accountEntry.Configure(currentGameMode,
                        oneVsOneRecordData?.record?.higherScore, oneVsThreeRecordData?.record?.higherScore,
                        oneVsOneRecordData?.record?.higherTier, oneVsThreeRecordData?.record?.higherTier);
                }

                int totalItems = leaderboardAccountEntries?.Count(x => x.WasConfiguredProperly) ?? 0;
                TotalPages = Mathf.CeilToInt(totalItems / (float)ITEMS_PER_PAGE);
            }

            // Determine the visibility of the arrow button
            var shouldShowArrowButtons = leaderboardAccountEntries.Count(x => x.gameObject.activeSelf) > ITEMS_PER_PAGE;
            leftArrow?.gameObject.SetActive(shouldShowArrowButtons);
            rightArrow?.gameObject.SetActive(shouldShowArrowButtons);

            // Show the elements correpondly to the current page
            ShowOnlyCurrentPage();

            // Once everything is configure, turn on the visibility
            rootCanvasGroup.SetActive(true);


            string ToCompactString(TimeSpan ts)
            {
                var parts = new List<string>();

                if (ts.Days > 0) parts.Add($"{ts.Days}d");
                if (ts.Hours > 0) parts.Add($"{ts.Hours}h");
                if (ts.Minutes > 0) parts.Add($"{ts.Minutes}m");
                if (ts.Seconds > 0 || parts.Count == 0) parts.Add($"{ts.Seconds}s");

                return string.Join(" ", parts);
            }
        }

        /// <summary>
        /// Navigates to the next or previous page and updates the displayed content accordingly.
        /// </summary>
        /// <param name="isNext">True to move to the next page; false to move to the previous page.</param>
        private void ChangePage(bool isNext)
        {
            currentPageIndex += isNext ? 1 : -1;

            if (currentPageIndex < 0)
                currentPageIndex = TotalPages - 1;
            else if (currentPageIndex >= TotalPages)
                currentPageIndex = 0;

            ShowOnlyCurrentPage();
        }

        /// <summary>
        /// Displays only the leaderboard entries for the current page and updates the page label accordingly.
        /// </summary>
        private void ShowOnlyCurrentPage()
        {
            // Configure the account entries with the data obtained
            if (leaderboardAccountEntries is not null and { Count: > 0 })
            {
                // Get the current page elements
                var pagedData = leaderboardAccountEntries
                    ?.Skip(currentPageIndex * ITEMS_PER_PAGE)
                    ?.Take(ITEMS_PER_PAGE)
                    ?.Where(x => x.WasConfiguredProperly)
                    ?.ToList() 
                    ?? new();

                // Turn on the current configure page elements 
                foreach (var item in pagedData)
                    item.gameObject.SetActive(true);

                // Select the elements to hide (excepting the corresponding to the current page
                leaderboardAccountEntries
                    ?.Except(pagedData)
                    ?.ToList()
                    ?.ForEach(x => x.gameObject.SetActive(false));
            }

            if (currentPageLabel)
                currentPageLabel.text = $"{currentPageIndex + 1}/{TotalPages}";
        }
    }
}
