using Newtonsoft.Json;
using ProDomino.Shared;
using System;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// Don't change names because its link with the 'firebaseDatabase' .jslib could be broken
    /// </summary>
    [Serializable]
    public class GlobalAnalyticsData
    {
        public int gamesPlayedToday;
        public int usersPlayingNow;
        public int french;
        public int block;
        public int draw;
        public int five;
        public int concentrate;
        public string lastUpdatedDate;
    }
}
