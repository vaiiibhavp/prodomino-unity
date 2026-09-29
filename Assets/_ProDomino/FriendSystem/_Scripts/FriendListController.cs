using Cysharp.Threading.Tasks;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.FriendSystem.FriendManager;
using static ProDomino.GameSystem.GameManager;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Controls the UI and behavior of the friends list, including requests, ordering, and interactions.
    /// </summary>
    public partial class PartyController
    {
        [Header("FriendList Fields")]
        [SerializeField] private CategoriesShown categoriesShown = CategoriesShown.All;
        [SerializeField] private CanvasGroup friendListCanvasGroup;
        [SerializeField] private FriendEntry friendEntryPrefab;
        [SerializeField] private Transform friendEntriesParent;
        [SerializeField] private TMP_Text friendsCountLabel;
        [SerializeField] private GameObject emptyFriendlistLabel;

        [SerializeField] private CustomButtonToggleGroupUI orderByToggleGroup;
        [SerializeField] private Button closePopUp;

        [Header("Request Friendship")]
        [SerializeField] private bool useUnityExactMatchForFriendRequests;
        [SerializeField] private CanvasGroup searchUsersCanvasGroup;
        [SerializeField] private CustomButtonUI requestFriendshipButton;
        [SerializeField] private TMP_InputField requestFriendshipInputfield;
        [SerializeField] private Transform searchUserEntriesParent;
        [SerializeField] private SearchUserEntry searchUserEntryPrefab;

        [Header("Screen States (optional)")]
        [Tooltip("Shown when there are no friends and no recently played players to suggest.")]
        [SerializeField] private GameObject firstMatchHint;
        [Tooltip("Search bar row: centered with a fixed width while the friends list is empty, full width otherwise.")]
        [SerializeField] private LayoutElement searchRowLayout;
        [SerializeField] private float emptyStateSearchRowWidth = 607f;
        [Tooltip("Grid holding the friend cards.")]
        [SerializeField] private GameObject friendsSection;
        [Tooltip("Section listing player cards: recently played players, or the search results.")]
        [SerializeField] private GameObject playersSection;
        [SerializeField] private TMP_Text playersSectionTitle;
        [SerializeField] private string recentlyPlayedTitle = "Recently played players";
        [SerializeField] private string searchResultsTitle = "Search results";
        [Tooltip("Shown when a search returned no players.")]
        [SerializeField] private GameObject playerNotFoundState;

#if UNITY_EDITOR
        [Header("Editor Tests")]
        [SerializeField] private bool sendInvite;
        [SerializeField] private string testPlayerIDToInvite;
        [SerializeField] private string testPlayerNameToInvite;
#endif

        private List<FriendEntry> friendEntriesInstances;
        private List<SearchUserEntry> searchUserEntryInstances;
        private bool isShowingSearchResults;
        private bool hasSearchResults;
        private bool hasRecentlyPlayedPlayers;
        private bool isSearchFieldFocused;

        private bool UsesScreenStates => playersSection;
        protected GameManager gameManager;
        protected FriendManager friendManager;

        internal bool IsViewingFriendList { get; private set; }
        internal FriendsEntryData[] FriendsEntryDatas => friendManager.FriendsEntryDatas?.ToArray();

        internal const string FriendList = "FriendList";
        internal const string RecentlyPlayed = "RecentlyPlayed";

        /// <summary>
        /// Handles the initialization of the controller and assigns UI callbacks.
        /// </summary>
        private void Awake_FriendListController()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            friendManager = ServiceLocator.Instance.GetService<FriendManager>();

            if (!gameManager || !friendManager)
                Debug.LogWarningFormat("Service {GameManager} or {FriendManager} is null and could be used.", nameof(GameManager), nameof(FriendManager));

            // The full-screen layout has no close button; the sidebar and outside clicks close it
            if (closePopUp)
                closePopUp.onClick.AddListener(() => SetVisibility(false));

            // Outside-click close bypasses SetVisibility; route it so listeners (sidebar highlight) get notified
            if (TryGetComponent<CanvasGroupVisibilityController>(out var outsideClickCloser))
                outsideClickCloser.OnHiddenByClick += () => SetVisibility(false);

            // By default, start viewing the friends list
            IsViewingFriendList = true;

            // Try to get instances already prepared in the editor. If there are, initialize them before using them
            friendEntriesInstances = friendEntriesParent?.GetComponentsInChildren<FriendEntry>(true)?.ToList() ?? new();
            foreach (var friendEntryInstance in friendEntriesInstances)
                friendEntryInstance.Initialize
                    (InviteFriendToPlay, 
                    RemoveFriendFromList,
                    () => PartyMembers);

            // Assign the request friendship button callback
            if (requestFriendshipButton)
                requestFriendshipButton.onClick.AddListener(OnRequestFriendship);

            if (requestFriendshipInputfield)
            {
                // Enter searches as well, and clearing the field leaves the search results
                requestFriendshipInputfield.onSubmit.AddListener(_ => OnRequestFriendship());
                requestFriendshipInputfield.onValueChanged.AddListener(OnSearchTextChanged);
                requestFriendshipInputfield.onSelect.AddListener(OnSearchFieldSelected);
                requestFriendshipInputfield.onDeselect.AddListener(OnSearchFieldDeselected);
            }

            // By default, hide the search users canvas group
            if (searchUsersCanvasGroup)
                searchUsersCanvasGroup.SetActive(false);

            // Try to get search user entry instances already prepared in the editor
            if (searchUserEntriesParent)
                searchUserEntryInstances = searchUserEntriesParent.GetComponentsInChildren<SearchUserEntry>(true)?.ToList() ?? new();

            // Initialize existing search user entry instances
            if (searchUserEntryInstances is not null and { Count: > 0 })
                foreach (var searchUserEntryInstance in searchUserEntryInstances)
                    searchUserEntryInstance.Initialize(OnInviteFriendToPlay);
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (sendInvite)
            {
                sendInvite = false;
                Test_InviteFriendToPlay().Forget();
            }

            async UniTask Test_InviteFriendToPlay()
            {
                await OnInviteFriendToPlay(new()
                {
                    displayName = testPlayerNameToInvite,
                    userId = testPlayerIDToInvite
                });
            }
        }
