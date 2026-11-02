using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.FriendSystem.FriendManager;
using static ProDomino.FriendSystem.PartyController;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Represents a UI entry for a friend, displaying their name, ID, status, and providing options to invite or remove
    /// them from the list.
    /// </summary>
    internal class FriendEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text friendNameLabel;
        [SerializeField] private TMP_Text friendIDLabel;
        [SerializeField] private TMP_Text friendStatusLabel;
        [SerializeField] private Image statusIndicatorImage;
        [SerializeField] private CustomButtonUI inviteButton;
        [SerializeField] private CustomButtonUI RemoveButton;

        [Header("Avatar (optional)")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Sprite defaultAvatarSprite;

        [Header("Actions Menu (optional)")]
        [Tooltip("Button that opens the actions menu holding the invite/remove buttons. Leave empty to show them inline.")]
        [SerializeField] private Button actionsMenuButton;
        [SerializeField] private GameObject actionsMenuPanel;

        [Header("Friend Request (optional)")]
        [Tooltip("Accept button shown for received friend requests. Leave empty to clone the invite button.")]
        [SerializeField] private CustomButtonUI acceptRequestButton;
        [Tooltip("Decline button shown for received friend requests. Leave empty to clone the remove button.")]
        [SerializeField] private CustomButtonUI declineRequestButton;
        [SerializeField] private string acceptRequestText = "Accept";
        [SerializeField] private string declineRequestText = "Decline";
        [SerializeField] private string requestStatusText = "Friend Request";

        [Header("Status Label Colors (optional)")]
        [SerializeField] private bool isTintingStatusLabel;
        [SerializeField] private Color onlineStatusLabelColor = new(0.902f, 0.902f, 0.906f, 1f);
        [SerializeField] private Color otherStatusLabelColor = new(0.541f, 0.541f, 0.561f, 1f);

        [Header("Status Indicator Colors")]
        [SerializeField] private Color onlineStatusColor = ColorUtility.TryParseHtmlString("#3FDF00", out var onlineColor) ? onlineColor : Color.green; 
        [SerializeField] private Color offlineStatusColor = ColorUtility.TryParseHtmlString("#DF0000", out var onlineColor) ? onlineColor : Color.red;
        [SerializeField] private Color busyStatusColor = ColorUtility.TryParseHtmlString("#FFC651", out var onlineColor) ? onlineColor : Color.yellow;
        [SerializeField] private Color unknownlineStatusColor = ColorUtility.TryParseHtmlString("#CCCCCC", out var onlineColor) ? onlineColor : Color.grey;

        private Action<FriendsEntryData?> onInviteFriendToPlay;
        private Action<FriendsEntryData?> onRemoveFriendFromList;
        private Func<IReadOnlyList<PartyEntryData?>> getPartyMembers;
        private Action<FriendsEntryData?> onAcceptRequest;
        private Action<FriendsEntryData?> onDeclineRequest;

        internal FriendsEntryData? FriendsEntryData { get; private set; }

        /// <summary>
        /// Whether this card shows a received friend request instead of a friend.
        /// </summary>
        internal bool IsIncomingRequest { get; private set; }
        internal IReadOnlyList<PartyEntryData?> PartyMembers => getPartyMembers?.Invoke();

        private void Awake()
        {
            if (inviteButton)
                inviteButton.onClick.AddListener(OnPressInviteButton);

            if (RemoveButton)
                RemoveButton.onClick.AddListener(OnPressRemoveButton);

            if (actionsMenuButton)
                actionsMenuButton.onClick.AddListener(ToggleActionsMenu);

            // Without dedicated buttons, clone the menu buttons so the request actions keep the card style
            if (!acceptRequestButton)
                acceptRequestButton = CloneRequestButton(inviteButton, "Button_AcceptRequest", acceptRequestText);
            if (!declineRequestButton)
                declineRequestButton = CloneRequestButton(RemoveButton, "Button_DeclineRequest", declineRequestText);

            if (acceptRequestButton)
                acceptRequestButton.onClick.AddListener(OnPressAcceptRequestButton);

            if (declineRequestButton)
                declineRequestButton.onClick.AddListener(OnPressDeclineRequestButton);

            SetRequestButtonsVisible(false);
            SetActionsMenuVisible(false);
        }

        /// <summary>
        /// Duplicates a card button next to the original, keeping its look but with its own label and listeners.
        /// </summary>
        private static CustomButtonUI CloneRequestButton(CustomButtonUI source, string name, string text)
        {
            if (!source)
                return null;

            var clone = Instantiate(source, source.transform.parent);
            clone.name = name;

            // Instantiate copies the persistent calls only; drop them so the clone runs just its own action
            clone.onClick = new();

            if (clone.GetMainText() is not null)
                clone.SetMainText(text);
            else if (clone.GetComponentInChildren<TMP_Text>(true) is { } label)
                label.text = text;

            return clone;
        }

        private void SetRequestButtonsVisible(bool isVisible)
        {
            if (acceptRequestButton)
                acceptRequestButton.gameObject.SetActive(isVisible);
            if (declineRequestButton)
                declineRequestButton.gameObject.SetActive(isVisible);
        }

        private void OnDisable()
        {
            SetActionsMenuVisible(false);
        }

        // Only one card keeps its actions menu open at a time
        private static FriendEntry openedMenuOwner;

        private bool isActionsMenuVisible;

        private void ToggleActionsMenu()
        {
            SetActionsMenuVisible(!isActionsMenuVisible);
        }

        private void SetActionsMenuVisible(bool isVisible)
        {
            if (!actionsMenuPanel)
                return;

            if (isVisible && openedMenuOwner && openedMenuOwner != this)
                openedMenuOwner.SetActionsMenuVisible(false);

            isActionsMenuVisible = isVisible;

            // A CanvasGroup keeps the menu buttons active while hidden: CustomButtonUI reads its default
            // interactable state in Awake, so an inactive button configured before its Awake stays disabled
            if (actionsMenuPanel.TryGetComponent<CanvasGroup>(out var menuGroup))
            {
                menuGroup.alpha = isVisible ? 1f : 0f;
                menuGroup.interactable = isVisible;
                menuGroup.blocksRaycasts = isVisible;
            }
            else
                actionsMenuPanel.SetActive(isVisible);

            if (isVisible)
                openedMenuOwner = this;
            else if (openedMenuOwner == this)
                openedMenuOwner = null;
        }

        /// <summary>
        /// Assigns actions for inviting and removing friends, and a function to retrieve party members.
        /// </summary>
        /// <param name="onInviteFriendToPlay">Action invoked when inviting a friend to play.</param>
        /// <param name="onRemoveFriendFromList">Action invoked when removing a friend from the list.</param>
        /// <param name="getPartyMembers">Function that returns the current party members.</param>
        internal void Initialize
            (Action<FriendsEntryData?> onInviteFriendToPlay, 
            Action<FriendsEntryData?> onRemoveFriendFromList,
            Func<IReadOnlyList<PartyEntryData?>> getPartyMembers)
        { 
            this.onInviteFriendToPlay = onInviteFriendToPlay;
            this.onRemoveFriendFromList = onRemoveFriendFromList;
            this.getPartyMembers = getPartyMembers;
        }

        /// <summary>
        /// Assigns the actions used when the card shows a received friend request.
        /// </summary>
        /// <param name="onAcceptRequest">Action invoked when accepting the friend request.</param>
        /// <param name="onDeclineRequest">Action invoked when declining the friend request.</param>
        internal void InitializeRequestActions
            (Action<FriendsEntryData?> onAcceptRequest,
            Action<FriendsEntryData?> onDeclineRequest)
        {
            this.onAcceptRequest = onAcceptRequest;
            this.onDeclineRequest = onDeclineRequest;
        }

        /// <summary>
        /// Configures the friend entry UI elements based on the provided friend data and categories shown.
        /// </summary>
        /// <param name="friendsEntryData">The friend entry data to display in the UI.</param>
        /// <param name="categoriesShown">Specifies which categories are currently shown.</param>
        internal bool HasAvatar => avatarImage;

        /// <summary>
        /// Shows the given profile icon, or the default avatar when it is null.
        /// </summary>
        internal void SetAvatar(Sprite sprite)
        {
            if (avatarImage)
                avatarImage.sprite = sprite ? sprite : defaultAvatarSprite;
        }

        /// <param name="isIncomingRequest">Whether the data is a received friend request, shown with accept and decline actions.</param>
        internal void Configure(FriendsEntryData? friendsEntryData, CategoriesShown categoriesShown, bool isIncomingRequest = false)
        {
            // A reused card must not keep the previous friend's icon while the new one loads
            if (FriendsEntryData?.TargetID != friendsEntryData?.TargetID)
                SetAvatar(null);

            FriendsEntryData = friendsEntryData;
            IsIncomingRequest = isIncomingRequest && friendsEntryData.HasValue;
            SetRequestButtonsVisible(IsIncomingRequest);

            if (!FriendsEntryData.HasValue)
            {
                Debug.LogWarning("Friend entry value tried to assign is null");
                return;
            }

            if (friendNameLabel && !string.IsNullOrEmpty(FriendsEntryData.Value.Name))
                friendNameLabel.text = FriendsEntryData.Value.Name;
            else
                Debug.LogWarning("The name label is null or the parameter 'Name' of the friend entry data is null or empty");

            if (friendIDLabel && !string.IsNullOrEmpty(FriendsEntryData.Value.TargetID))
                friendIDLabel.text = FriendsEntryData.Value.TargetID;
            else
                Debug.LogWarning("The Id label is null or the parameter 'Id' of the friend entry data is null or empty");

            if (friendStatusLabel)
            {
                friendStatusLabel.text = IsIncomingRequest
                    ? requestStatusText
                    : FriendsEntryData.Value.Availability.ToString();

                if (isTintingStatusLabel)
                    friendStatusLabel.color = FriendsEntryData.Value.Availability is Availability.Online && !IsIncomingRequest
                        ? onlineStatusLabelColor
                        : otherStatusLabelColor;
            }
            else
                Debug.LogWarning("The status label is null");

            // A pending request has no shared presence yet, so its indicator stays neutral
            if (statusIndicatorImage)
                statusIndicatorImage.color = IsIncomingRequest ? unknownlineStatusColor : FriendsEntryData.Value.Availability switch
                {
                    Availability.Online => onlineStatusColor,
                    Availability.Offline => offlineStatusColor,
                    Availability.Busy => busyStatusColor,
                    Availability.Unknown => unknownlineStatusColor,
                    _ => Color.white
                };

            //  Enable the invite button only if the friend is online AND not already in the party.
            // ignoreDefault: the card can be configured before the buttons run Awake (hidden section);
            // without it CustomButtonUI keeps the pre-Awake "not interactable" default forever
            if (inviteButton)
                inviteButton.SetButtonInteractable
                    (FriendsEntryData.HasValue 
                    && FriendsEntryData.Value.Availability is Availability.Online
                    && (PartyMembers == null || !PartyMembers.Any(x => x?.PlayerID == FriendsEntryData.Value.TargetID)),
                    ignoreDefault: true);

            // Only allow removing friends
            if (RemoveButton)
                RemoveButton.SetButtonInteractable(FriendsEntryData.HasValue, ignoreDefault: true);

            if (acceptRequestButton)
                acceptRequestButton.SetButtonInteractable(IsIncomingRequest, ignoreDefault: true);
            if (declineRequestButton)
                declineRequestButton.SetButtonInteractable(IsIncomingRequest, ignoreDefault: true);

            UpdateCategories(categoriesShown);
        }

        /// <summary>
        /// Sets the active state of the associated GameObject.
        /// </summary>
        /// <param name="isActive">True to activate the GameObject; false to deactivate it.</param>
        internal void SetActive(bool isActive)
        {
            if (gameObject)
                gameObject.SetActive(isActive);
            else
                Debug.LogWarning("GameObject is not set. Please assign a GameObject to control its active state.");
        }

        /// <summary>
        /// Updates the visibility of UI elements based on the specified categories to be shown.
        /// </summary>
        /// <param name="categories">Flags indicating which categories should be visible.</param>
        private void UpdateCategories(CategoriesShown categories)
        {
            // Set visibility based on the current categories shown
            if (friendNameLabel)
                friendNameLabel.gameObject.SetActive(categories.HasFlag(CategoriesShown.Name));
            if (friendIDLabel)
                friendIDLabel.gameObject.SetActive(categories.HasFlag(CategoriesShown.ID));
            if (friendStatusLabel)
                friendStatusLabel.gameObject.SetActive(categories.HasFlag(CategoriesShown.Status));
            // A received request only offers accepting or declining it
            if (inviteButton)
                inviteButton.gameObject.SetActive(categories.HasFlag(CategoriesShown.Invite) && !IsIncomingRequest);
            if (RemoveButton)
                RemoveButton.gameObject.SetActive(categories.HasFlag(CategoriesShown.Remove) && !IsIncomingRequest);
        }

        /// <summary>
        /// Invokes the friend invitation event with the current friend's entry data.
        /// </summary>
        private void OnPressInviteButton()
        {
            if (onInviteFriendToPlay is null)
            {
                Debug.LogWarning("Event registered when a friend is invite to a party is null");
                return;
            }

            SetActionsMenuVisible(false);
            onInviteFriendToPlay.Invoke(FriendsEntryData);
        }

        /// <summary>
        /// Invokes the event to remove a friend from the list if it is registered.
        /// </summary>
        private void OnPressRemoveButton()
        {
            if (onRemoveFriendFromList is null)
            {
                Debug.LogWarning("Event registered when a friend is remove from the friend list is null");
                return;
            }

            SetActionsMenuVisible(false);
            onRemoveFriendFromList.Invoke(FriendsEntryData);
        }

        /// <summary>
        /// Invokes the event to accept the received friend request shown by this card.
        /// </summary>
        private void OnPressAcceptRequestButton()
        {
            if (onAcceptRequest is null)
            {
                Debug.LogWarning("Event registered when a friend request is accepted is null");
                return;
            }

            SetActionsMenuVisible(false);
            onAcceptRequest.Invoke(FriendsEntryData);
        }

        /// <summary>
        /// Invokes the event to decline the received friend request shown by this card.
        /// </summary>
        private void OnPressDeclineRequestButton()
        {
            if (onDeclineRequest is null)
            {
                Debug.LogWarning("Event registered when a friend request is declined is null");
                return;
            }

            SetActionsMenuVisible(false);
            onDeclineRequest.Invoke(FriendsEntryData);
        }
    }
}
