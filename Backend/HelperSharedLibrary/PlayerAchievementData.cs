using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerAchievementData : ICloneable, IEquatable<PlayerAchievementData>
    {
        [JsonProperty("achievement")]
        public Achievement achievement;

        [JsonProperty("progress")]
        public uint progress;

        [JsonProperty("completed")]
        public bool completed;

        [JsonProperty("claimed")]
        public bool claimed;

        [JsonProperty("lastProgressUpdateTime")]
        public long lastProgressUpdateTime;

        [JsonProperty("completedTime")]
        public long? completedTime;

        [JsonProperty("reclaimedTime")]
        public long? reclaimedTime;

        public PlayerAchievementData()
        {
        }

        public PlayerAchievementData(Achievement achievement)
        {
            this.achievement = achievement;
        }
        
        public PlayerAchievementData(Achievement achievement, uint progress, bool completed, bool claimed, long lastProgressUpdateTime, long? completedTime, long? reclaimedTime)
        {
            this.achievement = achievement;
            this.progress = progress;
            this.completed = completed;
            this.claimed = claimed;
            this.lastProgressUpdateTime = lastProgressUpdateTime;
            this.completedTime = completedTime;
            this.reclaimedTime = reclaimedTime;
        }

        public object Clone() =>
            new PlayerAchievementData(achievement, progress, completed, claimed, lastProgressUpdateTime, completedTime, reclaimedTime);

        public bool Equals(PlayerAchievementData? other)
        {
            if (other == null)
                return false;

            return achievement == other.achievement &&
                   completed == other.completed &&
                   claimed == other.claimed &&
                   completedTime == other.completedTime &&
                   reclaimedTime == other.reclaimedTime;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerAchievementData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(achievement);
            hash.Add(completed);
            hash.Add(claimed);
            hash.Add(completedTime);
            hash.Add(reclaimedTime);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerAchievementData? left, PlayerAchievementData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerAchievementData? left, PlayerAchievementData? right)
        {
            return !(left == right);
        }
    }
}
