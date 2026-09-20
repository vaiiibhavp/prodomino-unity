using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class GameTutorialData : ICloneable, IEquatable<GameTutorialData>
    {
        [JsonProperty("relatedGameMode")]
        public GameModeFilter? relatedGameMode;

        [JsonProperty("steps")]
        public string[]? steps;

        public GameTutorialData()
        {
        }

        public GameTutorialData(GameModeFilter? relatedGameMode, string[]? steps)
        {
            this.relatedGameMode = relatedGameMode;
            this.steps = steps;
        }

        public object Clone() =>
            new GameTutorialData(relatedGameMode, steps);

        public bool Equals(GameTutorialData? other)
        {
            if (other == null)
                return false;

            return relatedGameMode == other.relatedGameMode
                && steps?.Length == other.steps?.Length
                && System.Linq.Enumerable.SequenceEqual(steps, other.steps);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as GameTutorialData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(relatedGameMode);
            foreach (var step in steps)
                hash.Add(step);
            return hash.ToHashCode();
        }


        public static bool operator ==(GameTutorialData? left, GameTutorialData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(GameTutorialData? left, GameTutorialData? right)
        {
            return !(left == right);
        }
    }
}
