using Newtonsoft.Json;
using System;

namespace HelperSharedLibrary
{
    [Serializable]
    public class PlayerProfileData : ICloneable, IEquatable<PlayerProfileData>
    {
        [JsonProperty("profileIconID")]
        public string? profileIconID; // If null, the default (or provider) icon is used

        [JsonProperty("tileSkinID")]
        public string? tileSkinID; // If null, the default tile skin is used

        [JsonProperty("boardSkinID")]
        public string? boardSkinID; // If null, the default board skin is used

        [JsonProperty("boardFundSkinID")]
        public string? boardFundSkinID; // If null, the default board fund skin is used

        [JsonProperty("badgesIDs")]
        public string[]? badgesIDs;

        public PlayerProfileData()
        {
        }

        public PlayerProfileData(string? profileIconID, string? tileSkinID, string? boardSkinID, string? boardFundSkinID, string[]? badgesIDs)
        {
            this.profileIconID = profileIconID;
            this.tileSkinID = tileSkinID;
            this.boardSkinID = boardSkinID;
            this.boardFundSkinID = boardFundSkinID;
            this.badgesIDs = badgesIDs;
        }

        public object Clone() =>
            new PlayerProfileData(profileIconID, tileSkinID, boardSkinID, boardFundSkinID, badgesIDs);

        public bool Equals(PlayerProfileData? other)
        {
            if (other == null)
                return false;

            return  profileIconID == other.profileIconID &&
                    tileSkinID == other.tileSkinID &&
                    boardSkinID == other.boardSkinID &&
                    boardFundSkinID == other.boardFundSkinID &&
                    badgesIDs == other.badgesIDs;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PlayerProfileData);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(profileIconID);
            hash.Add(tileSkinID);
            hash.Add(boardSkinID);
            hash.Add(boardFundSkinID);
            hash.Add(badgesIDs);
            return hash.ToHashCode();
        }


        public static bool operator ==(PlayerProfileData? left, PlayerProfileData? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(PlayerProfileData? left, PlayerProfileData? right)
        {
            return !(left == right);
        }
    }
}
