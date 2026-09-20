using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.AuthenticationSystem;

/// <summary>
/// Provides functionality to sign up users with username, email, and password, integrating Unity Gaming Services and
/// Firebase authentication.
/// </summary>
/// <param name="logger"></param>
/// <param name="gameApiClient"></param>
public class SignUpWithCredentialsModule(ILogger<SignUpWithCredentialsModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<SignUpWithCredentialsModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Registers a new user with username, email, and password, linking accounts between UGS and Firebase, and updates
    /// user data in both systems.
    /// </summary>
    /// <param name="executionContext">The execution context containing player and access information.</param>
    /// <param name="parametersEncryptedJson">Encrypted JSON string containing sign-up parameters.</param>
    /// <returns>A task representing the asynchronous sign-up operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the execution context is null.</exception>
    /// <exception cref="Exception">Thrown if the player ID is invalid or if sign-up fails in UGS.</exception>
    /// <exception cref="ArgumentException">Thrown if input data is missing, invalid, or required fields are empty.</exception>
    /// <exception cref="UGSException">Thrown if the username is already taken or other UGS-specific errors occur.</exception>
    [CloudCodeFunction(nameof(SignUpWithCredentials))]
    public async Task SignUpWithCredentials(IExecutionContext executionContext, string parametersEncryptedJson)
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

        // Validate the input data. This will throw an exception if any of the required fields are missing or invalid
        await ValidateSignUpData(data).ConfigureAwait(false);

        // Extract the username, email and password from the data dictionary
        var username = data["username"] is string usernameString ? usernameString : string.Empty;
        var email = data["email"] is string emailString ? emailString : string.Empty;
        var password = data["password"] is string passwordString ? passwordString : string.Empty;

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

        // Check if the user already exists in UGS and Firebase before signing up
        await CheckIfUserExists(username, email).ConfigureAwait(false);

        // Sign up the user in UGS and Firebase, getting the necessary data from both platforms to link the accounts and update the user data in UGS with the Firebase information obtained
        var playerAuthResponseData = await SignUpInUGS(username, password);

        // Sign up in Firebase using the credentials and update the Firebase user profile with the username to link it to UGS, getting the necessary data from Firebase to link the accounts and update the user data in UGS with the Firebase information obtained
        var firebaseAuthResponseData = await SignUpInFirebase(email, password, username);

        // Try to ensure the user exists in Realtime Database
        await AuthenticationSystem_Helper.TryToEnsureUserExistsInRTDB(executionContext, _gameApiClient,
            firebaseAuthResponseData,
            playerId: playerId, profileIconID: profileIconId!, optionalDisplayName: username,
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

        // Save the email in UGS protected data
        if (payload.Count > 0)
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, payload);

        _logger?.LogInformation("User signed up successfully with credentials. User ID: {PlayerId}", 
            playerAuthResponseData.userId);

        #region Helpers
        async Task ValidateSignUpData(Dictionary<string, object> data)
        {
            if (data is null || data.Count == 0)
                throw new ArgumentException("Invalid input data. Provide email, password, and username.");

            // Check if the data dictionary contains the required keys and values
            var requiredKeys = new[] { "username", "email", "password" };
            foreach (var key in requiredKeys)
                if (!data.TryGetValue(key, out var value) 
                    || value is not string valueJson 
                    || string.IsNullOrEmpty(valueJson))
                    throw new ArgumentException($"{key} cannot be empty.");

            // Initialize the credentials validator and set the environment ID
            await new CloudCodeCredentialsValidator(_gameApiClient, executionContext).Initialize();

            // Check if the username is valid
            if (!CredentialsValidator.IsValidUsername(data["username"] is string username ? username : string.Empty))
                throw new ArgumentException($"The username {data["username"]} is not valid");

            // Check if the email accomplish with the structure and if it's not a temporal email
            if (!CredentialsValidator.IsValidEmail(data["email"] is string email ? email : string.Empty))
                throw new ArgumentException($"The email {data["email"]} is not valid");

            // Check if the password is using the correct structure
            if (!CredentialsValidator.IsValidPassword(data["password"] is string password ? password : string.Empty))
                throw new ArgumentException($"The password {data["password"]} is not strong enough");
        }

        /// Try to get the user by username in UGS and by email in Firebase to check if they already exist before signing up. 
        /// If the user is not found, both helpers will throw an exception that we will catch to check the error code/message 
        /// and continue the flow if the user doesn't exist or rethrow it if the error is different.
        async Task CheckIfUserExists(string username, string email)
        {
            try
            {
                // Check if the user already exists in UGS
                var userResponseData = await UGSApiHelper.GetUserByUsernameAsync(_gameApiClient, executionContext, username).ConfigureAwait(false);
                if (userResponseData != null)
                    throw new UGSException($"The username {username} is already taken.");
            }

            // If the user is not found in UGS, it will throw a UGSException that we will catch to check the error code and continue the flow
            catch (UGSException ex)
            {
                // We are checking for not found user, it means that if the user was not found is considered a valid case and we want to continue the flow,
                // but if the error is different than user not found, we want to throw an exception to indicate that something went wrong during the process.
                if (ex is null or { errorResponse: null or { title: not "RESOURCE_NOT_FOUND" } })
                    throw new UGSException(ex?.Message ?? string.Empty, ex, ex?.content);
            }
           
            try
            {
                // Check if the email is already registered in Firebase
                var firebaseSignInResponse = await FirebaseApiHelper.SignInByEmailAsync(email, password, _logger).ConfigureAwait(false);
                if (firebaseSignInResponse != null)
                    throw new FirebaseException($"The email {email} is already registered in Firebase.");
            }

            // If the user is not found in Firebase, it will throw a FirebaseException that we will catch to check the error code and continue the flow
            catch (FirebaseException ex)
            {
                // Firebase Auth user not found is semantic, not HTTP.
                // This means that Firebase returns a 400 Bad Request with a specific error code in the response body,
                // so we need to check the error code/message to identify this case and not treat it as an unexpected error.
                if (ex.HasErrorCode("EMAIL_NOT_FOUND", "NOT_FOUND", "Not Found", "INVALID_LOGIN_CREDENTIALS"))
                    _logger?.LogInformation("Firebase: User not found, continuing flow");
                else
                    throw ex.FirebaseAuthException();
            }
        }

        /// Try to sign up the user in UGS using the provided credentials and then set the username using the specific API.
        async Task<UGSAuthResponseData> SignUpInUGS(string username, string password)
        {
            try
            {
                // Call the helpers to sign up in UGS
                var signUpDataResponse = await UGSApiHelper.SignUpByCredentials(executionContext, username, password).ConfigureAwait(false);
                if (signUpDataResponse is null)
                    throw new Exception("Failed to sign up in UGS. Response is empty.");

                // Extract the player ID and the ID token from the sign-up response to set the username in UGS using the specific API that requires them as parameters
                var signUpPlayerId = signUpDataResponse.userId;
                var signUpIdToken = signUpDataResponse.idToken;

                // Set the username in UGS using the specific API that requires the player ID and the ID token obtained in the sign-up response as parameters
                var registeredName = await UGSApiHelper.SetUsername(executionContext, 
                    newName: username, alternativePlayerID: signUpPlayerId, alternativeAccessToken: signUpIdToken,
                    _logger: _logger).ConfigureAwait(false);

                // If the username was set successfully, we will log it. If the response is null or empty, it means that the username was not set successfully, so we will log an error.
                if (!string.IsNullOrEmpty(registeredName))
                    _logger?.LogInformation($"UGS user created successfully. User ID: {signUpDataResponse.userId}");

                // If the response is null, it means that the username was not set successfully, so we will throw an exception to indicate that the sign-up process failed.
                else
                    _logger?.LogError("Failed to set username in UGS after sign-up.");

                // Return the sign-up response obtained in UGS, which contains the player ID and the ID token that we will need to link the UGS user with the Firebase user and to update the user data in UGS with the Firebase information obtained.
                return signUpDataResponse;
            }
            catch (UGSException ex)
            {
                // UGS Auth user already exists is semantic, not HTTP.
                // This is used to inform frontend the multiple possibility of the error, so we need to check the error code/message
                // to identify this case and not treat it as an unexpected error.
                throw ex.UGSAuthException();
            }
            catch (Exception)
            {
                _logger?.LogError("Unexpected error during UGS sign-up.");
                throw;
            }
        }

        /// Try to sign up the user in Firebase using the provided credentials and then update the Firebase user profile with the username to link it to UGS
        async Task<FirebaseAuthResponseData> SignUpInFirebase(string email, string password, string username)
        {
            try
            {
                // Try to sign up in Firebase using the credentials
                var signUpResponse = await FirebaseApiHelper.SignUpByCredentials(email, password).ConfigureAwait(false);
                if (signUpResponse is null)
                    throw new FirebaseException("Empty response received from Firebase API.");

                _logger?.LogInformation($"Firebase user created successfully. User ID: {signUpResponse.localId}");

                // Update Firebase user profile with the username ot link it to UGS
                var firebaseBasicResponse = await FirebaseApiHelper.UpdateFirebaseUserProfile(signUpResponse.idToken, signUpResponse.refreshToken, _logger, 
                    ("displayName", username)) ?? throw new FirebaseException("Empty response received from Firebase API.");

                // Refresh the context data with the new tokens
                // Exist the possibility that the ID token obtained in the sign-up response is detected as expired and refreshed automatically by the Firebase helper,
                // so we need to make sure to use the refreshed tokens if that is the case to link the Firebase user with the UGS one and update the user data in UGS
                // with the Firebase information obtained.
                if (signUpResponse.idToken != firebaseBasicResponse.idToken)
                    signUpResponse.idToken = firebaseBasicResponse.idToken;
                if (signUpResponse.refreshToken != firebaseBasicResponse.refreshToken)
                    signUpResponse.refreshToken = firebaseBasicResponse.refreshToken;

                _logger?.LogInformation("Firebase user profile updated successfully. User ID: {LocalId}", signUpResponse.localId);
                return signUpResponse;
            }
            catch (FirebaseException ex)
            {
                _logger?.LogError($"Firebase Exception during sign-up: {ex.Message}");
                throw ex.FirebaseAuthException();
            }
            catch (Exception)
            {
                _logger?.LogError("Unexpected error during Firebase sign-up.");
                throw;
            }
        }
        #endregion
    }
}
