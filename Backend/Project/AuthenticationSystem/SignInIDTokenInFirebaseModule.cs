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
/// Provides functionality to sign in a user to Firebase using an ID token, linking and synchronizing user data between
/// Unity Gaming Services (UGS) and Firebase.
/// </summary>
/// <param name="logger">The logger used for logging information and errors.</param>
/// <param name="gameApiClient">The game API client used to interact with UGS services.</param>
public class SignInIDTokenInFirebaseModule(ILogger<SignInIDTokenInFirebaseModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<SignInIDTokenInFirebaseModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Signs in a user to Firebase using an ID token, synchronizes user data between Firebase and UGS, and updates UGS
    /// with Firebase information as needed.
    /// </summary>
    /// <param name="executionContext">The execution context containing player and request information.</param>
    /// <returns>A task representing the asynchronous sign-in operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="Exception">Thrown when the player ID is invalid or null.</exception>
    [CloudCodeFunction(nameof(SignInIDTokenInFirebase))]
    public async Task SignInIDTokenInFirebase(IExecutionContext executionContext)
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
            : string.Empty;

        // "Sign-In" in Firebase with the ID token, refreshing the token if needed, and getting the firebase user data
        // This action will get you the firebase user data if the token is valid, and if the token is expired but the refresh token is valid,
        // it will refresh the token and get the firebase user data, and if both are invalid it will return null
        var firebaseLookUpResponseData = await FirebaseApiHelper.LookUpAsync(firebaseIdToken, firebaseRefreshToken);
        var userFirebaseAuthResponseData = firebaseLookUpResponseData.users?.FirstOrDefault();

        // Try to get the username attached to the UGS account, if it exists, to link the Firebase user with the UGS one with the same display name, prioritizing the one registered in UGS if it exists,
        // and if not, using the optional one provided in the parameters, and if none of them exist, use null.
        var usernameAttachedToUGS = await UGSApiHelper.GetUsername(executionContext, _logger: _logger);

        // Once we have the Firebase response, try to update the Firebase user profile with the UGS username if it doesn't exist to link them with the same display name
        //await AuthenticationSystem_Helper.ValidateFirebaseUsername(executionContext,
        //    firebaseLookUpResponseData, optionalAttachedUsername: usernameAttachedToUGS,
        //    _logger: _logger).ConfigureAwait(false);

        // Validate the club name associated with the user to ensure that the user is in the correct club according to the one registered in UGS, and if not, throw an exception since we don't want to link accounts with different clubs to avoid account takeover issues.
        await AuthenticationSystem_Helper.ValidateClubName(executionContext, _gameApiClient,
            firebaseLookUpResponseData,
            userUnityId: playerId, username: usernameAttachedToUGS, clubName: clubName,
            _logger: _logger).ConfigureAwait(false);

        // Try to ensure the user exists in Realtime Database
        await AuthenticationSystem_Helper.TryToEnsureUserExistsInRTDB(executionContext, _gameApiClient,
            firebaseLookUpResponseData,
            playerId: playerId, profileIconID: profileIconId!, optionalDisplayName: usernameAttachedToUGS,
            _logger: _logger).ConfigureAwait(false);

        // Check if the email associated with the Firebase user is the same as the one registered in UGS to ensure they are the same user, and if not, throw an exception since we don't want to link accounts with different emails to avoid account takeover issues.
        // If there is no email in UGS, we will allow the linking and save the Firebase email in UGS.
        await AuthenticationSystem_Helper.CheckIfFirebaseUserHasSameUGSEmail(firebaseLookUpResponseData,
            emailAssociatedToUGS: emailAssociatedToUGS,
            _logger: _logger).ConfigureAwait(false);

        // Initialize the payload that will be used to update the user data in UGS
        var payload = new Dictionary<string, object>();

        // If there is no email registered in UGS but there is in Firebase, save it in the payload to update it in UGS and also save it in protected data
        if (string.IsNullOrEmpty(emailAssociatedToUGS) && !string.IsNullOrEmpty(userFirebaseAuthResponseData?.email))
            payload[emailKey] = userFirebaseAuthResponseData.email;

        // Try to ensure the UGS user has the firebase access data registered in UGS protected data, and if not, save it in the payload to update it in UGS protected data
        AuthenticationSystem_Helper.TryToRecordFirebaseAccessData(ref payload, firebaseLookUpResponseData,
            firebaseID, firebaseIdToken, firebaseRefreshToken,
            firebaseIDKey, firebaseIDTokenKey, firebaseProviderIDKey, firebaseRefreshTokenKey, 
            _logger);

        // Try to register creation date if it doesn't exist
        AuthenticationSystem_Helper.TryToRegisterCreationDate(ref payload, accountCreatedAt, accountCreationKey, _logger);

        // Try to update the days streak in analytics data
        AuthenticationSystem_Helper.TryToUpdateDaysStreak(ref payload, playerAnalyticsData, analyticsKey, _logger);

        // Save the email in UGS protected data
        if (payload.Count > 0)
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, payload);

        _logger.LogInformation("User with player ID {playerId} signed in to Firebase successfully with the ID token.", 
            playerId);
    }
}
