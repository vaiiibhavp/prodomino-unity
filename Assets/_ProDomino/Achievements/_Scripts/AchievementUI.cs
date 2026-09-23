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
    public class AchievementUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
        [SerializeField] private Color hightlightColor = Color.yellow;
        [SerializeField] private TMP_InputField searchAchievementInputfield;
        [SerializeField] private CustomButtonToggleGroupUI filtersToggleGroupUI;
        [SerializeField] private CustomButtonUI[] gameModeFilters;
        [Space(10), Header("Dropdown Filters")]
        [SerializeField] private TMP_Dropdown sortByDropdown;
        [SerializeField] private TMP_Dropdown statusDropdown;
        [SerializeField] private TMP_Dropdown gameDropdown;

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

        private bool _isAwakeInitialized;

        private void Awake()
        {
            Awake_AchievementUI();
        }

        /// <summary>
        /// Initializes the achievement UI by retrieving achievement elements, setting up filter selection callbacks,
        /// and registering the search input listener.
        /// </summary>
        internal void Awake_AchievementUI()
        {
            if (_isAwakeInitialized) return;
            _isAwakeInitialized = true;

            achievementInstances = achievementElementParent?.GetComponentsInChildren<AchievementElement>(true)?.ToList() ?? new();

            if (filtersToggleGroupUI)
                filtersToggleGroupUI.SetOnCustomButtonSelectedCallback(OnFilterSelected);

            if (searchAchievementInputfield)
                searchAchievementInputfield.onValueChanged.AddListener(OnSearchInputChanged);

            if (sortByDropdown)
                sortByDropdown.onValueChanged.AddListener(_ => ApplyFilterAndSearch());

            if (statusDropdown)
                statusDropdown.onValueChanged.AddListener(_ => ApplyFilterAndSearch());

            if (gameDropdown)
                gameDropdown.onValueChanged.AddListener(_ => ApplyFilterAndSearch());
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
                totalAchievementsLabel.text = $"{completedAchievements?.Count ?? 0} / {achievementDataCollection.Keys?.Count ?? 0}";

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
            if (achievementInstances is null or { Count: 0 })
            {
                if (achievementElementParent != null)
                    achievementInstances = achievementElementParent.GetComponentsInChildren<AchievementElement>(true)?.ToList() ?? new();

                if (achievementInstances is null or { Count: 0 })
                {
                    ConfigureUI();
                    return;
                }
            }

            // 1. Read current filter selections
            string selectedGame = gameDropdown != null && gameDropdown.options.Count > gameDropdown.value
                ? gameDropdown.options[gameDropdown.value].text
                : "Game";

            string selectedStatus = statusDropdown != null && statusDropdown.options.Count > statusDropdown.value
                ? statusDropdown.options[statusDropdown.value].text
                : "Status";

            string selectedSort = sortByDropdown != null && sortByDropdown.options.Count > sortByDropdown.value
                ? sortByDropdown.options[sortByDropdown.value].text
                : "Sort By";

            string input = searchAchievementInputfield != null ? searchAchievementInputfield.text.Trim() : string.Empty;

            // 2. Filter visibility for each element
            foreach (var element in achievementInstances)
            {
                if (element == null) continue;
                var data = element.GameAchievementData;
                var pData = element.PlayerAchievementData;

                if (data == null)
                {
                    // Fallback for sample/preview rows when data is null:
                    string titleText = element.transform.Find("Col_Info/NameLabel")?.GetComponent<TMP_Text>()?.text ?? "";
                    string descText = element.transform.Find("Col_Info/DescLabel")?.GetComponent<TMP_Text>()?.text ?? "";
                    string gameText = element.transform.Find("Col_Game/GameLabel")?.GetComponent<TMP_Text>()?.text ?? "";
                    bool isClaimActive = element.transform.Find("Col_Action/ClaimButton")?.gameObject.activeSelf ?? false;
                    bool isClaimedActive = element.transform.Find("Col_Action/CompletedContainer")?.gameObject.activeSelf ?? false;
                    bool isInProgActive = element.transform.Find("Col_Action/IncompletedContainer")?.gameObject.activeSelf ?? false;

                    bool isVis = true;
                    if (!string.IsNullOrEmpty(selectedGame) && selectedGame != "Game" && selectedGame != "All Games")
                    {
                        if (gameText.IndexOf(selectedGame.Replace(" Game", "").Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                            isVis = false;
                    }
                    if (isVis && !string.IsNullOrEmpty(selectedStatus) && selectedStatus != "Status" && selectedStatus != "All Status")
                    {
                        if (selectedStatus == "Claimed" && !isClaimedActive) isVis = false;
                        else if (selectedStatus == "Claimable" && !isClaimActive) isVis = false;
                        else if (selectedStatus == "In Progress" && !isInProgActive) isVis = false;
                    }
                    if (isVis && !string.IsNullOrEmpty(input))
                    {
                        if (titleText.IndexOf(input, StringComparison.OrdinalIgnoreCase) < 0 &&
                            descText.IndexOf(input, StringComparison.OrdinalIgnoreCase) < 0)
                            isVis = false;
                    }
                    element.gameObject.SetActive(isVis);
                    if (isVis && !string.IsNullOrEmpty(input)) element.HighlightNameMatches(input);
                    else element.HighlightNameMatches("");
                    continue;
                }

                bool isVisible = true;

                // Game Filter
                if (!string.IsNullOrEmpty(selectedGame) && selectedGame != "Game" && selectedGame != "All Games")
                {
                    string achName = data.name ?? string.Empty;
                    string filterMode = selectedGame.Replace(" Game", "").Trim();
                    bool matchesGame = achName.IndexOf(filterMode, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matchesGame && data.gameModeFilter.HasValue)
                    {
                        matchesGame = data.gameModeFilter.Value.ToString().IndexOf(filterMode, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (!matchesGame)
                        isVisible = false;
                }
                else if (CurrentGameModeFilter != GameModeFilter.All && CurrentGameModeFilter != GameModeFilter.None)
                {
                    if (data.gameModeFilter.HasValue && !CurrentGameModeFilter.HasFlag(data.gameModeFilter.Value))
                        isVisible = false;
                }

                // Status Filter
                if (isVisible && !string.IsNullOrEmpty(selectedStatus) && selectedStatus != "Status" && selectedStatus != "All Status")
                {
                    bool isClaimed = pData?.claimed ?? false;
                    bool isCompleted = pData?.completed ?? false;
                    long curProg = pData?.progress ?? 0;
                    long goal = data.goalAmount > 0 ? data.goalAmount : 1;
                    bool isClaimable = !isClaimed && (isCompleted || curProg >= goal);
                    bool isInProgress = !isClaimed && !isCompleted && curProg < goal;

                    if (selectedStatus == "Claimed" && !isClaimed) isVisible = false;
                    else if (selectedStatus == "Claimable" && !isClaimable) isVisible = false;
                    else if (selectedStatus == "In Progress" && !isInProgress) isVisible = false;
                }

                // Search Filter (substring match or token relevance)
                if (isVisible && !string.IsNullOrEmpty(input))
                {
                    string achName = data.name ?? string.Empty;
                    string achDesc = data.description ?? string.Empty;
                    bool matchesSearch = achName.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         achDesc.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!matchesSearch)
                    {
                        var nameTokens = AchievementManager.Tokenize(achName);
                        var searchTokens = AchievementManager.Tokenize(input);
                        int score = CalculateRelevance(nameTokens, searchTokens);
                        if (score < 2) isVisible = false;
                    }
                }

                element.gameObject.SetActive(isVisible);

                // Highlight search matches
                if (isVisible && !string.IsNullOrEmpty(input))
                    element.HighlightNameMatches(input);
                else
                    element.HighlightNameMatches("");
            }

            // 3. Sorting visible achievements
            var visibleList = achievementInstances.Where(x => x != null && x.gameObject.activeSelf).ToList();

            if (selectedSort == "Alphabetical" || currentFilter == AchiemeventFilters.Alphabetical)
            {
                visibleList = visibleList.OrderBy(x => x.GameAchievementData?.name).ToList();
            }
            else if (selectedSort == "Points")
            {
                visibleList = visibleList.OrderByDescending(x => x.GameAchievementData?.points ?? 0).ToList();
            }
            else if (selectedSort == "Progress")
            {
                visibleList = visibleList.OrderByDescending(x =>
                {
                    long cur = x.PlayerAchievementData?.progress ?? 0;
                    long goal = x.GameAchievementData?.goalAmount ?? 1;
                    return goal > 0 ? (float)cur / goal : 0f;
                }).ToList();
            }
            else if (selectedSort == "Most Recent" || currentFilter == AchiemeventFilters.MostRecent)
            {
                visibleList = visibleList.OrderByDescending(x => x.PlayerAchievementData?.completedTime ?? 0).ToList();
            }
            else if (currentFilter == AchiemeventFilters.CompletionStatus)
            {
                visibleList = visibleList.OrderByDescending(x => x.PlayerAchievementData?.completed ?? false).ToList();
            }
            else if (!string.IsNullOrEmpty(input))
            {
                // Order by search relevance
                visibleList = SearchAchievementsByRelevance(input, visibleList);
            }
            else
            {
                // Default sorting by rank/tier/type/goal
                visibleList = visibleList
                    .Select((achievement, index) => new { achievement, index })
                    .OrderBy(x => ((int?)x.achievement?.GameAchievementData?.achievementRank) ?? x.index)
                    .ThenBy(x => x.achievement?.GameAchievementData?.leaderboardTier)
                    .ThenBy(x => x.achievement?.GameAchievementData?.achievementType)
                    .ThenBy(x => x.achievement?.GameAchievementData?.goalAmount)
                    .Select(x => x.achievement)
                    .ToList();
            }

            for (int i = 0; i < visibleList.Count; i++)
            {
                visibleList[i].transform.SetSiblingIndex(i);
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
                return;
            }

            // Get the button UI from the toggle group using the button ID
            var toggle = gameModeFilters.FirstOrDefault(x => x != null && x.CustomButtonID == buttonID);
            if (toggle == null)
            {
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
