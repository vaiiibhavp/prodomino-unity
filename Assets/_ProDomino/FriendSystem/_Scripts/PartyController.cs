using Cysharp.Threading.Tasks;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using Unity.Services.Relay;
using UnityEngine;
using static ProDomino.FriendSystem.FriendManager;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Manages party session operations, including joining, inviting, kicking members, handling party events, and
    /// synchronizing party member data within a multiplayer environment.
    /// </summary>
    public partial class PartyController : MonoBehaviour
    {
        [Header("Party Fields")]
        [SerializeField] private PartyMembers mainPartyMembers;
        [SerializeField] private bool isInviteAllowedInInactiveRoom;

        private AuthManager authManager;
        private DictionaryService dictionaryService;
        private PromptFadeController promptFadeController;

        internal IReadOnlyList<PartyEntryData?> PartyMembers => getPartyPlayers
            ?.Invoke()
            ?.Select(x => getPlayerData.Invoke(x.clientId))
            ?.Where(x => x is not null)
            ?.ToList();
        
        /// <summary>
        /// Try to get the join code of the current session
        /// </summary>
        private static Func<string> getJoinCode;

        /// <summary>
        /// Try to get the client and player id of your party (excluding the no invited ones)
        /// </summary>
        private static Func<(int clientId, string playerId)[]> getPartyPlayers;

        /// <summary>
        /// Try to get the client and player id of your session (including the no invited ones)
        /// </summary>
        private static Func<(int clientId, string playerId)[]> getRelayPlayers;

        /// <summary>
        /// According the Client Id arg, obtain its correspondly icon profile sprite
        /// </summary>
        private static Func<int, PartyEntryData?> getPlayerData;
        
        /// <summary>
        /// Get the data of the this client party entry
        /// </summary>
        private static Func<PartyEntryData?> getLocalPlayerData;

        /// <summary>
        /// Try to determine if the player is in a active match
        /// </summary>
        private static Func<bool> checkIfIsInMatch;
        
        /// <summary>
        /// Try to determine if the player is in a active relay session
        /// </summary>
        private static Func<bool> checkIfIsRelay;
        
        /// <summary>
        /// Async function that that tries to create or join a lobby acording the JoinCode pased as argument<br></br>
        /// MatchManager initialize statically (because this assembly couldn't have referenced MatchManager one/>
        /// </summary>
        private static AsyncActionHandler<string> createOrJoinPartySessionProxy;

        /// <summary>
        /// Async function that that tries to kick a member of the party<br></br>
        /// MatchManager initialize statically (because this assembly couldn't have referenced MatchManager one/>
        /// </summary>
        private static AsyncActionHandler<int, string> kickRelayMember;

        /// <summary>
        /// Async function that that tries to create or join a lobby acording the JoinCode pased as argument<br></br>
        /// MatchManager initialize statically (because this assembly couldn't have referenced MatchManager one/>
        /// </summary>
        private static AsyncActionHandler<string> onPlayerJoined;

        /// <summary>
        /// Async function that that tries to create or join a lobby acording the JoinCode pased as argument<br></br>
        /// MatchManager initialize statically (because this assembly couldn't have referenced MatchManager one/>
        /// </summary>
        private static AsyncActionHandler<string> onPlayerLeave;

        public static bool IsRelay => checkIfIsRelay?.Invoke() ?? false;
        public static int PartyCount => getPartyPlayers?.Invoke()?.Length ?? 0;
        public static string CurrentPlayerID { get; private set; }
        public static PartyMembers _mainPartyMembers;
        public static PartyMembers MainPartyMembers 
        { 
            get 
            { 
                return _mainPartyMembers != null 
                    ? _mainPartyMembers 
                    : _mainPartyMembers = FindObjectsByType<PartyMembers>(findObjectsInactive: FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)?.FirstOrDefault(x => x.IsMainPartyMembers);
            } 
            private set { _mainPartyMembers = value; } 
        }

        /// <summary>
        /// Checks if the user is authenticated via Unity Gaming Services or any provider.
        /// </summary>
        public bool IsAuthenticated => gameManager?.IsAuthenticated ?? false;

        public bool IsAlreadyInitialized { get; private set; }

        internal void Awake()
        {
            Awake_FriendListController();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
        }

        internal async void Start()
        {
            Start_FriendListController();

            if (!gameManager || !friendManager)
            {
                Debug.LogErrorFormat($"Could not initialize the controller due GameManager or FriendManager are null");
                return;
            }

            await UniTask.WaitUntil(() => gameManager.IsAlreadyInitialized && friendManager.IsAlreadyInitialized);

            IsAlreadyInitialized = true;
            CurrentPlayerID = authManager?.UUID;

            if (mainPartyMembers) 
            {
                mainPartyMembers.MainReferenceInitialize
                    (() => PartyMembers,
                    PromAsLeader, 
                    KickMember,
                    () => authManager.UUID,
                    () => PartyMembers?.Any(x => x is not null && x.Value.IsLeader && !string.IsNullOrEmpty(x.Value.PlayerID) && x?.PlayerID == authManager.UUID) ?? false,
                    _playerId =>
                    {
                        // Check if the player is registered in the array of party members to be able to remove it
                        var playerToAddBasicInfo = PartyMembers?.FirstOrDefault(x => x?.PlayerID == _playerId);
                        if (!playerToAddBasicInfo.HasValue || string.IsNullOrEmpty(playerToAddBasicInfo.Value.PlayerID))
                        {
                            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't announce new member because the player ID {_playerId} is not found...</color>");
                            return;
                        }

                        promptFadeController?.Fade($"{playerToAddBasicInfo.Value.PlayerName} was added into the party");
                    },
                    _iconId => gameManager.GetSpriteAsync(_iconId, Consts.CollectionKeys.Icons));

                // Once the main party member reference is initialized, register it as a static reference
                if (!MainPartyMembers
                    && mainPartyMembers != null
                    && mainPartyMembers.IsMainPartyMembers)
                    MainPartyMembers = mainPartyMembers;
            }
        }

        public void OnDestroy()
        {
            OnDestroy_FriendListController();
        }

        /// <summary>
        /// Initializes the MatchManager with delegates for party session management, relay member actions, player data
        /// retrieval, join code access, and match state checks.
        /// </summary>
        /// <param name="_createOrJoinPartySessionProxy">Delegate for creating or joining a party session.</param>
        /// <param name="_kickRelayMember">Delegate for kicking a relay member by client ID and player ID.</param>
        /// <param name="_getRelayPlayers">Function to retrieve relay player information.</param>
        /// <param name="_getPartyPlayers">Function to retrieve party player information.</param>
        /// <param name="_getPlayerData">Function to get party entry data for a specified client ID.</param>
        /// <param name="_getLocalPlayerData">Function to get party entry data for the local player.</param>
        /// <param name="_getJoinCode">Function to retrieve the current join code.</param>
        /// <param name="_checkIfIsInMatch">Function to check if currently in a match.</param>
        /// <param name="_checkIfIsRelay">Function to check if currently in relay mode.</param>
        public static void Initialize_MatchManager
            (AsyncActionHandler<string> _createOrJoinPartySessionProxy,
            AsyncActionHandler<int, string> _kickRelayMember,
            Func<(int clientId, string playerId)[]> _getRelayPlayers,
            Func<(int clientId, string playerId)[]> _getPartyPlayers,
            Func<int, PartyEntryData?> _getPlayerData,
            Func<PartyEntryData?> _getLocalPlayerData,
            Func<string> _getJoinCode,
            Func<bool> _checkIfIsInMatch,
            Func<bool> _checkIfIsRelay)
        {
            createOrJoinPartySessionProxy = _createOrJoinPartySessionProxy;
            kickRelayMember = _kickRelayMember;

            getRelayPlayers = _getRelayPlayers;
            getPartyPlayers = _getPartyPlayers;
            getPlayerData = _getPlayerData;
            getLocalPlayerData = _getLocalPlayerData;
            getJoinCode = _getJoinCode;
            checkIfIsInMatch = _checkIfIsInMatch;
            checkIfIsRelay = _checkIfIsRelay;
        }

        /// <summary>
        /// Handles the event when a player attempts to join a party.
        /// </summary>
        /// <param name="onJoinParty">Delegate to handle the join party event.</param>
        public static void HandleOnJoinParty(AsyncActionHandler<string> onJoinParty)
        {
            if (onPlayerJoined == null)
                onPlayerJoined = onJoinParty;
            else
                onPlayerJoined += onJoinParty;
        }

        /// <summary>
        /// Unhandles the event when a player attempts to join a party.
        /// </summary>
        /// <param name="onJoinParty">Delegate to unhandle the join party event.</param>
        public static void UnHandleOnJoinParty(AsyncActionHandler<string> onJoinParty)
        {
            if (onPlayerJoined != null)
                onPlayerJoined -= onJoinParty;
        }

        /// <summary>
        /// Handles the event when a player attempts to leaves a party.
        /// </summary>
        /// <param name="onLeaveParty">Delegate to handle the leave party event.</param>
        public static void HandleOnLeaveParty(AsyncActionHandler<string> onLeaveParty)
        {
            if (onPlayerLeave == null)
                onPlayerLeave = onLeaveParty;
            else
                onPlayerLeave += onLeaveParty;
        }

        /// <summary>
        /// Unhandles the event when a player attempts to leaves a party.
        /// </summary>
        /// <param name="onLeaveParty">Delegate to unhandle the leave party event.</param>
        public static void UnHandleOnLeaveParty(AsyncActionHandler<string> onLeaveParty)
        {
            if (onPlayerLeave != null)
                onPlayerLeave -= onLeaveParty;
        }

        /// <summary>
        /// Client joins the party using the join code
        /// </summary>
        /// <param name="joinCode">The join code for the party.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown when an error occurs while joining the party.</exception>
        internal async UniTask JoinToParty(string joinCode)
        {
            if (!MainPartyMembers)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't configure party entries. The MainPartMembers GUI is null</color>");
                return;
            }

            if (!IsAuthenticated)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't create friend party because the environment is not initialized or the user is not authenticated yet");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Could not join party with join code {joinCode} because the main friend manager is not initialized");
                        MainPartyMembers.MainRefreshPartyEntries();
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Could not join party with join code {joinCode} because the main friend manager is null");
                    MainPartyMembers.MainRefreshPartyEntries();
                    return;
                }
            }

            // Basic checks before trying to join a party session
            if (CheckMatchManagerFuncs())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't join party because the func to obtain data from MatchManager are null</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (checkIfIsInMatch())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't was possible accept a party invitation due you are in a match</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (string.IsNullOrEmpty(authManager.UUID))
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b>Couldn't was possible send a party invitation due player id is null or empty");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (PartyMembers?.Any(x => x?.PlayerID == authManager.UUID) ?? false)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b>Couldn't join to party because the player is already in the party");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // Attempt to create or join a matchmaking session
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(PartyController)}]</b> Attempting to join to a party session</color>");

            SetInteractivity(false);
            try
            {
                // Check if there is an active relay and if the new join code is differente of the current room's one
                if (checkIfIsRelay() && getJoinCode() != joinCode)
                { 
                    // Check if player is accepting changing relays session
                    if (isInviteAllowedInInactiveRoom)
                    { 
                        Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(PartyController)}]</b> Trying to kick yourself join to another session party with id: {joinCode} (from session with join code id: {getJoinCode()}</color>");
                        
                        var localPlayerData = getLocalPlayerData();
                        await KickMember(localPlayerData);
                    } 
                    else
                    { 
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't accept invitations if you are in an active relay session</color>");
                        return;
                    }
                }

                // Create or join a matchmaking session
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => createOrJoinPartySessionProxy.Invoke(joinCode),
                    taskId: nameof(createOrJoinPartySessionProxy),
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true,
                    returnExceptionOnError: true,
                    shouldRetrySomeTimes: true);

                if (checkIfIsRelay())
                { 
                    Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(PartyController)}]</b> Joined party session successfully</color>");
                    promptFadeController?.Fade("Joined party successfully");
                }
                else
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Failed to join to a party session</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Failed to join to a party session:\n\n{e.Message}</color>");
                MainPartyMembers.MainRefreshPartyEntries();
            }
            finally
            {
                MainPartyMembers.MainRefreshPartyEntries();

                if (friendListCanvasGroup.alpha is 0)
                    SetVisibility(false);
                else
                    SetInteractivity(true);
            }
        }

        /// <summary>
        /// Invite a friend to play by sending them the party join code
        /// </summary>
        /// <param name="playerEntryData">The data of the friend to invite.</param>
        internal async void InviteFriendToPlay(FriendsEntryData? playerEntryData)
        {
            // Basic checks before trying to kick a member
            if (!MainPartyMembers)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't configure party entries. The MainPartMembers GUI is null</color>");
                return;
            }

            if (!IsAuthenticated)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't create friend party because the environment is not initialized or the user is not authenticated yet</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                // If the friend manager is not initialized, try to get the friends service first
                if (friendManager != null)
                {
                    await friendManager.GetFriendsServiceSafeAsync();

                    if (!friendManager.IsAlreadyInitialized)
                    {
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Could not invite friend to party because the main friend manager is not initialized</color>");
                        return;
                    }
                }

                // But, if the reference is null, just log the warning and exit
                else
                {
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Could not invite friend to party because the main friend manager is null</color>");
                    MainPartyMembers.MainRefreshPartyEntries();
                    return;
                }
            }

            // Basic checks before trying to join a party session
            if (CheckMatchManagerFuncs())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't send party invitation because the func to obtain data from MatchManager are null</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (checkIfIsInMatch())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't was possible send a party invitation due you are in a match</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (!playerEntryData.HasValue || string.IsNullOrEmpty(playerEntryData.Value.TargetID))
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't was possible send a party invitation due player id is null or empty</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (PartyMembers?.Any(x => x?.PlayerID == playerEntryData.Value.TargetID) ?? false)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't join to party because the player is already in the party</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            var joinCode = getJoinCode.Invoke();

            SetInteractivity(false);
            try
            {
                // If not already created, create the party lobby
                if (!checkIfIsRelay())
                {
                    // Use the gamemanager to handle the process with loading and retry
                    await gameManager.HandleProcess_GameManagerProxy
                        (uniTask: () => createOrJoinPartySessionProxy.Invoke(null),
                        taskId: nameof(createOrJoinPartySessionProxy),
                        showLoading: true,
                        shouldIgnoreTryAgainProcess: true,
                        returnExceptionOnError: true,
                        shouldRetrySomeTimes: true);
                }

                // If the relay was just created, get again the join code
                joinCode = getJoinCode.Invoke();

                // Once session has value, invite the friend to the party
                if (!string.IsNullOrEmpty(joinCode))
                {
                    await gameManager.HandleProcess_GameManagerProxy
                        (uniTask: () => friendManager.InviteToParty(playerEntryData.Value.TargetID, joinCode),
                        taskId: nameof(friendManager.InviteToParty),
                        showLoading: true,
                        shouldIgnoreTryAgainProcess: true,
                        returnExceptionOnError: true,
                        shouldRetrySomeTimes: true);

                    promptFadeController?.Fade($"Party invitation sent to {playerEntryData.Value.Name}");
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(PartyController)}]</b> {playerEntryData.Value.Name}({playerEntryData.Value.TargetID}) invited to party with join code {joinCode}</color>");
                } 
                else
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> PlayerSession null, cannot crate host to invite party</color>");
            }
            catch (RelayServiceException e)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Error joining Relay: {e.Message}</color>");
            }
            finally
            {
                if (friendListCanvasGroup.alpha is 0)
                    SetVisibility(false);
                else
                    SetInteractivity(true);
            }
        }

        /// <summary>
        /// Completes the promotion of a party member to leader asynchronously.
        /// </summary>
        /// <param name="friendEntryData">Optional data for the party member being promoted.</param>
        /// <returns>A completed UniTask representing the asynchronous operation.</returns>
        internal UniTask PromAsLeader(PartyEntryData? friendEntryData)
        {
            // NOTE: this mechanic was abandoned in the current implementation of the party system
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Attempts to remove a specified member from the party, performing necessary validation checks before
        /// executing the kick operation.
        /// </summary>
        /// <param name="friendEntryData">Party entry data of the member to be removed.</param>
        /// <returns>A UniTask representing the asynchronous kick operation.</returns>
        internal async UniTask KickMember(PartyEntryData? friendEntryData)
        {
            // Basic checks before trying to kick a member
            if (!MainPartyMembers)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't configure party entries. The MainPartMembers GUI is null</color>");
                return;
            }

            if (!IsAuthenticated)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't create friend party because the environment is not initialized or the user is not authenticated yet</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            if (CheckMatchManagerFuncs())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because the func to obtain data from MatchManager are null</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // Check if the id of the player has value before trying to remove an empty member
            if (string.IsNullOrEmpty(friendEntryData?.PlayerID))
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because the to kick is null</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // Check if the there is an existing Relay match session. If not, is not possible kick anyone
            if (!checkIfIsRelay.Invoke())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because the session is null</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }
            
            // Check if the there is an existing Match session. If does, is not possible kick anyone until the game is active
            if (checkIfIsInMatch.Invoke())
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because there is a active match</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // If there isn't members in the party, is not possible kick anyone
            if (PartyMembers.Count is 0)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because there are no players in the session.</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // Check if the player is registered in the array of party members to be able to remove it
            var playerToKickBasicInfo = PartyMembers?.FirstOrDefault(x => x?.PlayerID == friendEntryData.Value.PlayerID);
            if (!playerToKickBasicInfo.HasValue || string.IsNullOrEmpty(playerToKickBasicInfo.Value.PlayerID))
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't kick member because the player ID {friendEntryData?.PlayerID} is not found...</color>");
                MainPartyMembers.MainRefreshPartyEntries();
                return;
            }

            // According to the player to remove, search its client id and calls the kick function 
            var (clientId, playerId) = getPartyPlayers.Invoke()?.FirstOrDefault(x => x.playerId == playerToKickBasicInfo.Value.PlayerID) ?? default;
            if (!string.IsNullOrEmpty(playerId))
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(PartyController)}]</b> Trying to kick party member with id: <b>{friendEntryData?.PlayerID}</b>...</color>");
                await kickRelayMember.Invoke(clientId, playerId);

                promptFadeController?.Fade($"{playerToKickBasicInfo.Value.PlayerName} was kicked from the party");
            }
        }

        /// <summary>
        /// Handles a player joining the party, displays a prompt if the player is not the local one, invokes the player
        /// joined event, and refreshes party entries.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player joining the party.</param>
        /// <param name="playerName">The display name of the player joining the party.</param>
        public static void OnPlayerJoin(string playerId, string playerName)
        {
            if (!MainPartyMembers)
            { 
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't configure party entries. The MainPartMembers GUI is null</color>");
                return;
            }

            // If the player that joined is not the local one, show a prompt (is necessary because the local player join is handled in the JoinToParty method)
            if (playerId != CurrentPlayerID)
                MainPartyMembers.PromptFadeController?.Fade($"{(!string.IsNullOrEmpty(playerName) ? playerName : "New player" )} joined to party successfully");

            onPlayerJoined?.Invoke(playerId);
            MainPartyMembers.MainRefreshPartyEntries();
        }

        /// <summary>
        /// Handles player departure by updating party member entries and invoking related events.
        /// </summary>
        /// <param name="playerId">The identifier of the player who is leaving.</param>
        /// <param name="updatedList">The updated list of party members after the player has left.</param>
        public static void OnPlayerLeave(string playerId, (int clientId, string playerId)[] updatedList)
        {
            if (!MainPartyMembers)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(PartyController)}]</b> Couldn't configure party entries. The MainPartMembers GUI is null</color>");
                return;
            }

            var isHost = (updatedList ?? getPartyPlayers?.Invoke())?.FirstOrDefault(x => x.clientId is 0).playerId == playerId;
            var updatedPartyMembers = updatedList
                ?.Select(x => getPlayerData.Invoke(x.clientId))
                ?.ToList();

            onPlayerLeave?.Invoke(playerId);
            MainPartyMembers?.MainRefreshPartyEntries(updatedPartyMembers);
        }

        /// <summary>
        /// Simply auxiliar tha helps to check every reference got from MatchManager
        /// </summary>
        /// <returns>True if any of the MatchManager references are null, otherwise false.</returns>
        private bool CheckMatchManagerFuncs()
        {
            return createOrJoinPartySessionProxy is null
                || kickRelayMember is null
                || checkIfIsRelay is null
                || checkIfIsInMatch is null
                || getJoinCode is null
                || getPlayerData is null
                || getLocalPlayerData is null
                || getPartyPlayers is null
                || getRelayPlayers is null;
        }

        /// <summary>
        /// Represents a party member's entry, including player ID, name, profile icon, and leader status.
        /// </summary>
        [Serializable]
        public struct PartyEntryData
        {
            public string PlayerID;
            public string PlayerName;
            public string ProfileIconID;
            public bool IsLeader;

            public PartyEntryData(string playerID, string playerName, string profileIconID, bool isLeader)
            {
                PlayerID = playerID;
                PlayerName = playerName;
                ProfileIconID = profileIconID;
                IsLeader = isLeader;
            }
        }
    }
}
