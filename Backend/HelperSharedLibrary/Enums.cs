using System;

namespace HelperSharedLibrary
{
    public class Enums
    {
        public enum Currency
        {
            None = 0,
            Token = 1,
        }

        public enum AnalyticType
        {
            None = 0,

            Bonus = 5,

            MissionsCompleted = 10,
            AchievementsCompleted = 11,

            PlaceTilesOnBoard = 40,
            GamesPlayed = 50,
            CompleteTutorial = 60,

            TotalWins = 200,
            TotalCompetitiveWins = 220,
            ConcentrateMatchTiles = 460,

            OnTokenModified = 500,
            AddFriends = 600,
            ObtainCosmetic = 620,

            SelectCosmetic = 700
        }

        public enum CosmeticType
        {
            None = 0,
            Icons = 1,
            Tiles = 2,
            Boards = 3,
            Fund = 4,
            Badges = 5,
        }

        public enum CosmeticPurchaseMethod
        {
            None = 0,
            Default = 1, // Player has it by default
            Shop = 2, // Purchased with shop
            Achievement = 3, // Obtained through achievements
            RealMoney = 4, // Purchased with real money
        }

        public enum LeaderboardTier
        {
            None = 0,
            ClassC = 1,
            ClassB = 2,
            ClassA = 3,
            Expert = 4,
            Master = 5,
            GrandMaster = 6,
        }

        public enum Achievement
        {
            None = 0,

            #region ## ReachLeaderboardTier
            ReachClassC_Block = 100,
            ReachClassB_Block = 101,
            ReachClassA_Block = 102,
            ReachExpert_Block = 103,
            ReachMaster_Block = 104,
            ReachGrandMaster_Block = 105,

            ReachClassC_French = 200,
            ReachClassB_French = 201,
            ReachClassA_French = 202,
            ReachExpert_French = 203,
            ReachMaster_French = 204,
            ReachGrandMaster_French = 205,

            ReachClassC_Five = 300,
            ReachClassB_Five = 301,
            ReachClassA_Five = 302,
            ReachExpert_Five = 303,
            ReachMaster_Five = 304,
            ReachGrandMaster_Five = 305,

            ReachClassC_Concentrate = 400,
            ReachClassB_Concentrate = 401,
            ReachClassA_Concentrate = 402,
            ReachExpert_Concentrate = 403,
            ReachMaster_Concentrate = 404,
            ReachGrandMaster_Concentrate = 405,

            ReachClassC_Draw = 500,
            ReachClassB_Draw = 501,
            ReachClassA_Draw = 502,
            ReachExpert_Draw = 503,
            ReachMaster_Draw = 504,
            ReachGrandMaster_Draw = 502,
            #endregion

            #region ## ObtainCosmetic
            Obtain50Cosmetics = 1000,
            #endregion

            #region ## PlaceTiles
            Place1000TilesAcrossAllModes = 1100,
            #endregion

            #region ## AddFriends
            Add20Friends = 1200,
            #endregion

            #region ## CompleteTutorial
            CompleteTutorial_Block = 2000,
            CompleteTutorial_French = 2001,
            CompleteTutorial_Draw = 2002,
            CompleteTutorial_Five = 2003,
            CompleteTutorial_Concentrate = 2004,
            CompleteEveryTutorial = 2005,
            #endregion
        }

        public enum AchievementType
        {
            None = 0,
            ReachLeaderboardTier = 1, // Reaching a specific leaderboard tier
            ObtainCosmetic = 2, // Obtaining a cosmetic item
            PlaceTiles = 3, // Placing a certain number of tiles across all game modes
            AddFriends = 4, // Adding a certain number of friends
            CompleteTutorial = 5, // Completing a specific tutorial or all tutorials
        }

        public enum AchievementRank
        {
            None = 0,
            Star = 1,
            DoubleStar = 2,
            TripleStar = 3,
        }

        [Flags]
        public enum CosmeticRarity
        {
            None = 0,

            Common = 1 << 0, // 1
            Uncommon = 1 << 1, // 2
            Rare = 1 << 2, // 4
            Mythic = 1 << 3, // 8
            Legendary = 1 << 4, // 16
            Special = 1 << 5, // 32

            All = Common | Uncommon | Rare | Mythic | Legendary | Special // 126
        }

        [Flags]
        public enum GameModeFilter
        {
            None = 0,
            French = 1 << 0, // 1
            Block = 1 << 1, // 2
            Draw = 1 << 2, // 4
            Five = 1 << 3, // 8
            Concentrate = 1 << 4, // 16

            All = French | Block | Draw | Five | Concentrate // 31
        }

        public enum NotificationType
        {
            Simple = 0,
            Reward = 1,
            FriendRequest = 2,
            PartyInvite = 3,
        }

        public enum ClubRanksTypes
        {
            Member = 0,
            Official = 1,
            GuildMaster = 2,
        }

        [Flags]
        public enum ClubPermissionsTypes
        {
            None = 0,

            AcceptApplicant = 1 << 0, // 1
            DeclineApplicant = 1 << 1, // 2
            RemoveMember = 1 << 2,     // 4
            ChangeLogo = 1 << 3,       // 8
            ChangeSlogan = 1 << 4,     // 16
            OverrideRank = 1 << 5,     // 32

            All = AcceptApplicant | DeclineApplicant | RemoveMember | ChangeLogo | ChangeSlogan | OverrideRank // 63
        }


        public enum ClubUpdateType
        {
            None = 0,
            Slogan = 1,
            Icon = 2,
        }

        public enum ClubResponseCodeType
        {
            None = 0,
            Success = 1,

            ClubNotFound = 2,
            ClubFull = 3,

            AlreadyInClub = 4,
            NotInClub = 5,

