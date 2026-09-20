using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerCosmeticData : ICloneable, IEquatable<PlayerCosmeticData>
    {
        [JsonProperty("id")]
        public string? id;

        [JsonProperty("adquiredTime")]
        public long acquiredTime; // Unix timestamp UTC

        [JsonProperty("ammoutPaid")]
        public uint ammoutPaid;

        [JsonProperty("costType")]
        public Currency costType;

        [JsonProperty("cosmeticPurchaseMethod")]
        public CosmeticPurchaseMethod? cosmeticPurchaseMethod;

        public PlayerCosmeticData()
        {
        }

        public PlayerCosmeticData(string? id, long adquiredTime, uint ammoutPaid, Currency costType, CosmeticPurchaseMethod? cosmeticPurchaseMethod)
        {
            this.id = id;
            this.acquiredTime = adquiredTime;
            this.ammoutPaid = ammoutPaid;
            this.costType = costType;
            this.cosmeticPurchaseMethod = cosmeticPurchaseMethod;
        }

        public object Clone() =>
            new PlayerCosmeticData(id, acquiredTime, ammoutPaid, costType, cosmeticPurchaseMethod);

        public bool Equals(PlayerCosmeticData? other)
        {
            if (other == null)
                return false;

            return id == other.id &&
                acquiredTime == other.acquiredTime &&
                ammoutPaid == other.ammoutPaid &&
                costType == other.costType &&
                cosmeticPurchaseMethod == other.cosmeticPurchaseMethod;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerCosmeticData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(id);
            hash.Add(acquiredTime);
            hash.Add(ammoutPaid);
            hash.Add(costType);
            hash.Add(cosmeticPurchaseMethod);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerCosmeticData? left, PlayerCosmeticData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerCosmeticData? left, PlayerCosmeticData? right)
        {
            return !(left == right);
        }
    }
}
