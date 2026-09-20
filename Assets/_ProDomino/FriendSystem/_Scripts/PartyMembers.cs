using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using UnityEngine;
using UnityEngine.Events;
using static ProDomino.FriendSystem.PartyController;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Manages party member entries, handles UI updates, and provides functionality for promoting, kicking, and
    /// announcing party members within a party system.
    /// </summary>
    public class PartyMembers : MonoBehaviour
    {
        [SerializeField] private Transform partyEntryParent;
        [SerializeField] private CanvasGroup partyCanvasGroup;

        private PromptFadeController promptFadeController;
        private List<PartyEntry> partyEntries;
        private Func<IReadOnlyList<PartyEntryData?>> getPartyMembers;
        private AsyncFuncHandler<Sprite, string> getProfileIcon;
        private Func<string> getCurrentPlayerID;
        private Func<bool> checkIfIsHost;
        private Action<string> announceNewPartyMember;

        public bool IsMainPartyMembers { get; private set; }
        public IReadOnlyList<PartyEntry> PartyEntries => partyEntries;
        internal PromptFadeController PromptFadeController => promptFadeController;

        private static UnityEvent<(Func<IReadOnlyList<PartyEntryData?>>, AsyncActionHandler<PartyEntryData?>, AsyncActionHandler<PartyEntryData?>, Func<string>, Func<bool>, Action<string>, AsyncFuncHandler<Sprite, string>)> _onInitialize;
        private static AsyncActionHandler<PartyEntryData?> _onPromMemberAsLeader;
        private static AsyncActionHandler<PartyEntryData?> _onKickMember;
        private static UnityEvent<IReadOnlyList<PartyEntryData?>> _onRefreshPartyEntries;

        private void Awake()
        {
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
            partyEntries = partyEntryParent?.GetComponentsInChildren<PartyEntry>(true)?.ToList() ?? new();
            if (partyEntries is null or { Count: 0 })
            {
                Debug.LogError("Party entries are not set or empty. Please assign PartyEntry instances in the inspector.");
                return;
            }

            foreach (var entry in partyEntries)
                entry.Initialize(PromAsLeader, KickMember, GetCurrentPlayerID, IsHost, GetProfileIcon);

            _onInitialize ??= new();
            _onInitialize.AddListener(InitializeProxy);

            _onRefreshPartyEntries ??= new();
            _onRefreshPartyEntries.AddListener(RefreshPartyEntries);

            void InitializeProxy
                ((Func<IReadOnlyList<PartyEntryData?>> getPartyMembers, 
                AsyncActionHandler<PartyEntryData?> onPromMemberAsLeader, 
                AsyncActionHandler<PartyEntryData?> onKickMember, 
                Func<string> getCurrentPlayerID,
                Func<bool> checkIfIsHost,
                Action<string> announceNewPartyMember,
                AsyncFuncHandler<Sprite, string> getProfileIcon) data)
            {
                Initialize(data.getPartyMembers, data.onPromMemberAsLeader, data.onKickMember, data.getCurrentPlayerID, data.checkIfIsHost, data.announceNewPartyMember, data.getProfileIcon);
            }
        }

        private void Start()
        {
             // Update layout immediately to avoid visual glitches
            var targetRefresh = transform.parent ?? transform;
            targetRefresh?.RefreshLayoutGroupsImmediateAndRecursive();
        }

        private void OnDestroy()
        {
            if (_onRefreshPartyEntries != null)
            { 
                _onRefreshPartyEntries.RemoveAllListeners();
                _onRefreshPartyEntries = null;
            }

            if (_onInitialize != null)
            { 
                _onInitialize.RemoveAllListeners();
                _onInitialize = null;
            }

            _onPromMemberAsLeader = null;
            _onPromMemberAsLeader = null;
        }

        /// <summary>
        /// Initializes the main PartyMembers instance and sets up static event listeners for other instances.
        /// </summary>
        /// <param name="getPartyMembers">Function to retrieve the list of party members.</param>
        /// <param name="onPromMemberAsLeader">Handler invoked when a party member is promoted to leader.</param>
        /// <param name="onKickMember">Handler invoked when a party member is kicked.</param>
        /// <param name="getCurrentPlayerID">Function to retrieve the current player's ID.</param>
        /// <param name="checkIfIsHost">Function to determine if the current player is the host.</param>
        /// <param name="announceNewPartyMember">Action to announce a new party member.</param>
        /// <param name="getProfileIcon">Handler to obtain a profile icon given a string identifier.</param>
        internal void MainReferenceInitialize
            (Func<IReadOnlyList<PartyEntryData?>> getPartyMembers,
            AsyncActionHandler<PartyEntryData?> onPromMemberAsLeader,
            AsyncActionHandler<PartyEntryData?> onKickMember,
            Func<string> getCurrentPlayerID,
            Func<bool> checkIfIsHost,
            Action<string> announceNewPartyMember,
            AsyncFuncHandler<Sprite, string> getProfileIcon)
        {
            IsMainPartyMembers = true;

            // Initialize the main instance
            Initialize(getPartyMembers, onPromMemberAsLeader, onKickMember, getCurrentPlayerID, checkIfIsHost, announceNewPartyMember, getProfileIcon);

            // Invoke the static event to initialize any other instances
            _onInitialize?.Invoke((getPartyMembers, onPromMemberAsLeader, onKickMember, getCurrentPlayerID, checkIfIsHost, announceNewPartyMember, getProfileIcon));
        }

        /// <summary>
        /// Initializes the PartyMembers instance with necessary callbacks and data providers.
        /// </summary>
        /// <param name="getPartyMembers">Function to retrieve the list of party members.</param>
        /// <param name="getProfileIcon">Handler to obtain a profile icon given a string identifier.</param>
        /// <param name="onPromMemberAsLeader">Handler invoked when a party member is promoted to leader.</param>
        /// <param name="onKickMember">Handler invoked when a party member is kicked.</param>
        /// <exception cref="ArgumentNullException"></exception>
        private void Initialize
            (Func<IReadOnlyList<PartyEntryData?>> getPartyMembers,
            AsyncActionHandler<PartyEntryData?> onPromMemberAsLeader,
            AsyncActionHandler<PartyEntryData?> onKickMember,
            Func<string> getCurrentPlayerID,
            Func<bool> checkIfIsHost,
            Action<string> announceNewPartyMember,
            AsyncFuncHandler<Sprite, string> getProfileIcon)
        {
            this.getPartyMembers = getPartyMembers ?? throw new ArgumentNullException(nameof(getPartyMembers), "getPartyMembers cannot be null.");
            _onPromMemberAsLeader = onPromMemberAsLeader ?? throw new ArgumentNullException(nameof(onPromMemberAsLeader), "onPromMemberAsLeader cannot be null.");
            _onKickMember = onKickMember ?? throw new ArgumentNullException(nameof(onKickMember), "onKickMember cannot be null.");
            this.getCurrentPlayerID = getCurrentPlayerID ?? throw new ArgumentNullException(nameof(getCurrentPlayerID), "getCurrentPlayerID cannot be null.");
            this.checkIfIsHost = checkIfIsHost ?? throw new ArgumentNullException(nameof(checkIfIsHost), "checkIfIsHost cannot be null.");
            this.announceNewPartyMember = announceNewPartyMember ?? throw new ArgumentNullException(nameof(announceNewPartyMember), "announceNewPartyMember cannot be null.");
            this.getProfileIcon = getProfileIcon ?? throw new ArgumentNullException(nameof(getProfileIcon), "getProfileIcon cannot be null.");
        }

        /// <summary>
        /// Refreshes the party entries by fetching the latest party members and updating the UI.
        /// </summary>
        /// <param name="optionalList">Optional list of party members to use instead of fetching from the provider.</param>
        internal void MainRefreshPartyEntries(List<PartyEntryData?> optionalList = null)
        {
            var partyMembers = optionalList ?? getPartyMembers?.Invoke();
            if (partyMembers == null || partyEntries == null)
            {
                Debug.LogWarning("Party members or entries are null. Cannot refresh party entries.");
                return;
            }
            _onRefreshPartyEntries?.Invoke(partyMembers);
        }

        /// <summary>
        /// Updates the UI elements of each PartyEntry based on the provided party members list.
        /// </summary>
        /// <param name="partyMembers">List of party members to display in the UI.</param>
        private async void RefreshPartyEntries(IReadOnlyList<PartyEntryData?> partyMembers)
        {
            if (partyEntries == null)
            {
                Debug.LogWarning("Party members or entries are null. Cannot refresh party entries.");
                return;
            }

            partyEntries.ForEach(x => x.CleanSlot());

            var currentPlayerId = getCurrentPlayerID?.Invoke() ?? string.Empty;
            if (partyMembers is not null and { Count: > 0 })
            {
                var configurePartyMemberTasks = new List<UniTask>();

                for (int i = 0; i < partyMembers.Count; i++)
                    configurePartyMemberTasks.Add(UniTask.Create(async () =>
                    {
                        var partyMemberData = partyMembers[i];

                        // Skip null entries and log a warning
                        if (partyMemberData == null)
                        {
                            Debug.LogWarning($"Party member data at index {i} is null. Skipping this entry.");
                            return;
                        }

                        // If index exceeds available PartyEntry instances, log a warning and break
                        if (i >= partyEntries.Count)
                        {
                            Debug.LogWarning("Not enough PartyEntry instances to display all members.");
                            return;
                        }

                        var entry = partyEntries[i];
                        entry.SetActive(true);
                        await entry.OccupySlot(partyMembers[i], partyMembers[i]?.IsLeader ?? false);
                    }));

                // Await all configuration tasks to complete before proceeding
                await UniTask.WhenAll(configurePartyMemberTasks);
            }

            // Set the active state of the PartyMembers GameObject based on active PartyEntry instances
            gameObject.SetActive(partyEntries.Any(x => x.gameObject.activeSelf));

            // Update layout immediately to avoid visual glitches
            var targetRefresh = transform.parent ?? transform;
            targetRefresh?.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Promotes a party member to leader and handles UI state during the operation.
        /// </summary>
        /// <param name="friendEntryData">The data of the party member to promote.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        internal async UniTask PromAsLeader(PartyEntryData? friendEntryData)
        {
            partyCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                await (_onPromMemberAsLeader?.Invoke(friendEntryData) ?? default);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            partyCanvasGroup?.SetActive(true, isSettingAlpha: false);
        }

        /// <summary>
        /// Kicks a party member and handles UI state during the operation.
        /// </summary>
        /// <param name="friendEntryData">The data of the party member to kick.</param>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        internal async UniTask KickMember(PartyEntryData? friendEntryData)
        {
            partyCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                await (_onKickMember?.Invoke(friendEntryData) ?? default);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            partyCanvasGroup?.SetActive(true, isSettingAlpha: false);
        }

        /// <summary>
        /// Fetches the profile icon sprite for a given identifier.
        /// </summary>
        /// <param name="arg">The identifier for the profile icon.</param>
        /// <returns>A UniTask representing the asynchronous operation, returning the profile icon sprite.</returns>
        private async UniTask<Sprite> GetProfileIcon(string arg)
        {
            return await (getProfileIcon?.Invoke(arg) ?? default);
        }
        
        /// <summary>
        /// Get the current player id
        /// </summary>
        /// <returns>The current player's ID.</returns>
        private string GetCurrentPlayerID()
        {
            return getCurrentPlayerID?.Invoke() ?? default;
        }

        // <summary>
        /// Check if the currnt client is the host
        /// </summary>
        /// <returns> True if the current client is the host, otherwise false.</returns>
        private bool IsHost()
        {
            return checkIfIsHost?.Invoke() ?? default;
        }

        /// <summary>
        /// Invokes the announcement for a change in party members using the specified player ID.
        /// </summary>
        /// <param name="playerId">The ID of the player whose party membership has changed.</param>
        public void AnnouncePartyMembersChanged(string playerId)
        {
            announceNewPartyMember?.Invoke(playerId);
        }
    }
}
