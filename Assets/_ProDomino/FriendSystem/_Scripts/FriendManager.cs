using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timba.Patterns;
using Timba.Utils;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.Enums;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Manages friend-related actions such as sending/receiving requests, blocking/unblocking users,
    /// listening to presence updates, and refreshing the friends list.
    /// </summary>
    public class FriendManager : SingleInstanceMonoBehaviour<FriendManager>, IService
    {
        [SerializeField] private UnityEvent<FriendAction> onFriendActionMade;
        [SerializeField] private ConfirmFriendshipPopUp confirmFriendshipPopUp;
        [SerializeField] private PartyController partyController;
        [SerializeField] private float offlineDelaySeconds = 60;

        [Header("Friend Event Callbacks")]
        [SerializeField] private UnityEvent<string> onBeforeSendFriendRequest;
        [SerializeField] private UnityEvent<string> onFriendRequestSent;
        [SerializeField] private UnityEvent<string> onAcceptRequest;
        [SerializeField] private UnityEvent<string> onDeclineRequest;
        [SerializeField] private UnityEvent<string> onBlockFriend;
        [SerializeField] private UnityEvent<string> onUnblockFriend;
        [SerializeField] private UnityEvent<string> onRemoveFriend;

        [Header("UGS Friend Service Event Callback")]
        [SerializeField] private UnityEvent<IRelationshipAddedEvent> RelationshipAdded;
        [SerializeField] private UnityEvent<IMessageReceivedEvent> MessageReceived;
        [SerializeField] private UnityEvent<IPresenceUpdatedEvent> PresenceUpdated;
        [SerializeField] private UnityEvent<IRelationshipDeletedEvent> RelationshipDeleted;
        [SerializeField] private UnityEvent<INotificationsStateChangedEvent> NotificationsConnectivityChanged;

        [Header("Dummy Test")]
        public Button testButton;

        // Internal data structures to track friends, requests, and blocked users
        private FriendsEventConnectionState currentFriendsEventConnectionState;

        public List<FriendsEntryData> FriendsEntryDatas;
        public List<FriendsEntryData> RequestsEntryDatas;
        public List<FriendsEntryData> BlockEntryDatas;

        private AuthManager authManager;
        private GameManager gameManager;
        private Coroutine offlineCoroutine;
        private (Availability availability, Activity activity)? lastPresenceSet;

        public bool IsAlreadyInitialized { get; private set; }
        public FriendsConfig FriendsConfigData => gameManager?.GameBackendConfigData?.friendsConfig;

        // Special events
        public ref UnityEvent<string> OnBeforeSendFriendRequest => ref onBeforeSendFriendRequest;
        public ref UnityEvent<string> OnFriendRequestSent => ref onFriendRequestSent;

        /// <summary>
        /// Indicates whether the Friends Service is initialized and ready for use.
        /// </summary>
        public bool IsFriendsServiceReady 
        { 
            get {
                // Try to get the current friend service, if it fails try to initialize it
                try
                {
                    return FriendsService.Instance.Friends is not null;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"FriendsService is not initialized: {ex}");
                    return false;
                }
            } 
        } 

        protected override void Awake()
        {
            base.Awake();

            FriendsEntryDatas = new();
            RequestsEntryDatas = new();
            BlockEntryDatas = new();

            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
        }

        private async void Start()
        {
            // Wait until authentication and game managers are fully initialized
            await UniTask.WaitUntil(() => gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true });

            // MainReferenceInitialize the unity friend service
            await FriendsService.Instance.InitializeAsync();          
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true })
            {
                if (!pauseStatus)
                {
                    // Resumed
                    if (offlineCoroutine != null)
                    {
                        StopCoroutine(offlineCoroutine);
                        offlineCoroutine = null;
                    }

                    SetPresence(Availability.Online, "Back from Pause").Forget();
                } 
                else
                {
                    // Paused
                    if (offlineCoroutine == null)
                        offlineCoroutine = StartCoroutine(DelayedOffline(Availability.Offline, "Paused/Background"));
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true })
            {
                if (hasFocus)
                {
                    // Player is back: cancel any pending offline
                    if (offlineCoroutine != null)
                    {
                        StopCoroutine(offlineCoroutine);
                        offlineCoroutine = null;
                    }

                    SetPresence(Availability.Online, "Active Again").Forget();
                } 
                else
                {
                    // Player lost focus: start delay before going offline
                    if (offlineCoroutine == null)
                        offlineCoroutine = StartCoroutine(DelayedOffline(Availability.Offline, "Tab Unfocused"));
                }
            }
        }

