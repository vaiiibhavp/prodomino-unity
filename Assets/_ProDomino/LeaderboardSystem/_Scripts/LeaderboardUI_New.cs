using Cysharp.Threading.Tasks;
using DG.Tweening.Core.Easing;
using HelperSharedLibrary;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using TMPro;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.FirestoreClubData;
using static HelperSharedLibrary.PlayerLeaderboardData;
using static TMPro.TMP_Dropdown;

namespace ProDomino.Leaderboard
{
    internal class LeaderboardUI_New : AbstractLeaderboardUI, INavigationPanel
    {
        [Header("Leaderboard UI - New")]
        [SerializeField] protected Transform elementsParent;

        [Header("Filter Dropdowns")]
        [SerializeField] private TMP_Dropdown gameModesFiltersDropdown;
        [SerializeField] private TMP_Dropdown tierFiltersDropdown;
        [SerializeField] private TMP_Dropdown playerAmountFiltersDropdown;
        [SerializeField] private TMP_Dropdown nationalityFiltersDropdown;

        protected Func<PlayerLeaderboardData[]> getNationalityPlayers;
        protected AsyncActionHandler<NationalityType> tryToGetLeaderboardsFromRTDB;

        // This constant is used as the display text for the "All" option
        private const string allNationalitiesOption = "All";
        internal NationalityType? SelectedNationalityType { get; private set; } = null; // By default, the player has selected "All" nationalities, so the value is null
        internal PlayerLeaderboardData[] NationalityPlayers => getNationalityPlayers?.Invoke();

        internal override void Awake_LeaderboardUI()
        {
            leaderboardElements = elementsParent.GetComponentsInChildren<LeaderboardElement>(true)?.ToList() ?? new();

            // Initialize each leaderboard element with the method to get the sprite for a given game mode
            if (leaderboardElements is not null and { Count: > 0 })
                leaderboardElements.ForEach(element => element.Initialize(GetSprite));          
        }

        internal override void Start_LeaderboardUI()
        {
            // By default, select the first button in each toggle group (each entry filters the leaderboard)
            gameModesFiltersDropdown?.SetValueWithoutNotify(0);
            tierFiltersDropdown?.SetValueWithoutNotify(0);
            playerAmountFiltersDropdown?.SetValueWithoutNotify(0);
            nationalityFiltersDropdown?.SetValueWithoutNotify(0);
        }
        internal override void Update_LeaderboardUI() { }

