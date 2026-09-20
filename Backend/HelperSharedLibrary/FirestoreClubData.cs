using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using static HelperSharedLibrary.Enums;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Represents the Firestore document data for a club entry in a leaderboard.<br></br><br></br>
    /// DON'T MODIFY THIS CLASS WITHOUT UPDATING THE ParseClubData AND EACH POSSIBLE USAGE OF IT.
    /// </summary>
    public class FirestoreClubData
    {
        [JsonProperty("clubName")]
        public string clubName;

        [JsonProperty("normalizedName")]
        public string normalizedName;

        [JsonProperty("score")] 
        public int score;

        [JsonProperty("iconData")]
        public IconData? iconData;
        
        [JsonProperty("slogan")]
        public string slogan;

        [JsonProperty("members")] 
        public List<MemberData> members;
        
        [JsonProperty("applicants")] 
        public List<ApplicantData> applicants;

        [JsonProperty("clubRank")]
        public int clubRank;

        [JsonProperty("updatedAt")]
        [JsonConverter(typeof(FirestoreTimestampConverter))]
        public DateTime updatedAt;
        
        [JsonProperty("creationDate")]
        [JsonConverter(typeof(FirestoreTimestampConverter))]
        public DateTime creationDate;

        [JsonProperty("searchTokens")]
        public string[] searchTokens;

        /// <summary>
        /// Parameterless constructor with safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirestoreClubData()
        {
            clubName = string.Empty;
            normalizedName = string.Empty;
            score = 0;
            iconData = new();
            slogan = string.Empty;
            members = new();
            applicants = new();
            updatedAt = DateTime.MinValue;
            creationDate = DateTime.UtcNow;
            searchTokens = Array.Empty<string>();
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirestoreClubData(string clubName, string normalizedName, int score, IconData? iconData, string slogan, List<MemberData>? members, int clubRank, List<ApplicantData>? applicants = default, string[]? searchTokens = default)
        {
            this.score = score;
            this.clubName = !string.IsNullOrEmpty(clubName) ? clubName : string.Empty;
            this.normalizedName = !string.IsNullOrEmpty(normalizedName) ? normalizedName : string.Empty;
            this.iconData = iconData ?? new();
            this.slogan = !string.IsNullOrEmpty(slogan) ? slogan : string.Empty;
            this.members = members ?? new();
            this.clubRank = clubRank;
            this.applicants = applicants ?? new();
            updatedAt = DateTime.MinValue;
            creationDate = DateTime.UtcNow;
            this.searchTokens = searchTokens ?? Array.Empty<string>();
        }

        /// <summary>
        /// Parse Firestore JSON (raw string from REST API) into a FirestorePlayerClubData object.<br>
        /// Handles Firestore wrappers: stringValue, integerValue, arrayValue, mapValue.<br></br><br></br>
        /// If the data of <see cref="FirestoreClubData"/> is modified, it must be parsed again (manually) from the Firestore JSON format.
        /// </summary>
        public static FirestoreClubData? ParseClubData(string firestoreJson)
        {
            if (string.IsNullOrWhiteSpace(firestoreJson))
                return null;

            JObject raw;
            try
            {
                raw = JObject.Parse(firestoreJson);
            }
            catch
            {
                return null;
            }

            var fields = raw["fields"];
            if (fields == null)
                return null;

            var club = new FirestoreClubData
            {
                // Parse simple fields
                clubName = fields["clubName"]?["stringValue"]?.ToString() ?? string.Empty,
                normalizedName = fields["normalizedName"]?["stringValue"]?.ToString() ?? string.Empty,
                score = int.TryParse(fields["score"]?["integerValue"]?.ToString(), out var score) ? score : 0,
                slogan = fields["slogan"]?["stringValue"]?.ToString() ?? string.Empty,
                clubRank = int.TryParse(fields["clubRank"]?["integerValue"]?.ToString(), out var clubRank) ? clubRank : 0,
                updatedAt = DateTime.TryParse(fields["updatedAt"]?["timestampValue"]?.ToString(), out var updateAt)
                    ? updateAt
                    : DateTime.MinValue,
                creationDate = DateTime.TryParse(fields["creationDate"]?["timestampValue"]?.ToString(), out var creationDate)
                    ? creationDate
                    : DateTime.UtcNow,
            };

            // Parse iconData
            var iconFields = fields["iconData"]?["mapValue"]?["fields"];
            if (iconFields != null)
            {
                club.iconData = new IconData
                {
                    shieldId = iconFields["shieldId"]?["stringValue"]?.ToString(),
                    textureId = iconFields["textureId"]?["stringValue"]?.ToString(),
                    centralImageId = iconFields["centralImageId"]?["stringValue"]?.ToString(),
                    shieldColorId = iconFields["shieldColorId"]?["stringValue"]?.ToString(),
                    textureColorId = iconFields["textureColorId"]?["stringValue"]?.ToString(),
                    centralImageColorId = iconFields["centralImageColorId"]?["stringValue"]?.ToString(),
                    backgroundColorId = iconFields["backgroundColorId"]?["stringValue"]?.ToString()
                };
            }

            // Parse members
            var membersToken = fields["members"]?["arrayValue"]?["values"];
            if (membersToken != null)
            {
                foreach (var memberToken in membersToken)
                {
                    var field = memberToken["mapValue"]?["fields"];
                    if (field == null)
                        continue;

                    var member = new MemberData
                    {
                        firebaseMemberId = field["firebaseMemberId"]?["stringValue"]?.ToString() ?? string.Empty,
                        unityMemberId = field["unityMemberId"]?["stringValue"]?.ToString() ?? string.Empty,
                        profileIconId = field["profileIconId"]?["stringValue"]?.ToString() ?? string.Empty,
                        memberName = field["memberName"]?["stringValue"]?.ToString() ?? string.Empty,
                        victories = int.TryParse(field["victories"]?["integerValue"]?.ToString(), out var victories) ? victories : 0,
                        joinedDate = long.TryParse(field["joinedDate"]?["integerValue"]?.ToString(), out var joinedDate) ? joinedDate : 0,
                        totalAchievements = int.TryParse(field["totalAchievements"]?["integerValue"]?.ToString(), out var totalAchievements) ? totalAchievements : 0,
                        rank = Enum.TryParse(field["rank"]?["integerValue"]?.ToString(), out ClubRanksTypes rank)
                            ? rank
                            : ClubRanksTypes.Member
                    };

                    // Parse badges array
                    var badgesToken = field["badges"]?["arrayValue"]?["values"];
                    if (badgesToken != null)
                    {
                        member.badges = badgesToken
                            .Select(b => b?["stringValue"]?.ToString() ?? string.Empty)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToArray();
                    } else
                    {
                        member.badges = Array.Empty<string>();
                    }

                    club.members.Add(member);
                }
            }

            // Parse applicants
            var applicantsToken = fields["applicants"]?["arrayValue"]?["values"];
            if (applicantsToken != null)
            {
                foreach (var applicantToken in applicantsToken)
                {
                    var field = applicantToken["mapValue"]?["fields"];
                    if (field == null)
                        continue;

                    club.applicants.Add(new ApplicantData
                    {
                        firebaseID = field["firebaseID"]?["stringValue"]?.ToString() ?? string.Empty,
                        unityID = field["unityID"]?["stringValue"]?.ToString() ?? string.Empty,
                        applicantName = field["applicantName"]?["stringValue"]?.ToString() ?? string.Empty,
                        profileIconId = field["profileIconId"]?["stringValue"]?.ToString() ?? string.Empty,
                        eloRating = int.TryParse(field["eloRating"]?["integerValue"]?.ToString(), out var eloRating) ? eloRating : 0,
                        bestLeaderboardTier = Enum.TryParse(field["bestLeaderboardTier"]?["integerValue"]?.ToString(), out LeaderboardTier bestLeaderboardTier)
                            ? bestLeaderboardTier
                            : LeaderboardTier.None,
                        bestLeaderboardScore = int.TryParse(field["bestLeaderboardScore"]?["integerValue"]?.ToString(), out var bestLeaderboardScore) ? bestLeaderboardScore : 0,
                    });
                }
            }

            // Try to extract the searchTokens array from the Firestore document
            var searchTokensToken = fields["searchTokens"]?["arrayValue"]?["values"];

            if (searchTokensToken != null && searchTokensToken is JArray tokensArray && tokensArray.Count > 0)
            {
                // Safely parse each token as stringValue and filter out null/empty entries
                club.searchTokens = tokensArray
                    .Select(token => token?["stringValue"]?.ToString()?.Trim() ?? string.Empty)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();
            } 
            else
            {
                // Default empty array if field is missing or empty
                club.searchTokens = Array.Empty<string>();
            }


            return club;
        }

        [Serializable]
        public class MemberData
        {
            [JsonProperty("firebaseMemberId")]
            public string firebaseMemberId;
            
            [JsonProperty("unityMemberId")]
            public string unityMemberId;
            
            [JsonProperty("memberName")]
            public string memberName;
            
            [JsonProperty("profileIconId")]
            public string profileIconId;

            [JsonProperty("victories")]
            public int victories;

            [JsonProperty("joinedDate")]
            public long joinedDate;

            [JsonProperty("badges")]
            public string[] badges;

            [JsonProperty("totalAchievements")]
            public int totalAchievements;

            [JsonProperty("rank")]
            public ClubRanksTypes rank;

            [JsonConstructor]
            public MemberData()
            {
                firebaseMemberId = string.Empty;
                unityMemberId = string.Empty;
                memberName = string.Empty;
                profileIconId = string.Empty;

                victories = 0;
                badges = Array.Empty<string>();
                totalAchievements = 0;

                rank = ClubRanksTypes.Member;
                joinedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            
            public MemberData(string firebaseMemberId, string unityMemberId, string memberName, string profileIconId, string[] badges, int totalAchievements, ClubRanksTypes rank)
            {
                this.firebaseMemberId = firebaseMemberId;
                this.unityMemberId = unityMemberId;
                this.memberName = memberName;
                this.profileIconId = profileIconId;
                this.rank = rank;
                this.badges = badges ?? Array.Empty<string>();
                this.totalAchievements = totalAchievements >= 0 ? totalAchievements : 0;
                victories = 0;
                joinedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        [Serializable]
        public class IconData : ICloneable, IEquatable<IconData>
        {
            // Textures Ids
            [JsonProperty("shieldId")]
            public string? shieldId;

            [JsonProperty("textureId")]
            public string? textureId;

            [JsonProperty("centralImageId")]
            public string? centralImageId;
            
            // Colors Id Ids
            [JsonProperty("shieldColorId")]
            public string? shieldColorId;

            [JsonProperty("textureColorId")]
            public string? textureColorId;

            [JsonProperty("centralImageColorId")]
            public string? centralImageColorId;
            
            [JsonProperty("backgroundColorId")]
            public string? backgroundColorId;

            [JsonConstructor]
            public IconData()
            {
            }

            public IconData(string? shieldId, string? textureId, string? centralImageId, string? shieldColorId, string? textureColorId, string? centralImageColorId, string? backgroundColorId)
            {
                this.shieldId = shieldId;
                this.textureId = textureId;
                this.centralImageId = centralImageId;
                this.shieldColorId = shieldColorId;
                this.textureColorId = textureColorId;
                this.centralImageColorId = centralImageColorId;
                this.backgroundColorId = backgroundColorId;
            }

            public bool Equals(IconData? other)
            {
                if (other == null) return false;

                return shieldId == other.shieldId &&
                       textureId == other.textureId &&
                       centralImageId == other.centralImageId &&
                       shieldColorId == other.shieldColorId &&
                       textureColorId == other.textureColorId &&
                       centralImageColorId == other.centralImageColorId &&
                       backgroundColorId == other.backgroundColorId;
            }

            public object Clone() =>
                new IconData(shieldId, textureId, centralImageId, shieldColorId, textureColorId, centralImageColorId, backgroundColorId);

            public override bool Equals(object? obj)
            {
                return Equals(obj as IconData);
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(shieldId);
                hash.Add(textureId);
                hash.Add(centralImageId);
                hash.Add(shieldColorId);
                hash.Add(textureColorId);
                hash.Add(centralImageColorId);
                hash.Add(backgroundColorId);
                return hash.ToHashCode();
            }

            public static bool operator ==(IconData? left, IconData? right)
            {
                if (left is null) return right is null;
                return left.Equals(right);
            }

            public static bool operator !=(IconData? left, IconData? right)
            {
                return !(left == right);
            }
        }

        [Serializable]
        public class ApplicantData
        {
            [JsonProperty("firebaseID")]
            public string firebaseID;

            [JsonProperty("unityID")]
            public string unityID;

            [JsonProperty("applicantName")]
            public string applicantName;

            [JsonProperty("profileIconId")]
            public string profileIconId;

            [JsonProperty("eloRating")]
            public int eloRating;

            [JsonProperty("bestLeaderboardTier")]
            public LeaderboardTier bestLeaderboardTier;

            [JsonProperty("bestLeaderboardScore")]
            public int bestLeaderboardScore;

            [JsonConstructor]
            public ApplicantData()
            {
                firebaseID = string.Empty;
                unityID = string.Empty;
                applicantName = string.Empty;
                profileIconId = string.Empty;
                eloRating = 0;
                bestLeaderboardTier = LeaderboardTier.None;
                bestLeaderboardScore = 0;
            }

            public ApplicantData(string firebaseID, string unityID, string applicantName, string profileIconId, LeaderboardTier bestLeaderboardTier, int bestLeaderboardScore)
            {
                this.firebaseID = firebaseID;
                this.unityID = unityID;
                this.applicantName = applicantName;
                this.profileIconId = profileIconId;
                this.bestLeaderboardTier = bestLeaderboardTier;
                this.bestLeaderboardScore = bestLeaderboardScore;
                eloRating = 0;
            }
        }
    }
}
