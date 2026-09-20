using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;

namespace ProDomino.RelayMultiplayer
{
    /// <summary>
    /// Custom INetworkHandler that integrates with an existing NetworkManager,
    /// preventing errors when a host is already running.
    /// </summary>
    public sealed class ExistingNgoNetworkHandler : INetworkHandler
    {
        private readonly NetworkManager nm;
        private NetworkRole? activeRole;

        public ExistingNgoNetworkHandler(NetworkManager nm)
        {
            this.nm = nm ?? throw new ArgumentNullException(nameof(nm));
        }

        /// <summary>
        /// Configure and start networking according to the session configuration.
        /// </summary>
        public async Task StartAsync(NetworkConfiguration cfg)
        {
            var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (utp == null)
                throw new InvalidOperationException("UnityTransport required.");

            // Configure transport according to network type and role
            switch (cfg.Type)
            {
                case NetworkType.Relay:
                    if (cfg.Role == NetworkRole.Client)
                        utp.SetRelayServerData(cfg.RelayClientData);
                    else
                        utp.SetRelayServerData(cfg.RelayServerData);
                    break;

                case NetworkType.Direct:
                    if (cfg.Role == NetworkRole.Client)
                    {
                        utp.SetConnectionData(cfg.DirectNetworkPublishAddress.Address,
                                              (ushort)cfg.DirectNetworkPublishAddress.Port);
                    } else
                    {
                        utp.SetConnectionData(cfg.DirectNetworkListenAddress.Address,
                                              (ushort)cfg.DirectNetworkListenAddress.Port);
                    }
                    break;
            }

            // Prevent double start
            if (cfg.Role == NetworkRole.Host && (nm.IsHost || nm.IsServer))
                return;

            if (cfg.Role == NetworkRole.Client && nm.IsClient)
                return;

            // Actually start network according to role
            if (cfg.Role == NetworkRole.Host)
                nm.StartHost();
            else if (cfg.Role == NetworkRole.Server)
                nm.StartServer();
            else if (cfg.Role == NetworkRole.Client)
                nm.StartClient();

            activeRole = cfg.Role;
            await Task.Yield();
        }

        /// <summary>
        /// Stop networking cleanly according to the role that was active.
        /// </summary>
        public async Task StopAsync()
        {
            if (nm == null || !nm.IsListening)
                return;

            // Shut down the NGO network manager
            nm.Shutdown(true);

            activeRole = null;
            await Task.Yield();
        }
    }

}
