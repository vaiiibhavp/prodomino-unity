using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.PlayerLeaderboardData;

namespace ProDomino.Leaderboard
{
    public abstract class AbstractLeaderboardUI : MonoBehaviour
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; protected set; }
        [SerializeField] protected LeaderboardElement leaderboardElementPrefab;
        [SerializeField] protected GameObject noLeaderboardEntriesLabel;
        [SerializeField] protected TMP_Text ownRankLabel;
        [SerializeField] protected UnityEvent<Dictionary<string, LeaderboardEntry>> onPlayerLeaderboardDataUpdated;

        protected List<LeaderboardElement> leaderboardElements;

        protected Func<bool> checkIfIsSavingInCache;
        protected Func<DateTime> getNextUpdateTime;
        protected Func<Dictionary<string, LeaderboardEntry>> getPlayerLeaderboards;
        protected Func<GameMode, NumberPlayers, string> getLeaderboardID;
        protected Func<PlayerLeaderboardData[]> getLeaderboardPlayers;
        protected Func<string, string, Sprite> getSprite;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Leaderboard;
        public bool RequiresAuthentication => true;

        public GameMode SelectedGameMode { get; protected set; } = GameMode.french;
        public LeaderboardTier SelectedTier { get; protected set; } = LeaderboardTier.ClassC;
        public NumberPlayers SelectedNumberPlayers { get; protected set; } = NumberPlayers.oneVsOne;

        internal PlayerLeaderboardData[] LeaderboardPlayers => getLeaderboardPlayers?.Invoke();
        internal bool IsSavingInCache => checkIfIsSavingInCache?.Invoke() ?? false;
        internal DateTime NextUpdateTime => getNextUpdateTime?.Invoke() ?? DateTime.UtcNow;
        internal Dictionary<string, LeaderboardEntry> PlayerLeaderboards => getPlayerLeaderboards?.Invoke();

        public string CurrentLeaderboardId { get; private set; }

        /// <summary>
        /// Controller representation of the UI Monobehavour Awake method.<br></br>
        /// This method is responsible for setting up any necessary references or initial configurations for the Leaderboard UI. <br></br>
        /// It is called when the UI is first instantiated and should be used to prepare the UI for any subsequent operations or interactions.
        /// </summary>
        internal abstract void Awake_LeaderboardUI();

        /// <summary>
        /// Controlled representation of the UI Monobehavour Start method.<br></br>
        /// This method is responsible for initializing the Leaderboard UI and setting up any necessary components or listeners. <br></br>
        /// It is called when the UI is first loaded and should be used to prepare the UI for display and interaction.
        /// </summary>
        internal abstract void Start_LeaderboardUI();

        /// <summary>
        /// Initialize the Leaderboard UI with the necessary data and functions to work properly.
        /// </summary>
        /// <param name="checkIfIsSavingInCache"> A function that checks if the leaderboard data is being saved in cache. This is used to determine if the UI should display cached data or not.</param>
        /// <param name="getNextUpdateTime"> A function that returns the next update time for the leaderboard data. This is used to display a countdown or timer for when the next update will occur.</param>
        /// <param name="getPlayerLeaderboards"> A function that returns a dictionary of the player's leaderboard data. The key is the leaderboard ID and the value is the leaderboard entry data. This is used to display the player's current standings on the leaderboard.</param>
        /// <param name="getLeaderboardID"> A function that takes a GameMode and NumberPlayers as parameters and returns the corresponding leaderboard ID. This is used to determine which leaderboard to display based on the current game mode and number of players.</param>
        /// <param name="getLeaderboardPlayers"> A function that returns an array of LeaderboardPlayer objects that represent the players on the leaderboard. This is used to display the players' names, scores, and other relevant information on the UI.</param>
        internal virtual void Initialize
            (Func<bool> checkIfIsSavingInCache,
            Func<DateTime> getNextUpdateTime,
            Func<Dictionary<string, LeaderboardEntry>> getPlayerLeaderboards,
            Func<PlayerLeaderboardData[]> getLeaderboardPlayers,
            Func<GameMode, NumberPlayers, string> getLeaderboardID,
            Func<string, string, Sprite> getSprite)
        {
            this.checkIfIsSavingInCache = checkIfIsSavingInCache;
            this.getNextUpdateTime = getNextUpdateTime;
            this.getPlayerLeaderboards = getPlayerLeaderboards;
            this.getLeaderboardPlayers = getLeaderboardPlayers;
            this.getLeaderboardID = getLeaderboardID;
            this.getSprite = getSprite;

            ConfigureFilters();
        }

        /// <summary>
        /// Updates the player's leaderboard UI, notifies listeners of leaderboard data changes, and sets the player's
        /// rank label based on the current leaderboard.
        /// </summary>
        protected virtual void ConfigureOwnPlayerUI()
        {
            if (PlayerLeaderboards is null or { Count: 0 })
                Debug.LogWarning("Player leaderboards data is null or empty. Setting default values...");

            // Inform the listerner about the player leaderboard data update
            onPlayerLeaderboardDataUpdated?.Invoke(PlayerLeaderboards);

            // Set the own rank label if it exists
            if (ownRankLabel)
            {
                var currentLeaderboard = getLeaderboardID?.Invoke(SelectedGameMode, SelectedNumberPlayers) ?? $"{SelectedGameMode}{SelectedNumberPlayers}";
                ownRankLabel.text = "Your Rank: ";

                // Set the rank label to the player's rank
                if ((PlayerLeaderboards?.TryGetValue(currentLeaderboard, out var ownPlayerEntry) ?? false) && ownPlayerEntry != null)
                    ownRankLabel.text += $"<b>{ownPlayerEntry.Rank + 1}</b>"; // +1 because the rank is 0-based in the leaderboard system
                else
                {
                    Debug.LogWarning($"Player's rank not found for leaderboard: {currentLeaderboard}");
                    ownRankLabel.text += "<b>N/A</b>"; // or some default value if the player is not found
                }
            }
        }

        /// <summary>
        /// Registers a listener to be invoked when player leaderboard data is updated.
        /// </summary>
        /// <param name="listener">The callback to execute with the updated leaderboard data.</param>
        internal abstract void AddListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener);

        /// <summary>
        /// Unsubscribes a listener from player leaderboard data update events.
        /// </summary>
        /// <param name="listener">The callback to remove from the update event.</param>
        internal abstract void RemoveListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener);

        /// <summary>
        /// Refreshes and displays the current leaderboard information in the user interface.
        /// </summary>
        internal abstract void Update_LeaderboardUI();

        /// <summary>
        /// Filters the collection of elements based on predefined criteria.
        /// </summary>
        internal abstract void ConfigureFilters();

        /// <summary>
        /// Filters and orders leaderboard elements based on the selected game mode, number of players, and tier, then
        /// updates the leaderboard display accordingly.
        /// </summary>
        /// <param name="playerLeaderboardDatas">An array of player leaderboard data to be filtered and displayed.</param>
        protected virtual void FilterAndOrderElements(PlayerLeaderboardData[] playerLeaderboardDatas)
        {
            // Configure the own player UI first
            ConfigureOwnPlayerUI();
            if (leaderboardElements is null or { Count: 0 })
            {
                Debug.LogWarning("[LeaderboardUI_New] Leaderboard elements list is null or empty. Cannot filter elements.");
                return;
            }

            // Configure every leaderboard element based on the selected filters
            CurrentLeaderboardId = getLeaderboardID?.Invoke(SelectedGameMode, SelectedNumberPlayers) ?? $"{SelectedGameMode}{SelectedNumberPlayers}";

            // Filter the players based on the selected tier and the current leaderboard, then order them by rank and select the player data for each one
            var filteredPlayers = FilterElements(playerLeaderboardDatas);

            ConfigureLeaderboardElements(filteredPlayers);
        }

        /// <summary>
        /// Filters and orders player leaderboard data by the selected tier and current leaderboard.
        /// </summary>
        /// <param name="playerLeaderboardDatas">An array of player leaderboard data to filter.</param>
        /// <returns>An array of filtered and ordered player leaderboard data.</returns>
        protected virtual PlayerLeaderboardData[] FilterElements(PlayerLeaderboardData[] playerLeaderboardDatas)
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
                    x.leaderboardInfo.leaderboardData.tier == SelectedTier.ToString()
                )
                .OrderBy(x => x.leaderboardInfo.leaderboardData.rank)
                .Select(x => x.playerData)
                .ToArray();
        }

        /// <summary>
        /// Configures leaderboard UI elements based on the provided player data and updates the visibility of the 'no
        /// entries' label.
        /// </summary>
        /// <param name="filteredPlayers">An array of leaderboard elements to be configured.</param>
        /// <param name="filteredPlayers">An array of player leaderboard data corresponding to each leaderboard element.</param>
        protected virtual void ConfigureLeaderboardElements(PlayerLeaderboardData[] filteredPlayers)
        { 
            // Update each leaderboard element with the corresponding player data
            if (filteredPlayers is not null and { Length: > 0 })
                for (var i = 0; i < leaderboardElements.Count; i++)
                {
                    var element = leaderboardElements[i];
                    var leaderboardPlayer = filteredPlayers.ElementAtOrDefault(i);
                    if (leaderboardPlayer is not null)
                    {
                        element.SetActive(true);
                        element.Configure(leaderboardPlayer, CurrentLeaderboardId);
                    } 
                    else
                        element.SetActive(false);
                }
            else
                foreach (var element in leaderboardElements)
                    element.SetActive(false);

            // Show/hide the "no entries" label based on whether there are filtered players
            if (noLeaderboardEntriesLabel)
                noLeaderboardEntriesLabel.SetActive(filteredPlayers is null or { Length: 0 });
        }

        /// <summary>
        /// Retrieves a sprite from the specified collection using the provided sprite and collection identifiers.
        /// </summary>
        /// <param name="spriteId">The identifier of the sprite to retrieve.</param>
        /// <param name="collectionId">The identifier of the sprite collection.</param>
        /// <returns>The requested sprite, or the default value if the retrieval function is not set.</returns>
        protected virtual Sprite GetSprite(string spriteId, string collectionId)
        {
            if (getSprite is null)
            { 
                Debug.LogError("GetSprite function is not set. Make sure to initialize the Leaderboard UI with a valid GetSprite function.");
                return default;
            }

            return getSprite.Invoke(spriteId, collectionId);
        }

        /// <summary>
        /// Handles actions to perform when a user signs in.
        /// </summary>
        internal virtual void OnSignIn()
        {
            // Filter the leaderboard elements based on the selected filters
            ConfigureFilters();
        }

        /// <summary>
        /// Handles user sign-out operations.
        /// </summary>
        internal virtual void OnSignOut()
        {
            // Filter the leaderboard elements based on the selected filters
            ConfigureFilters();
        }      
    }
}
