using Google.Protobuf.WellKnownTypes;
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
        internal static readonly string blockedEmailDomainsConfig = "29dccedf-b5af-469b-837c-43527abb2011";
        internal static readonly string bannedWordsDomainsConfig = "57e2462f-6e4b-4034-8b86-8411aabfe0bc";

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
