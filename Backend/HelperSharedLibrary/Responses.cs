using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.PlayerPurchasesData;
using static HelperSharedLibrary.PurchaseOrderResponse;

namespace HelperSharedLibrary
{
    #region FirebaseAuthResponseData
    /// <summary>
    /// Basic Firebase response containing tokens only.
    /// Use this when you only need to pass id/refresh tokens along with optional content.
    /// </summary>
    public class FirebaseBasicResponse
    {
        // Nullable strings by rule. Defaults are assigned in the parameterless constructor.
        public string idToken;
        public string refreshToken;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseBasicResponse()
        {
            // Empty string avoids null-reference surprises in consumers.
            idToken = string.Empty;
            refreshToken = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseBasicResponse(string idToken, string refreshToken)
        {
            this.idToken = idToken;
            this.refreshToken = refreshToken;
        }
    }

    /// <summary>
    /// Generic Firebase response with tokens and a typed content payload.
    /// </summary>
    public class FirebaseBasicResponse<T>
    {
        public string idToken;
        public string refreshToken;
        public T? content;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseBasicResponse()
        {
            idToken = string.Empty;
            refreshToken = string.Empty;
            content = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseBasicResponse(string idToken, string refreshToken, T? content)
        {
            this.idToken = idToken;
            this.refreshToken = refreshToken;
            this.content = content;
        }
    }

    /// <summary>
    /// Detailed Firebase authentication response as returned by Identity Toolkit.
    /// </summary>
    public class FirebaseAuthResponseData
    {
        [JsonProperty("kind")] public string kind;
        [JsonProperty("idToken")] public string idToken;
        [JsonProperty("displayName")] public string displayName;
        [JsonProperty("email")] public string email;
        [JsonProperty("refreshToken")] public string refreshToken;
        [JsonProperty("expiresIn")] public string expiresIn;
        [JsonProperty("localId")] public string localId;
        [JsonProperty("providerId")] public string providerId;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseAuthResponseData()
        {
            kind = string.Empty;
            idToken = string.Empty;
            displayName = string.Empty;
            email = string.Empty;
            refreshToken = string.Empty;
            expiresIn = string.Empty;
            localId = string.Empty;
            providerId = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseAuthResponseData(string kind, string idToken, string displayName, string email, string refreshToken, string expiresIn, string localId, string providerId)
        {
            this.kind = kind;
            this.idToken = idToken;
            this.displayName = displayName;
            this.email = email;
            this.refreshToken = refreshToken;
            this.expiresIn = expiresIn;
            this.localId = localId;
            this.providerId = providerId;
        }
    }

    /// <summary>
    /// Firebase send-email response containing tokens and email.
    /// </summary>
    public class FirebaseSendEmailResponse
    {
        // Nullable strings by rule. Defaults are assigned in the parameterless constructor.
        public string kind;
        public string email;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseSendEmailResponse()
        {
            // Empty string avoids null-reference surprises in consumers.
            kind = string.Empty;
            email = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseSendEmailResponse(string kind, string email)
        {
            this.kind = kind;
            this.email = email;
        }
    }

    /// <summary>
    /// Firebase lookup response for user information, augmented with tokens.
    /// </summary>
    public class FirebaseLookUpResponseData
    {
        [JsonProperty("kind")] public string kind;
        [JsonProperty("users")] public FirebaseUserData[] users;

        public string idToken;
        public string refreshToken;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseLookUpResponseData()
        {
            kind = string.Empty;
            users = new FirebaseUserData[0];
            idToken = string.Empty;
            refreshToken = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseLookUpResponseData(string kind, FirebaseUserData[] users, string idToken, string refreshToken)
        {
            this.kind = kind;
            this.users = users;
            this.idToken = idToken;
            this.refreshToken = refreshToken;
        }
    }

