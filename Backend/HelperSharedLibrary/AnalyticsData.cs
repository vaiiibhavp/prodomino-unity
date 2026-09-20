using System;
using System.Collections.Generic;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class AnalyticsData
    {
        public long[]? dailyBonusReclaimedDates;

        public uint tilesPlaced; 
        public uint friendsAdded; 
        public int cosmeticsObtainedCount;

        public int achievementsClaimedCount;
        public int missionsClaimedCount;
        public int tutorialsCompleted;

        // Casual game statistics
        public int casualVictoriesCount;
        public int casualDefeatsCount;
        public int CasualGamesCount => casualVictoriesCount + casualDefeatsCount;
        public double CasualWLRatio
        {
            get 
            {
                var totalCasualGames = casualVictoriesCount + casualDefeatsCount;
                return (totalCasualGames > 0) ? (float)casualVictoriesCount / totalCasualGames : 0f;
            }
        }

        // Competitive game statistics
        public int competitiveVictoriesCount;

        public int competitiveDefeatsCount;
        public int competitive2ndPositionCount;
        public int competitive3rdPositionCount;
        public int competitive4thPositionCount;

        public int competitiveVictoryLongestStreak;
        public int competitiveDefeatLongestStreak;


        public DateTime lastLoginDate;
        public DateTime lastClubMoveDate;

        public int daysStreaked;
        public long totalMatchTimeTicks;

        public int CompetitiveGamesCount => competitiveVictoriesCount + competitiveDefeatsCount;
        public double CompetitiveWLRatio
        {
            get
            {
                var totalCompetitiveGames = competitiveVictoriesCount + competitiveDefeatsCount;
                return (totalCompetitiveGames > 0) ? (float)competitiveVictoriesCount / totalCompetitiveGames : 0f;
            }
        }

        public int TotalMatches => CasualGamesCount + CompetitiveGamesCount;

        public List<LeaderboardRecord> leaderboardRecords;

        public AnalyticsData()
        {
            dailyBonusReclaimedDates = null;

            tilesPlaced = 0;
            friendsAdded = 0;

            cosmeticsObtainedCount = 0;
            achievementsClaimedCount = 0;
            missionsClaimedCount = 0;
            tutorialsCompleted = 0;
            casualVictoriesCount = 0;
            casualDefeatsCount = 0;

            competitiveVictoriesCount = 0;

            competitiveDefeatsCount = 0;
            competitive2ndPositionCount = 0;
            competitive3rdPositionCount = 0;
            competitive4thPositionCount = 0;

            competitiveVictoryLongestStreak = 0;
            competitiveDefeatsCount = 0;

            lastLoginDate = DateTime.MinValue;
            lastClubMoveDate = DateTime.MinValue;

            totalMatchTimeTicks = 0;

            leaderboardRecords = new List<LeaderboardRecord>();
        }

        [Serializable]
        public class LeaderboardRecord
        {
            public string? leaderboardId;

            public LeaderboardTier higherTier;
            public double higherScore;

            public LeaderboardRecord() { }

            public LeaderboardRecord(string? leaderboardId)
            {
                this.leaderboardId = leaderboardId;
                higherScore = 0;
                higherTier = LeaderboardTier.None;
            }
        }
    }
}
