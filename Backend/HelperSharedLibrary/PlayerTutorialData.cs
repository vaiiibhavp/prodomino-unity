using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerTutorialData : ICloneable, IEquatable<PlayerTutorialData>
    {
        [JsonProperty("relatedGameMode")]
        public GameModeFilter? relatedGameMode;

        [JsonProperty("steps")]
        public PlayerDataTutorialStep[] steps;

        public PlayerTutorialData()
        {
            relatedGameMode = null;
            steps = Array.Empty<PlayerDataTutorialStep>();
        }

        public PlayerTutorialData(GameModeFilter? relatedGameMode, PlayerDataTutorialStep[] steps)
        {
            this.relatedGameMode = relatedGameMode;
            this.steps = steps;
        }

        public object Clone() =>
            new PlayerTutorialData(relatedGameMode, steps);

        public bool Equals(PlayerTutorialData? other)
        {
            if (other == null)
                return false;

            return relatedGameMode == other.relatedGameMode
                && steps.Length == other.steps.Length
                && System.Linq.Enumerable.SequenceEqual(steps, other.steps);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerTutorialData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(relatedGameMode);
            foreach (var step in steps)
                hash.Add(step);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerTutorialData? left, PlayerTutorialData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerTutorialData? left, PlayerTutorialData? right)
        {
            return !(left == right);
        }

        [Serializable]
        public class PlayerDataTutorialStep
        {
            [JsonProperty("id")]
            public string? id;

            [JsonProperty("completedTime")]
            public long completedTime; // Unix timestamp UTC
        }
    }
}