    /// <summary>
    /// Firebase user record returned by the Identity Toolkit user lookup endpoint.
    /// Arrays are non-null by design; strings are nullable.
    /// </summary>
    public class FirebaseUserData
    {
        [JsonProperty("localId")] public string localId;
        [JsonProperty("email")] public string email;
        [JsonProperty("displayName")] public string displayName;
        [JsonProperty("passwordHash")] public string passwordHash;
        [JsonProperty("emailVerified")] public bool emailVerified;
        [JsonProperty("passwordUpdatedAt")] public long passwordUpdatedAt;
        [JsonProperty("providerUserInfo")] public FirebaseProviderData[] providerUserInfo;
        [JsonProperty("validSince")] public string validSince;
        [JsonProperty("lastLoginAt")] public string lastLoginAt;
        [JsonProperty("createdAt")] public string createdAt;
        [JsonProperty("lastRefreshAt")] public string lastRefreshAt;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseUserData()
        {
            localId = string.Empty;
            email = string.Empty;
            displayName = string.Empty;
            passwordHash = string.Empty;
            emailVerified = false;
            passwordUpdatedAt = 0;
            providerUserInfo = new FirebaseProviderData[0];
            validSince = string.Empty;
            lastLoginAt = string.Empty;
            lastRefreshAt = string.Empty;
            createdAt = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseUserData(string localId, string email, string displayName, string passwordHash, bool emailVerified, long passwordUpdatedAt, FirebaseProviderData[] providerUserInfo, string validSince, string lastLoginAt, string createdAt, string lastRefreshAt)
        {
            this.localId = localId;
            this.email = email;
            this.displayName = displayName;
            this.passwordHash = passwordHash;
            this.emailVerified = emailVerified;
            this.passwordUpdatedAt = passwordUpdatedAt;
            this.providerUserInfo = providerUserInfo;
            this.validSince = validSince;
            this.lastLoginAt = lastLoginAt;
            this.createdAt = createdAt;
            this.lastRefreshAt = lastRefreshAt;
        }
    }

    /// <summary>
    /// Identity provider details linked to a Firebase user (e.g., Google, Facebook).
    /// </summary>
    public class FirebaseProviderData
    {
        [JsonProperty("providerId")] public string providerId;
        [JsonProperty("displayName")] public string displayName;
        [JsonProperty("federatedId")] public string federatedId;
        [JsonProperty("email")] public string email;
        [JsonProperty("rawId")] public string rawId;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseProviderData()
        {
            providerId = string.Empty;
            displayName = string.Empty;
            federatedId = string.Empty;
            email = string.Empty;
            rawId = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseProviderData(string providerId, string displayName, string federatedId, string email, string rawId)
        {
            this.providerId = providerId;
            this.displayName = displayName;
            this.federatedId = federatedId;
            this.email = email;
            this.rawId = rawId;
        }
    }

    /// <summary>
    /// Error envelope returned by Firebase REST APIs.
    /// </summary>
    public class FirebaseRequestErrorResponse
    {
        public int code;
        public string message;
        public FirebaseErrorDetail[] errors;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseRequestErrorResponse()
        {
            code = 0;
            message = string.Empty;
            errors = new FirebaseErrorDetail[0];
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseRequestErrorResponse(int code, string message, FirebaseErrorDetail[] errors)
        {
            this.code = code;
            this.message = message;
            this.errors = errors;
        }
    }

    /// <summary>
    /// Detailed error item used within FirebaseRequestErrorResponse.
    /// </summary>
    public class FirebaseErrorDetail
    {
        public string message;
        public string domain;
        public string reason;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirebaseErrorDetail()
        {
            message = string.Empty;
            domain = string.Empty;
            reason = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirebaseErrorDetail(string message, string domain, string reason)
        {
            this.message = message;
            this.domain = domain;
            this.reason = reason;
        }
    }

    /// <summary>
    /// Simple model for the user returned by RTDB.
    /// </summary>
    public class FirebaseUserSearchResult
    {
        public string? userId;
        public string? displayName;
        public string? normalizedName;

        public FirebaseUserSearchResult()
        {
        }

        public FirebaseUserSearchResult(string userId, string displayName, string normalizedName)
        {
            this.userId = userId;
            this.displayName = displayName;
            this.normalizedName = normalizedName;
        }
    }
    #endregion

    #region UGSAuthResponseData
    /// <summary>
    /// Cloud Save list response wrapper. Arrays are non-null; objects are nullable.
    /// </summary>
    [Serializable]
    public class CloudSaveResponse
    {
        [JsonProperty("results")] public ResponseData[] results;
        [JsonProperty("links")] public Links? links;
        [JsonProperty("sizeLimit")] public int sizeLimit;
        [JsonProperty("totalSize")] public int totalSize;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public CloudSaveResponse()
        {
            results = new ResponseData[0];
            links = default;
            sizeLimit = 0;
            totalSize = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public CloudSaveResponse(ResponseData[] results, Links? links, int sizeLimit, int totalSize)
        {
            this.results = results;
            this.links = links;
            this.sizeLimit = sizeLimit;
            this.totalSize = totalSize;
        }
    }

    /// <summary>
    /// Response for listing users in UGS Authentication.
    /// </summary>
    [Serializable]
    public class ListUsersResponse
    {
        [JsonProperty("next")] public string next;
        [JsonProperty("results")] public UserData[] results;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public ListUsersResponse()
        {
            next = string.Empty;
            results = new UserData[0];
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public ListUsersResponse(string next, UserData[] results)
        {
            this.next = next;
            this.results = results;
        }
    }

