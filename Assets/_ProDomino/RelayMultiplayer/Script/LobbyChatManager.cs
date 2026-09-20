using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using ProDomino.Shared;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ProDomino.GameModes;
using Timba.Utils;
using System.Collections.Generic;
using ProDomino.GameSystem;
using Timba.Patterns;

namespace ProDomino.RelayMultiplayer
{
    /// <summary>
    /// Manages the lobby chat functionality in a multiplayer lobby.
    /// </summary>
    public class LobbyChatManager : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_InputField message_InputField;
        [SerializeField] private Button sendMessageButton;
        [SerializeField] private RectTransform messagesContainer;
        [SerializeField] private GameObject messageChatPrefab;
        [SerializeField] private float waitingTimeBetweenMessages = 1f;
        [SerializeField] private LobbyChatEntry[] lobbyChatEntries;
        [SerializeField] private InputActionReference sendAction;

        private GameManager gameManager;
        private float lastMessageTime = 0;
        private bool wasInputLastObjectTargeted;
        private Func<string, Sprite> getDefaultTierSpriteIcon;

        private Action<string> onSendMessageFromClientToHost = null;
        public Action<string> OnSendMessageFromClientToHost
        {
            get => onSendMessageFromClientToHost;
            set => onSendMessageFromClientToHost = value;
        }

        private RectTransformPanZoomController rectTransformPanZoomController;
        private RectTransformPanZoomController RectTransformPanZoomController => rectTransformPanZoomController = rectTransformPanZoomController != null 
            ? rectTransformPanZoomController 
            : FindFirstObjectByType<RectTransformPanZoomController>();

