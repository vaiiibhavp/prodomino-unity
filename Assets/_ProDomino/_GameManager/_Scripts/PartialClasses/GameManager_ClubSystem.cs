using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Timba.Utils;
using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This partial class was create to content all info related to ClubSystem into the GameManager
    /// </summary>
    public partial class GameManager
    {
        [HideInInspector]
        public AsyncEventHandler<FirestoreClubChatData> onGetClubChatData = new();

        public FirestoreClubData PlayerClubData { get; private set; }
        public FirestoreClubChatData PlayerClubChatData { get; private set; }

        public Dictionary<string, SearchDataModel> SearchedClubsCollection { get; private set; }
        public FirestoreClubData[] ClubLeaderboard { get; private set; }
        public string UGSPlayerClubName { get; private set; }
        public bool WasLastClubMessageSentProperly { get; private set; }
        public bool WasSubscribeToClubChatProperly { get; private set; }
        public bool WasUnsubscribeToClubChatProperly { get; private set; }

        private void Awake_ClubSystem()
        {
            onGetClubChatData ??= new();
        }

        private bool IsPlayerInClub() =>
            PlayerClubData is not null &&
            PlayerClubData.members.Any(x => x.unityMemberId == authManager.UUID);

        /// <summary>
        /// Overrides the current Firestore player club data with the provided data.
        /// </summary>
        public void OverrideFirestorePlayerClubData(FirestoreClubData firestorePlayerClubData)
        {
            PlayerClubData = firestorePlayerClubData;
        }

        /// <summary>
        /// Overrides the current Firestore player club name with the provided name.
        /// </summary>
        /// <param name="clubName"></param>
        public void OverrideFirestorePlayerClubName(string clubName)
        {
            UGSPlayerClubName = clubName;
        }

        /// <summary>
        /// Resets the last leaderboard fetch time to force a new fetch on the next request.
        /// </summary>
        public void StartFetchingClubLeaderboard()
        {
            lastLeaderboardFetchTime = DateTime.MinValue;
        }

        /// <summary>
        /// Cleans the club search cache to keep memory and relevance in check.
        /// Keeps only the last 30 searches made in the last 5 minutes.
        /// </summary>
        private void CleanSearchCollection()
        {
            // Ensure collection is initialized
            if (SearchedClubsCollection is null)
                SearchedClubsCollection = new();

            // Skip cleaning if small enough
            if (SearchedClubsCollection.Count < 30)
                return;

            // Filter recent results and keep only 30 most recent
            var filtered = SearchedClubsCollection
                .Where(x => (DateTime.UtcNow - x.Value.searchTime).TotalMinutes <= 5)
                .OrderByDescending(x => x.Value.searchTime)
                .Take(30)
                .ToDictionary(x => x.Key, x => x.Value);

            // Replace contents safely
            SearchedClubsCollection.Clear();
            foreach (var kv in filtered)
                SearchedClubsCollection[kv.Key] = kv.Value;
        }

        #region Refresh Firestore Player Models
        /// <summary>
        /// Refreshes the Firestore player club data if the player belongs to a club.
        /// </summary>
        public async UniTask RefreshFirestoreClubData()
        {
            // Check if already waiting for a previous request
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.GetClubData, out var isWaitingGetClubData) && isWaitingGetClubData)
            {
                Debug.LogWarning("Already waiting for a previous request to get club data");
                return;
            }

            // Check if the player has a club
            if (string.IsNullOrEmpty(UGSPlayerClubName))
            {
                Debug.LogWarning("Player does not belong to any club or its data is not updated");
                return;
            }

            Debug.Log($"Fetching club data for club: {UGSPlayerClubName}");

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.GetClubData] = true;

            // Check if the platform is valid for using JSlib (WebGL)
            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Getting document from collection: {clubsCollectionIdKey}, document ID: {UGSPlayerClubName}, callback: {nameof(OnGetClubData)}, error callback: {nameof(OnFailToGetClubData)}");

                // Generate a wrapper to await the callback
                var getDocumentWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.GetDocument
                        (clubsCollectionIdKey,
                        UGSPlayerClubName,
                        gameObject.name,
                        nameof(OnGetClubData),
                        nameof(OnFailToGetClubData));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.GetClubData])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: getDocumentWrapper,
                     taskId: nameof(getDocumentWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.GetClubData] = false;
            } 
            else
            {
                Debug.LogWarning("Firestore client is only available on WebGL platform - Simulating club data fetch in editor");
                var dataEncrypted = authManager.SerializeAndEncryptData(new()
                {
                    ["clubName"] = UGSPlayerClubName,
                });

                var clubEncryptedData = await HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToGetClubData(dataEncrypted).AsUniTask(),
                    taskId: nameof(module.TryToGetClubData),
                    showLoading: true);

                var clubDataResponse = default(FirestoreClubDataResponse);

                try
                {
                    clubDataResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(clubEncryptedData);

                    if (clubDataResponse is null)
                    {
                        OnFailToGetClubData("Failed to deserialize Firestore club data response.");
                        return;
                    }

                    PlayerClubData = clubDataResponse.playerClubData;
                    if (PlayerClubData is not null)
                        Debug.Log($"Successfully retrieved club data for club: {UGSPlayerClubName} with {PlayerClubData.members?.Count ?? 0} members.");
                    else
                        Debug.LogWarning("Failed to deserialize club data from Firestore.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception while deserializing Firestore club data response: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.GetClubData] = false;
                }
            }
        }

        /// <summary>
        /// Callback method for handling the retrieved club data from Firestore.
        /// </summary>
        private void OnGetClubData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetClubData] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty club data from Firestore.");
                return;
            }

            // Trt to deserialize the output to FirestoreClubData
            try
            {
                Debug.Log($"Club output: \n\n{output}");

                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                PlayerClubData = JsonConvert.DeserializeObject<FirestoreClubData>(output, settings);
                if (PlayerClubData is not null)
                {
                    Debug.Log($"Successfully retrieved club data for club: {PlayerClubData.clubName} with {PlayerClubData.members?.Count ?? 0} members.");

                    // Get the club data from Firestore
                    if (IsValidPlatformToUseJSlib())
                        FirebaseFirestore.GetClubRank
                            (clubsCollectionIdKey,
                            PlayerClubData.clubName,
                            gameObject.name,
                            nameof(OnClubRankReceived),
                            nameof(OnClubRankFailed));
                } else
                    Debug.LogWarning("Failed to deserialize club data from Firestore.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while deserializing club data from Firestore: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback method for handling failures in retrieving club data from Firestore.
        /// </summary>
        /// <param name="output"></param>
        private void OnFailToGetClubData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetClubData] = false;
            PlayerClubData = null;

            Debug.LogWarning($"Failed to get club data from Firestore: {output}");
        }

        /// <summary>
        /// Callback method for handling the retrieved club rank from Firestore.
        /// </summary>
        public void OnClubRankReceived(string rankStr)
        {
            // Try to get the rank as integer
            if (int.TryParse(rankStr, out var rank))
            {
                Debug.Log($"Club rank: {rank}");

                if (PlayerClubData is not null)
                    PlayerClubData.clubRank = rank;
                else
                    Debug.LogWarning("Failed to assign new rank because player club data is null");
            } else
                Debug.LogWarning("Failed to parse rank.");
        }

        /// <summary>
        /// Callback method for handling failures in retrieving club rank from Firestore.
        /// </summary>
        public void OnClubRankFailed(string error)
        {
            Debug.LogError($"Failed to get club rank: {error}");
        }
        #endregion
        
        #region Firestore Club Chat Methods
        /// <summary>
        /// Refreshes the Firestore player club data if the player belongs to a club.
        /// </summary>
        public async UniTask RefreshFirestoreClubChatData()
        {
            // Check if already waiting for a previous request
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.GetClubChatData, out var isWaitingGetClubChatData) && isWaitingGetClubChatData)
            {
                Debug.LogWarning("Already waiting for a previous request to get club chat data");
                return;
            }

            // Check if the player has a club
            if (string.IsNullOrEmpty(UGSPlayerClubName))
            {
                Debug.LogWarning("Player does not belong to any club or its data is not updated");
                return;
            }

            Debug.Log($"Fetching club chat data for club: {UGSPlayerClubName}");

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.GetClubChatData] = true;

            // Check if the platform is valid for using JSlib (WebGL)
            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Getting club chat data from collection: {clubsChatsCollectionIdKey}, document ID: {UGSPlayerClubName}, callback: {nameof(OnGetClubChatData)}, error callback: {nameof(OnFailToGetClubChatData)}");

                var getClubChatDataWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.GetDocument
                        (clubsChatsCollectionIdKey,
                        UGSPlayerClubName,
                        gameObject.name,
                        nameof(OnGetClubChatData),
                        nameof(OnFailToGetClubChatData));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.GetClubChatData])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: getClubChatDataWrapper,
                     taskId: nameof(getClubChatDataWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.GetClubChatData] = false;
            } 
            else
            {
                Debug.LogWarning("Firestore client is only available on WebGL platform - Simulating club data fetch in editor");
                var dataEncrypted = authManager.SerializeAndEncryptData(new()
                {
                    ["clubName"] = UGSPlayerClubName,
                });

                var clubEncryptedData = await HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToGetClubChatData(dataEncrypted).AsUniTask(),
                    taskId: nameof(module.TryToGetClubChatData),
                    showLoading: true);

                var clubChatDataResponse = default(FirestoreClubChatResponse);

                try
                {
                    clubChatDataResponse = authManager.DeserializeAndDecryptData<FirestoreClubChatResponse>(clubEncryptedData);
                    if (clubChatDataResponse is null)
                    {
                        OnFailToGetClubChatData("Failed to deserialize Firestore club chat data response.");
                        return;
                    }

                    PlayerClubChatData = clubChatDataResponse.clubChatData;
                    if (PlayerClubChatData is not null)
                        Debug.Log($"Successfully retrieved club chat data for club: {PlayerClubChatData.clubName} with {PlayerClubChatData.messages?.Count ?? 0 } messages in total.");
                    else
                        Debug.LogWarning("Failed to deserialize club chat data from Firestore.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception while deserializing Firestore club chat data response: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.GetClubChatData] = false;
                }
            }
        }

        /// <summary>
        /// Callback method for handling the retrieved club data from Firestore.
        /// </summary>
        private void OnGetClubChatData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetClubChatData] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty club chat data from Firestore.");
                return;
            }

            // Trt to deserialize the output to FirestoreClubChatData
            try
            {
                Debug.Log($"Club chat output: \n\n{output}");

                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                PlayerClubChatData = JsonConvert.DeserializeObject<FirestoreClubChatData>(output, settings);
                if (PlayerClubChatData is not null)
                { 
                    Debug.Log($"Successfully retrieved club chat data for club: {PlayerClubChatData.clubName} with {PlayerClubChatData.messages?.Count ?? 0 } messages in total.");
                    onGetClubChatData?.InvokeAllAtTimeAsync(PlayerClubChatData);
                }
                else
                    Debug.LogWarning("Failed to deserialize club chat data from Firestore.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while deserializing club chat data from Firestore: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback method for handling failures in retrieving club data from Firestore.
        /// </summary>
        /// <param name="output"></param>
        private void OnFailToGetClubChatData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetClubChatData] = false;
            PlayerClubChatData = null;

            Debug.LogWarning($"Failed to get club data from Firestore: {output}");
        }
        #endregion

        #region Search Clubs By Name Models
        /// <summary>
        /// Tries to search clubs by name.
        /// </summary>
        public async UniTask<FirestoreClubData[]> TryToSearchClubsByName(string partialName)
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.SearchingClub, out var isWaitingSearchingClub) && isWaitingSearchingClub)
            {
                Debug.LogWarning("Already waiting for a previous request to search clubs");
                return default;
            }

            // Validate the partial name
            if (string.IsNullOrEmpty(partialName))
            {
                Debug.LogWarning("Partial name is null or empty");
                return default;
            }

            lastNormalizedSearchTerm = NormalizeName(partialName);
            Debug.Log($"Searching clubs with normalized partial name: {lastNormalizedSearchTerm}");

            // After the cleaning, check if we have a cached result for the same search term
            if (SearchedClubsCollection.TryGetValue(lastNormalizedSearchTerm, out var cachedSearchDataModel))
            {
                Debug.Log($"Returning cached search results for term: {lastNormalizedSearchTerm} with {cachedSearchDataModel.clubsFound?.Count ?? 0} clubs found.");
                return cachedSearchDataModel.clubsFound.ToArray();
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.SearchingClub] = true;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Searching clubs in collection: {clubsCollectionIdKey}, normalized partial name: {lastNormalizedSearchTerm}, callback: {nameof(OnSearchClubsByNameReceived)}, error callback: {nameof(OnSearchClubsByNameFailed)}");
                
                var getClubsByNameWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.SearchClubsByName
                        (clubsCollectionIdKey,
                        lastNormalizedSearchTerm,
                        gameObject.name,
                        nameof(OnSearchClubsByNameReceived),
                        nameof(OnSearchClubsByNameFailed));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.SearchingClub])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore using the wrapper
                await HandleProcess_GameManagerProxy
                     (uniTask: getClubsByNameWrapper,
                     taskId: nameof(getClubsByNameWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.SearchingClub] = false;
            } 
            else
            {
                Debug.LogWarning("Firestore client is only available on WebGL platform - Simulating club search in editor");

                var dataEncrypted = authManager.SerializeAndEncryptData(new()
                {
                    ["partialName"] = lastNormalizedSearchTerm,
                });

                // Store the variables to use the trycat pattern to store the response
                var clubsSearchEncryptedData = default(string);
                var clubsSearchResponse = default(FirestoreClubSearchResponse);

                try
                {
                    // Use the backend binding of the corresponding player to change its rank
                    clubsSearchEncryptedData = await HandleProcess_GameManagerProxy
                        (uniTask: () => module.TryToSearchClubsByName(dataEncrypted).AsUniTask(), 
                        taskId: nameof(module.TryToSearchClubsByName), 
                        showLoading: true,
                        returnExceptionOnError: true);

                    // Deserialize and decrypt the data
                    clubsSearchResponse = authManager.DeserializeAndDecryptData<FirestoreClubSearchResponse>(clubsSearchEncryptedData);

                    if (clubsSearchResponse is null)
                    {
                        OnSearchClubsByNameFailed("Failed to deserialize Firestore club Search response.");
                        return null;
                    }

                    // Cache the search result
                    SearchedClubsCollection[lastNormalizedSearchTerm] = new(clubsSearchResponse.clubsFound ?? new(), DateTime.UtcNow);

                    CleanSearchCollection();
                    Debug.Log($"Successfully retrieved {clubsSearchResponse.clubsFound?.Count ?? 0} clubs matching the search term.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception occurred while trying to override member rank: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.SearchingClub] = false;
                }
            }

            return SearchedClubsCollection.TryGetValue(lastNormalizedSearchTerm, out var clubSearchDataModel) 
                ? clubSearchDataModel.clubsFound.ToArray() 
                : null;

            /*
            Normalizes a club name for Firestore search and indexing.
            Removes accents, diacritics, and special characters, then lowercases.
            */
            string NormalizeName(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return string.Empty;

                // 1. Convert to lowercase
                string lower = input.ToLowerInvariant();

                // 2. Decompose accented characters
                string normalized = lower.Normalize(NormalizationForm.FormD);

                // 3. Remove diacritic marks using LINQ filtering
                var filtered = new string(normalized
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray());

                // 4. Remove any remaining special characters (keep letters, digits, and spaces)
                filtered = Regex.Replace(filtered, @"[^a-z0-9\s]", string.Empty);

                // 5. Trim and collapse multiple spaces into one
                filtered = Regex.Replace(filtered.Trim(), @"\s+", " ");

                return filtered;
            }
        }

        /// <summary>
        /// Callback method for handling the retrieved clubs data from Firestore.
        /// </summary>
        private void OnSearchClubsByNameReceived(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.SearchingClub] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty club data from Firestore.");
                return;
            }

            // Try to deserialize the output to a list of FirestoreClubData
            try
            {
                Debug.Log($"[OnSearchClubsByNameReceived] Search output: \n\n{output}");
                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                var clubsFound = JsonConvert.DeserializeObject<List<FirestoreClubData>>(output, settings);

                // Cache the search result
                SearchedClubsCollection[lastNormalizedSearchTerm] = new(clubsFound ?? new(), DateTime.UtcNow);

                // Clean the collection if it exceeds the limit
                CleanSearchCollection();
                Debug.Log($"Successfully retrieved {clubsFound?.Count ?? 0} clubs matching the search term.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while deserializing Firestore club data response: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback method for handling failures in retrieving clubs data from Firestore.
        /// </summary>
        private void OnSearchClubsByNameFailed(string output)
        {
            waitingDictionary[Consts.CollectionKeys.SearchingClub] = false;

            Debug.LogWarning($"Failed to search clubs by name from Firestore: {output}");
        }
        #endregion

        #region Get Club Leaderboards Models
        /// <summary>
        /// Tries to get the club leaderboard.
        /// </summary>
        /// <returns></returns>
        public async UniTask<FirestoreClubData[]> TryToGetClubLeaderboard()
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.GettingLeaderboard, out var isWaitingGettingLeaderboard) && isWaitingGettingLeaderboard)
            {
                Debug.LogWarning("Already waiting for a previous request to get clubs");
                return default;
            }

            Debug.Log("Getting club leaderboard");

            // After the cleaning, check if we have a cached result for the same search term
            if (ClubLeaderboard is not null and { Length: > 0 } && (DateTime.UtcNow - lastLeaderboardFetchTime).TotalMinutes <= minutesToFetchLeaderboard)
            {
                Debug.Log("Returning cached club leaderboard. Time since last fetch: " + (DateTime.UtcNow - lastLeaderboardFetchTime).TotalMinutes + " minutes.");
                return ClubLeaderboard;
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.GettingLeaderboard] = true;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Getting club leaderboard: {clubsCollectionIdKey}, normalized partial name: {lastNormalizedSearchTerm}, callback: {nameof(OnClubLeaderboardReceived)}, error callback: {nameof(OnGetClubsLeaderboardFailed)}");
                
                var getClubLeaderboardWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.GetTopClubsByScore
                        (clubsCollectionIdKey,
                        gameObject.name,
                        nameof(OnClubLeaderboardReceived),
                        nameof(OnGetClubsLeaderboardFailed));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.GettingLeaderboard])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore using the wrapper
                await HandleProcess_GameManagerProxy
                     (uniTask: getClubLeaderboardWrapper,
                     taskId: nameof(getClubLeaderboardWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.GettingLeaderboard] = false;
            } 
            else
            {
                Debug.LogWarning("Firestore client is only available on WebGL platform - Simulating club search in editor");

                // Store the variables to use the trycat pattern to store the response
                var getClubLeaderboardRequest = default(string);
                var clubsLeaderboardResponse = default(FirestoreTopClubsResponse);
                try
                {
                    getClubLeaderboardRequest = await HandleProcess_GameManagerProxy
                        (uniTask: () => module.TryToGetClubLeaderboard().AsUniTask(),
                        taskId: nameof(module.TryToGetClubLeaderboard),
                        showLoading: true,
                        returnExceptionOnError: true);

                    clubsLeaderboardResponse = authManager.DeserializeAndDecryptData<FirestoreTopClubsResponse>(getClubLeaderboardRequest);

                    if (clubsLeaderboardResponse is null)
                    {
                        OnGetClubsLeaderboardFailed("Failed to deserialize Firestore club leaderboard response.");
                        return null;
                    }

                    // Cache the search result
                    ClubLeaderboard = clubsLeaderboardResponse.leaderboard?.ToArray();
                    lastLeaderboardFetchTime = DateTime.UtcNow;

                    Debug.Log($"Successfully retrieved {clubsLeaderboardResponse.leaderboard?.Count ?? 0} clubs leaderboard.");

                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception occurred while trying to get club leaderboard: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.GettingLeaderboard] = false;
                }
            }

            return ClubLeaderboard;
        }

        /// <summary>
        /// Callback method for handling the retrieved clubs data from Firestore.
        /// </summary>
        private void OnClubLeaderboardReceived(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GettingLeaderboard] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty club data from Firestore.");
                return;
            }

            // Try to deserialize the output to FirestoreClubSearchResponse
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                var clubsFound = JsonConvert.DeserializeObject<List<FirestoreClubData>>(output, settings);

                // Cache the search result
                ClubLeaderboard = clubsFound?.ToArray();
                lastLeaderboardFetchTime = DateTime.UtcNow;

                Debug.Log($"Successfully retrieved {clubsFound?.Count ?? 0} clubs leaderboard.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while deserializing Firestore club data response: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback method for handling failures in retrieving clubs data from Firestore.
        /// </summary>
        private void OnGetClubsLeaderboardFailed(string output)
        {
            waitingDictionary[Consts.CollectionKeys.GettingLeaderboard] = false;

            Debug.LogWarning($"Failed to get club leaderboard from Firestore: {output}");
        }
        #endregion

        #region Send Club Chat Message Models
        /// <summary>
        /// Tries to send a new club chat message from this client
        /// </summary>
        public async UniTask<bool> TryToSendClubChatMessage(string newMessage)
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.SendingClubChatMessage, out var isWaitingSendingChatMessage) && isWaitingSendingChatMessage)
            {
                Debug.LogWarning("Already waiting for a previous request to send messages");
                return default;
            }

            // Validate the new message
            if (string.IsNullOrEmpty(newMessage))
            {
                Debug.LogWarning("New message is null or empty");
                return default;
            }

            // Check if the member is part of the club
            if (!IsPlayerInClub())
            {
                Debug.LogWarning("The player mus be into a club to be able to send chat messages");
                return default;
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage] = true;

            // Reset property before the request
            WasLastClubMessageSentProperly = false;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Send club chat message, callback: {nameof(OnSendClubMessageReceived)}, error callback: {nameof(OnSendClubMessageFailed)}");
                
                var sendClubMessageWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.AddClubMessage
                        (collectionPath: clubsChatsCollectionIdKey,
                        clubName: PlayerClubData.clubName,
                        senderId: authManager.UUID,
                        senderName: authManager.Username,
                        profileIconId: GetProfilePicture().id ?? string.Empty,
                        messageContent: newMessage,
                        objectName: gameObject.name,
                        callback: nameof(OnSendClubMessageReceived),
                        fallback: nameof(OnSendClubMessageFailed));
                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the club data from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: sendClubMessageWrapper,
                     taskId: nameof(sendClubMessageWrapper),
                     showLoading: false);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage] = false;
            } 
            else
            {
                Debug.LogWarning("Firestore client is only available on WebGL platform - Simulating club sending message");

                var dataEncrypted = authManager.SerializeAndEncryptData(new()
                {
                    ["newMessage"] = newMessage,
                });

                // Store the variables to use the trycat pattern to store the response
                var clubChatMessageEncryptedData = default(string);
                var clubsChatMessageResponse = default(FirestoreClubChatResponse);

                try
                {
                    // Use the backend binding of the corresponding player to change its rank
                    clubChatMessageEncryptedData = await HandleProcess_GameManagerProxy
                        (uniTask: () => module.TryToSendClubChatMessage(dataEncrypted).AsUniTask(), 
                        taskId: nameof(module.TryToSendClubChatMessage), 
                        showLoading: true,
                        returnExceptionOnError: true);

                    // Deserialize and decrypt the data
                    clubsChatMessageResponse = authManager.DeserializeAndDecryptData<FirestoreClubChatResponse>(clubChatMessageEncryptedData);

                    if (clubsChatMessageResponse is null or { clubResponseCodeType: not Enums.ClubResponseCodeType.Success})
                    {
                        OnSendClubMessageFailed("Failed to send club chat message. " +
                            $"\nError code: {clubsChatMessageResponse.clubResponseCodeType.ToString()}" +
                            $"\n\nError: {clubsChatMessageResponse.message}");
                        return false;
                    }

                    Debug.Log($"Successfully sent club chat message");
                    WasLastClubMessageSentProperly = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception occurred while trying to send club chat message: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage] = false;
                }
            }

            return WasLastClubMessageSentProperly;
        }

        /// <summary>
        /// Callback method for handling the sending club chat messages from Firestore.
        /// </summary>
        private void OnSendClubMessageReceived(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty club data from Firestore.");
                return;
            }

            // Try to deserialize the output to FirestoreClubSearchResponse
            WasLastClubMessageSentProperly = true;
            Debug.Log($"Success to send club chat Message from Firestore: \n\n{output}");
        }

        /// <summary>
        /// Callback method for handling failures in sending club chat messages from Firestore.
        /// </summary>
        private void OnSendClubMessageFailed(string output)
        {
            waitingDictionary[Consts.CollectionKeys.SendingClubChatMessage] = false;
            WasLastClubMessageSentProperly = false;
            Debug.LogError($"Failed to send club chat Message from Firestore: {output}");
        }
        #endregion

        #region Subscribe To Club Chat Models
        /// <summary>
        /// Tries to subscribe into the club chat
        /// </summary>
        public async UniTask<bool> TryToSubscribeToClubChat()
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.SubscribingClubChatMessage, out var isListeningClubChatMessage) && isListeningClubChatMessage)
            {
                Debug.LogWarning("Already waiting for a previous request to subscribe to club chat");
                return default;
            }

            // Check if the member is part of the club
            if (!IsPlayerInClub())
            {
                Debug.LogWarning("The player mus be into a club to be able to send chat messages");
                return default;
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.SubscribingClubChatMessage] = true;

            // Reset property before the request
            WasSubscribeToClubChatProperly = false;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Subscribe to club chat, callback: {nameof(OnSubscribeToClubChatSucess)}, error callback: {nameof(OnSubscribeToClubChatFailed)}");
                
                var subscribeToClubChatWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.SubscribeToClubChat
                        (collectionPath: clubsChatsCollectionIdKey,
                        clubId: PlayerClubData.clubName,
                        objectName: gameObject.name,
                        onSubscribeSuccess: nameof(OnSubscribeToClubChatSucess),
                        onSubscribeError: nameof(OnSubscribeToClubChatFailed),
                        onMessagesUpdated: nameof(OnMessagesUpdated),
                        onMessagesError: nameof(OnMessagesError));
                    
                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.SubscribingClubChatMessage])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Subscribe to club chat from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: subscribeToClubChatWrapper,
                     taskId: nameof(subscribeToClubChatWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.SubscribingClubChatMessage] = false;
            } 
            else
                Debug.LogWarning("Firestore client is only available on WebGL platform - TryToSubscribeToClubChat couldn't been called");

            return WasSubscribeToClubChatProperly;
        }

        /// <summary>
        /// Callback method for handling the subcribing into club chat from Firestore.
        /// </summary>
        private void OnSubscribeToClubChatSucess(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.SubscribingClubChatMessage] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty response of TryToSubscribeToClubChat.");
                return;
            }

            // Try to deserialize the output to FirestoreClubSearchResponse
            WasSubscribeToClubChatProperly = true;
            Debug.Log($"Success to subscribe into club chat from Firestore: \n\n{output}");
        }

        /// <summary>
        /// Callback method for handling failures in subscribing into club chat from Firestore.
        /// </summary>
        private void OnSubscribeToClubChatFailed(string output)
        {
            waitingDictionary[Consts.CollectionKeys.SubscribingClubChatMessage] = false;
            WasSubscribeToClubChatProperly = false;
            Debug.LogError($"Failed to subscribe into club chat from Firestore: {output}");
        }
        #endregion
        
        #region Unsubscribe To Club Chat Models
        /// <summary>
        /// Tries to unsubscribe into the club chat
        /// </summary>
        public async UniTask<bool> TryToUnsubscribeFromClubChat()
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.UnsubscribingClubChatMessage, out var isUnsubscribingClubChatMessage) && isUnsubscribingClubChatMessage)
            {
                Debug.LogWarning("Already waiting for a previous request to unsubscribe from club chat");
                return default;
            }

            // Check if the member is part of the club
            if (!IsPlayerInClub())
            {
                Debug.LogWarning("The player must be into a club to be able to unsubscribe from club chat");
                return default;
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.UnsubscribingClubChatMessage] = true;

            // Reset property before the request
            WasUnsubscribeToClubChatProperly = false;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Unsubscribe to club chat, callback: {nameof(OnUnsubscribeFromClubChatSucess)}, error callback: {nameof(OnUnsubscribeFromClubChatFailed)}");
                
                var unsubscribeFromClubChatWrapper = new Func<UniTask>(async () =>
                {
                    // Get the club data from Firestore
                    FirebaseFirestore.UnsubscribeFromClubChat
                        (collectionPath: clubsChatsCollectionIdKey,
                        clubId: PlayerClubData.clubName,
                        objectName: gameObject.name,
                        onUnsubscribeSuccess: nameof(OnUnsubscribeFromClubChatSucess),
                        onUnsubscribeError: nameof(OnUnsubscribeFromClubChatFailed));
                    
                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.UnsubscribingClubChatMessage])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Unsubscribe from club chat from Firestore
                await HandleProcess_GameManagerProxy
                     (uniTask: unsubscribeFromClubChatWrapper,
                     taskId: nameof(unsubscribeFromClubChatWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.UnsubscribingClubChatMessage] = false;
            } 
            else
                Debug.LogWarning("Firestore client is only available on WebGL platform - TryToUnsubscribeToClubChat couldn't been called");

            return WasUnsubscribeToClubChatProperly;
        }

        /// <summary>
        /// Callback method for handling the subcribing into club chat from Firestore.
        /// </summary>
        private void OnUnsubscribeFromClubChatSucess(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.UnsubscribingClubChatMessage] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output))
            {
                Debug.LogWarning("Received empty response of TryToUnsubscribeToClubChat.");
                return;
            }

            // Try to deserialize the output to FirestoreClubSearchResponse
            WasUnsubscribeToClubChatProperly = true;
            Debug.Log($"Success to unsubscribe into club chat from Firestore: \n\n{output}");
        }

        /// <summary>
        /// Callback method for handling failures in subscribing into club chat from Firestore.
        /// </summary>
        private void OnUnsubscribeFromClubChatFailed(string output)
        {
            waitingDictionary[Consts.CollectionKeys.UnsubscribingClubChatMessage] = false;
            WasUnsubscribeToClubChatProperly = false;
            Debug.LogError($"Failed to unsubscribe into club chat from Firestore: {output}");
        }

        /// <summary>
        /// Called when Firestore sends an updated snapshot of the club chat document.
        /// This is invoked by the .jslib listener (via SendMessage).
        /// </summary>
        /// <param name="jsonPayload">A JSON array of messages from Firestore.</param>
        public void OnMessagesUpdated(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                Debug.LogWarning("[ChatManager] Empty chat payload received.");

                PlayerClubChatData = default;
                onGetClubChatData?.InvokeAllAtTimeAsync(default);
                return;
            }

            try
            {
                Debug.Log("[ChatManager] Messages\n\n" + jsonPayload);

                // Deserialize the JSON payload into a list of messages
                var newClubChatData = JsonConvert.DeserializeObject<FirestoreClubChatData>(jsonPayload);
                if (newClubChatData is null or { messages: null or { Count: 0 } })
                {
                    Debug.LogWarning("[ChatManager] Deserialized messages are null.");
                    return;
                }

                var last = newClubChatData.messages[^1];
                Debug.Log($"[ChatManager] Received {newClubChatData.messages.Count} messages. Last: {last.senderName}: {last.content}");
                
                // Reverse messages if Firestore stores oldest-first
                newClubChatData.messages.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

                PlayerClubChatData = newClubChatData;
                onGetClubChatData?.InvokeAllAtTimeAsync(PlayerClubChatData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatManager] Failed to parse chat update: {ex.Message}\n{ex.StackTrace}");

                PlayerClubChatData = default;
                onGetClubChatData?.InvokeAllAtTimeAsync(default);
            }
        }

        /// <summary>
        /// Called when an error occurs during a Firestore snapshot update.
        /// This can include permission issues, missing document, or network failure.
        /// </summary>
        public void OnMessagesError(string error)
        {
            Debug.LogError($"Failed to send club chat message: {error}");
        }
        #endregion
    }
}
