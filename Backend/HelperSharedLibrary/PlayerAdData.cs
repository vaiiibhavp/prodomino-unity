using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Data structure representing a player's ad-related data.<br></br>
    /// Note: some fields are commented out as they are handled via Firebase real-time database.<br></br>
    /// This data only manages ad display timings and related information.
    /// </summary>
    [Serializable]
    public class PlayerAdData : ICloneable, IEquatable<PlayerAdData>
    {
        /// <summary>
        /// Note: this information is handled via firebase real-time database,<br></br>
        /// This is because the payment handler (paypal) requires direct communication with firebase to<br></br>
        /// </summary>
        //[JsonProperty("subscriptionPaidDate")]
        //public DateTime? subscriptionPaidDate;

        [JsonProperty("lastTimeAdDisabled")]
        public DateTime? lastTimeAdDisabled; // Indicates the last time ads were disabled temporarily

        [JsonProperty("lastTimeAdSaw")]
        public DateTime? lastTimeAdSaw; // Indicates the last time the player saw an ad

        public PlayerAdData()
        {
        }

        public PlayerAdData(/*DateTime? subscriptionPaidDate, */DateTime? lastTimeAdDisabled, DateTime? lastTimeAdSaw)
        {
            //this.subscriptionPaidDate = subscriptionPaidDate;
            this.lastTimeAdDisabled = lastTimeAdDisabled;
            this.lastTimeAdSaw = lastTimeAdSaw;
        }

        public object Clone() =>
            new PlayerAdData(/*subscriptionPaidDate, */lastTimeAdDisabled, lastTimeAdSaw);

        public bool Equals(PlayerAdData? other)
        {
            if (other == null)
                return false;

            return /*subscriptionPaidDate == other.subscriptionPaidDate &&*/
                   lastTimeAdDisabled == other.lastTimeAdDisabled &&
                     lastTimeAdSaw == other.lastTimeAdSaw;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerAdData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            //hash.Add(subscriptionPaidDate);
            hash.Add(lastTimeAdDisabled);
            hash.Add(lastTimeAdSaw);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerAdData? left, PlayerAdData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerAdData? left, PlayerAdData? right)
        {
            return !(left == right);
        }
    }
}
