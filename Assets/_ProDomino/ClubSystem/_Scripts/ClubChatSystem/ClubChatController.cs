using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static HelperSharedLibrary.FirestoreClubChatData;


namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages club chat functionality, including sending messages, displaying chat entries, handling notifications,
    /// and listening for chat updates within a Unity UI.
    /// </summary>
    public class ClubChatController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private TMP_InputField chatBoxInputfield;
        [SerializeField] private Transform chatsEntryParent;
        [SerializeField] private ClubChatEntry chatEntryPrefab;
        [SerializeField] private Button sendMessageButton;
        [SerializeField] private Image messageNotificationImage;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private InputActionReference sendAction;

        private float lastSendTime;
        private const float SendCooldown = 0.2f;

        private GameManager gameManager;
        private AuthManager authManager;
        private DictionaryService dictionaryService;
        private List<ClubChatEntry> clubChatEntries;
        private string lastRegisteredMessage;

        private Func<FirestoreClubData> getClubData;
        private Func<ConfigData> getConfigData;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;
        private AsyncFuncHandler<bool, string> tryToSendClubChatMessage;
        private AsyncFuncHandler<bool> startListeningMessages;
        private AsyncFuncHandler<bool> stopListeningMessages;

        internal FirestoreClubData ClubData => getClubData?.Invoke();

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            clubChatEntries = chatsEntryParent.GetComponentsInChildren<ClubChatEntry>(true)?.ToList() ?? new();
            if (clubChatEntries is not null and { Count: > 0 })
                foreach (var entry in clubChatEntries)
                { 
                    entry.Initialize(dictionaryService, () => getCurrentPlayerMemberData?.Invoke());
                    
                    // By default, turn off the gameobject
                    entry.gameObject.SetActive(false);
                }

            if (sendMessageButton)
                sendMessageButton.onClick.AddListener(TryToSendMessage);
            else
                Debug.LogError("Edit Icon Button is not assigned in the inspector.", this);
        }

        private void Start()
        {
            if (sendMessageButton)
                sendMessageButton.interactable = false;

            if (messageNotificationImage)
                messageNotificationImage.gameObject.SetActive(false);

            // Make sure the content is down by default
            if (scrollRect)
                scrollRect.verticalNormalizedPosition = 0;
            else
                Debug.LogError($"Missing reference of {nameof(scrollRect)}");
        }

        private void OnEnable()
        {
            if (sendAction is not null and { action: not null })
                sendAction.action.Enable();
            else
                Debug.LogWarning("SendAction is not assigned in ClubChatController. Please assign it in the inspector or through code.");
        }

        private void OnDisable()
        {
            if (sendAction is not null and { action: not null })
                sendAction.action.Disable();
            else
                Debug.LogWarning("SendAction is not assigned in ClubChatController. Please assign it in the inspector or through code.");
        }

        private void Update()
        {
            if (!sendMessageButton || !chatBoxInputfield)
                return;

            sendMessageButton.interactable = !string.IsNullOrWhiteSpace(chatBoxInputfield.text);

            bool isEnterPressed = sendAction?.action?.WasPerformedThisFrame() ?? false;
            bool hasFocus = EventSystem.current.currentSelectedGameObject == chatBoxInputfield.gameObject;
            bool isTextNotEmpty = !string.IsNullOrEmpty(chatBoxInputfield.text);

            if (isTextNotEmpty && isEnterPressed && hasFocus && Time.time - lastSendTime > SendCooldown)
            {
                sendMessageButton.onClick.Invoke();
                lastSendTime = Time.time;

                // Keep focus visually and logically
                chatBoxInputfield.ActivateInputField();
            }
        }

        /// <summary>
        /// Initialize the ClubMemberController with the given actions
        /// </summary>
        /// <param name="getClubData">Function to retrieve the current club data.</param>
        /// <param name="getConfigData">Function to retrieve the configuration data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to retrieve the current player's member data.</param>
        /// <param name="tryToSendClubChatMessage">Async function handler for sending a club chat message.</param>
        /// <param name="startListeningMessages">Async function handler for starting to listen to club chat messages.</param>
        /// <param name="stopListeningMessages">Async function handler for stopping listening to club chat messages.</param>
        /// <param name="refGetClubChatData">Reference to the async event handler for getting club chat data.</param>
        internal void Initialize
            (Func<FirestoreClubData> getClubData,
            Func<ConfigData> getConfigData,
            Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,

			AsyncFuncHandler<bool, string> tryToSendClubChatMessage,
            AsyncFuncHandler<bool> startListeningMessages,
            AsyncFuncHandler<bool> stopListeningMessages,
            ref AsyncEventHandler<FirestoreClubChatData> refGetClubChatData)
        {
            this.getClubData = getClubData ?? throw new ArgumentNullException(nameof(getClubData));
            this.getConfigData = getConfigData ?? throw new ArgumentNullException(nameof(getConfigData));
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.tryToSendClubChatMessage = tryToSendClubChatMessage ?? throw new ArgumentNullException(nameof(tryToSendClubChatMessage));
            this.startListeningMessages = startListeningMessages ?? throw new ArgumentNullException(nameof(startListeningMessages));
            this.stopListeningMessages = stopListeningMessages ?? throw new ArgumentNullException(nameof(stopListeningMessages));

            // Subscribe listener to configure the UI with the data got from firestore
            if (refGetClubChatData is not null)
                refGetClubChatData.AddListener(Configure);
            else
                throw new ArgumentNullException(nameof(refGetClubChatData));
        }

        /// <summary>
        /// Configures the club members list with the provided member data.
        /// </summary>
        /// <param name="clubChatData">The club chat data to configure the chat entries with.</param>
        internal async UniTask Configure(FirestoreClubChatData clubChatData)
        {
            // Check for necessary references
            if (!chatEntryPrefab || !chatsEntryParent)
            {
                Debug.LogError("Member Entry Prefab or Members Entry Parent is not assigned in the inspector.", this);
                return;
            }

            var wasScrollAtEnd = scrollRect?.verticalNormalizedPosition <= .1f;
            var messagesDatas = clubChatData?.messages;

            // Get the current number of entries and calculate the difference to add new ones if needed
            var difference = (messagesDatas?.Count ?? 0) - clubChatEntries.Count;
            if (difference > 0)
                for (int i = 0; i < difference; i++)
                {
                    var newEntry = Instantiate(chatEntryPrefab, chatsEntryParent);
                    newEntry.Initialize
                        (dictionaryService,
                        () => getCurrentPlayerMemberData?.Invoke());
                    clubChatEntries.Add(newEntry);
                }

            // Configure each entry with the corresponding member data or deactivate if no data
            if (clubChatEntries is not null and { Count: > 0 })
            {
                if (messagesDatas is not null)
                    await TryToCachePlayersProfileIcons(messagesDatas.ToArray());

                for (int i = 0; i < clubChatEntries.Count; i++)
                {
                    var dataExists = i < (messagesDatas?.Count ?? 0);
                    var messageData = messagesDatas?.ElementAtOrDefault(i);
                    var profileSprite = gameManager.GetSprite(messageData?.profileIconId, Consts.CollectionKeys.Icons);

                    clubChatEntries[i].gameObject.SetActive(dataExists);
                    clubChatEntries[i].Configure(messageData, profileSprite);
                }

                // Sort entries by rank and then by member name
                clubChatEntries = clubChatEntries
                    .Where(entry => entry.gameObject.activeSelf)
                    .OrderBy(entry => entry.MessageData.timestamp)
                    .ToList();

                // Reparent entries to reflect the new order in the hierarchy
                for (int i = 0; i < clubChatEntries.Count; i++)
                    clubChatEntries[i].transform.SetSiblingIndex(i);
            }

            // Refresh the ui each time it's modified
            transform.parent.RefreshLayoutGroupsImmediateAndRecursive();

            if (clubChatData is not null) // Check if Chat data exists
            {
                // If there is a new message and its sender is not our player, activate the notification image
                var currentPlayerData = getCurrentPlayerMemberData?.Invoke();
                var lastMessageData = clubChatData.messages.FirstOrDefault(x => x.content == clubChatData.lastMessage);
                var isCurrentPlayerLastSender = !string.IsNullOrEmpty(lastMessageData?.senderId) && lastMessageData.senderId == currentPlayerData?.unityMemberId;
                
                if (currentPlayerData is not null // Check player data has value
                    && lastRegisteredMessage != clubChatData.lastMessage // Validate that the last message registered is different for the new one
                    && lastMessageData != null // Check if the data of the last message exists
                    && !isCurrentPlayerLastSender) // Validate that the last message sender was not you
                {
                    lastRegisteredMessage = clubChatData.lastMessage;

                    if (messageNotificationImage)
                        messageNotificationImage.gameObject.SetActive(true);
                    else
                        Debug.LogError($"Mission reference of {nameof(messageNotificationImage)}");
                }
            } 
            else
                Debug.LogWarning("Club chat data is missing. Couldn't determine new message notification");

            // Make sure the content is always at end if previously the scroll was
            if (scrollRect)
                scrollRect.verticalNormalizedPosition = wasScrollAtEnd ? 0 : scrollRect.verticalNormalizedPosition;
            else
                Debug.LogError($"Missing reference of {nameof(scrollRect)}");
        }

        /// <summary>
        /// Try to send a club chat message using firestore
        /// </summary>
        private async void TryToSendMessage()
        {
            if (tryToSendClubChatMessage is null)
            {
                Debug.LogError("The event that will send the club chat message is null");
                return;
            }

            if (chatBoxInputfield is null)
            {
                Debug.LogError("The club chat box inputfield reference is missing");
                return;
            }

            if (chatBoxInputfield.text == string.Empty)
            {
                Debug.LogWarning("You couldn't send an empty message");
                return;
            }

            rootCanvasGroup?.SetActive(false, isSettingAlpha: false);

            var temporalText = chatBoxInputfield.text;
            chatBoxInputfield.text = string.Empty;

            try
            {
                var wasChatMessageSentProperly = await tryToSendClubChatMessage(temporalText);
                if (wasChatMessageSentProperly)
                { 
                    Debug.Log("The club chat message was sent properly");

                    // Reselect the inputfield after the text was sent
                    EventSystem.current.SetSelectedGameObject(chatBoxInputfield.gameObject);

                    // The message should be appears due the listener, not for the data of the send message callback
                }
                else
                    Debug.LogWarning("Failed to sent club chat message");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send club chat message:\n\n{ex.Message}");
                chatBoxInputfield.text = temporalText;
            }
            finally
            {
                rootCanvasGroup?.SetActive(true, isSettingAlpha: false);
            }
        }


        /// <summary>
        /// Asynchronously caches profile icon sprites for players in the provided message data.
        /// </summary>
        /// <param name="messageDatas">An array of message data containing profile icon information.</param>
        /// <returns>A UniTask representing the asynchronous caching operation.</returns>
        private async UniTask TryToCachePlayersProfileIcons(MessageData[] messageDatas)
        {
            if (messageDatas is null or { Length: 0 })
            {
                Debug.LogWarning("No message data provided for caching icons.");
                return;
            }

            var cacheIconTasks = messageDatas
                .Where(data => data is not null && !string.IsNullOrEmpty(data.profileIconId))
                .Select(data => gameManager.GetSpriteAsync(data.profileIconId, Consts.CollectionKeys.Icons))
                .ToArray();

            await UniTask.WhenAll(cacheIconTasks);
        }

        /// <summary>
        /// Initializes chat message listening and disables the message notification image when the controller is
        /// opened.
        /// </summary>
        internal void OnOpenController()
        {
            if (startListeningMessages is null)
            {
                Debug.LogError("The event that will start the chat listening is null");
                return;
            }

            startListeningMessages();

            // Turn off the notification image when the screen is open
            if (messageNotificationImage)
                messageNotificationImage.gameObject.SetActive(false);
            else
                Debug.LogError($"Mission reference of {nameof(messageNotificationImage)}");
        }

        /// <summary>
        /// Stops the chat message listening process by invoking the associated event.
        /// </summary>
        internal void OnCloseController()
        {
            if (stopListeningMessages is null)
            {
                Debug.LogError("The event that will stops the chat listening is null");
                return;
            }

            stopListeningMessages();
        }
    }
}
