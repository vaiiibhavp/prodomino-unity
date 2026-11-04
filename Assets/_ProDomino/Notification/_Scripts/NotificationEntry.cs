using Cysharp.Threading.Tasks;
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
    public class NotificationEntry : MonoBehaviour
    {
        [SerializeField] private Image iconObject;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [SerializeField] private Button confirmButton;
        [SerializeField] private Button declineButton;

        [Header("Figma Card Additions")]
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private TMP_Text timestampLabel;
        [SerializeField] private Image avatarImage;
        [Tooltip("Container of confirm/decline buttons; hidden when neither is available")]
        [SerializeField] private GameObject actionsRow;

        private Func<string> getCurrentPlayerID;
        private Func<string, Sprite> getIconSprite;
        private AsyncActionHandler<PlayerNotificationData> onConfirmAction;
        private AsyncActionHandler<PlayerNotificationData> onDeclineAction;
        private bool isRunningAction;

        /// <summary>
        /// The data backing this notification entry.
        /// </summary>
        internal PlayerNotificationData PlayerNotificationData { get; private set; }

        /// <summary>
        /// True for cards built with the Accept/Decline actions row (the notification entry prefab's design).
        /// </summary>
        internal bool HasActionsRow => actionsRow && confirmButton && declineButton;

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

            var hasTitle = !string.IsNullOrEmpty(playerNotificationData.title);
            if (headerLabel && hasTitle)
                headerLabel.text = playerNotificationData.title;

            if (bodyLabel)
            {
                // Card without a dedicated header: render title as a bold first line above the body
                var body = playerNotificationData.body ?? string.Empty;
                bodyLabel.text = !headerLabel && hasTitle
                    ? $"<color=#FFFFFF><b>{playerNotificationData.title}</b></color>\n{body}"
                    : body;
            }
            else
                Debug.LogWarning("Body label is missing.");

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

            if (categoryLabel)
            {
                var category = "Notification";
                if (playerNotificationData.notificationType == HelperSharedLibrary.Enums.NotificationType.FriendRequest)
                    category = "Friends Notification";
                else if (playerNotificationData.notificationType == HelperSharedLibrary.Enums.NotificationType.PartyInvite)
                    category = "Party Notifications";
                else if (!string.IsNullOrEmpty(playerNotificationData.title) && playerNotificationData.title.IndexOf("Club", StringComparison.OrdinalIgnoreCase) >= 0)
                    category = "Club Membership";
                else if (playerNotificationData.isGameNotification)
                    category = "Game Notification";
                categoryLabel.text = category;
            }

            if (timestampLabel)
            {
                if (playerNotificationData.timestamp > 0)
                {
                    try
                    {
                        var dto = DateTimeOffset.FromUnixTimeSeconds(playerNotificationData.timestamp.Value).ToLocalTime();
                        timestampLabel.text = dto.ToString("hh:mm tt");
                    }
                    catch
                    {
                        timestampLabel.text = "12:00 PM";
                    }
                }
                else
                {
                    timestampLabel.text = "12:00 PM";
                }
            }

            if (avatarImage)
            {
                if (iconImage != null && iconImage.sprite != null)
                {
                    avatarImage.sprite = iconImage.sprite;
                    avatarImage.gameObject.SetActive(true);
                }
            }

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

            var row = ActionsRow;
            if (row)
                row.SetActive((confirmButton && confirmButton.gameObject.activeSelf) || (declineButton && declineButton.gameObject.activeSelf));
        }

        internal void Reset()
        {
            PlayerNotificationData = null;
            onConfirmAction = null;
            onDeclineAction = null;
            isRunningAction = false;
            SetButtonsInteractable(true);

            if (categoryLabel)
                categoryLabel.text = string.Empty;
            if (timestampLabel)
                timestampLabel.text = string.Empty;
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
            var row = ActionsRow;
            if (row)
                row.SetActive(false);
        }

        /// <summary>
        /// Container of the confirm/decline buttons. When the reference isn't assigned, falls back to the buttons'
        /// own parent row: some cards leave it empty while that row is saved inactive, which kept the buttons hidden.
        /// </summary>
        private GameObject ActionsRow
        {
            get
            {
                if (actionsRow)
                    return actionsRow;

                var button = confirmButton ? confirmButton : declineButton;
                var parent = button ? button.transform.parent : null;
                if (!parent || parent == transform)
                    return null;

                // Only treat it as the actions row when it holds nothing but the action buttons
                foreach (Transform child in parent)
                    if ((!confirmButton || child != confirmButton.transform) && (!declineButton || child != declineButton.transform))
                        return null;

                return parent.gameObject;
            }
        }

        private void OnConfirmButtonClicked() => RunAction(onConfirmAction, "confirm").Forget();

        private void OnDeclineButtonClicked() => RunAction(onDeclineAction, "decline").Forget();

        /// <summary>
        /// Runs a confirm/decline action with both buttons locked, so the notification can't be answered twice.
        /// </summary>
        private async UniTaskVoid RunAction(AsyncActionHandler<PlayerNotificationData> action, string actionName)
        {
            if (action == null)
            {
                Debug.LogWarning($"No {actionName} action assigned for this notification entry.");
                return;
            }

            if (isRunningAction)
                return;

            var actionData = PlayerNotificationData;
            isRunningAction = true;
            SetButtonsInteractable(false);
            try
            {
                await action(actionData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing {actionName} action: {e.Message}");
            }
            finally
            {
                // The entry may have been reset and reused for another notification while the action ran
                if (this && ReferenceEquals(PlayerNotificationData, actionData))
                {
                    isRunningAction = false;
                    SetButtonsInteractable(true);
                }
            }
        }

        private void SetButtonsInteractable(bool isInteractable)
        {
            if (confirmButton)
                confirmButton.interactable = isInteractable;
            if (declineButton)
                declineButton.interactable = isInteractable;
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