    /// <summary>
    /// Single Cloud Save item including metadata and locks.
    /// </summary>
    [Serializable]
    public class ResponseData
    {
        [JsonProperty("key")] public string key;
        [JsonProperty("value")] public JToken? value;
        [JsonProperty("writeLock")] public string writeLock;
        [JsonProperty("modified")] public DateContainer? modified;
        [JsonProperty("created")] public DateContainer? created;
        [JsonProperty("storedSize")] public int storedSize;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public ResponseData()
        {
            key = string.Empty;
            value = default;
            writeLock = string.Empty;
            modified = default;
            created = default;
            storedSize = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public ResponseData(string key, JToken? value, string writeLock, DateContainer? modified, DateContainer? created, int storedSize)
        {
            this.key = key;
            this.value = value;
            this.writeLock = writeLock;
            this.modified = modified;
            this.created = created;
            this.storedSize = storedSize;
        }
    }

    /// <summary>
    /// Simple date container used by Cloud Save metadata.
    /// </summary>
    [Serializable]
    public class DateContainer
    {
        [JsonProperty("date")] public DateTime date;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public DateContainer()
        {
            date = DateTime.MinValue;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public DateContainer(DateTime date)
        {
            this.date = date;
        }
    }

    /// <summary>
    /// Pagination links returned by Cloud Save.
    /// </summary>
    [Serializable]
    public class Links
    {
        [JsonProperty("next")] public string next;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public Links()
        {
            next = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public Links(string next)
        {
            this.next = next;
        }
    }

    /// <summary>
    /// Authentication response object for UGS.
    /// </summary>
    [Serializable]
    public class UGSAuthResponseData
    {
        [JsonProperty("expiresIn")] public int expiresIn;
        [JsonProperty("idToken")] public string idToken;
        [JsonProperty("sessionToken")] public string sessionToken;
        [JsonProperty("user")] public UserData? user;
        [JsonProperty("userId")] public string userId;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UGSAuthResponseData()
        {
            expiresIn = 0;
            idToken = string.Empty;
            sessionToken = string.Empty;
            user = default;
            userId = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UGSAuthResponseData(int expiresIn, string idToken, string sessionToken, UserData? user, string userId)
        {
            this.expiresIn = expiresIn;
            this.idToken = idToken;
            this.sessionToken = sessionToken;
            this.user = user;
            this.userId = userId;
        }
    }

    /// <summary>
    /// UGS user profile summary.
    /// </summary>
    [Serializable]
    public class UserData
    {
        [JsonProperty("disabled")] public bool disabled;
        [JsonProperty("externalIds")] public List<ExternalId> externalIds;
        [JsonProperty("id")] public string id;
        [JsonProperty("username")] public string username;
        [JsonProperty("createdAt")] public long createdAt;
        [JsonProperty("lastLoginAt")] public long lastLoginAt;
        [JsonProperty("usernamePasswordLoginMetadata")] public UsernamePasswordLoginMetadata? usernamePasswordLoginMetadata;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UserData()
        {
            disabled = false;
            externalIds = new List<ExternalId>();
            id = string.Empty;
            username = string.Empty;
            createdAt = 0;
            lastLoginAt = 0;
            usernamePasswordLoginMetadata = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UserData(bool disabled, List<ExternalId> externalIds, string id, string username, long createdAt, long lastLoginAt, UsernamePasswordLoginMetadata? usernamePasswordLoginMetadata)
        {
            this.disabled = disabled;
            this.externalIds = externalIds ?? new List<ExternalId>();
            this.id = id;
            this.username = username;
            this.createdAt = createdAt;
            this.lastLoginAt = lastLoginAt;
            this.usernamePasswordLoginMetadata = usernamePasswordLoginMetadata;
        }
    }

    /// <summary>
    /// External identity reference for a UGS user.
    /// </summary>
    [Serializable]
    public class ExternalId
    {
        [JsonProperty("externalId")] public string externalId;
        [JsonProperty("providerId")] public string providerId;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public ExternalId()
        {
            externalId = string.Empty;
            providerId = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public ExternalId(string externalId, string providerId)
        {
            this.externalId = externalId;
            this.providerId = providerId;
        }
    }

