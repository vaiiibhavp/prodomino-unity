using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace ProDomino.HandleProcessesSystem
{
    /// <summary>
    /// Periodically checks whether the device has internet connectivity by requesting
    /// a lightweight "ping" file from the same domain where the WebGL build is hosted.
    /// Falls back to a known stable URL when running in the Unity Editor.
    /// </summary>
    public class InternetChecker : MonoBehaviour
    {
        // Event invoked whenever the internet status changes
        [SerializeField] private UnityEvent<InternetStatus> onInternetStatusChanged;

        // Interval (in seconds) between connectivity checks
        public float checkInterval = 5f;

        // Stores the last known connection status
        private InternetStatus lastStatus;

        /// <summary>
        /// Returns the last internet status detected.
        /// </summary>
        public InternetStatus LastInternetStatus => lastStatus;

        private void Start()
        {
            // Start the periodic checking loop
            StartCoroutine(CheckLoop());
        }

        /// <summary>
        /// Extracts the base host (domain) from Application.absoluteURL.
        /// Returns null when running in the Editor or when URL cannot be parsed.
        /// </summary>
        private string GetBaseDomain()
        {
            // absoluteURL is only valid in WebGL builds
            var fullUrl = Application.absoluteURL;

            // Editor or local environment always gives empty string
            if (string.IsNullOrEmpty(fullUrl))
                return null;

            try
            {
                // Parse the hosting URL
                var uri = new Uri(fullUrl);
                return uri.Host; // Example: "www.playprodomino.com"
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Registers a listener for the internet status change event.
        /// </summary>
        public void HandleInternetStatusChanged(UnityAction<InternetStatus> action)
        {
            if (action == null || onInternetStatusChanged == null)
            {
                Debug.LogWarning("InternetChecker: action or event is null.");
                return;
            }

            onInternetStatusChanged.AddListener(action);
        }

        /// <summary>
        /// Removes a listener from the internet status change event.
        /// </summary>
        public void UnhandleInternetStatusChanged(UnityAction<InternetStatus> action)
        {
            if (action == null || onInternetStatusChanged == null)
            {
                Debug.LogWarning("InternetChecker: action or event is null.");
                return;
            }

            onInternetStatusChanged.RemoveListener(action);
        }

        /// <summary>
        /// Coroutine loop that repeatedly checks internet connectivity.
        /// </summary>
        private IEnumerator CheckLoop()
        {
            while (true)
            {
                yield return StartCoroutine(CheckInternet());
                yield return new WaitForSeconds(checkInterval);
            }
        }

        /// <summary>
        /// Performs the actual internet check by requesting a lightweight endpoint
        /// from the hosting domain (ping.txt). Uses a fallback domain in the Editor.
        /// </summary>
        private IEnumerator CheckInternet()
        {
            using (UnityWebRequest www = UnityWebRequest.Get(BuildPingUrl()))
            {
                // Timeout is critical in WebGL to avoid hanging forever
                www.timeout = 4;

                // Send the request
                yield return www.SendWebRequest();

                bool isOnline = false;

                // result==Success means a response arrived (even if HTTP error)
                // so we ensure the HTTP response code is 200
                if (www.result == UnityWebRequest.Result.Success && www.responseCode.ToString().StartsWith("20"))
                    isOnline = true;

                var newStatus = isOnline ? InternetStatus.Online : InternetStatus.Lost;

                // Trigger event only if the state changed
                if (newStatus != lastStatus)
                {
                    lastStatus = newStatus;
                    onInternetStatusChanged?.Invoke(lastStatus);
                }
            }

            /// <summary>
            /// Builds a valid ping URL. Uses the hosting domain when available,
            /// or a fallback domain (gstatic) when running in the Editor.
            /// </summary>
            string BuildPingUrl()
            {
                // Get hosting domain from WebGL environment
                var host = GetBaseDomain();

                // If null -> Editor mode Å® use a stable external URL
                if (string.IsNullOrEmpty(host))
                    return "https://www.gstatic.com/generate_204";

                // Construct absolute ping path in the hosting root
                return $"https://{host}/ping.txt";
            }
        }

        /// <summary>
        /// Possible internet status values.
        /// </summary>
        public enum InternetStatus
        {
            Online = 0,
            SearchingForConnection = 1, // Reserved for systems with multi-step logic
            Lost = 2,
        }
    }
}
