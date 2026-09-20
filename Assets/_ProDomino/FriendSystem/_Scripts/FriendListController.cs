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

#if UNITY_EDITOR
        [Header("Editor Tests")]
        [SerializeField] private bool sendInvite;
        [SerializeField] private string testPlayerIDToInvite;
        [SerializeField] private string testPlayerNameToInvite;
#endif

        private List<FriendEntry> friendEntriesInstances;
        private List<SearchUserEntry> searchUserEntryInstances;
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

            if (closePopUp)
                closePopUp.onClick.AddListener(() => SetVisibility(false));
            else
                Debug.LogWarning("Close popUp reference is null. Make sure the reference is set in the inspector");

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

                // Search for users matching the input
                var searchResults = (await gameManager.TryToSearchPlayersByName(requestFriendshipInputfield.text))
                    ?.Where(x =>
                        x.userId != authManager.UUID // Ignore self
                        && (!FriendsEntryDatas?.Any(y => y.UUID == x.userId) ?? true)) // Ignore already friends
                    ?.ToArray(); 

                // Provide user feedback that the request was sent
                if (promptFadeController)
                    promptFadeController.Fade($"Searching players with name <b>{requestFriendshipInputfield.text}</b>", 3f);

                var leftingInstances = searchResults?.Length - (searchUserEntryInstances?.Count ?? 0) ?? 0;
                if (leftingInstances > 0)
                    for (var i = 0; i < leftingInstances; i++)
                    {
                        var newEntry = Instantiate(searchUserEntryPrefab, searchUserEntriesParent);
                        newEntry.Initialize(OnInviteFriendToPlay);

                        searchUserEntryInstances ??= new();
                        searchUserEntryInstances.Add(newEntry);
                    }
                
                // Clear previous search entries
                searchUserEntryInstances?.ForEach(x => x.gameObject.SetActive(false));

                // Create new search entries for each result
                if (searchResults is not null and { Length: > 0 })
                {
                    var configureEntriesTasks = searchResults.Select((x, i) => UniTask.Create(async () =>
                    {
                        var result = searchResults[i];
                        var entryInstance = searchUserEntryInstances[i];

                        await entryInstance.Configure(result);
                        entryInstance.gameObject.SetActive(true);

                    }));

                    // Wait until all entries are configured before allowing interactions, to ensure a smooth user experience without partial data shown
                    if (configureEntriesTasks is not null)
                        await UniTask.WhenAll(configureEntriesTasks);
                }
                else
                    Debug.LogWarning("No users found matching the search criteria.");

                // Show or hide the search users canvas group based on results
                if (searchUsersCanvasGroup)
                    searchUsersCanvasGroup.SetActive(searchResults is not null and { Length: > 0 });
            }
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

            // Show or hide the "empty friendlist" label based on total friends count
            if (emptyFriendlistLabel)
            {
                var hasAnyFriend = (FriendsEntryDatas?.Length ?? 0) > 0;

                emptyFriendlistLabel.gameObject.SetActive(!hasAnyFriend);
            }

            // By default, hide the search users canvas group each time we configure the UI
            if (searchUsersCanvasGroup)
                searchUsersCanvasGroup.SetActive(false);
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
                ConfigureUI();
        }

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
            friendsEntryDatas = friendsEntryDatas
                ?.GroupBy(x => x.UUID)
                ?.Select(x => x.OrderByDescending(y => y.Timestamp))
                ?.FirstOrDefault()
                ?.ToList();

            // Ensure we have enough instances to display all entries
            if (friendsEntryDatas is not null and { Count: > 0 })
            { 
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
            }
            else
                Debug.LogWarning("Couldn't create friends entry instances. Friends entry data collection is null or empty.");


            // Deactivate all current instances
            friendEntriesInstances.ForEach(instance => instance.SetActive(false));

            // Configure and activate only those matching the criteria
            if (friendsEntryDatas is not null and { Count: > 0 })
                foreach (var friendEntryData in friendsEntryDatas)
                {
                    if (friendEntryData.Availability is Availability.Invisible or Availability.Away)
                        continue;

                    var instance = friendEntriesInstances?.FirstOrDefault(i => !i.gameObject.activeSelf);
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