        public bool IsBlocked => RectTransformPanZoomController ? RectTransformPanZoomController.IsBlocked : false;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
        }

        private void Start()
        {
            AbstractGameMode.HandleOnResetLobby(ResetData);
            sendMessageButton.onClick.AddListener(SendMessageToHost);
        }

        private void OnEnable()
        {
            if (sendAction is not null and { action: not null })
                sendAction.action.Enable();
            else
                Debug.LogWarning("SendAction is not assigned in LobbyChatManager. Please assign it in the inspector or through code.");
        }

        private void OnDisable()
        {
            if (sendAction is not null and { action: not null })
                sendAction.action.Disable();
            else
                Debug.LogWarning("SendAction is not assigned in LobbyChatManager. Please assign it in the inspector or through code.");
        }

        private void OnDestroy()
        {
            AbstractGameMode.ClearOnResetLobby();
        }

        private void Update()
        {
            // Check if the input field lost selection
            if (wasInputLastObjectTargeted
                && EventSystem.current.currentSelectedGameObject != message_InputField.gameObject)
            {
                wasInputLastObjectTargeted = false;
            }

            bool isEnterPressed = sendAction?.action?.WasPerformedThisFrame() ?? false;
            bool isTextNotEmpty = !string.IsNullOrEmpty(message_InputField.text);
            bool isActuallySelected = EventSystem.current.currentSelectedGameObject == message_InputField.gameObject;

            if (isTextNotEmpty && isEnterPressed && (wasInputLastObjectTargeted || isActuallySelected))
            {
                wasInputLastObjectTargeted = true;
                SendMessageToHost();

                // Reactivate so it keeps focus visually + logically
                EventSystem.current.SetSelectedGameObject(message_InputField.gameObject);
                message_InputField.ActivateInputField();
            }
        }


        public void Initialize(Func<string, Sprite> getDefaultTierSpriteIcon)
        {
            this.getDefaultTierSpriteIcon = getDefaultTierSpriteIcon;


            // Ensure gameManager is assigned (in case Initialize is called before Start/Awake)
            // This is a safeguard; ideally, the lifecycle should be managed to avoid this scenario, but it's here to prevent null reference issues if Initialize is called early.
            // When the match is being created everything occurs in the same frame, so if Initialize is called before Start/Awake, gameManager would be null, so we try to get it again here.
            if (!gameManager)
                gameManager = ServiceLocator.Instance.GetService<GameManager>();
        }

        /// <summary>
        /// Configures the lobby chat entries based on the provided player data.
        /// </summary>
        /// <param name="onFinishConfigure">Callback invoked when configuration is complete.</param>
        /// <param name="playerDataInfos">Array of player data information.</param>
        public async void Configure(Action onFinishConfigure, params PlayerDataInfo[] playerDataInfos)
        { 
            if (lobbyChatEntries is null or { Length : 0 })
            {
                Debug.LogWarning("LobbyChatEntries is not assigned or empty in LobbyChatManager.");
                return;
            }

            for (int i = 0; i < lobbyChatEntries.Length; i++)
            {
                var entry = lobbyChatEntries[i];
                if (entry == null)
                {
                    Debug.LogWarning($"LobbyChatEntry at index {i} is not assigned in LobbyChatManager.");
                    continue;
                }

                if (i < playerDataInfos.Length)
                {
                    var playerData = playerDataInfos[i];
                    if (!playerData.IsConfigured)
                    {
                        Debug.LogWarning($"PlayerDataInfo at index {i} is not configured in LobbyChatManager. Skipping entry.");
                        entry.SetActive(false); // Hide unused entries
                        continue;
                    } 
                    else
                        entry.gameObject.SetActive(true); // Show the entry if player data is configured

                    var profileSprite = await gameManager.GetSpriteAsync(playerData.profileIconID, Consts.CollectionKeys.Icons);
                    var badgesSprites = !string.IsNullOrEmpty(playerData.badgesIDs) ? playerData.badgesIDs.Split(',')?.Select(id => gameManager.GetSprite(id, Consts.CollectionKeys.Achievements))?.ToArray() : null;
                    var tierSprite = gameManager.GetSprite(playerData.leaderboardTier, Consts.CollectionKeys.Ranks); // Assuming tileSkinID is used for rank sprite
                    var defaultTierSpriteIcon = getDefaultTierSpriteIcon?.Invoke(Consts.CollectionKeys.Ranks); // Get default tier sprite

                    entry.Configure(
                        profileSprite: profileSprite,
                        uuid: playerData.userId,
                        displayName: playerData.username,
                        badgesSprites: badgesSprites,
                        tierSprite: tierSprite,
                        defaultTierSprite: defaultTierSpriteIcon,
                        score: (int)playerData.leaderboardScore);

                } 
                
                else
                    entry.SetActive(false); // Hide unused entries
            }
            
            // Once all entries are configured, invoke the callback (use callback instead of async task to be used in Relay methods)
            if (onFinishConfigure is not null)
                onFinishConfigure();
            else
                Debug.LogWarning("onFinishConfigure action is null in LobbyChatManager.");
        }

        /// <summary>
        /// Resets the lobby chat data, clearing messages and resetting timers.
        /// </summary>
        public void ResetData()
        {
            lastMessageTime = 0;
            Configure(() =>
            {
                Debug.Log("LobbyChatManager resetting data...");

                // Once configured, clear messages
                if (messagesContainer)
                    messagesContainer.DestroyChildren();
            });
        }

        /// <summary>
        /// Sends a chat message to the host if the waiting time has passed.
        /// </summary>
        private void SendMessageToHost()
        {
            if (Time.time - lastMessageTime >= waitingTimeBetweenMessages)
            {
                string auxMessage = message_InputField.text.Trim(); //Elimine spaces to valide

                if (string.IsNullOrEmpty(auxMessage))
                {
                    return; //Message is empty
                }

                onSendMessageFromClientToHost?.Invoke(message_InputField.text);

                lastMessageTime = Time.time;

                message_InputField.text = "";
            }
            else
            {
                float remaining = waitingTimeBetweenMessages - (Time.time - lastMessageTime);
                Debug.Log($"You must wait {remaining:F1} seconds before sending another message.");
            }
        }

        /// <summary>
        /// Displays a chat message in the lobby chat UI.
        /// </summary>
        /// <param name="fromClientID">The client ID of the sender.</param>
        /// <param name="toClientID">The client ID of the recipient.</param>
        /// <param name="fromPlayerID">The player ID of the sender.</param>
        /// <param name="fromPlayerName">The player name of the sender.</param>
        /// <param name="message">The chat message content.</param>
        /// <param name="dateTime">The date and time the message was sent.</param>
        /// <param name="SendBanToPlayerAction">Action to send a ban to a player.</param>
        public void ShowMessageInChat
            (int fromClientID, 
            int toClientID, 
            string fromPlayerID, 
            string fromPlayerName, 
            string message, 
            DateTime dateTime, 
            Action<int> SendBanToPlayerAction)
        {
            var lobbyChatEntry = lobbyChatEntries.FirstOrDefault(entry => entry.PlayerID == fromPlayerID);

            var messageChat = Instantiate(messageChatPrefab, messagesContainer);
            var auxPlayerName = "Player ID " + fromClientID;

            if (fromClientID == toClientID)
            {
                messageChat.GetComponent<HorizontalLayoutGroup>().reverseArrangement = true;
                auxPlayerName = $"You";

                if (!string.IsNullOrEmpty(fromPlayerName))
                    auxPlayerName += $" ({fromPlayerName})";
            }
            else if (!string.IsNullOrEmpty(fromPlayerName))
                auxPlayerName = fromPlayerName;

            // Get profile sprite from cache if available
            var profileSprite = gameManager.GetSprite(fromPlayerID, Consts.CollectionKeys.Icons);

            messageChat.GetComponent<MessageChatContainer>().SetMessageData
                (fromClientID, 
                toClientID,
                fromPlayerID,
                auxPlayerName,
                dateTime.ToLocalTime().ToString("hh:mm tt"), 
                message,
                profileSprite,
                SendBanToPlayerAction);

            messageChat.transform.parent?.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Using by UI button to toggle chat active state
        /// </summary>
        /// <param name="isActive"></param>
        public void SetChatActive(bool isActive)
        {
            if (!RectTransformPanZoomController)
            {
                Debug.LogWarning("Zoom controller not found, couldn't check blocked state.");
                return;
            }

            if (IsBlocked)
            {
                Debug.Log("Cannot toggle chat while zoom controller is blocked.");
                return;
            }

            if (root)
                root.SetActive(isActive);
            else
                Debug.LogWarning("Root GameObject is not assigned in LobbyChatManager.");
        }
    }
}
