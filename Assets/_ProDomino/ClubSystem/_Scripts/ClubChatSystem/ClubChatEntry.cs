using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.FirestoreClubChatData;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a UI entry for a club chat message, displaying sender information, message content, timestamp, and
    /// profile image, and managing layout alignment based on the sender.
    /// </summary>
    internal class ClubChatEntry : MonoBehaviour
    {
        [Header("Message properties")]
        [SerializeField] private Image playerImage;
        [SerializeField] private TMP_Text usernameLabel;
        [SerializeField] private TMP_Text uuidLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text timestampLabel;

        [Header("Layout properties")]
        [SerializeField] private LayoutGroup rootLayout;
        [SerializeField] private LayoutGroup contentLayout;
        
        private DictionaryService dictionaryService;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;

        public MessageData MessageData { get; private set; }

        /// <summary>
        /// Initialize the entry passing args
        /// </summary>
        /// <param name="dictionaryService">The dictionary service used for localization and other dictionary-related tasks.</param>
        /// <param name="getCurrentPlayerMemberData">Function to retrieve the current player's member data.</param>
        internal void Initialize
            (DictionaryService dictionaryService, 
            Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData)
        {
            this.dictionaryService = dictionaryService;
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData;
        }

        /// <summary>
        /// Configure the entry using the message data
        /// </summary>
        /// <param name="messageData">The message data to configure the entry with.</param>
        /// <param name="profileIconSprite">The sprite to use for the player's profile icon.</param>
        internal void Configure(MessageData messageData, Sprite profileIconSprite)
        {
            MessageData = messageData;

            if (messageData is null)
            {
                Debug.LogWarning("The message data to assignt to assign to the entry is null and couldn't be configured");
                ClearData();
                return;
            }

            var currentPlayerData = getCurrentPlayerMemberData?.Invoke();

            if (usernameLabel)
                usernameLabel.text = currentPlayerData is not null && currentPlayerData.unityMemberId == messageData.senderId 
                    ? "You" 
                    : messageData.senderName;
            else
                Debug.LogWarning($"{nameof(usernameLabel)} is null");

            if (uuidLabel)
                uuidLabel.text = messageData.senderId;
            else
                Debug.LogWarning($"{nameof(uuidLabel)} is null");

            if (messageLabel)
                messageLabel.text = messageData.content;
            else
                Debug.LogWarning($"{nameof(messageLabel)} is null");

            if (timestampLabel)
                timestampLabel.text = FormatMessageTimestamp(messageData.timestamp);
            else
                Debug.LogWarning($"{nameof(timestampLabel)} is null");

            // Fill in player image
            if (playerImage)
                playerImage.sprite = profileIconSprite;
            else
                Debug.LogError("Player Image is not assigned in the inspector.", this);

            // Get the current player member data and alternate the layourt properties if it's necessary
            if (currentPlayerData is not null)
            {
                var isTheCurrentPlayer = currentPlayerData.unityMemberId == messageData.senderId;

                // For the root layout, determine if it will be shown on the right or on the left
                if (rootLayout)
                    rootLayout.childAlignment = isTheCurrentPlayer ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                else
                    Debug.LogError($"{nameof(rootLayout)} is not assigned in the inspector");

                // For the content layout, determine if the content will be shown normal or reversed
                if (contentLayout && contentLayout is HorizontalOrVerticalLayoutGroup layoutGroup)
                    layoutGroup.reverseArrangement = isTheCurrentPlayer ? true : false;
                else
                    Debug.LogError($"{nameof(contentLayout)} is not assigned in the inspector");
            } 
        }

        /// <summary>
        /// Converts a UTC Unix timestamp (milliseconds) into a localized time string.
        /// </summary>
        /// <param name="timestamp">Unix timestamp in milliseconds (UTC).</param>
        /// <returns>Localized 12-hour format string, e.g. "3:42 PM".</returns>
        public static string FormatMessageTimestamp(DateTime timestamp)
        {
            try
            {
                // 1. Convert UTC: Local time of the device
                var localTime = timestamp.ToLocalTime();

                // 2. Format as 12-hour clock with AM/PM
                if (localTime.Date == DateTime.Now.Date)
                    return localTime.ToString("h:mm tt"); // today
                else
                    return localTime.ToString("MMM dd, h:mm tt"); // "Oct 29, 9:25 PM"

            }
            catch (Exception ex)
            {
                // Defensive fallback — return placeholder instead of throwing
                UnityEngine.Debug.LogWarning($"[Chat] Failed to format timestamp: {ex.Message}");
                return "--:--";
            }
        }

        /// <summary>
        /// Clears all label texts, resets the player image, and restores layout alignments to their default states.
        /// </summary>
        private void ClearData()
        {
            if (usernameLabel)
                usernameLabel.text = string.Empty;
            else
                Debug.LogWarning($"{nameof(usernameLabel)} is null");

            if (uuidLabel)
                uuidLabel.text = string.Empty;
            else
                Debug.LogWarning($"{nameof(uuidLabel)} is null");

            if (messageLabel)
                messageLabel.text = string.Empty;
            else
                Debug.LogWarning($"{nameof(messageLabel)} is null");

            if (timestampLabel)
                timestampLabel.text = string.Empty;
            else
                Debug.LogWarning($"{nameof(timestampLabel)} is null");

            // Fill in player image
            if (playerImage)
                playerImage.sprite = default;
            else
                Debug.LogError("Player Image is not assigned in the inspector.", this);

            // For the root layout, determine if it will be shown on the right or on the left
            if (rootLayout)
                rootLayout.childAlignment = TextAnchor.MiddleLeft;
            else
                Debug.LogError($"{nameof(rootLayout)} is not assigned in the inspector");
            
            // For the content layout, determine if the content will be shown normal or reversed
            if (contentLayout && contentLayout is HorizontalOrVerticalLayoutGroup layoutGroup)
                layoutGroup.reverseArrangement = false;
            else
                Debug.LogError($"{nameof(contentLayout)} is not assigned in the inspector");
        }
    }
}
