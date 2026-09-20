using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using ProDomino.Authentication;
using Timba.Patterns;
using System.Threading;
using ProDomino.Shared;
using System;
using Cysharp.Threading.Tasks;

public class RelayManager : MonoBehaviour
{
    private AuthManager authManager;
    public string JoinCode { get; private set; }

    private void Awake()
    {
        authManager = ServiceLocator.Instance.GetService<AuthManager>();
    }

    /// <summary>
    /// Host creates an Allocation in Relay and starts as host.
    /// </summary>
    public async UniTask<string> TryToCreateRelay(int maxPlayers, CancellationToken cancellationToken)
    {
        if (authManager is null or { IsUGSAuthenticated: false })
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Can't create relay. User is not signed in.</color>");
            return null;
        }

        try
        {
            // Check cancel before starting
            cancellationToken.ThrowIfCancellationRequested();

            // 0. Ensure NetworkManager is shutdown before starting
            NetworkManager.Singleton.Shutdown();

            // 1. Create allocation
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(RelayManager)}]</b> Allocation created." +
                      $"\nAllocation Id: {allocation.AllocationId}</color>");

            // 2. Get join code
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(RelayManager)}]</b> Join code acquired." +
                      $"\nAllocation Id: {allocation.AllocationId}" +
                      $"\nJoin Code: {joinCode}</color>");

            // 3. Configure transport and start host
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "wss");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayServerData);

           
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(RelayManager)}]</b> Set relay server successfully" +
                      $"\nAllocation Id: {allocation.AllocationId}" +
                      $"\nJoin Code: {joinCode}</color>");

            return joinCode;
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Relay creation cancelled by user.</color>");
            return null;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Error creating Relay: <b>{e.Reason}</b>\n\n{e.Message}</color>");
            return null;
        }
        catch (TimeoutException)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> No client connected to the host after timeout.</color>");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Unexpected error creating Relay: {e}</color>");
            return null;
        }
    }

    /// <summary>
    /// Starts the host after allocation is created and transport is configured.
    /// </summary>
    public async UniTask<bool> StartHostRelay(CancellationToken cancellationToken)
    {
        try
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Starting host...</color>");
            var wasStartedSuccessfully = NetworkManager.Singleton.StartHost();
            if (!wasStartedSuccessfully)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Failed to start host after allocation.</color>");
                return false;
            } 
            else
                Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Host started, waiting for connection...</color>");

            // Once the connect process starts, wait until the client is connected succesfully to the server
            await UniTask.WaitUntil(() =>
                NetworkManager.Singleton.IsConnectedClient ||
                NetworkManager.Singleton.ShutdownInProgress ||
                cancellationToken.IsCancellationRequested,
                cancellationToken: cancellationToken,
                cancelImmediately: true
            ).Timeout(TimeSpan.FromSeconds(5)); // Short timeout since host is also a client

            // Once we exit the wait, check the status
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Start relay cancelled by user.</color>");
            return false;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Error Starting relay: <b>{e.Reason}</b>\n\n{e.Message}</color>");
            return false;
        }
        catch (TimeoutException)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> No client connected to the host after timeout.</color>");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Unexpected error Starting relay: {e}</color>");
            return false;
        }
    }

    /// <summary>
    /// Client uses the Join Code to connect to Relay
    /// </summary>
    /// <param name="joinCode"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async UniTask<bool> TryToJoinRelay(string joinCode, CancellationToken cancellationToken)
    {
        if (authManager is null or { IsUGSAuthenticated: false })
        {
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Can't join relay. User is not signed in.</color>");
            return false;
        }

        if (string.IsNullOrEmpty(joinCode))
        { 
            Debug.Log($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Can't join relay. Join code is null or empty.</color>");
            return false;
        }

        try
        {
            // Check cancel before starting
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Join allocation using Join Code
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            cancellationToken.ThrowIfCancellationRequested();

            // 2. Set up UnityTransport for the Client
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "wss"); //udp, dtls, wss
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayServerData);

            // 3. Start as Client
            var started = NetworkManager.Singleton.StartClient();
            if (!started)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Failed to start client</color>");
                return false;
            }

            try
            {
                Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Player connected as a Client, check if server is configurated...</color>");
               
                // Wait until is connected or is turned of, or the timeout is finished
                await UniTask.WaitUntil(
                    () => NetworkManager.Singleton.IsConnectedClient ||
                          NetworkManager.Singleton.ShutdownInProgress,
                    cancellationToken: cancellationToken
                ).Timeout(TimeSpan.FromSeconds(60));
            }
            catch (TimeoutException e)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> No host detected: {e}</color>");
                NetworkManager.Singleton.Shutdown(true);
                return false;
            }

            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(RelayManager)}]</b> Set relay server successfully" +
                      $"\nAllocation Id: {allocation.AllocationId}" +
                      $"\nJoin Code: {joinCode}</color>");

            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(RelayManager)}]</b> Relay joining cancelled by user.</color>");
            throw;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(RelayManager)}]</b> Error joining Relay: <b>{e.Reason}</b>\n\n{e.Message}</color>");
            return false;
        }
    }
}