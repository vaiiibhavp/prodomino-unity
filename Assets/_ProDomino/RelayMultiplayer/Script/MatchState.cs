using ProDomino.Shared;
using System;
using Unity.Netcode;
using UnityEngine;
using static MatchManager;

namespace ProDomino.RelayMultiplayer
{
    public class MatchState : NetworkBehaviour
    {
        #region NetworkVariables
        /// <summary>
        /// The remaining time for the current player's turn in seconds.
        /// </summary>
        [SerializeField]
        private NetworkVariable<float> turnTimeRemaining = new NetworkVariable<float>(
            value: 0f,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        [SerializeField]
        private NetworkVariable<StatusTimerInHost> statusTimerInHost = new NetworkVariable<StatusTimerInHost>(
            value: Shared.StatusTimerInHost.none,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// The number of players that the current session expects
        /// </summary>
        [SerializeField]
        private NetworkVariable<int> clientsCount = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// The client ID of the player whose turn it currently is
        /// </summary>
        [SerializeField]
        private NetworkVariable<int> currentPlayablePlayerClientID = new NetworkVariable<int>(
            value: -1,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Check if the relay was created to be a party one
        /// </summary>
        [SerializeField]
        private NetworkVariable<bool> isPartyRelay = new NetworkVariable<bool>(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Check if the relay is searching for players
        /// </summary>
        [SerializeField]
        private NetworkVariable<bool> isMatchmaking = new NetworkVariable<bool>(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Check if the current turn has been played
        /// </summary>
        [SerializeField]
        private NetworkVariable<bool> isTurnPlayed = new NetworkVariable<bool>(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );
        
        /// <summary>
        /// Determine if the host forced the play on timeout
        /// </summary>
        [SerializeField]
        private NetworkVariable<bool> wasPlayForced = new NetworkVariable<bool>(
            value: false,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// Those are the player IDs for each client, used to identify the players in the session.
        /// </summary>
        [SerializeField]
        private NetworkVariable<ClientsBasicInfoCollection> clientsBasicInfoCollection = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary>
        /// Those are the player IDs for specifically the clients, used to identify the players in the session.
        /// </summary>
        [SerializeField]
        private NetworkVariable<ClientsBasicInfoCollection> partyBasicInfoCollection = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary>
        /// Registers the clients connected in history of the relay
        /// </summary>
        [SerializeField]
        private NetworkList<PlayerRecord> playerRecords = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary>
        /// Registers the clients' EMCs for each player
        /// </summary>
        [SerializeField]
        private NetworkList<ClientEMC> clientsEMCs = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

        [SerializeField]
        private NetworkList<IntHandPair> tmpClientsHands = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );
        #endregion

        #region Events
        // Static event used as a "bind"
        // Any system interested in the MatchState existence can subscribe to this
        public static event Action<MatchState> OnMatchStateSpawned;
        public static event Action OnMatchStateDespawned;
        #endregion

        #region Properties
        public NetworkVariable<float> TurnTimeRemaining { get => turnTimeRemaining; set => turnTimeRemaining = value; }
        public NetworkVariable<StatusTimerInHost> StatusTimerInHost { get => statusTimerInHost; set => statusTimerInHost = value; }
        public NetworkVariable<int> ClientsCount { get => clientsCount; set => clientsCount = value; }
        public NetworkVariable<int> CurrentPlayablePlayerClientID { get => currentPlayablePlayerClientID; set => currentPlayablePlayerClientID = value; }
        public NetworkVariable<bool> IsPartyRelay { get => isPartyRelay; set => isPartyRelay = value; }
        public NetworkVariable<bool> IsMatchmaking { get => isMatchmaking; set => isMatchmaking = value; }
        public NetworkVariable<bool> IsTurnPlayed { get => isTurnPlayed; set => isTurnPlayed = value; }
        public NetworkVariable<bool> WasPlayForced { get => wasPlayForced; set => wasPlayForced = value; }
        public NetworkVariable<ClientsBasicInfoCollection> ClientsBasicInfoCollection { get => clientsBasicInfoCollection; set => clientsBasicInfoCollection = value; }
        public NetworkVariable<ClientsBasicInfoCollection> PartyBasicInfoCollection { get => partyBasicInfoCollection; set => partyBasicInfoCollection = value; }
        public NetworkList<PlayerRecord> PlayerRecords { get => playerRecords; set => playerRecords = value; }
        public NetworkList<ClientEMC> ClientsEMCs { get => clientsEMCs; set => clientsEMCs = value; }
        #endregion

        /// <summary>
        /// Called when the NetworkObject is spawned
        /// </summary>
        public override void OnNetworkSpawn()
        {
            // This is called on BOTH server and clients
            // It guarantees that the NetworkObject is fully initialized
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchState)}]</b> NetworkSpawn</color>");

            // Notify listeners that the MatchState is now available
            OnMatchStateSpawned?.Invoke(this);
        }

        /// <summary>
        /// Called when the NetworkObject is despawned
        /// </summary>
        public override void OnNetworkDespawn()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchState)}]</b> NetworkDespawn</color>");

            // Notify listeners that the MatchState no longer exists
            OnMatchStateDespawned?.Invoke();
        }

        /// <summary>
        /// Helper to set the clients basic info collection
        /// </summary>
        /// <param name="isPartyCollection"></param>
        /// <param name="collectionOverride"></param>
        public void SetClientsBasicInfoCollection(bool isPartyCollection, NetworkVariable<ClientsBasicInfoCollection> collectionOverride)
        { 
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(MatchState)}]</b> Setting {(isPartyCollection ? "Party" : "Clients")} Basic Info Collection Override</color>");

            if (isPartyCollection)
                PartyBasicInfoCollection.Value = collectionOverride.Value;
            else
                ClientsBasicInfoCollection.Value = collectionOverride.Value;
        }
    }
}