    /// <summary>
    /// Username/password metadata for a UGS user.
    /// </summary>
    [Serializable]
    public class UsernamePasswordLoginMetadata
    {
        [JsonProperty("username")] public string username;
        [JsonProperty("createdAt")] public long createdAt;
        [JsonProperty("lastLoginAt")] public long lastLoginAt;
        [JsonProperty("passwordUpdatedAt")] public long passwordUpdatedAt;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UsernamePasswordLoginMetadata()
        {
            username = string.Empty;
            createdAt = 0;
            lastLoginAt = 0;
            passwordUpdatedAt = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UsernamePasswordLoginMetadata(string username, long createdAt, long lastLoginAt, long passwordUpdatedAt)
        {
            this.username = username;
            this.createdAt = createdAt;
            this.lastLoginAt = lastLoginAt;
            this.passwordUpdatedAt = passwordUpdatedAt;
        }
    }

    /// <summary>
    /// Error envelope used by UGS HTTP APIs.
    /// Readonly fields are set in constructors only.
    /// </summary>
    [Serializable]
    public class UGSRequestErrorResponse
    {
        [JsonProperty("code")] public readonly int code;
        [JsonProperty("title")] public readonly string title;
        [JsonProperty("status")] public readonly int status;
        [JsonProperty("detail")] public readonly string detail;
        [JsonProperty("details")] public readonly List<UGSErrorDetail> details;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UGSRequestErrorResponse()
        {
            code = 0;
            title = string.Empty;
            status = 0;
            detail = string.Empty;
            details = new List<UGSErrorDetail>();
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UGSRequestErrorResponse(int code, string title, int status, string detail, List<UGSErrorDetail>? details)
        {
            this.code = code;
            this.title = title;
            this.status = status;
            this.detail = detail;
            this.details = details ?? new List<UGSErrorDetail>();
        }
    }


    /// <summary>
    /// Detailed error item within a UGSRequestErrorResponse.
    /// </summary>
    [Serializable]
    public class UGSErrorDetail
    {
        [JsonProperty("code")] public readonly string code;
        [JsonProperty("path")] public readonly string path;
        [JsonProperty("message")] public readonly string message;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UGSErrorDetail()
        {
            code = string.Empty;
            path = string.Empty;
            message = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UGSErrorDetail(string code, string path, string message)
        {
            this.code = code;
            this.path = path;
            this.message = message;
        }
    }

    #endregion

    #region MissionResponseData
    /// <summary>
    /// Aggregates mission lists and server-side reset information.
    /// </summary>
    [Serializable]
    public class MissionResponse
    {
        [JsonProperty("dailyGameMissions")] public List<GameMissionData> dailyGameMissions;
        [JsonProperty("weeklyGameMissions")] public List<GameMissionData> weeklyGameMissions;
        [JsonProperty("dailyPlayerMissions")] public List<PlayerMissionData> dailyPlayerMissions;
        [JsonProperty("weeklyPlayerMissions")] public List<PlayerMissionData> weeklyPlayerMissions;
        [JsonProperty("serverTime")] public string serverTime;
        [JsonProperty("nextDailyReset")] public string nextDailyReset;
        [JsonProperty("nextWeeklyReset")] public string nextWeeklyReset;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public MissionResponse()
        {
            dailyGameMissions = new List<GameMissionData>();
            weeklyGameMissions = new List<GameMissionData>();
            dailyPlayerMissions = new List<PlayerMissionData>();
            weeklyPlayerMissions = new List<PlayerMissionData>();
            serverTime = string.Empty;
            nextDailyReset = string.Empty;
            nextWeeklyReset = string.Empty;
        }

        /// <summary>
        /// Constructor that initializes mission lists; server time fields will be filled later.
        /// </summary>
        public MissionResponse(List<GameMissionData> dailyGameMissions, List<GameMissionData> weeklyGameMissions, List<PlayerMissionData> dailyPlayerMissions, List<PlayerMissionData> weeklyPlayerMissions)
        {
            this.dailyGameMissions = dailyGameMissions ?? new List<GameMissionData>();
            this.weeklyGameMissions = weeklyGameMissions ?? new List<GameMissionData>();
            this.dailyPlayerMissions = dailyPlayerMissions ?? new List<PlayerMissionData>();
            this.weeklyPlayerMissions = weeklyPlayerMissions ?? new List<PlayerMissionData>();
            serverTime = string.Empty;
            nextDailyReset = string.Empty;
            nextWeeklyReset = string.Empty;
        }

        /// <summary>
        /// Helper to populate server-time fields (ISO-8601 strings) and next reset moments.
        /// </summary>
        public void InitializeServerTimes()
        {
            var now = DateTime.UtcNow;
            serverTime = now.ToString("o");
            nextDailyReset = GetNextDailyReset(now).ToString("o");
            nextWeeklyReset = GetNextWeeklyReset(now).ToString("o");
        }

