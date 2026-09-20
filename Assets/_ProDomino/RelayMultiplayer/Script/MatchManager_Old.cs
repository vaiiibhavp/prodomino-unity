// Commented because there is another PartyController that create a party without using Relay + Multiplayer.Services + WebGL
// (this combination is not compatbile with webGL)

#if false
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ProDomino.Authentication;
using ProDomino.GameModes;
using ProDomino.GameSystem;
using ProDomino.Leaderboard;
using ProDomino.RelayMultiplayer;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timba.Patterns;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using static HelperSharedLibrary.Enums;

public class MatchManager_Old : NetworkBehaviour
{
    public static MatchManager_Old Instance { get; private set; }

    [SerializeField]
    private RelayManager relayManager;

    [SerializeField]
    private JoinCodeManager joinCodeManager;

    [SerializeField]
    private LobbyChatManager lobbyChatManager;

    [SerializeField]
    private MenuControllerGameMode menuControllerGameMode;

    [Tooltip("Time in seconds for each player's turn.")]
    [SerializeField] private float turnDuration = 60f; // 60 segundos por turno
    [SerializeField] private float waitTimeForNextRound = 20f;
    [SerializeField] private float waitTimeForRematch = 20f;
    [SerializeField] private bool waitConfirmationForNextRound = false;
    [SerializeField] private bool waitConfirmationForRematch = false;

    [Header("Matchmaking Settings")]
    [Tooltip("Determine if the usual matchmaking will be use")]
    [SerializeField] private bool useUsualMatchmaking;
    
    [Tooltip("Determine if the party matchmaking will be use")]
    [SerializeField] private bool usePartyMatchmaking;
    
    [Tooltip("Determine if the bot matchmaking will be use")]
    [SerializeField] private bool useBotMatchmaking;


    [Tooltip("Time in seconds to wait before stopping matchmaking if not enough players have joined.")]
    [SerializeField] private float timeToStopMatchmaking = 120f;

    [Tooltip("Number of attempts to create/join/leave a session before giving up.")]
    [SerializeField] private int newtworkProcessAttempt = 5;


    /// <summary>
    /// The remaining time for the current player's turn in seconds.
    /// </summary>
    [SerializeField] private NetworkVariable<float> TurnTimeRemaining = new NetworkVariable<float>(
        value: 0f,
        writePerm: NetworkVariableWritePermission.Server
    );

    //[SerializeField] private bool isTurnActive = false;
    /*private NetworkVariable<bool> isTurnActive = new NetworkVariable<bool>(
        value: false,
        writePerm: NetworkVariableWritePermission.Server
    );
    public bool Get_IsTurnActive() => isTurnActive.Value;*/
    //public bool IsTurnActive => isTurnActive;
    
    private bool enableTimerInHost = false;
    [SerializeField] private NetworkVariable<StatusTimerInHost> statusTimerInHost = new NetworkVariable<StatusTimerInHost>(
        value: StatusTimerInHost.none,
        writePerm: NetworkVariableWritePermission.Server
    );
    public StatusTimerInHost Get_StatusTimerInHost() => statusTimerInHost.Value;

    /// <summary>
    /// The number of players that the current session expects
    /// </summary>
    private NetworkVariable<int> ClientsCount = new
        (readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server);

    /// <summary>
    /// Registers the clients' EMCs for each player
    /// </summary>
    public NetworkList<ClientEMC> ClientsEMCs = new
        (readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server);

    /// <summary>
    /// The client ID of the player whose turn it currently is
    /// </summary>
    private NetworkVariable<int> CurrentPlayablePlayerClientID = new NetworkVariable<int>(
        value: -1,
        writePerm: NetworkVariableWritePermission.Server
    );

    // Those are the player IDs for each client, used to identify the players in the session.
    private NetworkVariable<ClientsBasicInfoCollection> clientsBasicInfoCollection =
        new(writePerm: NetworkVariableWritePermission.Server);

    private AbstractGameMode currentGameMode;
    private Coroutine matchmakingTimerCoroutine;
    private CancellationTokenSource matchmakerCancellationSource;
    private List<int> playerUpdateComplete = new List<int>();
    private int localPlayerMatchID;

    public event EventHandler OnCliendConnected;
    public event EventHandler OnGameStarted;
    public event EventHandler OnCurrentPlayablePlayerChanged;

    public event EventHandler<string> OnJoinSession;
    public event EventHandler<string> OnLeftSession;
    public event EventHandler<string> OnReconnectSession;

    public event EventHandler OnSessionRemovedEvent;
    public event EventHandler OnStartMatchmakingEvent;
    public event EventHandler OnCancelMatchmakingEvent;
    public event EventHandler OnMatchmakingNotFoundEvent;

    private GameManager gameManager;
    private AuthManager authManager;
    private DictionaryService dictionaryService;
    private LeaderboardManager leaderboardManager;


    private bool _isTurnActive = false;
    public bool IsTurnActive => _isTurnActive;

    /// <summary>
    /// The current multiplayer session, if any.<br></br>
    /// Contains details such as session ID, join code, players, and more.
    /// </summary>
    public ISession MatchmakingSession { get; private set; }

    /// <summary>
    /// Checks if the user is currently in a match session.
    /// </summary>
    public bool IsInMatch => MatchmakingSession is not null && currentGameMode != null;

    /// <summary>
    /// Checks if the user is currently matchmaking.
    /// </summary>
    public bool IsMatchmaking => matchmakingTimerCoroutine != null;