#endif

        /// <summary>
        /// Sends a friends request to the player ID specified in the input field.
        /// </summary>
        private async void OnRequestFriendship()
        {
            if (string.IsNullOrEmpty(requestFriendshipInputfield?.text))
            {
                Debug.LogWarning("Cannot send a friends request because the input field is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning("Cannot send a friends request because the friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                { 
                    Debug.LogWarning("Cannot send a friends request because the friends manager is null or not initialized");
                    return;
                }
            }

            requestFriendshipButton.SetButtonInteractable(false);
            try
            {
                if (useUnityExactMatchForFriendRequests)
                    await UGSExactSearch();

                else
                    await FirebasePartialSearch();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Exception trying to send a friends request:\n\n{ex.Message}</color>");
            }
            finally
            {
                requestFriendshipButton.SetButtonInteractable(true);
            }

            // Search users by exact name using Unity Gaming Services Friends API
            async UniTask UGSExactSearch()
            {
                // Send the friends request using the FriendManager
                await friendManager.MakeFriendAction(FriendAction.SendRequest, requestFriendshipInputfield.text);

                // Provide user feedback that the request was sent
                if (promptFadeController)
                    promptFadeController.Fade($"Friendship request was sent to {requestFriendshipInputfield.text}", 3f);

                // Clear the input field after sending the request
                requestFriendshipInputfield.text = string.Empty;
            }

            // Search users by partial name using Firebase Realtime Database
            async UniTask FirebasePartialSearch()
            {
                // Validate search user entries parent and prefab
                if (!searchUserEntriesParent || !searchUserEntryPrefab)
                {
                    Debug.LogWarning("Cannot search users because the search user entries parent or prefab is not assigned");
                    return;
                }

                // Let a running avatar lookup finish, the search system rejects overlapping requests
                RtdbUserData[] rawResults;
                isUserSearchPending = true;
                try
                {
                    await UniTask.WaitWhile(() => isResolvingProfileIcons);
                    rawResults = await gameManager.TryToSearchPlayersByName(requestFriendshipInputfield.text);
                }
                finally
                {
                    isUserSearchPending = false;
                }

                CacheProfileIcons(rawResults);

                // Search for users matching the input
                var searchResults = rawResults
                    ?.Where(x =>
                        x.userId != authManager.UUID // Ignore self
                        && (!FriendsEntryDatas?.Any(y => y.TargetID == x.userId) ?? true)) // Ignore already friends
                    ?.ToArray(); 

                // Provide user feedback that the request was sent
                if (promptFadeController)
                    promptFadeController.Fade($"Searching players with name <b>{requestFriendshipInputfield.text}</b>", 3f);

                await ShowPlayerEntries(searchResults);

                if (searchResults is null or { Length: 0 })
                    Debug.LogWarning("No users found matching the search criteria.");

                isShowingSearchResults = true;
                hasSearchResults = searchResults is not null and { Length: > 0 };
                ApplyScreenState();
            }
        }

        /// <summary>
        /// Fills the player cards section with the given players, reusing the existing instances.
        /// </summary>
        private async UniTask ShowPlayerEntries(RtdbUserData[] players)
        {
            if (!searchUserEntriesParent || !searchUserEntryPrefab)
                return;

            var leftingInstances = players?.Length - (searchUserEntryInstances?.Count ?? 0) ?? 0;
            if (leftingInstances > 0)
                for (var i = 0; i < leftingInstances; i++)
                {
                    var newEntry = Instantiate(searchUserEntryPrefab, searchUserEntriesParent);
                    newEntry.Initialize(OnInviteFriendToPlay);

                    searchUserEntryInstances ??= new();
                    searchUserEntryInstances.Add(newEntry);
                }

            // Clear previous entries
            searchUserEntryInstances?.ForEach(x => x.gameObject.SetActive(false));

            if (players is null or { Length: 0 })
                return;

            // Wait until all entries are configured before allowing interactions, to ensure a smooth user experience without partial data shown
            await UniTask.WhenAll(players.Select((player, i) => UniTask.Create(async () =>
            {
                var entryInstance = searchUserEntryInstances[i];

                await entryInstance.Configure(player);
                entryInstance.gameObject.SetActive(true);
            })));

            searchUserEntriesParent.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Shows the recently played players that are not friends yet, so they can be added from the empty state.
        /// </summary>
        private async UniTask ShowRecentlyPlayedPlayers()
        {
            if (!UsesScreenStates)
                return;

            var recentlyPlayed = gameManager?.RecentlyPlayedDatas
                ?.Where(x => x != null
                    && !string.IsNullOrEmpty(x.id)
                    && x.id != authManager?.UUID // Ignore self
                    && (!FriendsEntryDatas?.Any(y => y.TargetID == x.id) ?? true)) // Ignore already friends
                ?.GroupBy(x => x.id)
                ?.Select(x => x.OrderByDescending(y => y.playedTime ?? 0).First())
                ?.OrderByDescending(x => x.playedTime ?? 0)
                ?.Select(x => new RtdbUserData
                {
                    userId = x.id,
                    displayName = x.playerName
                })
                ?.ToArray();

            hasRecentlyPlayedPlayers = recentlyPlayed is not null and { Length: > 0 };

            // A search started meanwhile owns the player cards
            if (isShowingSearchResults)
                return;

            await ShowPlayerEntries(recentlyPlayed);
        }

        // Player id -> profile icon id, filled from name searches; empty string means "looked up, not found"
        private readonly Dictionary<string, string> profileIconIdsCache = new();
        private bool isResolvingProfileIcons;
        private bool isUserSearchPending;

        /// <summary>
        /// Finds a player's profile icon id through the existing name search (the friends service carries no icon).
        /// Lookups run one by one because the search system rejects overlapping requests.
        /// </summary>
        private async UniTask<string> ResolveProfileIconId(string playerId, string playerName)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(playerName) || !gameManager)
                return null;

            if (profileIconIdsCache.TryGetValue(playerId, out var cached))
                return cached;

            // Friend names carry a "#1234" tag that is not part of the searchable display name
            var tagIndex = playerName.IndexOf('#');
            if (tagIndex > 0)
                playerName = playerName.Substring(0, tagIndex);

            var results = await gameManager.TryToSearchPlayersByName(playerName);

            // A rejected request (search already running) returns null: leave it uncached to retry later
            if (results is null)
                return null;

            CacheProfileIcons(results);
            return profileIconIdsCache.TryGetValue(playerId, out var iconId)
                ? iconId
                : profileIconIdsCache[playerId] = string.Empty;
        }

        private void CacheProfileIcons(IEnumerable<RtdbUserData> players)
        {
            if (players is null)
                return;

            foreach (var player in players)
                if (player != null && !string.IsNullOrEmpty(player.userId) && !string.IsNullOrEmpty(player.profileIconID))
                    profileIconIdsCache[player.userId] = player.profileIconID;
        }

        /// <summary>
        /// Loads the profile icons of the visible friend cards and recently played cards.
        /// </summary>
        private async UniTaskVoid LoadProfileIcons()
        {
            if (!UsesScreenStates || isResolvingProfileIcons || !gameManager)
                return;

            isResolvingProfileIcons = true;
            try
            {
                var friendEntries = friendEntriesInstances
                    ?.Where(x => x && x.gameObject.activeSelf && x.HasAvatar && x.FriendsEntryData.HasValue)
                    .ToArray() ?? Array.Empty<FriendEntry>();

                foreach (var entry in friendEntries)
                {
                    // The player's own search has priority over background lookups
                    if (isUserSearchPending)
                        return;

                    var data = entry.FriendsEntryData.Value;
                    var iconId = await ResolveProfileIconId(data.TargetID, data.Name);
                    if (string.IsNullOrEmpty(iconId))
                        continue;

                    var sprite = await gameManager.GetSpriteAsync(iconId, Consts.CollectionKeys.Icons);

                    // Skip cards reused for another friend meanwhile
                    if (sprite && entry && entry.FriendsEntryData?.TargetID == data.TargetID)
                        entry.SetAvatar(sprite);
                }

                if (isShowingSearchResults)
                    return;

                var playerEntries = searchUserEntryInstances
                    ?.Where(x => x && x.gameObject.activeSelf && x.SearchedUserData != null && string.IsNullOrEmpty(x.SearchedUserData.profileIconID))
                    .ToArray() ?? Array.Empty<SearchUserEntry>();

                foreach (var entry in playerEntries)
                {
                    if (isUserSearchPending || isShowingSearchResults)
                        return;

                    var data = entry.SearchedUserData;
                    var iconId = await ResolveProfileIconId(data.userId, data.displayName);
                    if (!string.IsNullOrEmpty(iconId) && entry.SearchedUserData == data)
                        await entry.SetPlayerIcon(iconId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(PartyController)}] Couldn't load profile icons: {e.Message}");
            }
            finally
            {
                isResolvingProfileIcons = false;
            }
        }

        /// <summary>
        /// Leaves the search results once the search field is cleared.
        /// </summary>
        private void OnSearchFieldSelected(string _)
        {
            if (isSearchFieldFocused)
                return;

            isSearchFieldFocused = true;
            ApplyScreenState();
        }

        /// <summary>
        /// Leaving an empty search bar goes back to the friends list.
        /// </summary>
        private void OnSearchFieldDeselected(string text)
        {
            if (!isSearchFieldFocused || !string.IsNullOrWhiteSpace(text))
                return;

            isSearchFieldFocused = false;
            ApplyScreenState();
        }

        private async void OnSearchTextChanged(string text)
        {
            if (!isShowingSearchResults || !string.IsNullOrWhiteSpace(text))
                return;

            isShowingSearchResults = false;
            hasSearchResults = false;
            await ShowRecentlyPlayedPlayers();
            ApplyScreenState();
            LoadProfileIcons().Forget();
        }

        /// <summary>
        /// Switches between the empty, recently played, friends and search layouts of the screen.
        /// </summary>
        private void ApplyScreenState()
        {
            var hasAnyFriend = (FriendsEntryDatas?.Length ?? 0) > 0;

            // Layouts without the screen states (party panel) only toggle the empty label and search results
            if (!UsesScreenStates)
            {
                if (emptyFriendlistLabel)
                    emptyFriendlistLabel.SetActive(!hasAnyFriend);

                if (searchUsersCanvasGroup)
                    searchUsersCanvasGroup.SetActive(isShowingSearchResults && hasSearchResults);
                return;
            }

            // Focusing the search bar or showing results switches to the search layout: the empty-state
            // header with the centered bar, and the results listed under it instead of the friends grid
            var isSearchMode = isShowingSearchResults || isSearchFieldFocused;
            var isPlayerNotFound = isShowingSearchResults && !hasSearchResults;
            var isShowingRecentlyPlayed = !hasAnyFriend && !isShowingSearchResults && hasRecentlyPlayedPlayers;

            if (emptyFriendlistLabel)
                emptyFriendlistLabel.SetActive((!hasAnyFriend || isSearchMode) && !isPlayerNotFound);

            if (firstMatchHint)
                firstMatchHint.SetActive(!hasAnyFriend && !isSearchMode && !hasRecentlyPlayedPlayers);

            if (friendsSection)
                friendsSection.SetActive(hasAnyFriend && !isSearchMode);

            if (playerNotFoundState)
                playerNotFoundState.SetActive(isPlayerNotFound);

            if (playersSectionTitle)
                playersSectionTitle.text = isShowingSearchResults ? searchResultsTitle : recentlyPlayedTitle;

            var isShowingPlayers = (isShowingSearchResults && hasSearchResults) || isShowingRecentlyPlayed;

            if (searchUsersCanvasGroup)
                searchUsersCanvasGroup.SetActive(isShowingPlayers);

            // Deactivate the section too, so a hidden section takes no room in the layout
            if (playersSection)
                playersSection.SetActive(isShowingPlayers);

            // Zero flexible width keeps the row from inheriting the input field's flexible width
            if (searchRowLayout)
            {
                var isFullWidth = (hasAnyFriend && !isSearchMode) || isPlayerNotFound;
                searchRowLayout.preferredWidth = isFullWidth ? -1f : emptyStateSearchRowWidth;
                searchRowLayout.flexibleWidth = isFullWidth ? 1f : 0f;
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Initializes the controller after the FriendManager has been fully set up, and subscribes to friends action listeners.
        /// </summary>
        internal async void Start_FriendListController()
        {
            if (!gameManager || !friendManager)
            {
                Debug.LogErrorFormat("Could not initialize the controller due {GameManager} or {FriendManager} is null", nameof(GameManager), nameof(FriendManager));
                return;
            }

            gameManager.HandleOnSignIn(ConfigureUI);
            gameManager.HandleOnSignOut(ConfigureUI);

            // By default, hide all friends entries
            friendEntriesInstances.ForEach(x => x?.gameObject.SetActive(false));
            orderByToggleGroup?.SetOnCustomButtonSelectedCallback(OrderPlayersBy);

            await UniTask.WaitUntil(() => gameManager.IsAlreadyInitialized && friendManager.IsAlreadyInitialized);

            friendManager.RegisterFriendEvent
                ((FriendAction.SendRequest, OnSendFriendRequest),
                (FriendAction.AcceptRequest, OnAcceptFriendRequest),
                (FriendAction.DeclineRequest, OnDeclineFriendRequest),
                (FriendAction.Block, OnBlockFriend),
                (FriendAction.Unblock, OnUnblockFriend));

            ConfigureUI();
        }

        /// <summary>
        /// Unregisters event handlers related to friend actions and sign-in/sign-out events when the
        /// FriendListController is destroyed.
        /// </summary>
        private void OnDestroy_FriendListController()
        {
            if (!gameManager || !friendManager)
                return;

            gameManager.UnHandleOnSignIn(ConfigureUI);
            gameManager.UnHandleOnSignOut(ConfigureUI);

            friendManager.UnregisterFriendEvent
                ((FriendAction.SendRequest, OnSendFriendRequest),
                (FriendAction.AcceptRequest, OnAcceptFriendRequest),
                (FriendAction.DeclineRequest, OnDeclineFriendRequest),
                (FriendAction.Block, OnBlockFriend),
                (FriendAction.Unblock, OnUnblockFriend));
        }

        /// <summary>
        /// Updates the UI and entries based on current friends and request data.
        /// </summary>
        private void ConfigureUI()
        {
            // Filter the notifications based on the current view
            // Try use button toggle group to select the current view and update the list accordingly
            var buttonId = IsViewingFriendList ? FriendList : RecentlyPlayed;
            if (orderByToggleGroup && !orderByToggleGroup.CheckIfSelected(buttonId))
                orderByToggleGroup.GetButtonUI(buttonId)?.Select();

            // Else, directly order the players
            else
                OrderPlayersBy(buttonId);

            if (friendsCountLabel)
            {
                var friendsAndRequestCount = FriendsEntryDatas?.Length ?? 0;
                friendsCountLabel.text = $"{friendsAndRequestCount}/{friendManager?.FriendsConfigData?.friendshipsLimit ?? 50}";
            }

            // Legacy layouts hide the search results each time the UI is configured
            if (!UsesScreenStates)
                hasSearchResults = isShowingSearchResults = false;

            // Show the empty, friends or search layout based on the current data
            ApplyScreenState();
        }

        /// <summary>
        /// Drops the current search, going back to the friends list or the empty state.
        /// </summary>
        private void ResetSearch()
        {
            isShowingSearchResults = false;
            hasSearchResults = false;
            isSearchFieldFocused = false;

            if (requestFriendshipInputfield)
                requestFriendshipInputfield.SetTextWithoutNotify(string.Empty);
        }

        /// <summary>
        /// Sets the visibility of the friends list UI by enabling or disabling the CanvasGroup component.
        /// </summary>
        /// <param name="isVisible">Whether the UI should be visible.</param>
        public void SetVisibility(bool isVisible)
        {
            if (!friendListCanvasGroup)
            {
                Debug.LogWarning($"Cannot set visibility, CanvasGroup is not assigned.");
                return;
            }

            friendListCanvasGroup.SetActive(isVisible);

            if (isVisible)
            {
                ResetSearch();
                ConfigureUI();
            }

            OnFriendListVisibilityChanged?.Invoke(isVisible);
        }

        /// <summary>
        /// Raised when the friend list popup is shown or hidden.
        /// </summary>
        public event Action<bool> OnFriendListVisibilityChanged;

        /// <summary>
        /// Sets the block of the friends list UI by enabling or disabling the CanvasGroup component.
        /// </summary>
        /// <param name="IsInteractable">Whether the UI should be visible.</param>
        public void SetInteractivity(bool IsInteractable)
        {
            if (!friendListCanvasGroup)
            {
                Debug.LogWarning($"Cannot set block, CanvasGroup is not assigned.");
                return;
            }

            friendListCanvasGroup.SetActive(IsInteractable, isSettingAlpha: false);
        }

        /// <summary>
        /// Updates the list of visual instances of friends entries, ordering them by availability
        /// and reusing instances to avoid unnecessary instantiations.
        /// </summary>
        /// <param name="isIncludingRecentlyPlayedUnknownPlayers">Whether to include recently played unknown players in the list.</param>
        private async UniTask UpdateInstances(bool isIncludingRecentlyPlayedUnknownPlayers)
        {
            if (!gameManager || !friendManager)
            {
                Debug.LogErrorFormat("Could not update instances due {GameManager} or {FriendManager} is null", nameof(GameManager), nameof(FriendManager));
                return;
            }

            // Refresh friends requests to ensure the latest data
            // This could be optimized to avoid multiple calls if UpdateInstances is called frequently
            await friendManager.RefreshAll();

            SetInteractivity(false);
            try
            {
                await gameManager.RefreshPublicPlayerData();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                if (friendListCanvasGroup.alpha is 0)
                    SetVisibility(false);
                else
                    SetInteractivity(true);
            }

            // Order according the friends availability
            var friendsEntryDatas = FriendsEntryDatas
                ?.OrderByDescending(x => x.Availability)
                ?.ToList();

            // According the recent player (excluding the ones already registered in the other list) create a list of entries with them
            if (isIncludingRecentlyPlayedUnknownPlayers)
            { 
                var recentlyPlayed = gameManager.RecentlyPlayedDatas;
                var recentlyUnknownPlayed = recentlyPlayed
                    ?.Where(x => friendsEntryDatas?.Any(y => y.TargetID == x.id) ?? true)
                    ?.Select(x => new FriendsEntryData( 
                        senderID: authManager.UUID,
                        targetID: x.id,
                        name: x.playerName,
                        activity: "Unknown",
                        availability: Availability.Unknown,
                        timestamp: ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()))
                    ?.ToArray();

                // Register the unknown new player in the list of the players to show
                if (recentlyUnknownPlayed is not null)
                    friendsEntryDatas.AddRange(recentlyUnknownPlayed);
            }

            // Remove duplicated entries selecting the most recent one
            // (UUID is a creation timestamp shared by every entry built in the same second, so dedupe by player)
            friendsEntryDatas = friendsEntryDatas
                ?.GroupBy(x => x.TargetID)
                ?.Select(x => x.OrderByDescending(y => y.Timestamp).First())
                ?.ToList();

            // Ensure we have enough instances to display all entries
            if (friendsEntryDatas is not null and { Count: > 0 })
            { 
                friendEntriesInstances ??= new();
                for (var i = 0; i < friendsEntryDatas.Count; i++)
                    if (i >= friendEntriesInstances.Count)
                    {
                        var newInstance = Instantiate(friendEntryPrefab, friendEntriesParent);
                        newInstance.Initialize
                            (InviteFriendToPlay, 
                            RemoveFriendFromList,
                            () => PartyMembers);
                        friendEntriesInstances.Add(newInstance);
                    }
                    else if (friendEntriesInstances[i] == null)
                    {
                        var newInstance = Instantiate(friendEntryPrefab, friendEntriesParent);
                        newInstance.Initialize
                            (InviteFriendToPlay, 
                            RemoveFriendFromList,
                            () => PartyMembers);
                        friendEntriesInstances[i] = newInstance;
                    }
            }
            else
                Debug.LogWarning("Couldn't create friends entry instances. Friends entry data collection is null or empty.");


            // Deactivate all current instances
            friendEntriesInstances?.ForEach(instance => instance?.SetActive(false));

            // Configure and activate only those matching the criteria
            if (friendsEntryDatas is not null and { Count: > 0 })
                foreach (var friendEntryData in friendsEntryDatas)
                {
                    if (friendEntryData.Availability is Availability.Invisible or Availability.Away)
                        continue;

                    var instance = friendEntriesInstances?.FirstOrDefault(i => i != null && !i.gameObject.activeSelf);
                    if (instance != null)
                    {
                        instance.Configure(friendEntryData, categoriesShown);
                        instance.SetActive(true);
                    }
                }
            else
                Debug.LogWarning("Couldn't configure friends entries. Friends entry data collection is null or empty.");

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Orders the friends entries based on the player's recently played list from GameManager.
        /// Only displays entries that are part of that list.
        /// </summary>
        /// <param name="toggleId">The ID of the toggle used to determine the ordering criteria.</param>
        private async void OrderPlayersBy(string toggleId)
        {
            if (!gameManager)
            {
                Debug.LogErrorFormat("Could not order due {GameManager} is null", nameof(GameManager), nameof(FriendManager));
                return;
            }

            IsViewingFriendList = toggleId is FriendList;

            await UpdateInstances(!IsViewingFriendList);

            if (friendEntriesInstances is not null and { Count: > 0 })
            {
                var orderedFriendEntries = default(List<FriendEntry>);

                var instancesToFilter = friendEntriesInstances
                    .Where(x => x.gameObject.activeSelf && x.FriendsEntryData.HasValue)
                    .ToList();

                var friendEntryDict = instancesToFilter.ToDictionary(entry => entry.FriendsEntryData?.TargetID);

                if (IsViewingFriendList)
                {
                    var friends = await friendManager.GetFriends();
                    orderedFriendEntries = friendEntryDict
                        ?.Where(x => friends?.Any(y => y.Id == x.Key) ?? false)
                        ?.Select(x => x.Value)
                        ?.OrderBy(x => x.FriendsEntryData?.Name)
                        ?.ToList() ?? new();
                } 
                else
                { 
                    var recentlyPlayed = gameManager.RecentlyPlayedDatas;
                    orderedFriendEntries = recentlyPlayed
                        ?.Where(x => friendEntryDict.ContainsKey(x.id))
                        ?.Select(x => friendEntryDict[x.id])
                        ?.OrderBy(x => x.FriendsEntryData?.Timestamp)
                        ?.ToList() ?? new();

                }

                if (orderedFriendEntries is null)
                {
                    Debug.LogWarning("Couldn't order by recently played. Collection is null or empty");
                    return;
                }

                friendEntriesInstances.ForEach(instance => instance.SetActive(false));

                foreach (var friendEntry in orderedFriendEntries)
                {
                    friendEntry.SetActive(true);
                    friendEntry.transform.SetAsLastSibling();
                }

                transform.RefreshLayoutGroupsImmediateAndRecursive();
            }

            // Data is fresh now: suggest recently played players and pick the layout
            await ShowRecentlyPlayedPlayers();
            ApplyScreenState();
            LoadProfileIcons().Forget();
        }

        /// <summary>
        /// Removes a friends from the list by sending the appropriate friends action to the backend.
        /// </summary>
        /// <param name="playerEntryData">The friends's data to remove.</param>
        private async void RemoveFriendFromList(FriendsEntryData? playerEntryData)
        {
            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning("Cannot remove friends from list because the friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning("Cannot remove friends from list because the friends manager is null or not initialized");
                    return;
                }
            }

            if (!playerEntryData.HasValue)
            {
                Debug.LogWarning("Couldn't remove friends from list because the player entry data is null");
                return;
            }

            SetInteractivity(false);
            try
            {
                await friendManager.MakeFriendAction(FriendAction.RemoveFriend, playerEntryData?.TargetID);

                // Provide user feedback that the request was sent
                if (promptFadeController)
                    promptFadeController.Fade($"Friendship with {playerEntryData.Value.Name} was removed", 3f);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                if (friendListCanvasGroup.alpha is 0)
                    SetVisibility(false);
                else
                    SetInteractivity(true);
            }

            ConfigureUI();
        }

        /// <summary>
        /// Sends a friends request to the given player ID.
        /// </summary>
        /// <param name="playerID">The ID of the player to send a request to.</param>
        private async void OnSendFriendRequest(string playerID)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                Debug.LogWarning($"Couldn't send friends request because player id is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"Cannot send friends request to {playerID} because friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"Cannot send friends request to {playerID} because friends manager is null or not initialized");
                    return;
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Accepts a friends request from the given player ID.
        /// </summary>
        /// <param name="playerID">The ID of the player whose request is accepted.</param>
        private async void OnAcceptFriendRequest(string playerID)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                Debug.LogWarning($"Couldn't accept friends request because player id is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"Cannot accept friends request from {playerID} because friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"Cannot accept friends request from {playerID} because friends manager is null or not initialized");
                    return;
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Declines a friends request from the given player ID.
        /// </summary>
        /// <param name="playerID">The ID of the player whose request is declined.</param>
        private async void OnDeclineFriendRequest(string playerID)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                Debug.LogWarning($"Couldn't decline friends request because player id is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"Cannot decline friends request from {playerID} because friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"Cannot decline friends request from {playerID} because friends manager is null or not initialized");
                    return;
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Blocks a friends with the specified player ID.
        /// TODO: this method is not used due is not defined in the mocks nor in the GDD
        /// </summary>
        /// <param name="playerID">The ID of the player to block.</param>
        [Obsolete]
        private async void OnBlockFriend(string playerID)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                Debug.LogWarning($"Couldn't block a player because its player id is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"Cannot block {playerID} because friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"Cannot block {playerID} because friends manager is null or not initialized");
                    return;
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Unblocks a previously blocked friends with the specified player ID.
        /// TODO: this method is not used due is not defined in the mocks nor in the GDD
        /// </summary>
        /// <param name="playerID">The ID of the player to unblock.</param>
        [Obsolete]
        private async void OnUnblockFriend(string playerID)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                Debug.LogWarning($"Couldn't unblock a player because its player id is null or empty");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"Cannot unblock {playerID} because friends manager is not initialized after trying to get the friends service");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"Cannot unblock {playerID} because friends manager is null or not initialized");
                    return;
                }
            }

            ConfigureUI();
        }

        /// <summary>
        /// Invites a friend to play by sending a friends request to the specified player ID.
        /// </summary>
        /// <param name="rtdbUserData">The user data of the friend to invite.</param>
        private async UniTask OnInviteFriendToPlay(RtdbUserData rtdbUserData)
        {
            // Check for null references
            if (rtdbUserData is null)
            { 
                Debug.LogWarning("Cannot send a friends request because the player data is null");
                return;
            }

            if (string.IsNullOrEmpty(rtdbUserData.userId))
            {
                Debug.LogWarning("Cannot send a friends request because the player ID is null or empty");
                return;
            }

            searchUsersCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                await friendManager.MakeFriendAction(FriendAction.SendRequest, rtdbUserData.userId);

                // Provide user feedback that the request was sent
                if (promptFadeController)
                    promptFadeController.Fade($"Friendship request was sent to {rtdbUserData.displayName}", 3f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Couldn't send a friends request to {rtdbUserData.userId}:\n\n{ex.Message}");

                // Let the entry know the request failed so it keeps offering the action
                throw;
            }
            finally
            {
                searchUsersCanvasGroup?.SetActive(true, isSettingAlpha: false);
            }
        }

        /// <summary>
        /// Defines which categories should be shown for each friends entry in the UI.
        /// Flags allow multiple categories to be displayed simultaneously.
        /// </summary>
        [Flags]
        public enum CategoriesShown
        {
            None = 0,
            Name = 1 << 0,
            ID = 1 << 1,
            Status = 1 << 2,
            Invite = 1 << 3,
            Remove = 1 << 4,
            All = Name | ID | Status | Invite | Remove
        }
    }
}