        /// <summary>
        /// Computes the next daily reset at 00:00 UTC.
        /// </summary>
        private DateTime GetNextDailyReset(DateTime nowUtc)
        {
            var next = nowUtc.Date.AddDays(1);
            return next;
        }

        /// <summary>
        /// Computes the next weekly reset at Tuesday 00:00 UTC.
        /// </summary>
        private DateTime GetNextWeeklyReset(DateTime nowUtc)
        {
            int daysUntilNextTuesday = ((int)DayOfWeek.Tuesday - (int)nowUtc.DayOfWeek + 7) % 7;
            if (daysUntilNextTuesday == 0) daysUntilNextTuesday = 7;
            var nextTuesday = nowUtc.Date.AddDays(daysUntilNextTuesday);
            return nextTuesday;
        }
    }

    /// <summary>
    /// Response for claiming a mission reward. Contains the mission config, a flag for daily bonus, and the new currency amount.
    /// </summary>
    [Serializable]
    public class MissionClaimRewardResponse
    {
        [JsonProperty("missionConfigData")] public MissionConfigData? missionConfigData;
        [JsonProperty("isRetrievingDailyBonus")] public bool isRetrievingDailyBonus;
        [JsonProperty("newCurrencyAmount")] public uint newCurrencyAmount;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public MissionClaimRewardResponse()
        {
            missionConfigData = default;
            isRetrievingDailyBonus = false;
            newCurrencyAmount = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public MissionClaimRewardResponse(MissionConfigData? missionConfigData, bool isRetrievingDailyBonus, uint newCurrencyAmount)
        {
            this.missionConfigData = missionConfigData;
            this.isRetrievingDailyBonus = isRetrievingDailyBonus;
            this.newCurrencyAmount = newCurrencyAmount;
        }
    }
    #endregion

    #region AchievementResponseData
    /// <summary>
    /// Aggregates player achievement data plus optional analytics payload.
    /// </summary>
    [Serializable]
    public class AchievementResponse
    {
        [JsonProperty(nameof(playerDataAchievements))] public List<PlayerAchievementData> playerDataAchievements;
        [JsonProperty(nameof(analyticsData))] public AnalyticsData? analyticsData;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public AchievementResponse()
        {
            playerDataAchievements = new List<PlayerAchievementData>();
            analyticsData = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public AchievementResponse(List<PlayerAchievementData> playerDataAchievements, AnalyticsData? analyticsData)
        {
            this.playerDataAchievements = playerDataAchievements ?? new List<PlayerAchievementData>();
            this.analyticsData = analyticsData;
        }
    }

    /// <summary>
    /// Response after claiming an achievement reward; includes unlocked cosmetic identifiers.
    /// </summary>
    [Serializable]
    public class AchievementClaimRewardResponse
    {
        [JsonProperty("achievementConfigData")] public string[] cosmeticsObtained;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public AchievementClaimRewardResponse()
        {
            cosmeticsObtained = new string[0];
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public AchievementClaimRewardResponse(string[] cosmeticsObtained)
        {
            this.cosmeticsObtained = cosmeticsObtained ?? new string[0];
        }
    }
    #endregion

    #region LeaderboardReponseData
    /// <summary>
    /// Wrapper for leaderboard query results.
    /// </summary>
    public class LeaderboardResponse
    {
        public List<LeaderboardData> results;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public LeaderboardResponse()
        {
            results = new List<LeaderboardData>();
        }
    }

    /// <summary>
    /// Flattened entry for displaying leaderboard rows on the client.
    /// </summary>
    [Serializable]
    public class LeaderboardData
    {
        [JsonProperty("playerId")] public string playerId;
        [JsonProperty("playerName")] public string playerName;
        [JsonProperty("rank")] public int rank;
        [JsonProperty("score")] public double score;
        [JsonProperty("tier")] public string tier;
        [JsonProperty("metadata")] public string metadata;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public LeaderboardData()
        {
            playerId = string.Empty;
            playerName = string.Empty;
            rank = 0;
            score = 0;
            tier = string.Empty;
            metadata = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public LeaderboardData(string playerId, string playerName, int rank, double score, string tier, string metadata)
        {
            this.playerId = playerId;
            this.playerName = playerName;
            this.rank = rank;
            this.score = score;
            this.tier = tier;
            this.metadata = metadata;
        }
    }

    /// <summary>
    /// Batch response with leaderboard entries mapped to achievement data.
    /// </summary>
    [Serializable]
    public class GetLeaderboardEntriesAchievementsResponse
    {
        [JsonProperty("results")] public List<PlayerAchievementResult> results;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public GetLeaderboardEntriesAchievementsResponse()
        {
            results = new List<PlayerAchievementResult>();
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public GetLeaderboardEntriesAchievementsResponse(List<PlayerAchievementResult> results)
        {
            this.results = results ?? new List<PlayerAchievementResult>();
        }
    }

