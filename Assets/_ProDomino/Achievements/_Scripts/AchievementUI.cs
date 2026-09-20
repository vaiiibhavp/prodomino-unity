using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AchievementSystem
{
    internal class AchievementUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
        [SerializeField] private Color hightlightColor = Color.yellow;
        [SerializeField] private TMP_InputField searchAchievementInputfield;
        [SerializeField] private CustomButtonToggleGroupUI filtersToggleGroupUI;
        [SerializeField] private CustomButtonUI[] gameModeFilters;

        [Space(15), Header("Own Player elements")]
        [SerializeField] private TMP_Text totalAchievementsLabel;
        [SerializeField] private TMP_Text totalAchievementsPointsLabel;
        [SerializeField] private TMP_Text categoryAchievementCompletedLabel;

        [Space(15), Header("Instantiate elements")]
        [SerializeField] private AchievementElement achievementElementPrefab;
        [SerializeField] private Transform achievementElementParent;

        private List<AchievementElement> achievementInstances;
        private List<AchievementElement> currentFilteredList;
        private AchiemeventFilters currentFilter = AchiemeventFilters.All;

        private Func<bool> checkIfIsAuthenticated;
        private Func<(Achievement achievement, AchievementRank rank), Sprite> getAchievementIcon;
        private Func<string, Sprite> getRewardIcon;
        private Func<Dictionary<GameAchievementData, PlayerAchievementData>> getAchievementDataCollection;
        private AsyncActionHandler<Achievement> claimReward;
        private string _highlightHex;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Achievements;
        public bool RequiresAuthentication => true;
        internal bool IsAuthenticated => checkIfIsAuthenticated?.Invoke() ?? false;
        internal string HighlightHex => _highlightHex ??= ColorUtility.ToHtmlStringRGBA(hightlightColor);
        internal GameModeFilter CurrentGameModeFilter { get; private set; } = GameModeFilter.All;
        internal Dictionary<GameAchievementData, PlayerAchievementData> AchievementDataCollection => getAchievementDataCollection?.Invoke();

        /// <summary>
        /// Initializes the achievement UI by retrieving achievement elements, setting up filter selection callbacks,
        /// and registering the search input listener.
        /// </summary>
        internal void Awake_AchievementUI()
        {
            achievementInstances = achievementElementParent?.GetComponentsInChildren<AchievementElement>(true)?.ToList() ?? new();

            if (filtersToggleGroupUI)
                filtersToggleGroupUI.SetOnCustomButtonSelectedCallback(OnFilterSelected);

            if (searchAchievementInputfield)
                searchAchievementInputfield.onValueChanged.AddListener(OnSearchInputChanged);
        }

        /// <summary>
        /// Initializes the achievement UI by setting up game mode filter buttons, activating the default filter, and
        /// selecting the first filter button.
        /// </summary>
        internal void Start_AchievementUI()
        {
            // Register the rarity filters buttons to set the rarity filter when clicked
            if (gameModeFilters is not null and { Length: > 0 })
                Array.ForEach(gameModeFilters, x =>
                {
                    if (x != null && x.Button)
                        x.Button.onClick.AddListener(() => OnGameModeSelected(x.CustomButtonID));
                });
            else
                Debug.LogWarning("Rarity filters are not assigned or empty in the ShopUI");

            // By default, activate the "All" rarity filter and disable others
            ActivateAllGameModesOnly();

            filtersToggleGroupUI?.GetFirstButtonUI()?.Select();
        }

        /// <summary>
        /// Initializes the achievement system with authentication, icon retrieval, data collection, and reward claiming
        /// handlers.
        /// </summary>
        /// <param name="checkIfIsAuthenticated">Function to determine if the user is authenticated.</param>
        /// <param name="getAchievementIcon">Function to retrieve the icon for a specific achievement and rank.</param>
        /// <param name="getRewardIcon">Function to retrieve the icon for a specific reward.</param>
        /// <param name="getAchievementDataCollection">Function to obtain the collection of achievement data.</param>
        /// <param name="claimReward">Handler for processing achievement reward claims.</param>
        internal void Initialize
            (Func<bool> checkIfIsAuthenticated,
            Func<(Achievement achievement, AchievementRank rank), Sprite> getAchievementIcon,
            Func<string, Sprite> getRewardIcon,
            Func<Dictionary<GameAchievementData, PlayerAchievementData>> getAchievementDataCollection,
            AsyncActionHandler<Achievement> claimReward)
        {
            this.checkIfIsAuthenticated = checkIfIsAuthenticated;
            this.getAchievementIcon = getAchievementIcon;
            this.getRewardIcon = getRewardIcon;
            this.getAchievementDataCollection = getAchievementDataCollection;
            this.claimReward = claimReward;

            // Set the initial state of the achievement UI
            if (achievementInstances is not null and { Count: > 0 })
                foreach (var instance in achievementInstances)
                    instance.Initialize
                        (checkIfIsAuthenticated, 
                        this.getAchievementIcon,
                        this.getRewardIcon, 
                        () => HighlightHex, this.claimReward);
        }

        /// <summary>
        /// Updates the achievement UI elements by instantiating, configuring, and displaying achievement instances
        /// based on the current achievement data collection.
        /// </summary>
        internal void RefreshElements()
        {
            var achievementDataCollection = AchievementDataCollection;
            if (achievementDataCollection is null or { Count: 0 })
            {
                Debug.LogWarning("Achievement data collection is null or empty.");
                return;
            }

            for (var i = 0; i < achievementDataCollection.Count; i++)
                if (i >= achievementInstances.Count)
                {
                    var newInstance = Instantiate(achievementElementPrefab, achievementElementParent);
                    newInstance.Initialize
                        (checkIfIsAuthenticated,
                        getAchievementIcon,
                        getRewardIcon,
                        () => HighlightHex, claimReward);
                    achievementInstances.Add(newInstance);
                }

            // Deactivate unused daily achievement instances
            achievementInstances.ForEach(instance => instance.gameObject.SetActive(false));

            // Iterate through the weekly achievements and ensure we have enough instances
            foreach (var (gameAchievementData, playerAchievementData) in achievementDataCollection)
            {
                var instance = achievementInstances?.FirstOrDefault(i => !i.gameObject.activeSelf);
                if (instance != null)
                {
                    instance.Configure(gameAchievementData, playerAchievementData);
                    instance.HighlightNameMatches(""); // clear highlights
                    instance.gameObject.SetActive(true);
                }
            }

            // Apply the initial filter and search
            ApplyFilterAndSearch();

            // Clear the search input field if it exists
            if (searchAchievementInputfield)
                searchAchievementInputfield.text = string.Empty; // Clear the search input field

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Updates UI elements to display achievement completion counts, category-specific completion, and total
        /// achievement points based on the current achievement data and game mode filter.
        /// </summary>
        internal void ConfigureUI()
        {
            var achievementDataCollection = AchievementDataCollection;
            if (achievementDataCollection is null or { Count: 0 })
            {
                Debug.LogWarning("Achievement data collection is null or empty.");
                return;
            }

            var completedAchievements = achievementDataCollection.Values
                ?.Where(x => x is not null and { completed: true })
                ?.ToList();

            // Indicates the total quantity of achievements completed and the total quantity of achievements available
            if (totalAchievementsLabel)
                totalAchievementsLabel.text = $"{completedAchievements?.Count ?? 0}/{achievementDataCollection.Keys?.Count ?? 0}";

            // Indicates the total quantity of achievements completed by game mode
            if (categoryAchievementCompletedLabel)
            { 
                var gameModeAchievementDataCollection = achievementDataCollection
                    .Where(x => CurrentGameModeFilter is GameModeFilter.All or GameModeFilter.None 
                        || (x.Key.gameModeFilter.HasValue && CurrentGameModeFilter.HasFlag(x.Key.gameModeFilter.Value)))
                    .ToDictionary(x => x.Key, x => x.Value);

                var completedGameModeAchievements = gameModeAchievementDataCollection.Values
                    ?.Where(x => x is not null and { completed: true })
                    ?.ToList();

                var completionPercentage = gameModeAchievementDataCollection.Count > 0
                    ? (completedGameModeAchievements?.Count ?? 0) * 100 / gameModeAchievementDataCollection.Count
                    : 0;
                categoryAchievementCompletedLabel.text = $"Category completion {completedGameModeAchievements?.Count ?? 0}/{gameModeAchievementDataCollection.Keys?.Count ?? 0}({completionPercentage}%)";
            }

            // Indicates the total quantity of points obtained by the player in all achievements
            if (totalAchievementsPointsLabel)
            { 
                var obtainedGameAchievements = achievementDataCollection.Keys?
                    .Where(x => completedAchievements?.Any(y => y.achievement == x.achievement) ?? false)
                    .ToList();
                totalAchievementsPointsLabel.text = $"{obtainedGameAchievements?.Sum(x => x.points) ?? 0}";
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Filters, sorts, and searches the list of achievement instances based on the current game mode, filter
        /// selection, and search input, updating their visibility and UI order accordingly.
        /// </summary>
        private void ApplyFilterAndSearch()
        {
            // Step 0: Apply GameModeFilter visibility
            foreach (var element in achievementInstances)
            {
                if (element?.GameAchievementData is not GameAchievementData data)
                    continue;

                // Determine filter by game mode
                var isVisible = CurrentGameModeFilter.HasFlag(data.gameModeFilter ?? GameModeFilter.All)
                    || CurrentGameModeFilter == GameModeFilter.All
                    || CurrentGameModeFilter == GameModeFilter.None;

                element.gameObject.SetActive(isVisible);
            }

            // Step 1: Apply achievement filter (ordering logic)
            currentFilteredList = currentFilter switch
            {
                AchiemeventFilters.All => achievementInstances?
                    .Select((achievement, index) => new { achievement, index })
                    .OrderBy(x => ((int?)x.achievement?.GameAchievementData?.achievementRank) ?? x.index)
                    .ThenBy(x => x.achievement?.GameAchievementData?.leaderboardTier)
                    .ThenBy(x => x.achievement?.GameAchievementData?.achievementType)
                    .ThenBy(x => x.achievement?.GameAchievementData?.goalAmount)
                    .Select(x => x.achievement)
                    .ToList(),

                AchiemeventFilters.Alphabetical => achievementInstances?
                    .OrderBy(x => x.GameAchievementData.name)
                    .ToList(),

                AchiemeventFilters.MostRecent => achievementInstances?
                    .OrderByDescending(x => x.PlayerAchievementData?.completedTime)
                    .ToList(),

                AchiemeventFilters.CompletionStatus => achievementInstances?
                    .OrderByDescending(x => x.PlayerAchievementData?.completed)
                    .ToList(),

                _ => achievementInstances?.ToList()
            };

            // Step 2: Apply search and relevance ordering
            var input = searchAchievementInputfield.text;
            var finalList = string.IsNullOrWhiteSpace(input)
                ? currentFilteredList
                : SearchAchievementsByRelevance(input, currentFilteredList);

            // Step 3: Apply ordering and highlight matches
            if (finalList is not null and { Count: > 0 })
                for (int i = 0; i < finalList.Count; i++)
                {
                    finalList[i].transform.SetSiblingIndex(i);
                    finalList[i].HighlightNameMatches(searchAchievementInputfield.text);
                }

            // Step 4: Deactivate elements with very low relevance scores
            if (!string.IsNullOrWhiteSpace(input))
            {
                int minMatchScoreThreshold = 2; // Minimum score required to keep visible. Adjust as needed.

                foreach (var element in achievementInstances)
                {
                    var nameTokens = AchievementManager.Tokenize(element.GameAchievementData.name);
                    var searchTokens = AchievementManager.Tokenize(input);
                    var score = CalculateRelevance(nameTokens, searchTokens);

                    // Deactivate if relevance is too low
                    bool shouldBeVisible = score >= minMatchScoreThreshold;

                    if (!shouldBeVisible)
                    {
                        element.gameObject.SetActive(false);
                        element.HighlightNameMatches(""); // Clear highlight if hidden
                    }
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Searches and sorts achievements by their relevance to the input string, placing unmatched achievements at
        /// the end.
        /// </summary>
        /// <param name="input">The search string used to determine relevance.</param>
        /// <param name="baseList">The list of achievements to search and sort.</param>
        /// <returns>A list of achievements ordered by relevance to the input string, with unmatched achievements at the bottom.</returns>
        private List<AchievementElement> SearchAchievementsByRelevance(string input, List<AchievementElement> baseList)
        {
            if (string.IsNullOrWhiteSpace(input))
                return baseList;

            var searchTokens = AchievementManager.Tokenize(input);

            var results = baseList
                .Where(a => a.gameObject.activeSelf)
                .Select(a => new
                {
                    element = a,
                    score = CalculateRelevance(AchievementManager.Tokenize(a.name), searchTokens)
                })
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Select(x => x.element)
                .ToList();

            // Keep unmatched items at the bottom
            var unmatched = baseList.Except(results).ToList();
            results.AddRange(unmatched);

            return results;
        }

        /// <summary>
        /// Calculates a relevance score based on prefix and partial matches between target and search tokens.
        /// </summary>
        /// <param name="targetTokens">The list of tokens to be searched.</param>
        /// <param name="searchTokens">The list of tokens to search for within the target tokens.</param>
        /// <returns>An integer representing the calculated relevance score.</returns>
        private int CalculateRelevance(List<string> targetTokens, List<string> searchTokens)
        {
            int score = 0;

            foreach (var search in searchTokens)
            {
                foreach (var token in targetTokens)
                {
                    if (token.StartsWith(search)) score += 3; // prefix match has more weight
                    else if (token.Contains(search)) score += 1; // partial match
                }
            }

            return score;
        }

        /// <summary>
        /// Activates only the 'All Game Modes' filter by deactivating all game modes and enabling the corresponding
        /// button.
        /// </summary>
        private void ActivateAllGameModesOnly()
        {
            DeactivateAllGameModes();
            SetGameModeButtonState(GameModeFilter.None.ToString(), true);
        }

        /// <summary>
        /// Deactivates all game modes and resets the current game mode filter.
        /// </summary>
        private void DeactivateAllGameModes()
        {
            CurrentGameModeFilter = GameModeFilter.None;

            foreach (GameModeFilter mode in Enum.GetValues(typeof(GameModeFilter)))
                if (mode != GameModeFilter.None)
                    SetGameModeButtonState(mode.ToString(), false);
        }

        /// <summary>
        /// Enables the specified game mode filter and updates the corresponding button state.
        /// </summary>
        /// <param name="mode">The game mode filter to enable.</param>
        private void AddGameModeFlag(GameModeFilter mode)
        {
            CurrentGameModeFilter |= mode;
            SetGameModeButtonState(mode.ToString(), true);
        }

        /// <summary>
        /// Removes the specified game mode flag from the current filter and updates the corresponding button state.
        /// </summary>
        /// <param name="mode">The game mode flag to remove.</param>
        private void RemoveGameModeFlag(GameModeFilter mode)
        {
            CurrentGameModeFilter &= ~mode;
            SetGameModeButtonState(mode.ToString(), false);
        }

        /// <summary>
        /// Updates the state of a game mode button in the UI based on its identifier and activation status.
        /// </summary>
        /// <param name="buttonID">The unique identifier of the button to update.</param>
        /// <param name="isActive">Indicates whether the button should be set to active or inactive.</param>
        private void SetGameModeButtonState(string buttonID, bool isActive)
        {
            if (gameModeFilters is null or { Length: 0 })
            {
                Debug.LogWarning("Rarity filters are not assigned or empty in the ShopUI");
                return;
            }

            // Get the button UI from the toggle group using the button ID
            var toggle = gameModeFilters.FirstOrDefault(x => x.CustomButtonID == buttonID);
            if (toggle == null)
            {
                Debug.LogWarning($"Button with ID '{buttonID}' not found in the categories toggle group.");
                return;
            }

            // Invoke the appropriate action based on the toggle state
            (isActive ? (Action<bool>)toggle.Select : toggle.Deselect).Invoke(true);
        }

        /// <summary>
        /// Handles selection of a filter by parsing the provided string and applying the corresponding filter and
        /// search.
        /// </summary>
        /// <param name="obj">The string representation of the selected filter.</param>
        private void OnFilterSelected(string obj)
        {
            if (!Enum.TryParse(obj, out AchiemeventFilters completionStatus))
            {
                Debug.LogWarning($"Invalid completionStatus selected: {obj}");
                return;
            }

            currentFilter = completionStatus;

            ApplyFilterAndSearch();
        }

        /// <summary>
        /// Handles changes to the search input by applying filtering and search logic.
        /// </summary>
        /// <param name="input">The updated search input string.</param>
        private void OnSearchInputChanged(string input)
        {
            ApplyFilterAndSearch();
        }

        /// <summary>
        /// Handles selection of a game mode by updating the current game mode filter based on the provided button ID
        /// and applies the corresponding filter and search.
        /// </summary>
        /// <param name="buttonID">The identifier of the button representing the selected game mode.</param>
        private void OnGameModeSelected(string buttonID)
        {
            if (string.IsNullOrEmpty(buttonID))
            {
                Debug.LogWarning("Button ID is null or empty. Cannot set game mode filter.");
                return;
            }

            if (!Enum.TryParse<GameModeFilter>(buttonID, out var parsedMode))
            {
                Debug.LogWarning($"Failed to parse button ID '{buttonID}' to GameModeFilter enum.");
                return;
            }

            if (parsedMode == GameModeFilter.None)
            {
                ActivateAllGameModesOnly();
                ApplyFilterAndSearch();
                return;
            }

            // Toggle logic
            if (CurrentGameModeFilter.HasFlag(parsedMode))
                RemoveGameModeFlag(parsedMode);
            else
                AddGameModeFlag(parsedMode);

            // If no filters remain, fallback to All
            if (CurrentGameModeFilter == GameModeFilter.None)
                ActivateAllGameModesOnly();
            else
                SetGameModeButtonState(GameModeFilter.None.ToString(), false);

            ApplyFilterAndSearch();
        }

        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            RootCanvasGroup?.SetActive(isActive);
            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

            ConfigureUI();
        }

        /// <summary>
        /// Specifies filters for sorting or displaying achievements.
        /// </summary>
        internal enum AchiemeventFilters
        {
            All,
            Alphabetical,
            MostRecent,
            CompletionStatus
        }
    }
}