#if !UNITY_WEBGL

        private void OnDestroy()
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true })
                SetPresence(Availability.Offline, "Disconnected").Forget();
        }

        private void OnApplicationQuit()
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true })
                SetPresence(Availability.Offline, "Disconnected").Forget();
        }
#endif

        /// <summary>
        /// Handles cleanup by setting the user's presence to offline and marking as disconnected if the game manager is
        /// initialized and authenticated.
        /// </summary>
        /// <param name="isTryingToInitializeOnFail"></param>
        /// <returns></returns>
        internal async UniTask<IFriendsService> GetFriendsServiceSafeAsync(bool isTryingToInitializeOnFail = true)
        {
            if (gameManager is null or { IsAlreadyInitialized: false } or { IsAuthenticated: false })
            {
                Debug.LogWarning("Couldn't get the FriendsService because the user is not authenticated");
                return null;
            }

            // Always grab the singleton
            var isFriendServicesInitialized = false;

            // Try to get the current friend service, if it fails try to initialize it
            try
            {
                isFriendServicesInitialized = FriendsService.Instance.Friends != null;
                if (!isFriendServicesInitialized)
                    await TryToInitialize();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FriendsService is not initialized: {ex}\n\nTrying to re-initialize...");
                if (isTryingToInitializeOnFail)
                    await TryToInitialize();
            }
           
            return FriendsService.Instance;

            /// Tries to initialize the FriendsService and set the presence to online. This is called when trying to access the service and it's not initialized yet.
            async Task TryToInitialize()
            {
                // Check if already initialized
                try
                {
                    await FriendsService.Instance.InitializeAsync();

                    if (!IsAlreadyInitialized)
                    {
                        IsAlreadyInitialized = true;

                        // Subscribe to Friends SDK events after initializing
                        await RegisterFriendsEventCallbacks();
                    }

                    await SetAsOnline();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"FriendsService.InitializeAsync failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Registers event callbacks for friends-related events such as message received, relationship changes,
        /// presence updates, and notifications connectivity changes.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous registration operation.</returns>
        private async UniTask RegisterFriendsEventCallbacks()
        {
            try
            {
                var friendsService = await GetFriendsServiceSafeAsync();
                if (friendsService is null || !IsFriendsServiceReady)
                {
                    Debug.LogWarning("FriendsService is null. Couldn't register event callbacks.");
                    return;
                }

                friendsService.MessageReceived += async e =>
                {
                    await RefreshAll();

                    MessageReceived?.Invoke(e);
                    Debug.Log("MessageReceived EventReceived");
                };

                friendsService.RelationshipAdded += async e =>
                {
                    await RefreshAll();

                    RelationshipAdded?.Invoke(e);
                    Debug.Log($"create {e.Relationship} EventReceived");
                };

                friendsService.RelationshipDeleted += async e =>
                {
                    await RefreshAll();

                    RelationshipDeleted?.Invoke(e);
                    Debug.Log($"Delete {e.Relationship} EventReceived");
                };

                friendsService.PresenceUpdated += async e =>
                {
                    await RefreshAll();

                    PresenceUpdated?.Invoke(e);
                    Debug.Log("PresenceUpdated EventReceived");
                };

                friendsService.NotificationsConnectivityChanged += async e =>
                {
                    // Log current change
                    Debug.Log($"Change of state in notification system from {currentFriendsEventConnectionState} to {e.State}");

                    // If the subscription was lost, mark as offline
                    if (e.State is FriendsEventConnectionState.Unsynced)
                    {
                        await SetPresence(Availability.Offline, "Connectivity Problems");

                        // Intentar reconectar
                        Debug.Log("Attempting to reconnect FriendsService...");
                        try
                        {
                            var friendsService = await GetFriendsServiceSafeAsync();
                            if (friendsService is null || !IsFriendsServiceReady)
                            {
                                Debug.LogWarning("FriendsService is null. Couldn't reconnect.");
                                return;
                            }

                            await SetPresence(Availability.Online, "Back Online");
                            await RefreshAll();
                            Debug.Log("Reconnection successful.");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Reconnection failed: {ex}");
                        }
                    }

                    // If the connection was restored, set presence to online
                    else if (currentFriendsEventConnectionState == FriendsEventConnectionState.Unsynced && e.State == FriendsEventConnectionState.Subscribed)
                        await SetPresence(Availability.Online, "Back Online");

                    currentFriendsEventConnectionState = e.State;
                    NotificationsConnectivityChanged?.Invoke(e);
                };
            }
            catch (FriendsServiceException e)
            {
                Debug.Log(
                    "An error occurred while performing the action. HttpCode: " + e.StatusCode + ", FriendsErrorCode: " + e.ErrorCode + ", Message: " + e.Message);
            }
        }

        /// <summary>
        /// Sets the user's presence to online, refreshes all relevant data, and logs the username.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask SetAsOnline()
        {
            await SetPresence(Availability.Online, "In Friends Menu");
            await RefreshAll();
            Debug.Log($"Logged in as {authManager.Username}");
        }

        /// <summary>
        /// Waits for 30 seconds before setting the user's presence and activity status.
        /// </summary>
        /// <param name="presenceAvailabilityOptions">The availability options to set after the delay.</param>
        /// <param name="activityStatus">The activity status to set after the delay.</param>
        /// <returns>An enumerator for the delayed presence update operation.</returns>
        IEnumerator DelayedOffline(Availability presenceAvailabilityOptions, string activityStatus = "")
        {
            yield return new WaitForSecondsRealtime(30);
            SetPresence(presenceAvailabilityOptions, activityStatus).Forget();
        }

        /// <summary>
        /// Executes the requested friend action (add, block, unblock, etc.).
        /// </summary>
        /// <param name="friendAction">The friend action to execute.</param>
        /// <param name="id">The ID of the friend on which to perform the action.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        public async UniTask MakeFriendAction(FriendAction friendAction, string id)
        {
            if (!IsFriendsServiceReady)
            {
                Debug.LogWarning("Couldn't create make a friend action because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            if (friendAction is FriendAction.None)
            {
                Debug.LogWarning("Could not implement any friend action called 'None'");
                return;
            }

            var friendTask = (friendAction switch
            {
                FriendAction.SendRequest => AddFriendAsync,
                FriendAction.AcceptRequest => AcceptRequestAsync,
                FriendAction.DeclineRequest => DeclineRequestAsync,
                FriendAction.Block => BlockFriendAsync,
                FriendAction.Unblock => UnblockFriendAsync,
                FriendAction.RemoveFriend => RemoveFriendAsync,
                _ => default(AsyncActionHandler<string>)
            });

            if (friendTask is null)
            {
                Debug.LogWarningFormat("The friend task set according the friend action {Action} is null", friendAction.ToString());
                return;
            }

            try
            {
                await friendTask(id);
                onFriendActionMade?.Invoke(friendAction);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            await RefreshAll();
        }

        /// <summary>
        /// Sends a party invitation to a specified player using a join code.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player to invite.</param>
        /// <param name="joinCode">The code used to join the party.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        public async UniTask InviteToParty(string playerId, string joinCode)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("Couldn't send a party request because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("Couldn't join to a party because the player id is null or empty");
                return;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogWarning("Couldn't join to a party because the join code is null or empty");
                return;
            }

            if (FriendsEntryDatas.Any(x => x.UUID == playerId))
            {
                Debug.LogWarning($"Player with id <b>({playerId})</b> is already in the party. Leave the current party before joining another one.");
                return;
            }

            var friendMessagePayload = new FriendMessagePayload
                (type: NotificationType.PartyInvite, 
                content: $"{authManager.Username} invites you to joins its party",
                dataCollection: new Dictionary<string, string>() { [Consts.CollectionKeys.JoinCode] = joinCode });

            try
            {
                await friendsService.MessageAsync(playerId, friendMessagePayload);
                Debug.Log($"Party request sent to {playerId}.");
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to invite to {playerId} a party. - {e}");
            }
        }

        /// <summary>
        /// Attempts to join a party using the specified join code, logging warnings or errors if conditions are not
        /// met.
        /// </summary>
        /// <param name="joinCode">The code used to join the target party.</param>
        /// <returns>A UniTask representing the asynchronous join operation.</returns>
        public async UniTask JoinToParty(string joinCode)
        {
            if (!partyController)
            {
                Debug.LogWarning("Couldn't join to a party because the party controller reference is null");
                return;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogWarning("Couldn't join to a party because the join code is null or empty");
                return;
            }

            if (FriendsEntryDatas.Any(x => x.UUID == authManager.UUID))
            {
                Debug.LogWarning("You are already in a party. Leave the current party before joining another one.");
                return;
            }

            try
            {
                await partyController.JoinToParty(joinCode);
                Debug.Log($"Joined to party with code {joinCode}.");
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to join to party with code {joinCode}. - {e}");
            }
        }

        /// <summary>
        /// Determines whether the specified player is a friend or has a pending incoming or outgoing friend request.
        /// </summary>
        /// <param name="playerId">The ID of the player to check.</param>
        /// <returns>True if the player is a friend or has a pending request; otherwise, false.</returns>
        public async UniTask<bool> IsFriendOrRequested(string playerId)
        {
            return ((await GetFriends())?.Any(x => x.Id == playerId) ?? false) 
                || ((await GetIncomingRequests())?.Any(x => x.Id == playerId) ?? false)
                || ((await GetOutgoingRequests())?.Any(x => x.Id == playerId) ?? false);
        }

        /// <summary>
        /// Displays a confirmation popup for friendship with the specified player.
        /// </summary>
        /// <param name="playerName">The name of the player to confirm friendship with.</param>
        /// <param name="playerId">The ID of the player to confirm friendship with.</param>
        public void OpenConfirmFrienshipPopUp(string playerName, string playerId)
        {
            if (!confirmFriendshipPopUp)
            {
                Debug.LogWarning("confirmFriendshipPopUp reference is null");
                return;
            }

            confirmFriendshipPopUp.OpenPopUp(playerName, playerId);
        }


        #region Friend Generic Handlers
        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of tuples containing the friend action and the corresponding UnityAction to register.</param>
        public void RegisterFriendEvent(params (FriendAction friendAction, UnityAction<string> unityAction)[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var (friendAction, unityAction) in eventsToHandle)
            {
                var eventToHandler = friendAction switch
                {
                    FriendAction.SendRequest => onFriendRequestSent,
                    FriendAction.AcceptRequest => onAcceptRequest,
                    FriendAction.DeclineRequest => onDeclineRequest,
                    FriendAction.Block => onBlockFriend,
                    FriendAction.Unblock => onUnblockFriend,
                    FriendAction.RemoveFriend => onRemoveFriend,
                    _ => default
                };

                if (eventToHandler is not null)
                    eventToHandler.AddListener(unityAction);
            }
        }

        /// <summary>
        /// Unregisters a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of tuples containing the friend action and the corresponding UnityAction to unregister.</param>
        public void UnregisterFriendEvent(params (FriendAction friendAction, UnityAction<string> unityAction)[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var (friendAction, unityAction) in eventsToHandle)
            {
                var eventToHandler = friendAction switch
                {
                    FriendAction.SendRequest => onFriendRequestSent,
                    FriendAction.AcceptRequest => onAcceptRequest,
                    FriendAction.DeclineRequest => onDeclineRequest,
                    FriendAction.Block => onBlockFriend,
                    FriendAction.Unblock => onUnblockFriend,
                    FriendAction.RemoveFriend => onRemoveFriend,
                    _ => default
                };

                if (eventToHandler is not null)
                    eventToHandler.RemoveListener(unityAction);
            }
        }
        #endregion

        #region UGS Friend Event Handlers
        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to register as listeners for the message received event.</param>
        public void RegisterMessageReceived(params UnityAction<IMessageReceivedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (MessageReceived is not null)
                    MessageReceived.AddListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to register as listeners for the relationship added event.</param>
        public void RegisterRelationshipAdded(params UnityAction<IRelationshipAddedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (RelationshipAdded is not null)
                    RelationshipAdded.AddListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to register as listeners for the relationship deleted event.</param>
        public void RegisterRelationshipDeleted(params UnityAction<IRelationshipDeletedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (RelationshipDeleted is not null)
                    RelationshipDeleted.AddListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to register as listeners for the presence updated event.</param>
        public void RegisterPresenceUpdated(params UnityAction<IPresenceUpdatedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (PresenceUpdated is not null)
                    PresenceUpdated.AddListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to register as listeners for the notifications connectivity changed event.</param>
        public void RegisterNotificationsConnectivityChanged(params UnityAction<INotificationsStateChangedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (NotificationsConnectivityChanged is not null)
                    NotificationsConnectivityChanged.AddListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to unregister as listeners for the message received event.</param>
        public void UnregisterMessageReceived(params UnityAction<IMessageReceivedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (MessageReceived is not null)
                    MessageReceived.RemoveListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to unregister as listeners for the relationship added event.</param>
        public void UnregisterRelationshipAdded(params UnityAction<IRelationshipAddedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (RelationshipAdded is not null)
                    RelationshipAdded.RemoveListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to unregister as listeners for the relationship deleted event.</param>
        public void UnregisterRelationshipDeleted(params UnityAction<IRelationshipDeletedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (RelationshipDeleted is not null)
                    RelationshipDeleted.RemoveListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to unregister as listeners for the presence updated event.</param>
        public void UnregisterPresenceUpdated(params UnityAction<IPresenceUpdatedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (PresenceUpdated is not null)
                    PresenceUpdated.RemoveListener(unityAction);
        }

        /// <summary>
        /// Registers a UnityEvent as a listener to a specific friend action.
        /// </summary>
        /// <param name="eventsToHandle">An array of UnityActions to unregister as listeners for the notifications connectivity changed event.</param>
        public void UnregisterNotificationsConnectivityChanged(params UnityAction<INotificationsStateChangedEvent>[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var unityAction in eventsToHandle)
                if (NotificationsConnectivityChanged is not null)
                    NotificationsConnectivityChanged.RemoveListener(unityAction);
        }
        #endregion

        #region Friend Events
        /// <summary>
        /// Sends a friend request to a user by ID.
        /// </summary>
        /// <param name="playerId">The ID of the player to send the friend request to.</param>
        private async UniTask<bool> SendFriendRequest(string playerId)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return false;
            }

            try
            {
                var relationship = await friendsService.AddFriendAsync(playerId);
                Debug.Log($"Friend request sent to {playerId}.");
                return relationship.Type is RelationshipType.FriendRequest or RelationshipType.Friend;
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to Request {playerId} - {e}.");
                return false;
            }
        }

        /// <summary>
        /// Sets the user presence and activity message.
        /// </summary>
        /// <param name="presenceAvailabilityOptions">The availability status to set for the user.</param>
        /// <param name="activityStatus">The activity message to set for the user.</param>
        private async UniTask SetPresence(Availability presenceAvailabilityOptions, string activityStatus = "")
        {
            var friendsService = await GetFriendsServiceSafeAsync(isTryingToInitializeOnFail: false);
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            var activity = new Activity { Status = activityStatus };
            if (lastPresenceSet.HasValue
                && lastPresenceSet.Value.availability == presenceAvailabilityOptions
                && lastPresenceSet.Value.activity.Status == activity.Status)
                return;

            try
            {
                lastPresenceSet = (presenceAvailabilityOptions, activity);
                
                await friendsService.SetPresenceAsync(presenceAvailabilityOptions, activity);
                Debug.Log($"Availability changed to {presenceAvailabilityOptions}.");
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to set the presence to {presenceAvailabilityOptions} - {e}");
            }
        }

        /// <summary>
        /// Sends a friend request to the specified player and triggers related events and data refresh if applicable.
        /// </summary>
        /// <param name="playerId">The ID of the player to whom the friend request is sent.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask AddFriendAsync(string playerId)
        {
            var wasRequestSent = await SendFriendRequest(playerId);
            if (wasRequestSent)
            {
                onFriendRequestSent?.Invoke(playerId);

                if (RequestsEntryDatas.Any(entry => entry.TargetID == playerId))
                    await RefreshAll();
            }
        }

        /// <summary>
        /// Blocks a friend by their player ID asynchronously and updates the friends list.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player to block.</param>
        /// <returns>A task representing the asynchronous block operation.</returns>
        private async UniTask BlockFriendAsync(string playerId)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            var relationShip = default(Relationship);
            try
            {
                relationShip = await friendsService.AddBlockAsync(playerId);
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to block {playerId}. - {e}");
            }

            await RefreshAll();

            if (relationShip is not null and { Type: RelationshipType.Block })
            {
                onBlockFriend?.Invoke(playerId);
                Debug.Log($"{playerId} was blocked.");
            }
        }

        /// <summary>
        /// Asynchronously removes the block for the specified friend and triggers the unblock event if successful.
        /// </summary>
        /// <param name="playerId">The ID of the player to unblock.</param>
        /// <returns>A UniTask representing the asynchronous unblock operation.</returns>
        private async UniTask UnblockFriendAsync(string playerId)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            try
            {
                await friendsService.DeleteBlockAsync(playerId);
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to unblock {playerId} - {e}.");
            }

            await RefreshAll();

            var blocks = await GetBlockedMembers();
            var stillBlocked = blocks?.Any(x => x.Id == playerId) ?? false;
            if (!stillBlocked)
            {
                onUnblockFriend?.Invoke(playerId);
                Debug.Log($"{playerId} was unblocked.");
            }
        }

        /// <summary>
        /// Removes a friend with the specified player ID asynchronously and updates the friends list.
        /// </summary>
        /// <param name="playerId">The ID of the player to remove from the friends list.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask RemoveFriendAsync(string playerId)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            try
            {
                await friendsService.DeleteFriendAsync(playerId);

            }
            catch (FriendsServiceException e)
            {
                Debug.LogError($"Failed to remove {playerId}. - {e}");
            }

            await RefreshAll();

            var friends = await GetFriends();
            var stillFriend = friends?.Any(x => x.Id == playerId) ?? false;
            if (!stillFriend)
            {
                onRemoveFriend?.Invoke(playerId);
                Debug.Log($"{playerId} was removed from the friends list.");
            }
        }

        /// <summary>
        /// Accepts a friend request from the specified player asynchronously.
        /// </summary>
        /// <param name="playerId">The ID of the player whose friend request is being accepted.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask AcceptRequestAsync(string playerId)
        {
            if (!IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            var requestOrAccepted = false;
            try
            {
                requestOrAccepted = await SendFriendRequest(playerId);
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to accept request from {playerId}. - {e}");
            }

            await RefreshAll();

            if (requestOrAccepted)
            {
                onAcceptRequest?.Invoke(playerId);
                Debug.Log($"Friend request from {playerId} was accepted.");
            }
        }

        /// <summary>
        /// Declines an incoming friend request from the specified player asynchronously.
        /// </summary>
        /// <param name="playerId">The ID of the player whose friend request is to be declined.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        private async UniTask DeclineRequestAsync(string playerId)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            try
            {
                await friendsService.DeleteIncomingFriendRequestAsync(playerId);
                Debug.Log($"Friend request from {playerId} was declined.");
            }
            catch (FriendsServiceException e)
            {
                Debug.Log($"Failed to decline request from {playerId}. - {e}");
            }

            await RefreshAll();
            onDeclineRequest?.Invoke(playerId);
        }
        #endregion

        #region Refresh Aux
        /// <summary>
        /// Updates the FriendsEntryDatas list with the latest information about friends, including their online status
        /// and activity.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous refresh operation.</returns>
        private async UniTask RefreshFriends()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            FriendsEntryDatas.Clear();
            var friends = await GetFriends();
            if (friends is null || friends.Count == 0)
            {
                Debug.Log("No friends found.");
                return;
            }

            foreach (var friend in friends)
            {
                var presence = friend.Presence;
                bool isReallyOnline = presence.Availability == Availability.Online
                    || (DateTime.UtcNow - presence.LastSeen) < TimeSpan.FromSeconds(offlineDelaySeconds);

                string activityText;
                if (presence.Availability == Availability.Offline ||
                    presence.Availability == Availability.Invisible)

                    activityText = presence.LastSeen.ToShortDateString() + " " +
                                   presence.LastSeen.ToLongTimeString();
                else
                    activityText = presence.GetActivity<Activity>()?.Status ?? "Unknown";

                var info = new FriendsEntryData
                {
                    SenderID = authManager.UUID,
                    TargetID = friend.Id,
                    Name = friend.Profile.Name,
                    Availability = isReallyOnline ? Availability.Online : Availability.Offline,
                    Activity = activityText,
                };
                FriendsEntryDatas.Add(info);
            }
        }

        /// <summary>
        /// Updates the list of friend requests by retrieving incoming and outgoing requests from the friends service.
        /// </summary>
        /// <returns></returns>
        private async UniTask RefreshRequests()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            RequestsEntryDatas.Clear();

            // Get incoming requests
            var requests = await GetIncomingRequests();
            if (requests is not null and { Count: > 0 })
            {
                Debug.Log($"There are {requests.Count} incoming requests");

                foreach (var request in requests)
                    RequestsEntryDatas.Add(new FriendsEntryData
                    {
                        SenderID = request.Id,
                        TargetID = authManager.UUID,
                        Name = request.Profile?.Name ?? "Unknown",
                        Availability = request.Presence?.Availability ?? Availability.Unknown,
                        Activity = request.Presence?.GetActivity<Activity>()?.Status ?? "Unknown"
                    });
            }

            // Also include outgoing requests if any
            requests = await GetOutgoingRequests();
            if (requests is not null and { Count: > 0 })
            { 
                Debug.Log($"There are {requests.Count} outgoing requests");
            
                foreach (var request in requests)
                    RequestsEntryDatas.Add(new FriendsEntryData
                    {
                        SenderID = authManager.UUID,
                        TargetID = request.Id,
                        Name = request.Profile?.Name ?? "Unknown",
                        Availability = request.Presence?.Availability ?? Availability.Unknown,
                        Activity = request.Presence?.GetActivity<Activity>()?.Status ?? "Unknown"
                    });
            }
        }

        /// <summary>
        /// Asynchronously updates the BlockEntryDatas collection with the latest block entries from the friends
        /// service.
        /// </summary>
        /// <returns></returns>
        private async UniTask RefreshBlocks()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return;
            }

            BlockEntryDatas.Clear();

            foreach (var block in friendsService.Blocks)
                BlockEntryDatas.Add(new FriendsEntryData
                {
                    SenderID = authManager.UUID,
                    TargetID = block.Id,
                    Name = block.Member.Profile.Name,
                    Availability = Availability.Unknown,
                    Activity = Availability.Unknown.ToString()
                });
        }

        /// <summary>
        /// Refreshes all friend-related lists.
        /// </summary>
        public async UniTask RefreshAll()
        {
            await UniTask.WhenAll(RefreshFriends(), RefreshRequests(), RefreshBlocks());
        }
        #endregion

        #region Getters
        /// <summary>
        /// Retrieves a list of non-blocked friends asynchronously.
        /// </summary>
        /// <returns>A list of Member objects representing non-blocked friends.</returns>
        public async UniTask<List<Member>> GetFriends()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return new List<Member>();
            }
            return await GetNonBlockedMembers(friendsService.Friends);
        }

        /// <summary>
        /// Retrieves a list of incoming friend requests that are not blocked.
        /// </summary>
        /// <returns>A list of Member objects representing incoming friend requests.</returns>
        public async UniTask<List<Member>> GetIncomingRequests()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return new List<Member>();
            }
            return await GetNonBlockedMembers(friendsService.IncomingFriendRequests);
        }

        /// <summary>
        /// Retrieves a list of outgoing friend requests excluding blocked members.
        /// </summary>
        /// <returns>A list of Member objects representing outgoing friend requests.</returns>
        public async UniTask<List<Member>> GetOutgoingRequests()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return new List<Member>();
            }
            return await GetNonBlockedMembers(friendsService.OutgoingFriendRequests);
        }

        /// <summary>
        /// Retrieves a list of members from the provided relationships who are not blocked.
        /// </summary>
        /// <param name="relationships">A read-only list of relationships to filter for non-blocked members.</param>
        /// <returns>A list of members who are not blocked.</returns>
        private async UniTask<List<Member>> GetNonBlockedMembers(IReadOnlyList<Relationship> relationships)
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return new List<Member>();
            }

            var blocks = friendsService.Blocks;
            return relationships
                   .Where(relationship =>
                       !blocks.Any(blockedRelationship => blockedRelationship.Member.Id == relationship.Member.Id))
                   .Select(relationship => relationship.Member)
                   .ToList();
        }

        /// <summary>
        /// Retrieves a list of members who are blocked by the user.
        /// </summary>
        /// <returns>A list of blocked members.</returns>
        private async UniTask<List<Member>> GetBlockedMembers()
        {
            var friendsService = await GetFriendsServiceSafeAsync();
            if (friendsService is null || !IsFriendsServiceReady)
            {
                Debug.LogWarning("FriendsService is not initialized yet.");
                return new List<Member>();
            }

            var blocks = friendsService.Blocks;
            return blocks
                   .Select(relationship => relationship.Member)
                   .ToList();
        }
        #endregion

        /// <summary>
        /// Represents a payload for friend messages, including notification type, content, associated data, and
        /// timestamp.
        /// </summary>
        [Serializable]
        public class FriendMessagePayload
        {
            public NotificationType type; 
            public string content;     // Actual message content
            public Dictionary<string, string> dataCollection;    // Join code for parties or rooms, if applicable
            public long timestamp;     // Unix timestamp in UTC

            public FriendMessagePayload() { }

            public FriendMessagePayload(NotificationType type, string content, Dictionary<string, string> dataCollection)
            {
                this.type = type;
                this.content = content;
                this.dataCollection = dataCollection;
                this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        /// <summary>
        /// Represents a custom player activity (status message).
        /// </summary>
        [Serializable]
        internal class Activity
        {
            [JsonProperty("status", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Include)]
            public string Status { get; set; } = string.Empty;
        }

        /// <summary>
        /// Represents a friend's entry containing identification, availability, activity, and timestamp information.
        /// </summary>
        [Serializable]
        public struct FriendsEntryData
        {
            public string UUID;
            public string SenderID;
            public string TargetID;
            public string Name;
            public Availability Availability;
            public string Activity;
            public long Timestamp;

            public FriendsEntryData(string senderID, string targetID, string name, Availability availability, string activity, long timestamp)
            {
                UUID = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                SenderID = senderID;
                TargetID = targetID;
                Name = name;
                Availability = availability;
                Activity = activity;
                Timestamp = timestamp;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder("FriendEntryData: \n");
                sb.Append(Name);
                sb.Append(" : ");
                sb.AppendLine(SenderID);
                sb.Append(" : ");
                sb.AppendLine(TargetID);
                sb.Append(Availability);
                sb.Append(" : ");
                sb.AppendLine(Activity);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Enum representing all possible friend-related actions.
        /// </summary>
        public enum FriendAction
        {
            None = 0,

            SendRequest = 1,
            AcceptRequest = 2,
            DeclineRequest = 3,

            Block = 4,
            Unblock = 5,

            RemoveFriend = 6,
        }

        /// <summary>
        /// Represents events related to friend services, such as message reception, notification changes, presence
        /// updates, and relationship modifications.
        /// </summary>
        public enum FriendServiceEvent
        { 
            None = 0,

            MessageReceived = 1,
            NotificationsStateChanged = 2,
            PresenceUpdated = 3,

            RelationshipAdded = 5,
            RelationshipDeleted = 6
        }
    }
}
