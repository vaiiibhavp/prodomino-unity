using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    // Local class to store win/lose streak from Cloud Save
    [Serializable]
    public class PlayerMatchData : ICloneable, IEquatable<PlayerMatchData>
    {
        [JsonProperty("elo")]
        public int elo;

        [JsonProperty("winStreak")]
        public int winStreak;

        [JsonProperty("loseStreak")]
        public int loseStreak;
        
        [JsonProperty("Streak")]
        public int Streak => winStreak - loseStreak;


        public PlayerMatchData()
        {
        }

        public PlayerMatchData(int elo, int winStreak, int loseStreak)
        {
            this.elo = elo;
            this.winStreak = winStreak;
            this.loseStreak = loseStreak;
        }

        public object Clone() =>
            new PlayerMatchData(elo, winStreak, loseStreak);

        public bool Equals(PlayerMatchData? other)
        {
            if (other == null)
                return false;

            return elo == other.elo &&
                   winStreak == other.winStreak &&
                   loseStreak == other.loseStreak;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerMatchData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(elo);
            hash.Add(winStreak);
            hash.Add(loseStreak);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerMatchData? left, PlayerMatchData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerMatchData? left, PlayerMatchData? right)
        {
            return !(left == right);
        }
    }
}
