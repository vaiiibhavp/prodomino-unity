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
using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This partial class was create to content all info related to Search into the GameManager
    /// </summary>
    public partial class GameManager
    {
        // Cache dictionary for player searches
        public Dictionary<string, PlayerSearchCacheModel> SearchedPlayersCache { get; private set; }

        private string lastNormalizedPlayerSearch;

        /// <summary>
        /// Cleans the user search cache to keep memory and relevance in check.
        /// Keeps only the last 30 searches made in the last 5 minutes.
        /// </summary>
        private void CleanPlayerSearchCache()
        {
            // Ensure collection is initialized
            if (SearchedPlayersCache is null)
                SearchedPlayersCache = new();

            // Filter recent results and keep only 30 most recent
            var filtered = SearchedPlayersCache
                .Where(x => (DateTime.UtcNow - x.Value.timestamp).TotalMinutes <= 5)
                .OrderByDescending(x => x.Value.timestamp)
                .Take(30)
                .ToDictionary(x => x.Key, x => x.Value);

            // Replace contents safely
            SearchedPlayersCache.Clear();
            foreach (var kv in filtered)
                SearchedPlayersCache[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Tries to search users by name using Realtime Database.
        /// Identical architecture to club search (Firestore).
        /// </summary>
        public async UniTask<RtdbUserData[]> TryToSearchPlayersByName(string partialName)
        {
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.SearchingUser, out var isWaiting) && isWaiting)
            {
                Debug.LogWarning("Already waiting for a previous request to search players.");
                return default;
            }

            // Validate input
            if (string.IsNullOrEmpty(partialName))
            {
                Debug.LogWarning("partialName is null or empty");
                return default;
            }

            lastNormalizedPlayerSearch = NormalizeName(partialName);
            Debug.Log($"Searching players with normalized search: {lastNormalizedPlayerSearch}");

            // After normalization, check cache
            if (SearchedPlayersCache.TryGetValue(lastNormalizedPlayerSearch, out var cachedModel) && cachedModel.playersFound.Count > 0)
            {
                Debug.Log($"Returning cached results with {cachedModel.playersFound.Count} players.");

                // Update players that could be stale for the next search
                CleanPlayerSearchCache();
                return cachedModel.playersFound.ToArray();
            }

            // Mark waiting
            waitingDictionary[Consts.CollectionKeys.SearchingUser] = true;

            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Executing WebGL RTDB search for: {lastNormalizedPlayerSearch}");

                var searchPlayersWrapper = new Func<UniTask>(async () =>
                {
                    FirebaseDatabase.SearchPlayersByName(
                        lastNormalizedPlayerSearch,
                        gameObject.name,
                        nameof(OnSearchPlayersByNameReceived),
                        nameof(OnSearchPlayersByNameFailed));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.SearchingUser])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Execute the search process
                await HandleProcess_GameManagerProxy
                    (uniTask: searchPlayersWrapper,
                    taskId: nameof(searchPlayersWrapper),
                    showLoading: false);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.SearchingUser] = false;
            } 
            else
            {
                Debug.LogWarning("RTDB JS search only works on WebGL — editor simulation");

                waitingDictionary[Consts.CollectionKeys.SearchingUser] = false;
                return default;
            }

            return SearchedPlayersCache.TryGetValue(lastNormalizedPlayerSearch, out var finalModel)
                ? finalModel.playersFound.ToArray()
                : null;

            /*
                Normalizes a user name for Firestore search and indexing.
                Removes accents, diacritics, and special characters, then lowercases.
            */
            string NormalizeName(string input)
            {
                string n = input.ToLowerInvariant().Normalize(NormalizationForm.FormD);
                n = new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
                n = Regex.Replace(n, @"[^a-z0-9\s]", "");
                n = Regex.Replace(n.Trim(), @"\s+", " ");
                return n;
            }
        }

        /// <summary>
        /// Callback for RTDB search success.
        /// Matches JS result structure.
        /// </summary>
        private void OnSearchPlayersByNameReceived(string json)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.SearchingUser] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("Received empty player search result.");
                return;
            }

            // Try to deserialize the output to a list of RtdbUserSearchResult
            try
            {
                Debug.Log("[OnSearchPlayersByNameReceived] Search output:\n\n" + json);
                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                // Incoming array: [{id,displayName,createdAt,lastSeen}]
                var list = JsonConvert.DeserializeObject<List<RtdbUserData>>(json, settings);

                // Cache the search result
                SearchedPlayersCache[lastNormalizedPlayerSearch] = new(list ?? new(), DateTime.UtcNow);

                // Clean the collection if it exceeds the limit
                CleanPlayerSearchCache();
                Debug.Log($"Successfully retrieved {list?.Count ?? 0} players from RTDB search.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception while parsing player search: " + ex.Message);
            }
        }

        /// <summary>
        /// Callback for RTDB search error.
        /// </summary>
        private void OnSearchPlayersByNameFailed(string error)
        {
            waitingDictionary[Consts.CollectionKeys.SearchingUser] = false;

            Debug.LogWarning("Failed to search players by name: " + error);
        }

        // Cache container
        [Serializable]
        public class PlayerSearchCacheModel
        {
            public List<RtdbUserData> playersFound;
            public DateTime timestamp;

            public PlayerSearchCacheModel(List<RtdbUserData> list, DateTime time)
            {
                playersFound = list;
                timestamp = time;
            }
        }

        // Result model (id + data)
        [Serializable]
        public class RtdbUserData
        {
            // The user's unique RTDB key (you attach this client-side)
            public string userId;

            // Display name chosen by the player
            public string displayName;

            // Icon used by the player
            public string profileIconID;

            // Normalized string used for prefix search ("john doe" → "john doe")
            public string normalizedName;

            // Tokenized substrings used for exact-search matches
            // Example: ["john", "doe"]
            public List<string> searchTokens;

            // Timestamps stored as DateTime.ToBinary() long
            public long createdAt;
            public long lastSeen;
        }
    }
}
