// Commented because there is another PartyController that create a party without using Relay + Multiplayer.Services + WebGL
// (this combination is not compatbile with webGL)

#if false
using Cysharp.Threading.Tasks;
using ProDomino.Authentication;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.Events;
using static ProDomino.FriendSystem.FriendManager;

namespace ProDomino.FriendSystem
{
    public partial class PartyController : MonoBehaviour
    {
        [SerializeField] private int newtworkProcessAttempt = 5;

        [Header("Party Fields")]
        [SerializeField] private PartyMembers mainPartyMembers;

        [Header("Network's Connection Events")]
        [SerializeField] private UnityEvent<string> onPlayerJoinedEvent;
        [SerializeField] private UnityEvent<string> onPlayerLeavingEvent;

        public static UnityEvent<string> OnPlayerJoinedStaticEvent;
        public static UnityEvent<string> OnPlayerLeavingStaticEvent;
        public static UnityEvent<string> OnPlayerReconnectToParty;

        internal int maxPlayers = 4;

        private AuthManager authManager;
        private DictionaryService dictionaryService;
        private PromptFadeController promptFadeController;

        private static bool isNetworkAlreadyConfigured;

        internal IReadOnlyList<PartyEntryData> PartyMembers => Session?.Players
            ?.Select(x => new PartyEntryData
                (playerID: x.Properties.GetValueOrDefault(Consts.CollectionKeys.PlayerID)?.Value?.ToString() ?? string.Empty,
                playerName: x.Properties.GetValueOrDefault(Consts.CollectionKeys.PlayerUsername)?.Value?.ToString() ?? string.Empty,
                profileIconID: x.Properties.GetValueOrDefault(Consts.CollectionKeys.PlayerProfileIcon)?.Value?.ToString() ?? string.Empty,
                isLeader: x.Id == Session.Host)) // Leader is the host
            ?.ToList(); 

        static public ISession Session { get; private set; }
        static public SessionOptions SessionOptions { get; private set; }

        internal static UnityEvent<string> OnPlayerJoinedEvent { get; private set; }
        internal static UnityEvent<string> OnPlayerLeavingEvent { get; private set; }

        /// <summary>
        /// Checks if the user is authenticated via Unity Gaming Services or any provider.
        /// </summary>
        internal bool IsAuthenticated => authManager is 
            { IsAlreadyInitialized: true, IsUGSAuthenticated: true }
            and { IsUserAuthenticatedWithCredentials: true }
            or { IsUserAuthenticatedWithProvider: true };

        public bool IsAlreadyInitialized { get; private set; }

        internal void Awake()
        {
            Awake_FriendListController();

            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            OnPlayerJoinedEvent ??= onPlayerJoinedEvent;
            OnPlayerLeavingEvent ??= onPlayerLeavingEvent;

            OnPlayerJoinedStaticEvent ??= new();
            OnPlayerLeavingStaticEvent ??= new();
            OnPlayerReconnectToParty = new();
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

            if (mainPartyMembers)
                mainPartyMembers.MainReferenceInitialize
                    (() => PartyMembers,
                    (_profileIconID) => dictionaryService.GetSprite(Consts.CollectionKeys.Icons, _profileIconID),
                    PromAsLeader, 
                    KickMember,
                    () => authManager.UUID);
            else
                Debug.LogWarning("Main Party Members is not assigned. Please assign it in the inspector.");
        }

        public void OnDestroy()
        {
            OnPlayerJoinedStaticEvent?.RemoveAllListeners();
            OnPlayerLeavingStaticEvent?.RemoveAllListeners();
            OnPlayerReconnectToParty?.RemoveAllListeners();

            OnDestroy_FriendListController();
        }

        public static void RegisterConnectionEvent(params (bool isPlayerJoined, UnityAction<string> unityAction)[] eventsToHandle)
        {
            if (eventsToHandle is null or { Length: 0 })
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            foreach (var (isPlayerJoined, unityAction) in eventsToHandle)
            {
                var eventToHandler = isPlayerJoined ? OnPlayerJoinedEvent : OnPlayerLeavingEvent;
                if (eventToHandler is not null)
                    eventToHandler.AddListener(unityAction);
            }
        }
        
