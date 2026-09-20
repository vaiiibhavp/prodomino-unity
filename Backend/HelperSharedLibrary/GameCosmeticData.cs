using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class GameCosmeticData : ICloneable, IEquatable<GameCosmeticData>
    {
        [JsonProperty("id")]
        public string? id;

        [JsonProperty("name")]
        public string? name;

        [JsonProperty("description")]
        public string? description;

        [JsonProperty("type")]
        public CosmeticType type;

        [JsonProperty("rarity")]
        public CosmeticRarity rarity;

        [JsonProperty("isAvailable")]
        public bool isAvailable;

        [JsonProperty("price")]
        public uint price;

        [JsonProperty("currency")]
        public Currency currency;

        public GameCosmeticData()
        {
        }

        public GameCosmeticData(string? id, string? name, string? description, CosmeticType type, CosmeticRarity rarity, bool isAvailable, uint price, Currency currency)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.type = type;
            this.rarity = rarity;
            this.isAvailable = isAvailable;
            this.price = price;
            this.currency = currency;
        }

        public object Clone() =>
            new GameCosmeticData(id, name, description, type, rarity, isAvailable, price, currency);

        public bool Equals(GameCosmeticData? other)
        {
            if (other == null)
                return false;

            return id == other.id &&
                name == other.name &&
                description == other.description &&
                type == other.type &&
                rarity == other.rarity &&
                isAvailable == other.isAvailable &&
                price == other.price &&
                currency == other.currency;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as GameCosmeticData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(id);
            hash.Add(name);
            hash.Add(description);
            hash.Add(type);
            hash.Add(rarity);
            hash.Add(isAvailable);
            hash.Add(price);
            hash.Add(currency);
            return hash.ToHashCode();
        }


        public static bool operator ==(GameCosmeticData? left, GameCosmeticData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(GameCosmeticData? left, GameCosmeticData? right)
        {
            return !(left == right);
        }
    }
}