            NoPermission = 6,

            InvalidRank = 7,
            InvalidIcon = 8,
            InvalidSlogan = 9,
            InvalidName = 10,

            AlreadyRequested = 11,
            NotRequested = 12,

            InvalidConfiguration = 13,
            UnknownIssue = 14,
        }

        public enum NationalityType
        {
            International = 0,
            Afghanistan = 1,
            Albania = 2,
            Algeria = 3,
            Andorra = 4,
            Angola = 5,
            AntiguaAndBarbuda = 6,
            Argentina = 7,
            Armenia = 8,
            Australia = 9,
            Austria = 10,
            Azerbaijan = 11,
            Bahamas = 12,
            Bahrain = 13,
            Bangladesh = 14,
            Barbados = 15,
            Belarus = 16,
            Belgium = 17,
            Belize = 18,
            Benin = 19,
            Bhutan = 20,
            Bolivia = 21,
            BosniaAndHerzegovina = 22,
            Botswana = 23,
            Brazil = 24,
            BruneiDarussalam = 25,
            Bulgaria = 26,
            BurkinaFaso = 27,
            Burundi = 28,
            CaboVerde = 29,
            Cambodia = 30,
            Cameroon = 31,
            Canada = 32,
            CentralAfricanRepublic = 33,
            Chad = 34,
            Chile = 35,
            China = 36,
            Colombia = 37,
            Comoros = 38,
            CongoBrazzaville = 39,
            CongoKinshasa = 40,
            CostaRica = 41,
            CoteDIvoire = 42,
            Croatia = 43,
            Cuba = 44,
            Cyprus = 45,
            CzechRepublic = 46,
            Denmark = 47,
            Djibouti = 48,
            Dominica = 49,
            DominicanRepublic = 50,
            Ecuador = 51,
            Egypt = 52,
            ElSalvador = 53,
            EquatorialGuinea = 54,
            Eritrea = 55,
            Estonia = 56,
            Eswatini = 57,
            Ethiopia = 58,
            Fiji = 59,
            Finland = 60,
            France = 61,
            Gabon = 62,
            Gambia = 63,
            Georgia = 64,
            Germany = 65,
            Ghana = 66,
            Greece = 67,
            Grenada = 68,
            Guatemala = 69,
            Guinea = 70,
            GuineaBissau = 71,
            Guyana = 72,
            Haiti = 73,
            Honduras = 74,
            Hungary = 75,
            Iceland = 76,
            India = 77,
            Indonesia = 78,
            Iran = 79,
            Iraq = 80,
            Ireland = 81,
            Israel = 82,
            Italy = 83,
            Jamaica = 84,
            Japan = 85,
            Jordan = 86,
            Kazakhstan = 87,
            Kenya = 88,
            Kiribati = 89,
            KoreaNorth = 90,
            KoreaSouth = 91,
            Kuwait = 92,
            Kyrgyzstan = 93,
            Laos = 94,
            Latvia = 95,
            Lebanon = 96,
            Lesotho = 97,
            Liberia = 98,
            Libya = 99,
            Liechtenstein = 100,
            Lithuania = 101,
            Luxembourg = 102,
            Madagascar = 103,
            Malawi = 104,
            Malaysia = 105,
            Maldives = 106,
            Mali = 107,
            Malta = 108,
            MarshallIslands = 109,
            Mauritania = 110,
            Mauritius = 111,
            Mexico = 112,
            Micronesia = 113,
            Moldova = 114,
            Monaco = 115,
            Mongolia = 116,
            Montenegro = 117,
            Morocco = 118,
            Mozambique = 119,
            Myanmar = 120,
            Namibia = 121,
            Nauru = 122,
            Nepal = 123,
            Netherlands = 124,
            NewZealand = 125,
            Nicaragua = 126,
            Niger = 127,
            Nigeria = 128,
            NorthMacedonia = 129,
            Norway = 130,
            Oman = 131,
            Pakistan = 132,
            Palau = 133,
            Panama = 134,
            PapuaNewGuinea = 135,
            Paraguay = 136,
            Peru = 137,
            Philippines = 138,
            Poland = 139,
            Portugal = 140,
            Qatar = 141,
            Romania = 142,
            Russia = 143,
            Rwanda = 144,
            SaintKittsAndNevis = 145,
            SaintLucia = 146,
            SaintVincentAndTheGrenadines = 147,
            Samoa = 148,
            SanMarino = 149,
            SaoTomeAndPrincipe = 150,
            SaudiArabia = 151,
            Senegal = 152,
            Serbia = 153,
            Seychelles = 154,
            SierraLeone = 155,
            Singapore = 156,
            Slovakia = 157,
            Slovenia = 158,
            SolomonIslands = 159,
            Somalia = 160,
            SouthAfrica = 161,
            SouthSudan = 162,
            Spain = 163,
            SriLanka = 164,
            Sudan = 165,
            Suriname = 166,
            Sweden = 167,
            Switzerland = 168,
            Syria = 169,
            Taiwan = 170,
            Tajikistan = 171,
            Tanzania = 172,
            Thailand = 173,
            TimorLeste = 174,
            Togo = 175,
            Tonga = 176,
            TrinidadAndTobago = 177,
            Tunisia = 178,
            Turkey = 179,
            Turkmenistan = 180,
            Tuvalu = 181,
            Uganda = 182,
            Ukraine = 183,
            UnitedArabEmirates = 184,
            UnitedKingdom = 185,
            UnitedStates = 186,
            Uruguay = 187,
            Uzbekistan = 188,
            Vanuatu = 189,
            VaticanCity = 190,
            Venezuela = 191,
            Vietnam = 192,
            Yemen = 193,
            Zambia = 194,
            Zimbabwe = 195
        }
    }
}
