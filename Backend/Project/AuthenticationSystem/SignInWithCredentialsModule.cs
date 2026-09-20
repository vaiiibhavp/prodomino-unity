using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.AuthenticationSystem;

public class SignInWithCredentialsModule(ILogger<SignInWithCredentialsModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<SignInWithCredentialsModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Sign-In function for Unity Gaming Services and Firebase using Username & Password.<br></br>
    /// </summary>
    /// <param name="executionContext"></param>
    /// <param name="parametersEncryptedJson">It require: credentials and password</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    [CloudCodeFunction(nameof(SignInWithCredentials))]
    public async Task<string> SignInWithCredentials(IExecutionContext executionContext, string parametersEncryptedJson)
    {
        // Validate the execution context
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Register the anonymous player ID and accessToken to make de encryption with if instead of the one that is going to be used
        var basePlayerID = executionContext.PlayerId;
        var baseAccessToken = executionContext.AccessToken;
        if (string.IsNullOrEmpty(basePlayerID))
            throw new Exception("Player ID is invalid or null.");

        // Get the data from the encrypted parameters JSON
        var data = BackendHelper.ValidateEncriptedParameters
            (parametersEncryptedJson,
            basePlayerID,
            baseAccessToken);

        // Validate entry data
        await ValidateSignInData(data).ConfigureAwait(false);

        // Extract the credentials and password from the data dictionary, ensuring they are strings and not null
        var credentials = data["credentials"] is string usernameString ? usernameString : string.Empty;
        var password = data["password"] is string passwordString ? passwordString : string.Empty;

        // Define the keys for the data we want to load from UGS
        var profileKey = CloudSaveProperties.Profile.ToString();
        var analyticsKey = CloudSaveProperties.Analytics.ToString();
        var emailKey = CloudSaveProperties.Email.ToString();
        var accountCreationKey = CloudSaveProperties.AccountCreatedAt.ToString();
        var clubKey = CloudSaveProperties.Club.ToString();

        // Define the keys for the firebase access data
        var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
        var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
        var firebaseProviderIDKey = CloudSaveProperties.FirebaseProviderID.ToString();
        var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

        // Initialize the variables that will store the user data loaded from UGS to check if we need to update any data in UGS or Firebase and to ensure that the user profile is complete with the necessary data to work with both platforms.
        var playerProfileData = default(PlayerProfileData);
        var playerAnalyticsData = default(AnalyticsData);
        var accountCreatedAt = default(string);
        var emailAssociatedToUGS = default(string);
        var clubName = default(string);

        var firebaseID = default(string);
        var firebaseIdToken = default(string);
        var firebaseRefreshToken = default(string);

        var profileIconId = default(string);

        // Initialize the variable that will store the credential used to sign in, which can be either the email or the username depending on the case,
        // to be used later to generate the lefting credential that is going to be returned and used in the client to link the account in Firebase and UGS.
        var leftingCredential = default(string);
        var usernameAssociatedToUGS = default(string);

        // Initialize the UGSAuthResponseData that will be used to store the UGS authentication response data obtained in the sign-in process to update the UGS user data
        var playerAuthResponseData = default(UGSAuthResponseData);

        // Initialize the FirebaseAuthResponseData that will be used to store the Firebase authentication response data obtained in the sign-in process to update the UGS user data
        var firebaseAuthResponseData = default(FirebaseAuthResponseData);

        // Check if the credentials are an email or a username
        var isSignInWithEmail = credentials.Contains('@');
        var signInMethod = (Func<string, string, Task>)(isSignInWithEmail
            ? SignInWithEmailPassword
            : SignInWithUsernamePassword);

        // Try to sign in and catch errors if the user does not exist
        await signInMethod(credentials, password);
        if (string.IsNullOrEmpty(leftingCredential))
            throw new UGSException("Failed to sign in. No credentials provided.");

        // In this momenti, both playerAuthResponseData and firebaseAuthResponseData should be filled with the corresponding data obtained in the sign-in process,
        // if not, it means that the sign-in process failed in one of the platforms and we should throw an exception.
        if (playerAuthResponseData is null || firebaseAuthResponseData is null)
            throw new UGSException("Failed to sign in. Authentication is incomplete.");

        /*
            At this moment, the user is authenticated in both platforms, we have the authentication response data from both UGS and Firebase, 
            and we have loaded the user data from UGS to check if we need to update any data in UGS or Firebase and to ensure that 
            the user profile is complete with the necessary data to work with both platforms.
         */

        // Get the player ID from the UGS authentication response data to be used in the following validations and operations.
        var playerId = playerAuthResponseData.userId;

        // Validate if the username associated with the email is the same that the one used to sign in
        await AuthenticationSystem_Helper.ValidateUGSUsername(executionContext, _gameApiClient,
            playerId: playerAuthResponseData.userId, idToken: playerAuthResponseData.idToken, 
            usernameToCheck: usernameAssociatedToUGS, 
            _logger: _logger);

        // Once we have the Firebase response, try to update the Firebase user profile with the UGS username if it doesn't exist to link them with the same display name
        //await AuthenticationSystem_Helper.ValidateFirebaseUsername(executionContext, 
        //    firebaseAuthResponseData, 
        //    ugsUsername: usernameAssociatedToUGS,
        //    _logger: _logger).ConfigureAwait(false);

        // Validate the club name associated with the user to ensure that the user is in the correct club according to the one registered in UGS, and if not, throw an exception since we don't want to link accounts with different clubs to avoid account takeover issues.
        await AuthenticationSystem_Helper.ValidateClubName(executionContext, _gameApiClient,
            firebaseAuthResponseData,
            userUnityId: playerId, username: usernameAssociatedToUGS, clubName: clubName,
            _logger: _logger).ConfigureAwait(false);

        // Try to ensure the user exists in Realtime Database
        await AuthenticationSystem_Helper.TryToEnsureUserExistsInRTDB(executionContext, _gameApiClient,
            firebaseAuthResponseData, 
            playerId: playerId, profileIconID: profileIconId!, optionalDisplayName: usernameAssociatedToUGS,
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

        // Use the base player ID for encryption instead of the one used to sign in.
        // This is to ensure that the data can be decrypted by the client.
        var derivedKey = SecurityHelper.DeriveKey(basePlayerID, baseAccessToken);
        var derivedIv = SecurityHelper.DeriveIV(basePlayerID, baseAccessToken);

        var leftingCredentialJson = JsonConvert.SerializeObject(leftingCredential);
        var leftingCredentialEncrypted = SecurityHelper.EncryptData(leftingCredentialJson, derivedKey, derivedIv);
        var leftingCrendialEncryptedJson = JsonConvert.SerializeObject(leftingCredentialEncrypted);

        _logger?.LogInformation("User signed up successfully with credentials. User ID: {PlayerId}",
            playerAuthResponseData.userId);

        return leftingCrendialEncryptedJson;

        /// Load the user data from UGS to check if we need to update any data in UGS or Firebase and to ensure that the user profile is complete with the necessary data to work with both platforms.
        async Task LoadPlayerData(string playerId)
        {
            // Once the user is signed in both in UGS and Firebase, we will try to load the user data from UGS to check if we need to update any data in UGS or Firebase
            // and to ensure that the user profile is complete with the necessary data to work with both platforms.
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
                customID: null, alternativePlayerID: playerId, isThrowingException: false,
                profileKey, analyticsKey, accountCreationKey, emailKey, clubKey,
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey).ConfigureAwait(false);

            // Get the player profile data
            playerProfileData = playerDataResponse?.FirstOrDefault(x => x?.key == profileKey)?.value?.ToObject<PlayerProfileData>() ?? new();

            // Get the player analytics data
            playerAnalyticsData = playerDataResponse?.FirstOrDefault(x => x?.key == analyticsKey)?.value?.ToObject<AnalyticsData>() ?? new();

            // Try to get the account creation date
            accountCreatedAt = playerDataResponse?.FirstOrDefault(x => x?.key == accountCreationKey)?.value?.ToObject<string>() ?? string.Empty;

            // Try to get the email associated
            emailAssociatedToUGS = playerDataResponse?.FirstOrDefault(x => x?.key == emailKey)?.value?.ToObject<string>() ?? string.Empty;

            // Try to get the club name
            clubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubKey)?.value?.ToObject<string>() ?? string.Empty;

            // Try to get the firebase access data
            firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            firebaseIdToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            // Determine the profile icon ID to use in Realtime Database, prioritizing the one registered in UGS, then the optional one provided in the parameters, and if none of them exist, use an empty string
            profileIconId = !string.IsNullOrEmpty(playerProfileData.profileIconID)
                ? playerProfileData.profileIconID
                : string.Empty;
        }

        /// Check if the credentials provided are valid, ensuring that the required keys are present, the values are not empty, 
        /// and that they comply with the expected structure for usernames, emails, and passwords.
        async Task ValidateSignInData(Dictionary<string, object> data)
        {
            // Check if the data dictionary is null or empty
            if (data is null || data.Count == 0)
                throw new UGSException("Invalid input data. Provide credentials and password.");

            // Check if the data dictionary contains the required keys and values
            var requiredKeys = new[] { "credentials", "password" };
            foreach (var key in requiredKeys)
                if (!data.TryGetValue(key, out var value)
                    || value is not string valueJson
                    || string.IsNullOrEmpty(valueJson))
                    throw new UGSException($"{key} cannot be empty.");

            // Initialize the credentials validator and set the environment ID
            await new CloudCodeCredentialsValidator(_gameApiClient, executionContext).Initialize();

            // Check if the credentials are using the correct structure for username or email
            var credentials = data["credentials"] is string credentialsString ? credentialsString : null;
            if (credentials is not null)
            {
                var isUsername = !credentials.Contains('@');

                // Check if the username is valid
                if (isUsername && !CredentialsValidator.IsValidUsername(credentials))
                    throw new UGSException($"The username {credentials} is not valid");

                // Check if the email accomplish with the structure and if it's not a temporal email
                else if (!isUsername && !CredentialsValidator.IsValidEmail(credentials))
                    throw new UGSException($"The email {credentials} is not valid");
            }

            // Check if the password is using the correct structure
            if (!CredentialsValidator.IsValidPassword(data["password"] is string password ? password : string.Empty))
                throw new UGSException($"The password {data["password"]} is not strong enough");
        }

        // This method try to sign in in Firebase using the email and password, then it will try to Sign-in in UGS using the username got from Firebase and password.
        // If the sign-in fails and the user previously was signed in in Firebase, it will change the password in UGS according to the one in Firebase
        async Task SignInWithEmailPassword(string email, string password)
        {
            try
            {
                // Authenticate in Firebase and retrieve the account's username.
                firebaseAuthResponseData = await FirebaseApiHelper.SignInByEmailAsync(email, password).ConfigureAwait(false);
                if (firebaseAuthResponseData is null or { displayName: null or "" })
                    throw new FirebaseException("Failed to sign in in Firebase. Response is empty.");

                // Check if the username is null or empty
                var firebaseAssociatedUsername = firebaseAuthResponseData.displayName;

                // Sign-in in UGS using the username got from Firebase and password
                playerAuthResponseData = await SignInInUGS(executionContext, firebaseAssociatedUsername, password).ConfigureAwait(false);
                if (playerAuthResponseData is null)
                    throw new UGSException("Failed to sign in in UGS. Response is empty.");

                // Once the user is signed in both in UGS and Firebase, we will try to load the user data from UGS to check if we need to update any data in UGS or Firebase
                await LoadPlayerData(playerAuthResponseData.userId).ConfigureAwait(false);

                // If the sign-in in UGS fails and the user previously was signed in in Firebase, it will change the password in UGS according to the one in Firebase. This logic is handled inside the SignInInUGS method.
                leftingCredential = firebaseAssociatedUsername;

                // Save the username associated to UGS to be used in the client to link the account in Firebase and UGS.
                usernameAssociatedToUGS = playerAuthResponseData.user?.username;
            }
            catch (UGSException ex)
            {
                _logger?.LogError("UGS authentication failed for email {Email}. Error: {Message}",
                    email, ex.Message);

                // If the authentication in UGS fails, we will not try to sign in in Firebase since we want to avoid signing in in Firebase with an account that doesn't have access to UGS to avoid account takeover issues, so we will throw the exception directly.
                throw ex.UGSAuthException();
            }
            catch (FirebaseException ex)
            {
                _logger?.LogError("Firebase authentication failed for email {Email}. Error: {Message}",
                    email, ex.Message);

                // If the authentication in Firebase fails, we will not try to sign in in UGS since we want to avoid signing in in UGS with an account that doesn't have access to Firebase to avoid account takeover issues, so we will throw the exception directly.
                throw ex.FirebaseAuthException();
            }
        }

        // This method try to sign in in Firebase using the username associated email and password, then it will try to Sign-in in UGS using the username and password.
        // If the sign-in in UGS fails and the user previously was signed in in Firebase, it will change the password in UGS according to the one in Firebase
        async Task SignInWithUsernamePassword(string username, string password)
        {
            try
            {
                // Get the user data from UGS using the username to retrieve the Player ID (this implementation was needed to start ChangePassword validation)
                var userDataResponse = await UGSApiHelper.GetUserByUsernameAsync(_gameApiClient, executionContext, username).ConfigureAwait(false);
                if (userDataResponse is null)
                    throw new UGSException("Failed to get user by username. Response is empty.");

                // Override the context data with the user ID to load the user data and check the email associated with the account to sign in in Firebase with the correct email and password
                await LoadPlayerData(userDataResponse.id).ConfigureAwait(false);

                // Sign-in in Firebase according the uid obtained by UGS
                firebaseAuthResponseData = await FirebaseApiHelper.SignInByEmailAsync(emailAssociatedToUGS!, password).ConfigureAwait(false);
                if (firebaseAuthResponseData is null)
                    throw new FirebaseException("Failed to sign in in Firebase. Response is empty.");

                // Sign-in in UGS and return the account UID to be used to log-in in Firebase registered in the sign-up process
                playerAuthResponseData = await SignInInUGS(executionContext, username, password);
                if (playerAuthResponseData is null)
                    throw new UGSException("Failed to sign in in UGS. Response is empty.");

                // If the sign-in in UGS fails and the user previously was signed in in Firebase, it will change the password in UGS according to the one in Firebase. This logic is handled inside the SignInInUGS method.
                leftingCredential = emailAssociatedToUGS;

                // Save the username associated to UGS to be used in the client to link the account in Firebase and UGS.
                usernameAssociatedToUGS = username;
            }
            catch (UGSException ex)
            {
                _logger?.LogError("UGS authentication failed for username {Username}. Error: {Message}",
                    username, ex.Message);

                // If the authentication in UGS fails, we will not try to sign in in Firebase since we want to avoid signing in in Firebase with an account that doesn't have access to UGS to avoid account takeover issues, so we will throw the exception directly.
                throw ex.UGSAuthException();
            }
            catch (FirebaseException ex)
            {
                _logger?.LogError("Firebase authentication failed for username {Username}. Error: {Message}",
                    username, ex.Message);

                // If the authentication in Firebase fails, we will not try to sign in in UGS since we want to avoid signing in in UGS with an account that doesn't have access to Firebase to avoid account takeover issues, so we will throw the exception directly.
                throw ex.FirebaseAuthException();
            }
        }
    }

    /// <summary>
    /// Authenticates the user in UGS and retrieves their account information.
    /// </summary>
    /// <param name="contextData">Current execution context.</param>
    /// <param name="username">User's username.</param>
    /// <param name="password">User's password.</param>
    /// <returns>User's Player Auth ID.</returns>
    /// <exception cref="UGSException">Thrown when sign-in fails due to incorrect credentials or other issues.</exception>
    private async Task<UGSAuthResponseData> SignInInUGS(IExecutionContext executionContext, string username, string password)
    {
        try
        {
            // Call the helpers to sign in in UGS
            var signInDataResponse = await UGSApiHelper.SignInByCredentials(executionContext, username, password).ConfigureAwait(false);
            if (signInDataResponse is null or { userId: null or "" })
                throw new UGSException("Failed to sign up in UGS. Response is empty.");

            return signInDataResponse;
        }
        catch (UGSException ex)
        {
            // If the user is recovering, change the password in UGS according to the one in Firebase
            // NOTE: only change the password if previously the user was signed in in Firebase
            if (ex is not null and { errorResponse: not null and { status: 400 } and { title: "WRONG_USERNAME_PASSWORD" } })
            { 
                await UGSApiHelper.ChangePassword(_gameApiClient, executionContext, password);
            
                // Once the password is changed, try to sign in again
                var signInDataResponse = await UGSApiHelper.SignInByCredentials(executionContext, username, password).ConfigureAwait(false);
                if (signInDataResponse is null)
                    throw new UGSException($"\nFailed Sign-In With Credentials. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);

                return signInDataResponse;
            }
            throw;
        }
    }

}
