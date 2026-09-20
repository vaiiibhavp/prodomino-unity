using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerMissionData : ICloneable, IEquatable<PlayerMissionData>
    {
        [JsonProperty("id")]
        public string? id;

        [JsonProperty("isDaily")]
        public bool isDaily;
        
        [JsonProperty("isBonus")]
        public bool isBonus;

        [JsonProperty("progress")]
        public uint progress;

        [JsonProperty("completed")]
        public bool completed;

        [JsonProperty("claimed")]
        public bool claimed;

        [JsonProperty("lastProgressUpdateTime")]
        public long lastProgressUpdateTime;

        [JsonProperty("startTime")]
        public long startTime; // Unix timestamp UTC

        [JsonProperty("completedTime")]
        public long? completedTime;

        [JsonProperty("reclaimedTime")]
        public long? reclaimedTime;

        public PlayerMissionData()
        {
        }

        public PlayerMissionData(string? id, bool isDaily, bool isBonus, long startTime)
        {
            this.id = id;
            this.isDaily = isDaily;
            this.isBonus = isBonus;
            this.lastProgressUpdateTime = startTime;
            this.startTime = startTime;
        }
        
        public PlayerMissionData(string? id, bool isDaily, bool isBonus, uint progress, bool completed, bool claimed, long lastProgressUpdateTime, long startTime, long? completedTime, long? reclaimedTime)
        {
            this.id = id;
            this.isDaily = isDaily;
            this.isBonus = isBonus;
            this.progress = progress;
            this.completed = completed;
            this.claimed = claimed;
            this.lastProgressUpdateTime = lastProgressUpdateTime;
            this.startTime = startTime;
            this.completedTime = completedTime;
            this.reclaimedTime = reclaimedTime;
        }

        public object Clone() =>
            new PlayerMissionData(id, isDaily, isBonus, progress, completed, claimed, lastProgressUpdateTime, startTime, completedTime, reclaimedTime);

        public bool Equals(PlayerMissionData? other)
        {
            if (other == null)
                return false;

            return id == other.id &&
                   isDaily == other.isDaily &&
                   progress == other.progress &&
                   completed == other.completed &&
                   claimed == other.claimed &&
                   lastProgressUpdateTime == other.lastProgressUpdateTime &&
                   startTime == other.startTime &&
                   completedTime == other.completedTime &&
                   reclaimedTime == other.reclaimedTime;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerMissionData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(id);
            hash.Add(isDaily);
            hash.Add(progress);
            hash.Add(completed);
            hash.Add(claimed);
            hash.Add(lastProgressUpdateTime);
            hash.Add(startTime);
            hash.Add(completedTime);
            hash.Add(reclaimedTime);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerMissionData? left, PlayerMissionData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerMissionData? left, PlayerMissionData? right)
        {
            return !(left == right);
        }
    }
}
