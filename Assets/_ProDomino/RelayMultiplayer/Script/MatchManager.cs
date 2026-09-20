using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameModes;
using ProDomino.GameSystem;
using ProDomino.Leaderboard;
using ProDomino.NavigationSystem;
using ProDomino.RelayMultiplayer;
using ProDomino.ReplaySystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Timba.Patterns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;
using static UniqueBoolEvent;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }

    [SerializeField]
    private MatchState matchStatePrefab;

    [SerializeField]
    private RelayManager relayManager;

    [SerializeField]
    private JoinCodeManager joinCodeManager;

    [SerializeField]
    private LobbyChatManager lobbyChatManager;

    [SerializeField]
    private GameObject networkManagerPrefab;

    [SerializeField]
    private MenuControllerGameMode menuControllerGameMode;

    [SerializeField]
    private NavigationPanelController navigationPanelController;

    [Tooltip("Time in seconds for each player's turn.")]
    [SerializeField] private float turnDuration = 60f; // 60 segundos por turno
    [SerializeField] private float waitTimeForNextRound = 20f;
    [SerializeField] private float waitTimeForRematch = 20f;
    [SerializeField] private bool waitConfirmationForNextRound = false;
    [SerializeField] private bool waitConfirmationForRematch = false;

    [Header("Matchmaking Settings")]
    [Tooltip("Determine if the bot matchmaking will be use")]
    [SerializeField] private bool useBotMatchmaking;

    [Tooltip("Time in seconds to wait before stopping matchmaking if not enough players have joined.")]
    [SerializeField] private float timeToStopMatchmaking = 120f;

    [Tooltip("Number of attempts to create/join/leave a session before giving up.")]
    [SerializeField] private int newtworkProcessAttempt = 5;

    [Header("Events")]
    public UnityEvent OnCliendConnected;
    public UnityEvent OnGameStarted;
    public UnityEvent OnCurrentPlayablePlayerChanged;

    public UnityEvent<string> OnJoinSession;
    public UnityEvent<string> OnLeftSession;
    public UnityEvent<string> OnReconnectSession;

    public UnityEvent OnSessionRemovedEvent;
    public UnityEvent OnSetMatchmakingPreviousSettingsEvent;
    public UnityEvent OnStartMatchmakingEvent;
    public UnityEvent OnCancelMatchmakingEvent;
    public UnityEvent OnMatchmakingNotFoundEvent;

    private MatchState currentMatchState;
    private AbstractGameMode currentGameMode;
    private CancellationTokenSource matchmakerCancellationSource;
    private List<int> playerUpdateComplete = new List<int>();
    private List<int> playersSkippedSinceLastTileIds = new List<int>();
    private List<int> playerHandsToShowCalled = new List<int>();
    private int localPlayerMatchID;
    private int currentBoneyardTileSelected = -1;
    private bool enableTimerInHost = false;
    private bool? localIsMatchmaking;
    private bool? localIsPartyRelay;

    private GameManager gameManager;
    private AuthManager authManager;
    private DictionaryService dictionaryService;
    private LeaderboardManager leaderboardManager;

    // Fallback NetworkVariables - always instantiated to avoid null references
    private readonly NetworkVariable<float> defaultTurnTimeRemaining = new();
    private readonly NetworkVariable<StatusTimerInHost> defaultStatusTimerInHost = new();
    private readonly NetworkVariable<int> defaultClientsCount = new();
    private readonly NetworkVariable<int> defaultCurrentPlayablePlayerClientID = new(-1);
    private readonly NetworkVariable<bool> defaultIsPartyRelay = new();
    private readonly NetworkVariable<bool> defaultIsMatchmaking = new();
    private readonly NetworkVariable<bool> defaultIsTurnPlayed = new();
    private readonly NetworkVariable<bool> defaultWasPlayForced = new();
    private readonly NetworkVariable<ClientsBasicInfoCollection> defaultClientsBasicInfosCollection = new();
    private readonly NetworkVariable<ClientsBasicInfoCollection> defaultPartyBasicInfosCollection = new();

    // NetworkLists also need safe fallbacks
    private readonly NetworkList<PlayerRecord> defaultPlayerRecords = new();
    private readonly NetworkList<ClientEMC> defaultClientsEMCs = new();

    // Returns active NetworkVariable or fallback if MatchState is not ready
    public NetworkVariable<float> TurnTimeRemaining
    {
        get => currentMatchState != null && currentMatchState.TurnTimeRemaining != null
            ? currentMatchState.TurnTimeRemaining
            : defaultTurnTimeRemaining;
    }

    // Status timer state
    public NetworkVariable<StatusTimerInHost> StatusTimerInHostProp
    {
        get => currentMatchState != null && currentMatchState.StatusTimerInHost != null
            ? currentMatchState.StatusTimerInHost
            : defaultStatusTimerInHost;
    }

    // Expected number of clients
    public NetworkVariable<int> ClientsCount
    {
        get => currentMatchState != null && currentMatchState.ClientsCount != null
            ? currentMatchState.ClientsCount
            : defaultClientsCount;
    }

    // Current playable player
    public NetworkVariable<int> CurrentClientID
    {
        get => currentMatchState != null && currentMatchState.CurrentPlayablePlayerClientID != null
            ? currentMatchState.CurrentPlayablePlayerClientID
            : defaultCurrentPlayablePlayerClientID;
    }

    // Party relay flag
    public NetworkVariable<bool> IsPartyRelay
    {
        get => currentMatchState != null && currentMatchState.IsPartyRelay != null
            ? currentMatchState.IsPartyRelay
            : defaultIsPartyRelay;
    }

    // Matchmaking flag
    public NetworkVariable<bool> IsMatchmaking
    {
        get => currentMatchState != null && currentMatchState.IsMatchmaking != null
            ? currentMatchState.IsMatchmaking
            : defaultIsMatchmaking;
    }

    // Forced play flag
    public NetworkVariable<bool> IsTurnPlayed
    {
        get => currentMatchState != null && currentMatchState.IsTurnPlayed != null
            ? currentMatchState.IsTurnPlayed
            : defaultIsTurnPlayed;
    }
    
    // Forced play flag
    public NetworkVariable<bool> WasPlayForced
    {
        get => currentMatchState != null && currentMatchState.WasPlayForced != null
            ? currentMatchState.WasPlayForced
            : defaultWasPlayForced;
    }

    // Clients basic info
    public NetworkVariable<ClientsBasicInfoCollection> ClientsBasicInfosCollection
    {
        get => currentMatchState != null && currentMatchState.ClientsBasicInfoCollection != null
            ? currentMatchState.ClientsBasicInfoCollection
            : defaultClientsBasicInfosCollection;
    }

    // Party clients basic info
    public NetworkVariable<ClientsBasicInfoCollection> PartyBasicInfosCollection
    {
        get => currentMatchState != null && currentMatchState.PartyBasicInfoCollection != null
            ? currentMatchState.PartyBasicInfoCollection
            : defaultPartyBasicInfosCollection;
    }

    // Player records history
    public NetworkList<PlayerRecord> PlayerRecords
    {
        get => currentMatchState != null && currentMatchState.PlayerRecords != null
            ? currentMatchState.PlayerRecords
            : defaultPlayerRecords;
    }

    // EMCs per client
    public NetworkList<ClientEMC> ClientsEMCs
    {
        get => currentMatchState != null && currentMatchState.ClientsEMCs != null
            ? currentMatchState.ClientsEMCs
            : defaultClientsEMCs;
    }

    /// <summary>
    /// Checks if the user is currently in a relay session.
    /// </summary>
    public bool IsInRelay => NetworkManager.Singleton?.IsConnectedClient ?? false;

    /// <summary>
    /// Checks if the user is currently in a match session.
    /// First check for matchmanager's current game mode, then checks menu controller's game mode.
    /// </summary>
    public bool IsInMatch => currentGameMode != null || menuControllerGameMode.IsInMatch;

    /// <summary>
    /// Checks if the user is authenticated via Unity Gaming Services or any provider.
    /// </summary>
    internal bool IsAuthenticated => gameManager is { IsAuthenticated: true };

    public bool IsMatchmakingRef => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient ? IsMatchmaking.Value : localIsMatchmaking ?? false;
    public ClientsBasicInfoCollection? ClientsInfoCollectionRef => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient ? ClientsBasicInfosCollection.Value : default;
    public ClientsBasicInfoCollection? PartyBasicInfoCollectionRef => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient ? PartyBasicInfosCollection.Value : default;

    public StatusTimerInHost Get_StatusTimerInHost() => StatusTimerInHostProp.Value;


    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("More than one GameManager instance!");
        }
        Instance = this;
    }

    private void Start()
    {
        relayManager = GetComponent<RelayManager>();
        joinCodeManager = GetComponent<JoinCodeManager>();

        gameManager = ServiceLocator.Instance.GetService<GameManager>();
        authManager = ServiceLocator.Instance.GetService<AuthManager>();
        dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
        leaderboardManager = ServiceLocator.Instance.GetService<LeaderboardManager>();

        PartyController.Initialize_MatchManager
            (CreateOrJoinPartySession,
            KickRelayMember,
            () => ClientsInfoCollectionRef?.playerBasicInfos
                ?.Where(x => !string.IsNullOrEmpty(x.playerId))
                ?.Select(x => ((int)x.clientId, x.playerId))
                ?.ToArray(),
            () => PartyBasicInfoCollectionRef?.playerBasicInfos
                ?.Where(x => !string.IsNullOrEmpty(x.playerId))
                ?.Select(x => ((int)x.clientId, x.playerId))
                ?.ToArray(),
            _clientId =>
            {
                var clientBasicInfo = PartyBasicInfoCollectionRef?.playerBasicInfos
                    .FirstOrDefault(x => (int)x.clientId == _clientId);

                if (!string.IsNullOrEmpty(clientBasicInfo?.playerId))
                {
                    return new PartyController.PartyEntryData
                        (clientBasicInfo?.playerId,
                        clientBasicInfo?.playerName,
                        clientBasicInfo?.profileIconId,
                        _clientId is 0);
                } else
                    return default;
            },
            () =>
            {
                var clientBasicInfo = PartyBasicInfoCollectionRef?.playerBasicInfos
                    .FirstOrDefault(x => (int)x.clientId == localPlayerMatchID);

                if (!string.IsNullOrEmpty(clientBasicInfo?.playerId))
                {
                    return new PartyController.PartyEntryData
                    (clientBasicInfo?.playerId,
                    clientBasicInfo?.playerName,
                    clientBasicInfo?.profileIconId,
                    localPlayerMatchID is 0);
                } else
                    return default;
            },
            () => joinCodeManager?.JoinCode,
            () => IsInMatch,
            () => IsInRelay);
    }

    private void Update()
    {
        // Validations to run the timer in host
        if (!IsInMatch
            || !IsSpawned
            || !IsServer
            || !enableTimerInHost
            || StatusTimerInHostProp.Value is StatusTimerInHost.none)
            return;

        // Decrement the remaining time each frame
        TurnTimeRemaining.Value -= Time.deltaTime;

        // When the timer reaches zero, handle the timeout event
        if (TurnTimeRemaining.Value <= 0f)
        {
            // Change the flag to avoid multiple calls
            enableTimerInHost = false;

            // Reset the timer value
            TurnTimeRemaining.Value = 0f;

            // According to the current status, handle the timeout
            switch (StatusTimerInHostProp.Value)
            {
                // Case when the host is waiting for player's turn to end
                case ProDomino.Shared.StatusTimerInHost.waitingTurn:
                    StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.none;
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Turn time expired for player with ClientID: {CurrentClientID.Value}. Handling timeout...</color>");

                    // Here you can force turn passing, auto-play a piece, etc.
                    ForcePlay_ServerRpc();
                    break;

                //  Case when the host is waiting for next round confirmation
                case ProDomino.Shared.StatusTimerInHost.waitingNextRound:
                    StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.none;
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Time expired for next round confirmation. Proceeding to shuffle tiles again...</color>");

                    // Here the tiles are shuffled again for the next round
                    ShufflingTilesAgain_ClientRpc();
                    break;

                // Case when the host is waiting for rematch confirmation
                case ProDomino.Shared.StatusTimerInHost.waitingRematch:
                    StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.none;
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Time expired for rematch confirmation. Exiting to main menu...</color>");

                    // Here the match is ended and players are returned to the main menu
                    ExitTheGameAndGoToMainMenu_ClientRpc();
                    break;

                default:
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Invalid status timer in host value: {StatusTimerInHostProp.Value}</color>");
                    break;
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CancelMatchmaking();
    }

    /// <summary>
    /// Creates or joins a matchmaking session based on the provided game mode, game type, and number of players.
    /// </summary>
    public async UniTask CreateOrJoinMatchSession(GameModeData gameModeData)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CreateOrJoinMatchSession: trying to create/join a matchmaking session...</color>");

        // Mark that the network manager has been used at least once
        networkManagerHasBeenUsed = true;

        // Extract game mode, game type, and number of players from the provided game mode data
        var (gameMode, numberPlayers, gameType, concentrateNumberOfTiles) =
            (gameModeData.gameMode,
            gameModeData.NumberPlayers,
            gameModeData.gameType,
            gameModeData.concentrateNumberOfTiles);

        #region Validations
        // If the local player is already matchmaking, ignore this request
        if (localIsMatchmaking.HasValue && localIsMatchmaking.Value)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create a match because the local player is already matchmaking.</color>");

        // Check if there is an active relay session running (matchmaking or match). If there is and is a party one, ignore this exception
        if (IsInRelay && !IsPartyRelay.Value)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create a match because there is an active one running</color>");

        // Check if therer is an active match running 
        if (IsInMatch)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create a match because player still in one.</color>");

        // Check if the game mode is valid
        if (gameMode is GameMode.none)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Invalid game mode.</color>");

        // Check if the number of players is valid
        if (numberPlayers is not NumberPlayers.oneVsOne and not NumberPlayers.oneVsThree and not NumberPlayers.none and not NumberPlayers.twoVsTwo)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Invalid number of players. Must be either 'oneVsOne' or 'oneVsThree'.</color>");

        // Check if the user is authenticated using providers
        if (gameType is GameType.competitive && !IsAuthenticated)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Competitive games require the user to be authenticated with Unity Gaming Services to create a competitive session.</color>");

        // Else, check if at least the player is authenticated anonimously for casual games
        else if (gameType is GameType.casual && !authManager.IsUGSAuthenticated)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Casual games require the user to be authenticated (at least anonimously) with Unity Gaming Services to create a casual session.</color>");
        #endregion

        #region Local Variables
        // Determine the player that the matchmaker session is aiming to
        var maxPlayers = numberPlayers is NumberPlayers.oneVsOne ? 2 : 4;

        // Construct the matchmaking queue name based on game mode, game type, and number of players
        var matchMakingQueueName = $"{gameMode}-{gameType}-{numberPlayers}";
        #endregion

        // In this moment, the relay doesn't exist yet, but some process could be stopped as if. Simulate matchmaking until its server value is ready
        localIsMatchmaking = true;

        // Create a cancellation source in case the user wants to cancel the matchmaking
        matchmakerCancellationSource ??= new();

        // If the client is not in relay match, try to join or create a relay sesion
        if (!IsInRelay)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Attempting to create or join a matchmaking session for queue: {matchMakingQueueName}</color>");

            // Called before the matchmaking starts
            OnSetMatchmakingPreviousSettingsEvent?.Invoke();

            // Try to prepare a client using the match name
            await TryToPrepareRelayClient
                (maxPlayers: maxPlayers,
                matchMakingQueueName: matchMakingQueueName);
        }

        // But, if the relay is a party one, it means the matchMakingQueueName in the server relay code wasn't defined. Define it to make possible matchmaking
        else if (IsPartyRelay.Value)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Attempting to update matchmaking relay code with queue: {matchMakingQueueName}</color>");

            // Called before the matchmaking starts
            OnSetMatchmakingPreviousSettingsEvent?.Invoke();

            // Try to register the relay code without the necessity of creating the session (it should already exist)
            var wasSuccessfullyCreated = await joinCodeManager.TryToRegisterRelayCodeInServer(matchMakingQueueName, joinCodeManager.JoinCode, matchmakerCancellationSource.Token);
            if (wasSuccessfullyCreated)
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Created successfully relay session</color>");
            else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't register relay join code:" +
                    $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                    $"\nJoin Code: {joinCodeManager.JoinCode}" +
                    $"</color>");
        }

        // If the mathmaking was cancelled, stops it inmediatly
        if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
            return;

        // Once the session is created, start it
        if (IsInRelay)
        {
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Connected as: <b>{(IsHost ? "Host" : "Client")}</b></color>");

            if (IsHost)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b>Matchmaking started:" +
                    $"\n\nCurrent Connected: {NetworkManager.ConnectedClients.Count}" +
                    $"\n\nConnected Goal: {maxPlayers}" +
                    $"</color>");

                IsMatchmaking.Value = true;
            }

            // Trigger the event to notify that matchmaking has started
            OnStartMatchmakingEvent?.Invoke();

            // Check if the player session count already reached the max required
            var startPartyPulling = DateTime.UtcNow;
            while (matchmakerCancellationSource is not null and { IsCancellationRequested: false }
                && NetworkManager.ConnectedClients.Count != maxPlayers
                && (DateTime.UtcNow - startPartyPulling).TotalSeconds < timeToStopMatchmaking)
                await UniTask.NextFrame();

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                return;

            // If the player count reached the max required or the matchmaking was cancelled, try to remove the join code from server and stop the matchmaking
            if (IsHost)
            {
                // If the player count didn't reach the max required, stop the matchmaking.
                if (!IsPartyRelay.Value)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b>Matchmaking stopped:" +
                        $"\n\nCurrent Connected: {NetworkManager.ConnectedClients.Count}" +
                        $"\n\nConnected Goal: {maxPlayers}" +
                        $"</color>");

                    var wasJoinCodeRemovedSuccessfully = await joinCodeManager.TryToRemoveRelayCodeAsync(matchMakingQueueName, joinCodeManager.JoinCode, matchmakerCancellationSource?.Token);
                    if (wasJoinCodeRemovedSuccessfully)
                        Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Session removed successully from server:" +
                            $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                            $"\nJoinCode: {joinCodeManager.JoinCode}" +
                            $"</color>");
                    else
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Failed to removed session from server:" +
                            $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                            $"\nJoinCode: {joinCodeManager.JoinCode}" +
                            $"</color>");

                    // If the mathmaking was cancelled, stops it inmediatly
                    if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                        return;
                }

                // Stop the matchmaking flag
                IsMatchmaking.Value = false;
            }

            // Once the session is ready to start, reset the local matchmaking flag
            localIsMatchmaking = false;

            // Wait a moment until everyone is loaded successfully
            await UniTask.WaitForSeconds(3);

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                return;

            // Try to start the match from the host
            TryToStartMatchFromHost_Rpc
                (isReadyToStart: true,
                    requiredClients: maxPlayers,
                    gameMode: gameMode,
                    gameType: gameType,
                    numberPlayers: numberPlayers,
                    concentrateNumberOfTiles: concentrateNumberOfTiles);
        }

        // If the player coudln't create a matchmaking, cancel its timer
        else
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Player couldn't create a matchmaking</color>");
            OnMatchmakingNotFoundEvent?.Invoke();

            CancelMatchmaking();
        }
    }

    /// <summary>
    /// Creates or joins a matchmaking session based on the provided game mode, game type, and number of players.
    /// </summary>
    public async UniTask CreateOrJoinPartySession(string joinCode = null)
    {
        if (IsInMatch)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create a match because player still in one.</color>");

        if (!IsAuthenticated)
            throw new ArgumentException($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create a match because player should be authenticated.</color>");

        // If the client is not in relay match, try to join or create a relay sesion
        if (!IsInRelay)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Attempting to create or join a <b>Party</b> session</color>");

            // Create a cancellation source in case the user wants to cancel the matchmaking
            matchmakerCancellationSource ??= new();

            // First at all, validate if a party exist to use the convetional matchmaking by Matchmaker service
            var shouldJoin = !string.IsNullOrEmpty(joinCode);
            await TryToPrepareRelayClient(
                maxPlayers: 4,
                joinCode: joinCode,
                isPartyRelay: true,
                isTryingToCreate: !shouldJoin,
                isTryingToJoin: shouldJoin);

            // If the mathmaking was cancelled, stops it inmediatly
            if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                return;
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Shouldn't create/join to <b>Party</b> session because there is an active one</color>");
    }

    /// <summary>
    /// Try to Create/Join to a session usig exclusively <paramref name="matchMakingQueueName"/>(optional no join option) and <paramref name="maxPlayers"/>
    /// </summary>
    /// <param name="matchMakingQueueName"></param>
    /// <param name="maxPlayers"></param>
    private async UniTask TryToPrepareRelayClient(
        int maxPlayers,
        string joinCode = null,
        string matchMakingQueueName = null,
        bool isPartyRelay = false,
        bool isTryingToJoin = true, bool isTryingToCreate = true)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TryToPrepareRelayClient: Preparing Relay Client for matchmaking...</color>");

        // Local copy of isPartyRelay to avoid closure issues
        localIsPartyRelay = isPartyRelay;

        // Variables for the matchmaking process
        var wasJoinedToRelay = false;
        var attempts = 0;
        var attemptLimit = 5;
        var forceCreation = false;

        // Wait until the relay is created or joined successfully
        while (!wasJoinedToRelay && attempts < attemptLimit)
        {
            try
            {
                // If the mathmaking was cancelled, stops it inmediatly
                if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                    throw new OperationCanceledException();

                // Try to obtain a join code of an existing relay session
                joinCode ??= await joinCodeManager.TryToObtainRelayCodeAsync(matchMakingQueueName, matchmakerCancellationSource.Token);

                // If the mathmaking was cancelled, stops it inmediatly
                if (matchmakerCancellationSource is null or { IsCancellationRequested: true })
                    throw new OperationCanceledException();

                // If exist a session that could be joined, the creation is not force and the attempts are less than 2, try to join to it
                if (isTryingToJoin && !string.IsNullOrEmpty(joinCode) && !forceCreation && attempts < 2)
                    await TryToJoinRelay(joinCode);

                // Else, try to create your own relay session
                else if (isTryingToCreate)
                    await TryToCreateRelay();

                else
                    Debug.LogError("What that hell are you doing here?");

                // If the iteration could reach this line, it means a properly relay was deployed and the join code was obtained too
                wasJoinedToRelay = true;
                break;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Matchmacking cancelled by user.</color>");
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Error creating matchmaking</color>");
                Debug.LogWarning(e.Message);

                if (attempts < attemptLimit)
                {
                    await UniTask.WaitForSeconds(5, true);
                    attempts++;
                }
            }
        }

        async UniTask TryToJoinRelay(string joinCode)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Get at first attempt the relay session with id <b>{joinCode}</b>. Procesing to join to the session...</color>");

            var wasJoinedSuccessfully = default(bool);
            try
            {
                wasJoinedSuccessfully = await relayManager.TryToJoinRelay(joinCode, matchmakerCancellationSource.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            matchmakerCancellationSource.Token.ThrowIfCancellationRequested();

            // if the player couldn't join to join code session, try to remove it from server and try again
            if (!wasJoinedSuccessfully)
            {
                forceCreation = true;

                if (!string.IsNullOrEmpty(matchMakingQueueName))
                {
                    var wasJoinCodeRemovedSuccessfully = await joinCodeManager.TryToRemoveRelayCodeAsync(matchMakingQueueName, joinCode, matchmakerCancellationSource?.Token);
                    if (wasJoinCodeRemovedSuccessfully)
                        Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Session removed successully from server:" +
                            $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                            $"\nJoinCode: {joinCode}" +
                            $"</color>");
                    else
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Failed to removed session from server:" +
                            $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                            $"\nJoinCode: {joinCode}" +
                            $"</color>");
                }
                matchmakerCancellationSource.Token.ThrowIfCancellationRequested();

                throw new Exception($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't join to relay match:" +
                    $"\n\nJoinCode: {joinCode}" +
                    $"</color>");
            }

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Joined successfully to relay session</color>");
        }

        async UniTask TryToCreateRelay()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Couldn't Join at first attempt to relay session. Procesing to create a new session...</color>");
            var isPartyRelay = localIsPartyRelay.HasValue && localIsPartyRelay.Value;

            // Try to create the relay match if there isn't any session available
            var joinCode = await relayManager.TryToCreateRelay(maxPlayers, matchmakerCancellationSource.Token);

            // Check for cancellation
            matchmakerCancellationSource.Token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(joinCode))
                throw new Exception($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't create relay match:" +
                    $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                    $"</color>");

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Created successfully relay session with join code: {joinCode}</color>");

            // Once the relay was deployed, register the new relay code
            var wasSuccessfullyCreated = isPartyRelay
                ? true // If the relay is a party one, skip this registration of the join code in server
                : await joinCodeManager.TryToRegisterRelayCodeInServer(matchMakingQueueName, joinCode, matchmakerCancellationSource.Token); // Register the join code in server for matchmaking

            // Check for cancellation
            matchmakerCancellationSource.Token.ThrowIfCancellationRequested();

            // If the join code couldn't be registered, throw an exception
            if (!wasSuccessfullyCreated)
                throw new Exception($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't register relay join code:" +
                    $"\n\nMatchmaking Queue Name: {matchMakingQueueName}" +
                    $"\nJoin Code: {joinCode}" +
                    $"</color>");

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Created successfully relay session</color>");

            // Finally, start the relay session.
            var wasSuccessfullyStarted = await relayManager.StartHostRelay(matchmakerCancellationSource.Token);

            // If it couldn't be started, throw an exception
            if (!wasSuccessfullyStarted)
                throw new Exception($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Couldn't start relay match after its creation:" +
                    $"\n\nJoin Code: {joinCode}" +
                    $"</color>");

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Started successfully relay session</color>");

            // If the relay is a party one, force configure the join code locally
            if (isPartyRelay)
                joinCodeManager.ForceConfigureJoinCode(joinCode);

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Created successfully relay session</color>");
        }
    }

    /// <summary>
    /// Leaves the current match session, if any
    /// </summary>
    /// <returns></returns>
    public async UniTask LeaveMatch(bool areSurrender)
    {
        var attempts = 0;
        while (NetworkManager.Singleton.IsConnectedClient && attempts < newtworkProcessAttempt)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Attempting to Leave the current match session... Attempts: {attempts}</color>");

            try
            {
                // Disconnect player from the relay created if the relay is a normal one or if the player to disconnect is not a member of the party
                if (!IsPartyRelay.Value
                    || (!PartyBasicInfoCollectionRef?.playerBasicInfos.Any(x => !string.IsNullOrEmpty(x.playerId) && x.playerId == authManager.UUID) ?? false))
                    DisconnectPlayerRPC
                        (localPlayerMatchID,
                        (int)NetworkManager.Singleton.LocalClientId,
                        authManager.UUID,
                        Consts.Reasons.LeftingOwnWill);

                // If the one leaveing is the host and a party relay is active, remove the match participants that no are contain into the party
                else
                    PartyReturnToLobby_Rpc(localPlayerMatchID, areSurrender);

                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Failed to leave the match session: {e.Message}</color>");
                attempts++;
                if (attempts >= newtworkProcessAttempt)
                {
                    Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Exceeded maximum attempts ({newtworkProcessAttempt}) to disconnect session.</color>");
                    break;
                }

                // Wait for a short period before retrying
                var delay = Mathf.Pow(2, attempts); // 2, 4, 8, 16 segundos...
                await UniTask.WaitForSeconds(delay, cancellationToken: matchmakerCancellationSource.Token);
            }
        }
    }

    /// <summary>
    /// Returns every client to lobby if the match is a party relay one<br></br>
    /// If the one who triggered the return to lobby is not the local player, just disconnect it; else, send the party members to lobby
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void PartyReturnToLobby_Rpc(int originalClientIdWhoTriggers, bool areSurrender)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Returning every client to lobby</color>");

        // Filter the party members to exclude invalid playerIds and the client who triggered the return to lobby
        var filteredPartyMembers = PartyBasicInfoCollectionRef?.playerBasicInfos
            ?.Where(x => !string.IsNullOrEmpty(x.playerId))
            ?.ToList();

        // Only send to lobby the members of the party of everything if the host was sent to the lobby
        if (filteredPartyMembers.Any(x => x.playerId == authManager.UUID))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Sending party members to lobby. They surrender?: {areSurrender}</color>");

            if (areSurrender)
                menuControllerGameMode.ToLobby(false);
            else
                menuControllerGameMode.ToLobbyWithoutGiveUp();
        }

        // Else, disconnect the player if is not the host
        // The host will be disconnected by the local shutdown process
        else if (!IsHost)
            DisconnectPlayerRPC(
                originalClientIdWhoTriggers,
                (int)NetworkManager.LocalClientId,
                authManager.UUID,
                Consts.Reasons.LeftingOwnWill);

        // Finally, if the local player is the host, update the clients info collection to remove the non-party members
        if (IsHost)
        {
            enableTimerInHost = default;

            var currentPartyMembers = PartyBasicInfoCollectionRef?.playerBasicInfos
                ?.Where(x => !string.IsNullOrEmpty(x.playerId))  // Check if the playerId is valid
                ?.ToList();

            // Get a copy of the current infos
            var infos = ClientsBasicInfosCollection.Value.playerBasicInfos
                .OrderByDescending(x => !string.IsNullOrEmpty(x.playerId))
                .ThenBy(x => x.clientId)
                .ToArray();

            // Check if the member is not contain into the party list. If not, set it as default
            for (int i = 0; i < infos.Length; i++)
                if (!currentPartyMembers.Any(x => x.playerId == infos[i].playerId))
                    infos[i] = default;

            // At last, order the list
            infos = infos
                .OrderByDescending(x => !string.IsNullOrEmpty(x.playerId))
                .ThenBy(x => x.clientId)
                .ToArray();

            // Reassign the whole struct so NGO detects the change
            ClientsBasicInfosCollection.Value = new ClientsBasicInfoCollection(
                infos.ElementAtOrDefault(0),
                infos.ElementAtOrDefault(1),
                infos.ElementAtOrDefault(2),
                infos.ElementAtOrDefault(3));
        }
    }

    /// <summary>
    /// Try to kick a player of the relay.<br></br>
    /// This could be done if the player is the host or if the player is the one to kick
    /// </summary>
    public async UniTask KickRelayMember(int originalClientId, string playerId)
    {
        if (!IsHost && originalClientId != localPlayerMatchID)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> KickRelayMember: Only the host, or the same client, could kick a client</color>");
            return;
        }

        // Try to get a previously record of the current joined client
        var recordedPlayerRecord = FindPlayerRecord(originalClientId);
        if (recordedPlayerRecord is null)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> KickRelayMember: Couldn't found player record of clietn with id {originalClientId}</color>");
            return;
        }

        var newClientId = recordedPlayerRecord.Value.newClientId;

        var attempts = 0;
        while (NetworkManager.Singleton.ConnectedClientsList.Any(x => (int)x.ClientId == newClientId)
            && attempts < newtworkProcessAttempt)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> KickRelayMember: Attempting to kick client with id {newClientId} from the current match session... Attempts: {attempts}</color>");

            try
            {
                DisconnectPlayerRPC
                    (originalClientId,
                    newClientId,
                    playerId,
                    Consts.Reasons.LeftingOwnWill);
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> KickRelayMember: Failed to kick client with id {newClientId} from the match session: {e.Message}</color>");
                attempts++;
                if (attempts >= newtworkProcessAttempt)
                {
                    Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> KickRelayMember: Exceeded maximum attempts ({newtworkProcessAttempt}) to kick client with id {newClientId} from match session.</color>");
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
    [Rpc(SendTo.Server)]
    private void DisconnectPlayerRPC(int originalClientIdToDisconnect, int newClientIdToDisconnect, string playerIdToDisconnect,
        string reasonToLeave = "")
    {
        if (!IsServer)
            return;

        // Disconnect from NGO transport
        try
        {
            // If the local player is the host and the one that will leave the relay. Shutdown the relay
            if (localPlayerMatchID == (int)originalClientIdToDisconnect && IsHost)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> DisconnectPlayerRPC: Host with original client id {originalClientIdToDisconnect} (new is: {newClientIdToDisconnect}) is trying to shutdown the server...</color>");

                // Calls the local shutdown for the host before disconnecting it
                LocalShutDown_Rpc((int)originalClientIdToDisconnect, playerIdToDisconnect);
            } else
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> DisconnectPlayerRPC: Trying to disconnect client with id new client id {newClientIdToDisconnect} (original is: {originalClientIdToDisconnect})...</color>");
                NetworkManager.Singleton.DisconnectClient((ulong)newClientIdToDisconnect, reasonToLeave);

                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> DisconnectPlayerRPC: Client with new client id {newClientIdToDisconnect} (original is: {originalClientIdToDisconnect}) successfully left the match session.</color>");
            }

        }
        catch (RpcException e)
        {
            Debug.LogError($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> DisconnectPlayerRPC: Failed to disconnect player with new client id {newClientIdToDisconnect} (original is: {originalClientIdToDisconnect}) .</color>\n\n{e}");
            throw;
        }
    }

    /// <summary>
    /// Calls the shutdown of the local client after being disconnected from server<br></br>
    /// This process is necessary to ensure the server still active before the client is completely removed from lists
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void LocalShutDown_Rpc(int originalClientIdToDisconnect, string playerIdToDisconnect)
    {
        // Trigger leave events for the disconnected client
        if (!NetworkManager.Singleton.ShutdownInProgress)
        {
            TriggerLeaveEvents(originalClientIdToDisconnect, playerIdToDisconnect).Forget();
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Client with id {originalClientIdToDisconnect} ({playerIdToDisconnect}) successfully shutdown the relay session</color>");
        }

        if (IsHost)
            NetworkManager.Singleton.Shutdown(true);
    }

    /// <summary>
    /// If there is a matchmaking process, and the match is not started yet, cancell it
    /// </summary>
    public void CancelMatchmaking()
    {
        // If the relay is hosted by this player and is not a party one, try to remove the join code from server
        if (IsHost && !IsPartyRelay.Value)
            joinCodeManager.TryToRemoveRelayCodeAsync(joinCodeManager.MatchMakingQueueName, joinCodeManager.JoinCode).AsUniTask().Forget();

        // Once the reasonToLeave is set, try to leave the session (if it exists)
        if (matchmakerCancellationSource is not null and { IsCancellationRequested: false })
        {
            // Cancel the player session creation's process
            matchmakerCancellationSource.Cancel(false);
            matchmakerCancellationSource = null;
        }

        // If there is an active matchmaking process, stops it
        if (IsMatchmakingRef)
        {
            // Once the coroutine was stopped, invoked a callback informing
            OnCancelMatchmakingEvent?.Invoke();

            // If the relay is not a party one, leave the match
            if (!IsPartyRelay.Value)
                LeaveMatch(false).Forget();
        }

        localIsMatchmaking = false;
        if (IsHost)
            IsMatchmaking.Value = default;
    }

    /// <summary>
    /// Invokes the OnCliendConnected event on clients and host.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnCliendConnectedRpc()
    {
        OnCliendConnected?.Invoke();
    }

    /// <summary>
    /// Called by a client to place a tile in the match
    /// </summary>
    [Rpc(SendTo.Server)]
    public void PlaceTileRpc(string tile_id, int playerMatch_ID)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Player {playerMatch_ID} placed tile {tile_id}</color>");

        // Reset turn timer data
        enableTimerInHost = false;

        // Reset status timer in host
        StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.none;

        // Only the player whose turn it is can place a tile
        if (playerMatch_ID != CurrentClientID.Value)
        {
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Player {playerMatch_ID} tried to place a tile out of turn. Current turn is for player {CurrentClientID.Value}.</color>");
            return;
        }

        // Notify the current game mode about the placed tile
        DetermineNextTurn(playerMatch_ID);

        #region Next Turn Logic
        /// Determines the next turn player ID without skipping eliminated players<br></br>
        int DetermineNextTurn(int playerMatch_ID)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> DetermineNextTurn: Determining next turn player from player {playerMatch_ID} without skipping eliminated players...</color>");

            // Get the next player ID
            CurrentClientID.Value = playerMatch_ID + 1 > ClientsCount?.Value - 1 ? 0 : playerMatch_ID + 1;
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> DetermineNextTurn: Next playable player is {CurrentClientID.Value}.</color>");

            // Return the current playable player ID
            return CurrentClientID.Value;
        }
        #endregion
    }

    /// <summary>
    /// Retrieves the match ID associated with the local player.
    /// </summary>
    /// <returns>The match ID of the local player.</returns>
    public int GetLocalPlayerMatchID()
    {
        return localPlayerMatchID;
    }

    /// <summary>
    /// Gets the ID of the current playable player.
    /// </summary>
    /// <returns>The ID of the current playable player.</returns>
    public int GetCurrentPlayablePlayer()
    {
        return CurrentClientID.Value;
    }

    /// <summary>
    /// Gets the current number of connected players.
    /// </summary>
    /// <returns>The number of connected players.</returns>
    public int GetNumberOfPlayers()
    {
        return ClientsCount.Value;
    }

    /// <summary>
    /// Initializes and configures the current game mode, sets up player data, AI profiles, turn timers, and optionally
    /// fills missing player slots with bots.
    /// </summary>
    /// <param name="expectedClients">The total number of clients expected to participate in the game.</param>
    /// <param name="gameModeData">Optional data used to generate or configure the game mode.</param>
    /// <param name="usingBotsToFill">Indicates whether to fill missing player slots with bots if not enough clients are connected.</param>
    private void CreateGameMode(int expectedClients, GameModeData gameModeData = null, bool usingBotsToFill = false)
    {
        // Create or generate the current game mode
        currentGameMode = menuControllerGameMode.GenerateGameMode(gameModeData);

        // Check if the DeckController is of type ExtendedDeckController to override skins
        if (currentGameMode.ExtendedGameController?.DeckController is ExtendedDeckController extendedDeckController)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Overriding skins for player {localPlayerMatchID}...</color>");

            // Get the player data info of the current one
            var fromPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(localPlayerMatchID);
            extendedDeckController.OverrideBoneyardSkin(fromPlayerDataInfo.tileSkinID);

            var dominoViews = extendedDeckController.DominoTiles?.Select(x => x.GetDominoView())?.ToArray();
            extendedDeckController.OverrideTilesSkin(fromPlayerDataInfo.tileSkinID, dominoViews);
        }

        // If this instance is the server, perform server-specific setup
        if (IsServer)
        {
            // Setup random hands for the players
            currentGameMode.SetupRandomHands();

            // Clear the list that tracks which players have finished updating
            playerUpdateComplete.Clear();

            // Get the host leaderboard specific entry data to determine the AI level
            var leaderboardEntry = leaderboardManager?.GetCurrentClientLeaderboardEntry(LeaderboardManager.GetLeaderboardID(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID));
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

        // Always share the host's own data with itself first
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Sharing client id: {localPlayerMatchID} data info</color>");
        currentGameMode.GenerateNewPlayerDataInfo(localPlayerMatchID, false, TransferDataInfoOnAllPlayers_ServerRpc);

        // Calculate how many players are missing to reach the expected number
        var leftingPlayers = expectedClients - NetworkManager.Singleton.ConnectedClients.Count;

        // Only the host should fill with bots, and only if there are missing players
        if (leftingPlayers > 0 && IsHost && usingBotsToFill)
        {
            // Step 1: Create the full expected range of client IDs [0 .. expectedClients - 1]
            var expectedIds = Enumerable.Range(0, expectedClients).Select(x => (ulong)x);

            // Step 2: Get the actual connected client IDs from the NetworkManager
            var connectedIds = ClientsInfoCollectionRef?.playerBasicInfos
                ?.Where(x => !string.IsNullOrEmpty(x.playerId))
                ?.Select(x => (ulong)x.clientId)
                ?.Distinct()
                ?.ToList();

            // Step 3: Find the IDs that are missing (expected but not currently connected)
            var missingIds = expectedIds.Except(connectedIds).ToList();

            // Step 4: Assign data for each missing ID (these will represent bots)
            foreach (var fakeId in missingIds)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Sharing bot client id: {fakeId} data info</color>");
                currentGameMode.GenerateNewPlayerDataInfo((int)fakeId, true, TransferDataInfoOnAllPlayers_ServerRpc);
            }
        }
    }

    #region Collect data from all aplayers
    /// <summary>
    /// Transfers the player data info from clients to the server and manages the distribution of this data to all clients once all have reported in.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void TransferDataInfoOnAllPlayers_ServerRpc(PlayerDataInfo playerDataInfo)
    {
        // This shouldn't happen, but just in case, validate the current game mode
        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> TransferDataInfoOnAllPlayers_ServerRpc: CurrentGameMode is null, cannot transfer player data info from clients to server.</color>");
            return;
        }

        if (!playerUpdateComplete.Contains(playerDataInfo.clientId))
        {
            playerUpdateComplete.Add(playerDataInfo.clientId);
            currentGameMode.RegisterRemotePlayersDataInfo_fromHost(playerDataInfo);

            //clientsBasicInfoCollection.Value.playerBasicInfos

            // Check if the goal of players to update reaches the clients quantities
            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> TransferDataInfoOnAllPlayers_ServerRpc: Server transfer data current value reached:" +
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

                // Try to get those clients that are already configured
                var playerClients = clientsDataInfoPlayer
                    ?.Where(x => x.IsConfigured /*&& !x.isBot*/)
                    ?.ToArray();

                // Iterate for each player client to send them the data info
                if (playerClients is not null and { Length: > 0 })
                {
                    foreach (var data in playerClients)
                        RefreshPlayerDataInfoInClients_ClientRpc(data, playerClients.Length);
                } else
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TransferDataInfoOnAllPlayers_ServerRpc: Server couldn't pass data because there isn't anyone to received it</color>");

                // try to get every bot data in the match
                var botplayer = clientsDataInfoPlayer
                    ?.Where(x => x.IsConfigured && x.isBot)
                    ?.ToArray();

                // Check if there are bots that need confirm that receive the data
                if (botplayer is not null and { Length: > 0 })
                    foreach (var data in botplayer)
                        ConfirmDeliveryUserDataInfo_ServerRpc(data.clientId);
            } else
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TransferDataInfoOnAllPlayers_ServerRpc: Server doesn't have the required connected clients yet:" +
                    $"\n\nClients Count: {ClientsCount.Value}" +
                    $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                    $"</color>");
        } else
            Debug.Log($"<color={Consts.Colors.Error}>[{nameof(MatchManager)}] TransferDataInfoOnAllPlayers_ServerRpc: Player update data already contains client ID: {playerDataInfo.clientId}" +
                $"\n\nplayerUpdateComplete: {(playerUpdateComplete is not null and { Count: > 0 } ? string.Join("\n* ", playerUpdateComplete?.Select(x => x.ToString())?.ToArray()) : "None")}" +
                $"</color>");
    }

    /// <summary>
    /// Sends the player data info from the server to all clients to refresh their local data
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void RefreshPlayerDataInfoInClients_ClientRpc(PlayerDataInfo playerDataInfo, int clientToConfigure)
    {
        if (currentGameMode)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RefreshPlayerDataInfoInClients_ClientRpc: Player data info sent: \n\n{JsonConvert.SerializeObject(playerDataInfo, Formatting.Indented)}</color>");

            // Register the Client data data the server gave
            currentGameMode.RegisterRemotePlayersDataInfo_fromHost(playerDataInfo);

            // Confirm that client received the data once each client data were configured locally
            if (currentGameMode.GetPlayersDataInfo_fromHost().Count(x => x.IsConfigured) == clientToConfigure)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> RefreshPlayerDataInfoInClients_ClientRpc: Each client configure locally \n\n{JsonConvert.SerializeObject(currentGameMode.GetPlayersDataInfo_fromHost(), Formatting.Indented)}</color>");
                ConfirmDeliveryUserDataInfo_ServerRpc(localPlayerMatchID);
            }
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> RefreshPlayerDataInfoInClients_ClientRpc: CurrentGameMode is null, cannot send player data info to clients.</color>");
    }

    /// <summary>
    /// Confirms to the server that the client with <paramref name="clientID"/> has received and processed its player data info
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ConfirmDeliveryUserDataInfo_ServerRpc(int clientID)
    {
        // Check if the record of player which info was updated already contains the client ID
        if (!playerUpdateComplete.Contains(clientID))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ConfirmDeliveryUserDataInfo_ServerRpc: Client with id {clientID} confirmed data info received.</color>");
            playerUpdateComplete.Add(clientID);

            // Once the amount of clients updated reach the goal, start the game for every client
            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ConfirmDeliveryUserDataInfo_ServerRpc: Server confirm data current value reached:" +
                    $"\n\nClients Count: {ClientsCount.Value}" +
                    $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                    $"</color>");

                // Clear the process (is not necesary continue with the process)
                playerUpdateComplete.Clear();

                // NOTE: commented because the replay system in casual/competitive is temporarily disabled to avoid desyncs because of the current structure
                //var tmpClientsHands = new List<IntHandPair>();

                //// Get every player's and bot's hand
                //GetBotsHand(tmpClientsHands);
                //GetClientsHand(tmpClientsHands);

                //// Initialize the replay manager for clients with the collected hands
                //// This will initialize the replay data for every human client (except the host because he is already initialized)
                //InitializeClientsReplayManager_ClientRpc(tmpClientsHands?.ToArray());

                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ConfirmDeliveryUserDataInfo_ServerRpc: Starting Match in every client!</color>");
                RestartGame_ClientRpc();
            }
        } else
            Debug.Log($"<color={Consts.Colors.Error}>[{nameof(MatchManager)}] ConfirmDeliveryUserDataInfo_ServerRpc: Player update data already contains client ID: {clientID}" +
                $"\n\nplayerUpdateComplete: {(playerUpdateComplete is not null and { Count: > 0 } ? string.Join("\n* ", playerUpdateComplete?.Select(x => x.ToString())?.ToArray()) : "None")}" +
                $"</color>");
    }

    /// <summary>
    /// Restart the game in each client once every player data info was transfered and confirmed
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void RestartGame_ClientRpc()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGame_ClientRpc: Restarting Match in each client...</color>");
        currentGameMode.PrintDataPlayers();

        // Store if the replay was initialized before restarting the game mode to avoid reset replay data if the game mode is replay or concentrate
        var isReplayInitialized = currentGameMode.ReplayIsInitialize;

        // Restart the game mode
        currentGameMode.RestartGame();

        // Initalize replay data if the game mode is not replay or concentrate
        // Usually the replayManager was initialized before, but at this point it was reseated
        if (isReplayInitialized && currentGameMode.GameModeID is not GameMode.replay and not GameMode.concentrate)
            currentGameMode.ForceReplayInitialization();

        // Set the events to communicate with the host
        currentGameMode.SetOnStartPlayerMovementEvent(SendOnStartPlayerMovementToHost);
        currentGameMode.SetPlayerMovementEvent(SendPlayerMovementToHost);
        currentGameMode.SetPlayerFinishGameEvent(SendPlayerResult);

        // Subscribe to game mode actions
        currentGameMode.OnRestartGameModeRoundHostAction = RestartGameModeRound;
        currentGameMode.OnRematchGameModeHostAction = RematchGameMode;
        currentGameMode.OnPlayerTakesFromBoneyard = PlayerTakesFromBoneyard;

        lobbyChatManager = currentGameMode.LobbyChatContainerObj?.GetComponentInChildren<LobbyChatManager>(true);
        if (lobbyChatManager)
        {
            lobbyChatManager.Initialize(_spriteCollection => dictionaryService.GetSpriteCollection(_spriteCollection)?.FirstOrDefault());

            lobbyChatManager.Configure
                (OnFinishLobbyChatManagerConfigure,
                currentGameMode.DataInfoPlayer_0,
                currentGameMode.DataInfoPlayer_1,
                currentGameMode.DataInfoPlayer_2,
                currentGameMode.DataInfoPlayer_3);

            lobbyChatManager.OnSendMessageFromClientToHost = SendMessageFromClientToHost;
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> RestartGame_ClientRpc: LobbyChatManager not found in the current game mode.</color>");

        #region Callbacks after restarting the game mode
        // Helper method to request the host to restart the game mode round
        void RestartGameModeRound()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGameModeRound: Client {localPlayerMatchID} is trying to restart teh game round...</color>");

            // Notify the host to restart the game mode round
            RestartGameModeRound_ServerRpc(localPlayerMatchID);
        }

        // Helper method to request a rematch from the host
        void RematchGameMode()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode: Client {localPlayerMatchID} is trying to request a rematch...</color>");

            // Notify the host to request a rematch
            RematchGameMode_ServerRpc(localPlayerMatchID);
        }

        // Helper method to request taking a tile from the boneyard
        void PlayerTakesFromBoneyard(int indexOfSelectedTile)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerTakesFromBoneyard: Client {localPlayerMatchID} is trying to take tile index {indexOfSelectedTile} from the boneyard...</color>");

            // Reset the counter of selected tile
            currentBoneyardTileSelected = -1;

            // Get the current player data info
            var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(CurrentClientID.Value);

            // Notify the host to take a tile from the boneyard
            ClientTakesFromHostBoneyard_ServerRpc
                (!currentPlayerDataInfo.isBot ? localPlayerMatchID : currentPlayerDataInfo.clientId,
                indexOfSelectedTile);
        }

        // Action called when the lobby chat manager finish its configuration
        void OnFinishLobbyChatManagerConfigure()
        {
            disconnectedClientIds.Clear();

            // Only the host configure the hands of every client
            ConfigureClientHand_ServerRpc(localPlayerMatchID, false);
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnFinishLobbyChatManagerConfigure: Hosting is trying to configure bots...</color>");

            // Modify the ui according to the game mode
            currentGameMode.ConfigurePlayersUI();
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> OnFinishLobbyChatManagerConfigure: Game UI Configured</color>");
        }
        #endregion
    }
    #endregion

    #region Calculate and assign random hands
    /// <summary>
    /// The host configures the hand of the client with the specific <paramref name="originalClientId"/><br></br>
    /// The host server knows each hand, but the host client never knows any different of itself one
    /// </summary>
    [Rpc(SendTo.Server)]
    public void ConfigureClientHand_ServerRpc(int originalClientId, bool isBot)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ConfigureClientHand_Rpc: Configuring hand to Client with id: <br>{originalClientId}</b></color>");

        // Get the player index in the game mode
        var auxCliendIndex = GetPlayerIndexID(originalClientId);

        // Get the client hand
        var auxHand = currentGameMode.GetHandOfPlayerWithID(auxCliendIndex); //List<int> auxHand = currentGameMode.GetHandOfPlayerWithID(clientId);
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ConfigureClientHand_Rpc: Hand to Client with id: <br>{originalClientId} is: \n\n{string.Join(", ", auxHand)}</b></color>");

        // Get the status of the round. Check if any player is already eliminated before give to them hands
        var auxPlayerEliminatedState = currentGameMode.ExtendedGameController.TurnScript._playerIsEliminated;
        var auxLeftEliminatedState = currentGameMode.ExtendedGameController.TurnScript._leftAIIsEliminated;
        var auxTopEliminatedState = currentGameMode.ExtendedGameController.TurnScript._topAIIsEliminated;
        var auxRightEliminatedState = currentGameMode.ExtendedGameController.TurnScript._rightAIIsEliminated;

        var recordedClient = FindPlayerRecord(originalClientId);
        if (recordedClient is null)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ConfigureClientHand_Rpc: Failed to get recorded client with client id {originalClientId}</b></color>");
            return;
        }

        // The host configures the hand only to the specific client
        RegisterClientHand_ClientRpc
            (auxHand.ToArray(),
            auxPlayerEliminatedState,
            auxLeftEliminatedState,
            auxTopEliminatedState,
            auxRightEliminatedState,
            !isBot ? // If the player is not a bot, send the data only for its client. But otherwise, send the data to every client.
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { (ulong)recordedClient.Value.newClientId }
                    }
                }
                : default);

        // Register the pass action in the replay manager with the current player index
        if (currentGameMode.GameTypeSelectedID is not GameType.singlePlayerIA
            && currentGameMode.GameModeID is not GameMode.concentrate and not GameMode.replay)
        {
            var auxValidTileCounter = 0;
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
            auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

            var isBlocked = auxValidTileCounter == 0;
            var isGameOver = currentGameMode.RoundShouldBeStopped();

            var tmpClientsHands = new List<IntHandPair>();

            // Get every player's and bot's hand
            GetBotsHand(tmpClientsHands);
            GetClientsHand(tmpClientsHands);

            // Create the new turn for play action in the replay manager with the corresponding data. This is necessary because the host already created the turn for play action when processing the player movement, but the clients that don't have the turn action in the replay (because they are not the ones that played) need to have it created to avoid desyncs in the replay
            CreateReplayTurnFromHost_ClientRpc(TurnActionReplay.play, 
                currentClientId: CurrentClientID.Value, currentBoneyardTileSelected: currentBoneyardTileSelected, 
                isBlocked: isBlocked, isGameOver: isGameOver,
                tmpClientsHand: tmpClientsHands?.ToArray(),
                rpcParams: new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        // Ignore the hostwhen creating the new turn
                        TargetClientIds = new List<ulong>() { (ulong)originalClientId }
                    }
                });
        }
    }

    /// <summary>
    /// From each client, gets and register the hand the the host distributed them<br></br>
    /// This uses the attributes of ClientRpc to send the hand only to the specific client
    /// </summary>
    [ClientRpc]
    public void RegisterClientHand_ClientRpc
        (int[] auxHand,
        bool playerEliminatedState,
        bool leftEliminatedState,
        bool topEliminatedState,
        bool rightEliminatedState,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RegisterClientHand_ClientRpc: Registering hand of the Client with id: <br>{localPlayerMatchID}</b></color>");

        // Register the hand in the local game mode
        currentGameMode.SetLocalPlayerHand
            (new List<int>(auxHand.ToList()),
            localPlayerMatchID,
            playerEliminatedState,
            leftEliminatedState,
            topEliminatedState,
            rightEliminatedState,
            OnClientHandIsDistributed);

        #region Callback once the hand is configured
        // Event calls as callback when the client finish to configure his hand
        void OnClientHandIsDistributed(int clientID)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnClientHandIsDistributed: Hand of player with ClientId {clientID} distributed successfully</b></color>");

            // Once the hand is distributed, if the replay turn where hands are delivered is active, create the replay turn with result none to avoid break the replay flow and force it to be that one
            if (currentGameMode.ReplayTurnWhereHandsDelivered && currentGameMode.GameModeID is not GameMode.replay and not GameMode.concentrate)
                currentGameMode.ForceReplayTurnWhereHandsDelivered();

            // Check who starts the first turn (register client participation in the process)
            ValidateIfClientStartsFirstTurn_ServerRpc(clientID);

            // Only the host configure the bots hands in each client (maybe use server instead every client for security)
            if (IsHost)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnClientHandIsDistributed: Hosting is trying to validate if bots starts the first turn...</color>");

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
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnClientHandIsDistributed: Hosting is validating whos starting first turn in <br>{botsClients.Length}</b> bots...</color>");

                    for (int i = 0; i < botsClients.Length; i++)
                    {
                        var botData = botsClients[i];

                        // Check who starts the first turn (register bot participation in the process)
                        ValidateIfClientStartsFirstTurn_ServerRpc(botData.clientId);
                    }
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Once every player hand is distributed successfully, validate who of them starts the first turn
    /// </summary>
    [Rpc(SendTo.Server)]
    public void ValidateIfClientStartsFirstTurn_ServerRpc(int clientId)
    {
        // Check every player hands were already assigned
        if (!playerUpdateComplete.Contains(clientId))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: Player with ClientId {clientId} confirmed hand assigned.</color>");
            playerUpdateComplete.Add(clientId);

            // Check if the progress reaches the player target quantity
            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: All players have their hands assigned:" +
                    $"\n\nClients Count: {ClientsCount.Value}" +
                    $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                    $"</color>");

                // Determine wich player will take the first turn
                var auxClientIdFirstPlayer = currentGameMode.SelectPlayerWhoWillTakeFirstTurn();

                // If a player was selected, start its turn
                if (auxClientIdFirstPlayer is not -1)
                {
                    Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: Player with ClientId {auxClientIdFirstPlayer} selected to start first turn!</color>");

                    // Register the current playable player
                    CurrentClientID.Value = auxClientIdFirstPlayer;

                    // Start the turn timer for the selected player
                    StartTurnTimer_ServerRPC();

                    // Get the client data info of the current one to determine if this client is a bot
                    var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(auxClientIdFirstPlayer);
                    if (!currentPlayerDataInfo.isBot)
                    {
                        Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: First player selected is a human with ClientId: {auxClientIdFirstPlayer}</color>");
                        EnableTurn_ClientRpc(auxClientIdFirstPlayer);
                    } else
                    {
                        Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: First player selected is a bot with ClientId: {auxClientIdFirstPlayer}</color>");
                        EnableBotTurn_ServerRpc(auxClientIdFirstPlayer);
                    }
                }

                // Else, shuffle again the hands
                else
                {
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: Couldn't select any player to start first turn, shuffling hands again...</color>");
                    ShufflingTilesAgain_ClientRpc();

                    // Notify every client that the hands are being shuffled again
                    ShowAlertFeedback_ClientRpc(clientId, "Couldn't select any player to start first turn, shuffling hands again!", 3f);
                }
            }

            // Else, it means not every player has his hand assigned yet
            else
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfClientStartsFirstTurn_ServerRpc: Not every player has his assigned hand yet:" +
                    $"\n\nClients Count: {ClientsCount.Value}" +
                    $"\nPlayer Update Complete: {playerUpdateComplete.Count}" +
                    $"</color>");
        }

        // Else, the player was already registered
        else
            Debug.Log($"<color={Consts.Colors.Error}>[{nameof(MatchManager)}] ValidateIfClientStartsFirstTurn_ServerRpc: Player update hand already contains client ID: {clientId}" +
                $"\n\nplayerUpdateComplete: {(playerUpdateComplete is not null and { Count: > 0 } ? string.Join("\n* ", playerUpdateComplete?.Select(x => x.ToString())?.ToArray()) : "None")}" +
                $"</color>");
    }

    /// <summary>
    /// Shuffles the tiles again and restarts the game round<br></br>
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ShufflingTilesAgain_ClientRpc(bool restartNewRound = true)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ShufflingTilesAgain_ClientRpc: Shuffling hands again...</color>");

        // Reset the player update complete process
        playerUpdateComplete.Clear();

        // Only the host setup random hands again
        if (IsServer)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ShufflingTilesAgain_ClientRpc: Hosting is shuffling hands again...</color>");

            // Reset turn timer status flag
            enableTimerInHost = false;

            // Reset turn timer status enum
            StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.none;

            // Setup random hands again
            currentGameMode.SetupRandomHands();

            // NOTE: commented because the replay system in casual/competitive is temporarily disabled to avoid desyncs because of the current structure
            //// Initialize the replay manager on the server (this is called directly because every client already has initialized its currentGameMode)
            //InitializeClientsReplayManager_ServerRpc();
        }

        // Restart the game mode
        currentGameMode.RestartGame(restartNewRound: restartNewRound);

        // Set the events to communicate with the host
        currentGameMode.SetOnStartPlayerMovementEvent(SendOnStartPlayerMovementToHost);
        currentGameMode.SetPlayerMovementEvent(SendPlayerMovementToHost);

        // Register the hand of the local player again
        ConfigureClientHand_ServerRpc(localPlayerMatchID, false);
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ShufflingTilesAgain_ClientRpc: Hosting is trying to configure bots...</color>");
    }

    /// <summary>
    /// Initializes the replay manager on clients by collecting player and bot hands and sending the data via RPC,
    /// either to all clients or a specific client.
    /// NOTE: this is dangerous, only is used because a review system restructuration is no planned yet
    /// </summary>
    /// <param name="optionalClientIdTarget">The client ID to target for initialization; if -1, all clients are targeted.</param>
    [Rpc(SendTo.Server)]
    private void InitializeClientsReplayManager_ServerRpc(int optionalClientIdTarget = -1)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ServerRpc: Initializing replay manager on server...</color>");
        
        var tmpClientsHands = new List<IntHandPair>();

        // Get every player's and bot's hand
        GetBotsHand(tmpClientsHands);
        GetClientsHand(tmpClientsHands);

        var targets = ClientsInfoCollectionRef.Value.playerBasicInfos
                        ?.Select(x => (ulong)x.clientId)
                        ?.Where(x => x != 0)
                        ?.ToArray() ?? default;

        // If the optional client id target is different than -1, it means that the replay manager should be initialized only in that specific client, so we set the rpc params to send the rpc only to that client. But otherwise, if the optional client id target is -1,
        // it means that the replay manager should be initialized in every client, so we set the rpc params to send the rpc to every client.
        var rpcParams = default(ClientRpcParams);
        if (optionalClientIdTarget != -1)
        {
            rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { (ulong)optionalClientIdTarget }
                }
            };
        }

        // NOTE: commented because the replay system in casual/competitive is temporarily disabled to avoid desyncs because of the current structure
        //// Initialize the replay manager on the clients
        //InitializeClientsReplayManager_ClientRpc(tmpClientsHands.ToArray(), rpcParams);
    }

    /// <summary>
    /// Initializes the replay manager on client instances by processing and assigning player hand data.
    /// NOTE: this is dangerous, only is used because a review system restructuration is no planned yet
    /// </summary>
    /// <param name="tmpClientsHand">An array containing client IDs and their corresponding hand data.</param>
    /// <param name="rpcParams">Optional parameters for configuring the client RPC call.</param>
    [ClientRpc]
    private void InitializeClientsReplayManager_ClientRpc(IntHandPair[] tmpClientsHand, 
        ClientRpcParams rpcParams = default)
    {
        // Ignore the host when initializing the replay manager on clients, because the host already has it initialized and the clients that are bots are managed by the host, so they don't need to initialize the replay manager in themselves
        if (IsHost)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Host doesn't need to initialize replay manager in itself, skipping...</color>");
            return;
        }

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Initializing replay manager in clients...</color>");

        // Only initialize the replay manager in the clients that are not bots, because the host already has the replay manager initialized and the bots are managed by the host
        if (currentGameMode.GameModeID is not GameMode.replay and not GameMode.concentrate)
        {
            // By default, consider the relative position of the player turn being synchronized as 0 (same as local player) and 2 (opponent/Top)
            var relativePosition = CurrentClientID.Value == localPlayerMatchID ? 0 : 2;

            if (currentGameMode.VSPlayerSelectedID is not NumberPlayers.oneVsOne)
                relativePosition = (CurrentClientID.Value - localPlayerMatchID + 4) % 4;

            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Showing every player's hand at the end of the round...</color>");
            var tmpDictionary = new Dictionary<int, List<int>>();

            // Convert the temporary storage to a dictionary
            if (tmpClientsHand is not null)
            {
                foreach (var item in tmpClientsHand)
                {
                    // By default, consider the relative position of the player turn being synchronized as 0 (same as local player) and 2 (opponent/Top)
                    var itemRelativePosition = item.ClientId == localPlayerMatchID ? 0 : 2;

                    if (currentGameMode.VSPlayerSelectedID is not NumberPlayers.oneVsOne)
                        itemRelativePosition = (item.ClientId - localPlayerMatchID + 4) % 4;

                    // Convert FixedList32 to List<int>
                    if (!tmpDictionary.TryAdd(itemRelativePosition, item.Hand.FixedListToList()))
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Couldn't add the hand tiles of client with id {item.ClientId} because the key already exists in the dictionary.</color>");
                }
            } else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Temporary clients hand is null, cannot show hands.</color>");


            currentGameMode.InitializeReplayManager(tmpDictionary);
        }
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> InitializeClientsReplayManager_ClientRpc: Current game mode is not supported for replay manager initialization.</color>");
    }

    /// <summary>
    /// Validates if the current player has available tiles to play<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ValideCurrentPlayerAvalibleTiles_ServerRpc(int playableClientId, bool isFromBoneyard = false)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Validating available tiles for player {playableClientId}...</color>");

        // Get the client data info of the current one to determine if this client is a bot
        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(playableClientId);

        // Start the turn timer for the current playable player.
        // If it comes from the boneyard, the timer was already started and shouldn't be started again
        if (!isFromBoneyard)
        {
            // Start the turn timer for the player/bot (if the client is host)
            StartTurnTimer_ServerRPC();

            // Start the time bar for everyone
            StartTimeBarForEveryone_ClientRpc(playableClientId);
        }

        // If the client is not bot, try to start its turn
        if (!currentPlayerDataInfo.isBot)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Validating available tiles for player {playableClientId}...</color>");

            // Get the client available moves
            int auxValidTileCounter = currentGameMode.PlayerAvalibleTilesInGameMode(playableClientId);

            // If the client has moves that could be done, enable his turn
            if (auxValidTileCounter > 0)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Player {playableClientId} has {auxValidTileCounter} available tiles.</color>");

                // Enable the turn for the current playable player
                var isTimeout = TurnTimeRemaining.Value <= 0;

                // If it's not timeout, enable the turn for the player
                if (!isTimeout)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Enabling turn for player {playableClientId}...</color>");
                    EnableTurn_ClientRpc(playableClientId);
                }

                //  If it is timeout select a random move for the player
                else
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Player {playableClientId} is a not bot and the action proceeded from boneyard and has moves to play, enabling random autoplay...</color>");

                    // Play a random move for the bot
                    ForceClientToPlay_ClientRpc(playableClientId, new()
                    {
                        Send = new()
                        {
                            TargetClientIds = new[] { (ulong)playableClientId }
                        }
                    });
                }
            }

            // If not, open the boneyard if it's possible or enable pass button
            else
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Player {playableClientId} has NO available tiles.</color>");

                // Notify the client that has no valid moves
                CurrentPlayerHasNoValidMoves_ClientRpc(playableClientId, isFromBoneyard: isFromBoneyard);
            }
        }

        // Else, the bot will manage its turn completely
        else
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValideCurrentPlayerAvalibleTiles_Rpc: Player {playableClientId} is a bot, enabling its turn...</color>");
            EnableBotTurn_ServerRpc(playableClientId);
        }
    }

    /// <summary>
    /// Notifies the client that the current player has no valid moves available<br></br>
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void CurrentPlayerHasNoValidMoves_ClientRpc(int currentPlayablePlayerId, bool isFromBoneyard = false)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Notifying player {currentPlayablePlayerId} has no valid moves...</color>");

        // Validate the current game mode
        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: CurrentGameMode is null, cannot process no valid moves for player {currentPlayablePlayerId}.</color>");
            return;
        }

        var isCurrentPlayerLocal = localPlayerMatchID == currentPlayablePlayerId;

        // Check if there is a delegate assigned to handle no valid moves in the game mode
        if (currentGameMode.ExtendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode != null)
        {
            // Determine if the current player has valid moves in the game mode
            var isTimeout = TurnTimeRemaining.Value <= 0;

            // Determine if the player should interact with the boneyard. Only if it's not timeout and the current player is the local one
            var shouldInteractWithBoneyard = !isTimeout && isCurrentPlayerLocal;

            // Check if the boneyard has tiles and handle no valid moves in the game mode
            var auxValidMovesInGameMode = currentGameMode.ExtendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode(shouldInteractWithBoneyard);

            // If there are no valid moves, check the boneyard status
            if (!auxValidMovesInGameMode)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Player {currentPlayablePlayerId} has no valid moves in game mode after checking boneyard...</color>");

                // If it's not timeout, check if the boneyard is empty and make interactable the pass turn button
                if (!isTimeout)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Player {currentPlayablePlayerId} has no valid moves after checking boneyard, passing turn...</color>");
                    currentGameMode.BoneyardIsEmpy
                        (localPlayerMatchID == currentPlayablePlayerId, 
                        PassTurn_Rpc);
                }

                // If the turn is timeout and the current player is not a bot, make an autoplay from boneyard only if the current player is the local one (avoid multiple calls to server)
                else if (localPlayerMatchID == currentPlayablePlayerId)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Player {currentPlayablePlayerId} is a bot and there are NO tiles in the boneyard, passing turn...</color>");
                    PassTurn_Rpc(true);
                }
            }

            // If there are valid moves, check if the action proceeded from the boneyard
            else if (!isFromBoneyard)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Player {currentPlayablePlayerId} has valid moves after checking boneyard, starting turn timer...</color>");

                // If it's the local player, start the turn timer (avoid multiple calls to server)
                StartTurnTimer_ServerRPC();
            }

            // But, if the action proceeded from the boneyard and the time is out, enable autoplay for the player
            else if (isTimeout && isCurrentPlayerLocal)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: Player {currentPlayablePlayerId} is a not bot and there are tiles in the boneyard, taking tile from boneyard in autoplay ...</color>");

                // Make a bot the controller of the current player forcing it to play a tile randomly
                currentGameMode.StartTurn_Bot
                    (currentClientID: currentPlayablePlayerId,
                    localClientID: localPlayerMatchID,
                    useRandomTile: true,
                    useRandomTimeToWait: false,
                    onBoneyardTileTaken: BotTakesFromHostBoneyard_ServerRpc,
                    onBoneyardEmpty: PassTurn_Rpc);
            }
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> CurrentPlayerHasNoValidMoves_ClientRpc: No delegate assigned to handle no valid moves in game mode.</color>");
    }

    /// <summary>
    /// Passes the turn to the next player<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    private void PassTurn_Rpc(bool clientOrBotPressButton = false)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_Rpc: Player {CurrentClientID.Value} is passing the turn...</color>");

        // Set the flag to indicate that the turn has been played (passing the turn is also playing the turn, so the flag is set to true to avoid that the player can play after passing)
        IsTurnPlayed.Value = true;

        // If the pass was made by the client or bot pressing the button, register it
        if (clientOrBotPressButton)
            playersSkippedSinceLastTileIds.Add(CurrentClientID.Value);

        // Show notification or alert to the player
        var currentClientData = currentGameMode.GetPlayersDataInfo_fromHost(CurrentClientID.Value);
        ShowFeedback_ClientRpc(CurrentClientID.Value, $"{currentClientData.username} passed", "Pass!", 3f);

        // Start the delay before passing the turn
        PassTurn_OnFinishDelayTime();

        #region Delay before passing the turn
        // Delay before passing the turn to the next player
        async void PassTurn_OnFinishDelayTime()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_OnFinishDelayTime: Waiting 3 seconds before passing the turn...</color>");

            // Block the turn timer flag
            enableTimerInHost = false;

            // Block the turn timer status enum
            StatusTimerInHostProp.Value = StatusTimerInHost.none;

            // Wait for 3 seconds before passing the turn
            await UniTask.WaitForSeconds(3);

            // Register the pass action in the replay manager with the current player index
            if (currentGameMode.GameTypeSelectedID is not GameType.singlePlayerIA
                && currentGameMode.GameModeID is not GameMode.concentrate and not GameMode.replay)
            {
                var auxValidTileCounter = 0;
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
                auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

                var isBlocked = auxValidTileCounter == 0;
                var isGameOver = currentGameMode.RoundShouldBeStopped();

                var tmpClientsHands = new List<IntHandPair>();

                // Get every player's and bot's hand
                GetBotsHand(tmpClientsHands);
                GetClientsHand(tmpClientsHands);

                CreateReplayTurnFromHost_ClientRpc(TurnActionReplay.pass, 
                    currentClientId: CurrentClientID.Value, currentBoneyardTileSelected: currentBoneyardTileSelected,
                    isBlocked: isBlocked, isGameOver: isGameOver,
                    tmpClientsHand: tmpClientsHands?.ToArray());
            }
            
            // Determine the next player
            var auxNextPlayerId = NextTurn();

            // Get a count of how many players have skipped since last tile. This is used to determine if the game is blocked
            if (!playersSkippedSinceLastTileIds.Contains(auxNextPlayerId) && ClientsCount.Value > 1)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_OnFinishDelayTime: Next player is {auxNextPlayerId}, validating available tiles...</color>");
                ValideCurrentPlayerAvalibleTiles_ServerRpc(auxNextPlayerId);
            }

            // If every player has skipped since last tile, the game is blocked
            else
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_OnFinishDelayTime: Every player has skipped since last tile, the game is blocked.</color>");

                // Clear the skipped players record
                playersSkippedSinceLastTileIds.Clear();
                playerUpdateComplete.Clear();

                // Check if the round is over due to game being blocked
                string gameOverCase = "";
                currentGameMode.CheckRound_GameOver(ref gameOverCase, gameIsBlocked: true);

                // If the client is the only one, mark the game as complete
                if (ClientsCount.Value == 1)
                    currentGameMode.ExtendedGameController.GameIsCompleteAndFinished = true;

                // Get the necessary data to notify the end of the round
                var auxGameIsCompleteAndFinished = currentGameMode.ExtendedGameController.GameIsCompleteAndFinished;

                // Determine who is the winner player
                var auxPlayerWinner = currentGameMode.ExtendedGameController.PlayerWinner;

                // Once the round is over, try to show every player hand
                TryToShowHandOnEndGameServer_ServerRpc();

                // Notify every client about the end of the round and update scores
                NotifyEndOfRoundAndUpdateScores_ClientRpc(
                    currentGameMode.ExtendedGameController.TurnScript.PlayerScore,
                    currentGameMode.ExtendedGameController.TurnScript.LeftAIScore,
                    currentGameMode.ExtendedGameController.TurnScript.TopAIScore,
                    currentGameMode.ExtendedGameController.TurnScript.RightAIScore,
                    gameOverCase,
                    auxGameIsCompleteAndFinished,
                    auxPlayerWinner
                );

                // If the game is complete and finished, prepare for rematch. Else, prepare for next round
                if (auxGameIsCompleteAndFinished)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_OnFinishDelayTime: Game is complete and finished, preparing for rematch...</color>");

                    // Start the wait time for rematch
                    TurnTimeRemaining.Value = waitTimeForRematch;

                    // Enable the turn timer flag
                    enableTimerInHost = true;

                    // Change the status of the turn timer
                    StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.waitingRematch;

                    // Register the new analytic in firebase 
                    gameManager.UpdateAnalyticsValue((true, GlobalAnalyticType.gamesPlayedToday, GameMode.none)); // Sum a new match played to the day
                }

                // Else, prepare for next round
                else
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PassTurn_OnFinishDelayTime: Preparing for next round...</color>");

                    // Start the wait time for next round
                    TurnTimeRemaining.Value = waitTimeForNextRound;

                    // Enable the turn timer flag
                    enableTimerInHost = true;

                    // Change the status of the turn timer
                    StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.waitingNextRound;

                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Force the current player to play by making a bot take control and play a random tile
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ForcePlay_ServerRpc()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ForcePlay_ServerRpc: Forcing player {CurrentClientID.Value} to play a tile...</color>");

        // Get the client data info of the current player
        var currentClientData = currentGameMode.GetPlayersDataInfo_fromHost(CurrentClientID.Value);

        // If the current player is a bot, cannot force play because bots already play automatically
        // If this not considered, bots could play twice in a row causing desyncs and playing white tiles
        if (currentClientData.isBot)
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ForcePlay_ServerRpc: Current playable player {CurrentClientID.Value} is a bot, cannot force play.</color>");
            return;
        }

        // When the time is out, if the player has already played, cannot force play because the player already played his turn and the turn timer just reached 0 when the player was playing, so the player shouldn't be punished for that
        if (IsTurnPlayed.Value)
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ForcePlay_ServerRpc: Current playable player {CurrentClientID.Value} has already played, cannot force play.</color>");
            return;
        }

        // Inform the player that his turn
        WasPlayForced.Value = true;

        ShowFeedback_ClientRpc(CurrentClientID.Value, $"{currentClientData.username} TimeOut", "TimeOut!", 3f, false);

        // Start the turn timer for the current playable player
        currentGameMode.StartTimeBarEventExternally(CurrentClientID.Value, localPlayerMatchID);

        // Make a bot the controller of the current player forcing it to play a tile randomly
        ForceClientToPlay_ClientRpc(CurrentClientID.Value, new() {
            Send = new() {
                TargetClientIds = new[] { (ulong)CurrentClientID.Value }
            }
        });
    }

    /// <summary>
    /// Force the specific client to play by making a bot take control and play a random tile
    /// </summary>
    [ClientRpc]
    private void ForceClientToPlay_ClientRpc(int forcedClientId, ClientRpcParams clientRpcParams)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ForceClientToPlay: Forcing local player {localPlayerMatchID} to play a tile...</color>");

        // Make a bot the controller of the current player forcing it to play a tile randomly
        currentGameMode.StartTurn_Bot
            (currentClientID: forcedClientId,
            localClientID: localPlayerMatchID,
            useRandomTile: true,
            useRandomTimeToWait: false,
            onBoneyardTileTaken: BotTakesFromHostBoneyard_ServerRpc,
            onBoneyardEmpty: PassTurn_Rpc);
    }

    /// <summary>
    /// Starts the turn of the correspondly client
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void EnableTurn_ClientRpc(int clientID)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> EnableTurn_ClientRpc: Enabling turn for player {clientID}...</color>");

        // Check if the current client has valid moves. This also updates the UI accordingly
        currentGameMode.ExtendedGameController.DeckScript.Deck_HandleHasValidMoves?.Invoke();

        // Start the turn for the specific client and enable the time bar
        currentGameMode.StartTurn(clientID, localPlayerMatchID);
        currentGameMode.UpdatePlayerTurnExternally(clientID, localPlayerMatchID);
        currentGameMode.StartTimeBarEventExternally(clientID, localPlayerMatchID);

        // If the client that is starting the turn is the local one, reset the flag that indicates if the player has played in his turn, to allow the player to play and avoid issues with timeouts when the player has already played but the turn timer just reached 0 when the player was playing, so the player shouldn't be punished for that
        if (IsHost)
            IsTurnPlayed.Value = false;
    }

    /// <summary>
    /// Starts the turn of the correspondly bot
    /// </summary>
    [Rpc(SendTo.Server)]
    private void EnableBotTurn_ServerRpc(int clientID)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> EnableBotTurn_ServerRpc: Enabling turn for bot player {clientID}...</color>");

        // Validate the current game mode
        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> EnableBotTurn_ServerRpc: CurrentGameMode is null, cannot enable bot turn for player {clientID}.</color>");
            return;
        }

        // Start the turn for the specific bot client (the handle move process will be done by the bot AI)
        currentGameMode.StartTurn_Bot
            (currentClientID: clientID,
            localClientID: localPlayerMatchID,
            useRandomTile: false,
            useRandomTimeToWait: true,
            onBoneyardTileTaken: BotTakesFromHostBoneyard_ServerRpc,
            onBoneyardEmpty: PassTurn_Rpc);

        // Start the time bar for the specific bot client
        currentGameMode.StartTimeBarEventExternally(clientID, localPlayerMatchID);
    }

    /// <summary>
    /// Starts the time bar for every client
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void StartTimeBarForEveryone_ClientRpc(int targetClientId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> StartTimeBarForEveryone_ClientRpc: Starting time bar for player {targetClientId}...</color>");

        // Validate the current game mode
        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> StartTimeBarForEveryone_ClientRpc: CurrentGameMode is null, cannot start time bar for player {targetClientId}.</color>");
            return;
        }

        // Update the player turn externally
        currentGameMode.UpdatePlayerTurnExternally(targetClientId, localPlayerMatchID);

        // Start the time bar event for the current playable player
        currentGameMode.StartTimeBarEventExternally(targetClientId, localPlayerMatchID);
    }
    #endregion

    #region Turns Timer
    /// <summary>
    /// Starts the turn timer for the current playable player
    /// </summary>
    [Rpc(SendTo.Server)]
    public void StartTurnTimer_ServerRPC()
    {
        if (!IsServer)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> StartTurnTimer: Only the server can start the turn timer for a player.</color>");
            return;
        }

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> StartTurnTimer: Starting turn timer for player {CurrentClientID.Value}.</color>");

        // Reset the turn timer used in the Update loop
        TurnTimeRemaining.Value = turnDuration;

        // Change the status of the turn timer
        StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.waitingTurn;

        // Reset and enable the turn timer flag
        enableTimerInHost = true;
    }
    #endregion

    #region Player movement in turn
    /// <summary>
    /// Notifies the host that the player has started moving a tile.
    /// </summary>
    /// <remarks>Marks the turn as played to prevent further actions and avoid timeout issues if the player
    /// has already acted.</remarks>
    /// <param name="tileID">The ID of the tile being played.</param>
    /// <param name="sideInfo">Additional information about the side or context of the move.</param>
    private void SendOnStartPlayerMovementToHost(int tileID, string sideInfo)
    {
        // When the player starts to play a tile, we mark the turn as played to avoid that the player can play after playing a tile or passing, and to avoid issues with timeouts when the player has already played but the turn timer just reached 0 when the player was playing, so the player shouldn't be punished for that
        StartPlayerMovementHost_ServerRpc();
    }

    /// <summary>
    /// Sends the player movement to the host server<br></br>
    /// </summary>
    private void SendPlayerMovementToHost(int tileID, string sideInfo)
    {
        // Instead of use localPlayerMatchID, use the CurrentPlayablePlayerClientID to avoid issues with bots
        // This is because the local player could be the host that played as a bot for the current client (ex: play a random tile when time out)
        var currentPlayablePlayerClientID = CurrentClientID.Value;

        // Get the player data info of the current one
        var currentPlayerDataInfo = currentGameMode.GetPlayersDatasInfo_fromHost()
            ?.FirstOrDefault(x => x.IsConfigured && x.clientId == currentPlayablePlayerClientID)
            ?? default;

        // If the player is not a bot, remove the tile from his hand and notify the host
        if (!currentPlayerDataInfo.isBot)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SendPlayerMovementToHost: Player {CurrentClientID.Value} is not a bot, removing tile {tileID} from hand.</color>");
            currentGameMode.HandOfLocalPlayer.Remove(tileID);

            // Get the K factor for the leaderboard calculation
            var leaderboardEntry = leaderboardManager.GetCurrentClientLeaderboardEntry(LeaderboardManager.GetLeaderboardID(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID));
            var factorK = 32;

            // Get the game manager to access the config data
            if (leaderboardEntry is not null
                && gameManager is not null
                and { GameBackendConfigData: not null
                and { leaderboardConfig: not null
                and { kFactorByTier: not null
                and { Length: > 0 } } } })
            {
                // Parse the tier from the leaderboard entry and get the corresponding K factor
                var tierToCheck = Enum.TryParse(leaderboardEntry.Tier, out LeaderboardTier tier) ? tier : LeaderboardTier.None;
                factorK = gameManager.GameBackendConfigData.leaderboardConfig.kFactorByTier
                    .FirstOrDefault(x => x.leaderboardTier == tierToCheck)
                    ?.kFactor ?? factorK;

                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SendPlayerMovementToHost: Found leaderboard entry and game manager config data, using K factor {factorK} for tier {tierToCheck}.</color>");
            } else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> SendPlayerMovementToHost: Could not find leaderboard entry or game manager config data, using default K factor {factorK}.</color>");

            // Notify the host of the movement
            PlayerMovementHost_ServerRpc(tileID, sideInfo, currentPlayablePlayerClientID, factorK);
        } else
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SendPlayerMovementToHost: Player {currentPlayablePlayerClientID} is a bot, notifying host of movement.</color>");

            // If the player is a bot, just notify the host of the movement
            PlayerMovementHost_ServerRpc(tileID, sideInfo, currentPlayerDataInfo.clientId);
        }
    }

    /// <summary>
    /// Marks the player's turn as played on the server to prevent further actions and handle turn timeouts
    /// appropriately.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void StartPlayerMovementHost_ServerRpc()
    { 
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> StartPlayerMovementHost_ServerRpc: Starting player movement process...</color>");

        // Set the flag to indicate that the turn has been played, to avoid that the player can play after playing a tile or passing, and to avoid issues with timeouts when the player has already played but the turn timer just reached 0 when the player was playing, so the player shouldn't be punished for that
        IsTurnPlayed.Value = true;
    }

    /// <summary>
    /// The host server receives the player movement from a client<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    private void PlayerMovementHost_ServerRpc(int tileID, string sideInfo, int clientId, int factorK = 0)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Received movement of tile {tileID} from player {clientId}.</color>");

        // Get the player data info of the current one
        var currentPlayerDataInfo = currentGameMode.GetPlayersDatasInfo_fromHost()
            ?.FirstOrDefault(x => x.IsConfigured && x.clientId == clientId)
            ?? default;

        // Clear the skipped players record since a tile has been played
        playersSkippedSinceLastTileIds.Clear();

        // If the turn was forced, reset the flag and set the clientId to the current playable player
        // This situation happens when the player runs out of time and the system forces a play
        var wasPlayForced = WasPlayForced.Value;

        // Make a copy of the original clientId to send it to clients later (this is because the force play is managed by the host, and the client should know the changes too)
        var clientIdOriginal = clientId;

        // Check if the play was forced (time out)
        if (wasPlayForced)
        {
            WasPlayForced.Value = false;

            // Override the clientId to be the expected one
            clientId = CurrentClientID.Value;
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Play was forced, overriding clientId to current playable player {clientId}.</color>");
        }

        // Ignore movements from players who are not currently active and play is not forced
        if (clientId != CurrentClientID.Value && !wasPlayForced)
        {
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Ignoring movement from player {clientId} who is not the current playable player {CurrentClientID.Value}.</color>");
            return;
        }

        // Create a dictionary to hold the hands of each client
        var clientHands = new Dictionary<int, List<int>>();

        // Get the hands of each client
        if (ClientsCount.Value is >= 2)
        {
            clientHands.Add(0, currentGameMode.GetHandOfPlayerWithID(0));
            clientHands.Add(1, currentGameMode.GetHandOfPlayerWithID(1));
        }

        // If there are clients, get the hand of the rest ones
        if (ClientsCount.Value is 4)
        {
            clientHands.Add(2, currentGameMode.GetHandOfPlayerWithID(2));
            clientHands.Add(3, currentGameMode.GetHandOfPlayerWithID(3));
        }

        // Check if the player that moved the tile is the same that the current client
        // Try to define the value of the move that the player did
        if (currentPlayerDataInfo.IsConfigured // Ignore unconfigured players
            && !currentPlayerDataInfo.isBot  // Ignore bots movements for EMC calculation
            && (currentPlayerDataInfo.clientId == localPlayerMatchID || wasPlayForced)) // Only calculate EMC for the local player or if the play was forced (host managing a client)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Evaluating move for player {clientId}.</color>");

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

                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Updated EMC for player {clientId}. New EMC: {clientEMC.MatchEMC}, Average: {clientEMC.AverageEMC}.</color>");
            }

            // Else, create a new entry for the client
            else
            {
                var newEntry = new ClientEMC(clientId);
                newEntry.AddToEMC(moveEvaluationResult.EMC);
                ClientsEMCs.Add(newEntry);

                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> PlayerMovementHost_ServerRpc: Created new EMC entry for player {clientId}. EMC: {newEntry.MatchEMC}, Average: {newEntry.AverageEMC}.</color>");
            }
        }

        // Remove the tile moved from the hand to registered on the board
        currentGameMode.RemoveTileFromUserHand_InHost(clientId, tileID);

        // Reset and disable the turn timer flag
        enableTimerInHost = false;

        // Reset the turn timer status enum
        StatusTimerInHostProp.Value = StatusTimerInHost.none;

        // Clear the player update complete list to start registering the movement updates
        playerUpdateComplete.Clear();

        // Register the pass action in the replay manager with the current player index
        if (currentGameMode.GameTypeSelectedID is not GameType.singlePlayerIA
            && currentGameMode.GameModeID is not GameMode.concentrate and not GameMode.replay)
        {
            var auxValidTileCounter = 0;
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
            auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

            var isBlocked = auxValidTileCounter == 0;
            var isGameOver = currentGameMode.RoundShouldBeStopped();

            var tmpClientsHands = new List<IntHandPair>();

            // TODO: Commenteed for now, because casual/competitive modes are on hold and the replay system is not fully implemented, but when those modes and the replay system will be ready, this should be uncommented to avoid desyncs in the replay because of missing turns for clients that are not the host and played a tile (because the host creates the turn for play action when processing the player movement, but the clients that don't have the turn action in the replay because they are not the ones that played need to have it created to avoid desyncs in the replay)
            //// Get every player's and bot's hand
            //GetBotsHand(tmpClientsHands);
            //GetClientsHand(tmpClientsHands);

            //// For every clients (except the host), create the new turn for play action in the replay manager with the corresponding data. This is necessary because the host already created the turn for play action when processing the player movement, but the clients that don't have the turn action in the replay (because they are not the ones that played) need to have it created to avoid desyncs in the replay
            //CreateReplayTurnFromHost_ClientRpc(TurnActionReplay.play, 
            //    currentClientId: CurrentClientID.Value, currentBoneyardTileSelected: currentBoneyardTileSelected, 
            //    isBlocked: isBlocked, isGameOver: isGameOver,
            //    tmpClientsHand: tmpClientsHands?.ToArray(),
            //    rpcParams: new ClientRpcParams
            //    {
            //        Send = new ClientRpcSendParams
            //        {
            //            // Ignore the hostwhen creating the new turn
            //            TargetClientIds = ClientsInfoCollectionRef.Value.playerBasicInfos
            //                ?.Select(x => (ulong)x.clientId)
            //                ?.Where(x => x != 0)
            //                ?.ToArray() ?? default
            //        }
            //    });
        }

        // Notify every client about the player movement
        SyncronizePlayerMovement_ClientRpc(tileID, sideInfo, clientIdOriginal);
    }

    /// <summary>
    /// Notifies every client about a player movement
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void SyncronizePlayerMovement_ClientRpc(int tileID, string sideInfo, int clientId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SyncronizePlayerMovement_ClientRpc: Notifying clients of movement of tile {tileID} from player {clientId} with side info {sideInfo}.</color>");

        var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(clientId);
        var isBot = currentPlayerDataInfo.isBot;

        // If the player is a bot, ignore the client with clientId 0 (host) because bots are already managed by the host
        int ignoredClientId = isBot ? 0 : clientId;

        // Check if the local player is the one that moved the tile. If not, update the board with the movement. If it is, just mark as delivered
        if (localPlayerMatchID != ignoredClientId)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SyncronizePlayerMovement_ClientRpc: Local player {localPlayerMatchID} is updating board with movement of tile {tileID} from player {clientId}.</color>");

            currentGameMode.UpdateTilePlacedByPlayer
                (tileID,
                sideInfo,
                clientId,
                localPlayerMatchID,
                () => ValidateMovementDeliveredAllClients_Rpc(clientId, localPlayerMatchID));
        } else
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SyncronizePlayerMovement_ClientRpc: Local player {localPlayerMatchID} moved tile {tileID}, marking as delivered.</color>");

            // Directly validate the movement as delivered for the local player
            ValidateMovementDeliveredAllClients_Rpc(clientId, localPlayerMatchID);
        }

        // If the client is the host, validate every bot move
        if (IsHost)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SyncronizePlayerMovement_ClientRpc: Hosting is trying to validate every bot move...</color>");

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
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SyncronizePlayerMovement_ClientRpc: Hosting is validating move delivered to each client <br>{botsClients.Length}</b> bots...</color>");

                for (int i = 0; i < botsClients.Length; i++)
                {
                    var botData = botsClients[i];
                    ValidateMovementDeliveredAllClients_Rpc(clientId, botData.clientId);
                }
            }
        }
    }

    /// <summary>
    /// Validates that the movement has been delivered to all clients<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ValidateMovementDeliveredAllClients_Rpc(int senderClientId, int receiverClientID)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Validating movement delivery from player {senderClientId} to player {receiverClientID}...</color>");

        // If the player has not updated his movement yet, register it
        if (!playerUpdateComplete.Contains(receiverClientID)) //Checks if this player's movement has already been updated.
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Player {receiverClientID} has updated movement.</color>");

            // Register the player that has updated his movement
            playerUpdateComplete.Add(receiverClientID);

            // If all players have updated their movement, proceed to the next turn
            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: All players have updated movement. Proceeding to next turn.</color>");

                // Check if the game is still running or if it is over. Also check if there is only one player left
                var gameOverCase = "";
                var auxIsGameOver = Check_GameOver(ref gameOverCase) || ClientsCount.Value == 1;

                // If the game is not over, proceed to the next turn
                if (!auxIsGameOver)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Game continues, proceeding to next turn.</color>");

                    // Get the client data info of the current one to determine if this client is a bot
                    var currentPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(senderClientId);

                    // Check if the current player is about to finish to show an alert
                    var leftingTilesInHand = currentGameMode.GetHandOfPlayerWithID(senderClientId)?.Count ?? 0;
                    if (leftingTilesInHand is 1)
                        ShowFeedback_ClientRpc(senderClientId, $"Watch out! {currentPlayerDataInfo.username} is about to finish.", "Last Tile!", 3f);

                    // Proceed to the next turn
                    ValideCurrentPlayerAvalibleTiles_ServerRpc(NextTurn());
                } 
                else
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Game over detected, handling end of round.</color>");

                    // Clear the skipped players record and the player update complete list
                    // It is important to delete this list at this point in order to validate the rematch or the next round.
                    playerUpdateComplete.Clear();

                    // If the client is the only one, mark the game as complete
                    if (ClientsCount.Value == 1)
                        currentGameMode.ExtendedGameController.GameIsCompleteAndFinished = true;

                    // Get the necessary data to notify the end of the round
                    var auxGameIsCompleteAndFinished = currentGameMode.ExtendedGameController.GameIsCompleteAndFinished;

                    // Determine who is the winner player
                    var auxPlayerWinner = currentGameMode.ExtendedGameController.PlayerWinner;

                    // Once the round is over, try to show every player hand
                    TryToShowHandOnEndGameServer_ServerRpc();

                    // Notify every client about the end of the round and update scores
                    NotifyEndOfRoundAndUpdateScores_ClientRpc(
                        currentGameMode.ExtendedGameController.TurnScript.PlayerScore,
                        currentGameMode.ExtendedGameController.TurnScript.LeftAIScore,
                        currentGameMode.ExtendedGameController.TurnScript.TopAIScore,
                        currentGameMode.ExtendedGameController.TurnScript.RightAIScore,
                        gameOverCase,
                        auxGameIsCompleteAndFinished,
                        auxPlayerWinner
                    );

                    // If the game is complete and finished, prepare for rematch. Else, prepare for next round
                    if (auxGameIsCompleteAndFinished)
                    {
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Game is complete and finished, preparing for rematch...</color>");

                        // Start the wait time for rematch
                        TurnTimeRemaining.Value = waitTimeForRematch;

                        // Change the status of the turn timer
                        enableTimerInHost = true;

                        // Enable the turn timer flag
                        StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.waitingRematch;
                    } else
                    {
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Preparing for next round...</color>");

                        // Start the wait time for next round
                        TurnTimeRemaining.Value = waitTimeForNextRound;

                        // Enable the turn timer flag
                        enableTimerInHost = true;

                        // Change the status of the turn timer
                        StatusTimerInHostProp.Value = ProDomino.Shared.StatusTimerInHost.waitingNextRound;
                    }
                }
            }

            // Else, wait for the rest of the players to update their movement
            else
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Waiting for other players to update movement. Current count: {playerUpdateComplete.Count}/{ClientsCount.Value}.</color>");
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ValidateMovementDeliveredAllClients_Rpc: Player {receiverClientID} has already updated movement, ignoring.</color>");

        #region Game Over Check
        /// Checks if the game is over
        /// This is a helper method that calls the game mode's CheckRound_GameOver method
        bool Check_GameOver(ref string gameOverCase)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Check_GameOver: Checking if the game is over...</color>");

            var auxValidTileCounter = 0;
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
            auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
            auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Check_GameOver: Total valid tiles in game mode: {auxValidTileCounter}.</color>");

            // Check if the round is over
            var auxResult = currentGameMode.CheckRound_GameOver(ref gameOverCase, gameIsBlocked: auxValidTileCounter == 0);

            // Return the result
            return auxResult;
        }
        #endregion
    }

    /// <summary>
    /// Tries to show every player's hand at the end of the game<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    private void TryToShowHandOnEndGameServer_ServerRpc()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TryToShowHandOnEndGameServer_ServerRpc: Hosting is trying to gather every player's hand to show at the end of the round...</color>");

        // Clear temporary storage
        playerUpdateComplete.Clear();
        var tmpClientsHands = new List<IntHandPair>();

        // Get every player's and bot's hand
        GetBotsHand(tmpClientsHands);
        GetClientsHand(tmpClientsHands);

        // Once every hand is gathered, show them on end round
        ShowHandTilesOnEndRound_ClientRpc(tmpClientsHands.ToArray());
    }

    // Gets the hand of every bot player and stores it temporarily on the host<br></br>
    void GetBotsHand(List<IntHandPair> tmpClientsHands)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> GetBotsHand_ServerRpc: Host is trying to get bot hands...</color>");

        // Get every bot player's hand
        var botPlayerDatas = currentGameMode.GetPlayersDatasInfo_fromHost()
            ?.Where(x => x.IsConfigured && x.isBot)
            ?.ToArray();

        // Iterate through every bot player data and get their hand
        if (botPlayerDatas is not null and { Length: > 0 })
            foreach (var botPlayerData in botPlayerDatas)
            {
                var clientHand = currentGameMode.GetHandOfPlayerWithID(botPlayerData.clientId);
                var newIntHandPair = new IntHandPair(botPlayerData.clientId, clientHand.ToFixedList32());
                if (!tmpClientsHands.Any(x => x.ClientId == newIntHandPair.ClientId))
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> GetBotsHand_ServerRpc: Host registered hand of bot {botPlayerData.clientId} with {newIntHandPair.Hand.Length} tiles.</color>");
                    tmpClientsHands.Add(newIntHandPair);
                } else
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> GetBotsHand_ServerRpc: Couldn't add the hand tiles of bot with id {botPlayerData.clientId} because the key already exists in the dictionary.</color>");
            }
        else
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> GetBotsHand_ServerRpc: Players data is null or empty, cannot send hand tiles to clients.</color>");
    }

    // Gets the hand of the local client and sends it to the host<br></br>
    void GetClientsHand(List<IntHandPair> tmpClientsHands)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> GetClientsHand: Host is trying to get hands...</color>");

        // Get every bot player's hand
        var clientsIds = currentGameMode.GetPlayersDatasInfo_fromHost()
            ?.Where(x => x.IsConfigured && !x.isBot)
            ?.Select(x => x.clientId)
            ?.ToArray();

        if (clientsIds is null or { Length: 0 })
            clientsIds = ClientsBasicInfosCollection.Value.playerBasicInfos
                ?.Select(x => x.clientId)
                ?.ToArray();

        // Iterate through every bot player data and get their hand
        if (clientsIds is not null and { Length: > 0 })
            foreach (var clientId in clientsIds)
            {
                var newIntHandPair = new IntHandPair(clientId, currentGameMode.GetHandOfPlayerWithID(clientId).ToFixedList32());
                if (!tmpClientsHands.Any(x => x.ClientId == newIntHandPair.ClientId))
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> GetClientsHand: Host registered hand of player {clientId} with {newIntHandPair.Hand.Length} tiles.</color>");
                    tmpClientsHands.Add(newIntHandPair);
                } else
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> GetClientsHand: Couldn't add the hand tiles of player with id {clientId} because the key already exists in the dictionary.</color>");
            }
        else
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> GetClientsHand: Players data is null or empty, cannot send hand tiles to clients.</color>");
    }

    /// <summary>
    /// Shows every player's hand at the end of the round
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void ShowHandTilesOnEndRound_ClientRpc(IntHandPair[] tmpClientsHand)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Showing every player's hand at the end of the round...</color>");
        var tmpDictionary = new Dictionary<int, List<int>>();

        // Convert the temporary storage to a dictionary
        if (tmpClientsHand is not null)
        {
            foreach (var item in tmpClientsHand)
                // Convert FixedList32 to List<int>
                if (!tmpDictionary.TryAdd(item.ClientId, item.Hand.FixedListToList()))
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Couldn't add the hand tiles of client with id {item.ClientId} because the key already exists in the dictionary.</color>");
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Temporary clients hand is null, cannot show hands.</color>");

        // Show the hands using the game mode method
        currentGameMode.ShowHandTilesOnEndRound(localPlayerMatchID, tmpDictionary);
    }

    /// <summary>
    /// Synchronizes the match replay state from the host to clients, updating the replay manager with the current turn
    /// action and relevant data.
    /// 
    /// Usually, this replicates the turn action of the rival player (ex: passing or taking from the boneyard) in the replay for clients that are not the host, but it can also be used to synchronize the replay state for the local player when the host is managing his turn (ex: when the local player runs out of time and the host forces a play, so the host needs to synchronize that action in the replay for the local player).
    /// Using the turn that, originally, belonged to the current client
    /// 
    /// The other way to use this is to create a turn for play state. 
    /// </summary>
    /// <param name="turnActionReplay">Specifies the type of turn action to replay, such as passing or taking from the boneyard.</param>
    /// <param name="currentClientId">Indicates the index of the player turn being synchronized.</param>
    /// <param name="currentBoneyardTileSelected">Identifies the selected boneyard tile for the current turn, if applicable.</param>
    /// <param name="tmpClientsHand">DANGEROUS!!!!! Represents the temporary storage of clients' hands for synchronization.</param>
    [ClientRpc]
    public void CreateReplayTurnFromHost_ClientRpc(TurnActionReplay turnActionReplay, int currentClientId, int currentBoneyardTileSelected,
        bool isBlocked, bool isGameOver,
        IntHandPair[] tmpClientsHand, ClientRpcParams rpcParams = default)
    {
        // Check if the ReplayManager instance is available before trying to parse the replay data
        if (!ReplayManager.Instance)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> SynchronizeMatchReplayFromHost_ClientRpc: ReplayManager instance is null, cannot synchronize match replay.</color>");
            return;
        }

        if (ReplayManager.Instance.CurrentReplay is null)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> SynchronizeMatchReplayFromHost_ClientRpc: Current replay is null, cannot synchronize match replay.</color>");
            return;
        }

        // By default, consider the relative position of the player turn being synchronized as 0 (same as local player) and 2 (opponent/Top)
        var relativePosition = currentClientId == localPlayerMatchID ? 0 : 2;

        if (currentGameMode.VSPlayerSelectedID is not NumberPlayers.oneVsOne)
            relativePosition = (currentClientId - localPlayerMatchID + 4) % 4;

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Showing every player's hand at the end of the round...</color>");
        var tmpDictionary = new Dictionary<int, List<int>>();

        // Convert the temporary storage to a dictionary
        if (tmpClientsHand is not null)
        {
            foreach (var item in tmpClientsHand)
            {
                // By default, consider the relative position of the player turn being synchronized as 0 (same as local player) and 2 (opponent/Top)
                var itemRelativePosition = item.ClientId == localPlayerMatchID ? 0 : 2;

                if (currentGameMode.VSPlayerSelectedID is not NumberPlayers.oneVsOne)
                    itemRelativePosition = (item.ClientId - localPlayerMatchID + 4) % 4;

                // Convert FixedList32 to List<int>
                if (!tmpDictionary.TryAdd(itemRelativePosition, item.Hand.FixedListToList()))
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Couldn't add the hand tiles of client with id {item.ClientId} because the key already exists in the dictionary.</color>");
            }
        } else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ShowHandTilesOnEndRound_ClientRpc: Temporary clients hand is null, cannot show hands.</color>");


        // Create a new turn in the replay for the current game mode
        // NOTE: DANGEROUS!!!! passing the temporary storage of clients' hands is dangerous because it is not guaranteed that the data is correct or complete, but it is necessary to synchronize the replay state with the host.
        // A better approach should be implemented in the future to ensure data integrity and avoid potential issues.
        //currentGameMode.CreateReplayTurn(tmpDictionary);

        switch (turnActionReplay)
        {
            case TurnActionReplay.pass: ReplayManager.Instance.SetTurnAction_Pass(auxPlayerIndexId: relativePosition); break;
            case TurnActionReplay.takeBoneyard: ReplayManager.Instance.SetTurnAction_TakeBoneyard(auxPlayerIndexId: relativePosition, tileId: currentBoneyardTileSelected); 
                break;
        }

        var turnResultPlay = TurnResultReplay.none;

        if (isBlocked)
            turnResultPlay = TurnResultReplay.gameblocked;

        else if (isGameOver)
            turnResultPlay = TurnResultReplay.gameOver;

        ReplayManager.Instance.SetTurnResult(turnResultPlay);

        // If the action was set, create a new turn in the replay for our own next turn (consider that this process is a replay of the rival's turn, so the next turn will be our own turn) 
        if (turnActionReplay is not TurnActionReplay.play && !currentGameMode.ExtendedGameController.GameIsCompleteAndFinished)
            currentGameMode.CreateReplayTurn(tmpDictionary);

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SynchronizeMatchReplayFromHost_ClientRpc: Successfully synchronized match replay from host.</color>");
    }

    /// <summary>
    /// Determines the next turn player ID, skipping eliminated players<br></br>
    /// </summary>
    private int NextTurn()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NextTurn: Determining next turn player from current player {CurrentClientID.Value}...</color>");

        // By default, assume the next player is eliminated
        var nextPlayerIsEliminated = true;

        // Get the next player ID
        var auxNextTurnID = CurrentClientID.Value;

        // Loop until a non-eliminated player is found or all players have been checked
        int auxPlayerCounter = 0;
        while (nextPlayerIsEliminated && auxPlayerCounter < 4) 
        {
            if (ClientsCount == null)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NextTurn: ClientsCount is null, cannot determine next turn.</color>");
                break;
            }

            // Get the next player ID, wrapping around if necessary
            auxNextTurnID = auxNextTurnID + 1 > ClientsCount.Value - 1 ? 0 : auxNextTurnID + 1;

            // Check if the next player is eliminated
            nextPlayerIsEliminated = currentGameMode.ValidateIfPlayerIsEliminated_InHost(auxNextTurnID);

            // Increment the player counter
            auxPlayerCounter++;

            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NextTurn: Checking player {auxNextTurnID}, eliminated: {nextPlayerIsEliminated}.</color>");
        }

        // If a non-eliminated player is found, set it as the current playable player
        if (!nextPlayerIsEliminated)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NextTurn: Next playable player is {auxNextTurnID}.</color>");
            CurrentClientID.Value = auxNextTurnID;
        }
        else
        { 
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NextTurn: All players are eliminated, cannot determine next turn.</color>");

            // Set to an invalid player ID (or properly removed from the game)
            //CurrentPlayablePlayerClientID.Value = -1;
        }

        // Return the current playable player ID
        return CurrentClientID.Value;
    }

    /// <summary>
    /// Notifies clients of the end of a round, updates player scores, and displays the round result based on the
    /// provided game state and winner information.
    /// </summary>
    /// <param name="scorePlayer_0">Score of player 0.</param>
    /// <param name="scorePlayer_1">Score of player 1.</param>
    /// <param name="scorePlayer_2">Score of player 2.</param>
    /// <param name="scorePlayer_3">Score of player 3.</param>
    /// <param name="gameOverCase">The case or reason for the end of the round.</param>
    /// <param name="auxGameIsCompleteAndFinished">Indicates whether the game is completely finished.</param>
    /// <param name="auxPlayerWinner">The winner(s) of the round as provided by the server.</param>
    [Rpc(SendTo.ClientsAndHost)]
    public void NotifyEndOfRoundAndUpdateScores_ClientRpc
        (int scorePlayer_0, int scorePlayer_1, int scorePlayer_2, int scorePlayer_3, 
        string gameOverCase, 
        bool auxGameIsCompleteAndFinished, 
        string auxPlayerWinner)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyEndOfRoundAndUpdateScores_ClientRpc: Notifying end of round and updating scores for client {localPlayerMatchID}...</color>");

        // Check if the current game mode is valid
        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NotifyEndOfRoundAndUpdateScores_ClientRpc: Current game mode is null, cannot notify end of round.</color>");
            return;
        }

        /* NOTE!
            The winner index from the server is calculated based on the order of players (player, left, top, right) and the local player index.
            If the local players index is not the standard one (player = 0, left = 1, top = 2, right = 3), the winner index from the server needs to be standardized to match with the order of players.
         */

        // Standardize the winner index from the server to match with the order of players (player, left, top, right)
        var orderAssignationLowerized = new List<string>() { "player" , "top" };
        if (currentGameMode.VSPlayerSelectedID is not NumberPlayers.oneVsOne)
            orderAssignationLowerized = new List<string> { "player", "left", "top", "right" };

        // The winner information from the server could contain more than one player (for example, in a team game mode), so we need to split it and find the index of the local player among the winners
        var playerWinnerArray = auxPlayerWinner.Split(' ');

        // In some cases the match winner will contain not only the person that put the last tile, but the complete team
        var winnerIndex = playerWinnerArray
            ?.Select(x => orderAssignationLowerized.IndexOf(x.ToLower()))
            ?.FirstOrDefault(x => x is not -1)
            ?? -1; // If the winner is not in the array, return missing (-1)

        // This is a safety check, because the winner index should never be -1 at this point, since the server should always send a valid winner information.
        // If it is -1, it means that the winner information from the server is not valid and we should log an error and set the winner index to 0 to avoid breaking the game,
        // but this should not happen in a normal scenario.
        if (winnerIndex is -1)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NotifyEndOfRoundAndUpdateScores_ClientRpc: Winner index is -1, winner information from server: {auxPlayerWinner}.</color>");
            winnerIndex = 0; // Default to player index 0 to avoid breaking the game, but this should not happen
        }

        // Server indicates who is the winner, but its value should be interpreted from a global perspective to a local one.
        // For example, if the server says that the winner is the player with index 2, for a client that is playing as the top player (index 2),
        // the winner is the player, but for a client that is playing as the left player (index 1), the winner is the top player.
        var quantityOfPlayers = currentGameMode.VSPlayerSelectedID is NumberPlayers.oneVsOne ? 2 : 4;
        var relativeWinnerIndex = (winnerIndex - localPlayerMatchID + 4) % 4;

        // Once the relative winner index is calculated, we can determine the winner player index from the local player's perspective
        var serverWinnerPlayerIndex = string.Empty;

        // According to the game mode, the assignation of the player index to the position (player, left, top, right) is different,
        // so we need to check it to standardize the winner player index from the local player's perspective
        if (currentGameMode.VSPlayerSelectedID is NumberPlayers.oneVsOne)
            serverWinnerPlayerIndex = relativeWinnerIndex switch
            {
                0 => "Player",
                1 => "Top",
                _ => "Unknown"
            };
        else
            serverWinnerPlayerIndex = relativeWinnerIndex switch
            {
                0 => "Player",
                1 => "Left",
                2 => "Top",
                3 => "Right",
                _ => "Unknown"
            };

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyEndOfRoundAndUpdateScores_ClientRpc: Local Player ID: {localPlayerMatchID}, Server winner index: {winnerIndex}, Relative winner index: {relativeWinnerIndex}, Winner player index from local perspective: {serverWinnerPlayerIndex}.</color>");
       
        // This is a safety check, because the server winner player index should never be unknown at this point, since the server should always send a valid winner information and the relative winner index should always be between 0 and 3,
        if (serverWinnerPlayerIndex == "Unknown")
        { 
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NotifyEndOfRoundAndUpdateScores_ClientRpc: Server winner player index is unknown, relative winner index: {relativeWinnerIndex}.</color>");
            serverWinnerPlayerIndex = "Player"; // Default to player to avoid breaking the game, but this should not happen
        }

        // Update the scores and show the round result
        currentGameMode.UpdateScoreAndShowRoundResult
            (gameOverCase, 
            scorePlayer_0, 
            scorePlayer_1, 
            scorePlayer_2, 
            scorePlayer_3, 
            localPlayerMatchID, 
            auxGameIsCompleteAndFinished,
            serverWinnerPlayerIndex);
    }

    /// <summary>
    /// Requests the server to restart the game mode round<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    public void RestartGameModeRound_ServerRpc(int clientId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGameModeRound_ServerRpc: Client {clientId} is requesting to restart the round...</color>");

        // Check if the player already voted to restart
        if (!playerUpdateComplete.Contains(clientId)) //Checks if this player's movement has already been updated.
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGameModeRound_ServerRpc: Registering restart vote from client {clientId}.</color>");

            // Register the player's vote to restart
            playerUpdateComplete.Add(clientId);

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

            // Check if all players (excluding bots) have voted to restart
            if (playerUpdateComplete.Count == ClientsCount.Value - botsClients?.Length)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGameModeRound_ServerRpc: All players voted to restart, restarting round...</color>");

                // Shuffle tiles again to start a new round
                ShufflingTilesAgain_ClientRpc();
            }
            else
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RestartGameModeRound_ServerRpc: Waiting for other players to vote to restart. Current count: {playerUpdateComplete.Count}/{ClientsCount.Value - (botsClients?.Length ?? 0)}.</color>");
        }
    }

    /// <summary>
    /// Requests the server to start a rematch of the game mode<br></br>
    /// </summary>
    [Rpc(SendTo.Server)]
    public void RematchGameMode_ServerRpc(int playerID)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: Client {playerID} is requesting a rematch...</color>");

        // The first player to request a rematch resets the scores.
        if (playerUpdateComplete.Count == 0) 
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: Resetting scores for new match... Caller is client {playerID}.</color>");
           
            currentGameMode.ExtendedGameController.TurnScript.PlayerScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.LeftAIScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.TopAIScore = 0;
            currentGameMode.ExtendedGameController.TurnScript.RightAIScore = 0;

            currentGameMode.ExtendedGameController.UpdateScoreUI();
        }

        // Check if the player already voted for rematch
        if (!playerUpdateComplete.Contains(playerID))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: Registering rematch vote from client {playerID}.</color>");

            // Register the player's vote for rematch
            playerUpdateComplete.Add(playerID);

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

            // Check if all players (excluding bots) have voted to rematch
            if (playerUpdateComplete.Count == ClientsCount.Value - botsClients?.Length)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: All players voted for rematch, restarting match...</color>");

                // Reset the game complete flag
                currentGameMode.ExtendedGameController.GameIsCompleteAndFinished = false;

                // If there are more than one client, shuffle tiles again for a new match
                if (ClientsCount.Value > 1)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: Shuffling tiles again for new match...</color>");
                    ShufflingTilesAgain_ClientRpc(false);
                }

                // Else, exit to main menu
                else
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RematchGameMode_ServerRpc: Only one client present, exiting to main menu...</color>");
                    ExitTheGameAndGoToMainMenu_ClientRpc();
                }
            }
        }
    }

    /// <summary>
    /// Exits the game and goes to the main menu<br></br>
    /// </summary>
    [ClientRpc]
    public void ExitTheGameAndGoToMainMenu_ClientRpc()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ExitTheGameAndGoToMainMenu_ClientRpc: Exiting game and going to main menu for client {localPlayerMatchID}...</color>");

        // Use the menu controller to go back to the lobby
        menuControllerGameMode.ToLobbyWithoutGiveUp();
    }
    #endregion

    #region Player Boneyard
    /// <summary>
    /// Replicates the obtaining of the tile moved on the client, now on the server side
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ClientTakesFromHostBoneyard_ServerRpc(int originalClientId, int indexOfSelectedTile)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ClientTakesFromHostBoneyard_ServerRpc: Client {originalClientId} takes tile from boneyard at index {indexOfSelectedTile}.</color>");

        // Check if the playerID matches the current playable player
        if (originalClientId == CurrentClientID.Value)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ClientTakesFromHostBoneyard_ServerRpc: Valid player {originalClientId}, processing boneyard tile selection...</color>");

            // Clear the player update complete list to start registering the boneyard updates
            playerUpdateComplete.Clear();

            // Get the selected tile from the boneyard and add it to the player's hand
            currentBoneyardTileSelected = currentGameMode.GetTileFromBoneyard_InHost(indexOfSelectedTile);

            // Register the boneyard tile selection on the replay manager if the game mode is not single player against the AI, concentrate or replay, since in those game modes there is no need to register the boneyard tile selection because there is no replay or because there is no opponent.
            if (currentGameMode.GameTypeSelectedID is not GameType.singlePlayerIA
                && currentGameMode.GameModeID is not GameMode.concentrate and not GameMode.replay)
            {
                var auxValidTileCounter = 0;
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
                auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

                var isBlocked = auxValidTileCounter == 0;
                var isGameOver = currentGameMode.RoundShouldBeStopped();

                var tmpClientsHands = new List<IntHandPair>();

                // Get every player's and bot's hand
                GetBotsHand(tmpClientsHands);
                GetClientsHand(tmpClientsHands);

                CreateReplayTurnFromHost_ClientRpc(TurnActionReplay.takeBoneyard, 
                    currentClientId: CurrentClientID.Value, currentBoneyardTileSelected: currentBoneyardTileSelected, 
                    isBlocked: isBlocked, isGameOver: isGameOver,
                    tmpClientsHand: tmpClientsHands?.ToArray());
            }

            // Add the tile to the specific player's hand
            currentGameMode.AddingTileToSpecificPlayerHand(originalClientId, currentBoneyardTileSelected);

            var recordedClient = FindPlayerRecord(originalClientId);
            if (recordedClient is null)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ConfigureClientHand_Rpc: Failed to get recorded client with client id {originalClientId}</b></color>");
                return;
            }

            // Deliver the boneyard tile to the specific player
            DeliverBoneyardTileToPlayer_ClientRpc(currentBoneyardTileSelected, indexOfSelectedTile, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { (ulong)recordedClient.Value.newClientId }
                }
            });

            // Notify all clients that a boneyard tile has been selected
            NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(originalClientId, indexOfSelectedTile);
        }
    }
    
    /// <summary>
    /// Callback called when the bot doesn't have playable tiles
    /// </summary>
    [Rpc(SendTo.Server)]
    private void BotTakesFromHostBoneyard_ServerRpc(int originalClientId, int indexOfSelectedTile)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> BotTakesFromHostBoneyard_ServerRpc: Bot {originalClientId} takes tile from boneyard at index {indexOfSelectedTile}.</color>");

        // Check if the clientID matches the current playable player
        if (originalClientId == CurrentClientID.Value)
        {
            // Clear the player update complete list to start registering the boneyard updates
            playerUpdateComplete.Clear();

            // Get the selected tile from the boneyard and add it to the bot's hand
            currentBoneyardTileSelected = currentGameMode.GetTileFromBoneyard_InHost(indexOfSelectedTile);

            // Register the boneyard tile selection on the replay manager if the game mode is not single player against the AI, concentrate or replay, since in those game modes there is no need to register the boneyard tile selection because there is no replay or because there is no opponent.
            if (currentGameMode.GameTypeSelectedID is not GameType.singlePlayerIA
                && currentGameMode.GameModeID is not GameMode.concentrate and not GameMode.replay)
            {
                var auxValidTileCounter = 0;
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(0);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(1);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(2);
                auxValidTileCounter += currentGameMode.PlayerAvalibleTilesInGameMode(3);
                auxValidTileCounter += currentGameMode.GetTotalTilesInBoneyard();

                var isBlocked = auxValidTileCounter == 0;
                var isGameOver = currentGameMode.RoundShouldBeStopped();

                var tmpClientsHands = new List<IntHandPair>();

                // Get every player's and bot's hand
                GetBotsHand(tmpClientsHands);
                GetClientsHand(tmpClientsHands);

                CreateReplayTurnFromHost_ClientRpc(TurnActionReplay.takeBoneyard, 
                    currentClientId: CurrentClientID.Value, currentBoneyardTileSelected: currentBoneyardTileSelected, 
                    isBlocked: isBlocked, isGameOver: isGameOver,
                    tmpClientsHands?.ToArray());
            }

            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> BotTakesFromHostBoneyard_ServerRpc: Bot {originalClientId} selected tile wid id {currentBoneyardTileSelected} from boneyard.</color>");
            // Add the tile to the specific bot's hand
            currentGameMode.AddingTileToSpecificPlayerHand(originalClientId, currentBoneyardTileSelected);

            // Ignore delivery to host if the one that plays is himself
            if (originalClientId is not 0)
            { 
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> BotTakesFromHostBoneyard_ServerRpc: Delivering boneyard tile to bot {originalClientId}.</color>");

                // Deliver the boneyard tile to the specific player
                DeliverBoneyardTileToPlayer_ClientRpc(currentBoneyardTileSelected, indexOfSelectedTile, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { (ulong)originalClientId }
                    }
                });
            }

            // Deliver the boneyard tile to the specific bot
            NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(originalClientId, indexOfSelectedTile);
        }
        else
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> BotTakesFromHostBoneyard_ServerRpc: Invalid bot {originalClientId}, cannot process boneyard tile selection.</color>");
    }

    /// <summary>
    /// Notifies all clients that a boneyard tile has been selected<br></br>
    /// </summary>
    [ClientRpc]
    public void NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc(int clientId, int indexOfSelectedTile)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc: Notifying clients that player {clientId} selected a tile from the boneyard at index {indexOfSelectedTile}.</color>");

        if (!currentGameMode)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc: Current game mode is null, cannot process boneyard tile selection.</color>");
            return;
        }

        // Get the player data info of the current one
        var playerWhoTakeTileDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(clientId);

        // Check if the player data info is valid. Only one client (host) could be a bot; every other client is a human player.
        // isBot is equals to isHost
        var isBot = playerWhoTakeTileDataInfo.isBot;

        // Send boneyard tile to hand if the local clientID is not the same the one who got the tile 
        // Also, ignore bots receiving the tile since they don't have a client to update
        if (clientId != localPlayerMatchID 
            && (!IsHost || !isBot)) // If the current client is not the host or he is but is not a bot (human), receive the boneyard tile
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc: Client {localPlayerMatchID} is receiving boneyard tile selected by player {clientId}.</color>");

            //Simule boneyard tile selection for the player
            currentGameMode.SendBoneyardTileToHand_FromHost
                (clientId, 
                localPlayerMatchID, 
                indexOfSelectedTile, 
                ValidateBoneyardDeliveredAllClients);
        }

        // If the client is the one who selected the boneyard tile and is not a bot, just validate the delivery
        else
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc: Client {localPlayerMatchID} is the one who selected the boneyard tile or is a bot, no need to receive it.</color>");

            // No move, but inform that the host already got the boneyard tile
            ValidateBoneyardDeliveredAllClients();
        }

        // If the client is the host, validate every bot boneyard
        if (IsHost)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> NotifyAllClientsThatABoneyardTileIsSelected_ClientRpc: Hosting is trying to validate every bot boneyard...</color>");
         
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
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Hosting is validating boneyard to each client <br>{botsClients.Length}</b> bots...</color>");

                for (int i = 0; i < botsClients.Length; i++)
                {
                    var botData = botsClients[i];
                    if (botData.clientId != localPlayerMatchID)
                         ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc(botData.clientId);
                }
            }
        }
    }

    /// <summary>
    /// Delivers a boneyard tile to the local player on the client and updates the player's hand.
    /// </summary>
    /// <param name="tileID">The unique identifier of the tile to deliver.</param>
    /// <param name="indexOfSelectedTile">The index of the selected tile in the boneyard.</param>
    /// <param name="rpcParams">Parameters for configuring the client RPC call.</param>
    [ClientRpc]
    public void DeliverBoneyardTileToPlayer_ClientRpc(int tileID, int indexOfSelectedTile, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"Delivering tile {tileID} to player {localPlayerMatchID}.");

        //currentGameMode.SendBoneyardCardToHand_FromHost(localPlayerMatchID, localPlayerMatchID, indexOfSelectedTile);
        currentGameMode.SendBoneyardTileToHand_FromHost
            (localPlayerMatchID, 
            localPlayerMatchID, 
            indexOfSelectedTile, 
            ValidateBoneyardDeliveredAllClients, 
            tileID);
    }

    /// <summary>
    /// Validates that the boneyard update has been delivered to all clients<br></br>
    /// </summary>
    private void ValidateBoneyardDeliveredAllClients()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateBoneyardDeliveredAllClients: Client {localPlayerMatchID} has updated boneyard.</color>");
        
        currentGameMode.UpdateBoneyardText();
        ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc(localPlayerMatchID);
    }

    /// <summary>
    /// Validates on the server if the boneyard has been updated for all clients<br></br>
    /// </summary>
    /// <param name="clientId"></param>
    [Rpc(SendTo.Server)]
    private void ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc(int clientId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc: Server is validating boneyard update from client {clientId}.</color>");

        if (!playerUpdateComplete.Contains(clientId)) //Checks if this player's movement has already been updated.
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc: Registering boneyard update from client {clientId}.</color>");
            playerUpdateComplete.Add(clientId);

            if (playerUpdateComplete.Count == ClientsCount.Value)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc: All players have updated boneyard. Proceeding to validate hand moves of the current player.</color>");
                ValideCurrentPlayerAvalibleTiles_ServerRpc(CurrentClientID.Value, isFromBoneyard: true);
            }
            else
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc: Waiting for the boneyard to be updated to other players.</color>");
        }
        else
            Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ValidateIfBoneyardIsUpdatedForAllClients_ServerRpc: The player with ID already has updated: <b>{clientId}</b>.</color>");
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

    /// <summary>
    /// Displays a chat message from a specified client to all clients and shows an alert if the recipient is not the
    /// sender.
    /// </summary>
    /// <param name="fromClientID">The match ID of the client who sent the message.</param>
    /// <param name="message">The chat message to display.</param>
    /// <param name="dateTime">The timestamp when the message was sent.</param>
    [ClientRpc]
    private void UpdateMessageInChatFromAll_ClientRpc(int fromClientID, string message, DateTime dateTime)
    {
        // Only shows the alert if the player is not the sender
        if (fromClientID != localPlayerMatchID)
            currentGameMode.ShowMessageAlert();

        var fromPlayerDataInfo = currentGameMode.GetPlayersDataInfo_fromHost(fromClientID);
        lobbyChatManager.ShowMessageInChat
            (fromClientID, 
            localPlayerMatchID, 
            fromPlayerDataInfo.userId, 
            fromPlayerDataInfo.username,
            message, 
            dateTime, 
            SendBanToPlayer);
    }

    /// <summary>
    /// Attempts to send a ban report for the specified player, preventing duplicate or self-bans.
    /// </summary>
    /// <param name="playerID">The unique identifier of the player to be banned.</param>
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

    /// <summary>
    /// Increments the ban alert counter for the specified player on the server.
    /// </summary>
    /// <param name="playerID">The ID of the player whose ban alert counter should be incremented.</param>
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

    /// <summary>
    /// Shows an alert and prompt message to all clients except the one that triggered the action (ignoreSelf = true by default)
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ShowFeedback_ClientRpc
        (int clientId, 
        string alertMessage, 
        string promptMessage, 
        float duration = 3, 
        bool ignoreSelf = true)
    {
        // Avoid the client that is playing shown its own actions
        if (ignoreSelf && clientId == localPlayerMatchID)
            return;

        // Activate the prompt and alert text in sequence
        currentGameMode.ExtendedGameController.ActivatePrompt
            (promptMessage, 
            duration, 
            () => currentGameMode.ExtendedGameController.TurnScript.AlertTextIE(alertMessage, duration).Forget());
    }

    /// <summary>
    /// Directly shows an alert message to all clients except the one that triggered the action (ignoreSelf = true by default)
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void ShowAlertFeedback_ClientRpc
        (int clientId, 
        string alertMessage, 
        float duration = 3, 
        bool ignoreSelf = true)
    {
        // Avoid the client that is playing shown its own actions
        if (ignoreSelf && clientId == localPlayerMatchID)
            return;

        // Activate the prompt and alert text in sequence
        currentGameMode.ExtendedGameController.TurnScript.AlertTextIE(alertMessage, duration).Forget();
    }
    #endregion

    #region Auxiliary methods
    /// <summary>
    /// Method called only by the host to start the match<br></br>
    /// To avoid atemporary issues about using the field currentGameMode with out being set, this method calls a proxy that will handle the logic of starting the match both from host and clients.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void TryToStartMatchFromHost_Rpc
        (bool isReadyToStart,
        int requiredClients,
        GameMode gameMode = GameMode.none,
        GameType gameType = GameType.none,
        NumberPlayers numberPlayers = NumberPlayers.none,
        ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none)
    {
        TryToStartMatchProxy
            (false,
            isReadyToStart, 
            requiredClients, 
            gameMode, 
            gameType, 
            numberPlayers, 
            concentrateNumberOfTiles, 
            () => TryToStartMatchRPC(isReadyToStart, requiredClients, gameMode, gameType, numberPlayers, concentrateNumberOfTiles));
    }

    /// <summary>
    /// Methos that is called for each client to start the match once the host has started it<br></br>
    /// This is called only when the host is already set.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void TryToStartMatchRPC
        (bool isReadyToStart, 
        int requiredClients,
        GameMode gameMode = GameMode.none, 
        GameType gameType = GameType.none, 
        NumberPlayers numberPlayers = NumberPlayers.none,
        ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none)
    {       
        TryToStartMatchProxy(false, isReadyToStart, requiredClients, gameMode, gameType, numberPlayers, concentrateNumberOfTiles);
    }

    /// <summary>
    /// This method is the core used to start the match from both server and clients<br></br>
    /// This method is called from the current client.
    /// </summary>
    private void TryToStartMatchProxy
        (bool isHostAlreadySet,
        bool isReadyToStart,
        int requiredClients,
        GameMode gameMode = GameMode.none,
        GameType gameType = GameType.none,
        NumberPlayers numberPlayers = NumberPlayers.none,
        ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none,
        Action onFinishSettings = null)
    {
        // Try to avoid overriding the host client settings if it is already set
        if (isHostAlreadySet && localPlayerMatchID is 0)
        { 
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> The host is already set. Ignore host client settings</color>");
            return;
        }

        // Register the number of players that the session will expect
        if (IsServer)
            ClientsCount.Value = requiredClients;

        // Get the number of clients connected to the session
        var connectedClients = NetworkManager.Singleton.ConnectedClients.Count;
        var usingBotsToFill = useBotMatchmaking && requiredClients != connectedClients;

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Try to start match." +
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
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> All players connected. Starting game...</color>");

            // This event calls game config an cancels the matchmaking
            OnCancelMatchmakingEvent?.Invoke();

            if (IsServer)
            {
                // Reset the record of EMCs when a new game starts
                ClientsEMCs.Clear();

                //RemoveJoinCode(joinCodeManager.GameID, joinCodeManager.JoinCode);
                playerUpdateComplete.Clear();
            }

            // If optional data has value, use these instead the client ones (for party purposes mainly)
            var optionalGameModeData =
                    (gameMode is not GameMode.none
                    && gameType is not GameType.none
                    && numberPlayers is not NumberPlayers.none)
                ? new(gameMode, gameType, numberPlayers, concentrateNumberOfTiles)
                : default(GameModeData);

            // Create the game mode instance and set it up
            CreateGameMode(requiredClients, optionalGameModeData, usingBotsToFill);

            // Force deselect all navigation buttons and open the play panel
            navigationPanelController?.CustomButtonToggleGroupUI.DeselectAll();
            navigationPanelController?.ExternalActivateNavigationPanel(NavigationPanelType.Play);

            //Turn off the UI for selecting game mode
            if (currentGameMode)
            {
                currentGameMode.GameModeConfig.SetSelectionUIVisibility(false);
                currentGameMode.GameModeConfig._GameSearchStatus = GameSearchStatus.none;
            } else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> currentGameMode is null, cannot set selection UI visibility.</color>");

            // Once the game mode is created, we can start the game
            OnGameStarted?.Invoke();

            // Register the new analytic in firebase
            gameManager.UpdateAnalyticsValue((true, GlobalAnalyticType.usersPlayingNow, currentGameMode.GameModeConfig.GameModeSelectedID));

            gameManager.RegisterStartMatchDateTime();

            // Once everything is set, invoke the callback
            onFinishSettings?.Invoke();
        }
    }

    /// <summary>
    /// Adds a new player record to the collection or updates an existing one if it already exists.
    /// </summary>
    /// <param name="newPlayerRecord">The player record to add or update.</param>
    [Rpc(SendTo.Server)]
    public void AddPlayerRecord_Rpc(PlayerRecord newPlayerRecord)
    {
        if (!PlayerRecords.Contains(newPlayerRecord))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> AddPlayerRecord_Rpc: Added player with client id {newPlayerRecord.originClientId} ({newPlayerRecord.playerId}) to the records </color>");
            PlayerRecords.Add(newPlayerRecord);
        }
        else
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> AddPlayerRecord_Rpc: Overriding player with new client id {newPlayerRecord.newClientId} with origin one {newPlayerRecord.originClientId} ({newPlayerRecord.playerId}) to the records </color>");
            var index = PlayerRecords.IndexOf(newPlayerRecord);
            PlayerRecords[index] = newPlayerRecord;
        }
    }

    /// <summary>
    /// Sets the PlayerBasicInfo of a client in the server-side collection.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SetPlayerBasicInfo_Rpc(PlayerBasicInfo playerBasicInfo)
    {
        if (!NetworkObject.IsSpawned)
        {
            Debug.LogError("RPC received before NetworkObject was spawned");
            return;
        }

        if (!IsMatchmakingRef && IsPartyRelay.Value)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Overriding <b>Party</b> client basic info collection... </color>");
            OverrideCollectionEntry(playerBasicInfo, PartyBasicInfosCollection, true, SetClientBasicInfoCollection_Party);
        }

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Overriding client basic info collection... </color>");
        OverrideCollectionEntry(playerBasicInfo, ClientsBasicInfosCollection, false, SetClientBasicInfoCollection_NonParty);

        void OverrideCollectionEntry
            (PlayerBasicInfo playerBasicInfo, 
            NetworkVariable<ClientsBasicInfoCollection> collection,
            bool isPartyCollection,
            Action<NetworkVariable<ClientsBasicInfoCollection>> setCollection)
        {
            // Get a copy of the current infos
            if (string.IsNullOrEmpty(playerBasicInfo.playerId))
            { 
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Couldn't register client with id {playerBasicInfo.clientId}. Its player id is null or empty</color>");
                return;
            }

            // Get a copy of the current infos
            var infos = collection.Value.playerBasicInfos
                .OrderByDescending(x => !string.IsNullOrEmpty(x.playerId))
                .ThenBy(x => x.clientId)
                .ToArray();

            // Check if and old entry exist to replace it instead of creatin a new one
            if (infos.Any(x => !string.IsNullOrEmpty(x.playerId) && x.playerId == playerBasicInfo.playerId))
            {
                for (int i = 0; i < infos.Length; i++)
                    if (infos[i].playerId == playerBasicInfo.playerId)
                    {
                        infos[i] = playerBasicInfo;
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Replaced client basic info in slot {i} </color>");
                        break;
                    }
            }
            
            // Find the first empty slot and assign
            else 
                for (int i = 0; i < infos.Length; i++)
                    if (string.IsNullOrEmpty(infos[i].playerId))
                    {
                        infos[i] = playerBasicInfo;
                            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Assigned client basic info to slot {i} </color>");
                            break;
                    }

            // Special case: when is a party, make sure your first party member will be always your ally
            var shouldSwitchPlaces = !isPartyCollection && IsPartyRelay.Value;

            // If the match will be a party one and the collection to fill is the "normal" one (no-party collection)
            if (shouldSwitchPlaces && false) // TEMP: Disabled the switch places process because of the new party system implementation. We should review if we need to switch places with the new implementation or not, but for now we will disable it to avoid issues and we will review it in the future when we have more of the party system implemented. 
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Validating process to switch values between entries left and top . This because a member party should be at top</color>");

                // Get first valid non-host party member (host assumed to have clientId == 0)
                var partyWichWillBeTopMember = PartyBasicInfoCollectionRef?.playerBasicInfos
                    .Where(x => x.clientId is not 0 && !string.IsNullOrEmpty(x.playerId))
                    .Select(x => (PlayerBasicInfo?)x)
                    ?.FirstOrDefault();

                // Check if the party meber is valid
                if (partyWichWillBeTopMember.HasValue)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Starting process to switch values between entries left and top . This because a member party should be at top</color>");

                    // Get the current element at 'top' position (it doesn't care if its value is default or anyone). Info entries count always should be '4'
                    var oldTopEntry = infos[2];

                    // Try to get the party reference value that should be registered in 'normal' collection
                    var (data, index) = infos
                        .Select((x, i) => (data: x, index: i))
                        .Where(x =>
                            !string.IsNullOrEmpty(x.data.playerId)
                            && x.data.clientId == partyWichWillBeTopMember.Value.clientId
                            && x.data.playerId == partyWichWillBeTopMember.Value.playerId)
                        .Select(x => (data: (PlayerBasicInfo?)x.data, x.index))
                        .FirstOrDefault();

                    // Validate the target value could be obtained
                    if (data.HasValue)
                    {
                        if (index is not 2)
                        {
                            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Replacing 'normal' collection entries with indexes: " +
                                $"\n\nSwitching between the entry with index '2' (top) and one with index <b>{index}</b></color>");

                            infos[2] = data.Value;
                            infos[index] = oldTopEntry;
                        } 
                        else 
                            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Party member already at top position");
                    }
                    else
                        Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Couldn't replace 'normal' collection entries because the new entry couldn't be found");
                }
                else
                    Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Couldn't replace 'normal' collection entries because party collection doesn't have any entry that could be used");
            }

            // Reassign the whole struct so NGO detects the change
            collection.Value = new ClientsBasicInfoCollection(
                infos.ElementAtOrDefault(0),
                infos.ElementAtOrDefault(1),
                infos.ElementAtOrDefault(2),
                infos.ElementAtOrDefault(3));

            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetPlayerBasicInfo_Rpc: Current client basic infos: " +
                $"\n - Slot 0: {collection.Value.playerBasicInfos[0].playerId} (Client ID: {collection.Value.playerBasicInfos[0].clientId})" +
                $"\n - Slot 1: {collection.Value.playerBasicInfos[1].playerId} (Client ID: {collection.Value.playerBasicInfos[1].clientId})" +
                $"\n - Slot 2: {collection.Value.playerBasicInfos[2].playerId} (Client ID: {collection.Value.playerBasicInfos[2].clientId})" +
                $"\n - Slot 3: {collection.Value.playerBasicInfos[3].playerId} (Client ID: {collection.Value.playerBasicInfos[3].clientId})" +
                $"</color>");

            // Once changed, invoke the callback to set the collection in the match state
            setCollection?.Invoke(collection);
        }

        /// Sets the client basic info collection in the match state for party matches (in party matches, there are two collections: 
        /// one with all the clients and another one with only the party members, so this method will set the second one)
        void SetClientBasicInfoCollection_Party
            (NetworkVariable<ClientsBasicInfoCollection> collection)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetClientBasicInfoCollection_Party: Setting <b>Party</b> client basic info collection in match state... </color>");
            currentMatchState?.SetClientsBasicInfoCollection(true, collection);
        }

        /// Sets the client basic info collection in the match state for non-party matches or for the "normal" collection in party matches 
        /// (in party matches, there are two collections: one with all the clients and another one with only the party members, 
        /// so this method will set the first one and the SetClientBasicInfoCollection_Party will set the second one)
        void SetClientBasicInfoCollection_NonParty
            (NetworkVariable<ClientsBasicInfoCollection> collection)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> SetClientBasicInfoCollection_NonParty: Setting non-<b>Party</b> client basic info collection in match state... </color>");
            currentMatchState?.SetClientsBasicInfoCollection(false, collection);
        }
    }

    /// <summary>
    /// Removes the basic information of a player with the specified client ID from the relevant client collections on
    /// the server.
    /// </summary>
    /// <param name="clientId">The ID of the client whose basic information should be removed.</param>
    [Rpc(SendTo.Server)]
    public void RemovePlayerBasicInfo_Rpc(int clientId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RemovePlayerBasicInfo_Rpc: Removing client basic info collection... </color>");
        OverrideCollectionEntry(ClientsBasicInfosCollection, RemovingClientBasicInfoCollection_NonParty);

        if (IsPartyRelay.Value)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RemovePlayerBasicInfo_Rpc: Removing <b>Party</b> client basic info collection... </color>");
            OverrideCollectionEntry(PartyBasicInfosCollection, RemovingClientBasicInfoCollection_Party);
        }

        void OverrideCollectionEntry
            (NetworkVariable<ClientsBasicInfoCollection> collection, 
            Action<NetworkVariable<ClientsBasicInfoCollection>> setCollection)
        {
            // Get a copy of the current infos
            var infos = collection.Value.playerBasicInfos
                .OrderByDescending(x => !string.IsNullOrEmpty(x.playerId))
                .ThenBy(x => x.clientId)
                .ToArray();

            // Check if and old entry exist to remove it
            if (infos.Any(x => x.clientId == clientId && !string.IsNullOrEmpty(x.playerId)))
            {
                var isClientToRemoveHost = clientId is 0;
                for (int i = 0; i < infos.Length; i++)
                    if (isClientToRemoveHost || infos[i].clientId == clientId)
                    {
                        infos[i] = default;
                        break;
                    }
            }

            // Reassign the whole struct so NGO detects the change
            collection.Value = new ClientsBasicInfoCollection(
                infos.ElementAtOrDefault(0),
                infos.ElementAtOrDefault(1),
                infos.ElementAtOrDefault(2),
                infos.ElementAtOrDefault(3));
        }

        void RemovingClientBasicInfoCollection_Party
           (NetworkVariable<ClientsBasicInfoCollection> collection)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RemovingClientBasicInfoCollection_Party: Removing <b>Party</b> client basic info collection in match state... </color>");
            currentMatchState?.SetClientsBasicInfoCollection(true, collection);
        }

        void RemovingClientBasicInfoCollection_NonParty
            (NetworkVariable<ClientsBasicInfoCollection> collection)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> RemovingClientBasicInfoCollection_NonParty: Removing non-<b>Party</b> client basic info collection in match state... </color>");
            currentMatchState?.SetClientsBasicInfoCollection(false, collection);
        }
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
    /// Aux to find the index of the client in the ClientsEMCs list.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private PlayerRecord? FindPlayerRecord(int clientId)
    {
        for (int i = 0; i < PlayerRecords.Count; i++)
            if (PlayerRecords[i].originClientId == clientId || PlayerRecords[i].newClientId == clientId)
                return PlayerRecords[i];
        return null;
    }

    /// <summary>
    /// Aux to find the index of the client in the ClientsEMCs list.
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns></returns>
    private PlayerRecord? FindPlayerRecord(string playerId)
    {
        for (int i = 0; i < PlayerRecords.Count; i++)
            if (PlayerRecords[i].playerId == playerId)
                return PlayerRecords[i];
        return null;
    }

    /// <summary>
    /// Sends the player's match result to the leaderboard and updates analytics based on the current game mode.
    /// </summary>
    /// <param name="resultPosition">The final position or rank achieved by the player in the match.</param>
    private async void SendPlayerResult(int resultPosition)
    {
        if (currentGameMode.GameTypeSelectedID is GameType.casual)
        {
            try
            {
                // Use try catch to control the exception and be able to turn on the buttons again
                await leaderboardManager.UpdateCasualAnalytics(resultPosition is 1);

                // Once the match data is updated, refresh the game manager's protected player data
                if (gameManager is not null)
                    await gameManager.RefreshProtectedPlayerData();
                else
                    Debug.LogWarning("Failed to deserialize profile data response.");

            }
            catch (Exception ex)
            {
                Debug.LogError($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager)}]</b> Error while updating casual match result: {ex.Message}</color>");
            }
        }
        else if (currentGameMode.GameTypeSelectedID is GameType.competitive)
        { 
            var clientEMCIndex = FindClientEMCIndex(localPlayerMatchID);

            // Check if the client EMC index was found, if not, log a warning
            if (!clientEMCIndex.HasValue)
                Debug.LogWarning($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager)}]</b> Client EMC for player {localPlayerMatchID} not found. Updating with the minimmun value (0).</color>");

            // Try to get the average EMC for the client, if it doesn't exist, use 0
            var matchEMC = clientEMCIndex.HasValue && ClientsEMCs.Count > clientEMCIndex.Value 
                ? ClientsEMCs[clientEMCIndex.Value].AverageEMC 
                : 0;

            try
            {
                // Use try catch to control the exception and be able to turn on the buttons again
                await leaderboardManager.UpdateLeadeboardResult(currentGameMode.GameModeID, currentGameMode.VSPlayerSelectedID, resultPosition, matchEMC);

                // Once the match data is updated, refresh the game manager's protected player data
                if (gameManager is not null)
                    await gameManager.RefreshProtectedPlayerData();
                else
                    Debug.LogWarning("Failed to deserialize profile data response.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color={Consts.Colors.CloudCodeError}><b>[{nameof(MatchManager)}]</b> Error while updating competitive match result: {ex.Message}</color>");
            }
        }

        // Register the new analytic in firebase (Sum a new mathc played to the day)
        gameManager.UpdateAnalyticsValue((true, GlobalAnalyticType.gamesPlayedToday, GameMode.none));
    }

    /// <summary>
    /// Resets the session state on the server.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void ResetSession_Rpc()
    {
        ResetFields();
    }

    /// <summary>
    /// Resets all match session fields and collections to their default states.
    /// </summary>
    private void ResetFields()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Resetting match session fields...</color>");
       
        TurnTimeRemaining.Value = new();
        StatusTimerInHostProp.Value = new();
        ClientsCount.Value = new();
        CurrentClientID.Value = new();
        IsPartyRelay.Value = new();
        IsMatchmaking.Value = new();
        WasPlayForced.Value = new();
        ClientsBasicInfosCollection.Value = new();
        PartyBasicInfosCollection.Value = new();

        ClientsEMCs.Clear();
        PlayerRecords.Clear();
    }
    
    /// <summary>
    /// Resets the client state by returning to the lobby or destroying the current game mode, clearing player updates,
    /// and cancelling any ongoing matchmaking operations.
    /// </summary>
    private void ResetClient()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Resetting client...</color>");

        if (menuControllerGameMode)
        { 
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Returning to lobby...</color>");
            menuControllerGameMode.ToLobbyWithoutGiveUp();
        }

        else if (currentGameMode)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Destroying current game mode...</color>");

            Destroy(currentGameMode.gameObject);
            currentGameMode = default;
        }

        playerUpdateComplete.Clear();
        if (matchmakerCancellationSource is not null and { IsCancellationRequested: false })
        {
            matchmakerCancellationSource.Cancel();
            matchmakerCancellationSource = default;
        }
    }
    #endregion

    #region Reset the NetworkManager
    private bool networkManagerHasBeenUsed = false;

    /// <summary>
    /// Resets the network manager if it has been used and initiates its recreation.
    /// </summary>
    public void ResetNetworkManager()
    {
        if (networkManagerHasBeenUsed)
        {
            networkManagerHasBeenUsed = false;
            StartCoroutine(CreateNetworkManagerAndContinue());

            Debug.Log("//-- RESET NETWORK");
        }
    }

    /// <summary>
    /// Destroys the existing NetworkManager instance, instantiates a new one from a prefab, waits for initialization,
    /// and then enables the play game mode button.
    /// </summary>
    /// <returns>An enumerator for coroutine execution.</returns>
    private IEnumerator CreateNetworkManagerAndContinue()
    {
        // Store reference of the old NetworkManager before destroying it
        var oldManager = NetworkManager.Singleton;

        if (oldManager != null)
        {
            // Destroy the old NetworkManager GameObject
            //Destroy(oldManager.gameObject);
            /*if (oldManager.IsListening)
            {
                
            }*/
            //oldManager.Shutdown();
            
            //NetworkUpdateLoop.UnregisterAllNetworkUpdates(oldManager);
            Destroy(oldManager.gameObject);

            // Wait until the object is really destroyed
            // Unity overrides == operator, so we also use .Equals(null) to be extra safe
            yield return new WaitUntil(() => oldManager == null || oldManager.Equals(null));
        }

        // Instantiate the new NetworkManager prefab
        Instantiate(networkManagerPrefab);

        // Wait until the new Singleton is properly assigned
        yield return new WaitUntil(() => NetworkManager.Singleton != null);

        yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure everything is initialized

        menuControllerGameMode.EnablePlayGameModeButton(enableState: true);
        // Safe to continue with match session creation
        //CreateOrJoinMatchSession(gameModeData).Forget();
    }

    #endregion

    #region UI Events Proxy
    /// <summary>
    /// This is just a proxy to be used in the inspector because the real one is a task
    /// </summary>
    public void CreateOrJoinMatchSessionProxy(GameModeData gameModeData)
    {
        CreateOrJoinMatchSession(gameModeData).Forget();
        //StartCoroutine(CreateNetworkManagerAndContinue(gameModeData));
    }

    /// <summary>
    /// This is just a proxy to be used in the inspector because the real one is a task
    /// </summary>
    public void LeaveMatchSessionProxy()
    {
        LeaveMatch(true).Forget();
    }

    /// <summary>
    /// Checks if the player is currently in a match session.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    /// <returns></returns>
    public void CheckIfIsInMatch(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = IsInMatch;
    }

    /// <summary>
    /// Checks if the player is currently matchmaking.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfIsMatchmaking(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = IsMatchmakingRef;
    }

    /// <summary>
    /// Checks if the player is currently the hosting.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfLocalPlayerIsHost(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = IsHost;
    }
    
    /// <summary>
    /// Checks if the player is currently running a party relay.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfIsPartyRelay(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = IsPartyRelay.Value;
    }

    /// <summary>
    /// Checks if the player is timeout.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfIsTimeout(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = TurnTimeRemaining.Value <= 0;
    }
    #endregion

    #region Player Dropouts Case Manager
    [SerializeField] private List<ulong> disconnectedClientIds = new List<ulong>();
    /*[SerializeField] private bool disconnected_players_0 = false; //Host
    [SerializeField] private bool disconnected_players_1 = false;
    [SerializeField] private bool disconnected_players_2 = false;
    [SerializeField] private bool disconnected_players_3 = false;*/

    private void AssignDisconnectStatusToTheClient(int clientId)
    {
        //int auxClindex = clientsBasicInfoCollection.Value.playerBasicInfos.ToList().FindIndex(p => p.clientId == clientId);
        int auxClienIndex = GetPlayerIndexID(clientId);

        if (currentGameMode.GameTypeSelectedID == GameType.competitive)
        {
            if (currentGameMode.VSPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                switch (auxClienIndex)
                {
                    case 0:
                        currentGameMode.ExtendedGameController.TurnScript._playerIsEliminated = true;
                        break;
                    case 1:
                        currentGameMode.ExtendedGameController.TurnScript._topAIIsEliminated = true;
                        break;
                    default:
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Invalid client ID: {auxClienIndex}</color>");
                        break;
                }
            }
            else
            {
                switch (auxClienIndex)
                {
                    case 0:
                        currentGameMode.ExtendedGameController.TurnScript._playerIsEliminated = true;
                        break;
                    case 1:
                        currentGameMode.ExtendedGameController.TurnScript._leftAIIsEliminated = true;
                        break;
                    case 2:
                        currentGameMode.ExtendedGameController.TurnScript._topAIIsEliminated = true;
                        break;
                    case 3:
                        currentGameMode.ExtendedGameController.TurnScript._rightAIIsEliminated = true;
                        break;
                    default:
                        Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> Invalid client ID: {auxClienIndex}</color>");
                        break;
                }
            }
        }
        else if (currentGameMode.GameTypeSelectedID == GameType.casual)
        {
            currentGameMode.SetPlayer_Bot(auxClienIndex);
        }
    }
    #endregion

    private int GetPlayerIndexID(int clientId)
    {
        return ClientsBasicInfosCollection.Value.playerBasicInfos.ToList().FindIndex(p => p.clientId == clientId);
    }

    #region Events
    /// <summary>
    /// Event triggered when a player joins the session.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="data"></param>
    private async void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: {data.EventType} for client {data.ClientId}</color>");

        if (IsInMatch)
        {
            // If the host detects a disconnection from a client, reduce the clients count and assign the disconnected status to the player
            if (IsHost && data.EventType == ConnectionEvent.ClientDisconnected && !disconnectedClientIds.Contains(data.ClientId))
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: Disconnectiong client {data.ClientId}</color>");
                ClientsCount.Value -= currentGameMode.GameTypeSelectedID == GameType.competitive ? 1 : 0;
                disconnectedClientIds.Add(data.ClientId);
                AssignDisconnectStatusToTheClient((int)data.ClientId);
            }

            // If the host detects a reconnection from a client, log it
            else if (IsHost && data.EventType == ConnectionEvent.ClientConnected && disconnectedClientIds.Contains(data.ClientId))
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: Reconnecting client {data.ClientId}</color>");

            // If the local client disconnects, return to lobby
            else if (!IsHost && data.EventType == ConnectionEvent.ClientDisconnected && data.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: Local client disconnected {data.ClientId}</color>");

                // Get the status of the match completion to determine if the player will return to lobby or not (if the match is completed, the player should return without call the give up event)
                var wasMatchComppleted = currentGameMode.ExtendedGameController.GameIsCompleteAndFinished;

                // Trigger the give up event if the match was not completed, to save the match result in the leaderboard
                menuControllerGameMode.ToLobby(!wasMatchComppleted);
            }
        }
        else
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: Not in match session, ignoring connection event for client {data.ClientId}</color>");

        // If the host receives the any client event and is not the client (acting as a server), ignore the callback
        if (IsHost
            && (data.EventType is ConnectionEvent.ClientConnected or ConnectionEvent.ClientDisconnected)
            && data.ClientId is not NetworkManager.ServerClientId)
        {
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> OnConnectionEvent: Host received a client event for client {data.ClientId}, ignoring...</color>");
            return;
        }

        // Determine if the client that trigger the event is the host or not
        var isClientHost = data.ClientId is NetworkManager.ServerClientId;

        // Handle connection events
        if (data.EventType is ConnectionEvent.ClientConnected or ConnectionEvent.PeerConnected)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Client/Peer was connected successfully</color>");

            // Try to send the player ID to the server if the event is ClientConnected
            if (data.EventType is ConnectionEvent.ClientConnected)
            {
                // Get the record of the player to check if it was added before
                var playerRecord = !string.IsNullOrEmpty(authManager.UUID) ? FindPlayerRecord(authManager.UUID) : default;

                // Check if the client was not connected before 
                if (!string.IsNullOrEmpty(authManager.UUID))
                {
                    if (playerRecord is null)
                    { 
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Client {data.ClientId} ({authManager.UUID}) was connected successfully</color>");
                        var newPlayerRecord = new PlayerRecord(
                            originClientId: (int)data.ClientId, 
                            newClientId: (int)data.ClientId,
                            playerId: authManager.UUID);

                        // Register the user in the records
                        AddPlayerRecord_Rpc(newPlayerRecord);

                        // Configure relay players data
                        SetPlayerBasicInfo_Rpc(new
                            ((int)data.ClientId,
                            authManager.UUID,
                            authManager.Username,
                            gameManager.GetProfilePicture().id));
                    }
                    
                    // Else, the client was connected before (reconnection)
                    else
                    {
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Client {data.ClientId} is trying to reconnect...</color>");

                        var overridePlayerRecord = new PlayerRecord(
                            originClientId: playerRecord.Value.originClientId,
                            newClientId: (int)data.ClientId,
                            playerId: playerRecord.Value.playerId);

                        // Register the user in the records
                        AddPlayerRecord_Rpc(overridePlayerRecord);

                        // Configure relay players data
                        SetPlayerBasicInfo_Rpc(new
                            (playerRecord.Value.originClientId,
                            authManager.UUID,
                            authManager.Username,
                            gameManager.GetProfilePicture().id));

                        OnReconnectSession?.Invoke(authManager.UUID);
                    }
                }
            }
            else if (data.EventType is ConnectionEvent.PeerConnected)
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Peer {(int)data.ClientId} ({authManager.UUID}) was connected successfully</color>");
        } 
        
        else if (data.EventType is ConnectionEvent.ClientDisconnected or ConnectionEvent.PeerDisconnected)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Client/Peer was disconnect</color>");

            // Trigger leave events for the disconnected client
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                var playerLeavingId = string.Empty;
                var recordedClientPlayerId = FindPlayerRecord((int)data.ClientId);

                if (data.EventType is ConnectionEvent.ClientDisconnected)
                    playerLeavingId = authManager.UUID;

                // Try to get a previously record of the current joined client
                else
                    playerLeavingId = recordedClientPlayerId?.playerId.Value;

                if (!string.IsNullOrEmpty(playerLeavingId))
                    await TriggerLeaveEvents(recordedClientPlayerId?.originClientId ?? (int)data.ClientId, playerLeavingId);
                else
                    Debug.LogError($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Peer couldn't invoke trigger leave event because its player id is null or empty</color>");
            }

            // The ConnectedEvent.ClientDisconnect is been called when the ConnectedEvent.PeerDisconnect, due that we need check in another way
            var isLocalClientDisconnected = localPlayerMatchID == (int)data.ClientId;

            // If the session is not null, is possible reconnect againg. Else, the clidnt doesn't have nay session to reconnect
            if (isLocalClientDisconnected)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Client {data.ClientId} ({authManager.UUID}) was disconnected</color>");

                // If there is not a shutdown progress, you could do relay actions (like reconnection or shutdown)
                if (!NetworkManager.Singleton.ShutdownInProgress)
                {
                    if (isClientHost)
                    { 
                        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Host {data.ClientId} is shutting down the server</color>");

                        // This should be the last line of the method (it breaks the sequence)
                        ResetSession_Rpc();
                        NetworkManager.Singleton.Shutdown(true);
                    }

                    else
                    {
                        //StartHostReconnectionProcess_Rpc(data.ClientId); ===> Start reconnection process from Server Host
                    }
                }
            } 

            else
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Peer {data.ClientId} ({authManager.UUID}) was disconnected</color>");


                if (!NetworkManager.Singleton.ShutdownInProgress)
                { 
                    if (isClientHost)
                    {
                        //StartReconnectionProcess_Rpc(data.ClientId); ===> Start reconnection process from Server Host
                    }
                }
            }
        }
    }
    private async UniTask TriggerLeaveEvents(int originalClientId, string playerId)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TriggerLeaveEvents: events triggered for client {originalClientId} ({playerId})...</color>");

        // Remove the client date of the records only if the server is not shutted down and the client still connected
        if (IsHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            RemovePlayerBasicInfo_Rpc(originalClientId);
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> TriggerLeaveEvents: Could not remove player basic info for client {originalClientId} ({playerId}) because the server is shutted down or the client is not connected...</color>");

        // Try to wait until the server removes the player from the party collection or timeout
        if ((IsPartyRelay?.Value ?? false) || (localIsPartyRelay ?? false))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TriggerLeaveEvents: waiting for party collection to update for client {originalClientId} ({playerId})...</color>");

            await UniTask
                .WaitUntil(() => (!PartyBasicInfoCollectionRef?.playerBasicInfos.Any(x => x.clientId == originalClientId) ?? true) || !IsInRelay)
                .TimeoutWithoutException(TimeSpan.FromSeconds(10));

            // Inform the game mode that the player has left
            // NOTE: this event is called when the server is not shutted down yet, so there are alive references that will die at end of this method
            var updatedList = PartyBasicInfoCollectionRef?.playerBasicInfos
                ?.Where(x => !string.IsNullOrEmpty(x.playerId) && x.clientId != originalClientId)
                ?.Select(x => ((int)x.clientId, x.playerId))
                ?.ToArray();

            // If there are no players left, set the updated list to null
            if (updatedList?.All(x => string.IsNullOrEmpty(x.playerId)) ?? false)
                updatedList = default;

            // Inform the party controller that a player has left
            PartyController.OnPlayerLeave(playerId, updatedList);
        }

        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> TriggerLeaveEvents: invoking OnLeftSession event for client {originalClientId} ({playerId})...</color>");
        OnLeftSession?.Invoke(playerId);
    }

    private void SpawnMatchState()
    {
        // Safety check: ensure we do not spawn duplicates
        if (currentMatchState != null &&
            currentMatchState.NetworkObject.IsSpawned)
        {
            return;
        }

        // Instantiate locally on the server
        currentMatchState = Instantiate(matchStatePrefab);

        // Spawn across the network
        currentMatchState.NetworkObject.Spawn();

        Debug.Log("[MatchManager] MatchState spawned by server");
    }

    /// <summary>
    /// Main attaching point when the player joins the match session.<br></br>
    /// This registers all the needed events and set the local player match ID.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnNetworkSpawn: network spawned</color>");

        // Subscribe on both server and clients
        MatchState.OnMatchStateSpawned += OnMatchStateSpawned;
        MatchState.OnMatchStateDespawned += OnMatchStateDespawned;

        // Only the server is allowed to create networked objects
        if (IsServer)
        {
            SpawnMatchState();
        }

        // Reset the server properties manually to avoid data preserveation if the server is shutted down.
        // Force value change so NGO marks it dirty
        if (IsServer)
            ResetFields();
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> OnNetworkSpawn: Client is not the host, couldn't reset the server properties...</color>");


        // Register all events
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnConnectionEvent += OnConnectionEvent;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    /// <summary>
    /// Main detaching point when the player leaves the match session.<br></br>
    /// This unregisters all the events and reset the local player match ID.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnNetworkDespawn: network despawned</color>");

        // Always unsubscribe to avoid memory leaks and ghost callbacks
        MatchState.OnMatchStateSpawned -= OnMatchStateSpawned;
        MatchState.OnMatchStateDespawned -= OnMatchStateDespawned;

        // Set game search status to none and reset the client
        if (menuControllerGameMode._GameModeConfig._GameSearchStatus != GameSearchStatus.none)
            ResetClient();

        // If the status is already none, just enable the play button
        else
            menuControllerGameMode._GameModeConfig.EnablePlayButtonAfterCancelingSearch();

        // Trigger leave events for the local player
        TriggerLeaveEvents(default, default).Forget();

        // Unregister all events
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnConnectionEvent -= OnConnectionEvent;
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;

        // If the player was searching for a game, restart the matchmaking process
        if (menuControllerGameMode._GameModeConfig._GameSearchStatus == GameSearchStatus.SearchingGame)
            StartCoroutine(menuControllerGameMode._GameModeConfig.RestartMatchmaking());
    }

    private void OnMatchStateSpawned(MatchState state)
    {
        // This will be called on:
        // - Server (right after Spawn)
        // - Clients (when snapshot arrives)
        currentMatchState = state;

        // Ensure the MatchState is right after the MatchManager in the hierarchy for better organization
        var managerIndex = transform.GetSiblingIndex();
        state.transform.SetSiblingIndex(managerIndex + 1);

        // Try to get a previously record of the current joined client
        var recordedClientPlayerId = FindPlayerRecord(authManager.UUID);

        // Set the local player match ID
        if (recordedClientPlayerId.HasValue)
            localPlayerMatchID = recordedClientPlayerId.Value.originClientId;
        else
            localPlayerMatchID = (int)NetworkManager.Singleton.LocalClientId;

        // Register NetworkVariable change events
        CurrentClientID.OnValueChanged += OnCurrentPlayablePlayerModified;
        ClientsBasicInfosCollection.OnValueChanged += OnClientsBasicInfoCollectionChanged;
        PartyBasicInfosCollection.OnValueChanged += OnPartyBasicInfoCollectionChanged;

        Debug.Log("[MatchManager] MatchState reference acquired");
    }

    private void OnMatchStateDespawned()
    {
        // Clear local reference when the state is destroyed
        currentMatchState = null;

        // Reset local player match ID
        localPlayerMatchID = -1;

        // Unregister NetworkVariable change events
        CurrentClientID.OnValueChanged -= OnCurrentPlayablePlayerModified;
        ClientsBasicInfosCollection.OnValueChanged -= OnClientsBasicInfoCollectionChanged;
        PartyBasicInfosCollection.OnValueChanged -= OnPartyBasicInfoCollectionChanged;

        Debug.Log("[MatchManager] MatchState reference cleared");
    }

    /// <summary>
    /// Called when the server is started.<br></br>
    /// This sets the IsPartyRelay NetworkVariable if the local player is the host and the relay is marked as party one.
    /// </summary>
    private void OnServerStarted()
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnServerStarted: Server started</color>");

        // Only the host could mark this relay as a party one
        if (IsHost)
            IsPartyRelay.Value = localIsPartyRelay ?? false;
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> OnServerStarted: Client is not the host, couldn't set IsPartyRelay NetworkVariable...</color>");
    }

    /// <summary>
    /// Called when the server is stopped.<br></br>
    /// This removes the relay join code from the matchmaking server if needed.
    /// </summary>
    /// <param name="isClientStopped"></param>
    private void OnServerStopped(bool isClientStopped)
    {
        if (!string.IsNullOrEmpty(joinCodeManager.MatchMakingQueueName) && !string.IsNullOrEmpty(joinCodeManager.JoinCode))
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnServerStopped: Server is stopped, proceed to remove relay code in server...</color>");
            joinCodeManager.TryToRemoveRelayCodeAsync(joinCodeManager.MatchMakingQueueName, joinCodeManager.JoinCode).AsUniTask().Forget();
        } 
        else
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnServerStopped: Server is stopped, couldn't proceed to remove relay code in server...</color>");
    }

    /// <summary>
    /// Called when the CurrentPlayablePlayerClientID NetworkVariable changes.<br></br>
    /// This inform about the current playable player changing.
    /// </summary>
    /// <param name="previousValue"></param>
    /// <param name="newValue"></param>
    private void OnCurrentPlayablePlayerModified(int previousValue, int newValue)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnCurrentPlayablePlayerModified: Current playable player changed from {previousValue} to {newValue}</color>");

        // Inform the game mode that the current playable player has changed
        OnCurrentPlayablePlayerChanged?.Invoke();
    }

    /// <summary>
    /// Called when the ClientsBasicInfoCollection NetworkVariable changes.<br></br>
    /// This inform about new players joining the session.
    /// </summary>
    /// <param name="prev"></param>
    /// <param name="curr"></param>
    private void OnClientsBasicInfoCollectionChanged(ClientsBasicInfoCollection prev, ClientsBasicInfoCollection curr)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> ClientsBasicInfoCollection changed:</color>\nPrevious: {JsonConvert.SerializeObject(prev, Formatting.Indented)}\nCurrent: {JsonConvert.SerializeObject(curr, Formatting.Indented)}");

        // Get the distinct players that joined
        var distinctPlayers = curr.playerBasicInfos
            ?.Where(b => !prev.playerBasicInfos.Any(a => a.playerId == b.playerId))
            ?.ToList();

        // Try to get the player ID from the server
        if (ClientsInfoCollectionRef is not null)
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> Players Ids registered:\n\n{JsonConvert.SerializeObject(curr, Formatting.Indented)} </color>");
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> ClientsBasicInfoCollection is null.</color>");

        // Check if there are new players and iterate through them to trigger the join events
        if (distinctPlayers is not null and { Count: > 0 })
            foreach (var newPlayer in distinctPlayers)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> Player joined session: \n\nPlayerId: {newPlayer.playerId}\"ClientId: {newPlayer.clientId}\"</color>");

                // Inform the game mode that a player has joined
                OnJoinSession?.Invoke(newPlayer.playerId);

                // Trigger the connected RPC event
                TriggerOnCliendConnectedRpc();
            }
    }
    
    /// <summary>
    /// Called when the ClientsBasicInfoCollection NetworkVariable changes.<br></br>
    /// This inform about new players joining the session.
    /// </summary>
    /// <param name="prev"></param>
    /// <param name="curr"></param>
    private void OnPartyBasicInfoCollectionChanged(ClientsBasicInfoCollection prev, ClientsBasicInfoCollection curr)
    {
        Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnPartyBasicInfoCollectionChanged: Value changed</color>\nPrevious: {JsonConvert.SerializeObject(prev, Formatting.Indented)}\nCurrent: {JsonConvert.SerializeObject(curr, Formatting.Indented)}");

        // Get the distinct players that joined
        var distinctPlayers = curr.playerBasicInfos
            ?.Where(b => !prev.playerBasicInfos.Any(a => a.playerId == b.playerId))
            ?.ToList();

        // Try to get the player ID from the server
        if (PartyBasicInfoCollectionRef is not null)
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnPartyBasicInfoCollectionChanged: Players Ids registered:\n\n{JsonConvert.SerializeObject(curr, Formatting.Indented)} </color>");
        else
            Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(MatchManager)}]</b> OnPartyBasicInfoCollectionChanged: PartyBasicInfoCollection is null.</color>");

        // Check if there are new players and iterate through them to trigger the join events
        if (distinctPlayers is not null and { Count: > 0 })
            foreach (var newPlayer in distinctPlayers)
            {
                Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(MatchManager)}]</b> OnPartyBasicInfoCollectionChanged: Player joined session: \n\nPlayerId: {newPlayer.playerId}\"ClientId: {newPlayer.clientId}\"</color>");

                var clientName = PartyBasicInfoCollectionRef?.playerBasicInfos?.FirstOrDefault(x => x.clientId == newPlayer.clientId).playerName ?? string.Empty;
                
                // Inform the party controller that a player has joined
                if (IsPartyRelay?.Value ?? localIsPartyRelay ?? false)
                {
                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchManager)}]</b> OnPartyBasicInfoCollectionChanged: Informing PartyController about player with name {clientName} joining...</color>");

                    // Inform the party controller that a player has joined
                    PartyController.OnPlayerJoin(newPlayer.playerId, clientName);
                }
            }
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
        public int clientId;
        public string playerId;
        public string playerName;
        public string profileIconId;

        public PlayerBasicInfo(int clientId, string playerId, string playerName, string profileIconId)
        {
            this.clientId = clientId;
            this.playerId = playerId ?? string.Empty;
            this.playerName = playerName ?? string.Empty;
            this.profileIconId = profileIconId ?? string.Empty;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);

            // Unity Netcode no soporta null strings, convertir siempre
            if (serializer.IsWriter)
            { 
                if (playerId == null)
                    playerId = string.Empty;

                if (playerName == null)
                    playerName = string.Empty;
                
                if (profileIconId == null)
                    profileIconId = string.Empty;
            }

            serializer.SerializeValue(ref playerId);
            serializer.SerializeValue(ref playerName);
            serializer.SerializeValue(ref profileIconId);
        }
    }
    
    public struct PlayerRecord : INetworkSerializable, IEquatable<PlayerRecord>
    {
        public int originClientId;
        public int newClientId;
        public FixedString64Bytes playerId;

        public PlayerRecord(int originClientId, int newClientId, FixedString64Bytes playerId)
        {
            this.originClientId = originClientId;
            this.newClientId = newClientId; 
            this.playerId = playerId;
        }

        public bool Equals(PlayerRecord other)
        {
            return originClientId == other.originClientId &&
                   playerId == other.playerId;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref originClientId);
            serializer.SerializeValue(ref newClientId);
            serializer.SerializeValue(ref playerId);
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

        [JsonIgnore]
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

    // Custom serializable KeyValuePair replacement
    public struct IntHandPair : INetworkSerializable, IEquatable<IntHandPair>
    {
        public int ClientId;
        public FixedList32Bytes<int> Hand; // A small list of ints (max 8)

        public IntHandPair(int clientId, FixedList32Bytes<int> hand)
        {
            ClientId = clientId;
            Hand = hand;
        }

        public bool Equals(IntHandPair other)
        {
            if (ClientId != other.ClientId || Hand.Length != other.Hand.Length)
                return false;

            for (int i = 0; i < Hand.Length; i++)
                if (Hand[i] != other.Hand[i])
                    return false;
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize clientId normally
            serializer.SerializeValue(ref ClientId);

            // Serialize the length of the FixedList
            int count = Hand.Length;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader)
            {
                // Clear before filling when reading
                Hand.Clear();

                // Read each element
                for (int i = 0; i < count; i++)
                {
                    int value = 0;
                    serializer.SerializeValue(ref value);
                    Hand.Add(value);
                }
            } else
            {
                // Write each element
                for (int i = 0; i < count; i++)
                {
                    int value = Hand[i];
                    serializer.SerializeValue(ref value);
                }
            }
        }
    }
}