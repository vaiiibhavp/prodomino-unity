using Newtonsoft.Json;
using System;
using static HelperSharedLibrary.ConfigData.LeaderboardConfigData;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.PlayerPurchasesData.PlayerPurchaseReceiptData;

namespace HelperSharedLibrary
{
    /// <summary>
    /// Configuration data for various game systems.
    /// </summary>
    [Serializable]
    public class ConfigData
    {
        [JsonProperty("missionConfig")]
        public MissionConfigData missionConfig;

        [JsonProperty("leaderboardConfig")]
        public LeaderboardConfigData leaderboardConfig;

        [JsonProperty("tutorialConfig")]
        public TutorialConfigData tutorialConfig;
        
        [JsonProperty("cosmeticConfig")]
        public CosmeticConfig cosmeticConfig;
        
        [JsonProperty("friendsConfig")]
        public FriendsConfig friendsConfig;
        
        [JsonProperty("notificationsConfig")]
        public NotificationsConfig notificationsConfig;
        
        [JsonProperty("clubsConfig")]
        public ClubsConfig clubsConfig;

        [JsonProperty("adsConfig")]
        public AdsConfig adsConfig;

        [JsonProperty("purchaseConfig")]
        public PurchaseConfig purchaseConfig;
        
        [JsonProperty("nationalityConfig")]
        public NationalityConfig nationalityConfig;

        public ConfigData()
        {
            missionConfig = new();
            leaderboardConfig = new();
            tutorialConfig = new();
            cosmeticConfig = new();
            friendsConfig = new();
            notificationsConfig = new();
            clubsConfig = new();
            adsConfig = new();
            purchaseConfig = new();
            nationalityConfig = new();
        }

        public ConfigData
            (MissionConfigData missionConfig, 
            LeaderboardConfigData leaderboardConfig, 
            TutorialConfigData tutorialConfig,
            CosmeticConfig cosmeticConfig, 
            FriendsConfig friendsConfig, 
            NotificationsConfig notificationsConfig,
            ClubsConfig clubsConfig,
            AdsConfig adsConfig,
            PurchaseConfig paypalConfig,
            NationalityConfig nationalityConfig)
        {
            this.missionConfig = missionConfig;
            this.leaderboardConfig = leaderboardConfig;
            this.tutorialConfig = tutorialConfig;
            this.cosmeticConfig = cosmeticConfig;
            this.friendsConfig = friendsConfig;
            this.notificationsConfig = notificationsConfig;
            this.clubsConfig = clubsConfig;
            this.adsConfig = adsConfig;
            this.purchaseConfig = paypalConfig;
            this.nationalityConfig = nationalityConfig;
        }

        /// <summary>
        /// Configuration for Mission system.
        /// </summary>
        [Serializable]
        public class MissionConfigData
        {
            [JsonProperty("dailyMissionsCount")]
            public byte dailyMissionsCount = 3;

            [JsonProperty("weeklyMissionsCount")]
            public byte weeklyMissionsCount = 1;

            [JsonProperty("dailyMissionBonusCurrency")]
            public Currency dailyMissionBonusCurrency = Currency.Token;

            [JsonProperty("dailyMissionBonusAmout")]
            public byte dailyMissionBonusAmout = 3;

            public MissionConfigData()
            {
            }

            public MissionConfigData(byte dailyMissionsCount, byte weeklyMissionsCount, Currency dailyMissionBonusCurrency, byte dailyMissionBonusAmout)
            {
                this.dailyMissionsCount = dailyMissionsCount;
                this.weeklyMissionsCount = weeklyMissionsCount;
                this.dailyMissionBonusCurrency = dailyMissionBonusCurrency;
                this.dailyMissionBonusAmout = dailyMissionBonusAmout;
            }
        }

        /// <summary>
        /// Configuration for Leaderboard system.
        /// </summary>
        [Serializable]
        public class LeaderboardConfigData
        {
            [JsonProperty("achievementsToShow")]
            public int achievementsToShow;

            [JsonProperty("maxPlayers")]
            public int maxPlayers;
            
            [JsonProperty("maxEMCImpact")]
            public int maxEMCImpact;

            [JsonProperty("victoryStreaks")]
            public LeaderboardStreakData[] victoryStreaks;
            
            [JsonProperty("defeatStreaks")]
            public LeaderboardStreakData[] defeatStreaks;

            [JsonProperty("tiersData")]
            public TiersData[] tiersData;
            
            
            [JsonProperty("kFactorByTier")]
            public KFactorByTierData[] kFactorByTier;

            public LeaderboardConfigData()
            {
                achievementsToShow = 0;
                maxPlayers = 0;
                maxEMCImpact = 0;
                victoryStreaks = Array.Empty<LeaderboardStreakData>();
                defeatStreaks = Array.Empty<LeaderboardStreakData>();
                tiersData = Array.Empty<TiersData>();
                kFactorByTier = Array.Empty<KFactorByTierData>();
            }

