using Cysharp.Threading.Tasks;
using ProDomino.AchievementSystem;
using ProDomino.Authentication;
using ProDomino.CustomizationSystem;
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
        [SerializeField] private Button editProfileButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button verifyEmailButton;
        [SerializeField] private CustomizationController customizationController;

        [Header("Game Mode Tabs")]
        [SerializeField] private GameModeTab[] gameModeTabs;
        [SerializeField] private TMP_FontAsset selectedTabFont, unselectedTabFont;
        [SerializeField] private Color selectedTabLabelColor = Color.white;
        [SerializeField] private Color unselectedTabLabelColor = new(0.6901961f, 0.6901961f, 0.7058824f, 1f);
        [SerializeField] private TMP_Text eloRatingLabel, winsLabel, placementLabel;

        /// <summary>
        /// A tab of the profile tab bar and the game mode whose stats it shows.
        /// </summary>
        [Serializable]
        private class GameModeTab
        {
            public GameMode gameMode;
            public Button button;
            public Image background;
            public TMP_Text label;
        }

        private GameManager gameManager;
        private AuthManager authManager;
        private AchievementManager achievementManager;
        private LeaderboardManager leaderboardManager;
        private GameMode selectedGameMode = GameMode.block;
        private PromptFadeController promptFadeController;
        private List<LeaderboardAccountEntry> leaderboardAccountEntries;
        private int currentPageIndex;

        private const int ITEMS_PER_PAGE = 4;
        public int TotalPages { get; private set; }

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            achievementManager = ServiceLocator.Instance.GetService<AchievementManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
            leaderboardManager = ServiceLocator.Instance.GetService<LeaderboardManager>();
            leaderboardAccountEntries = new();

            if (gameModeTabs is not null)
                foreach (var tab in gameModeTabs)
                {
                    var gameMode = tab.gameMode;
                    tab.button?.onClick.AddListener(() => SelectGameMode(gameMode));
                }

            leftArrow?.onClick.AddListener(() => ChangePage(false));
            rightArrow?.onClick.AddListener(() => ChangePage(true));
            backButton?.onClick.AddListener(() => SetVisibility(false));
            editProfileButton?.onClick.AddListener(OpenCustomization);
            logoutButton?.onClick.AddListener(() => Logout().Forget());
            verifyEmailButton?.onClick.AddListener(() => SendEmailVerification().Forget());

            authManager?.HandleOnCheckEmailVerification(OnCheckEmailVerification);
        }

        private void OnDestroy()
        {
            authManager?.UnHandleOnCheckEmailVerification(OnCheckEmailVerification);
        }

        private void OnCheckEmailVerification(bool isVerified) => RefreshVerifyEmailButton();

        /// <summary>
        /// Shows the verify email button only for credential (email/password) accounts whose email is not verified
        /// yet, the same rule the legacy options dropdown used.
        /// </summary>
        private void RefreshVerifyEmailButton()
        {
            if (!verifyEmailButton)
                return;

            bool shouldShow = authManager != null
                && authManager.IsUserAuthenticatedWithCredentials
                && !authManager.IsEmailVerified;

            verifyEmailButton.interactable = shouldShow;
            verifyEmailButton.gameObject.SetActive(shouldShow);
        }

        /// <summary>
        /// Sends the verification email and reports the result through the prompt fade, mirroring the legacy options
        /// dropdown flow.
        /// </summary>
        private async UniTaskVoid SendEmailVerification()
        {
            if (!authManager)
            {
                Debug.LogError($"Missing reference: {nameof(authManager)}");
                return;
            }

            if (authManager.IsEmailVerified || authManager.IsUserAuthenticatedWithProvider)
            {
                Debug.LogWarning("The email is already verified or the user is authenticated with a provider.");
                RefreshVerifyEmailButton();
                return;
            }

            verifyEmailButton.interactable = false;

            var promptMessage = "";
            try
            {
                var emailWhoSentVerification = await authManager.SendEmailVerification();

                promptMessage = !string.IsNullOrEmpty(emailWhoSentVerification)
                    ? $"Email verification sent to <b>{emailWhoSentVerification}</b>"
                    : "Failed to send email verification. Try again later.";
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to send email verification: " + ex.Message);
                promptMessage = "An error occurred while sending email verification. Please try again later.";
            }
            finally
            {
                promptFadeController?.Fade(promptMessage);
                RefreshVerifyEmailButton();
            }
        }

        /// <summary>
        /// Hides the profile and signs the player out. AuthManager falls back to an anonymous session afterwards, the
        /// same flow the options menu sign out uses.
        /// </summary>
        private async UniTaskVoid Logout()
        {
            if (!authManager)
            {
                Debug.LogError($"Missing reference: {nameof(authManager)}");
                return;
            }

            if (logoutButton)
                logoutButton.interactable = false;

            SetVisibility(false);

            try
            {
                await authManager.SignOut();
            }
            finally
            {
                if (logoutButton)
                    logoutButton.interactable = true;
            }
        }

        /// <summary>
        /// Hides the profile and opens the customization pop-up. The controller lives outside this prefab, so it is
        /// resolved from the scene when not assigned.
        /// </summary>
        private void OpenCustomization()
        {
            if (!customizationController)
                customizationController = FindAnyObjectByType<CustomizationController>(FindObjectsInactive.Include);

            if (!customizationController)
            {
                Debug.LogError($"Missing reference: {nameof(customizationController)}");
                return;
            }

            SetVisibility(false);
            customizationController.SetVisibility(true);
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

            RefreshVerifyEmailButton();

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
                Debug.LogError($"Missing reference: {nameof(totalGameTimeLabel)}");

            var gameModes = Enum.GetValues(typeof(GameMode)).Cast<GameMode>()
                ?.Where(x => x is not GameMode.none and not GameMode.replay)
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
                    // Look up by game mode: the dictionary order follows the records, not the enum
                    var gameModeRecordData = recordsByGameMode is not null && recordsByGameMode.TryGetValue(currentGameMode, out var records)
                        ? records
                        : null;
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

            // Refresh the tab bar and the stats of the selected game mode
            SelectGameMode(selectedGameMode);

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
        /// Highlights the tab of the given game mode and shows its stats.
        /// </summary>
        /// <param name="gameMode">The game mode to show.</param>
        private void SelectGameMode(GameMode gameMode)
        {
            selectedGameMode = gameMode;

            if (gameModeTabs is not null)
                foreach (var tab in gameModeTabs)
                {
                    var isSelected = tab.gameMode == gameMode;

                    if (tab.background)
                        tab.background.color = isSelected ? Color.white : Color.clear;

                    if (tab.label)
                    {
                        var font = isSelected ? selectedTabFont : unselectedTabFont;
                        if (font)
                            tab.label.font = font;
                        tab.label.color = isSelected ? selectedTabLabelColor : unselectedTabLabelColor;
                    }
                }

            // Elo is tracked per game mode by its leaderboards
            if (eloRatingLabel)
            {
                var bestScore = leaderboardManager?.GetCurrentClientBestScore(gameMode);
                eloRatingLabel.text = bestScore.HasValue ? Mathf.RoundToInt((float)bestScore.Value).ToString() : "-";
            }

            // Wins and placements are only tracked across all game modes by the backend analytics
            var analyticsData = gameManager?.AnalyticsData;

            if (winsLabel)
                winsLabel.text = (analyticsData?.competitiveVictoriesCount ?? 0).ToString();

            if (placementLabel)
                placementLabel.text = $"{analyticsData?.competitive2ndPositionCount ?? 0} / {analyticsData?.competitive3rdPositionCount ?? 0} / {analyticsData?.competitive4thPositionCount ?? 0}";
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
