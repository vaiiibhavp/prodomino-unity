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

        [Header("Status Indicator Colors")]
        [SerializeField] private Color onlineStatusColor = ColorUtility.TryParseHtmlString("#3FDF00", out var onlineColor) ? onlineColor : Color.green; 
        [SerializeField] private Color offlineStatusColor = ColorUtility.TryParseHtmlString("#DF0000", out var onlineColor) ? onlineColor : Color.red;
        [SerializeField] private Color busyStatusColor = ColorUtility.TryParseHtmlString("#FFC651", out var onlineColor) ? onlineColor : Color.yellow;
        [SerializeField] private Color unknownlineStatusColor = ColorUtility.TryParseHtmlString("#CCCCCC", out var onlineColor) ? onlineColor : Color.grey;

        private Action<FriendsEntryData?> onInviteFriendToPlay;
        private Action<FriendsEntryData?> onRemoveFriendFromList;
        private Func<IReadOnlyList<PartyEntryData?>> getPartyMembers;

        internal FriendsEntryData? FriendsEntryData { get; private set; }
        internal IReadOnlyList<PartyEntryData?> PartyMembers => getPartyMembers?.Invoke();

        private void Awake()
        {
            if (inviteButton)
                inviteButton.onClick.AddListener(OnPressInviteButton);

            if (RemoveButton)
                RemoveButton.onClick.AddListener(OnPressRemoveButton);
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
        /// Configures the friend entry UI elements based on the provided friend data and categories shown.
        /// </summary>
        /// <param name="friendsEntryData">The friend entry data to display in the UI.</param>
        /// <param name="categoriesShown">Specifies which categories are currently shown.</param>
        internal void Configure(FriendsEntryData? friendsEntryData, CategoriesShown categoriesShown)
        {
            FriendsEntryData = friendsEntryData;

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
                friendStatusLabel.text = FriendsEntryData.Value.Availability.ToString();
            else
                Debug.LogWarning("The status label is null");

            if (statusIndicatorImage)
                statusIndicatorImage.color = FriendsEntryData.Value.Availability switch
                {
                    Availability.Online => onlineStatusColor,
                    Availability.Offline => offlineStatusColor,
                    Availability.Busy => busyStatusColor,
                    Availability.Unknown => unknownlineStatusColor,
                    _ => Color.white
                };

            //  Enable the invite button only if the friend is online AND not already in the party.
            if (inviteButton)
                inviteButton.SetButtonInteractable
                    (FriendsEntryData.HasValue 
                    && FriendsEntryData.Value.Availability is Availability.Online
                    && (PartyMembers == null || !PartyMembers.Any(x => x?.PlayerID == FriendsEntryData.Value.TargetID)));

            // Only allow removing friends
            if (RemoveButton)
                RemoveButton.SetButtonInteractable(FriendsEntryData.HasValue);

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
            if (inviteButton)
                inviteButton.gameObject.SetActive(categories.HasFlag(CategoriesShown.Invite));
            if (RemoveButton)
                RemoveButton.gameObject.SetActive(categories.HasFlag(CategoriesShown.Remove));
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

            onRemoveFriendFromList.Invoke(FriendsEntryData);
        }
    }
}