        public static void HandlerOnPlayerReconnectToParty(UnityAction<string> eventToHandle)
        {
            if (eventToHandle is null)
            {
                Debug.LogWarning("The unity action tried to add as a listener is null");
                return;
            }

            OnPlayerReconnectToParty.AddListener(eventToHandle);
        }

        /// <summary>
        /// Host creates the party lobby
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal async UniTask CreatePartyLobby()
        {
            if (authManager is null
                or { IsAlreadyInitialized: false }
                or { IsUserAuthenticatedWithProvider: false })
            {
                Debug.LogWarning("Couldn't create friend party because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            // Configure player properties
            var playerProperties = new Dictionary<string, PlayerProperty>()
            {
                [Consts.CollectionKeys.PlayerID] = new(authManager.UUID),
                [Consts.CollectionKeys.PlayerUsername] = new(authManager.Username),
                [Consts.CollectionKeys.PlayerProfileIcon] = new(gameManager.PlayerProfileData.profileIconID),
            };

            // Create a session options for the party
            SessionOptions = new SessionOptions
            {
                SessionProperties = new() { [Consts.CollectionKeys.IsPartySession] = new(true.ToString()) },
                PlayerProperties = playerProperties, // Set player properties
                MaxPlayers = 4, // Set the maximum number of players for the session
                IsPrivate = true, // Set the session to private
                Name = $"Party_{authManager.UUID}_{DateTime.Now:yyyyMMdd_HHmmss}", // Unique session name,
            }
            .WithRelayNetwork();

            var attempts = 0;

            // Attempt to create or join a matchmaking session
            while (Session == null && attempts < newtworkProcessAttempt)
            {
                try
                {
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(PartyController)}]</b> Attempting to create a party session, attempts: {attempts + 1}</color>");
                    SetBlock(true);

                    // Create or join a matchmaking session
                    Session = await MultiplayerService.Instance.CreateSessionAsync(SessionOptions);

                    Session.RemovedFromSession += OnRemovedFromSession;
                    Session.PlayerJoined += OnPlayerJoined;
                    Session.PlayerLeaving += OnPlayerLeaving;

                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Party session created successfully: " +
                        $"\n\nSession ID => {Session.Id}" +
                        $"\nSession Name => {Session.Name}" +
                        $"\nJoin Code => {Session.Code}" +
                        $"\nPlayers({Session.PlayerCount}) => {string.Join(", ", Session.Players.Select(x => x.Id))}" +
                        $"\nIs Host? => {Session.IsHost}" +
                        $"\nIs Private? => {Session.IsPrivate}" +
                        $"\nIs Locked? => {Session.IsLocked}</color>");
                }
                catch (SessionException e)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Failed to create party session: {e.Message}</color>");
                    attempts++;
                    if (attempts >= newtworkProcessAttempt)
                    {
                        var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to create party session.</color>";
                        Debug.LogError(errorMessage);
                    }

                    // Wait for a short period before retrying
                    var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                    await UniTask.WaitForSeconds(delay);
                }
            }

