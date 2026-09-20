using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;
using static ProDomino.FriendSystem.FriendManager;
using static ProDomino.NotificationSystem.FirebaseWebGLReceiver;

namespace ProDomino.NotificationSystem
{
    /// <summary>
    /// Controls the user interface for push notifications, including popup display,
    /// event listening, filtering, and Cloud Save integration.
    /// </summary>
    public class NotificationController : MonoBehaviour
    {
        [Header("Alarm / UI References")]
        [SerializeField] private CanvasGroup alarmNewNotificationCanvasGroup;
        [SerializeField] private FirebaseWebGLReceiver firebaseWebGLReceiver;
        [SerializeField] private Button openPopUpButton;
        [SerializeField] private UnityEvent onOpenNotificationPopUp;

        [Header("PopUp")]
        [SerializeField] private CanvasGroup popUpCanvasGroup;
        [SerializeField] private CanvasGroup noNotificationsCanvasGroup;
        [SerializeField] private NotificationEntry notificationEntryPrefab;
        [SerializeField] private Transform notificationEntriesParent;
        [SerializeField] private Button closePopUpButton;
        [SerializeField] private CustomButtonToggleGroupUI notificationFiltersToggleGroupUI;

        private AuthManager authManager;
        private GameManager gameManager;
        private FriendManager friendManager;
        private DictionaryService dictionaryService;
        private AnalyticsManager analyticsManager;
        private List<NotificationEntry> notificationEntries;

        public bool IsViewingLatestNews { get; private set; }
        internal FriendsEntryData[] RequestsEntryDatas => friendManager.RequestsEntryDatas?.ToArray();

        internal const string LatestNotifications = "LatestNotifications";
        internal const string NewsNotifications = "GameNotifications";

        /// <summary>
        /// Indicates whether the user is currently authenticated.
        /// </summary>
        public bool IsAuthenticated => gameManager is { IsAuthenticated: true };

        private void Awake()
        {
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            friendManager = ServiceLocator.Instance.GetService<FriendManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();

            // Validate required services
            if (!gameManager || !authManager || !dictionaryService || !analyticsManager)
                Debug.LogError("Could not initialize the controller due to missing services");

            notificationEntries = notificationEntriesParent?.GetComponentsInChildren<NotificationEntry>(true)?.ToList() ?? new();

            // Subscribe to Firebase message reception
            if (firebaseWebGLReceiver)
                firebaseWebGLReceiver.RegisterOnMessageReceived(OnReceiveMessage);
            else
                Debug.LogWarning("Couldn't subscribe handler on receive new message because receiver reference is null");

            // Register own methods when ugs friend api is called
            friendManager.RegisterMessageReceived(OnFriendMessageReceived);
            friendManager.RegisterRelationshipAdded(OnFriendRelationshipAdded);
            friendManager.RegisterRelationshipDeleted(OnFriendRelationshipDeleted);

            // UI bindings
            openPopUpButton?.onClick.AddListener(OpenPopUp);
            closePopUpButton?.onClick.AddListener(ClosePopUp);

            notificationFiltersToggleGroupUI.SetOnCustomButtonSelectedCallback(FilterEntries);

            // MainReferenceInitialize existing entries
            foreach (var notificationEntry in notificationEntries)
                notificationEntry.Initialize(() => authManager.UUID, _iconID => dictionaryService.GetSpriteNoCollection(_iconID));
        }

        private void Start()
        {
            if (!gameManager || !friendManager)
            {
                Debug.LogErrorFormat("Could not initialize the controller due {GameManager} or {FriendManager} is null", nameof(GameManager), nameof(FriendManager));
                return;
            }

            gameManager.HandleOnSignIn(UpdateInstacesSync);

            // By default, hide all notification entries
            notificationEntries.ForEach(x => x?.gameObject.SetActive(false));
            notificationFiltersToggleGroupUI.SelectedButtons?.FirstOrDefault()?.Select();
        }

        private void Update()
        {
            // Ensure the button is only interactable if the user is authenticated
            if (openPopUpButton != null)
                openPopUpButton.interactable = IsAuthenticated;
        }

