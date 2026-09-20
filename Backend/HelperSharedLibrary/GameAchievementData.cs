using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class GameAchievementData : ICloneable, IEquatable<GameAchievementData>
    {
        [JsonProperty("achievement")]
        public Achievement achievement; 
        
        [JsonProperty("achievementType")]
        public AchievementType achievementType;

        [JsonProperty("name")]
        public string name;
        
        [JsonProperty("description")]
        public string description;
        
        [JsonProperty("goalAmount")]
        public int goalAmount;
        
        [JsonProperty("points")]
        public long points;

        [JsonProperty("analyticType")]
        public AnalyticType analyticType;

        [JsonProperty("achievementRank")]
        public AchievementRank achievementRank;
        
        [JsonProperty("gameModeFilter")]
        public GameModeFilter? gameModeFilter;

        [JsonProperty("leaderboardTier")]
        public LeaderboardTier? leaderboardTier;

        [JsonProperty("rewardCosmetics")]
        public string[] rewardCosmeticsIDs;

        public GameAchievementData() 
        {
            achievement = Achievement.None;
            achievementType = AchievementType.None;
            name = string.Empty;
            description = string.Empty;
            goalAmount = 0;
            analyticType = AnalyticType.None;
            achievementRank = AchievementRank.None;
            gameModeFilter = null;
            leaderboardTier = null;
            rewardCosmeticsIDs = new string[0];
        }

        public GameAchievementData(Achievement achievement, AchievementType achievementType, string name, string description, int goalAmount, long points, AnalyticType analyticType, AchievementRank achievementRank, GameModeFilter? gameModeFilter, LeaderboardTier? leaderboardTier, string[] rewardCosmeticsIDs)
        {
            this.achievement = achievement;
            this.achievementType = achievementType;
            this.name = name;
            this.description = description;
            this.goalAmount = goalAmount;
            this.points = points;
            this.analyticType = analyticType;
            this.achievementRank = achievementRank;
            this.gameModeFilter = gameModeFilter;
            this.leaderboardTier = leaderboardTier;
            this.rewardCosmeticsIDs = rewardCosmeticsIDs;
        }

        public bool Equals(GameAchievementData? other)
        {
            if (other == null) return false;

            return achievement == other.achievement &&
                   achievementType == other.achievementType &&
                   name == other.name &&
                   description == other.description &&
                   goalAmount == other.goalAmount &&
                   points == other.points &&
                   analyticType == other.analyticType &&
                   achievementRank == other.achievementRank &&
                   gameModeFilter == other.gameModeFilter &&
                   leaderboardTier == other.leaderboardTier &&
                   rewardCosmeticsIDs == other.rewardCosmeticsIDs;
        }

        public object Clone() =>
            new GameAchievementData(achievement, achievementType, name, description, goalAmount, points, analyticType, achievementRank, gameModeFilter, leaderboardTier, rewardCosmeticsIDs);

        public override bool Equals(object? obj)
        {
            return Equals(obj as GameAchievementData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(achievement); 
            hash.Add(achievementType); 
            hash.Add(name); 
            hash.Add(goalAmount); 
            hash.Add(points); 
            hash.Add(analyticType); 
            hash.Add(achievementRank); 
            hash.Add(gameModeFilter); 
            hash.Add(leaderboardTier); 
            hash.Add(rewardCosmeticsIDs); 
            hash.Add(achievement);
            return hash.ToHashCode();
        }

        public static bool operator ==(GameAchievementData? left, GameAchievementData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(GameAchievementData? left, GameAchievementData? right)
        {
            return !(left == right);
        }
    }
}