    /// <summary>
    /// Single player achievement lookup result within a leaderboard context.
    /// </summary>
    [Serializable]
    public class PlayerAchievementResult
    {
        [JsonProperty("playerId")] public string playerId;
        [JsonProperty("leaderboardId")] public string leaderboardId;
        [JsonProperty("profileIconId")] public string profileIconId;
        [JsonProperty("achievementData")] public LeaderboardAchievementData? achievementData;
        [JsonProperty("updated")] public bool updated;
        [JsonProperty("error")] public string error;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// Marked with JsonConstructor and Preserve for WebGL/IL2CPP compatibility.
        /// </summary>
        [JsonConstructor]
        public PlayerAchievementResult()
        {
            playerId = string.Empty;
            leaderboardId = string.Empty;
            profileIconId = string.Empty;
            achievementData = null;
            updated = false;
            error = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// Also preserved to avoid IL2CPP stripping.
        /// </summary>
        public PlayerAchievementResult
            (string playerId, 
            string leaderboardId, 
            string profileIconId, 
            LeaderboardAchievementData? achievementData, 
            bool updated, 
            string error = "")
        {
            this.playerId = playerId;
            this.leaderboardId = leaderboardId;
            this.profileIconId = profileIconId;
            this.achievementData = achievementData;
            this.updated = updated;
            this.error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// Response after updating leaderboard score based on a match result.
    /// </summary>
    [Serializable]
    public class UpdateLeaderboardScoreByMatchResultResponse
    {
        [JsonProperty("leaderboardId")] public string leaderboardId;
        [JsonProperty("results")] public UpdateLeaderboardScoreByMatchResultResponseEntry? results;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UpdateLeaderboardScoreByMatchResultResponse()
        {
            leaderboardId = string.Empty;
            results = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UpdateLeaderboardScoreByMatchResultResponse(string leaderboardId, UpdateLeaderboardScoreByMatchResultResponseEntry? results)
        {
            this.leaderboardId = leaderboardId;
            this.results = results;
        }
    }

    /// <summary>
    /// Payload describing the scoring delta and streaks resulting from a match.
    /// </summary>
    [Serializable]
    public class UpdateLeaderboardScoreByMatchResultResponseEntry
    {
        [JsonProperty("isVictory")] public bool isVictory;
        [JsonProperty("scoreDelta")] public int scoreDelta;
        [JsonProperty("winStreak")] public int winStreak;
        [JsonProperty("loseStreak")] public int loseStreak;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UpdateLeaderboardScoreByMatchResultResponseEntry()
        {
            isVictory = false;
            scoreDelta = 0;
            winStreak = 0;
            loseStreak = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UpdateLeaderboardScoreByMatchResultResponseEntry(bool isVictory, int scoreDelta, int winStreak, int loseStreak)
        {
            this.isVictory = isVictory;
            this.scoreDelta = scoreDelta;
            this.winStreak = winStreak;
            this.loseStreak = loseStreak;
        }
    }

    /// <summary>
    /// Batch response with leaderboard entries mapped to player data.
    /// </summary>
    [Serializable]
    public class GetLeaderboardPlayerDataResponse
    {
        [JsonProperty("results")] public List<PlayerLeaderboardDataResult> results;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public GetLeaderboardPlayerDataResponse()
        {
            results = new List<PlayerLeaderboardDataResult>();
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public GetLeaderboardPlayerDataResponse(List<PlayerLeaderboardDataResult> results)
        {
            this.results = results ?? new List<PlayerLeaderboardDataResult>();
        }
    }
    /// <summary>
    /// Single player achievement lookup result within a leaderboard context.
    /// </summary>
    [Serializable]
    public class PlayerLeaderboardDataResult
    {
        [JsonProperty("playerLeaderboardData")] public PlayerLeaderboardData? playerLeaderboardData;
        [JsonProperty("error")] public string error;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// Marked with JsonConstructor and Preserve for WebGL/IL2CPP compatibility.
        /// </summary>
        [JsonConstructor]
        public PlayerLeaderboardDataResult()
        {
            playerLeaderboardData = null;
            error = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// Also preserved to avoid IL2CPP stripping.
        /// </summary>
        public PlayerLeaderboardDataResult
            (PlayerLeaderboardData? playerLeaderboardData,
            string error = "")
        {
            this.playerLeaderboardData = playerLeaderboardData;
            this.error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// Represents a response containing top nationality leaderboard data and an associated message.
    /// </summary>
    [Serializable]
    public class RTDBPlayerLeaderboardDataResponse
    {
        public Dictionary<string, List<RTDBPlayerLeaderboardData>> rankingByLeaderboard;
        public string message;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public RTDBPlayerLeaderboardDataResponse()
        {
            rankingByLeaderboard = new();
            message = string.Empty;
        }
    }
    #endregion

    #region CosmeticResponseData
    /// <summary>
    /// Response for purchasing a cosmetic item.
    /// </summary>
    [Serializable]
    public class PurchaseCosmeticResponse
    {
        public PlayerCosmeticData? cosmeticPurchased;
        public uint newCurrencyAmount;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public PurchaseCosmeticResponse()
        {
            cosmeticPurchased = default;
            newCurrencyAmount = 0;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public PurchaseCosmeticResponse(PlayerCosmeticData? cosmeticPurchased, uint newCurrencyAmount)
        {
            this.cosmeticPurchased = cosmeticPurchased;
            this.newCurrencyAmount = newCurrencyAmount;
        }
    }
    #endregion

    #region ProfileResponseData
    /// <summary>
    /// Response carrying the full player profile payload.
    /// </summary>
    [Serializable]
    public class ProfileResponse
    {
        public PlayerProfileData? playerProfileData;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public ProfileResponse()
        {
            playerProfileData = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public ProfileResponse(PlayerProfileData? playerProfileData)
        {
            this.playerProfileData = playerProfileData;
        }
    }
    #endregion

    #region ClubResponseData
    /// <summary>
    /// Represents the Firestore document data for a club entry in a leaderboard.
    /// </summary>
    public class FirestoreClubDataResponse
    {
        public FirestoreClubData? playerClubData;
        public string message;
        public ClubResponseCodeType clubResponseCodeType;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirestoreClubDataResponse()
        {
            playerClubData = default;
            message = string.Empty;
            clubResponseCodeType = ClubResponseCodeType.None;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirestoreClubDataResponse(FirestoreClubData? playerClubData, string message, ClubResponseCodeType clubResponseCodeType)
        {
            this.playerClubData = playerClubData;
            this.message = message;
            this.clubResponseCodeType = clubResponseCodeType;
        }
    }

    public class FirestoreClubSearchResponse
    {
        public List<FirestoreClubData>? clubsFound;
        public string message;
        public ClubResponseCodeType clubResponseCodeType;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirestoreClubSearchResponse()
        {
            clubsFound = new List<FirestoreClubData>();
            message = string.Empty;
            clubResponseCodeType = ClubResponseCodeType.None;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirestoreClubSearchResponse(string message, ClubResponseCodeType clubResponseCodeType, params FirestoreClubData[] clubsFound)
        {
            this.message = message;
            this.clubResponseCodeType = clubResponseCodeType;
            this.clubsFound = new List<FirestoreClubData>(clubsFound ?? Array.Empty<FirestoreClubData>());
        }
    }

    public class FirestoreClubChatResponse
    {
        public FirestoreClubChatData clubChatData;
        public string message;
        public ClubResponseCodeType clubResponseCodeType;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirestoreClubChatResponse()
        {
            clubChatData = new();
            message = string.Empty;
            clubResponseCodeType = ClubResponseCodeType.None;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirestoreClubChatResponse(FirestoreClubChatData clubChatData, string message, ClubResponseCodeType clubResponseCodeType)
        {
            this.clubChatData = clubChatData ?? new();
            this.message = message;
            this.clubResponseCodeType = clubResponseCodeType;
        }
    }

    public class FirestoreTopClubsResponse
    {
        public List<FirestoreClubData>? leaderboard;
        public string message;
        public ClubResponseCodeType clubResponseCodeType;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public FirestoreTopClubsResponse()
        {
            leaderboard = new List<FirestoreClubData>();
            message = string.Empty;
            clubResponseCodeType = ClubResponseCodeType.None;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public FirestoreTopClubsResponse(string message, ClubResponseCodeType clubResponseCodeType, params FirestoreClubData[] leaderboard)
        {
            this.message = message;
            this.clubResponseCodeType = clubResponseCodeType;
            this.leaderboard = new List<FirestoreClubData>(leaderboard ?? Array.Empty<FirestoreClubData>());
        }
    }

    #endregion

    #region AnalyticsResponseData
    /// <summary>
    /// Represents the analytics data sent to unity client.
    /// </summary>
    public class AnalyticsDataResponse
    {
        public AnalyticsData? analyticsData;
        public string message;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public AnalyticsDataResponse()
        {
            analyticsData = default;
            message = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public AnalyticsDataResponse(AnalyticsData? analyticsData, string? message = default)
        {
            this.analyticsData = analyticsData;
            this.message = message ?? string.Empty;
        }
    }
    #endregion

    #region PurchaseResponseData
    [Serializable]
    public class PurchaseDataResponse
    {
        [JsonProperty("purchaseData")] public PlayerPurchasesData? purchaseData;
        [JsonProperty("message")] public string message;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public PurchaseDataResponse()
        {
            purchaseData = default;
            message = string.Empty;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public PurchaseDataResponse(PlayerPurchasesData? purchaseData, string message)
        {
            this.purchaseData = purchaseData;
            this.message = message;
        }
    }

    /// <summary>
    /// Response model returned to Unity after creating a PayPal purchase order.
    /// Contains only safe, client-consumable data.
    /// </summary>
    [Serializable]
    public class PurchaseOrderResponse
    {
        /// <summary>
        /// High-level result of the purchase request.
        /// </summary>
        [JsonProperty("state")]
        public PurchaseOrderState state;

        /// <summary>
        /// Machine-readable reason for the returned state.
        /// Useful for UI logic.
        /// </summary>
        [JsonProperty("reason")]
        public PurchaseOrderReason reason;

        /// <summary>
        /// Optional human-readable message (for logs or UI).
        /// </summary>
        [JsonProperty("message")]
        public string message;

        /// <summary>
        /// Current player purchase receipt data (if any).
        /// Allows Unity to know existing status.
        /// </summary>
        [JsonProperty("playerPurchaseReceiptData")]
        public PlayerPurchaseReceiptData? playerPurchaseReceiptData;

        /// <summary>
        /// URL to redirect the user to approve the payment.
        /// Only present when state == Created.
        /// </summary>
        [JsonProperty("approvalUrl")]
        public string? approvalUrl;

        /// <summary>
        /// External order ID created by the payment provider.
        /// Only present when state == Created.
        /// </summary>
        [JsonProperty("externalOrderId")]
        public string? externalOrderId;

        [JsonConstructor]
        public PurchaseOrderResponse()
        {
            state = PurchaseOrderState.Unknown;
            reason = PurchaseOrderReason.None;
            message = string.Empty;
            playerPurchaseReceiptData = null;
            approvalUrl = null;
            externalOrderId = null;
        }

        public enum PurchaseOrderState
        {
            Unknown = 0,

            // Purchase order successfully created, redirect required
            Created = 1,

            // Purchase not created because of business rules
            Blocked = 2,

            // Purchase exists but is still pending
            Pending = 3,

            // Purchase already completed
            Completed = 4,

            // Technical error
            Error = 5
        }

        public enum PurchaseOrderReason
        {
            None = 0,

            // System / config
            PurchaseSystemDisabled = 10,
            ProductNotFound = 11,
            ProductDisabled = 12,

            // Player state
            AlreadyPurchased = 20,
            SubscriptionStillActive = 21,
            PurchasePending = 22,

            // External errors
            ProviderUnavailable = 30,
            ProviderError = 31
        }

    }
    #endregion

    #region NationalityResponseData
    [Serializable]
    public class UpdateNationalityResponse
    {
        [JsonProperty("newNationality")]
        public NationalityData? newNationality;

        [JsonProperty("message")]
        public string message;

        /// <summary>
        /// Parameterless constructor that sets safe defaults.
        /// </summary>
        [JsonConstructor]
        public UpdateNationalityResponse()
        {
            message = string.Empty;
            newNationality = default;
        }

        /// <summary>
        /// Full constructor for explicit initialization.
        /// </summary>
        public UpdateNationalityResponse(NationalityData? newNationality, string message)
        {
            this.newNationality = newNationality;
            this.message = message;
        }
    }
    #endregion

    #region AccountDeletionData
    [Serializable]
    public class AccountDeletionResponse
    {
        /// <summary>
        /// Machine-readable reason for the returned state.
        /// Useful for UI logic.
        /// </summary>
        [JsonProperty("reason")]
        public DenyDeletionReason reason;

        /// <summary>
        /// Optional human-readable message (for logs or UI).
        /// </summary>
        [JsonProperty("message")]
        public string message;

        [JsonConstructor]
        public AccountDeletionResponse()
        {
            reason = DenyDeletionReason.None;
            message = string.Empty;
        }

        public enum DenyDeletionReason
        {
            None = 0,

            AccountRecentlyCreated = 10,
            IncorrectConfirmationEmail = 11,

            FailedToDeleteFirebaseAccount = 20,
            FailedToDeleteUGSData = 30,
            FailedToDeleteUGSAccount = 31
        }
    }
    #endregion
}