        private void OnDestroy()
        {
            if (firebaseWebGLReceiver)
                firebaseWebGLReceiver.UnregisterOnMessageReceived(OnReceiveMessage);

            if (gameManager)
                gameManager.UnHandleOnSignIn(UpdateInstacesSync);

            if (friendManager)
            {
                friendManager.UnregisterMessageReceived(OnFriendMessageReceived);
                friendManager.UnregisterRelationshipAdded(OnFriendRelationshipAdded);
                friendManager.UnregisterRelationshipDeleted(OnFriendRelationshipDeleted);
            }

            openPopUpButton?.onClick.RemoveListener(OpenPopUp);
            closePopUpButton?.onClick.RemoveListener(ClosePopUp);
        }


        private async void OnFriendMessageReceived(IMessageReceivedEvent messageReceivedEvent)
        {
            if (!friendManager)
            {
                Debug.LogWarning("Couldn't call receive message callback because friend manager reference is null");
                return;
            }

            var friends = await friendManager.GetFriends();
            if (friends is null or { Count: 0 })
            {
                Debug.LogWarning("Couldn't determine message sender because friends collection is null or empty");
                return;
            }

            var friend = friends.FirstOrDefault(x => x.Id == messageReceivedEvent.UserId);
            if (friend is null)
            {
                Debug.LogWarning("Couldn't determine message sender");
                return;
            }

            var payload = messageReceivedEvent.GetAs<FriendMessagePayload>();
            if (payload is null or { content: null or "" })
            {
                Debug.LogWarning("Friend Notification is null or its content is null or empty");
                return;
            }

            var header = $"{friend.Profile.Name} ({friend.Id}):";

            var newPlayerNotificationData = new PlayerNotificationData
                (title: header,
                body: payload.content,
                image: null, // TODO: ask if this could show a specific image
                senderID: friend.Id,
                targetID: authManager.UUID,
                notificationType: payload.type,
                timestamp: payload.timestamp,
                isGameNotification: true,
                dataCollection: payload.dataCollection,
                read: false);

            AddNewNotification(newPlayerNotificationData);
        }

        private void OnFriendRelationshipAdded(IRelationshipAddedEvent relationshipAddedEvent)
        {
            var relationShip = relationshipAddedEvent.Relationship;
            //var relationshipJson = JsonConvert.SerializeObject(relationShip, settings: new() { Converters = { new MemberRoleConverter(), new RelationshipTypeConverter() } });
            
            if (relationShip is null or { Member: null })
            {
                Debug.LogWarning("Couldn't process relationship adding because it is null or its member is null");
                return;
            }

            var header = string.Empty;
            var body = string.Empty;
            var newPlayerNotificationData = default(PlayerNotificationData);

            if (relationShip.Type is RelationshipType.Friend)
            {
                header = $"¡{relationShip.Member.Profile.Name} accepted your friend request!";
                body = $"You are now friends with {relationShip.Member.Profile.Name} ({relationShip.Member.Id})";

                newPlayerNotificationData = new PlayerNotificationData
                    (title: header,
                    body: body,
                    image: null, // TODO: ask if this could show a specific image
                    senderID: authManager.UUID,
                    targetID: relationShip.Member.Id,
                    notificationType: NotificationType.Simple,
                    timestamp: ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                    isGameNotification: true,
                    dataCollection: null/*new() { [Consts.CollectionKeys.Relationship] = relationshipJson }*/,
                    read: false);

                analyticsManager.SendAnalytic(AnalyticType.AddFriends, 1);
            } 
            else if (relationShip.Type is RelationshipType.FriendRequest)
            {
                newPlayerNotificationData = GenerateRequestNotification
                    (relationShip.Member.Id,
                    authManager.UUID, 
                    relationShip.Member.Profile.Name);
            }
            else
            { 
                Debug.LogWarning($"Relationship type {relationShip.Type} is not a friend, skipping notification creation.");
                return;
            }


            AddNewNotification(newPlayerNotificationData);
        }
        
