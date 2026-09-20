using UnityEngine;

namespace ProDomino.Shared
{
    public class Consts
    {
        public class CollectionKeys
        {
            public const string Config = "Config";
            public const string Colors = "Colors";
            public const string Analytics = "Analytics";
            public const string Achievements = "Achievements";
            public const string Ranks = "Ranks";
            public const string DominoSkins = "Domino_Skins";
            public const string GameMode = "Game_Modes";
            public const string Cosmetics = "Cosmetics";
            public const string TooltipLinkID = "TooltipLink";
            public const string ClubRankPromptID = "ClubRankPrompt";
            public const string NavigationPanelIcons = "NavigationPanelIcons";
            public const string Profile = "Profile";
            public const string Match = "Match";
            public const string Icons = "Icons";
            public const string Back = "Back";
            public const string Default = "Default";
            public const string Currency = "Currency";
            public const string Club = "Club";
            public const string RecentlyPlayed = "RecentlyPlayed";
            public const string Notifications = "Notifications";
            public const string IsGameNotification = "IsGameNotification";
            public const string JoinCode = "JoinCode";
            public const string Nationality = "Nationality";
            public const string GetTopNationalityRanking = "GetTopNationalityRanking";
            public const string GetClubData = "GetClubData";
            public const string GetClubChatData = "GetClubChatData";
            public const string GetPurchasesData = "GetPurchasesData";
            public const string SearchingClub = "SearchingClub";
            public const string SearchingUser = "SearchingUser";
            public const string SendingClubChatMessage = "SendingClubChatMessage";
            public const string SubscribingClubChatMessage = "SubscribingClubChatMessage";
            public const string UnsubscribingClubChatMessage = "UnsubscribingClubChatMessage";
            public const string GettingLeaderboard = "GettingLeaderboard";
            public const string Ads = "Ads";
            public const string MonthlySubscriptionProductId = "monthly_subscription";
        }

        public class LocalizationKeys
        {
            public const string SendClubJoiningRequest = "SendClubJoiningRequest";
            public const string MonthlySubscriptionButton = "monthly_subscription_button";
            public const string MonthlySubscriptionConfirmation = "monthly_subscription_confirmation";
            public const string GoogleAccountExistWithDifferentProvider = "google_account_exist_with_different_provider";
            public const string GoogleAccountExistWithDifferentCredential = "google_account_exist_with_different_credential";
            public const string GoogleDuplicatedAccount = "google_duplicated_account";
        }

        public class PlayerPrefs
        { 
            
            public const string GameConfig = "GameConfig";
            public const string LeaderboardCache = "LeaderboardCache";
        }

        public class Reasons
        { 
            public const string LeftingOwnWill = "LeftingOwnWill";
        }

        public class Colors
        { 
            public const string CloudCodeError = "#eb7f54";
            public const string Process = "#dbd591";
            public const string Error = "#bf1784";
            public const string Success = "#42d696";
            public const string TeamAColor = "#0398fc";
            public const string TeamBColor = "#f21b3f";

            public static Color TeamAColorValue
            {
                get
                {
                    ColorUtility.TryParseHtmlString(TeamAColor, out var color);
                    return color;
                }
            }

            public static Color TeamBColorValue
            {
                get
                {
                    ColorUtility.TryParseHtmlString(TeamBColor, out var color);
                    return color;
                }
            }
        }

        public class Clips
        { 
            public const string ShowUI = "ShowUI";
            public const string HideUI = "HideUI";
        }

        public class Bools
        {
            public const string IsShowing = "IsShowing";
        }
    }
}
