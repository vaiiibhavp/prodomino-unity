using System;
using System.Runtime.InteropServices;

namespace FirebaseWebGL.Scripts.FirebaseBridge
{
    public static class FirebaseDatabase
    {
        /// <summary>
        /// Gets JSON from a specified path
        /// Will return a snapshot of the JSON in the callback output
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void GetJSON(string path, string objectName, string callback, string fallback);

        /// <summary>
        /// Posts JSON to a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="value"> JSON string to post to the specified path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void PostJSON(string path, string value, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Pushes JSON to a specified path with a Firebase generated unique key
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="value"> JSON string to push to the specified path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void PushJSON(string path, string value, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Updates JSON in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="value"> JSON string to update in the specified path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void UpdateJSON(string path, string value, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Deletes JSON in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void DeleteJSON(string path, string objectName, string callback, string fallback);

        /// <summary>
        /// Listens for value changes in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the onValueChanged/fallback of </param>
        /// <param name="onValueChanged"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForValueChanged(string path, string objectName, string onValueChanged,
            string fallback);

        /// <summary>
        /// Stops listening for value changed on a specific path
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForValueChanged(string path, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Listens for value changes in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the onChildAdded/fallback of </param>
        /// <param name="onChildAdded"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForChildAdded(string path, string objectName, string onChildAdded,
            string fallback);

        /// <summary>
        /// Stops listening for child added on a specific path
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForChildAdded(string path, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Listens for value changes in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the onChildChanged/fallback of </param>
        /// <param name="onChildChanged"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForChildChanged(string path, string objectName, string onChildChanged,
            string fallback);

        /// <summary>
        /// Stops listening for child changed on a specific path
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForChildChanged(string path, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Listens for value changes in a specified path
        /// </summary>
        /// <param name="path"> Database path </param>
        /// <param name="objectName"> Name of the gameobject to call the onChildRemoved/fallback of </param>
        /// <param name="onChildRemoved"> Name of the method to call when the listener is triggered. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ListenForChildRemoved(string path, string objectName, string onChildRemoved,
            string fallback);

        /// <summary>
        /// Stops listening for child removed on a specific path
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void StopListeningForChildRemoved(string path, string objectName, string callback,
            string fallback);

        /// <summary>
        /// Adds a specified number to a numeric value in a specified path using race conditions safe transactions
        /// If the value is not numeric or doesn't exist it will be treated as 0
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="amount"> Number to add </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ModifyNumberWithTransaction(string path, float amount, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Toggles a boolean flag in a specified path using race conditions safe transactions
        /// If the value is not a boolean or doesn't exist it will be treated as false
        /// </summary>
        /// <param name="path"> Database Path </param>
        /// <param name="objectName"> Name of the gameobject to call the callback/fallback of </param>
        /// <param name="callback"> Name of the method to call when the operation was successful. Method must have signature: void Method(string output) </param>
        /// <param name="fallback"> Name of the method to call when the operation was unsuccessful. Method must have signature: void Method(string output). Will return a serialized FirebaseError object </param>
        [DllImport("__Internal")]
        public static extern void ToggleBooleanWithTransaction(string path, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Initializes daily analytics when the player starts the game.
        /// Ensures the data structure exists and resets if the date changed.
        /// </summary>
        /// <param name="objectName">The name of the Unity object that will receive the callback.</param>
        /// <param name="callback">Callback method name (called on success).</param>
        /// <param name="fallback">Callback method name (called on error).</param>
        [DllImport("__Internal")]
        public static extern void InitializeDailyAnalytics(string objectName, string callback, string fallback);

        /// <summary>
        /// Modifies a specific analytics field in Firebase Realtime Database.
        /// Automatically creates the node if it does not exist.
        /// </summary>
        /// <param name="operationsJson">Json that contains every field required</param>
        /// <param name="objectName">The name of the Unity object that will receive the callback.</param>
        /// <param name="callback">Callback method name (called on success).</param>
        /// <param name="fallback">Callback method name (called on error).</param>
        [DllImport("__Internal")]
        public static extern void ModifyAnalyticsBatch(string operationsJson, string objectName,
            string callback, string fallback);

        /// <summary>
        /// Calls the WebGL .jslib search function, sending the search text and RTDB URL.
        /// Handles JS → C# callbacks and returns results via the provided delegates.
        /// </summary>
        /// <param name="searchTerm">Text used to filter matching players.</param>
        /// <param name="objectName">The name of the Unity object that will receive the callback.</param>
        /// <param name="callback">Callback invoked with the JSON result.</param>
        /// <param name="fallback">Callback invoked on failure.</param>
        [DllImport("__Internal")]
        public static extern void SearchPlayersByName(
            string searchTerm,
            string objectName,
            string callback,
            string fallback
        );


#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern string GetOrCreateStoredSessionId();
#else
        public static string GetOrCreateStoredSessionId() => Guid.NewGuid().ToString("N");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern void RemoveStoredSessionId();
#else
        public static void RemoveStoredSessionId() { }
#endif

        /// <summary>
        /// Checks if a user session is currently active in RTDB.
        /// This calls the WebGL .jslib function that compares:
        /// - existing sessionId stored in the database
        /// - new sessionId generated by this client/tab
        /// - lastHeartbeat timestamp
        ///
        /// JS will return either:
        /// - "ALLOW"  -> login permitted (no active session or expired)
        /// - "DENY"   -> another device/tab is currently active
        ///
        /// The JS result is delivered to Unity using:
        /// unityInstance.SendMessage(objectName, callbackMethod, result).
        /// </summary>
        /// <param name="userId">Unique user identifier in RTDB.</param>
        /// <param name="sessionId">Session ID generated by the client (one per tab).</param>
        /// <param name="objectName">Unity GameObject that will receive the callback.</param>
        /// <param name="callback">Method invoked on success (receives "ALLOW" or "DENY").</param>
        /// <param name="fallback">Method invoked when JS or RTDB throws an error.</param>
        [DllImport("__Internal")]
        public static extern void CheckIfSessionActive(
            string userId,
            string sessionId,
            string objectName,
            string callback,
            string fallback
        );


        /// <summary>
        /// Updates the user's session state in RTDB.
        /// This WebGL .jslib function writes:
        /// - sessionId   (to detect duplicate logins)
        /// - lastHeartbeat (epoch time in seconds)
        /// - status      (1 = online, 0 = offline)
        ///
        /// Typically called:
        /// - once after login validation
        /// - every X seconds (heartbeat)
        /// - once on ApplicationQuit (offline)
        ///
        /// The JS result is returned via:
        /// unityInstance.SendMessage(objectName, callbackMethod, "OK").
        /// </summary>
        /// <param name="userId">Unique user identifier in RTDB.</param>
        /// <param name="sessionId">Current session ID assigned to this tab.</param>
        /// <param name="status">Player status (1 = online, 0 = offline).</param>
        /// <param name="objectName">Unity GameObject that will receive the callback.</param>
        /// <param name="callback">Method invoked when the database update succeeds.</param>
        /// <param name="fallback">Method invoked when the update fails.</param>
        [DllImport("__Internal")]
        public static extern void UpdateSessionHeartbeat(
            string userId,
            string sessionId,
            int status,
            string objectName,
            string callback,
            string fallback
        );

        /// <summary>
        /// Retrieves the top entries from a leaderboard with optional filtering and callback handling.
        /// </summary>
        /// <param name="leaderboardIdsJson">Contains the IDs of the leaderboards to query, formatted as a JSON array string (e.g., '["leaderboard1", "leaderboard2"]').</param>
        /// <param name="nationality">The nationality filter to apply to the leaderboard entries.</param>
        /// <param name="limit">The maximum number of leaderboard entries to retrieve.</param>
        /// <param name="objectName">The name of the object to receive the callback.</param>
        /// <param name="callback">The name of the callback method to invoke on success.</param>
        /// <param name="fallback">The name of the fallback method to invoke on failure.</param>
        [DllImport("__Internal")]
        public static extern void GetTopLeaderboards(
            string leaderboardIdsJson,
            string nationality,
            int limit,
            string objectName,
            string callback,
            string fallback);
    }
}