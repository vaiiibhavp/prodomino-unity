using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProDomino.NotificationSystem
{
    /// <summary>
    /// Receives Firebase Cloud Messaging payloads from the JavaScript bridge in WebGL.
    /// This script should be attached to a GameObject named "GameObjectFCM"
    /// </summary>
    public class FirebaseWebGLReceiver : MonoBehaviour
    {
        [Tooltip("Event invoked when a new Firebase notification is received.")]
        [SerializeField] private UnityEvent<FirebaseNotificationPayload> getNotificationEvent;

        /// <summary>
        /// Called from JavaScript (index.html) when a new push notification arrives via SendMessage.
        /// Parses the incoming JSON string into a structured payload and invokes the event.
        /// </summary>
        /// <param name="jsonMessage">The raw JSON payload received from Firebase.</param>
        public void OnFirebaseMessage(string jsonMessage)
        {
            Debug.Log("[FirebaseWebGLReceiver] Received message: " + jsonMessage);

            try
            {
                var payload = JsonConvert.DeserializeObject<FirebaseNotificationPayload>(jsonMessage);
                if (payload is null or { notification: null })
                {
                    Debug.LogWarning("Couldn't receive notification. It was null or empty");
                    return;
                }

                Debug.Log($"[Firebase] Title: {payload.notification.title}");
                Debug.Log($"[Firebase] Body: {payload.notification.body}");
                getNotificationEvent?.Invoke(payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FirebaseWebGLReceiver] Failed to parse JSON: " + e.Message);
            }
        }

        /// <summary>
        /// Registers a listener to be called when a Firebase notification is received.
        /// </summary>
        public void RegisterOnMessageReceived(UnityAction<FirebaseNotificationPayload> onGetNotification)
        {
            if (getNotificationEvent is null || onGetNotification is null)
            {
                Debug.LogWarning("Couldn't subscribe action on get notification event because a null was detected");
                return;
            }

            getNotificationEvent.AddListener(onGetNotification);
        }

        /// <summary>
        /// Unregisters a previously added listener from the notification event.
        /// </summary>
        public void UnregisterOnMessageReceived(UnityAction<FirebaseNotificationPayload> onGetNotification)
        {
            if (getNotificationEvent is null || onGetNotification is null)
            {
                Debug.LogWarning("Couldn't unsubscribe action on get notification event because a null was detected");
                return;
            }

            getNotificationEvent.RemoveListener(onGetNotification);
        }

        /// <summary>
        /// Structure that represents the expected format of a Firebase message payload.
        /// </summary>
        [Serializable]
        public class FirebaseNotificationPayload
        {
            public string from;
            public Dictionary<string, string> data;
            public Notification notification;

            [Serializable] public class Notification
            {
                public string title;
                public string body;
                public string image;
            }
        }
    }
}
