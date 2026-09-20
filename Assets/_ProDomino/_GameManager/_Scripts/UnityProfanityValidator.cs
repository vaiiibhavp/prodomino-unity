using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Authentication;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timba.Patterns;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// Fetches and validates banned words directly from Unity Remote Config on the client side.
    /// Mirrors the Cloud Code equivalent: CloudCodeProfanityValidator.
    /// </summary>
    public class UnityProfanityValidator : ProfanityValidator
    {
        private AuthManager authManager;

        /// <summary>
        /// Fetches the list of banned or profane words from Remote Config.
        /// </summary>
        /// <returns>A HashSet of banned words.</returns>
        public override async Task<HashSet<string>> FetchBannedWordsAsync()
        {
            authManager = authManager != null 
                ? authManager 
                : ServiceLocator.Instance?.GetService<AuthManager>();
            if (authManager == null)
            {
                Debug.LogError("[UnityProfanityValidator] AuthManager service not found.");
                return new HashSet<string>();
            }

            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } and { IsUGSAuthenticated: true });

            // Fetch Remote Config data
            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

            // Retrieve JSON string from Remote Config (same key used in backend)
            var jsonData = RemoteConfigService.Instance.appConfig.GetJson("banned_words");

            if (string.IsNullOrWhiteSpace(jsonData))
            {
                Debug.LogWarning("[UnityProfanityValidator] No data found for key 'banned_words'.");
                return new HashSet<string>();
            }

            // Expecting a JSON array: ["word1", "word2", ...]
            var words = JsonConvert.DeserializeObject<List<string>>(jsonData);

            if (words == null || words.Count == 0)
            {
                Debug.LogWarning("[UnityProfanityValidator] Banned words list is empty or invalid.");
                return new HashSet<string>();
            }

            // Normalize and clean duplicates
            var cleaned = words
                .Select(w => w.ToLowerInvariant().Trim())
                .Distinct()
                .ToHashSet();

            Debug.Log($"[UnityProfanityValidator] Loaded {cleaned.Count} banned words from Remote Config.");

            return cleaned;
        }

        // These structs are required for Unity Remote Config
        public struct UserAttributes { }

        public struct AppAttributes
        {
            public string version;
            public string platform;
        }
    }
}
