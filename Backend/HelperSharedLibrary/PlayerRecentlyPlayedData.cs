using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerRecentlyPlayedData : ICloneable, IEquatable<PlayerRecentlyPlayedData>
    {
        [JsonProperty("id")]
        public string? id;

        [JsonProperty("playerName")]
        public string? playerName;

        [JsonProperty("playedTime")]
        public long? playedTime;

        public PlayerRecentlyPlayedData()
        {
        }

        public PlayerRecentlyPlayedData(string? id, string? playerName, long? playedTime)
        {
            this.id = id;
            this.playerName = playerName;
            this.playedTime = playedTime;
        }

        public object Clone() =>
            new PlayerRecentlyPlayedData(id, playerName, playedTime);

        public bool Equals(PlayerRecentlyPlayedData? other)
        {
            if (other == null)
                return false;

            return id == other.id && playerName == other.playerName;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerRecentlyPlayedData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(id);
            hash.Add(playerName);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerRecentlyPlayedData? left, PlayerRecentlyPlayedData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerRecentlyPlayedData? left, PlayerRecentlyPlayedData? right)
        {
            return !(left == right);
        }
    }
}