    /// <summary>
    /// Checks if the user is authenticated via Unity Gaming Services or any provider.
    /// </summary>
    internal bool IsAuthenticated => authManager is { IsUGSAuthenticated: true }
        and { IsUserAuthenticatedWithCredentials: true }
        or { IsUserAuthenticatedWithProvider: true };

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("More than one GameManager instance!");
        }
        Instance = this;
    }

    void Start()
    {
        relayManager = GetComponent<RelayManager>();
        joinCodeManager = GetComponent<JoinCodeManager>();

        gameManager = ServiceLocator.Instance.GetService<GameManager>();
        authManager = ServiceLocator.Instance.GetService<AuthManager>();
        dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
        leaderboardManager = ServiceLocator.Instance.GetService<LeaderboardManager>();
    }

    void Update()
    {
        if (!IsServer || !enableTimerInHost || !IsSpawned || statusTimerInHost.Value == StatusTimerInHost.none) 
            return;

        TurnTimeRemaining.Value -= Time.deltaTime;

        if (TurnTimeRemaining.Value <= 0f)
        {
            enableTimerInHost = false;
            TurnTimeRemaining.Value = 0f;

            //statusTimerInHost.Value = StatusTimerInHost.none;

            Debug.Log("aaaaaauxStatusTimerInHost: " + statusTimerInHost.Value);

            switch (statusTimerInHost.Value)
            {
                case StatusTimerInHost.waitingTurn:
                    statusTimerInHost.Value = StatusTimerInHost.none;
                    Debug.Log("aaaaaauxStatusTimerInHost 01: " + statusTimerInHost.Value);
                    HandleTurnTimeout();
                    break;
                case StatusTimerInHost.waitingNextRound:
                    statusTimerInHost.Value = StatusTimerInHost.none;
                    Debug.Log("aaaaaauxStatusTimerInHost 02: " + statusTimerInHost.Value);
                    //waitConfirmationForNextRound = false;
                    //StartNextRound();
                    ShufflingTilesAgainRpc();
                    break;
                case StatusTimerInHost.waitingRematch:
                    statusTimerInHost.Value = StatusTimerInHost.none;
                    Debug.Log("aaaaaauxStatusTimerInHost 03: " + statusTimerInHost.Value);
                    //waitConfirmationForRematch = false;
                    //ShufflingTilesAgainRpc(restartNewRound: false);
                    ExitTheGameAndGoToMainMenu_ClientRpc();
                    break;
                default:
                    //statusTimerInHost.Value = StatusTimerInHost.none;
                    break;
            }

            /*if (isTurnActive.Value)
            {
                isTurnActive.Value = false;
                HandleTurnTimeout();
            }
            else if (waitConfirmationForNextRound)
            {
                waitConfirmationForNextRound = false;
                //StartNextRound();
                ShufflingTilesAgainRpc();
            }
            else if (waitConfirmationForRematch)
            {
                waitConfirmationForRematch = false;
                ShufflingTilesAgainRpc(restartNewRound: false);
            }*/
        }
    }

    /// <summary>
    /// Creates or joins a matchmaking session based on the provided game mode, game type, and number of players.
    /// </summary>
    /// <param name="gameMode"></param>
    /// <param name="gameType"></param>
    /// <param name="numberPlayers"></param>
    /// <param name="playerData"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async UniTask CreateOrJoinMatchSession(GameModeData gameModeData, ISession partySession = null)
    {
        // Extract game mode, game type, and number of players from the provided game mode data
        var (gameMode, numberPlayers, gameType, concentrateNumberOfTiles) =
            (gameModeData.gameMode,
            gameModeData.NumberPlayers,
            gameModeData.gameType,
            gameModeData.concentrateNumberOfTiles);

        #region Validations
        if (IsInMatch)
            throw new ArgumentException($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Couldn't create a match because player still in one.</color>");

        // Check if the game mode is valid
        if (gameMode is GameMode.none)
            throw new ArgumentException($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Invalid game mode.</color>");

        // Check if the number of players is valid
        if (numberPlayers is not NumberPlayers.oneVsOne and not NumberPlayers.oneVsThree and not NumberPlayers.none)
            throw new ArgumentException($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Invalid number of players. Must be either 'oneVsOne' or 'oneVsThree'.</color>");

        // Check if the user is authenticated using providers
        if (gameType is GameType.competitive && !IsAuthenticated)
            throw new ArgumentException($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Competitive games require the user to be authenticated with Unity Gaming Services to create a competitive session.</color>");

        // Else, check if at least the player is authenticated anonimously for casual games
        else if (gameType is GameType.casual && !authManager.IsUGSAuthenticated)
            throw new ArgumentException($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Casual games require the user to be authenticated (at least anonimously) with Unity Gaming Services to create a casual session.</color>");
        #endregion

        #region Local Variables
        // Determine the player that the matchmaker session is aiming to
        var maxPlayers = numberPlayers is NumberPlayers.oneVsOne ? 2 : 4;

        // Construct the matchmaking queue name based on game mode, game type, and number of players
        var matchMakingQueueName = $"{gameMode}-{gameType}-{numberPlayers}";

        // Create a matchmaker options to define the matchmaking parameters
        var matchmakerOptions = new MatchmakerOptions() { QueueName = matchMakingQueueName };

        // Indicates if the session should be a party or a matchmaking one
        var shouldBePartySession = partySession is not null;
        #endregion

        // If there is still a previous session, delete it
        if (MatchmakingSession is not null)
            await CancelMatchmaking();

        // Trigger the event to notify that matchmaking has started
        OnStartMatchmakingEvent?.Invoke(this, EventArgs.Empty);

        // Start the process of stopping matchmaking after a certain time if no more players join the session
        matchmakingTimerCoroutine = StartCoroutine(StopMatchmaking());

        // First at all, validate if a party exist to use the convetional matchmaking by Matchmaker service
        if (!shouldBePartySession && useUsualMatchmaking)
        {
            await TryUsingMatchmaker();

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                return;
        }

        // Else, try to create a empty session and use the clients registered into the Network hosting
        else if (usePartyMatchmaking)
        {
            await TryUsingPartySession();

             // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                return;
        }

        // If at least the session stills null, try to create a basic session
        if (MatchmakingSession is null)
        {
            await TryUsingBasicSession();

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is not null and { IsCancellationRequested: true })
                return;
        }

        // Check if the player session count already reached the max required
        if (MatchmakingSession is not null)
        {
            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Matchmaking session created or joined successfully: " +
                $"\n\nSession ID => {MatchmakingSession.Id}" +
                $"\nSession Name => {MatchmakingSession.Name}" +
                $"\nJoin Code => {MatchmakingSession.Code}" +
                $"\nPlayers({MatchmakingSession.PlayerCount}) => {string.Join(", ", MatchmakingSession.Players.Select(x => x.Id))}" +
                $"\nIs Host? => {MatchmakingSession.IsHost}" +
                $"\nIs Private? => {MatchmakingSession.IsPrivate}" +
                $"\nIs Locked? => {MatchmakingSession.IsLocked}</color>");

            // Subscribe player when they leave the session
            MatchmakingSession.PlayerJoined += OnPlayerJoiningSession;
            MatchmakingSession.PlayerLeaving += OnPlayerLeavingSession;

            var startPartyPulling = DateTime.UtcNow;
            while (NetworkManager.ConnectedClients.Count != MatchmakingSession.PlayerCount
                && !matchmakerCancellationSource.IsCancellationRequested
                && (DateTime.UtcNow - startPartyPulling).TotalSeconds < 10)
                await UniTask.NextFrame();

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is not null and { IsCancellationRequested: true })
                return;
        }

        // Once the session is created (or not), start it
        if (NetworkManager.Singleton.IsServer)
            TryToStartMatchRPC
                (isReadyToStart: true,
                    requiredClients: maxPlayers,
                    gameMode: gameMode,
                    gameType: gameType,
                    numberPlayers: numberPlayers,
                    concentrateNumberOfTiles: concentrateNumberOfTiles);

        // If the player coudln't create a matchmaking, cancel its timer
        else if (MatchmakingSession is null)
        {
            Debug.Log($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Player coudln't create a matchmaking</color>");
            CancelMatchmaking().Forget();
        }

        #region Internal
        // Attempt to create or join a matchmaking session
        async UniTask TryUsingMatchmaker()
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to create or join a matchmaking session for queue: {matchMakingQueueName}</color>");

            // Create a session options to define the matchmaking parameters
            var sessionOptions = new SessionOptions()
            {
                SessionProperties = new() { [Consts.CollectionKeys.IsPartySession] = new(false.ToString()) },
                IsPrivate = false,
                MaxPlayers = maxPlayers,
            }
            .WithRelayNetwork();

            // Create a cancellation source in case the user wants to cancel the matchmaking
            matchmakerCancellationSource = new();

            try
            {
                // Create or join a matchmaking session
                MatchmakingSession = await MultiplayerService.Instance.MatchmakeSessionAsync
                    (matchmakerOptions,
                    sessionOptions,
                    matchmakerCancellationSource.Token);

                Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Matchmaking session succesfully created with id: {MatchmakingSession?.Id ?? "None"}</color>");
            }
            catch (SessionException e)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to create or join matchmaking session: {e.Message}</color>");
            }
        }

        // Attempts to create a session using Network Manager party clients
        async UniTask TryUsingPartySession()
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to create and join party client to new session for queue: {matchMakingQueueName}</color>");

            // Create a session options to define the matchmaking parameters
            var sessionOptions = new SessionOptions()
            {
                SessionProperties = new() { [Consts.CollectionKeys.IsPartySession] = new(false.ToString()) },
                IsPrivate = false,
                MaxPlayers = maxPlayers,
            }
            .WithNetworkHandler(new ExistingNgoNetworkHandler(NetworkManager.Singleton));

            // Create a cancellation source in case the user wants to cancel the matchmaking
            matchmakerCancellationSource = new();

            try
            {
                // If there is an optional session provided, use it
                if (partySession is not null and IHostSession hostSession)
                {
                    if (hostSession.PlayerCount <= sessionOptions.MaxPlayers)
                    {
                        // Create an empty session using Network Manager registered clients
                        MatchmakingSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);
                        Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Party succesfully created with id: {MatchmakingSession?.Id ?? "None"}</color>");

                        // Add players to new session using Network RPC system
                        JoinPlayersToNewSessionClientRpc(MatchmakingSession.Id, maxPlayers);

                        // Wait for a while until clients connect to the new session
                        var startPartyPulling = DateTime.UtcNow;
                        while (NetworkManager.Singleton.ConnectedClients.Count < sessionOptions.MaxPlayers
                            && (DateTime.UtcNow - startPartyPulling).TotalSeconds < 60)
                            await UniTask.NextFrame();

                        if (NetworkManager.Singleton.ConnectedClients.Count == sessionOptions.MaxPlayers)
                            await MatchmakingSession.RefreshAsync();

                        var partyRegistered = MatchmakingSession?.Players
                            ?.Select(x => x.Id)
                            ?.ToArray();

                        Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Party succesfully registered:\n\n{(partyRegistered is not null and { Length: > 0 } ? string.Join("\n* ", partyRegistered) : "None")}</color>");
                    } else
                        Debug.Log($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Party exceeds matchmaking members limis</color>");
                } else
                    Debug.Log($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Party matchmaking should be started only by the host. Permission Denied</color>");
            }
            catch (SessionException e)
            {
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to create or join matchmaking session: {e.Message}</color>");
            }
        }

        // Attempts to create a basic session 
        async UniTask TryUsingBasicSession()
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to create basic session client for queue: {matchMakingQueueName}</color>");

            // Create a session options to define the matchmaking parameters
            var sessionOptions = new SessionOptions()
            {
                SessionProperties = new() { [Consts.CollectionKeys.IsPartySession] = new(false.ToString()) },
                IsPrivate = false,
                MaxPlayers = maxPlayers,
            };

            // Id the server is not initialized yet
            if (!NetworkManager.Singleton.IsServer)
                sessionOptions = sessionOptions.WithRelayNetwork();

            // But, If there is a existing server, use it to create the session
            else
                sessionOptions = sessionOptions.WithNetworkHandler(new ExistingNgoNetworkHandler(NetworkManager.Singleton));

            // Create a cancellation source in case the user wants to cancel the matchmaking
            matchmakerCancellationSource = new();

            try
            {
                // Create an empty session using Network Manager registered clients
                MatchmakingSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);
                Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Basic session succesfully created with id: {MatchmakingSession?.Id ?? "None"}</color>");
            }
            catch (SessionException e)
            {
                Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to create matchmaking session: {e.Message}</color>");
            }
        }

        // Try to stop match making if the session is still active and the number of connected clients is less than the expected number of players.
        IEnumerator StopMatchmaking()
        {
            var stopTime = Time.time + timeToStopMatchmaking;
            while (Time.time < stopTime)
                yield return null; // Wait for the next frame

            // Stop matchmaking if the session is still active and the number of connected clients is less than the expected number of players
            if (MatchmakingSession is not null)
            {
                OnMatchmakingNotFoundEvent?.Invoke(this, EventArgs.Empty);
                yield return CancelMatchmaking();
            }
        }
        #endregion
    }


    [Rpc(SendTo.ClientsAndHost)]
    private void JoinPlayersToNewSessionClientRpc(string sessionID, int maxPlayers)
    {
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Host is already configurated. Couldn't configurate again into session: {sessionID}</color>");
            return;
        }
        JoinPlayersToNewSession().Forget();

        async UniTask JoinPlayersToNewSession()
        {
            // Register the attempts used to generate the session
            var attempts = 0;

            // Create a cancellation source in case the user wants to cancel the matchmaking
            matchmakerCancellationSource = new();
            var isSessionAlive = MatchmakingSession == null;
            while (MatchmakingSession == null
                && !matchmakerCancellationSource.IsCancellationRequested
                && attempts < newtworkProcessAttempt)
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting join a matchmaking party session for session with id: {sessionID}, attempts: {attempts + 1}</color>");

                // Create a session options to define the matchmaking parameters
                var sessionOptions = new SessionOptions()
                {
                    SessionProperties = new() { [Consts.CollectionKeys.IsPartySession] = new(false.ToString()) },
                    IsPrivate = false,
                    MaxPlayers = maxPlayers,
                }
                .WithRelayNetwork()
                .WithNetworkHandler(new ExistingNgoNetworkHandler(NetworkManager.Singleton));

                try
                {
                    // Create or join a matchmaking session
                    MatchmakingSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionID, sessionOptions);
                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b>Player ({MatchmakingSession?.CurrentPlayer?.Id ?? "Null"}) successfully joined to session: {MatchmakingSession?.Id ?? "Null"}</color>");
                }
                catch (SessionException e)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to join matchmaking party session: {e.Message}</color>");
                    attempts++;

                    // If the matchmake is time out, return inmediatily to start the next try
                    if (e.Error is SessionError.MatchmakerAssignmentTimeout)
                        return;

                    if (attempts >= newtworkProcessAttempt)
                    {
                        var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to join matchmaking party session.</color>";
                        Debug.LogError(errorMessage);
                    }

                    // Wait for a short period before retrying
                    var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                    await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
                }
            }
        }
    }

    /// <summary>
    /// Leaves the current match session, if any
    /// </summary>
    /// <returns></returns>
    public async UniTask LeaveMatchSession()
    {
        var attempts = 0;
        while (attempts < newtworkProcessAttempt)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to Leave the current match session... Attempts: {attempts}</color>");

            try
            {
                // First at all, cancel the matchmaking process
                await CancelMatchmaking();

                DisconnectPlayerRPC(NetworkManager.Singleton.LocalClientId);
                break;
            }
            catch (SessionException e)
            {
                Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to leave the match session: {e.Message}</color>");
                attempts++;
                if (attempts >= newtworkProcessAttempt)
                {
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to disconnect session.</color>");
                    break;
                }

                // Wait for a short period before retrying
                var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
            }
        }
    }

    /// <summary>
    /// Calls to server to try to disconnect a client
    /// </summary>
    /// <param name="playerIdToDisconnect"></param>
    [Rpc(SendTo.Server)]
    private void DisconnectPlayerRPC(ulong clientIdToDisconnect, string reasonToLeave = "")
    {
        if (!IsServer)
            return;

        // Disconnect from NGO transport
        try
        {
            NetworkManager.Singleton.DisconnectClient(clientIdToDisconnect, reasonToLeave);
            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Successfully left the match session.</color>");
        }
        catch (SessionException e)
        {
            Debug.LogError($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Failed to disconnect player with id: {clientIdToDisconnect}.</color>\n\n{e}");
            throw;
        }
    }

    public async UniTask CancelMatchmaking()
    {
        StopMatchmakingTimer();

        // Cancel the player session creation's process
        if (matchmakerCancellationSource is not null and { IsCancellationRequested: false })
        {
            matchmakerCancellationSource.Cancel(false);
            matchmakerCancellationSource = null;
        }

        // Once the reasonToLeave is set, try to leave the session (if it exists)
        if (MatchmakingSession != null)
        {
            // Leave the session
            var playerID = authManager.UUID;
            SetPlayerLeaveReason(Consts.Reasons.LeftingOwnWill);

            var attempts = 0;
            while (MatchmakingSession != null && attempts <= newtworkProcessAttempt)
            {
                // If the current client is the host, delete the session
                if (MatchmakingSession is IHostSession hostSession)
                {
                    // If the session is a party one, don't destroy it. Just remove the player (this is to avoid damage the "lobby"[party session])
                    if (hostSession.Properties.TryGetValue(Consts.CollectionKeys.IsPartySession, out var valueProperty)
                        && valueProperty != null
                        && !string.IsNullOrEmpty(valueProperty.Value)
                        && bool.TryParse(valueProperty.Value, out var value))
                    {
                        await LeaveIteration();
                    }

                    // But, if the session is the one created with the matchmaking... Destroy it!!!
                    else
                    {
                        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to delete session... Attempt {attempts + 1}/{newtworkProcessAttempt}</color>");

                        try
                        {
                            // Try to reconnect to the session
                            await hostSession.DeleteAsync();
                            MatchmakingSession = null;

                            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Successfully deleted matchmaking session</color>");
                        }
                        catch (SessionException e)
                        {
                            Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to delete matchmaking session: {e.Message}</color>");
                            attempts++;
                            if (attempts >= newtworkProcessAttempt)
                            {
                                var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to delete matchmaking session</color>";
                                Debug.LogError(errorMessage);
                            }
                        }

                        // Wait for a short period before retrying
                        var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                        await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
                    }
                }

                // If not, just try to leave
                else
                    await LeaveIteration();
            }

            // Just in case any issue appears
            MatchmakingSession = null;

            async UniTask LeaveIteration()
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to leave matchmaking session... Attempt {attempts + 1}/{newtworkProcessAttempt}</color>");
                try
                {
                    // Try to get the reason to leave of the player
                    var reasonToLeave = MatchmakingSession.CurrentPlayer.Properties.TryGetValue(Consts.CollectionKeys.LeaveReason, out var reasonSt) ? reasonSt.Value : string.Empty;

                    // Try to reconnect to the session
                    await MatchmakingSession.LeaveAsync();

                    MatchmakingSession = null;

                    Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Successfully leave matchmaking session</color>");
                }
                catch (SessionException e)
                {
                     attempts++;
                    Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to leave matchmaking session: {e.Error} \n{e.Message}</color>");
                    
                    // Wait for a short period before retrying
                    var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                    await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
                }
            }
        }
    }

    /// <summary>
    /// Sets the reasonToLeave for leaving the match session for a specific player.<br></br>
    /// </summary>
    /// <param name="playerId"></param>
    /// <param name="reason"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetPlayerLeaveReason(string reason)
    {
        if (MatchmakingSession != null)
            MatchmakingSession.CurrentPlayer.SetProperty(Consts.CollectionKeys.LeaveReason, new(reason, VisibilityPropertyOptions.Public));
        else
            Debug.Log($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> MatchmakingSession is not a host session, cannot set player leave reasonToLeave.</color>");
    }

    public async Task<string> AuxOnCreateRelayLobbyID(NumberPlayers numberPlayers)
    {
        switch (numberPlayers)
        {
            case NumberPlayers.oneVsOne:
                relayManager.maxPlayers = 2;
                ClientsCount.Value = 2;
                break;
            case NumberPlayers.oneVsThree:
                relayManager.maxPlayers = 4;
                ClientsCount.Value = 4;
                break;
            case NumberPlayers.twoVsTwo:
                relayManager.maxPlayers = 4;
                ClientsCount.Value = 4;
                break;
            default:
                Debug.LogWarning("Invalid number of players");
                break;
        }

        string joinCode = await relayManager.CreateRelay();

        return joinCode;
    }

    public async void ToLobby(Action callback, string joinGameID, NumberPlayers numberPlayers)
    {
        string auxJoinCode = await joinCodeManager.TryToObtainRelayCodeAsync(joinGameID, matchmakerCancellationSource.Token);

        Debug.Log("Join Game: " + joinGameID);

        if (auxJoinCode != null)
        {
            JoinToLobby(callback, joinGameID, auxJoinCode, numberPlayers);
            //callback?.Invoke();
            Debug.Log("Join code: " + auxJoinCode);
        } else
        {
            auxJoinCode = await AuxOnCreateRelayLobbyID(numberPlayers);

            bool auxSuccess = await joinCodeManager.TryToRegisterRelayCodeInServer(joinGameID, auxJoinCode, matchmakerCancellationSource.Token);

            if (auxSuccess)
                Debug.Log("Code created successfully.");
            else
                Debug.LogWarning("Failed to create the code.");

            //Debug.LogError("Failed to join relay.");
        }
    }

    public async void JoinToLobby(Action callback, string joinGameID, string joinCode, NumberPlayers numberPlayers)
    {
        bool success = await relayManager.TryToJoinRelay(joinCode);
        //await relayManager.TryToJoinRelay(joinCode);

        //string joinCode = await relayManager.TryToCreateRelay();
        //NetworkManager.Singleton.StartHost();

        //Debug.Log("Join code: " + joinCode);

        if (success)
        {
            callback?.Invoke();
            Debug.Log("Join code: " + joinCode);
        } else
        {
            await RemoveJoinCode(joinGameID, joinCode);

            ToLobby(callback, joinGameID, numberPlayers);

            Debug.LogWarning("Failed to join relay.");
        }
    }


    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnCliendConnectedRpc()
    {
        OnCliendConnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveJoinCode(string auxGameID, string auxJoinCode)
    {
        bool successDelete = await joinCodeManager.TryToRemoveRelayCodeAsync(auxGameID, auxJoinCode, matchmakerCancellationSource.Token);
    }

    [Rpc(SendTo.Server)]
    public void PlaceTileRpc(string tile_id, int playerMatch_ID)
    {
        //isTurnActive.Value = false;
        enableTimerInHost = false;
        statusTimerInHost.Value = StatusTimerInHost.none;

        if (playerMatch_ID != CurrentPlayablePlayerClientID.Value)
        {
            return;
        }

        NextTurn(playerMatch_ID);
        //StartTurnTimerForPlayerRPC();
    }

    public int GetLocalPlayerMatchID()
    {
        return localPlayerMatchID;
    }

    public int GetCurrentPlayablePlayer()
    {
        return CurrentPlayablePlayerClientID.Value;
    }

    public int GetNumberOfPlayers()
    {
        return ClientsCount.Value;
    }

    private void CreateGameMode(int expectedClients, GameModeData gameModeData = null, bool usingBotsToFill = false)
    {
        // Create or generate the current game mode
        currentGameMode = menuControllerGameMode.GenerateGameMode(gameModeData);

        // If this instance is the server, perform server-specific setup
        if (IsServer)
        {
            // Setup random hands for the players
            currentGameMode.SetupRandomHands();

            // Clear the list that tracks which players have finished updating
            playerUpdateComplete.Clear();

            // Get the host leaderboard specific entry data to determine the AI level
            var leaderboardEntry = leaderboardManager?.GetLeaderboardEntry(leaderboardManager?.GetLeaderboardID(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID));
            var playerTier = Enum.TryParse(leaderboardEntry?.Tier ?? string.Empty, out LeaderboardTier tier) ? tier : LeaderboardTier.None;

            // Modify the level of the AI
            currentGameMode.DominoAI.SetKnowledgeProfile(playerTier switch
            {
                LeaderboardTier.None or
                LeaderboardTier.ClassC or
                LeaderboardTier.ClassB => DominoAI.IAKnowledgeProfile.Realistic,

                LeaderboardTier.ClassA or
                LeaderboardTier.Expert => DominoAI.IAKnowledgeProfile.Undertaker,

                LeaderboardTier.Master or
                LeaderboardTier.GrandMaster or
                _ => DominoAI.IAKnowledgeProfile.Omniscient,
            });
        }

        // Setup the turn timer logic using delegates for time and activity checks
        currentGameMode.SetTurnTimerData(GetRemainingTime, Get_StatusTimerInHost, turnDuration);
        //currentGameMode.SetTurnTimerData(GetRemainingTime, Get_IsTurnActive, turnDuration);

        // Always share the host's own data with itself first
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Sharing client id: {localPlayerMatchID} data info</color>");
        currentGameMode.GenerateNewPlayerDataInfo(localPlayerMatchID, false, TransferDataInfoOnAllPlayers_Rpc);

        // Calculate how many players are missing to reach the expected number
        var leftingPlayers = expectedClients - NetworkManager.Singleton.ConnectedClients.Count;

        // Only the host should fill with bots, and only if there are missing players
        if (leftingPlayers > 0 && IsHost && usingBotsToFill)
        {
            // Step 1: Create the full expected range of client IDs [0 .. expectedClients - 1]
            var expectedIds = Enumerable.Range(0, expectedClients).Select(x => (ulong)x);

            // Step 2: Get the actual connected client IDs from the NetworkManager
            var connectedIds = NetworkManager.Singleton.ConnectedClients.Keys;

            // Step 3: Find the IDs that are missing (expected but not currently connected)
            var missingIds = expectedIds.Except(connectedIds).ToList();

            // Step 4: Assign data for each missing ID (these will represent bots)
            foreach (var fakeId in missingIds)
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Sharing bot client id: {fakeId} data info</color>");
                currentGameMode.GenerateNewPlayerDataInfo((int)fakeId, true, TransferDataInfoOnAllPlayers_Rpc);
            }
        }
    }

    #region Collect data from all aplayers

    [Rpc(SendTo.Server)]
    private void TransferDataInfoOnAllPlayers_Rpc(PlayerDataInfo playerDataInfo)
    {
        if (!playerUpdateComplete.Contains(playerDataInfo.clientId))
        {
            playerUpdateComplete.Add(playerDataInfo.clientId);
            currentGameMode.RegisterRemotePlayersDataInfo_fromHost(playerDataInfo);
        } 
        else
            Debug.Log($"<color={Consts.Colors.NetworkError}>[{nameof(MatchManager_Old)}] Player update data already contains client ID: {playerDataInfo.clientId}" +
                $"\n\nplayerUpdateComplete: {(playerUpdateComplete is not null and { Count: > 0 } ? string.Join("\n* ", playerUpdateComplete?.Select(x => x.ToString())?.ToArray()) : "None")}" +
                $"</color>");

        // Check if the goal of players to update reaches the clients quantities
        if (playerUpdateComplete.Count == ClientsCount.Value)
        {
            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Server transfer data current value reached:" +
                $"\n\nClients Count: {ClientsCount.Value}" +
                $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                $"</color>");

            // Clear the process (is not necesary continue with the process)
            playerUpdateComplete.Clear();


            // Compact every client data into one list
            var clientsDataInfoPlayer = new List<PlayerDataInfo>()
            {
                currentGameMode.DataInfoPlayer_0,
                currentGameMode.DataInfoPlayer_1,
                currentGameMode.DataInfoPlayer_2,
                currentGameMode.DataInfoPlayer_3,
            };

            // Try to get those clients that are bots
            var playerClients = clientsDataInfoPlayer
                ?.Where(x => x.IsConfigured && !x.isBot)
                ?.ToArray();

            if (playerClients is not null and { Length: > 0 })
            { 
                foreach (var data in playerClients)
                    RefreshPlayerDataInfoInClients_Rpc(data);          
            }
            else
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Server couldn't pass data because there isn't anyone to received it</color>");

            // try to get every bot data in the match
            var botplayer = clientsDataInfoPlayer
                ?.Where(x => x.IsConfigured && x.isBot)
                ?.ToArray();

            // Check if there are bots that need confirm that receive the data
            if (botplayer is not null and { Length: > 0 })
                foreach (var data in botplayer)
                    ConfirmDeliveryUserDataInfo_Rpc(data.clientId);
        } 
        else
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Server doesn't have the required connected clients yet:" +
                $"\n\nClients Count: {ClientsCount.Value}" +
                $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                $"</color>");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RefreshPlayerDataInfoInClients_Rpc(PlayerDataInfo playerDataInfo)
    {
        if (currentGameMode)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Player data info sent: \n\n{JsonConvert.SerializeObject(playerDataInfo, Formatting.Indented)}</color>");

            // Register the Client data data the server gave
            currentGameMode.RegisterRemotePlayersDataInfo_fromHost(playerDataInfo);

            // Confirm that client received the data
            ConfirmDeliveryUserDataInfo_Rpc(localPlayerMatchID);
        } 
        else
            Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> CurrentGameMode is null, cannot send player data info to clients.</color>");
    }

    [Rpc(SendTo.Server)]
    private void ConfirmDeliveryUserDataInfo_Rpc(int clientID)
    {
        if (!playerUpdateComplete.Contains(clientID))
            playerUpdateComplete.Add(clientID);
            
        // Once the amount of clients updated reach the goal, start the game for every client
        if (playerUpdateComplete.Count == ClientsCount.Value)
        {
            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Server confirm data current value reached:" +
                $"\n\nClients Count: {ClientsCount.Value}" +
                $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                $"</color>");

            // Clear the process (is not necesary continue with the process)
            playerUpdateComplete.Clear();

            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Starting Match in every client!</color>");
            RestartGame_Rpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RestartGame_Rpc()
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Restarting Match in each client...</color>");
        currentGameMode.PrintDataPlayers();

        currentGameMode.RestartGame();
        currentGameMode.SetPlayerMovementEvent(SendPlayerMovementToHost);
        currentGameMode.SetPlayerFinishGameEvent(SendPlayerResult);

        //if (currentGameMode.GameModeID == GameMode.french)
        currentGameMode.OnRestartGameModeRoundHostAction = RestartGameModeRound;
        currentGameMode.OnRematchGameModeHostAction = RematchGameMode;
        currentGameMode.OnPlayerTakesFromBoneyard = PlayerTakesFromBoneyard;

        lobbyChatManager = currentGameMode.LobbyChatContainerObj.GetComponent<LobbyChatManager>();
        lobbyChatManager.Initialize(dictionaryService.GetSprite);
        lobbyChatManager.Configure
            (currentGameMode.DataInfoPlayer_0,
            currentGameMode.DataInfoPlayer_1,
            currentGameMode.DataInfoPlayer_2,
            currentGameMode.DataInfoPlayer_3);
        lobbyChatManager.OnSendMessageFromClientToHost = SendMessageFromClientToHost;

        ConfigureClientHand_Rpc(localPlayerMatchID, false);
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is trying to configure bots...</color>");

        currentGameMode.ConfigurePlayersUI();
        Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Game Configured</color>");
    }

    #endregion

    #region Calculate and assign random hands

    /// <summary>
    /// The host configures the hand of the client with the specific <paramref name="clientId"/><br></br>
    /// The host server knows each hand, but the host client never knows any different of itself one
    /// </summary>
    /// <param name="clientId"></param>
    [Rpc(SendTo.Server)]
    public void ConfigureClientHand_Rpc(int clientId, bool isBot)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Configuring hand to Client with id: <br>{clientId}</b></color>");

        // Get the client hand
        List<int> auxHand = currentGameMode.GetHandOfPlayerWithID(clientId);

        // Get the status of the round. Check if any player is already eliminated before give to them hands
        bool auxPlayerEliminatedState = currentGameMode.ExtendedGameController.TurnScript._playerIsEliminated;
        bool auxLeftEliminatedState = currentGameMode.ExtendedGameController.TurnScript._leftAIIsEliminated;
        bool auxTopEliminatedState = currentGameMode.ExtendedGameController.TurnScript._topAIIsEliminated;
        bool auxRightEliminatedState = currentGameMode.ExtendedGameController.TurnScript._rightAIIsEliminated;

        // The host configures the hand only to the specific client
        RegisterClientHand_ClientRpc
            (auxHand.ToArray(), 
            auxPlayerEliminatedState, 
            auxLeftEliminatedState, 
            auxTopEliminatedState, 
            auxRightEliminatedState,
            !isBot ? // If the player is not a bot, send the data only for its client. But otherwise, send the data to every client.
                     // TODO: only server manages bot data
                new ClientRpcParams 
                {
                    Send = new ClientRpcSendParams 
                    {
                        TargetClientIds = new[] { (ulong)clientId }
                    }
                }
                : default);
    }

    /// <summary>
    /// From each client, gets and register the hand the the host distributed them
    /// </summary>
    /// <param name="auxHand"></param>
    /// <param name="playerEliminatedState"></param>
    /// <param name="leftEliminatedState"></param>
    /// <param name="topEliminatedState"></param>
    /// <param name="rightEliminatedState"></param>
    /// <param name="rpcParams"></param>
    [ClientRpc]
    public void RegisterClientHand_ClientRpc
        (int[] auxHand,
        bool playerEliminatedState,
        bool leftEliminatedState,
        bool topEliminatedState,
        bool rightEliminatedState,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Registering hand of the Client with id: <br>{localPlayerMatchID}</b></color>");

        currentGameMode.SetLocalPlayerHand
            (new List<int>(auxHand.ToList()),
            localPlayerMatchID,
            playerEliminatedState, 
            leftEliminatedState, 
            topEliminatedState, 
            rightEliminatedState, 
            OnClientHandIsDistributed);
    }

    /// <summary>
    /// Event calls as callback when the client finish to configure his hand
    /// </summary>
    /// <param name="clientID"></param>
    public void OnClientHandIsDistributed(int clientID)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hand of player with ClientId {clientID} distributed successfully</b></color>");

        ValidateIfClientStartsFirstTurn_Rpc(clientID);

        // Only the host configure the bots hands in each client (maybe use server instead every client for security)
        if (IsHost)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is trying to validate if bots starts the first turn...</color>");

            // Compact every client data into one list
            var clientsDataInfoPlayer = new List<PlayerDataInfo>()
            {
                currentGameMode.DataInfoPlayer_0,
                currentGameMode.DataInfoPlayer_1,
                currentGameMode.DataInfoPlayer_2,
                currentGameMode.DataInfoPlayer_3,
            };

            // Try to get those clients that are bots
            var botsClients = clientsDataInfoPlayer
                ?.Where(x => x.isBot)
                ?.ToArray();

            // Check if there are bots between the client
            if (botsClients is not null and { Length: > 0 })
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is validating whos starting first turn in <br>{botsClients.Length}</b> bots...</color>");

                for (int i = 0; i < botsClients.Length; i++)
                {
                    var botData = botsClients[i];
                    ValidateIfClientStartsFirstTurn_Rpc(botData.clientId);
                }
            }
        }
    }

    /// <summary>
    /// Once every player hand is distributed successfully, validate who of them starts the first turn
    /// </summary>
    /// <param name="clientId"></param>
    [Rpc(SendTo.Server)]
    public void ValidateIfClientStartsFirstTurn_Rpc(int clientId)
    {
        if (!playerUpdateComplete.Contains(clientId))
            playerUpdateComplete.Add(clientId);
        else
            Debug.Log("The player with ID already has his assigned hand: " + clientId);

        // Check if the progress reaches the player target quantity
        if (playerUpdateComplete.Count == ClientsCount.Value)
        {
            // Start the game
            Debug.Log("All players have their hands assigned. Starting the game.");

            // Try to get the ClientId of the player who takes the first turn
            int auxClientIdFirstPlayer = currentGameMode.SelectPlayerWhoWillTakeFirstTurn();

            // If the host couldn't get any client that starts first, shuffle the hands again
            if (auxClientIdFirstPlayer == -1)
            {
                ShufflingTilesAgainRpc();
                Debug.Log("===> *** No player selected for the first turn. Defaulting to Player 1.");
            } 
            
            // If not, make the client selected start the turn
            else
            {
                CurrentPlayablePlayerClientID.Value = auxClientIdFirstPlayer;
                StartTurnTimerForPlayerRPC();

                // Get the client data info of the current one to determine if this client is a bot
                var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(auxClientIdFirstPlayer);
                if (!currentPlayerDataInfo.isBot)
                    EnableTurnClientRpc(auxClientIdFirstPlayer);
                else
                    EnableTurnBotRpc(auxClientIdFirstPlayer);
            }
        } 
        
        else
            Debug.Log("Waiting for other players to assign their hands.");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShufflingTilesAgainRpc(bool restartNewRound = true)
    {
        playerUpdateComplete.Clear();
        
        StartCoroutine(currentGameMode.ExtendedGameController.TurnScript.AlertTextIE("Shuffling Tiles again!", 2.5f));

        if (IsServer)
        {
            enableTimerInHost = false;
            statusTimerInHost.Value = StatusTimerInHost.none;

            currentGameMode.SetupRandomHands();
        }

        currentGameMode.RestartGame(restartNewRound: restartNewRound);
        currentGameMode.SetPlayerMovementEvent(SendPlayerMovementToHost);

        ConfigureClientHand_Rpc(localPlayerMatchID, false);
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is trying to configure bots...</color>");
    }

    [Rpc(SendTo.Server)]
    private void ValideCurrentPlayerAvalibleTiles_Rpc(int playableClientId)
    {
        // Get the client data info of the current one to determine if this client is a bot
        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(playableClientId);

        // If the client is not bot, try to start its turn
        if (!currentPlayerDataInfo.isBot)
        {
            // Get the client available moves
            int auxValidTileCounter = currentGameMode.PlayerAvalibleTilesInGameMode(playableClientId);

            // If the client has moves that could be done, enable his turn
            if (auxValidTileCounter > 0)
            {
                Debug.Log($"+-+-+ Player {playableClientId} has {auxValidTileCounter} available tiles.");
                StartTurnTimerForPlayerRPC();

                EnableTurnClientRpc(playableClientId);
            } 

            // If not, open the boneyard if it's possible or enable pass button
            else
            {
                Debug.Log($"+-+-+ Player {playableClientId} has no available tiles."); //Enable boneyard tile selection

                CurrentPlayerHasNoValidMoves_ClientRpc(CurrentPlayablePlayerClientID.Value);
            }
        }

        // Else, the bot will manage its turn completely
        else
            EnableTurnBotRpc(playableClientId);
    }

    [ClientRpc]
    private void CurrentPlayerHasNoValidMoves_ClientRpc(int currentPlayablePlayerId)
    {
        Debug.Log($"--- Player {currentPlayablePlayerId} *** 01");

        if (currentGameMode.ExtendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode != null)
        {
            Debug.Log($"--- Player {currentPlayablePlayerId} *** 02");
            //currentGameMode.ExtendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode.Invoke(true);
            bool auxValidMovesInGameMode = currentGameMode.ExtendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode.Invoke(currentPlayablePlayerId == localPlayerMatchID);

            if (!auxValidMovesInGameMode /*&& clientId == localPlayerMatchID*/)
            {
                // Show notification or alert to the player
                Debug.Log($"--- Player {currentPlayablePlayerId} Enable Pass Button *** 03");
                ShowAlertTextIE_ClientRpc("The player passes the turn because he has no chips available.", 3f);

                currentGameMode.BoneyardIsEmpy(localPlayerMatchID == currentPlayablePlayerId, PassTurn_Rpc);
            }
            else
            {
                StartTurnTimerForPlayerRPC();
            }
        }
        else
        {
            Debug.Log($"--- Player {currentPlayablePlayerId} *** 04");
        }
    }

    [Rpc(SendTo.Server)]
    private void PassTurn_Rpc()
    {
        ValideCurrentPlayerAvalibleTiles_Rpc(NextTurn());
    }

    /// <summary>
    /// Starts the turn of the correspondly client
    /// </summary>
    /// <param name="clientID"></param>
    [ClientRpc]
    private void EnableTurnClientRpc(int clientID) //HERE TURN UI
    {
        currentGameMode.ExtendedGameController.DeckScript.Deck_HandleHasValidMoves?.Invoke();
        currentGameMode.StartTurn(clientID, localPlayerMatchID);
    }
    
    /// <summary>
    /// Starts the turn of the correspondly bot
    /// </summary>
    /// <param name="clientID"></param>
    [Rpc(SendTo.Server)]
    private void EnableTurnBotRpc(int clientID)
    {
        currentGameMode.StartTurn_Bot
            (clientID, 
            localPlayerMatchID,
            BotTakesFromHostBoneyard_Rpc, 
            PassTurn_Rpc);
    }

    #endregion

    #region Turns Timer
    [Rpc(SendTo.Server)]
    public void StartTurnTimerForPlayerRPC()
    {
        //currentPlayablePlayerMatchID.Value = (int)playerId;
        //ulong playerId = (ulong)currentPlayablePlayerMatchID.Value;
        if (IsServer)
        {
            TurnTimeRemaining.Value = turnDuration;
            //isTurnActive.Value = true;
            statusTimerInHost.Value = StatusTimerInHost.waitingTurn;
            enableTimerInHost = true;
        }


        //Debug.Log($"Start of turn for player {playerId} (has {turnDuration} seconds)");
        //NotifyTurnStartedClientRpc(playerId, turnDuration);
    }

    private void HandleTurnTimeout()
    {
        Debug.Log($"Player {CurrentPlayablePlayerClientID.Value} ran out of time.");

        // Here you can force turn passing, auto-play a piece, etc.
        //ForceEndTurnClientRpc((ulong)currentPlayablePlayerMatchID.Value);
        PassTurn_Rpc();

        //NextTurn(currentPlayablePlayerMatchID.Value);
        //StartTurnTimerForPlayerRPC();
    }

    [ClientRpc]
    private void NotifyTurnStartedClientRpc(ulong playerId, float duration)
    {
        Debug.Log($"It's player {playerId}'s turn, with {duration} seconds.");
        // Visually activate a timer or highlight the current player
    }

    [ClientRpc]
    private void ForceEndTurnClientRpc(ulong playerId)
    {
        Debug.Log($"Player {playerId} lost the turn due to timeout.");
        //Valide: In Casual Mode: A bot will play a random tile on behalf of the player. In Competitive Mode: The player's turn will be skipped directly.
        // Show notification or animation
        PassTurn_Rpc();
    }
    #endregion

    #region Player movement in turn
    private void SendPlayerMovementToHost(int tileID, string sideInfo)
    {
        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(CurrentPlayablePlayerClientID.Value);
        if (!currentPlayerDataInfo.isBot)
        { 
            Debug.Log($"++-- Player {localPlayerMatchID} played tile {tileID} on side {sideInfo}.");
            currentGameMode.HandOfLocalPlayer.Remove(tileID);

            var leaderboardEntry = leaderboardManager.GetLeaderboardEntry(leaderboardManager.GetLeaderboardID(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID));
            var factorK = 32;

            if (leaderboardEntry is not null 
                && gameManager is not null 
                and {GameBackendConfigData: not null 
                and { leaderboardConfig: not null 
                and { kFactorByTier: not null 
                and { Length: > 0} } } })
            {
                var tierToCheck = Enum.TryParse(leaderboardEntry.Tier, out LeaderboardTier tier) ? tier : LeaderboardTier.None;
               factorK = gameManager.GameBackendConfigData.leaderboardConfig.kFactorByTier
                    .FirstOrDefault(x => x.leaderboardTier == tierToCheck)
                    ?.kFactor ?? factorK;
            }

            PlayerMovementHost_Rpc(tileID, sideInfo, localPlayerMatchID, factorK);
        }
        else
            PlayerMovementHost_Rpc(tileID, sideInfo, currentPlayerDataInfo.clientId);
    }

    [Rpc(SendTo.Server)]
    private void PlayerMovementHost_Rpc(int tileID, string sideInfo, int clientId, int factorK = 0)
    {
        Debug.Log($"Player {clientId} played tile {tileID}.");

        if (clientId != CurrentPlayablePlayerClientID.Value)
            return; // Ignore movements from players who are not currently active

        var clientHands = new Dictionary<int, List<int>>();

        if (ClientsCount.Value is >= 2)
        {
            clientHands.Add(0, currentGameMode.GetHandOfPlayerWithID(0));
            clientHands.Add(1, currentGameMode.GetHandOfPlayerWithID(1));
        }

        if (ClientsCount.Value is 4)
        {
            clientHands.Add(2, currentGameMode.GetHandOfPlayerWithID(2));
            clientHands.Add(3, currentGameMode.GetHandOfPlayerWithID(3));
        }

        // Check if the player that moved the tile is the same that the current client
        // Try to define the valeu of the move that the player did
        if (clientId == localPlayerMatchID)
        { 
            var moveEvaluationResult = currentGameMode.DominoAI.EvaluateMove
                (tileID,
                currentGameMode.GameModeID, 
                clientHands.ToDictionary(x => x.Key, x => currentGameMode.ConvertIdTilesListToDominoList(x.Value)),
                currentGameMode.ConvertIdTilesListToDominoList(currentGameMode.GetBoardTiles()),
                currentGameMode.ConvertIdTilesListToDominoList(currentGameMode.GetBoneyardTiles()),
                factorK);

            // Try to get the index of the client in the list of ClientsEMCs
            var index = FindClientEMCIndex(clientId);

            // Check if the client's EMC already exists in the list (linq couldn't be used here because NetworkList doesn't support it)
            if (index.HasValue && index >= 0)
            {
                var clientEMC = ClientsEMCs[index.Value];
                clientEMC.AddToEMC(moveEvaluationResult.EMC);
                ClientsEMCs[index.Value] = clientEMC;
            }

            // Else, create a new entry for the client
            else
            {
                var newEntry = new ClientEMC(clientId);
                newEntry.AddToEMC(moveEvaluationResult.EMC);
                ClientsEMCs.Add(newEntry);
            }
        }

        // Remove the tile moved from the hand to registered on the board
        currentGameMode.RemoveTileFromUserHand_InHost(clientId, tileID);

        enableTimerInHost = false;
        statusTimerInHost.Value = StatusTimerInHost.none;

        playerUpdateComplete.Clear();

        PlayerMovement_ClientRpc(tileID, sideInfo, clientId);
    }

    [ClientRpc]
    private void PlayerMovement_ClientRpc(int tileID, string sideInfo, int clientId)
    {
        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(clientId);
        if (!currentPlayerDataInfo.isBot)
        {
            Debug.Log($"> ++-- 00 Player {clientId} played tile {tileID} on side {sideInfo}.");
            if (localPlayerMatchID != clientId)
            {
                Debug.Log($"> ++-- 01 Player {clientId} played tile {tileID} on side {sideInfo}.");
                currentGameMode.UpdateTilePlacedByPlayer
                    (tileID,
                    sideInfo,
                    clientId,
                    localPlayerMatchID,
                    () => ValidateMovementDeliveredAllClients_Rpc(localPlayerMatchID));
            }
        } 
        else
            ValidateMovementDeliveredAllClients_Rpc(localPlayerMatchID);

        if (IsHost)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is trying to validate every bot move...</color>");

            // Compact every client data into one list
            var clientsDataInfoPlayer = new List<PlayerDataInfo>()
            {
                currentGameMode.DataInfoPlayer_0,
                currentGameMode.DataInfoPlayer_1,
                currentGameMode.DataInfoPlayer_2,
                currentGameMode.DataInfoPlayer_3,
            };

            // Try to get those clients that are bots
            var botsClients = clientsDataInfoPlayer
                ?.Where(x => x.isBot)
                ?.ToArray();

            // Check if there are bots between the client
            if (botsClients is not null and { Length: > 0 })
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is validating move delivered to each client <br>{botsClients.Length}</b> bots...</color>");
                
                for (int i = 0; i < botsClients.Length; i++)
                {
                    var botData = botsClients[i];
                    if (botData.clientId != clientId)
                        ValidateMovementDeliveredAllClients_Rpc(botData.clientId);
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ValidateMovementDeliveredAllClients_Rpc(int playerID)
    {
        if (!playerUpdateComplete.Contains(playerID)) //Checks if this player's movement has already been updated.
        {
            playerUpdateComplete.Add(playerID);
            
            if (playerUpdateComplete.Count == (ClientsCount.Value - 1)) //It is ClientsCount.Value - 1 because it does not take into account the player who moved the tile.
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> All players have updated movement. Proceeding to next turn.</color>");

                string gameOverCase = "";

                if (!Check_GameOver(ref gameOverCase))
                {
                    ValideCurrentPlayerAvalibleTiles_Rpc(NextTurn());
                }
                else
                {
                    //Debug.Log("++--> Round or game completed");
                    playerUpdateComplete.Clear(); //It is important to delete this list at this point in order to validate the rematch or the next round.

                    bool auxGameIsCompleteAndFinished = currentGameMode.ExtendedGameController.GameIsCompleteAndFinished;

                    NotifyEndOfRoundAndUpdateScores_ClientRpc(
                        currentGameMode.ExtendedGameController.TurnScript.PlayerScore,
                        currentGameMode.ExtendedGameController.TurnScript.LeftAIScore,
                        currentGameMode.ExtendedGameController.TurnScript.TopAIScore,
                        currentGameMode.ExtendedGameController.TurnScript.RightAIScore,
                        gameOverCase,
                        auxGameIsCompleteAndFinished
                    );

                    if (auxGameIsCompleteAndFinished)
                    {
                        TurnTimeRemaining.Value = waitTimeForRematch;
                        //waitConfirmationForRematch = true;
                        statusTimerInHost.Value = StatusTimerInHost.waitingRematch;
                        enableTimerInHost = true;
                    }
                    else
                    {
                        TurnTimeRemaining.Value = waitTimeForNextRound;
                        //waitConfirmationForNextRound = true;
                        statusTimerInHost.Value = StatusTimerInHost.waitingNextRound;
                        enableTimerInHost = true;
                    }
                    
                    //currentGameMode.ExtendedGameController.TurnScript.EndRound(gameOverCase);
                }
            }
            else
            {
                Debug.Log("Waiting for the movement to be updated to other players.");
            }
        }
        else
        {
            Debug.Log("The player with ID already has updated:" + playerID);
        }
    }

    private bool Check_GameOver(ref string gameOverCase)
    {
        int auxValidTileCounter = 0;
        auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
        auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
        auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
        auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
        auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

        Debug.Log("++--> auxValidTileCounter: " + auxValidTileCounter);

        //string gameOverCase = "";

        bool auxResult = currentGameMode.CheckRound_GameOver(ref gameOverCase, gameIsBlocked: auxValidTileCounter == 0);

        return auxResult;
    }

    private int NextTurn()
    {
        bool nextPlayerIsEliminated = true;

        int auxNextTurnID = CurrentPlayablePlayerClientID.Value;

        int auxPlayerCounter = 0;

        while (nextPlayerIsEliminated && auxPlayerCounter < 4) 
        {
            auxNextTurnID = auxNextTurnID + 1 > ClientsCount.Value - 1 ? 0 : auxNextTurnID + 1;

            nextPlayerIsEliminated = currentGameMode.ValidateIfPlayerIsEliminated_InHost(auxNextTurnID);

            Debug.Log("-->-- " + "nextPlayerIsEliminated: " + nextPlayerIsEliminated + ", auxNextTurnID: " + auxNextTurnID + ", auxPlayerCounter: " + auxPlayerCounter);

            auxPlayerCounter++;
        }

        //Debug.Log("-->-- " + "nextPlayerIsEliminated: " + nextPlayerIsEliminated + ", auxNextTurnID: " + auxNextTurnID + ", auxPlayerCounter: " + auxPlayerCounter);

        if (!nextPlayerIsEliminated)
        {
            CurrentPlayablePlayerClientID.Value = auxNextTurnID;
            //CurrentPlayablePlayerClientID.Value = CurrentPlayablePlayerClientID.Value + 1 > ClientsCount.Value - 1 ? 0 : CurrentPlayablePlayerClientID.Value + 1;
        }
        else
        {
            Debug.Log("All players are eliminated");
        }

        Debug.Log("NextTurn: " + CurrentPlayablePlayerClientID.Value);

        return CurrentPlayablePlayerClientID.Value;
    }

    private int NextTurn(int playerMatch_ID)
    {
        CurrentPlayablePlayerClientID.Value = playerMatch_ID + 1 > ClientsCount.Value - 1 ? 0 : playerMatch_ID + 1;

        Debug.Log("NextTurn: " + CurrentPlayablePlayerClientID.Value);

        return CurrentPlayablePlayerClientID.Value;
    }

    [ClientRpc]
    public void NotifyEndOfRoundAndUpdateScores_ClientRpc(int scorePlayer_0, int scorePlayer_1, int scorePlayer_2, int scorePlayer_3, string gameOverCase, bool auxGameIsCompleteAndFinished)
    {
        currentGameMode.UpdateScoreAndShowRoundResult(gameOverCase, scorePlayer_0, scorePlayer_1, scorePlayer_2, scorePlayer_3, localPlayerMatchID, auxGameIsCompleteAndFinished);
    }

    public void RestartGameModeRound() //HERE
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Client {localPlayerMatchID} is trying to restart teh game round...</color>");
        RestartGameModeRound_Rpc(localPlayerMatchID);
    }

    public void RematchGameMode()
    {
        RematchGameMode_Rpc(localPlayerMatchID);
    }

    [Rpc(SendTo.Server)]
    public void RestartGameModeRound_Rpc(int clientId)
    {
        if (!playerUpdateComplete.Contains(clientId)) //Checks if this player's movement has already been updated.
        {
            playerUpdateComplete.Add(clientId);

            Debug.Log("+-++ playerUpdateComplete: " + playerUpdateComplete.Count + " / ClientsCount.Value: " + ClientsCount.Value);

            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                ShufflingTilesAgainRpc();
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void RematchGameMode_Rpc(int playerID)
    {
        if (playerUpdateComplete.Count == 0) //The first player to request a rematch resets the scores.
        {
            currentGameMode.ExtendedGameController.TurnScript.PlayerScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.LeftAIScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.TopAIScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.RightAIScore = 0;
        }
        
        if (!playerUpdateComplete.Contains(playerID)) //Checks if this player's movement has already been updated.
        {
            playerUpdateComplete.Add(playerID);

            Debug.Log("+-++ playerUpdateComplete: " + playerUpdateComplete.Count + " / ClientsCount.Value: " + ClientsCount.Value);

            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                ShufflingTilesAgainRpc(restartNewRound: false);
            }
        }
    }

    
    [ClientRpc]
    public void ExitTheGameAndGoToMainMenu_ClientRpc()
    {
        menuControllerGameMode.ToLobby();
    }
    #endregion

    #region Player Boneyard

    private int currentBoneyardTileSelected = -1;
    private void PlayerTakesFromBoneyard(int indexOfSelectedTile)
    {
        currentBoneyardTileSelected = -1;

        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(CurrentPlayablePlayerClientID.Value);
        ClientTakesFromHostBoneyard_Rpc
            (!currentPlayerDataInfo.isBot ? localPlayerMatchID : currentPlayerDataInfo.clientId, 
            indexOfSelectedTile);
    }

    /// <summary>
    /// Replicates the obtaining of the tile moved on the client, now on the server side
    /// </summary>
    /// <param name="playerID"></param>
    /// <param name="indexOfSelectedTile"></param>
    [Rpc(SendTo.Server)]
    private void ClientTakesFromHostBoneyard_Rpc(int playerID, int indexOfSelectedTile)
    {
        Debug.Log($"**** Player {playerID} takes tile from boneyard at index {indexOfSelectedTile}.");

        if (playerID == CurrentPlayablePlayerClientID.Value)
        {
            playerUpdateComplete.Clear();

            currentBoneyardTileSelected = currentGameMode.GetTileFromBoneyard_InHost(indexOfSelectedTile);
            currentGameMode.AddingTileToSpecificPlayerHand(playerID, currentBoneyardTileSelected);

            DeliverBoneyardTileToPlayer_ClientRpc(currentBoneyardTileSelected, indexOfSelectedTile, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { (ulong)playerID }
                }
            });
            
            NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(playerID, indexOfSelectedTile);
        }
    }
    
    /// <summary>
    /// Callback called when the bot doesn't have playable tiles
    /// </summary>
    /// <param name="playerID"></param>
    /// <param name="indexOfSelectedTile"></param>
    [Rpc(SendTo.Server)]
    private void BotTakesFromHostBoneyard_Rpc(int playerID, int indexOfSelectedTile)
    {
        Debug.Log($"**** Bot {playerID} takes tile from boneyard at index {indexOfSelectedTile}.");

        if (playerID == CurrentPlayablePlayerClientID.Value)
        {
            playerUpdateComplete.Clear();

            currentBoneyardTileSelected = currentGameMode.GetTileFromBoneyard_InHost(indexOfSelectedTile);
            currentGameMode.AddingTileToSpecificPlayerHand(playerID, currentBoneyardTileSelected);

            NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(playerID, indexOfSelectedTile);
        }
    }

    [ClientRpc]
    public void NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(int clientId, int indexOfSelectedTile)
    {
        var playerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(clientId);

        // Send boneyard tile to hand if the local clientID is not the same the one who got the tile and is not a bot 
        if (clientId != localPlayerMatchID && !playerDataInfo.isBot)
        {
            //Simule boneyard tile selection for the player
            Debug.Log($"Player {clientId} has selected a tile from the boneyard.");
            currentGameMode.SendBoneyardTileToHand_FromHost
                (clientId, 
                localPlayerMatchID, 
                indexOfSelectedTile, 
                ValidateBoneyardDeliveredAllClients);
        }

        if (IsHost)
        {
            // No move, but inform that the host already got the boneyard tile
            ValidateBoneyardDeliveredAllClients();

            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is trying to validate every bot boneyard...</color>");

            // Compact every client data into one list
            var clientsDataInfoPlayer = new List<PlayerDataInfo>()
            {
                currentGameMode.DataInfoPlayer_0,
                currentGameMode.DataInfoPlayer_1,
                currentGameMode.DataInfoPlayer_2,
                currentGameMode.DataInfoPlayer_3,
            };

            // Try to get those clients that are bots
            var botsClients = clientsDataInfoPlayer
                ?.Where(x => x.isBot)
                ?.ToArray();

            // Check if there are bots between the client
            if (botsClients is not null and { Length: > 0 })
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Hosting is validating boneyard to each client <br>{botsClients.Length}</b> bots...</color>");

                for (int i = 0; i < botsClients.Length; i++)
                {
                    var botData = botsClients[i];
                    if (botData.clientId != localPlayerMatchID)
                         ValidateIfBoneyardIsUpdatedForAllClients_Rpc(botData.clientId);
                }
            }
        }
    }

    [ClientRpc]
    public void DeliverBoneyardTileToPlayer_ClientRpc(int tileID, int indexOfSelectedTile, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"Delivering tile {tileID} to player {localPlayerMatchID}.");

        //currentGameMode.SendBoneyardCardToHand_FromHost(localPlayerMatchID, localPlayerMatchID, indexOfSelectedTile);
        currentGameMode.SendBoneyardTileToHand_FromHost
            (localPlayerMatchID, 
            localPlayerMatchID, 
            indexOfSelectedTile, 
            ValidateBoneyardDeliveredAllClients, tileID);
    }

    private void ValidateBoneyardDeliveredAllClients()
    {
        ValidateIfBoneyardIsUpdatedForAllClients_Rpc(localPlayerMatchID);
    }

    [Rpc(SendTo.Server)]
    private void ValidateIfBoneyardIsUpdatedForAllClients_Rpc(int clientId)
    {
        if (!playerUpdateComplete.Contains(clientId)) //Checks if this player's movement has already been updated.
        {
            playerUpdateComplete.Add(clientId);

            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> All players have updated boneyard. Proceeding to validate hand moves of the current player.</color>");
                ValideCurrentPlayerAvalibleTiles_Rpc(CurrentPlayablePlayerClientID.Value);
            }
            else
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Waiting for the boneyard to be updated to other players.</color>");
        }
        else
            Debug.LogWarning($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> The player with ID already has updated: <b>{clientId}</b>.</color>");
    }
    #endregion

    #region Lobby Chat Rpcs

    private int banAlert_player_0 = 0;
    private int banAlert_player_1 = 0;
    private int banAlert_player_2 = 0;
    private int banAlert_player_3 = 0;
    private List<string> playersWithBanReport = new List<string>();

    private void SendMessageFromClientToHost(string message)
    {
        ReceiveMessageInChatFromClient_Rpc(localPlayerMatchID, message);
    }

    [Rpc(SendTo.Server)]
    private void ReceiveMessageInChatFromClient_Rpc(int fromClientID, string message)
    {
        bool permittedMessage = true;

        switch (fromClientID)
        {
            case 0:
                permittedMessage = banAlert_player_0 < ClientsCount.Value / 2;
                break;
            case 1:
                permittedMessage = banAlert_player_1 < ClientsCount.Value / 2;
                break;
            case 2:
                permittedMessage = banAlert_player_2 < ClientsCount.Value / 2;
                break;
            case 3:
                permittedMessage = banAlert_player_3 < ClientsCount.Value / 2;
                break;
            default:
                Debug.Log("Invalid player id");
                break;
        }

        if (permittedMessage)
        {
            UpdateMessageInChatFromAll_ClientRpc(fromClientID, message, DateTime.UtcNow);
        }
        else
        {
            Debug.Log("Player " + fromClientID + " is baned from chat");
        }
    }

    [ClientRpc]
    private void UpdateMessageInChatFromAll_ClientRpc(int fromClientID, string message, DateTime dateTime)
    {
        var fromPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(fromClientID);
        currentGameMode.ShowMessageAlert();
        lobbyChatManager.ShowMessageInChat
            (fromClientID, 
            localPlayerMatchID, 
            fromPlayerDataInfo.userId, 
            fromPlayerDataInfo.username,
            message, 
            dictionaryService.GetSprite(Consts.CollectionKeys.Icons, fromPlayerDataInfo.profileIconID),
            dateTime, 
            SendBanToPlayer);
    }

    public void SendBanToPlayer(int playerID)
    {
        string ban_id = localPlayerMatchID + "_" + playerID;

        if (playerID != localPlayerMatchID && !playersWithBanReport.Contains(ban_id))
        {
            playersWithBanReport.Add(ban_id);
            AddBanToPlayer_Rpc(playerID);
        }
        else if (playerID == localPlayerMatchID)
        {
            Debug.Log("You can't ban yourself");
        }
        else
        {
            Debug.Log("Already reports ban by this player");
        }
    }

    [Rpc(SendTo.Server)]
    private void AddBanToPlayer_Rpc(int playerID)
    {
        switch (playerID)
        {
            case 0:
                banAlert_player_0++;
                break;
            case 1:
                banAlert_player_1++;
                break;
            case 2:
                banAlert_player_2++;
                break;
            case 3:
                banAlert_player_3++;
                break;
            default:
                Debug.Log("Invalid player id");
                break;
        }
    }

    #endregion

    #region Notifications to clients

    [ClientRpc]
    private void ShowAlertTextIE_ClientRpc(string message, float duration)
    {
        StartCoroutine(currentGameMode.ExtendedGameController.TurnScript.AlertTextIE(message, duration));
    }

    #endregion

    #region Auxiliary methods


    [Rpc(SendTo.ClientsAndHost)]
    private void TryToStartMatchRPC
        (bool isReadyToStart,
        
        int requiredClients,
        GameMode gameMode = GameMode.none, 
        GameType gameType = GameType.none, 
        NumberPlayers numberPlayers = NumberPlayers.none,
        ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none)
    {
        // Register the number of players that the session will expect
        if (IsServer)
            ClientsCount.Value = requiredClients;

        // Get the number of clients connected to the session
        var connectedClients = NetworkManager.Singleton.ConnectedClients.Count;
        var usingBotsToFill = useBotMatchmaking && requiredClients != connectedClients;

        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Try to start match." +
            $"\n\nIs Ready to start?: {isReadyToStart}" +
            $"\nConnected Clients: {connectedClients}" +
            $"\nRequired Clients: {requiredClients}" +
            $"\nUsing Bots To Fill: {usingBotsToFill}" +
            $"\nGameMode: {gameMode}, GameType: {gameType}, Number of Players: {numberPlayers}, Concentrate Number of Tiles: {concentrateNumberOfTiles}" +
            $"</color>");

        // Check if the number of connected clients is equal to the required number of players and if the matchmaking coroutine is running
        if (isReadyToStart 
            && (connectedClients == requiredClients || usingBotsToFill) 
            && currentGameMode == null)
        {
            // Once the everyone is connected, start the game
            Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> All players connected. Starting game...</color>");
            
            if (IsServer)
            {
                // Reset the record of EMCs when a new game starts
                ClientsEMCs.Clear();

                //RemoveJoinCode(joinCodeManager.GameID, joinCodeManager.JoinCode);
                playerUpdateComplete.Clear();
            }

            // Stops the matchmaking process if the required number of players is reached
            StopMatchmakingTimer();

            // If optional data has value, use these instead the client ones (for party purposes mainly)
            var optionalGameModeData = 
                    (gameMode is not GameMode.none 
                    && gameType is not GameType.none 
                    && numberPlayers is not NumberPlayers.none)
                ? new(gameMode, gameType, numberPlayers, concentrateNumberOfTiles)
                : default(GameModeData);

            // Create the game mode instance and set it up
            CreateGameMode(requiredClients, optionalGameModeData, usingBotsToFill);

            //Turn off the UI for selecting game mode
            if (currentGameMode)
                currentGameMode.GameModeConfig.SetSelectionUIVisibility(false);
            else
                Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> currentGameMode is null, cannot set selection UI visibility.</color>");

            // Once the game mode is created, we can start the game
            OnGameStarted?.Invoke(this, EventArgs.Empty);
        } 
    }

    /// <summary>
    /// Sets the PlayerBasicInfo of a client in the server-side collection.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SetClientPlayerIdRPC(PlayerBasicInfo playerBasicInfo)
    {
        if (!IsServer)
            throw new InvalidOperationException($"Only the server can set player info.");

        // Get a copy of the current infos
        var infos = clientsBasicInfoCollection.Value.playerBasicInfos;

        // Find the first empty slot and assign
        for (int i = 0; i < infos.Length; i++)
            if (string.IsNullOrEmpty(infos[i].userId))
            {
                infos[i] = playerBasicInfo;
                break;
            }

        // Reassign the whole struct so NGO detects the change
        clientsBasicInfoCollection.Value = new ClientsBasicInfoCollection(
            infos.ElementAtOrDefault(0),
            infos.ElementAtOrDefault(1),
            infos.ElementAtOrDefault(2),
            infos.ElementAtOrDefault(3));
    }

    /// <summary>
    /// Retrieves the userId string for a given clientId.
    /// </summary>
    private string GetClientPlayerIDVariable(ulong clientID)
    {
        var infos = clientsBasicInfoCollection.Value.playerBasicInfos;
        var playerBasicInfo = infos.FirstOrDefault(x => x.idInHost == clientID);

        if (string.IsNullOrEmpty(playerBasicInfo.userId))
            Debug.LogWarning($"[MatchManager_Old] Player ID not found for client ID {clientID}.");

        return playerBasicInfo.userId;
    }

    /// <summary>
    /// Gets the remaining time for the current turn.
    /// </summary>
    /// <returns></returns>
    public float GetRemainingTime() => TurnTimeRemaining.Value;

    /// <summary>
    /// Aux to find the index of the client in the ClientsEMCs list.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private int? FindClientEMCIndex(int clientId)
    {
        for (int i = 0; i < ClientsEMCs.Count; i++)
            if (ClientsEMCs[i].ClientId == clientId)
                return i;
        return null;
    }

    /// <summary>
    /// Updates the player profile with the selected cosmetic by calling the backend module.
    /// </summary>
    /// <returns></returns>
    private async void SendPlayerResult(bool isPlayerWinner)
    {
        if (currentGameMode.GameTypeSelectedID is GameType.casual)
        {
            try
            {
                // Use try catch to control the exception and be able to turn on the buttons again
                await leaderboardManager.UpdateCasualAnalytics(isPlayerWinner);

                // Once the match data is updated, refresh the game manager's protected player data
                if (gameManager is not null)
                    await gameManager.RefreshProtectedPlayerData();
                else
                    Debug.LogWarning("Failed to deserialize profile data response.");

            }
            catch (Exception ex)
            {
                Debug.LogError($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager_Old)}]</b> Error while updating casual match result: {ex.Message}</color>");
            }
        }
        else if (currentGameMode.GameTypeSelectedID is GameType.competitive)
        { 
            var clientEMCIndex = FindClientEMCIndex(localPlayerMatchID);

            // Check if the client EMC index was found, if not, log a warning
            if (!clientEMCIndex.HasValue)
                Debug.LogWarning($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager_Old)}]</b> Client EMC for player {localPlayerMatchID} not found. Updating with the minimmun value (0).</color>");

            // Try to get the average EMC for the client, if it doesn't exist, use 0
            var matchEMC = clientEMCIndex.HasValue && ClientsEMCs.Count > clientEMCIndex.Value 
                ? ClientsEMCs[clientEMCIndex.Value].AverageEMC 
                : 0;

            try
            {
                // Use try catch to control the exception and be able to turn on the buttons again
                await leaderboardManager.UpdateLeadeboardResult(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID, isPlayerWinner, matchEMC);

                // Once the match data is updated, refresh the game manager's protected player data
                if (gameManager is not null)
                    await gameManager.RefreshProtectedPlayerData();
                else
                    Debug.LogWarning("Failed to deserialize profile data response.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager_Old)}]</b> Error while updating competitive match result: {ex.Message}</color>");
            }
        }
    }

    /// <summary>
    /// Stops the matchmaking process if it is currently running
    /// </summary>
    private void StopMatchmakingTimer()
    {
        // If the matchmaking coroutine is running, stop it
        if (matchmakingTimerCoroutine is null)
        {
            Debug.LogWarning("Matchmaking coroutine is already stopped");
            return;
        }

        // Stop the timer to stop the coroutine
        StopCoroutine(matchmakingTimerCoroutine);
        matchmakingTimerCoroutine = null;

        // Once the coroutine was stopped, invoked a callback informing
        OnCancelMatchmakingEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Try to reconnect to the last registered session
    /// </summary>
    /// <returns></returns>
    private async UniTask<bool> TryToReconnect()
    {
        if (MatchmakingSession is null)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> MatchmakingSession is null, cannot reconnect anymore.</color>");
            return false;
        }

        var attempts = 0;
        while (MatchmakingSession != null && attempts <= newtworkProcessAttempt)
        {
            Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Attempting to reconnect to session... Attempt {attempts + 1}/{newtworkProcessAttempt}</color>");
            try
            {
                // Try to get the joined session IDs
                await MultiplayerService.Instance.GetJoinedSessionIdsAsync();

                // Try to reconnect to the session
                await (MatchmakingSession?.ReconnectAsync() ?? default);

                Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Successfully reconnected to session.</color>");
                return true;
            }
            catch (SessionException e)
            {
                Debug.LogError($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Failed to reconnect to session: {e.Message}</color>");
                attempts++;
                if (attempts >= newtworkProcessAttempt)
                {
                    var errorMessage = $"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to reconnect to session</color>";
                    Debug.LogError(errorMessage);
                }
            
                // Wait for a short period before retrying
                var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
            }
        }

        return false;
    }
    #endregion

    #region Events
    /// <summary>
    /// Event triggered when a player joins the session.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="data"></param>
    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType is ConnectionEvent.ClientConnected or ConnectionEvent.PeerConnected)
        {
            var isReconnection = false;

            // Try to send the player ID to the server if the event is ClientConnected
            if (data.EventType is ConnectionEvent.ClientConnected)
            {
                // Check if the client was not connected before 
                if (!clientsBasicInfoCollection.Value.playerBasicInfos.Any(x => x.userId != string.Empty && x.idInHost == data.ClientId))
                { 
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Player ID set for client {data.ClientId}: {authManager.UUID}</color>");
                    SetClientPlayerIdRPC(new(data.ClientId, authManager.UUID));
                }

                // Else, the client was connected before (reconnection)
                else
                {
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Client {data.ClientId} is trying to reconnect</color>");
                    isReconnection = true;
                    // ValidateReconnection_Rpc(data.ClientId); ===> Validate reconnection process from Server Host
                }
            }

            // Only report to clients that a new one is connect itf the reconnect process is not started
            if (!isReconnection)
            { 
                // Try to get the player ID from the server
                if (clientsBasicInfoCollection is not null)
                    Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Players Ids registered:\n\n{JsonConvert.SerializeObject(clientsBasicInfoCollection.Value, Formatting.Indented)} </color>");
                else
                    Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> ClientsBasicInfoCollection is null.</color>");

                var playerID = GetClientPlayerIDVariable(data.ClientId);
                Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Player joined session: {playerID}</color>");

                // Inform the game mode that a player has joined
                OnJoinSession?.Invoke(this, playerID);
                TriggerOnCliendConnectedRpc();
            }
        }

        // If the session is not null, is possible reconnect againg. Else, the clidnt doesn't have nay session to reconnect
        else if (MatchmakingSession is not null)
        {
            if (data.EventType is ConnectionEvent.ClientDisconnected)
            {
                if (manager.IsHost)
                    manager.Shutdown(true); // (check if is possible relieve responsibility to another client)

                else
                    MatchmakingSession.ReconnectAsync();
            } 

            else if (data.EventType is ConnectionEvent.PeerDisconnected)
            {
                if (manager.IsHost)
                { 
                    //StartReconnectionProcess_Rpc(data.ClientId); ===> Start reconnection process from Server Host
                }
            }
        }

        // But, if the one disconnected is the host. Shudown the entire match session (check if is possible relieve responsibility to another client)
        else  if (manager.IsHost)
            manager.Shutdown(true);
    }

    /// <summary>
    /// Handles the player joining the session.
    /// </summary>
    /// <param name="playerID"></param>
    private void OnPlayerJoiningSession(string playerID)
    { 
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Player joining session: {playerID}</color>");

        // Check if the player was already registered. If does, calls reconnect event
        if (clientsBasicInfoCollection is not null
            && clientsBasicInfoCollection.Value.playerBasicInfos.Any(x => x.userId == playerID))
        {
            OnReconnectSession?.Invoke(this, playerID);
        }
    }

    /// <summary>
    /// Handles the player leaving the session.
    /// </summary>
    /// <param name="playerID"></param>
    private async void OnPlayerLeavingSession(string playerID)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Player leaving session: {playerID}</color>");

        // Try to get the reasonToLeave for disconnection from session properties
        var playerToLeave = MatchmakingSession.Players.FirstOrDefault(x => x.Id == playerID);
        if (playerToLeave is null)
        { 
            Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Player to leave lost reference value. Couldn't be possible reconnect to the last session.</color>");
            return;
        }

        // Try to get the player to leave leave reasonToLeave
        var reason = playerToLeave.Properties.TryGetValue(Consts.CollectionKeys.LeaveReason, out var reasonSt) ? reasonSt.Value : string.Empty;

        // If the reasonToLeave for disconnection is not due to the player leaving on their own will, attempts to reconnect
        if (reason is not Consts.Reasons.LeftingOwnWill)
        {
            if (await TryToReconnect())
            { 
                Debug.Log($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Session recovery successfully. The last session was linked properly.</color>");
                return;
            }

            Debug.LogWarning($"<color={Consts.Colors.NetworkError}><b>[{nameof(MatchManager_Old)}]</b> Session lost. Couldn't be possible reconnect to the last session.</color>");
        }

        // If could not reconnect, remove the join code and notify the game mode
        await LeaveMatchSession();

        // Inform the game mode that the player has left
        OnLeftSession?.Invoke(this, playerID);
    }

    /// <summary>
    /// Handles the session being removed, which can happen when the host leaves or the session is closed.<br></br><br></br>
    /// * This method unsubscribes from session events and informs the game mode that the session has been removed.
    /// </summary>
    /// <param name="session"></param>
    private async void OnSessionRemoved(ISession session)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> MatchmakingSession removed: {session.Id}</color>");

        if (await TryToReconnect())
        {
            Debug.LogWarning($"<color={Consts.Colors.NetworkSuccess}><b>[{nameof(MatchManager_Old)}]</b> Session recovery successfully. The last session was linked properly.</color>");
            return;
        }

        // If could not reconnect, remove the join code and notify the game mode
        await LeaveMatchSession();

        // Unsubscribe from session events
        session.PlayerLeaving -= OnPlayerLeavingSession;

        // Inform the game mode that the session has been removed
        OnSessionRemovedEvent?.Invoke(this, EventArgs.Empty);
    }

    public async void OnCreateRelayLobbyID(Action<string> callback, NumberPlayers numberPlayers)
    {
        switch (numberPlayers)
        {
            case NumberPlayers.oneVsOne:
                relayManager.maxPlayers = 2;
                ClientsCount.Value = 2;
                break;
            case NumberPlayers.oneVsThree:
                relayManager.maxPlayers = 4;
                ClientsCount.Value = 4;
                break;
            case NumberPlayers.twoVsTwo:
                relayManager.maxPlayers = 4;
                ClientsCount.Value = 4;
                break;
            default:
                Debug.LogWarning("Invalid number of players");
                break;
        }

        string joinCode = await relayManager.CreateRelay();

        callback?.Invoke(joinCode);
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn: " + NetworkManager.Singleton.LocalClientId);

        localPlayerMatchID = (int)NetworkManager.Singleton.LocalClientId;

        NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
        MultiplayerService.Instance.SessionRemoved += OnSessionRemoved;
        CurrentPlayablePlayerClientID.OnValueChanged += OnCurrentPlayablePlayerModified;
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log("OnNetworkDespawn: " + NetworkManager.Singleton.LocalClientId);

        localPlayerMatchID = -1;

        NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        MultiplayerService.Instance.SessionRemoved -= OnSessionRemoved;
        CurrentPlayablePlayerClientID.OnValueChanged -= OnCurrentPlayablePlayerModified;
    }

    private void OnCurrentPlayablePlayerModified(int previousValue, int newValue)
    {
        Debug.Log($"<color={Consts.Colors.NetworkProcess}><b>[{nameof(MatchManager_Old)}]</b> Current playable player changed from {previousValue} to {newValue}</color>");

        // Inform the game mode that the current playable player has changed
        OnCurrentPlayablePlayerChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    public struct ClientEMC : INetworkSerializable, IEquatable<ClientEMC>
    {
        public int ClientId;
        public float MatchEMC;
        private int emcsCount;

        [JsonIgnore]
        public readonly float AverageEMC => emcsCount > 0 ? MatchEMC / emcsCount : 0f;

        // Constructor for safety
        public ClientEMC(int clientId)
        {
            ClientId = clientId;
            MatchEMC = 0f;
            emcsCount = 0;
        }

        public void AddToEMC(float value)
        {
            MatchEMC += value;
            emcsCount++;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref MatchEMC);
            serializer.SerializeValue(ref emcsCount);
        }

        public bool Equals(ClientEMC other)
        {
            // Float equality with tolerance
            return ClientId == other.ClientId && Math.Abs(MatchEMC - other.MatchEMC) < 0.0001f;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientEMC other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, MatchEMC);
        }
    }

    public struct PlayerBasicInfo : INetworkSerializable
    {
        public ulong idInHost;
        public string userId;

        public PlayerBasicInfo(ulong idInHost, string userId)
        {
            this.idInHost = idInHost;
            this.userId = userId ?? string.Empty;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref idInHost);

            // Unity Netcode no soporta null strings, convertir siempre
            if (serializer.IsWriter && userId == null)
                userId = string.Empty;

            serializer.SerializeValue(ref userId);
        }
    }

    public struct ClientsBasicInfoCollection : INetworkSerializable
    {
        public PlayerBasicInfo Client0_BasicInfo;
        public PlayerBasicInfo Client1_BasicInfo;
        public PlayerBasicInfo Client2_BasicInfo;
        public PlayerBasicInfo Client3_BasicInfo;

        public ClientsBasicInfoCollection
            (PlayerBasicInfo client0_BasicInfo,
            PlayerBasicInfo client1_BasicInfo,
            PlayerBasicInfo client2_BasicInfo,
            PlayerBasicInfo client3_BasicInfo)
        {
            Client0_BasicInfo = client0_BasicInfo;
            Client1_BasicInfo = client1_BasicInfo;
            Client2_BasicInfo = client2_BasicInfo;
            Client3_BasicInfo = client3_BasicInfo;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Client0_BasicInfo);
            serializer.SerializeValue(ref Client1_BasicInfo);
            serializer.SerializeValue(ref Client2_BasicInfo);
            serializer.SerializeValue(ref Client3_BasicInfo);
        }

        public PlayerBasicInfo[] playerBasicInfos
        {
            get
            {
                return new PlayerBasicInfo[]
                {
                    Client0_BasicInfo,
                    Client1_BasicInfo,
                    Client2_BasicInfo,
                    Client3_BasicInfo
                };
            }
        }
    }
}
#endif