            public LeaderboardConfigData
                (int achievementsToShow, 
                int maxPlayers,
                int maxEMCImpact,
                LeaderboardStreakData[] victoryStreaks, 
                LeaderboardStreakData[] defeatStreaks,
                TiersData[] tiersData,
                KFactorByTierData[] kFactorByTier)
            {
                this.achievementsToShow = achievementsToShow;
                this.maxPlayers = maxPlayers;
                this.maxEMCImpact = maxEMCImpact;
                this.victoryStreaks = victoryStreaks;
                this.defeatStreaks = defeatStreaks;
                this.tiersData = tiersData;
                this.kFactorByTier = kFactorByTier;
            }

            /// <summary>
            /// Data for a leaderboard streak (victory or defeat).
            /// </summary>
            [Serializable]
            public class LeaderboardStreakData
            {
                [JsonProperty("streakScore")]
                public int streakScore;

                [JsonProperty("startStreak")]
                public int startStreak;

                [JsonProperty("targetStreak")]
                public int? targetStreak;

                public LeaderboardStreakData()
                {
                    streakScore = 0;
                    startStreak = 0;
                    targetStreak = null;
                }

                public LeaderboardStreakData(int startStreak, int? targetStreak)
                {
                    this.startStreak = startStreak;
                    this.targetStreak = targetStreak;
                }
            }

            /// <summary>
            /// K-Factor configuration per leaderboard tier.
            /// </summary>
            [Serializable]
            public class TiersData
            {
                [JsonProperty("leaderboardTier")]
                public LeaderboardTier leaderboardTier;

                [JsonProperty("scoreStartsAt")]
                public int scoreStartsAt;

                public TiersData()
                {
                }

                public TiersData(LeaderboardTier leaderboardTier, int scoreStartsAt)
                {
                    this.leaderboardTier = leaderboardTier;
                    this.scoreStartsAt = scoreStartsAt;
                }
            }
           
            
            /// <summary>
            /// K-Factor configuration per leaderboard tier.
            /// </summary>
            [Serializable]
            public class KFactorByTierData
            {
                [JsonProperty("leaderboardTier")]
                public LeaderboardTier leaderboardTier;

                [JsonProperty("kFactor")]
                public int kFactor;

                public KFactorByTierData()
                {
                    leaderboardTier = LeaderboardTier.None;
                    kFactor = 32;
                }

                public KFactorByTierData(LeaderboardTier leaderboardTier, int kFactor)
                {
                    this.leaderboardTier = leaderboardTier;
                    this.kFactor = kFactor;
                }
            }
        }

        /// <summary>
        /// Configuration for Tutorial system.
        /// </summary>
        [Serializable] 
        public class TutorialConfigData
        {
            [JsonProperty("tutorialStepDatas")]
            public TutorialStepData[] tutorialStepDatas;

            public TutorialConfigData()
            {
                tutorialStepDatas = Array.Empty<TutorialStepData>();
            }

            public TutorialConfigData(TutorialStepData[] tutorialStepDatas)
            {
                this.tutorialStepDatas = tutorialStepDatas;
            }

            /// <summary>
            /// Data for a single tutorial step.
            /// </summary>
            [Serializable] public class TutorialStepData
            {
                [JsonProperty("stepId")]
                public byte stepId;

                [JsonProperty("isCompleted")]
                public bool isCompleted;

                [JsonProperty("completedTime")]
                public long? completedTime; // Unix timestamp UTC

                public TutorialStepData()
                {
                    stepId = 0;
                    isCompleted = false;
                    completedTime = 0;
                }

                public TutorialStepData(byte stepId)
                {
                    this.stepId = stepId;
                    isCompleted = false;
                    completedTime = null;
                }

                public TutorialStepData(byte stepId, bool isCompleted, long? completedTime)
                {
                    this.stepId = stepId;
                    this.isCompleted = isCompleted;
                    this.completedTime = completedTime;
                }
            }
        }

        /// <summary>
        /// Configuration for Cosmetics system.
        /// </summary>
        [Serializable]
        public class CosmeticConfig
        {
            [JsonProperty("defaultCosmeticIDs")]
            public string[]? defaultCosmeticIDs;

            public CosmeticConfig()
            {
                defaultCosmeticIDs = Array.Empty<string>();
            }

            public CosmeticConfig(string[]? defaultCosmeticIDs)
            {
                this.defaultCosmeticIDs = defaultCosmeticIDs;
            }
        }

        /// <summary>
        /// Configuration for Friends system.
        /// </summary>
        [Serializable]
        public class FriendsConfig
        {
            [JsonProperty("friendshipsLimit")]
            public byte friendshipsLimit;
            
            [JsonProperty("requestLimit")]
            public byte requestLimit;
            
            [JsonProperty("blockLimit")]
            public byte blockLimit;

            public FriendsConfig()
            {
                friendshipsLimit = 50;
                requestLimit = 10;
                blockLimit = 10;
            }

            public FriendsConfig(byte friendshipsLimit, byte requestLimit, byte blockLimit)
            {
                this.friendshipsLimit = friendshipsLimit;
                this.requestLimit = requestLimit;
                this.blockLimit = blockLimit;
            }
        }

        /// <summary>
        /// Configuration for Notifications system.
        /// </summary>
        [Serializable]
        public class NotificationsConfig
        {
            [JsonProperty("notificationsLimit")]
            public byte notificationsLimit;

