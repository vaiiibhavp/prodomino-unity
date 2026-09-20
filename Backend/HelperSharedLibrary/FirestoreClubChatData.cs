using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Represents a club chat document in Firestore.
    /// This includes the most recent messages, last message summary,
    /// and a reference to older messages stored in the history subcollection.
    /// </summary>
    [Serializable]
    public class FirestoreClubChatData
    {
        [JsonProperty("clubName")]
        public string clubName;

        // Last visible message text in the chat (used for previews)
        [JsonProperty("lastMessage")]
        public string lastMessage;

        // Timestamp (in Unix milliseconds) of the last message update
        [JsonProperty("updatedAt")]
        public DateTime updatedAt;

        // The last N (e.g., 100) recent messages
        [JsonProperty("messages")]
        public List<MessageData> messages;

        [JsonProperty("historyCount")]
        public int historyCount;

        // Default constructor required for (de)serialization
        public FirestoreClubChatData()
        {
            clubName = string.Empty;
            lastMessage = string.Empty;
            updatedAt = DateTime.UtcNow;
            messages = new List<MessageData>();
            historyCount = 0;
        }

        // Custom constructor
        public FirestoreClubChatData
            (string clubName, 
            string? lastMessage = default,
            List<MessageData>? messages = default)
        {
            this.clubName = clubName;
            this.lastMessage = lastMessage ?? string.Empty;
            this.messages = messages ?? new List<MessageData>();
            updatedAt = DateTime.UtcNow;
            historyCount = messages?.Count ?? 0;
        }

        /// <summary>
        /// Parse Firestore JSON (raw string from REST API) into a FirestoreClubChatData object.
        /// Handles Firestore wrappers: stringValue, integerValue, arrayValue, mapValue, timestampValue.
        /// </summary>
        public static FirestoreClubChatData? ParseChatData(string firestoreJson)
        {
            // Early exit if JSON is null or empty
            if (string.IsNullOrWhiteSpace(firestoreJson))
                return null;

            JObject raw;
            try
            {
                raw = JObject.Parse(firestoreJson);
            }
            catch
            {
                // Return null if parsing fails
                return null;
            }

            var fields = raw["fields"];
            if (fields == null)
                return null;

            var chatData = new FirestoreClubChatData
            {
                // Parse the club name (if available)
                clubName = fields["clubName"]?["stringValue"]?.ToString() ?? string.Empty,

                // Parse the last visible message text
                lastMessage = fields["lastMessage"]?["stringValue"]?.ToString() ?? string.Empty,

                // Parse updatedAt as Firestore timestamp (ISO 8601)
                updatedAt = DateTime.TryParse(fields["updatedAt"]?["timestampValue"]?.ToString(), out var updated)
                    ? updated
                    : DateTime.MinValue,

                // Parse the total count of messages registered
                historyCount = int.TryParse(fields["historyCount"]?["integerValue"]?.ToString(), out var historyCount) ? historyCount : 0,
            };

            // Parse messages array
            var messagesToken = fields["messages"]?["arrayValue"]?["values"];
            if (messagesToken != null)
            {
                foreach (var messageToken in messagesToken)
                {
                    var field = messageToken["mapValue"]?["fields"];
                    if (field == null)
                        continue;

                    var message = new MessageData
                    (
                        messageId: field["messageId"]?["stringValue"]?.ToString() ?? string.Empty,
                        senderId: field["senderId"]?["stringValue"]?.ToString() ?? string.Empty,
                        senderName: field["senderName"]?["stringValue"]?.ToString() ?? string.Empty,
                        profileIconId: field["profileIconId"]?["stringValue"]?.ToString() ?? string.Empty,
                        content: field["content"]?["stringValue"]?.ToString() ?? string.Empty,
                        timestamp: DateTime.TryParse(field["timestamp"]?["timestampValue"]?.ToString(), out var ts)
                            ? ts
                            : DateTime.MinValue
                    );

                    chatData.messages.Add(message);
                }
            }

            return chatData;
        }

        /// <summary>
        /// Generates a consistent, padded message ID like "msg_000123".
        /// Designed for use in Cloud Code (no Unity/Firebase dependencies).
        /// </summary>
        /// <param name="totalMessagesCount">The current total number of messages (before adding the new one).</param>
        /// <param name="prefix">Optional prefix (default: "msg_").</param>
        /// <param name="paddingDigits">Number of digits to pad (default: 6, allowing up to 999,999 messages).</param>
        /// <returns>A unique message ID string (e.g., "msg_000123").</returns>
        public static string GenerateMessageId(int totalMessagesCount, string prefix = "msg_", int paddingDigits = 6)
        {
            if (totalMessagesCount < 0)
                totalMessagesCount = 0;

            // Increment the counter since the new message will be the next in sequence.
            int nextId = totalMessagesCount + 1;

            // Clamp padding between 1 and 12 digits for sanity
            paddingDigits = Math.Clamp(paddingDigits, 1, 12);

            // Format with zero padding (e.g., 000123)
            string padded = nextId.ToString($"D{paddingDigits}");

            // Return final ID (e.g., msg_000123)
            return $"{prefix}{padded}";
        }

        /// <summary>
        /// Represents a chat message exchanged within a club chat in Firestore.
        /// This model is designed for client-side usage and to deserialize data
        /// coming from Firestore via the WebGL .jslib listener.
        /// </summary>
        [Serializable]
        public class MessageData
        {
            [JsonProperty("messageId")]
            public string messageId;

            [JsonProperty("senderId")]
            public string senderId;

            [JsonProperty("senderName")]
            public string senderName;

            [JsonProperty("profileIconId")]
            public string profileIconId;

            [JsonProperty("content")]
            public string content;

            [JsonProperty("timestamp")]
            [JsonConverter(typeof(FirestoreTimestampConverter))]
            public DateTime timestamp;

            /// <summary>
            /// Parameterless constructor with safe defaults.
            /// </summary>
            [JsonConstructor]
            public MessageData()
            {
                messageId = string.Empty;
                senderId = string.Empty;
                senderName = string.Empty;
                profileIconId = string.Empty;
                content = string.Empty;
                timestamp = DateTime.UtcNow;
            }

            /// <summary>
            /// Full constructor for explicit initialization.
            /// </summary>
            public MessageData(
                string messageId,
                string senderId,
                string senderName,
                string profileIconId,
                string content,
                DateTime? timestamp = default)
            {
                this.messageId = messageId ?? string.Empty;
                this.senderId = senderId ?? string.Empty;
                this.senderName = senderName ?? string.Empty;
                this.profileIconId = profileIconId ?? string.Empty;
                this.content = content ?? string.Empty;
                this.timestamp = timestamp ?? DateTime.UtcNow;
            }
        }
    }
}
