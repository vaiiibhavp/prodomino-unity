/*using UnityEngine;
using Unity.Netcode;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;

public class MultiplayerManager : MonoBehaviour
{
    [SerializeField] private int maxConnections = 4;
    private const string joinCodeToUse = "your-join-code-placeholder"; // normal sería obtenido dinámicamente

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        TryJoinLobbyOrCreate();
    }

    private async void TryJoinLobbyOrCreate()
    {
        try
        {
            // Intentar UNIRSE al lobby existente con un Join Code
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeToUse);

            var relayServerData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();

            Debug.Log("Cliente conectado al lobby existente.");
        }
        catch (RelayServiceException e)
        {
            Debug.LogWarning("No se encontró lobby existente. Creando uno nuevo. Error: " + e.Message);

            // Si falló la unión, entonces CREAR un nuevo lobby
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log("Nuevo lobby creado con Join Code: " + joinCode);

            var relayServerData = new RelayServerData(allocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
        }
    }
}*/