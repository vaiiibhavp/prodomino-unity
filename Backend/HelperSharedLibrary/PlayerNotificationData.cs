using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerNotificationData : ICloneable, IEquatable<PlayerNotificationData>
    {
        [JsonProperty("id")]
        public string? id;

        [JsonProperty("title")]
        public string? title;

        [JsonProperty("body")]
        public string? body;
        
        [JsonProperty("image")]
        public string? image;
        
        [JsonProperty("senderID")]
        public string? senderID;
        
        [JsonProperty("targetID")]
        public string? targetID;

        [JsonProperty("notificationType")]
        public NotificationType notificationType;

        [JsonProperty("timestamp")]
        public long? timestamp;  // Unix timestamp UTC

        [JsonProperty("isGameNotificationread")]
        public bool isGameNotification;
        
        [JsonProperty("dataCollection")]
        public Dictionary<string, string>? dataCollection;

        [JsonProperty("read")]
        public bool read;

        public PlayerNotificationData()
        {
        }

        public PlayerNotificationData(string? title, string? body, string? image, string? senderID, string? targetID, NotificationType notificationType, long? timestamp, bool isGameNotification, Dictionary<string, string>? dataCollection, bool read)
        {
            this.id = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            this.title = title;
            this.body = body;
            this.image = image;
            this.senderID = senderID;
            this.targetID = targetID;
            this.notificationType = notificationType;
            this.timestamp = timestamp;
            this.isGameNotification = isGameNotification;
            this.dataCollection = dataCollection;
            this.read = read;
        }

        public bool ShouldDeletePartyInvitation(NotificationType notificationType, string? senderID, string? targetID)
        { 
            return
                (notificationType == this.notificationType && this.notificationType == NotificationType.PartyInvite)
                && senderID == this.senderID
                && targetID == this.targetID;
        }

        public object Clone() =>
            new PlayerNotificationData(title, body, image, senderID, targetID, notificationType, timestamp, isGameNotification, dataCollection, read);

        public bool Equals(PlayerNotificationData? other)
        {
            if (other == null)
                return false;

            // Compare dictionaries safely (same logic as override)
            bool dataEquals = true;
            if (dataCollection != null || other.dataCollection != null)
            {
                if (dataCollection == null || other.dataCollection == null)
                    dataEquals = false;
                else
                    dataEquals = dataCollection.Count == other.dataCollection.Count &&
                                 !dataCollection.Except(other.dataCollection).Any();
            }

            return title == other.title &&
                   body == other.body &&
                   image == other.image &&
                   senderID == other.senderID &&
                   targetID == other.targetID &&
                   notificationType == other.notificationType &&
                   isGameNotification == other.isGameNotification &&
                   dataEquals;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not PlayerNotificationData other)
                return false;

            return Equals(other); // reuse the typed implementation
        }


        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(title);
            hash.Add(body);
            hash.Add(image);
            hash.Add(senderID);
            hash.Add(targetID);
            hash.Add(notificationType);
            hash.Add(isGameNotification);

            if (dataCollection != null)
            {
                foreach (var kv in dataCollection.OrderBy(kv => kv.Key))
                {
                    hash.Add(kv.Key);
                    hash.Add(kv.Value);
                }
            }

            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerNotificationData? left, PlayerNotificationData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerNotificationData? left, PlayerNotificationData? right)
        {
            return !(left == right);
        }
    }
}
