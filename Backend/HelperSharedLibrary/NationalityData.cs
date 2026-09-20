using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    [Serializable]
    public class NationalityData : ICloneable, IEquatable<NationalityData>
    {
        [JsonProperty("nationality")]
        public NationalityType nationalityType;

        /// <summary>
        /// UTC timestamp (ISO 8601) when the nationality was last updated.
        /// </summary>
        [JsonProperty("updateAtUtc")]
        public string? updateAtUtc;

        public NationalityData()
        {
        }

        public NationalityData(NationalityType nationalityType, string? updateAtUtc)
        {
            this.nationalityType = nationalityType;
            this.updateAtUtc = updateAtUtc;
        }

        public object Clone() =>
            new NationalityData(nationalityType, updateAtUtc);

        public bool Equals(NationalityData? other)
        {
            if (other == null)
                return false;

            return nationalityType == other.nationalityType &&
                   updateAtUtc == other.updateAtUtc;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NationalityData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(nationalityType);
            hash.Add(updateAtUtc);
            return hash.ToHashCode();
        }


        public static bool operator ==(NationalityData? left, NationalityData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(NationalityData? left, NationalityData? right)
        {
            return !(left == right);
        }
    }
}
