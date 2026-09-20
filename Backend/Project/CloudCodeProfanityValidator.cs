using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend
{
    /// <summary>
    /// Fetches and validates banned words in a Cloud Code environment using Unity Remote Config.
    /// Mirrors UnityProfanityValidator used on the client.
    /// </summary>
    internal class CloudCodeProfanityValidator : ProfanityValidator
    {
        private readonly IGameApiClient _gameApiClient;
        private readonly IExecutionContext _executionContext;
        private readonly ILogger _logger;

        public CloudCodeProfanityValidator(IGameApiClient gameApiClient, IExecutionContext executionContext, ILogger logger)
        {
            _gameApiClient = gameApiClient;
            _executionContext = executionContext;
            _logger = logger;
        }

        /// <summary>
        /// Fetches the list of banned or profane words from Remote Config.
        /// Expected format:
        /// "banned_words": ["word1", "word2", "word3", ...]
        /// </summary>
        public override async Task<HashSet<string>> FetchBannedWordsAsync()
        {
            // Fetch raw JSON data from Remote Config (via your custom UGS helper)
            var response = await UGSApiHelper.GetRemoteConfig(_gameApiClient, _executionContext, UGSConfigData.bannedWordsDomainsConfig);

            if (string.IsNullOrWhiteSpace(response))
                throw new Exception("Empty response from Remote Config.");

            // Parse Remote Config response
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

            if (data?.TryGetValue("value", out var jsonCollectionObj) != true || jsonCollectionObj is not JArray jsonCollection)
                throw new Exception("Invalid Remote Config response format.");

            // Find the Remote Config entry named "banned_words"
            var bannedWordsToken = jsonCollection
                .OfType<JObject>()
                .FirstOrDefault(x => x.TryGetValue("key", out var key) && key.ToString() == "banned_words")
                ?.GetValue("value");

            if (bannedWordsToken == null)
                throw new Exception("Remote Config key 'banned_words' not found.");

            // The value should be a JArray
            var bannedWordsArray = bannedWordsToken as JArray;
            if (bannedWordsArray == null)
                throw new Exception("'banned_words' value is not a JSON array.");

            // Normalize and deduplicate
            var cleaned = bannedWordsArray
                .Select(x => x.ToString().ToLowerInvariant().Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Log for debugging (optional)
            _logger.LogInformation($"[CloudCodeProfanityValidator] Loaded {cleaned.Count} banned words from Remote Config.");

            return cleaned;
        }
    }
}
