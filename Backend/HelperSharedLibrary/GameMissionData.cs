using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class GameMissionData : ICloneable, IEquatable<GameMissionData>
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("goalAmount")]
        public int goalAmount;

        [JsonProperty("analyticType")]
        public AnalyticType analyticType;

        [JsonProperty("rewardCurrency")]
        public Currency rewardCurrency;

        [JsonProperty("rewardAmount")]
        public uint rewardAmount;

        public GameMissionData() 
        {
            id = string.Empty;
            name = string.Empty;
            goalAmount = 0;
            analyticType = AnalyticType.None;
            rewardCurrency = Currency.None;
            rewardAmount = 0;
        }

        public GameMissionData(string id, string name, int goalAmount, AnalyticType analyticType, Currency rewardCurrency, uint rewardAmount)
        {
            this.id = id;
            this.name = name;
            this.goalAmount = goalAmount;
            this.analyticType = analyticType;
            this.rewardCurrency = rewardCurrency;
            this.rewardAmount = rewardAmount;
        }

        public bool Equals(GameMissionData? other)
        {
            if (other == null) return false;

            return id == other.id &&
                   name == other.name &&
                   goalAmount == other.goalAmount &&
                   analyticType == other.analyticType &&
                   rewardCurrency == other.rewardCurrency &&
                   rewardAmount == other.rewardAmount;
        }

        public object Clone() =>
            new GameMissionData(id, name, goalAmount, analyticType, rewardCurrency, rewardAmount);

        public override bool Equals(object? obj)
        {
            return Equals(obj as GameMissionData);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(id, name, goalAmount, analyticType, rewardCurrency, rewardAmount);
        }

        public static bool operator ==(GameMissionData? left, GameMissionData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(GameMissionData? left, GameMissionData? right)
        {
            return !(left == right);
        }
    }
}
