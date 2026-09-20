// AOTPreserveHelper.cs
// Purpose:
// - Force IL2CPP to preserve constructors and metadata for DTOs contained in external DLLs
// - Register the FirestoreTimestampConverter globally for Json.NET so converter resolution does not fail at runtime
//
// Usage:
// Put this file in your Unity project (Assets/Helpers/) and it will run before scene load.
// This is safe in editor and at runtime (IL2CPP / Mono).

using System;
using UnityEngine;
using Newtonsoft.Json;
using HelperSharedLibrary;

public static class AOTPreserveHelper
{
    // This runs before any scene is loaded and is ideal for forcing AOT type preservation.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreserveTypesForJson()
    {
        try
        {
            // ----- Touch the types directly so IL2CPP / the linker includes their constructors -----
            // We intentionally create instances here in the main project (not inside the external DLL)
            // so the Unity build process sees a direct reference.

            #region Responses
            // Firebase responses
            _ = new FirebaseBasicResponse();
            _ = new FirebaseAuthResponseData();
            _ = new FirebaseSendEmailResponse();
            _ = new FirebaseLookUpResponseData();

            _ = new FirebaseRequestErrorResponse();
            _ = new FirebaseErrorDetail();
            _ = new FirebaseUserSearchResult();
            _ = new RTDBPlayerLeaderboardDataResponse();

            // Cloud Save / UGS
            _ = new CloudSaveResponse();
            _ = new ListUsersResponse();
            _ = new ResponseData();
            _ = new DateContainer();
            _ = new Links();
            _ = new UGSAuthResponseData();
            _ = new UserData();
            _ = new ExternalId();
            _ = new UsernamePasswordLoginMetadata();
            _ = new UGSRequestErrorResponse();
            _ = new UGSErrorDetail();
            _ = new PurchaseDataResponse();
            _ = new PurchaseOrderResponse();

            // Missions
            _ = new MissionResponse();
            _ = new MissionClaimRewardResponse();

            // Achievements
            _ = new AchievementResponse();
            _ = new AchievementClaimRewardResponse();

            // Leaderboards
            _ = new LeaderboardResponse();
            _ = new LeaderboardData();
            _ = new GetLeaderboardEntriesAchievementsResponse();
            _ = new GetLeaderboardPlayerDataResponse();
            _ = new PlayerLeaderboardData();
            _ = new PlayerLeaderboardDataResult();
            _ = new PlayerAchievementResult();
            _ = new UpdateLeaderboardScoreByMatchResultResponseEntry();
            _ = new UpdateLeaderboardScoreByMatchResultResponse();

            // Cosmetics
            _ = new PurchaseCosmeticResponse();

            // Profile
            _ = new ProfileResponse();

            // Clubs / Firestore
            _ = new FirestoreClubDataResponse();
            _ = new FirestoreClubSearchResponse();
            _ = new FirestoreClubChatResponse();
            _ = new FirestoreTopClubsResponse();

            // Analytics
            _ = new AnalyticsDataResponse();
            #endregion

            #region Models
            // AnalyticsData
            _ = new AnalyticsData();
            _ = new AnalyticsData.LeaderboardRecord();

            // CloudPlayerClubData
            _ = new CloudPlayerClubData();

            // ConfigData
            _ = new ConfigData();
            _ = new ConfigData.MissionConfigData();
            _ = new ConfigData.LeaderboardConfigData();
            _ = new ConfigData.LeaderboardConfigData.LeaderboardStreakData();
            _ = new ConfigData.LeaderboardConfigData.KFactorByTierData();
            _ = new ConfigData.TutorialConfigData();
            _ = new ConfigData.TutorialConfigData.TutorialStepData();
            _ = new ConfigData.CosmeticConfig();
            _ = new ConfigData.FriendsConfig();
            _ = new ConfigData.NotificationsConfig();
            _ = new ConfigData.ClubsConfig();
            _ = new ConfigData.ClubsConfig.ClubPermissionData();
            _ = new ConfigData.AdsConfig();
            _ = new ConfigData.PurchaseConfig();
            _ = new ConfigData.PurchaseConfig.ProductConfig();

            // Firebase Auth
            _ = new FirebaseUserData();
            _ = new FirebaseProviderData();

            // Firebase RTDB
            _ = new RTDBPlayerLeaderboardData();

            // FirestoreClubChatData
            _ = new FirestoreClubChatData();
            _ = new FirestoreClubChatData.MessageData();

            // FirestoreClubData
            _ = new FirestoreClubData();
            _ = new FirestoreClubData.MemberData();
            _ = new FirestoreClubData.ApplicantData();
            _ = new FirestoreClubData.IconData();

            // FirestoreTimestampConverter
            _ = new FirestoreTimestampConverter();

            // GameAchievementData
            _ = new GameAchievementData();

            // GameCosmeticData
            _ = new GameCosmeticData();

            // GameMissionData
            _ = new GameMissionData();

            // GameTutorialData
            _ = new GameTutorialData();

            // LeaderboardAchievementData
            _ = new LeaderboardAchievementData();

            // PlayerAchievementData
            _ = new PlayerAchievementData();

            // PlayerAdData
            _ = new PlayerAdData();

            // PlayerCosmeticData
            _ = new PlayerCosmeticData();

            // PlayerMatchData
            _ = new PlayerMatchData();

            // PlayerMissionData
            _ = new PlayerMissionData();

            // PlayerNotificationData
            _ = new PlayerNotificationData();

            // PlayerProfileData
            _ = new PlayerProfileData();

            // PlayerPurchasesData
            _ = new PlayerPurchasesData();
            _ = new PlayerPurchasesData.PlayerPurchaseReceiptData();

            // PlayerRecentlyPlayedData
            _ = new PlayerRecentlyPlayedData();

            // PlayerTutorialData
            _ = new PlayerTutorialData();
            #endregion

            // If you have other DTOs that fail, add them here similarly:
            // _ = new AnotherDto();

            // ----- Register the converter globally for Json.NET -----
            // This ensures Json.NET has the converter instance available and doesn't try to instantiate via reflection.
            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings
                {
                    // Keep property name handling default; only adding converters explicitly.
                    Converters = { new FirestoreTimestampConverter() },
                    // You might want TypeNameHandling = TypeNameHandling.Auto if you use polymorphism
                };

                return settings;
            };

            Debug.Log("[AOTPreserveHelper] Preserved shared DTO types and registered FirestoreTimestampConverter.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AOTPreserveHelper] Error preserving types: {ex}");
        }
    }
}
