using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend;

internal class UGSBackend
{
    public const string customID = "LiveOps";

    internal class UGSConfigData
    {
        // Remote Config "settings" config ID of the PlayProDomino Dev project's production environment.
        // Both keys ("blocked_email_domains" and "banned_words") live in this same config.
        internal static readonly string blockedEmailDomainsConfig = "fa5890fc-bbe3-43fb-85b2-cff0efb97f09";
        internal static readonly string bannedWordsDomainsConfig = "fa5890fc-bbe3-43fb-85b2-cff0efb97f09";

        private const string SecretAuthHeader = "AUTHORIZATION_HEADER";

        /// <summary>
        /// Retrieves the authentication header value from the secret manager using the provided API client and
        /// execution context.
        /// </summary>
        /// <param name="api">The game API client used to access the secret manager.</param>
        /// <param name="ctx">The execution context for the secret retrieval operation.</param>
        /// <returns>A string containing the authentication header value.</returns>
        /// <exception cref="UGSException">Thrown if the service account secret cannot be retrieved or is invalid.</exception>
        internal static async Task<string> GetAuthHeader(IGameApiClient api, IExecutionContext ctx)
        {
            try
            {
                var secretServiceAccount = await api.SecretManager.GetSecret(ctx, SecretAuthHeader);
                if (secretServiceAccount == null || string.IsNullOrEmpty(secretServiceAccount.Value))
                    throw new UGSException("Failed to retrieve the service account secret. Ensure that the secret exists and has a valid value.");

                return secretServiceAccount.Value;
            }
            catch (Exception ex)
            {
                throw new UGSException($"Error retrieving the service account secret. \n\nResponse:\n{ex.Message}", ex);
            }
        }
    }

    public enum CloudSaveProperties
    {
        AccountCreatedAt,

        FirebaseID,
        FirebaseIDToken,
        FirebaseProviderID,
        FirebaseRefreshToken,

        Email,
        Nationality,

        Currency,

        Config,
        Analytics,
        Achievements,
        Club,
        Badgets,
        Ads,
        Purchase,

        Missions,
        DailyMissions,
        WeeklyMissions,

        Profile,
        Cosmetics,
        Tutorials,
        Match,
    }
}
