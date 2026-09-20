using HelperSharedLibrary;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.NotificationSystem
{
    /// <summary>
    /// Represents a single visual notification entry in the UI list.
    /// Handles title, body and icon assignment.
    /// </summary>
    internal class NotificationEntry : MonoBehaviour
    {
        [SerializeField] private Image iconObject;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [SerializeField] private Button confirmButton;
        [SerializeField] private Button declineButton;

        private Func<string> getCurrentPlayerID;
        private Func<string, Sprite> getIconSprite;
        private AsyncActionHandler<PlayerNotificationData> onConfirmAction;
        private AsyncActionHandler<PlayerNotificationData> onDeclineAction;

        /// <summary>
        /// The data backing this notification entry.
        /// </summary>
        internal PlayerNotificationData PlayerNotificationData { get; private set; }

        /// <summary>
        /// Initializes the entry with a function that resolves icons based on string IDs.
        /// </summary>
        internal void Initialize(Func<string> getCurrentPlayerID, Func<string, Sprite> getIconSprite)
        {
            this.getCurrentPlayerID = getCurrentPlayerID;
            this.getIconSprite = getIconSprite;
        }

        /// <summary>
        /// Configures the UI based on provided PlayerNotificationData.
        /// </summary>
        internal void Configure
            (PlayerNotificationData playerNotificationData, 
            AsyncActionHandler<PlayerNotificationData> onConfirm = null,
            AsyncActionHandler<PlayerNotificationData> onDecline = null)
        {
            PlayerNotificationData = playerNotificationData;
            onConfirmAction = onConfirm;
            onDeclineAction = onDecline;

            if (PlayerNotificationData == null)
            {
                Debug.LogWarning("Cannot configure notification entry because data is null.");
                return;
            }

            if (headerLabel && !string.IsNullOrEmpty(playerNotificationData.title))
                headerLabel.text = playerNotificationData.title;
            else
                Debug.LogWarning("Title label is missing or title data is empty.");

            if (bodyLabel && !string.IsNullOrEmpty(playerNotificationData.body))
                bodyLabel.text = playerNotificationData.body;
            else
                Debug.LogWarning("Body label is missing or body data is empty.");

            if (iconImage && getIconSprite != null)
            {
                if (!string.IsNullOrEmpty(playerNotificationData.image))
                {
                    var sprite = getIconSprite(playerNotificationData.image);
                    if (sprite != null)
                        iconObject?.gameObject.SetActive(true);
                    else
                        Debug.LogWarning($"Sprite not found for ID '{playerNotificationData.image}'");
                } 
            } 

            if (iconObject)
                iconObject.gameObject.SetActive(iconImage.sprite != null);

            var isHimself = playerNotificationData.senderID == getCurrentPlayerID?.Invoke();
            if (confirmButton)
            {
                var onConfirmHasValue = onConfirm is not null;
                confirmButton.gameObject.SetActive(onConfirmHasValue && !isHimself);

                if (onConfirmHasValue)
                {
                    confirmButton.onClick.RemoveAllListeners();

                    // Check if the notification is from himself to avoid self-interaction
                    if (!isHimself)
                        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
                }
            }
            
            if (declineButton)
            {
                var ondenyHasValue = onDecline is not null;
                declineButton.gameObject.SetActive(ondenyHasValue && !isHimself);

                if (ondenyHasValue)
                {
                    declineButton.onClick.RemoveAllListeners();

                    // Check if the notification is from himself to avoid self-interaction
                    if (!isHimself)
                        declineButton.onClick.AddListener(OnDeclineButtonClicked);
                }
            }
        }

        internal void Reset()
        {
            PlayerNotificationData = null;
            onConfirmAction = null;
            onDeclineAction = null;

            if (headerLabel)
                headerLabel.text = string.Empty;
            if (bodyLabel)
                bodyLabel.text = string.Empty;
            if (iconImage)
                iconImage.sprite = null;
            if (iconObject)
                iconObject.gameObject.SetActive(false);

            if (confirmButton)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.gameObject.SetActive(false);
            }
            if (declineButton)
            {
                declineButton.onClick.RemoveAllListeners();
                declineButton.gameObject.SetActive(false);
            }
        }

        private async void OnConfirmButtonClicked()
        {
            if (onConfirmAction == null)
            { 
                Debug.LogWarning("No confirm action assigned for this notification entry.");
                return;
            }

            confirmButton.interactable = false; // Prevent multiple clicks
            try
            {
                await onConfirmAction(PlayerNotificationData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing confirm action: {e.Message}");
            }
            finally
            {
                confirmButton.interactable = true; // Re-enable button after action
            }
        }

        private void OnDeclineButtonClicked()
        {
            if (onDeclineAction == null)
            {
                Debug.LogWarning("No decline action assigned for this notification entry.");
                return;
            }

            declineButton.interactable = false; // Prevent multiple clicks
            try
            {
                onDeclineAction(PlayerNotificationData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing decline action: {e.Message}");
            }
            finally
            {
                declineButton.interactable = true; // Re-enable button after action
            }
        }

        /// <summary>
        /// Toggles the active state of this entry.
        /// </summary>
        internal void SetActive(bool isActive)
        {
            if (gameObject)
                gameObject.SetActive(isActive);
            else
                Debug.LogWarning("GameObject is missing. Cannot toggle visibility.");
        }
    }
}