        /// <summary>
        /// Initialize the Leaderboard UI with the necessary data and functions to work properly.
        /// </summary>
        /// <param name="checkIfIsSavingInCache"> A function that checks if the leaderboard data is being saved in cache. This is used to determine if the UI should display cached data or not.</param>
        /// <param name="getNextUpdateTime"> A function that returns the next update time for the leaderboard data. This is used to display a countdown or timer for when the next update will occur.</param>
        /// <param name="currentClientLeaderboards"> A function that returns a dictionary of the player's leaderboard data. The key is the leaderboard ID and the value is the leaderboard entry data. This is used to display the player's current standings on the leaderboard.</param>
        /// <param name="getLeaderboardID"> A function that takes a GameMode and NumberPlayers as parameters and returns the corresponding leaderboard ID. This is used to determine which leaderboard to display based on the current game mode and number of players.</param>
        /// <param name="getLeaderboardPlayers"> A function that returns an array of LeaderboardPlayer objects that represent the players on the leaderboard. This is used to display the players' names, scores, and other relevant information on the UI.</param>
        /// <param name="getNationalityPlayers"> A function that returns an array of LeaderboardPlayer objects filtered</param>
        /// <param name="tryToGetLeaderboardsFromRTDB"> An asynchronous action handler that attempts to retrieve leaderboard data from a real-time database based on the provided</param>
        internal void Initialize
            (Func<bool> checkIfIsSavingInCache,
            Func<DateTime> getNextUpdateTime,
            Func<Dictionary<string, LeaderboardEntry>> currentClientLeaderboards,
            Func<PlayerLeaderboardData[]> getLeaderboardPlayers,
            Func<GameMode, NumberPlayers, string> getLeaderboardID,
            Func<string, string, Sprite> getSprite,

            Func<PlayerLeaderboardData[]> getNationalityPlayers,
            AsyncActionHandler<NationalityType> tryToGetLeaderboardsFromRTDB)
        {
            this.getNationalityPlayers = getNationalityPlayers;
            this.tryToGetLeaderboardsFromRTDB = tryToGetLeaderboardsFromRTDB;

            base.Initialize(
                checkIfIsSavingInCache,
                getNextUpdateTime,
                currentClientLeaderboards,
                getLeaderboardPlayers,
                getLeaderboardID,
                getSprite);

            InitializeDropdowns();
            ConfigureFilters();

            void InitializeDropdowns()
            {
                if (gameModesFiltersDropdown)
                {
                    // Get the game mode names from the enum, excluding the "none" value, and add them as options to the dropdown
                    var gameModes = Enum.GetNames(typeof(GameMode))
                        ?.Where(x => x != GameMode.none.ToString() && x != GameMode.concentrate.ToString() && x != GameMode.replay.ToString())
                        ?.Select(x => x.CapitalizeFirstLetter())
                        ?.ToList();

                    // Add an empty option at the beginning of the list to allow deselecting the filter (showing all game modes)
                    gameModesFiltersDropdown.ClearOptions();
                    gameModesFiltersDropdown.AddOptions(gameModes);

                    // Add listener to handle game mode selection changes
                    gameModesFiltersDropdown.onValueChanged.AddListener(OnGameModeSelected);
                }

                if (tierFiltersDropdown)
                {
                    // Get the tier names from the enum, excluding the "none" value, and add them as options to the dropdown
                    var tiers = Enum.GetNames(typeof(LeaderboardTier))
                        ?.Where(x => x != LeaderboardTier.None.ToString())
                        ?.Select(x => $"{x.SplitByUpperCase()} ({x.GetInitials()})")
                        ?.ToList();

                    // Add an empty option at the beginning of the list to allow deselecting the filter (showing all tiers)
                    tierFiltersDropdown.ClearOptions();
                    tierFiltersDropdown.AddOptions(tiers);

                    // Add listener to handle tier selection changes
                    tierFiltersDropdown.onValueChanged.AddListener(OnTierSelected);
                }

                if (playerAmountFiltersDropdown)
                {
                    // Get the player amount names from the enum, excluding the "none" value, and add them as options to the dropdown
                    var playerAmounts = Enum.GetValues(typeof(NumberPlayers))
                        ?.Cast<NumberPlayers>()
                        ?.Where(x => x is NumberPlayers.oneVsOne or NumberPlayers.oneVsThree)
                        ?.ToList();

                    var numberOfPlayers = playerAmounts
                        ?.Select(x => x switch
                        {
                            NumberPlayers.oneVsOne => "1v1",
                            NumberPlayers.oneVsThree => "1v3",
                            _ => x.ToString()
                        })
                        ?.ToList();

                    // Add an empty option at the beginning of the list to allow deselecting the filter (showing all player amounts)
                    playerAmountFiltersDropdown.ClearOptions();
                    playerAmountFiltersDropdown.AddOptions(numberOfPlayers);

                    // Add listener to handle player amount selection changes
                    playerAmountFiltersDropdown.onValueChanged.AddListener(OnPlayerAmountSelected);
                }

                if (nationalityFiltersDropdown)
                {
                    nationalityFiltersDropdown.ClearOptions();
                    nationalityFiltersDropdown.AddOptions(new List<string> { allNationalitiesOption });

                    // Simply get all the nationality
                    var nationalities = Enum.GetValues(typeof(NationalityType))?.Cast<NationalityType>()?.ToArray();

                    // Generate a collection of nationality and its sprite
                    var nationalitiesCollection = new Dictionary<string, Sprite>();

                    // Loop through the nationalities and get their corresponding sprites, then add them to the collection with
                    // Callin TryToCacheNationalities should ensure that the sprites are cached before, so this loop should be efficient
                    // and not cause performance issues since the sprites will be retrieved from cache
                    foreach (var nationality in nationalities)
                    {
                        var nationalityKey = nationality.ToString().SplitByUpperCase();
                        var nationalitySprite = GetSprite(nationality.ToString(), Consts.CollectionKeys.Nationality);
                        
                        nationalitiesCollection[nationalityKey] = nationalitySprite;
                    }

                    // Make a list with the options to set up in the dropdown
                    var nationalitiesOptionsData = nationalitiesCollection
                        ?.Select(x => new OptionData(x.Key, x.Value, Color.white))
                        ?.ToList();

                    // Add an empty option at the beginning of the list to allow deselecting the filter (showing all nationalities)
                    nationalityFiltersDropdown.AddOptions(nationalitiesOptionsData);

                    // Add listener to handle
                    nationalityFiltersDropdown.onValueChanged.AddListener(OnNationalitySelected);
                }
            }
        }

