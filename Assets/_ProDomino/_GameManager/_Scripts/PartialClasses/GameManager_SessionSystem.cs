using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using System;
using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This class contains all info related to Session into the GameManager
    /// </summary>
    public partial class GameManager
    {
        [SerializeField] private float heartbeatInterval = 20f;
        [SerializeField] private SessionPopUp sessionPopUp;

        private bool sessionActive;
        private float heartbeatTimer = 0f;

        public string SessionId { get; private set; }

        private void Awake_SessionSystem()
        {
            // In Editor, always active to avoid blocking gaming
#if UNITY_EDITOR
            sessionActive = true;
#endif
        }

        /// <summary>
        /// Heartbeat loop
        /// </summary>
        private void Update_SessionSystem()
        {
            if (!sessionActive || !IsValidPlatformToUseJSlib() || !IsAuthenticated)
                return;

            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= heartbeatInterval)
            {
                heartbeatTimer = 0f;
                SendHeartbeat();
            }
        }

        private void OnApplicationQuit_SessionSystem()
        {
            OnSignOut_SessionSystem();
        }

        /// <summary>
        /// Sends a heartbeat to the Realtime Database to keep the session alive
        /// </summary>
        private void SendHeartbeat()
        {
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning("Firebase Realtime Database client is only available on WebGL platform");
                return;
            }

            if (!IsAuthenticated)
            { 
                Debug.LogWarning("User is not properly authenticated. Cannot send heartbeat.");
                return;
            }

            Debug.Log("Sending heartbeat for session: " + SessionId);

            // Status integer example:
            // 1 = online
            // 0 = offline (on quit)
            FirebaseDatabase.UpdateSessionHeartbeat(
                authManager.UUID,
                SessionId,
                1,
                gameObject.name,
                nameof(OnHeartbeatCallback),
                nameof(OnHeartbeatError)
            );
        }

        #region Events
        /// <summary>
        /// When the user signs in, we start the session system
        /// </summary>
        public async UniTask OnSignIn_SessionSystem()
        {
            if (!IsValidPlatformToUseJSlib())
            {
                Debug.LogWarning("Firebase Realtime Database client is only available on WebGL platform");
                return;
            }

            if (authManager is null or { IsAuthenticated: false })
            {
                Debug.LogWarning("User is not properly authenticated. Cannot start session system.");
                return;
            }

            Debug.Log("Starting session system for user: " + authManager.UUID);

            // Persistent per-tab sessionId
            SessionId = FirebaseDatabase.GetOrCreateStoredSessionId();

            FirebaseDatabase.CheckIfSessionActive(
                authManager.UUID,
                SessionId,
                gameObject.name,
                nameof(OnCheckSessionCallback),
                nameof(OnCheckSessionError)
            );

            // Wait until session is active or sign out occurs
            await UniTask.WaitUntil(() => sessionActive || !authManager.IsAuthenticated)
                .TimeoutWithoutException(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// When the user signs out, we end the session system<br></br>
        /// </summary>
        public void OnSignOut_SessionSystem()
        {
            if (!IsValidPlatformToUseJSlib())
                return;

            if (string.IsNullOrEmpty(lastUGSPlayerIDRegistered))
            { 
                Debug.LogWarning("No UGS Player ID registered. Cannot close session.");
                return;
            }

            if (sessionActive)
            {
                Debug.Log("Closing session: " + SessionId);

                FirebaseDatabase.UpdateSessionHeartbeat(
                    lastUGSPlayerIDRegistered,
                    SessionId,
                    0,
                    gameObject.name,
                    nameof(OnQuitSuccessfully),
                    nameof(OnFailedQuiting)
                );
            }

            // Clean per-tab storage (WebGL)
            FirebaseDatabase.RemoveStoredSessionId();

            sessionActive = false;
            SessionId = null;
        }

        /// <summary>
        /// Callback on application quit
        /// </summary>
        private void OnQuitSuccessfully(string msg)
        {
            Debug.Log("[GameManager] Session closed successfully: \n" + msg);
        }
        
        /// <summary>
        /// Callback on application quit
        /// </summary>
        private void OnFailedQuiting(string msg)
        {
            Debug.Log("[GameManager] Session closed fail: \n" + msg);
        }
        #endregion

        #region Callbacks
        /// <summary>
        /// Callback after checking for active sessions<br></br><br></br>
        /// ALLOW Å® player can log in<br></br>
        /// DENY  Å® other tab has priority
        /// </summary>
        private void OnCheckSessionCallback(string msg)
        {
            // If allowed, start session and heartbeat
            if (msg == "ALLOW")
            {
                Debug.Log("Session allowed. Starting heartbeat.");

                sessionActive = true;

                // First heartbeat sets the session in RTDB
                SendHeartbeat();
            }

            // But if denied, block session
            else
            {
                Debug.LogWarning("Another active session detected. Login blocked.");

                if (sessionPopUp)
                    sessionPopUp.Show();
                else
                    Debug.LogWarning("SessionPopUp is not assigned in the GameManager.");

                sessionActive = false;
                authManager.SignOut().Forget();
            }
        }

        /// <summary>
        /// Callback when checking session fails
        /// </summary>
        private void OnCheckSessionError(string err)
        {
            Debug.LogError("Check session failed: " + err);
            sessionActive = false;
        }

        /// <summary>
        /// Callback after sending heartbeat
        /// </summary>
        /// <param name="msg"></param>
        public void OnHeartbeatCallback(string msg)
        {
            // Debug only
            // msg = "OK"
            // If it fails, the JS fallback is triggered instead
            Debug.Log("Heartbeat response: " + msg);
        }

        /// <summary>
        /// Callback when heartbeat fails
        /// </summary>
        /// <param name="err"></param>
        public void OnHeartbeatError(string err)
        {
            Debug.LogError("Heartbeat error: " + err);
        }
        #endregion
    }
}
