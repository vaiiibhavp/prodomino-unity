using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Detailed player data for a leaderboard entry, including match data, analytics, and achievements.
    /// </summary>
    [Serializable]
    public class PlayerLeaderboardData : ICloneable, IEquatable<PlayerLeaderboardData>
    {
        [JsonProperty("playerId")] public string playerId;
        [JsonProperty("playerNationality")] public NationalityType playerNationality;
        [JsonProperty("leaderboardInfos")] public List<LeaderboardInfo>? leaderboardInfos;
        [JsonProperty("playerProfileData")] public PlayerProfileData? playerProfileData;
        [JsonProperty("playerMatchData")] public PlayerMatchData? playerMatchData;
        [JsonProperty("analyticsData")] public AnalyticsData? analyticsData;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// Marked with JsonConstructor and Preserve for WebGL/IL2CPP compatibility.
        /// </summary>
        [JsonConstructor]
        public PlayerLeaderboardData()
        {
            playerId = string.Empty;
            playerNationality = default;
            leaderboardInfos = new();
            playerProfileData = default;
            playerMatchData = default;
            analyticsData = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// Also preserved to avoid IL2CPP stripping.
        /// </summary>
        public PlayerLeaderboardData
            (string playerId,
            NationalityType playerNationality,
            PlayerProfileData? playerProfileData,
            PlayerMatchData? playerMatchData,
            AnalyticsData? analyticsData,
            params LeaderboardInfo[]? leaderboardInfos)
        {
            this.playerId = playerId;
            this.playerNationality = playerNationality;
            this.leaderboardInfos = leaderboardInfos is not null and { Length: > 0 } ? leaderboardInfos.ToList() : new();
            this.playerProfileData = playerProfileData;
            this.playerMatchData = playerMatchData;
            this.analyticsData = analyticsData;
        }
        
        
        /// <summary>
        /// Full constructor for explicit initialization.
        /// Also preserved to avoid IL2CPP stripping.
        /// </summary>
        public PlayerLeaderboardData
            (string playerId,
            NationalityType playerNationality,
            PlayerProfileData? playerProfileData,
            PlayerMatchData? playerMatchData,
            AnalyticsData? analyticsData,
            List<LeaderboardInfo>? leaderboardInfos)
        {
            this.playerId = playerId;
            this.playerNationality = playerNationality;
            this.leaderboardInfos = leaderboardInfos is not null and { Count: > 0 } ? new(leaderboardInfos) : new();
            this.playerProfileData = playerProfileData;
            this.playerMatchData = playerMatchData;
            this.analyticsData = analyticsData;
        }

        public object Clone() =>
            new PlayerLeaderboardData(playerId, playerNationality, playerProfileData, playerMatchData, analyticsData, leaderboardInfos);

        public bool Equals(PlayerLeaderboardData? other)
        {
            if (other == null)
                return false;

            return playerId == other.playerId 
                && playerNationality == other.playerNationality
                && leaderboardInfos == other.leaderboardInfos
                && playerProfileData == other.playerProfileData
                && playerMatchData == other.playerMatchData 
                && analyticsData == other.analyticsData;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerLeaderboardData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(playerId);
            hash.Add(playerNationality);
            hash.Add(leaderboardInfos);
            hash.Add(playerProfileData);
            hash.Add(playerMatchData);
            hash.Add(analyticsData);
            return hash.ToHashCode();
        }

        public static bool operator ==(PlayerLeaderboardData? left, PlayerLeaderboardData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerLeaderboardData? left, PlayerLeaderboardData? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Represents information about a leaderboard, including its identifier and associated leaderboard data.
        /// </summary>
        [Serializable]
        public class LeaderboardInfo
        { 
            [JsonProperty("leaderboardId")] public string leaderboardId;
            [JsonProperty("leaderboardData")] public LeaderboardData? leaderboardData;

            [JsonConstructor()]
            public LeaderboardInfo()
            {
                leaderboardId = string.Empty;
                leaderboardData = default;
            }

            public LeaderboardInfo(string leaderboardId, LeaderboardData? leaderboardData)
            {
                this.leaderboardId = leaderboardId;
                this.leaderboardData = leaderboardData;
            }
        }
    }
}