        private void OnFriendRelationshipDeleted(IRelationshipDeletedEvent relationshipDeletedEvent)
        {
            var relationShip = relationshipDeletedEvent.Relationship;
            //var relationshipJson = JsonConvert.SerializeObject(relationShip, settings: new() { Converters = { new MemberRoleConverter(), new RelationshipTypeConverter() } });

            if (relationShip is null or { Member: null })
            {
                Debug.LogWarning("Couldn't process relationship deletion because it is null or its member is null");
                return;
            }

            if (relationShip.Type is not RelationshipType.Friend)
            {
                Debug.LogWarning($"Relationship type {relationShip.Type} is not a friend, skipping notification creation.");
                return;
            }

            var header = $"User is no longer your friend";
            var body = $"The user identified with id ({relationShip.Member.Id}) is no longer your friend";

            var newPlayerNotificationData = new PlayerNotificationData
                (title: header,
                body: body,
                image: null, // TODO: ask if this could show a specific image
                senderID: authManager.UUID,
                targetID: relationShip.Member.Id,
                notificationType: NotificationType.Simple,
                timestamp: ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),
                isGameNotification: true,
                dataCollection: null/*new() { [Consts.CollectionKeys.Relationship] = relationshipJson }*/,
                read: false);

            AddNewNotification(newPlayerNotificationData);
        }

