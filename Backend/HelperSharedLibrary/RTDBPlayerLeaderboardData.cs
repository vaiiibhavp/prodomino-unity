using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Data structure representing a player's purchases data.
    /// </summary>
    [Serializable]
    public class RTDBPlayerLeaderboardData
    {
        [JsonProperty("userId")]
        public string? userId;
        
        [JsonProperty("username")]
        public string? username;

        [JsonProperty("score")]
        public int score;

        [JsonProperty("updatedAt")]
        public long updatedAt;

        [JsonConstructor]
        public RTDBPlayerLeaderboardData()
        {
        }

        public RTDBPlayerLeaderboardData(string userId, string username, int score, long updatedAt)
        {
            this.userId = userId;
            this.username = username;
            this.score = score;
            this.updatedAt = updatedAt;
        }
    }
}