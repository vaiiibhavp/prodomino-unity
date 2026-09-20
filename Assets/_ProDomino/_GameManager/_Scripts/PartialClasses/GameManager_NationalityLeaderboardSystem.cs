using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms.Impl;
using static HelperSharedLibrary.Enums;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This partial class was create to content all info related to GlobalAnalyticSystem into the GameManager
    /// </summary>
    public partial class GameManager
    {
        private UnityEvent onGetTopLeaderboardData;

        public Dictionary<string, List<RTDBPlayerLeaderboardData>> TopNationalityRanking { get; private set; }

        /// <summary>
        /// Adds a listener to be invoked when the top leaderboard data is retrieved.
        /// </summary>
        /// <param name="eventToListen">The UnityAction to invoke when the top ranking event occurs.</param>
        public void AddListenerWhenGetTopRanking(UnityAction eventToListen)
        {
            if (onGetTopLeaderboardData is null)
            {
                Debug.LogWarning($"The event {nameof(onGetTopLeaderboardData)} is not initialized properly");
                return;
            }

            onGetTopLeaderboardData.AddListener(eventToListen);
        }
        
        /// <summary>
        /// Removes a listener from the onGetTopLeaderboardData event that is triggered when top leaderboard data is
        /// retrieved.
        /// </summary>
        /// <param name="eventToListen">The UnityAction delegate to remove from the event.</param>
        public void RemoveListenerWhenGetTopRanking(UnityAction eventToListen)
        {
            if (onGetTopLeaderboardData is null)
            {
                Debug.LogWarning($"The event {nameof(onGetTopLeaderboardData)} is not initialized properly");
                return;
            }

            onGetTopLeaderboardData.RemoveListener(eventToListen);
        }

        /// <summary>
        /// Updates analytics counters in batch mode.
        /// Each tuple defines one analytic change: (isAdding, globalType, gameMode).
        /// Prevents registering new game modes while one is already active.
        /// </summary>
        public async UniTask GetTopLeadeboard(string[] leaderboardsIds, NationalityType nationalityType)
        {
            // Check if already waiting for a previous request
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.GetTopNationalityRanking, out var isWaitingGetTopNationalityRanking) && isWaitingGetTopNationalityRanking)
            {
                Debug.LogWarning("Already waiting for a previous request to get club data");
                return;
            }

            // Only authenticated users can get the leaderboard data
            if (!IsAuthenticated)
            {
                Debug.LogWarning("[Leaderboard] User must be authenticated to update analytics.");
                return;
            }

            // Check if the leaderboard ID is valid
            if (leaderboardsIds is null or { Length: 0 })
            {
                Debug.LogWarning("[Leaderboard] Leaderboard IDs cannot be null or empty.");
                return;
            }

            // Default limit for the leaderboard
            var limit = 20;

            // Check if we have a custom limit for the leaderboard in the config data
            if (GameBackendConfigData is not null and { leaderboardConfig: not null })
                limit = GameBackendConfigData.leaderboardConfig.maxPlayers;

            Debug.Log($"[Leaderboard] Getting nationality '{nationalityType.ToString()}' ranking...");

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking] = true;

            // Check if the platform is valid for using JSlib (WebGL)
            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"[Leaderboard] Getting document from collection: {clubsCollectionIdKey}, document ID: {UGSPlayerClubName}, callback: {nameof(OnGetRankingSuccess)}, error callback: {nameof(OnGetRankingFailed)}");

                // Convert the leaderboard ID to JSON format to send it to the JS library
                var leaderboardsMap = JsonConvert.SerializeObject(leaderboardsIds);

                // Generate a wrapper to await the callback
                var getTopRankingWrapper = new Func<UniTask>(async () =>
                {
                    // Send to JS library
                    FirebaseDatabase.GetTopLeaderboards(
                        leaderboardIdsJson: leaderboardsMap,
                        nationality: nationalityType.ToString(),
                        limit: limit,
                        gameObject.name,
                        nameof(OnGetRankingSuccess),
                        nameof(OnGetRankingFailed)
                    );

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: getTopRankingWrapper,
                     taskId: nameof(getTopRankingWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking] = false;
            } 
            else
            {
                Debug.LogWarning("[Leaderboard] RTDB client is only available on WebGL platform - Simulating getting nationality ranking in editor");
                var dataEncrypted = authManager.SerializeAndEncryptData(new()
                {
                    ["leaderboardIds"] = leaderboardsIds,
                    ["nationalityType"] = nationalityType.ToString(),
                });

                var clubEncryptedData = await HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToGetNationalityLeaderboard(dataEncrypted).AsUniTask(),
                    taskId: nameof(module.TryToGetNationalityLeaderboard),
                    showLoading: true);

                var leaderboardDataResponse = default(RTDBPlayerLeaderboardDataResponse);

                try
                {
                    leaderboardDataResponse = authManager.DeserializeAndDecryptData<RTDBPlayerLeaderboardDataResponse>(clubEncryptedData);

                    if (leaderboardDataResponse is null)
                    {
                        OnGetRankingFailed("[Leaderboard] Failed to deserialize RTDB leaderboard data response.");
                        return;
                    }

                    TopNationalityRanking = leaderboardDataResponse.rankingByLeaderboard;
                    if (TopNationalityRanking is not null)
                        Debug.Log($"[Leaderboard] Successfully retrieved nationality leaderboard data");
                    else
                        Debug.LogWarning("[Leaderboard] Failed to deserialize nationality leaderboard data from RTDB.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Leaderboard] Exception while deserializing RTDB nationality leaderboard data response: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking] = false;
                }
            }
        }



        /// <summary>
        /// Updates analytics counters in batch mode.
        /// Each tuple defines one analytic change: (isAdding, globalType, gameMode).
        /// Prevents registering new game modes while one is already active.
        /// </summary>
        public async UniTask<UpdateNationalityResponse> TryToSetNationality(NationalityType nationalityType)
        {
            // Only authenticated users can get the leaderboard data
            if (!IsAuthenticatedAndVerified)
            {
                Debug.LogWarning("[Leaderboard] User must be authenticated to set its nationality.");
                return default;
            }

            if (nationalityType == NationalityData?.nationalityType)
            {
                Debug.LogWarning("[Leaderboard] User already has the selected nationality.");
                return default;
            }

            // Encrypt the data to send to the module
            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["newNationality"] = nationalityType,
            });

            try
            {
                // Override the player nationality in the protected UGS player data.
                var updateNationalityResponseEncrypted = await HandleProcess_GameManagerProxy
                    (uniTask: () => module.UpdateNationalityAsync(dataEncrypted).AsUniTask(),
                    taskId: nameof(module.UpdateNationalityAsync),
                    showLoading: true,
                    returnExceptionOnError: true);

                var updateNationalityResponse = authManager.DeserializeAndDecryptData<UpdateNationalityResponse>(updateNationalityResponseEncrypted);

                // Check if the response is valid
                if (updateNationalityResponse is null)
                {
                    Debug.LogError("Failed to deserialize the response from the module when trying to update the nationality data.");
                    return new UpdateNationalityResponse(null, "An error occurred while trying to update the nationality data. Try again later.");
                }

                // Check if the module returned any error message
                if (!string.IsNullOrEmpty(updateNationalityResponse.message))
                {
                    Debug.LogError($"Failed to update the nationality data: {updateNationalityResponse.message}");
                    return updateNationalityResponse;
                }

                // Check if the module returned the updated nationality data
                if (updateNationalityResponse.newNationality is null)
                {
                    Debug.LogError("The module did not return the updated nationality data.");
                    return updateNationalityResponse;
                }

                // Check if the nationality returned from the module is the same as the one sent.
                // If not, it means something went wrong in the module when trying to update
                if (updateNationalityResponse.newNationality.nationalityType != nationalityType)
                {
                    Debug.LogError($"The nationality returned from the module is different from the one sent. Sent: {nationalityType}, Returned: {updateNationalityResponse.newNationality}");
                    return updateNationalityResponse;
                }

                // Update the national type in the player data and refresh it to update all the systems that depends on it (e.g., leaderboard)
                UpdateNationalityExternally(updateNationalityResponse.newNationality);

                return updateNationalityResponse;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Leaderboard] Exception while trying to set nationality: {ex.Message}");
                return new UpdateNationalityResponse(null, $"An error occurred while trying to set nationality. Try again later.");
            }
        }

        /// <summary>
        /// Parses leaderboard ranking data from a JSON string, updates global analytics data, and triggers a
        /// leaderboard data event.
        /// </summary>
        /// <param name="jsonData">A JSON string containing leaderboard ranking data.</param>
        public void OnGetRankingSuccess(string jsonData)
        {
            waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking] = false;

            try
            {
                // Step 1: Deserialize as list
                var list = JsonConvert.DeserializeObject<List<LeaderboardWrapper>>(jsonData);

                // Step 2: Convert to dictionary
                TopNationalityRanking = list.ToDictionary(
                    x => x.leaderboardId,
                    x => x.entries
                );

                Debug.Log($"[Leaderboard] Leaderboard got successfully. \n\nLeaderboard:\n{jsonData}");
            }
            catch (Exception ex)
            {
                TopNationalityRanking = null;
                Debug.LogWarning($"[Leaderboard] Could not parse returned data: {ex.Message}");
            }

            onGetTopLeaderboardData?.Invoke();
        }

        /// <summary>
        /// Logs an error message when retrieving the leaderboard ranking fails.
        /// </summary>
        /// <param name="error">The error message describing the failure.</param>
        public void OnGetRankingFailed(string error)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetTopNationalityRanking] = false;

            Debug.LogError($"[Leaderboard] Failed to get ranking: {error}");
            TopNationalityRanking = null;

            // Inform any listener that the leaderboard data retrieval has failed, so they can update accordingly (e.g., show an error message or empty state)
            onGetTopLeaderboardData?.Invoke();
        }

        [Serializable]
        public class LeaderboardWrapper
        {
            public string leaderboardId;
            public List<RTDBPlayerLeaderboardData> entries;
        }

    }
}
