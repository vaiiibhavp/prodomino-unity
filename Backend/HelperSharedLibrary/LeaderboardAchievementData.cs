using System;

namespace HelperSharedLibrary
{
    [Serializable]
    public class LeaderboardAchievementData : ICloneable, IEquatable<LeaderboardAchievementData>
    {
        public string[] badgedIDs;
        public uint completedAchievements;
        public DateTime timeToUpdate;

        public LeaderboardAchievementData()
        {
            badgedIDs = Array.Empty<string>();
            completedAchievements = 0;
            timeToUpdate = DateTime.MinValue;
        }

        public LeaderboardAchievementData(DateTime timeToUpdate, string[] badgedIDs, uint completedAchievements)
        {
            this.timeToUpdate = timeToUpdate;
            this.badgedIDs = badgedIDs;
            this.completedAchievements = completedAchievements;
        }

        public object Clone() =>
            new LeaderboardAchievementData(timeToUpdate, badgedIDs, completedAchievements);

        public bool Equals(LeaderboardAchievementData? other)
        {
            if (other == null)
                return false;

            return timeToUpdate == other.timeToUpdate
                && completedAchievements == other.completedAchievements
                && badgedIDs.Length == other.badgedIDs.Length
                && System.Linq.Enumerable.SequenceEqual(badgedIDs, other.badgedIDs);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as LeaderboardAchievementData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(completedAchievements);
            foreach (var badge in badgedIDs)
                hash.Add(badge);
            return hash.ToHashCode();
        }

        public static bool operator ==(LeaderboardAchievementData? left, LeaderboardAchievementData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(LeaderboardAchievementData? left, LeaderboardAchievementData? right)
        {
            return !(left == right);
        }
    }
}
