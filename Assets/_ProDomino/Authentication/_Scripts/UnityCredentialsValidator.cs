using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timba.Patterns;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Provides functionality to validate credentials and retrieve blocked email domains using Unity services.
    /// </summary>
    public class UnityCredentialsValidator : CredentialsValidator
    {
        private AuthManager authManager;

        /// <summary>
        /// Check if the domain block list was successfully obtained
        /// </summary>
        public static bool IsInitialized => _tempEmailDomains is not null and { Count: > 0 };

        /// <summary>
        /// Asynchronously retrieves a set of blocked email domains from remote configuration.
        /// </summary>
        /// <returns>A HashSet containing blocked domain names, or an empty set if unavailable.</returns>
        public override async Task<HashSet<string>> FetchBlockedDomainsAsync()
        {
            authManager = authManager != null 
                ? authManager 
                : ServiceLocator.Instance?.GetService<AuthManager>();

            if (authManager == null)
            {
                Debug.LogError("[UnityCredentialsValidator] AuthManager service not found");
                return new HashSet<string>();
            }

            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } and { IsUGSAuthenticated: true });

            // Fetch Remote Config data
            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

            var jsonData = RemoteConfigService.Instance.appConfig.GetJson("blocked_email_domains");
            var data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonData);

            return data != null && data.ContainsKey("blockedDomains")
                ? new HashSet<string>(data["blockedDomains"])
                : new HashSet<string>();
        }

        public struct UserAttributes
        {
        }

        /// <summary>
        /// Represents application attributes including version and platform.
        /// </summary>
        public struct AppAttributes
        {
            public string version;
            public string platform;
        }
    }
}