            public NotificationsConfig()
            {
                notificationsLimit = 20;
            }

            public NotificationsConfig(byte notificationsLimit)
            {
                this.notificationsLimit = notificationsLimit;
            }
        }

        /// <summary>
        /// Configuration for Clubs system.
        /// </summary>
        [Serializable]
        public class ClubsConfig
        {
            [JsonProperty("chatMessageLimit")]
            public int chatMessageLimit;

            [JsonProperty("winFactor")]
            public int winFactor;
            
            [JsonProperty("membersLimit")]
            public int membersLimit;
            
            [JsonProperty("applicantsLimit")]
            public int applicantsLimit;

            [JsonProperty("clubPermissionDatas")]
            public ClubPermissionData[]? clubPermissionDatas;

            [JsonProperty("minDaysBetweenClubCreation")]
            public int minDaysBetweenClubCreation;
            
            [JsonProperty("minDaysStreakToCreateClub")]
            public int minDaysStreakToCreateClub;
            
            [JsonProperty("minDaysRequiredToLeaveAClub")]
            public int minDaysRequiredToLeaveAClub;

            public ClubsConfig()
            {
                chatMessageLimit = 100;
                winFactor = 100;
                membersLimit = 20;
                applicantsLimit = 10;
                clubPermissionDatas = default;

                minDaysBetweenClubCreation = 10;
                minDaysStreakToCreateClub = 10;
                minDaysRequiredToLeaveAClub = 10;
            }

            [Serializable]
            public class ClubPermissionData
            {
                [JsonProperty("clubRanksType")]
                public ClubRanksTypes clubRanksType;
                
                [JsonProperty("clubPermissionsType")]
                public ClubPermissionsTypes clubPermissionsType;
            }
        }

        /// <summary>
        /// Configuration for Ads integration.
        /// </summary>
        [Serializable]
        public class AdsConfig
        {
            [JsonProperty("secondsDisabled")]
            public int secondsDisabled;
            
            [JsonProperty("secondsOmmited")]
            public int secondsOmmited;

            public AdsConfig()
            {
                secondsDisabled = 1200;
                secondsOmmited = 120;
            }
        }

        /// <summary>
        /// Configuration for purchases integration.
        /// </summary>
        [Serializable]
        public class PurchaseConfig
        {
            /// <summary>
            /// Global switch to enable or disable purchases.
            /// </summary>
            [JsonProperty("enabled")]
            public bool enabled;

            /// <summary>
            /// Time-to-live in minutes for pending subscription purchases.
            /// </summary>
            [JsonProperty("subscriptionPendingTtlMinutes")]
            public int subscriptionPendingTtlMinutes;

            /// <summary>
            /// Payment provider to be used for processing purchases.
            /// </summary>
            [JsonProperty("paymentProvider")]
            public PaymentProvider paymentProvider;

            /// <summary>
            /// List of products available for purchase.
            /// </summary>
            [JsonProperty("products")]
            public ProductConfig[] products;

            public PurchaseConfig()
            {
                enabled = true;
                paymentProvider = PaymentProvider.PayPal; // By default, use PayPal
                subscriptionPendingTtlMinutes = 180; // By default, 3 hours
                products = Array.Empty<ProductConfig>();
            }

            /// <summary>
            /// Configuration for a single product.
            /// </summary>
            [Serializable]
            public class ProductConfig
            {
                /// <summary>
                /// Internal product identifier (used by Unity & backend).
                /// </summary>
                [JsonProperty("productId")]
                public string productId;

                /// <summary>
                /// Price value as string to preserve decimal precision.
                /// Example: "9.99"
                /// </summary>
                [JsonProperty("price")]
                public string price;

                /// <summary>
                /// ISO 4217 currency code (USD, EUR, JPY, etc.).
                /// </summary>
                [JsonProperty("currency")]
                public string currency;

                /// <summary>
                /// Indicates if the product is a subscription.
                /// </summary>
                [JsonProperty("isSubscription")]
                public bool isSubscription;

                /// <summary>
                /// Number of days the subscription is valid for.<br></br>
                /// Only applicable if isSubscription is true.
                [JsonProperty("daysValidFor")]
                public int daysValidFor;

                /// <summary>
                /// Optional human-readable name (used for logs or UI).
                /// </summary>
                [JsonProperty("displayName")]
                public string displayName;

                /// <summary>
                /// Determines if the product can currently be purchased.
                /// </summary>
                [JsonProperty("active")]
                public bool active;

                public ProductConfig()
                {
                    productId = string.Empty;
                    price = "0.00";
                    currency = "USD";
                    isSubscription = false;
                    daysValidFor = 31;
                    displayName = string.Empty;
                    active = true;
                }
            }
        }

        /// <summary>
        /// Configuration for Nationality system.
        /// </summary>
        [Serializable]
        public class NationalityConfig
        {
            [JsonProperty("secondsToAbilityToUpdateNationality")]
            public int secondsToAbilityToUpdateNationality;

            public NationalityConfig()
            {
                secondsToAbilityToUpdateNationality = 120;
            }
        }
    }
}