        internal override void AddListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            if (listener == null)
            {
                Debug.LogWarning("Listener is null. Cannot add to onPlayerLeaderboardDataUpdated.");
                return;
            }
            onPlayerLeaderboardDataUpdated.AddListener(listener);
        }

        internal override void RemoveListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            if (listener == null)
            {
                Debug.LogWarning("Listener is null. Cannot remove from onPlayerLeaderboardDataUpdated.");
                return;
            }
            onPlayerLeaderboardDataUpdated.RemoveListener(listener);
        }

        /// <summary>
        /// Filters the collection of elements based on predefined criteria.
        /// </summary>
        internal override void ConfigureFilters()
        {
            // Determine the source of players based
            var source = SelectedNationalityType.HasValue
                ? NationalityPlayers
                : LeaderboardPlayers;

            // Apply filtering and ordering to the leaderboard elements based on the selected filters
            FilterAndOrderElements(source);

            // After filtering the elements, we need to refresh the layout groups to ensure the UI updates correctly and the elements are displayed in the right order and with the correct spacing.
            // This is especially important after changing
            transform.RefreshLayoutGroupsImmediateAndRecursive();
            transform.RefreshContentSizeFitterImmediateAndRecursive(this);
        }

        /// <summary>
        /// Filters and orders player leaderboard data by the selected tier and current leaderboard.
        /// </summary>
        /// <param name="playerLeaderboardDatas">An array of player leaderboard data to filter.</param>
        /// <returns>An array of filtered and ordered player leaderboard data.</returns>
        protected override PlayerLeaderboardData[] FilterElements(PlayerLeaderboardData[] playerLeaderboardDatas)
        {
            // Local function to get the leaderboard info for a player based on the leaderboard ID
            var getLeaderboardInfo = new Func<PlayerLeaderboardData, string, LeaderboardInfo>
                ((_playerLeaderboardInfo, _leaderboardId) => _playerLeaderboardInfo.leaderboardInfos?.FirstOrDefault(x => x.leaderboardId == _leaderboardId));

            // Filter the players based on the selected tier and the current leaderboard, then order them by rank and select the player data for each one
            return playerLeaderboardDatas?
                .Select(playerData => new
                {
                    playerData,
                    leaderboardInfo = getLeaderboardInfo(playerData, CurrentLeaderboardId)
                })
                .Where(x =>
                    x.leaderboardInfo != null &&
                    x.leaderboardInfo.leaderboardData.tier == SelectedTier.ToString() &&
                    (!SelectedNationalityType.HasValue || x.playerData.playerNationality == SelectedNationalityType.Value)
                )
                .OrderBy(x => x.leaderboardInfo.leaderboardData.rank)
                .Select(x => x.playerData)
                .ToArray();
        }

        /// <summary>
        /// Handles selection of a game mode from the dropdown, updates the selected game mode, and filters elements
        /// accordingly.
        /// </summary>
        /// <param name="gameModeSelectedIndex">Index of the selected game mode in the dropdown options.</param>
        private void OnGameModeSelected(int gameModeSelectedIndex)
        {
            // Get the name of the selected game mode from the dropdown options based on the selected index, and convert it to lowercase for consistent parsing
            // (e.g., "Solo" vs "solo"), so this ensures that the parsing works correctly regardless of the case used in the dropdown options.
            var gameModeName = gameModesFiltersDropdown.options.ElementAtOrDefault(gameModeSelectedIndex)?.text?.ToLowerInvariant();
            if (!Enum.TryParse(gameModeName, out GameMode gameMode))
            {
                Debug.LogWarning($"Invalid game mode selected: {gameModeName}");
                return;
            }

            SelectedGameMode = gameMode;
            ConfigureFilters();
        }

        /// <summary>
        /// Handles selection of a leaderboard tier from the dropdown and updates the filter accordingly.
        /// </summary>
        /// <param name="tierSelectedIndex">Index of the selected tier in the dropdown options.</param>
        private void OnTierSelected(int tierSelectedIndex)
        {
            // Get the name of the selected tier from the dropdown options based on the selected index, split it by space to separate
            // the tier name from its initials (e.g., "Grand Master (GM)")
            var splittedTierWords = tierFiltersDropdown.options.ElementAtOrDefault(tierSelectedIndex)?.text?.Split(" ");

            // Join the splitted words except the last one (the initials between parentheses) to get the full tier name,
            // which will be used to parse the enum value (e.g., "Grand Master")
            var joinedTierName = string.Join(" ", splittedTierWords?.TakeWhile((x, i) => i != splittedTierWords.Length - 1));

            // Then compact the joined tier name by removing spaces before uppercase letters to match the enum naming convention
            // (e.g., "Grand Master" becomes "GrandMaster")
            var tierName = joinedTierName?.CompactByUpperCase();

            // The enums is capitalized
            if (!Enum.TryParse(tierName, out LeaderboardTier leaderboardTier))
            {
                Debug.LogWarning($"Invalid tier selected: {tierName}");
                return;
            }

            SelectedTier = leaderboardTier;
            ConfigureFilters();
        }

        /// <summary>
        /// Handles selection of the player amount from the dropdown, updates the selected number of players, and
        /// filters elements accordingly.
        /// </summary>
        /// <param name="playerAmountSelectedIndex">Index of the selected player amount in the dropdown options.</param>
        private void OnPlayerAmountSelected(int playerAmountSelectedIndex)
        {
            var playerAmountName = playerAmountFiltersDropdown.options.ElementAtOrDefault(playerAmountSelectedIndex)?.text;

            // Since the dropdown options for player amounts are displayed as "1v1" and "1v3", we need to convert these display names back to the corresponding enum names
            // ("oneVsOne" and "oneVsThree") before parsing them. This mapping ensures that the selected option from the dropdown correctly corresponds to the enum values used in the code.
            var playerAmountNameToParse = playerAmountName switch
            {
                "1v1" => "oneVsOne",
                "1v3" => "oneVsThree",
                _ => playerAmountName
            };

            // Try to parse the player amount name to the corresponding enum value. If parsing fails, it means the selection is invalid (e.g., an empty option or an unrecognized value).
            if (!Enum.TryParse(playerAmountNameToParse, out NumberPlayers numberPlayers))
            {
                // Check fi the entry arg is empty. If so, that means the toggle is probably deselecting
                if (playerAmountName is not "")
                    Debug.LogWarning($"Invalid player amount selected: {playerAmountName}");
                return;
            }

            SelectedNumberPlayers = numberPlayers;
            ConfigureFilters();
        }

        /// <summary>
        /// Handles the selection of a nationality from the dropdown, updates the selected nationality, retrieves
        /// corresponding leaderboards, and filters displayed elements.<br></br>
        /// 
        /// This methos needs a way to block the screen while the leaderboards are being retrieved from RTDB, 
        /// to avoid multiple rapid selections that could trigger multiple requests and cause performance issues or inconsistent UI states. 
        /// We assumed tha GameManager.HandleProcess with loading screen is being used or maybe the data is loading quickly<br></br>
        /// </summary>
        /// <param name="nationalitySelectedIndex">Index of the selected nationality in the dropdown options.</param>
        private async void OnNationalitySelected(int nationalitySelectedIndex)
        {
            // Check if the tryToGetLeaderboardsFromRTDB function is assigned before invoking it, and log an error if it's not
            if (tryToGetLeaderboardsFromRTDB is null)
            { 
                Debug.LogError("tryToGetLeaderboardsFromRTDB function is not assigned. Cannot retrieve leaderboards");
                return;
            }

            var nationalityName = nationalityFiltersDropdown.options.ElementAtOrDefault(nationalitySelectedIndex)?.text;
            var nationality = Enum.TryParse(nationalityName, out NationalityType nationalityValue) ? nationalityValue : default(NationalityType?);

            // If the selected option is not "All" and the parsed nationality value is null, it means the selection is invalid
            if (nationalityName is not allNationalitiesOption && !nationality.HasValue)
            {
                // Check fi the entry arg is empty. If so, that means the toggle is probably deselecting
                if (nationalityName is not "")
                    Debug.LogWarning($"Invalid nationality selected: {nationalityName}");
                return;
            }

            SelectedNationalityType = nationality;

            // If a nationality is selected (not "All"), try to get the leaderboards from RTDB for
            if (SelectedNationalityType.HasValue)
                await tryToGetLeaderboardsFromRTDB(SelectedNationalityType.Value);

            ConfigureFilters();

            // Wait until the next frame (gameobjects are enabled/disabled and the layout is updated) before refreshing the layout groups to ensure that the UI updates correctly and the elements are displayed in the right order and with the correct spacing.
            await UniTask.NextFrame();
        }
    }
}