            // Unblock interactivity
            SetBlock(false);
        }

        /// <summary>
        /// Client joins the party using the join code
        /// </summary>
        /// <param name="joinCode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal async UniTask JoinToParty(string joinCode)
        {
            if (authManager is null
                or { IsAlreadyInitialized: false }
                or { IsUserAuthenticatedWithProvider: false })
            {
                Debug.LogWarning("Couldn't create friend party because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                Debug.LogWarning("Couldn't create friend party because the main friend manager is not initialized");
                return;
            }

            if (string.IsNullOrEmpty(authManager.UUID))
            {
                Debug.LogWarning("Couldn't was possible send a party invitation due player id is null or empty");
                return;
            }

            if (PartyMembers?.Any(x => x.PlayerID == authManager.UUID) ?? false)
            {
                Debug.LogWarning("Couldn't join to party because the player is already in the party");
                return;
            }

            var attempts = 0;

            // Configure player properties
            var playerProperties = new Dictionary<string, PlayerProperty>()
            {
                [Consts.CollectionKeys.PlayerID] = new(authManager.UUID),
                [Consts.CollectionKeys.PlayerUsername] = new(authManager.Username),
                [Consts.CollectionKeys.PlayerProfileIcon] = new(gameManager.PlayerProfileData.profileIconID),
            };

            // Attempt to create or join a matchmaking session
            while (Session == null && attempts < newtworkProcessAttempt)
            {
                try
                {
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(PartyController)}]</b> Attempting to join to a party session, attempts: {attempts + 1}</color>");
                    SetBlock(true);

                    // Create or join a matchmaking session
                    Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, new() { PlayerProperties = playerProperties });

                    // Due Session events are not triggered for already connected players, manually trigger the event for each existing player
                    var partyMembers = PartyMembers;
                    if (partyMembers is not null and { Count: > 0 })
                        foreach (var member in partyMembers)
                            OnPlayerJoined(member.PlayerID);

                    // Prepare to handle session events for next players
                    Session.PlayerJoined += OnPlayerJoined;
                    Session.PlayerLeaving += OnPlayerLeaving;

                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Joined party session successfully: " +
                        $"\n\nSession ID => {Session.Id}" +
                        $"\nSession Name => {Session.Name}" +
                        $"\nJoin Code => {Session.Code}" +
                        $"\nPlayers({Session.PlayerCount}) => {string.Join(", ", PartyMembers?.Select(x => x.PlayerID))}" +
                        $"\nIs Host? => {Session.IsHost}" +
                        $"\nIs Private? => {Session.IsPrivate}" +
                        $"\nIs Locked? => {Session.IsLocked}</color>");
                }
                catch (SessionException e)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Failed to join to a party session: {e.Message}</color>");
                    attempts++;
                    if (attempts >= newtworkProcessAttempt)
                    {
                        var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to join to a party session.</color>";
                        Debug.LogError(errorMessage);
                    }

                    // Wait for a short period before retrying
                    var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                    await UniTask.WaitForSeconds(delay);
                }
                finally
                {
                    SetBlock(false);
                }
            }
        }

        /// <summary>
        /// Invite a friend to play by sending them the party join code
        /// </summary>
        /// <param name="playerEntryData"></param>
        internal async void InviteFriendToPlay(FriendsEntryData? playerEntryData)
        {
            if (authManager is null
                or { IsAlreadyInitialized: false }
                or { IsUserAuthenticatedWithProvider: false })
            {
                Debug.LogWarning("Couldn't create friend party because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            if (friendManager is null or { IsAlreadyInitialized: false })
            {
                Debug.LogWarning("Couldn't create friend party because the main friend manager is not initialized");
                return;
            }

            if (!playerEntryData.HasValue || string.IsNullOrEmpty(playerEntryData.Value.TargetID))
            {
                Debug.LogWarning("Couldn't was possible send a party invitation due player id is null or empty");
                return;
            }

            if (PartyMembers?.Any(x => x.PlayerID == playerEntryData.Value.TargetID) ?? false)
            {
                Debug.LogWarning("Couldn't join to party because the player is already in the party");
                return;
            }

            try
            {
                SetBlock(true);

                // If not already created, create the party lobby
                if (Session == null)
                    await CreatePartyLobby();

                // Once session has value, invite the friend to the party
                if (Session != null)
                { 
                    await friendManager.InviteToParty(playerEntryData.Value.TargetID, Session.Code);

                    promptFadeController?.Fade($"Party invitation send to {playerEntryData.Value.Name}");
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(PartyController)}]</b> {playerEntryData.Value.Name}({playerEntryData.Value.TargetID}) invited to party with join code {Session.Code}</color>");
                }
                else
                    Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> PlayerSession null, cannot crate host to invite party</color>");
            }
            catch (RelayServiceException e)
            {
                Debug.LogWarning($"Error joining Relay: {e.Message}");
                SetBlock(false);
            }
        }

        internal UniTask PromAsLeader(PartyEntryData friendEntryData)
        {
            return UniTask.CompletedTask;
        }

        internal async UniTask KickMember(PartyEntryData friendEntryData)
        {
            if (authManager is null
                or { IsAlreadyInitialized: false }
                or { IsUserAuthenticatedWithProvider: false })
            {
                Debug.LogWarning("Couldn't create friend party because the environment is not initialized or the user is not authenticated yet");
                return;
            }

            if (Session is null)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Couldn't kick member because the session is null</color>");
                return;
            }

            if (Session.PlayerCount is 0)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Couldn't kick member because there are no players in the session.</color>");
                return;
            }

            if (!(PartyMembers?.Any(x => x.PlayerID == friendEntryData.PlayerID) ?? false))
            {
                Debug.LogWarning($"Couldn't kick member because the player ID {friendEntryData.PlayerID} is not found...");
                return;
            }

            // Check if the click wants to kick himself
            if (Session.CurrentPlayer.Id == friendEntryData.PlayerID)
            {
                await SetPlayerLeaveReason(Session.CurrentPlayer.Id, Consts.Reasons.LeftingOwnWill);
                await Session.LeaveAsync();
            }

            // Or if the client is the host, try to remove the player from the session
            else if (Session.IsHost && Session is IHostSession hostSession)
            { 
                await SetPlayerLeaveReason(friendEntryData.PlayerID, Consts.Reasons.LeftingOwnWill);
                await hostSession.RemovePlayerAsync(friendEntryData.PlayerID);
            }
        }

        /// <summary>
        /// Try to reconnect to the last registered session
        /// </summary>
        /// <returns></returns>
        private async UniTask<bool> TryToReconnect()
        {
            if (Session is null)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> PlayerSession is null, cannot reconnect anymore.</color>");
                return false;
            }

            var attempts = 0;
            while (Session != null && attempts <= newtworkProcessAttempt)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(PartyController)}]</b> Attempting to reconnect to session... Attempt {attempts + 1}/{newtworkProcessAttempt}</color>");
                try
                {
                    // Try to get the joined session IDs
                    await MultiplayerService.Instance.GetJoinedSessionIdsAsync();

                    // Try to reconnect to the session
                    await Session.ReconnectAsync();

                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Successfully reconnected to session.</color>");
                    OnPlayerReconnectToParty?.Invoke(Session.CurrentPlayer.Id);
                    return true;
                }
                catch (SessionException e)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Failed to reconnect to session: {e.Message}</color>");
                    attempts++;
                    if (attempts >= newtworkProcessAttempt)
                    {
                        var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to reconnect to session</color>";
                        Debug.LogError(errorMessage);
                    }
                }

                // Wait for a short period before retrying
                var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                await UniTask.WaitForSeconds(delay);
            }

            return false;
        }

        /// <summary>
        /// Leaves the current match session, if any
        /// </summary>
        /// <returns></returns>
        public async UniTask LeaveMatchSession(string playerId)
        {
            var attempts = 0;
            while (attempts < newtworkProcessAttempt)
            {
                try
                {
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(PartyController)}]</b>  Attempting to leaving the current match session... Attempt: {attempts}</color>");
                    
                    // Try to leave the session (if it exists)
                    if (Session != null)
                    {
                        // IS necessary remove player from relay too
                        //Unity.Netcode.NetworkManager.Singleton.DisconnectClient(mainPartyMembers.);

                        Session = null;
                    }

                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Successfully left the match session.</color>");
                    break;
                }
                catch (SessionException e)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Failed to leave the match session: {e.Message}</color>");
                    attempts++;
                    if (attempts >= newtworkProcessAttempt)
                    {
                        Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to leave the match session.</color>");
                        return;
                    }

                    // Wait for a short period before retrying
                    var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                    await UniTask.WaitForSeconds(delay);
                }
            }
        }

        /// <summary>
        /// Sets the reason for leaving the match session for a specific player.<br></br>
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="reason"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public async UniTask SetPlayerLeaveReason(string playerId, string reason)
        {
            if (Session == null)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Session is null, cannot set player leave reason.</color>");
                return;
            }

            // Case A: Player is setting its own reason
            if (Session.CurrentPlayer is not null && Session.CurrentPlayer.Id == playerId)
            {
                Session.CurrentPlayer.SetProperty(Consts.CollectionKeys.LeaveReason,
                    new PlayerProperty(reason, VisibilityPropertyOptions.Public));

                await Session.SaveCurrentPlayerDataAsync();
                return;
            }

            // Case B: Host is setting the reason for another player
            if (Session.IsHost && Session is IHostSession hostSession)
            {
                var targetPlayer = hostSession.Players?.FirstOrDefault(p => p.Id == playerId);
                if (targetPlayer != null)
                {
                    targetPlayer.SetProperty(Consts.CollectionKeys.LeaveReason,
                        new PlayerProperty(reason, VisibilityPropertyOptions.Public));

                    await hostSession.SavePlayerDataAsync(playerId);
                } 
                else
                    Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Player {playerId} not found in session.</color>");
            } 
            else
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Only host can set leave reason for other players.</color>");
        }

        /// <summary>
        /// Event triggered when a new player joins the party session
        /// </summary>
        /// <param name="playerID"></param>
        private void OnPlayerJoined(string playerID)
        {
            var entry = PartyMembers.FirstOrDefault(x => x.PlayerID == playerID);
            if (string.IsNullOrEmpty(entry.PlayerID))
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Player {playerID} was not properly added to party list on client join.</color>");
                return;
            }

            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Player {playerID} added to party list on client join.</color>");

            OnPlayerJoinedEvent?.Invoke(playerID);
            OnPlayerJoinedStaticEvent?.Invoke(playerID);

            if (mainPartyMembers)
            { 
                mainPartyMembers.Configure(Session?.IsHost ?? false);
                mainPartyMembers.MainRefreshPartyEntries();
            }
        }

        /// <summary>
        /// Event triggered when a player leave the party session
        /// </summary>
        /// <param name="playerID"></param>
        private async void OnPlayerLeaving(string playerID)
        {
            var entry = PartyMembers.FirstOrDefault(x => x.PlayerID == playerID);
            if (string.IsNullOrEmpty(entry.PlayerID))
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Player {playerID} is already null.</color>");
                return;
            }

            // Try to get the reason for disconnection from session properties
            var playerToLeave = Session.Players.FirstOrDefault(x => x.Id == playerID);
            if (playerToLeave is null)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Player to leave lost reference value. Couldn't be possible reconnect to the last session.</color>");
                return;
            }

            // Try to get the player to leave leave reason
            var reason = (playerToLeave.Properties?.TryGetValue(Consts.CollectionKeys.LeaveReason, out var reasonSt) ?? false) ? reasonSt.Value : string.Empty;

            // If the reason for disconnection is not due to the player leaving on their own will, attempts to reconnect
            if (reason is not Consts.Reasons.LeftingOwnWill)
            {
                if (await TryToReconnect())
                {
                    Debug.LogWarning($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Session recovery successfully. The last session was linked properly.</color>");
                    return;
                }

                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Session lost. Couldn't be possible reconnect to the last session.</color>");
            }

            // If could not reconnect, remove the join code and notify the game mode
            await LeaveMatchSession(playerID);

            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Player {playerID} removed in party list on client disconnect.</color>");
                
            // Inform the game mode that the player has left
            OnPlayerLeavingEvent?.Invoke(playerID);
            OnPlayerLeavingStaticEvent?.Invoke(playerID);

            if (mainPartyMembers)
            { 
                mainPartyMembers.Configure(Session?.IsHost ?? false);
                mainPartyMembers.MainRefreshPartyEntries();
            }
        }

        /// <summary>
        /// Called when the session is removed, either by the host or due to network issues.
        /// </summary>
        private async void OnRemovedFromSession()
        {
            if (Session is null)
                return;

            // Try to get the player to leave leave reason
            var playerToLeave = Session.CurrentPlayer;
            var playerId = playerToLeave.Id;
            var reason = (playerToLeave?.Properties?.TryGetValue(Consts.CollectionKeys.LeaveReason, out var reasonSt) ?? false) ? reasonSt.Value : string.Empty;

            // If the reason for disconnection is not due to the player leaving on their own will, attempts to reconnect
            var isLeftingOwnWill = reason is Consts.Reasons.LeftingOwnWill;
            if (!isLeftingOwnWill)
            { 
                if (await TryToReconnect())
                {
                    Debug.LogWarning($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(PartyController)}]</b> Session recovery successfully. The last session was linked properly.</color>");
                    return;
                }

                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(PartyController)}]</b> Session lost. Couldn't be possible reconnect to the last session.</color>");
            }

            Debug.LogWarning($"<color={(isLeftingOwnWill ? Consts.Colors.NetworkSuccess : Consts.Colors.NetworkError)}><b>[{nameof(PartyController)}]</b> Removed from session: {Session.Id}</color>");

            Session.PlayerJoined -= OnPlayerJoined;
            Session.PlayerLeaving -= OnPlayerLeaving;

            // If could not reconnect, remove the join code and notify the game mode
            await LeaveMatchSession(playerId);
        }

        [SerializeField]
        public struct PartyEntryData
        {
            internal string PlayerID;
            internal string PlayerName;
            internal string ProfileIconID;
            internal bool IsLeader;

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
#endif