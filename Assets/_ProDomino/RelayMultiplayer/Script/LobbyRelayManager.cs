/*using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;

public class LobbyRelayManager : MonoBehaviour
{
    [SerializeField] private int maxPlayers = 4;
    private Lobby currentLobby;
    private float heartbeatInterval = 15f; // Para mantener vivo el lobby

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Autenticado como Anónimo.");
        }
    }

    // Crear Lobby + Relay usando un código personalizado
    public async Task CreateLobbyAndRelay(string customCode)
    {
        try
        {
            // Crear la Allocation en Relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Crear Lobby incluyendo el joinCode como metadata
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "customCode", new DataObject(DataObject.VisibilityOptions.Public, customCode) },
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };

            Lobby lobby = await Lobbies.Instance.CreateLobbyAsync(customCode, maxPlayers, options);
            currentLobby = lobby;

            Debug.Log($"Lobby creado: {lobby.Name} - Código personalizado: {customCode}");

            // Configurar el transporte de red
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            //RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            Debug.Log("Host iniciado.");

            // Comenzar el heartbeat
            InvokeRepeating(nameof(SendHeartbeat), 0, heartbeatInterval);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creando lobby/relay: {e.Message}");
        }
    }

    // Unirse usando el código personalizado
    public async Task JoinLobbyAndRelay(string customCode)
    {
        try
        {
            // Buscar Lobby por el campo "customCode"
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.S1, // Primer campo string
                        op: QueryFilter.OpOptions.EQ,       // Igualdad
                        value: customCode
                    )
                }
            };

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryOptions);

            if (queryResponse.Results.Count == 0)
            {
                Debug.LogWarning("No se encontró un lobby con ese código.");
                return;
            }

            Lobby foundLobby = queryResponse.Results[0];
            currentLobby = foundLobby;

            string joinCode = foundLobby.Data["joinCode"].Value;

            // Unirse al Allocation de Relay
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Configurar transporte
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            //RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            Debug.Log("Cliente conectado.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error uniéndose al lobby/relay: {e.Message}");
        }
    }

    private async void SendHeartbeat()
    {
        if (currentLobby != null)
        {
            try
            {
                await Lobbies.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"Heartbeat fallido: {e.Message}");
            }
        }
    }
}*/