        /// <summary>
        /// Filters the list of displayed notifications based on the selected category.
        /// </summary>
        private void FilterEntries(string toggleId)
        {
            IsViewingLatestNews = toggleId is LatestNotifications;

            if (notificationEntries is null or { Count: 0 })
            {
                Debug.Log("No possible filter notification because there aren't");
                return;
            }

            notificationEntries.ForEach(instance => instance.SetActive(false));

            var filteredEntries = notificationEntries
                .Where(x => x != null && 
                    (IsViewingLatestNews && (!x.PlayerNotificationData?.isGameNotification ?? false) ||
                    (!IsViewingLatestNews && (x.PlayerNotificationData?.isGameNotification ?? false))))
                .ToList();

            if (filteredEntries is null or { Count: 0 })
            {
                noNotificationsCanvasGroup?.SetActive(true, isSettingInteractable: false, isSettingBlocksRaycasts: false);
                Debug.LogWarning("Couldn't filter notification. Collection is null or empty");
                return;
            }
            noNotificationsCanvasGroup?.SetActive(false, isSettingInteractable: false, isSettingBlocksRaycasts: false);

            foreach (var notificationEntry in filteredEntries)
            {
                notificationEntry.SetActive(true);
                notificationEntry.transform.SetAsLastSibling();
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Updates the visibility of the new notification alert icon.
        /// </summary>
        private void UpdateAlarmNotificationVisibility(bool wasRead)
        {
            if (!alarmNewNotificationCanvasGroup)
            {
                Debug.LogWarning("Couldn't update alarm icon visibility because canvas group is null");
                return;
            }

            alarmNewNotificationCanvasGroup.SetActive(!wasRead, isSettingInteractable: false, isSettingBlocksRaycasts: false);
        }

        /// <summary>
        /// Triggers an asynchronous update of notification instances.
        /// </summary>
        private void UpdateInstacesSync() => UpdateInstances().Forget();

        /// <summary>
        /// Refreshes the UI entries for current notifications in memory.
        /// </summary>
        private async UniTask UpdateInstances()
        {
            if (!gameManager || !dictionaryService)
            {
                Debug.LogWarning("Could not update instances due to null services");
                return;
            }

            // Refresh friend requests to ensure the latest data
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
            SetInteractivity(true);

            var playerNotificationDatas = gameManager.PlayerNotificationDatas
               ?.OrderByDescending(x => x.timestamp)
               ?.Distinct()
               ?.ToList();

            // Check if there are any requests to show
            if (RequestsEntryDatas.Length > 0)
            {
                // Filter out requests that are already assigned to player notifications
                var noAssignedRequestsNotification = RequestsEntryDatas
                    ?.Where(x => !playerNotificationDatas.Any(y => x.TargetID == y.id)) // TODO: use a UUID that doesn't change in every run
                    ?.ToArray();

                // If there are no assigned requests, create notifications for them
                var hasNewRequests = false;
                if (noAssignedRequestsNotification is not null and { Length: > 0 })
                    foreach (var requestEntryData in noAssignedRequestsNotification)
                    {
                        var newPlayerNotificationData = GenerateRequestNotification
                            (requestEntryData.SenderID, 
                            requestEntryData.TargetID,
                            requestEntryData.Name);

                        if (!playerNotificationDatas.Contains(newPlayerNotificationData))
                        { 
                            playerNotificationDatas.Add(newPlayerNotificationData);
                            AddNewNotification(newPlayerNotificationData, false, false);
                            if (!hasNewRequests)
                                hasNewRequests = true;
                        }
                    }

                // If new requests were added, update the game manager and show the alarm icon 
                if (hasNewRequests)
                {
                    // Show the alarm icon since there are new notifications
                    UpdateAlarmNotificationVisibility(false);

                    // Sort and remove duplicates again
                    playerNotificationDatas = playerNotificationDatas
                       ?.OrderByDescending(x => x.timestamp)
                       ?.Distinct()
                       ?.ToList();

                    // Save the updated notifications to the game manager
                    gameManager.SavePublicGameData();
                }
            }

            // Instantiate additional entries if needed
            if (playerNotificationDatas is not null and { Count: > 0 })
                for (var i = 0; i < playerNotificationDatas.Count; i++)
                { 
                    if (i >= notificationEntries.Count)
                    {
                        var newInstance = Instantiate(notificationEntryPrefab, notificationEntriesParent);
                        newInstance.Initialize(() => authManager.UUID, _iconID => dictionaryService.GetSpriteNoCollection(_iconID));
                        notificationEntries.Add(newInstance);
                    }
                }
            else
                Debug.LogWarning("Player notification data collection is null or empty, no new instances will be created.");

            // Hide all current entries
            notificationEntries.ForEach(instance =>
            {
                instance.Reset();
                instance.SetActive(false);
            });

            if (playerNotificationDatas is not null and { Count: > 0 })
            {
                // Show active and relevant entries
                foreach (var notificationEntryData in playerNotificationDatas)
                {
                    var instance = notificationEntries?.FirstOrDefault(i => !i.gameObject.activeSelf);
                    if (instance != null)
                    {
                        var onConfirm = notificationEntryData.notificationType switch
                        {
                            NotificationType.Reward => OnConfirmReward,
                            NotificationType.FriendRequest => OnConfirmFriendRequest,
                            NotificationType.PartyInvite => OnConfirmPartyRequest,
                            _ => default(AsyncActionHandler<PlayerNotificationData>),
                        };

                        var onDecline = notificationEntryData.notificationType switch
                        {
                            NotificationType.FriendRequest => OnDeclineFriendRequest,
                            _ => default(AsyncActionHandler<PlayerNotificationData>),
                        };

                        instance.Configure
                            (notificationEntryData,
                            onConfirm,
                            onDecline);
                        instance.SetActive(true);
                    }
                    else
                        Debug.LogWarning("No available instance to configure the notification entry");
                }
            } 
            else
                Debug.LogWarning("Notification entry data collection is null or empty.");

            // Filter the notifications based on the current view
            notificationFiltersToggleGroupUI.GetButtonUI(IsViewingLatestNews ? LatestNotifications : NewsNotifications)?.Select();
        }

        private PlayerNotificationData GenerateRequestNotification(string senderID, string targetID, string name)
        {
            var imSender = senderID == authManager.UUID;
            var header = imSender ? "Friend request sent — waiting for response." : "You have a pending friend request.";
            var body = imSender
                ? $"Your friend request to <b>{name}</b> is still pending"
                : $"<b>{name}</b> has sent you a friend request";

            var newPlayerNotificationData = new PlayerNotificationData
                (title: header,
                body: body,
                image: null, // TODO: ask if this could show a specific image
                senderID: senderID,
                targetID: targetID,
                notificationType: NotificationType.FriendRequest,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                isGameNotification: true,
                dataCollection: null,
                read: false);

            return newPlayerNotificationData;
        }

        /// <summary>
        /// Displays the notification popup panel.
        /// </summary>
        private void OpenPopUp()
        {
            if (!popUpCanvasGroup)
            {
                Debug.LogWarning("Couldn't open popup because its canvas group reference is null");
                return;
            }

            if (notificationFiltersToggleGroupUI)
                notificationFiltersToggleGroupUI.DeselectAll();
            else
                Debug.LogWarning("Couldn't deselect all notification filters because toggle group reference is null");

            UpdateInstances().Forget();

            popUpCanvasGroup.SetActive(true);
            UpdateAlarmNotificationVisibility(true);
        }

        /// <summary>
        /// Hides the notification popup panel.
        /// </summary>
        private void ClosePopUp()
        {
            if (!popUpCanvasGroup)
            {
                Debug.LogWarning("Couldn't close popup because its canvas group reference is null");
                return;
            }

            popUpCanvasGroup.SetActive(false);

            if (notificationFiltersToggleGroupUI)
                notificationFiltersToggleGroupUI.DeselectAll();
            else
                Debug.LogWarning("Couldn't deselect all notification filters because toggle group reference is null");

            UpdateInstances().Forget();
        }

        /// <summary>
        /// Sets the block state of the popup canvas group, enabling or disabling user interaction.
        /// </summary>
        /// <param name="isInteractable"></param>
        private void SetInteractivity(bool isInteractable)
        {
            if (!popUpCanvasGroup)
            {
                Debug.LogWarning("Couldn't block popup because its canvas group reference is null");
                return;
            }

            // Only change interactivity if the popup is visible
            popUpCanvasGroup.SetActive(isInteractable, isSettingAlpha: false);
        }

        /// <summary>
        /// Adds a new notification to the game manager's notification list,
        /// </summary>
        /// <param name="newPlayerNotification"></param>
        /// <param name="isSaving"></param>
        /// <param name="isUpdatingInstances"></param>
        private void AddNewNotification(PlayerNotificationData newPlayerNotification, bool isSaving = true, bool isUpdatingInstances = true)
        {
            if (newPlayerNotification is null)
            {
                Debug.LogWarning("The notification tried to add is null");
                return;
            }

            if (!gameManager.PlayerNotificationDatas.Contains(newPlayerNotification))
            { 
                gameManager.PlayerNotificationDatas.Add(newPlayerNotification);
                gameManager.TrimNotificationExcess();

                if (isSaving)
                    gameManager.SavePublicGameData();

                UpdateAlarmNotificationVisibility(false);
            }

            if (isUpdatingInstances)
                UpdateInstances().Forget();
        }

        /// <summary>
        /// Called when a new Firebase notification is received.
        /// Converts the payload into a PlayerNotificationData instance,
        /// adds it to GameManager, and updates the UI.
        /// </summary>
        private void OnReceiveMessage(FirebaseNotificationPayload firebaseNotificationPayload)
        {
            if (!IsAuthenticated || !gameManager)
            {
                Debug.LogWarning("Couldn't process the notification due to auth or gameManager issue");
                return;
            }

            if (firebaseNotificationPayload is null or { notification: null })
            {
                Debug.LogWarning("Couldn't process the notification because it is null");
                return;
            }

            UpdateAlarmNotificationVisibility(false);

            var isGameNotification = (firebaseNotificationPayload.data?.TryGetValue(Consts.CollectionKeys.IsGameNotification, out var isGameNotificationString) ?? false)
                && bool.TryParse(isGameNotificationString, out var isGameNotificationBool)
                ? isGameNotificationBool
                : false;

            var newPlayerNotificationData = new PlayerNotificationData(
                title: firebaseNotificationPayload.notification?.title ?? "No Title",
                body: firebaseNotificationPayload.notification?.body ?? "No Body",
                image: firebaseNotificationPayload.notification?.image ?? null,
                senderID: firebaseNotificationPayload.from ?? "No sender",
                targetID: authManager.UUID,
                notificationType: NotificationType.Simple,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                read: false,
                dataCollection: firebaseNotificationPayload.data,
                isGameNotification: isGameNotification
            );

            AddNewNotification(newPlayerNotificationData);
        }

        private async UniTask OnConfirmFriendRequest(PlayerNotificationData playerNotificationData)
        {
            if (!friendManager)
            {
                Debug.LogWarning("Couldn't confirm friend request because friend manager reference is null");
                return;
            }

            SetInteractivity(false);
            try
            {
                await friendManager.MakeFriendAction(FriendAction.AcceptRequest, playerNotificationData.senderID);
                analyticsManager.SendAnalytic(AnalyticType.AddFriends, 1);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                SetInteractivity(true);

                gameManager.RemoveNotification(playerNotificationData.GetHashCode());
                ClosePopUp();
            }
        }

        private async UniTask OnDeclineFriendRequest(PlayerNotificationData playerNotificationData)
        {
            if (!friendManager)
            {
                Debug.LogWarning("Couldn't confirm friend request because friend manager reference is null");
                return;
            }

            SetInteractivity(false);
            try
            {
                await friendManager.MakeFriendAction(FriendAction.DeclineRequest, playerNotificationData.senderID);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                SetInteractivity(true);

                gameManager.RemoveNotification(playerNotificationData.GetHashCode());
                ClosePopUp();
            }
        }

        private async UniTask OnConfirmPartyRequest(PlayerNotificationData playerNotificationData)
        {
            if (!friendManager)
            {
                Debug.LogWarning("Couldn't confirm friend request because friend manager reference is null");
                return;
            }

            if (playerNotificationData is null or { dataCollection: null or { Count: 0 }})
            {
                Debug.LogWarning("Couldn't confirm party request because player notification data is null or its data collection is empty");
                return;
            }

            var joinCode = playerNotificationData.dataCollection.TryGetValue(Consts.CollectionKeys.JoinCode, out var joinCodeValue)
                ? joinCodeValue
                : null;

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogWarning("Couldn't confirm party request because join code is null or empty");
                return;
            }

            SetInteractivity(false);
            try
            {
                await friendManager.JoinToParty(joinCode);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                // if there are more notification like the one recently accepted, trim all of them
                if (gameManager.PlayerNotificationDatas.Contains(playerNotificationData))
                {
                    Debug.Log($"Detected notification equals to party invitation (hashcode: {playerNotificationData.GetHashCode()}). Procceding to delete them...");

                    // Try to remove every notification that is equals to the accepted invitation
                    var deletedQuantity = gameManager.PlayerNotificationDatas.RemoveAll(x => x == playerNotificationData);
                    if (deletedQuantity > 0)
                    { 
                        Debug.Log($"Deleted {deletedQuantity} notification equals to party invitation (hashcode: {playerNotificationData.GetHashCode()})");
                        gameManager.SavePublicGameData();
                    }
                }

                if (popUpCanvasGroup.alpha is 0)
                    ClosePopUp();
                else
                    SetInteractivity(true);

                UpdateInstances().Forget();
            }
        }

        private async UniTask OnDeclinePartyRequest(PlayerNotificationData playerNotificationData)
        {
            if (!friendManager)
            {
                Debug.LogWarning("Couldn't confirm friend request because friend manager reference is null");
                return;
            }

            SetInteractivity(false);
            try
            {
                // TODO: there is a way to decline a request or just not accept it?
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                if (popUpCanvasGroup.alpha is 0)
                    ClosePopUp();
                else
                    SetInteractivity(true);
            }
        }

        private async UniTask OnConfirmReward(PlayerNotificationData playerNotificationData)
        {
            // TODO: valdiate with Game Desing if this functionality will be integrated
            await UniTask.CompletedTask;
        }


        /// <summary>
        /// Event called when a player is added to the part<br></br>
        /// If the player joined is the client, close the poppUp<br></br>
        /// Called from MatchManager inspector event
        /// </summary>
        /// <param name="playerID"></param>
        public void OnPlayerJoined(string playerID)
        {
            if (playerID != authManager.UUID)
                return;

            ClosePopUp();
        }
    }
}
