using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.AuthenticationSystem;

/// <summary>
/// Provides functionality to sign in a user with a third-party provider in Firebase and link the account with an
/// existing Unity Gaming Services (UGS) account.
/// </summary>
/// <param name="logger"></param>
/// <param name="gameApiClient"></param>
public class SignInProviderInFirebaseModule(ILogger<SignInProviderInFirebaseModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<SignInProviderInFirebaseModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Signs in a user to Firebase using a third-party provider, links the Firebase account with the UGS account, and
    /// synchronizes user data between both systems.
    /// </summary>
    /// <param name="executionContext">The execution context containing player and authentication information.</param>
    /// <param name="parametersEncryptedJson">An encrypted JSON string containing provider authentication parameters.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="Exception">Thrown when the player ID is invalid or null.</exception>
    /// <exception cref="ArgumentException">Thrown when required input data is missing or invalid.</exception>
    [CloudCodeFunction(nameof(SignInProviderInFirebase))]
    public async Task SignInProviderInFirebase(IExecutionContext executionContext, string parametersEncryptedJson)
    {
        // Validate the execution context
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Get the player ID from the execution context and validate it
        var playerId = executionContext.PlayerId;
        if (string.IsNullOrEmpty(playerId))
            throw new Exception("Player ID is invalid or null.");

        // Get the data from the encrypted parameters JSON
        var data = BackendHelper.ValidateEncriptedParameters
            (parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate entry data
        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data. Provide provider id");

        if (!data.TryGetValue("providerid", out var providerIDObj) || providerIDObj is not string providerID || string.IsNullOrEmpty(providerID))
            throw new ArgumentException("providerid is not specified");

        if (!data.TryGetValue("providerIDToken", out var providerIDTokenObj) || providerIDTokenObj is not string providerIDToken || string.IsNullOrEmpty(providerIDToken))
            throw new ArgumentException("providerIDToken is not specified");

        if (!data.TryGetValue("tokenType", out var tokenTypeObj) || tokenTypeObj is not string tokenType || string.IsNullOrEmpty(tokenType))
            throw new ArgumentException("tokenType is not specified");

        // Try to get the optional display name and profile icon ID from the parameters, if they exist, to link the Firebase user with the UGS one with the same display name and profile icon ID, prioritizing the one registered in UGS if it exists,
        // and if not, using the optional ones provided in the parameters, and if none of them exist, use an empty string for the profile icon ID and null for the display name.
        var optionalDisplayName = data.TryGetValue("optionalDisplayName", out var optionalDisplayNameObj) && optionalDisplayNameObj is string optionalDisplayNameStr ? optionalDisplayNameStr : null;
        var optionalIconID = data.TryGetValue("optionalIconID", out var optionalIconIDObj) && optionalIconIDObj is string optionalIconIDStr ? optionalIconIDStr : null;

        // Define the keys for the data we want to load from UGS
        var profileKey = CloudSaveProperties.Profile.ToString();
        var analyticsKey = CloudSaveProperties.Analytics.ToString();
        var emailKey = CloudSaveProperties.Email.ToString();
        var accountCreationKey = CloudSaveProperties.AccountCreatedAt.ToString();

        // Define the keys for the firebase access data
        var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
        var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
        var firebaseProviderIDKey = CloudSaveProperties.FirebaseProviderID.ToString();
        var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
        var clubKey = CloudSaveProperties.Club.ToString();

        // Load the protected data from UGS to obtain the necessary information to link the Firebase user with the UGS one and to update the user data in UGS with the Firebase information obtained
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
            customID: null, alternativePlayerID: null, isThrowingException: false,
            profileKey, analyticsKey, accountCreationKey, emailKey,
            firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey).ConfigureAwait(false);

        // Get the player profile data
        var playerProfileData = playerDataResponse?.FirstOrDefault(x => x?.key == profileKey)?.value?.ToObject<PlayerProfileData>() ?? new();

        // Get the player analytics data
        var playerAnalyticsData = playerDataResponse?.FirstOrDefault(x => x?.key == analyticsKey)?.value?.ToObject<AnalyticsData>() ?? new();

        // Try to get the account creation date
        var accountCreatedAt = playerDataResponse?.FirstOrDefault(x => x?.key == accountCreationKey)?.value?.ToObject<string>() ?? string.Empty;

        // Try to get the email associated
        var emailAssociatedToUGS = playerDataResponse?.FirstOrDefault(x => x?.key == emailKey)?.value?.ToObject<string>() ?? string.Empty;

        // Try to get the club name
        var clubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubKey)?.value?.ToObject<string>() ?? string.Empty;

        // Try to get the firebase access data
        var firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
        var firebaseIdToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
        var firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

        // Determine the profile icon ID to use in Realtime Database, prioritizing the one registered in UGS, then the optional one provided in the parameters, and if none of them exist, use an empty string
        var profileIconId = !string.IsNullOrEmpty(playerProfileData.profileIconID)
            ? playerProfileData.profileIconID
            : optionalIconID ?? string.Empty;

        // Sign in to Firebase using the provider ID token.
        // This could return exceptions that we want to catch in the frontend
        var firebaseAuthResponseData = await FirebaseApiHelper.SignInWithOAuthCredential(tokenType, providerIDToken, providerID, logger: _logger).ConfigureAwait(false);

        // Try to get the username attached to the UGS account, if it exists, to link the Firebase user with the UGS one with the same display name, prioritizing the one registered in UGS if it exists,
        var usernameAttachedToUGS = await UGSApiHelper.GetUsername(executionContext, _logger: _logger);

        // Determine the username to use for the linking and validation, prioritizing the one provided in the parameters
        var usernameToUse = !string.IsNullOrEmpty(optionalDisplayName)
            ? optionalDisplayName
            : usernameAttachedToUGS;

        // Validate that the username attached to the UGS account, if it exists, is the same as the optional display name provided in the parameters to ensure that we are linking the correct accounts,
        // and if not, change the username in UGS to match the optional display name provided if the username attached to UGS is the same as the one registered in Firebase
        if (!string.IsNullOrEmpty(optionalDisplayName) && optionalDisplayName != usernameAttachedToUGS)
            await AuthenticationSystem_Helper.ValidateUGSUsername(executionContext, _gameApiClient,
                playerId: playerId, idToken: executionContext.AccessToken,
                usernameToCheck: optionalDisplayName,
                optionalAttachedUsername: usernameAttachedToUGS,
                _logger: _logger);

        // Once we have the Firebase response, try to update the Firebase user profile with the UGS username if it doesn't exist to link them with the same display name
        //await AuthenticationSystem_Helper.ValidateFirebaseUsername(executionContext,
        //    firebaseAuthResponseData, ugsUsername: usernameToUse,
        //    _logger: _logger).ConfigureAwait(false);

        // Validate the club name associated with the user to ensure that the user is in the correct club according to the one registered in UGS, and if not, throw an exception since we don't want to link accounts with different clubs to avoid account takeover issues.
        await AuthenticationSystem_Helper.ValidateClubName(executionContext, _gameApiClient,
            firebaseAuthResponseData,
            userUnityId: playerId, username: usernameToUse, clubName: clubName,
            _logger: _logger).ConfigureAwait(false);

        // Try to ensure the user exists in Realtime Database
        await AuthenticationSystem_Helper.TryToEnsureUserExistsInRTDB(executionContext, _gameApiClient,
            firebaseAuthResponseData,
            playerId: playerId, profileIconID: profileIconId!, optionalDisplayName: usernameToUse,
            _logger: _logger).ConfigureAwait(false);

        // Check if the email associated with the Firebase user is the same as the one registered in UGS to ensure they are the same user, and if not, throw an exception since we don't want to link accounts with different emails to avoid account takeover issues.
        // If there is no email in UGS, we will allow the linking and save the Firebase email in UGS.
        await AuthenticationSystem_Helper.CheckIfFirebaseUserHasSameUGSEmail(firebaseAuthResponseData,
            emailAssociatedToUGS: emailAssociatedToUGS,
            _logger: _logger).ConfigureAwait(false);

        // Initialize the payload that will be used to update the user data in UGS
        var payload = new Dictionary<string, object>();

        // If there is no email registered in UGS but there is in Firebase, save it in the payload to update it in UGS and also save it in protected data
        if (string.IsNullOrEmpty(emailAssociatedToUGS) && !string.IsNullOrEmpty(firebaseAuthResponseData.email))
            payload[emailKey] = firebaseAuthResponseData.email;

        // Try to ensure the UGS user has the firebase access data registered in UGS protected data, and if not, save it in the payload to update it in UGS protected data
        AuthenticationSystem_Helper.TryToRecordFirebaseAccessData(ref payload, firebaseAuthResponseData,
            firebaseID: firebaseID, firebaseIDToken: firebaseIdToken, firebaseRefreshToken: firebaseRefreshToken,
            firebaseIDKey: firebaseIDKey, firebaseIDTokenKey: firebaseIDTokenKey, firebaseProviderIDKey: firebaseProviderIDKey, firebaseRefreshTokenKey: firebaseRefreshTokenKey,
            _logger: _logger);

        // Try to register creation date if it doesn't exist
        AuthenticationSystem_Helper.TryToRegisterCreationDate(ref payload,
            accountCreatedAt: accountCreatedAt, accountCreationKey: accountCreationKey,
            _logger: _logger);

        // Try to update the days streak in analytics data
        AuthenticationSystem_Helper.TryToUpdateDaysStreak(ref payload,
            analyticsData: playerAnalyticsData, analyticsKey: analyticsKey,
            _logger: _logger);

        // If there is an optional profile icon ID provided and there is no profile icon ID registered in UGS, save the optional profile icon ID in the payload to update it in UGS protected data to link the Firebase user with the UGS one with the same profile icon ID
        if (!string.IsNullOrEmpty(optionalIconID) && string.IsNullOrEmpty(playerProfileData.profileIconID))
        {
            playerProfileData.profileIconID = optionalIconID;
            payload[profileKey] = playerProfileData;
        }

        // Save the email in UGS protected data
        if (payload.Count > 0)
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, payload);

        _logger.LogInformation("User with player ID {PlayerID} signed in with provider {ProviderID} in Firebase and linked with UGS account successfully.", 
            playerId, providerID);
    }
}
