using Cysharp.Threading.Tasks;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.FriendSystem.PartyController;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Represents a UI entry for a party member, managing display, actions for promoting or kicking members, and slot
    /// availability within a party system.
    /// </summary>
    public class PartyEntry : MonoBehaviour
    {
        [SerializeField] private bool turningOffDisocuppy;
        [SerializeField] private Image profileIcon;
        [SerializeField] private TMP_Text usernameLabel;
        [SerializeField] private TMP_Text userIDLabel;
        [SerializeField] private Button promAsLeaderButton;
        [SerializeField] private Button kickButton;

        [Header("Party Objects Containers")]
        [SerializeField] private GameObject leaderObject;
        [SerializeField] private GameObject hostingMemberObject;
        [SerializeField] private GameObject partyMemberObject;
        [SerializeField] private GameObject availableSlotObject;

        private AsyncActionHandler<PartyEntryData?> onPromMemberAsLeader;
        private AsyncActionHandler<PartyEntryData?> onKickMember;
        private Func<string> getCurrentPlayerID;
        private Func<bool> checkIfIsHost;
        private AsyncFuncHandler<Sprite, string> getProfileIcon;

        public bool IsLeader { get; private set; }
        public bool IsHosting => checkIfIsHost?.Invoke() ?? false;
        public PartyEntryData? PartyEntryData { get; private set; }

        private void Awake()
        {
            if (promAsLeaderButton)
                promAsLeaderButton.onClick.AddListener(PromAsLeader);

            if (kickButton)
                kickButton.onClick.AddListener(KickMember);
        }

        private void Start()
        {
            // By default, turn off the slot visibility
            if (turningOffDisocuppy)
                SetActive(false);
            else
                DetermineSlotAvailability(false);
        }

        /// <summary>
        /// Initializes the party member handler with actions and functions for promoting, kicking, retrieving player
        /// ID, checking host status, and obtaining profile icons.
        /// </summary>
        /// <param name="onPromMemberAsLeader">Handler invoked when a party member is promoted to leader.</param>
        /// <param name="onKickMember">Handler invoked when a party member is kicked.</param>
        /// <param name="getCurrentPlayerID">Function to retrieve the current player's ID.</param>
        /// <param name="checkIfIsHost">Function to determine if the current player is the host.</param>
        /// <param name="getProfileIcon">Handler to obtain a profile icon given a string identifier.</param>
        internal void Initialize
            (AsyncActionHandler<PartyEntryData?> onPromMemberAsLeader, 
            AsyncActionHandler<PartyEntryData?> onKickMember,
            Func<string> getCurrentPlayerID,
            Func<bool> checkIfIsHost,
            AsyncFuncHandler<Sprite, string> getProfileIcon)
        { 
            this.onPromMemberAsLeader = onPromMemberAsLeader;
            this.onKickMember = onKickMember;
            this.getProfileIcon = getProfileIcon;
            this.getCurrentPlayerID = getCurrentPlayerID;
            this.checkIfIsHost = checkIfIsHost;
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
        /// Assigns party entry data and leader status to the slot, updates UI elements, and manages slot availability
        /// and visibility.
        /// </summary>
        /// <param name="partyEntryData">Party entry data to assign to the slot.</param>
        /// <param name="isLeader">Indicates whether the slot represents the party leader.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        internal async UniTask OccupySlot(PartyEntryData? partyEntryData, bool isLeader)
        {
            PartyEntryData = partyEntryData;
            if (!PartyEntryData.HasValue)
                Debug.LogWarning("Couldn't assign data to party because it doesn't have value");

            IsLeader = isLeader;

            if (usernameLabel && !string.IsNullOrEmpty(PartyEntryData?.PlayerName))
                usernameLabel.text = PartyEntryData?.PlayerName;
            else
                Debug.LogWarning("PartEntry username label or its new content is null");

            if (userIDLabel && !string.IsNullOrEmpty(PartyEntryData?.PlayerID))
                userIDLabel.text = PartyEntryData?.PlayerID;
            else
                Debug.LogWarning("PartEntry username label or its new content is null");

            if (profileIcon && getProfileIcon != null)
            { 
                var sprite = await getProfileIcon.Invoke(PartyEntryData?.ProfileIconID);
                profileIcon.sprite = sprite;
            }
            else
                Debug.LogWarning("PartEntry profile icon or its getProfileIcon function is null");

            DetermineSlotAvailability(PartyEntryData is not null);

            if (turningOffDisocuppy)
                SetActive(true);

            if (leaderObject)
                leaderObject.SetActive(IsLeader);
            else
                Debug.LogWarning("Couldn't determine the visibility of the leader object because its reference is null");

            if (hostingMemberObject)
            {
                var isCurrentPlayer = getCurrentPlayerID?.Invoke() == PartyEntryData?.PlayerID;
                hostingMemberObject.SetActive(IsHosting || isCurrentPlayer);
            }
            else
                Debug.LogWarning("Couldn't determine the visibility of the no leader object because its reference is null");
        }

        /// <summary>
        /// Resets the slot by clearing party data, updating leader and hosting status, adjusting object visibility, and
        /// determining slot availability.
        /// </summary>
        public void CleanSlot()
        {
            PartyEntryData = null;
            IsLeader = false;

            if (leaderObject)
                leaderObject.SetActive(IsLeader);
            else
                Debug.LogWarning("Couldn't determine the visibility of the leader object because its reference is null");

            if (hostingMemberObject)
                hostingMemberObject.SetActive(IsHosting);
            else
                Debug.LogWarning("Couldn't determine the visibility of the no leader object because its reference is null");

            DetermineSlotAvailability(false);

            if (turningOffDisocuppy)
                SetActive(false);
        }

        /// <summary>
        /// Sets the active state of partyMemberObject and availableSlotObject based on slot occupancy.
        /// </summary>
        /// <param name="isSlotOccupied">Indicates whether the slot is currently occupied.</param>
        private void DetermineSlotAvailability(bool isSlotOccupied)
        {
            if (partyMemberObject)
                partyMemberObject.SetActive(isSlotOccupied);

            if (availableSlotObject)
                availableSlotObject.SetActive(!isSlotOccupied);
        }

        /// <summary>
        /// Attempts to promote a party member to leader asynchronously, handling errors and UI state updates.
        /// </summary>
        internal async void PromAsLeader()
        {
            if (!IsHosting)
            {
                Debug.LogWarning($"Couldn't prom member to leader because it is not the hosting");
                RefreshEntries();
                return;
            }

            if (PartyEntryData is null)
            {
                Debug.LogWarning($"Couldn't prom member to leader because its data is null");
                RefreshEntries();
                return;
            }

            if (onPromMemberAsLeader is null)
            {
                Debug.LogWarning($"Couldn't prom member {PartyEntryData.Value.PlayerName} to leader because main action is null");
                RefreshEntries();
                return;
            }

            if (promAsLeaderButton)
                promAsLeaderButton.interactable = false;
            try
            {
                await onPromMemberAsLeader.Invoke(PartyEntryData.Value);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                RefreshEntries();
            }
            finally
            { 
                if (promAsLeaderButton)
                    promAsLeaderButton.interactable = true;
            }
        }

        /// <summary>
        /// Attempts to remove a party member, handling permission checks, null data, and UI updates.
        /// </summary>
        internal async void KickMember()
        {
            var isCurrentPlayer = getCurrentPlayerID?.Invoke() == PartyEntryData?.PlayerID;
            if (!IsHosting && !isCurrentPlayer)
            {
                Debug.LogWarning($"Couldn't kick member because it is not the hosting or himself");
                RefreshEntries();
                return;
            }

            if (PartyEntryData is null)
            {
                Debug.LogWarning($"Couldn't kick member because its data is null");
                RefreshEntries();
                return;
            }

            if (onKickMember is null)
            {
                Debug.LogWarning($"Couldn't kick member {PartyEntryData.Value.PlayerName} because main action is null");
                RefreshEntries();
                return;
            }

            if (kickButton)
                kickButton.interactable = false;
            try
            {
                await onKickMember.Invoke(PartyEntryData.Value);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                RefreshEntries();
            }
            finally
            {
                if (kickButton)
                    kickButton.interactable = true;
            }

            DetermineSlotAvailability(false);

            if (turningOffDisocuppy)
                SetActive(true);
        }

        /// <summary>
        /// Refreshes the party entries in the main party members controller.
        /// </summary>
        private void RefreshEntries()
        {
            // Refresh the party entries in the main party members controller
            if (PartyController.MainPartyMembers)
            { 
                Debug.Log("Refreshing party entries from PartyEntry");
                PartyController.MainPartyMembers.MainRefreshPartyEntries();
            }
            else
                Debug.LogWarning("Couldn't refresh party entries because the main party members is null");
        }
    }
}
