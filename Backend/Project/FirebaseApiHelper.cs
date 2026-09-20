using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;
using static HelperSharedLibrary.FirestoreClubData;
using static HelperSharedLibrary.PlayerPurchasesData;

namespace Backend;

internal class FirebaseApiHelper
{
    internal enum FirebaseService
    {
        Auth,
        Firestore,
        RealtimeDatabase,
        FirebaseFunctions
    }


    internal const string clubsCollectionIdKey = "clubs";
    internal const string clubsChatsCollectionIdKey = "clubChats";
    internal const string purchasesCollectionIdKey = "purchases";
    internal const string leaderboardsCollectionIdKey = "leaderboards";

    /// <summary>
    /// Sends an asynchronous HTTP request to a specified Firebase service endpoint with optional payload, headers, and
    /// content type, and returns the response.
    /// </summary>
    /// <param name="endpoint">The relative endpoint URL for the Firebase service.</param>
    /// <param name="payload">The request body payload to send, or null if not required.</param>
    /// <param name="httpMethod">The HTTP method to use for the request. Defaults to POST.</param>
    /// <param name="contentTypeString">The content type of the request body. Defaults to 'application/json'.</param>
    /// <param name="customHeaders">Optional custom headers to include in the request.</param>
    /// <param name="firebaseService">The Firebase service to which the request is sent.</param>
    /// <param name="logger">Optional logger for diagnostic or error information.</param>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response from the Firebase service.</returns>
    /// <exception cref="FirebaseException">Thrown when an error occurs during request preparation, execution, or response validation.</exception>
    private static async Task<RestResponse> ExecuteRequestAsync
        (string endpoint,
        object? payload,
        Method httpMethod = Method.Post,
        string contentTypeString = "application/json",
        Dictionary<string, string>? customHeaders = null,
        FirebaseService firebaseService = FirebaseService.Auth,
        ILogger? logger = null)
    {
        // Prepare the RestRequest with the specified endpoint, HTTP method, and content type. We also add any custom headers provided.
        var request = new RestRequest(endpoint) { Method = httpMethod };

        // Set the content type header for the request based on the provided contentTypeString parameter, which defaults to "application/json"
        request.AddHeader("Content-Type", contentTypeString);

        // Add the payload to the request body if it's not null
        if (payload is not null)
        {
            try
            {
                var contentType = (ContentType)contentTypeString;
                request.AddBody(payload, contentType);
            }
            catch (Exception ex)
            {
                throw new FirebaseException("Firebase request crashed during casting body content type", ex, ex.Message);
            }
        }

        if (customHeaders is not null)
            foreach (var header in customHeaders)
                request.AddHeader(header.Key, header.Value);

        // Select the appropriate RestClient based on the Firebase service being accessed
        var restClient = default(RestClient);
        try
        {
            restClient = firebaseService switch
            {
                FirebaseService.Auth => FirebaseBackend._authClient,
                FirebaseService.Firestore => FirebaseBackend._firestoreClient,
                FirebaseService.RealtimeDatabase => FirebaseBackend._realtimeDatabaseClient,
                FirebaseService.FirebaseFunctions => FirebaseBackend._functionsClient,
                _ => throw new ArgumentOutOfRangeException(nameof(firebaseService), firebaseService, null)
            };

            // Perform a sanity check to ensure the RestClient and request are not null before executing the request
            if (restClient is null || request is null)
                throw new FirebaseException("Firebase request crashed due main fields are null");

        }
        catch (Exception ex)
        {
            throw new FirebaseException(
                "Firebase request crashed getting rest client",
                ex,
                ex.Message
            );
        }

        // Once we have the RestClient and request properly configured, we execute the request asynchronously and handle any exceptions that may occur during execution
        try
        {
            // Execute the request asynchronously and validate the response
            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (response is null)
                throw new FirebaseException($"Firebase request failed. Response is missing");

            if (!response.IsSuccessful)
                throw new FirebaseException($"Firebase request failed. Status code: {response.StatusCode}\n\nResponse:\n{response.ErrorException?.Message}\n", response.ErrorException, response.Content);

            if (string.IsNullOrEmpty(response.Content))
                throw new FirebaseException($"Empty response received. Status code: {response.StatusCode}\n\nResponse:\n{response.ErrorException?.Message}\n", response.ErrorException);
       
            return response;
        }
        catch (Exception ex)
        {
            throw new FirebaseException(
                "Firebase request crashed during ExecuteAsync",
                ex,
                ex.Message
            );
        }
    }


    /// <summary>
    /// Generates a backend ID token by creating and exchanging a custom Firebase token.
    /// </summary>
    /// <param name="executionContext">The execution context containing relevant request information.</param>
    /// <param name="gameApiClient">The game API client used for token generation.</param>
    /// <returns>A backend ID token as a string.</returns>
    /// <exception cref="Exception">Thrown if custom token generation or backend ID token creation fails.</exception>
    internal static async Task<string> GetBackendIdTokenAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient)
    {
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        return await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");
    }


    #region Authentication
    /// <summary>
    /// Validates the Firebase response data for the LookUp request
    /// </summary>
    /// <param name="response"></param>
    /// <param name="isThrowingException"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static FirebaseLookUpResponseData? ValidFirebaseLookUpResponseData(RestResponse response, bool isThrowingException = true)
    {
        var content = response.Content ?? ""; // The content is previously checked to be not null or empty
        var authResponseData = JsonConvert.DeserializeObject<FirebaseLookUpResponseData>(content);
        if (isThrowingException && authResponseData is null or { users: null or { Length: 0 } })
            throw new FirebaseException("Invalid response format: missing 'users' field.");

        return authResponseData;
    }

    /// <summary>
    /// Validates the Firebase response data for the SignIn request
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private static FirebaseAuthResponseData? ValidFirebaseAuthResponseData(RestResponse response, bool isThrowingException = true)
    {
        var content = response.Content ?? ""; // The content is previously checked to be not null or empty
        var authResponseData = JsonConvert.DeserializeObject<FirebaseAuthResponseData>(content);
        if (isThrowingException && authResponseData is null or { idToken: null or "" } or { localId: null or "" })
            throw new FirebaseException("Invalid response format: missing 'idToken' or 'localId' field.");

        return authResponseData;
    }

    /// <summary>
    /// Validates the Firebase response data for the SendEmail request
    /// </summary>
    private static FirebaseSendEmailResponse? ValidFirebaseSendEmailResponse(RestResponse response, bool isThrowingException = true)
    {
        var content = response.Content ?? ""; // The content is previously checked to be not null or empty
        var authResponseData = JsonConvert.DeserializeObject<FirebaseSendEmailResponse>(content);
        if (isThrowingException && authResponseData is null or { kind: null or "" } or { email: null or "" })
            throw new FirebaseException("Invalid response format: missing 'kind' or 'email' field.");

        return authResponseData;
    }


    /// <summary>
    /// Performs a Firebase user lookup using the provided ID token and refresh token, returning user information if
    /// successful.
    /// </summary>
    /// <param name="idToken">The Firebase ID token to use for the lookup.</param>
    /// <param name="refreshToken">The refresh token associated with the Firebase user.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the Firebase user lookup response
    /// data, or null if not found.</returns>
    /// <exception cref="FirebaseException">Thrown when the Firebase API returns an error or an empty response.</exception>
    internal static async Task<FirebaseLookUpResponseData> LookUpAsync(string? idToken, string? refreshToken)
    {
        (idToken, _, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);

        // The Firebase API for looking up user information requires the idToken to be sent in the payload
        var payload = new { idToken };

        // Execute the request to the Firebase API and validate the response
        var response = await ExecuteRequestAsync($"v1/accounts:lookup?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);

        // Validate the response data and return it, or throw an exception if it's invalid
        var responseData = ValidFirebaseLookUpResponseData(response);
        if (responseData is null)
            throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

        // Attach the tokens to the response data for later use
        responseData.idToken = idToken;
        responseData.refreshToken = refreshToken;
        return responseData;
    }

    /// <summary>
    /// Generates a Firebase custom token for a Unity user ID.
    /// This token can later be exchanged for an ID token + refresh token using:
    ///     accounts:signInWithCustomToken
    /// </summary>
    internal static async Task<string> GenerateCustomTokenForUnityAsync(
        string unityUserId,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");

        try
        {
            // Generates a signed JWT using Firebase Admin SDK
            var customToken = await FirebaseAdmin.Auth.FirebaseAuth
                .DefaultInstance
                .CreateCustomTokenAsync(unityUserId);

            logger?.LogInformation($"Generated custom token for Unity ID '{unityUserId}'.");

            return customToken;
        }
        catch (Exception ex)
        {
            logger?.LogError($"Failed to generate custom token: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates the provided ID token and refresh token, ensuring the ID token is not expired and is in a valid
    /// format.
    /// </summary>
    /// <param name="idToken">The JWT ID token to validate.</param>
    /// <param name="refreshToken">The refresh token associated with the ID token.</param>
    /// <returns>True if the ID token is valid and not expired; otherwise, false.</returns>
    /// <exception cref="FirebaseException">Thrown when the ID token cannot be validated due to a Firebase-related error.</exception>
    internal static bool VerifyIDToken(string? idToken, string? refreshToken)
    {
        // If either token is null or empty, we consider the ID token invalid
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(refreshToken))
            return false;

        try
        {
            // Intentar decodificar el idToken
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);

            // Check if the id token is invalid format
            if (token?.Payload?.Expiration is null)
                return false;

            // Get the expiration date from the token payload
            var expirationUnixTime = token.Payload.Expiration;
            var expirationDate = DateTimeOffset.FromUnixTimeSeconds(expirationUnixTime.Value).UtcDateTime;

            // Check if the token is still valid
            return expirationDate > DateTime.UtcNow;
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to validate Firebase idToken. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    internal static async Task<bool> VerifyIDTokenAdmin(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid parameters: idToken");

        var decodedToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
        return decodedToken != null;
    }

    /// <summary>
    /// Asynchronously refreshes the Firebase ID token using the provided refresh token and returns the new ID token,
    /// access token, and refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token used to obtain new Firebase tokens.</param>
    /// <returns>A tuple containing the new ID token, access token, and refresh token.</returns>
    /// <exception cref="ArgumentException">Thrown if the refresh token is null or empty.</exception>
    /// <exception cref="Exception">Thrown if the response from Firebase does not contain valid token data.</exception>
    /// <exception cref="FirebaseException">Thrown if the request to refresh the Firebase ID token fails or returns an error.</exception>
    internal static async Task<(string newIdToken, string newAccessToken, string newRefreshToken)> RefreshFirebaseIdTokenAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new ArgumentException("Firebase refreshToken is required.");

        // The Firebase API for refreshing ID tokens requires the refresh token to be sent in the payload, along with the grant type
        var payloadCollection = new Dictionary<string, object>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        // In this case, we need to encode the payload as application/x-www-form-urlencoded
        var encodedPayload = string.Join("&", payloadCollection.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString((string)kvp.Value)}"));

        try
        {
            // Execute the request to the Firebase API to refresh the ID token using the refresh token
            var response = await ExecuteRequestAsync(
                $"https://securetoken.googleapis.com/v1/token?key={FirebaseBackend._firebaseConfigData.apiKey}",
                encodedPayload,
                contentTypeString: "application/x-www-form-urlencoded"
            );

            if (response is null or { Content: null or "" })
                throw new FirebaseException($"Empty response received from Firebase API");

            var responseData = JsonConvert.DeserializeObject<JObject>(response.Content);

            // Extract the new ID token, access token, and refresh token from the response data
            var newIdToken = responseData?["id_token"]?.ToString() ?? throw new Exception("Invalid token response.");
            var newRefreshToken = responseData?["refresh_token"]?.ToString() ?? throw new Exception("Invalid refresh token response.");
            var newAccessToken = responseData?["access_token"]?.ToString() ?? throw new Exception("Invalid access token response.");

            // Return the new tokens as a tuple
            return (newIdToken, newAccessToken, newRefreshToken);

        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to refresh Firebase idToken. Status code: {ex.StatusCode}\n\nResponse: {ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>
    /// Retrieves a valid Firebase ID token, access token, and refresh token, refreshing them if necessary.
    /// </summary>
    /// <param name="idToken">The current Firebase ID token, or null.</param>
    /// <param name="refreshToken">The current Firebase refresh token, or null.</param>
    /// <returns>A tuple containing a valid ID token, access token, and refresh token.</returns>
    internal static async Task<(string validIdToken, string validAccessToken, string validRefreshToken)> GetValidFirebaseIdTokenAsync(string? idToken, string? refreshToken)
    {
        // First, we perform a local validation of the idToken to check if it's well-formed and not expired
        var isValid = VerifyIDToken(idToken, refreshToken);
        if (isValid)
            return (idToken!, string.Empty, refreshToken!);

        // If the token is not valid, we attempt to refresh it using the refresh token
        var (newValidIDToken, newAccessToken, newValidRefreshToken) = await RefreshFirebaseIdTokenAsync(refreshToken!);

        // After refreshing, return the new valid ID token, access token, and refresh token
        return (newValidIDToken, newAccessToken, newValidRefreshToken);
    }

    /// <summary>
    /// Gets the display name of a Firebase user by their IDToken
    /// </summary>
    /// <param name="idToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseBasicResponse<string>> GetFirebaseUserDisplayNameAsync(string idToken, string refreshToken)
    {
        (idToken, _, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);

        try
        {
            var response = await LookUpAsync(idToken, refreshToken);
            var user = response?.users?.FirstOrDefault();
            if (user == null)
                throw new FirebaseException($"User not found in Firebase");

            return new(idToken, refreshToken, user.displayName);
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to get Firebase user display name. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>Checks if the email of a user is verified by Firebase</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseBasicResponse<bool>> IsEmailVerifiedAsync(string idToken, string refreshToken)
    {
        (idToken, _, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);

        try
        {
            var response = await LookUpAsync(idToken, refreshToken);
            var user = response?.users?.FirstOrDefault();
            if (user == null)
                throw new FirebaseException("User not found in Firebase.");

            return new(idToken, refreshToken, user.emailVerified);
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to check email verification. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>
    /// Updates a Firebase user's profile with the specified fields using the provided ID and refresh tokens.
    /// </summary>
    /// <param name="idToken">The Firebase ID token for authentication.</param>
    /// <param name="refreshToken">The refresh token to obtain a valid ID token if needed.</param>
    /// <param name="_logger">Optional logger for error reporting.</param>
    /// <param name="dataToUpdate">Key-value pairs representing the user profile fields to update.</param>
    /// <returns>A FirebaseBasicResponse containing the updated tokens, or null if the update fails.</returns>
    /// <exception cref="FirebaseException">Thrown when the Firebase API returns an error or an empty response.</exception>
    internal static async Task<FirebaseBasicResponse?> UpdateFirebaseUserProfile(string idToken, string refreshToken, 
        ILogger? _logger = null, 
        params (string key, string value)[] dataToUpdate)
    {
        (idToken, _, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);

        if (dataToUpdate is null or { Length: 0 })
        {
            _logger?.LogError("Could not validate idToken using local validation");
            return null;
        }

        // The Firebase API for updating user profiles requires the idToken to be sent in the payload, along with the fields to update
        var payloadCollection = new Dictionary<string, object> { { "idToken", idToken } };

        // Add the fields to update to the payload
        foreach (var fields in dataToUpdate)
            payloadCollection[fields.key] = fields.value;

        // Serialize the payload to JSON
        var payload = JsonConvert.SerializeObject(payloadCollection);

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:update?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            if (response is null)
                throw new FirebaseException($"Empty response received from Firebase API");

            return new(idToken, refreshToken);
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to update Firebase user profile. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>Sends an email verification to the user with the specified idToken</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseSendEmailResponse> SendVerifyEmailAsync(string idToken, string refreshToken, ILogger? logger = null)
    {
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        logger?.LogInformation($"[{nameof(SendVerifyEmailAsync)}] Sending email verification to user with valid idToken.");

        var payload = new Dictionary<string, object>()
        {
            { "requestType", "VERIFY_EMAIL" },
            { "idToken", idToken },
        };

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:sendOobCode?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            var responseData = ValidFirebaseSendEmailResponse(response);
            if (responseData is null)
                throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content); 
            
            logger?.LogInformation($"Email verification sent successfully");

            return responseData;
        }
        catch (FirebaseException ex)
        {
            logger?.LogError("Error sending email verification: {Message}", ex.Message);
            throw new FirebaseException($"\nFailed to send email verification. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>Sends an email verification to the user with the specified idToken</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task SendResetPasswordEmailAsync(string email)
    {
        var payloadCollection = new Dictionary<string, object>()
        {
            { "requestType", "PASSWORD_RESET" },
            { "email", email },
        };
        var payload = JsonConvert.SerializeObject(payloadCollection);

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:sendOobCode?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            if (!response.IsSuccessful)
            {
                string errorMessage = response.Content ?? "Unknown error";
                throw new Exception($"Firebase request failed: {errorMessage}");
            }

            if (string.IsNullOrEmpty(response.Content))
                throw new Exception("Empty response received.");
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to send email recovery. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }


    /// <summary>Sign-Up function for Firebase using email and password</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseAuthResponseData> SignUpByCredentials(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Invalid parameters: Email or password");

        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:signUp?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            var responseData = ValidFirebaseAuthResponseData(response);
            if (responseData is null)
                throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            return responseData;
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to sign up with credentials. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>
    /// Signs in a user to Firebase using email and password credentials.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="logger">Optional logger for capturing request and response details.</param>
    /// <returns>A FirebaseAuthResponseData object containing authentication information.</returns>
    /// <exception cref="FirebaseException">Thrown when email or password is invalid, or if the sign-in request fails.</exception>
    internal static async Task<FirebaseAuthResponseData> SignInByEmailAsync(string email, string password, 
        ILogger? logger = null)
    {
        // Validate input parameters to ensure email and password are not null or empty, as they are required for signing in
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            throw new FirebaseException("Email and password cannot be null or empty");

        // Construct the payload for the sign-in request, including the email, password, and a flag to return a secure token
        var payload = new
        {
            email = email,
            password = password,
            returnSecureToken = true
        };

        try
        {
            // Execute the request to the Firebase API for signing in with email and password, and validate the response
            var response = await ExecuteRequestAsync($"v1/accounts:signInWithPassword?key={FirebaseBackend._firebaseConfigData.apiKey}", payload, logger: logger);

            // Validate the response data to ensure it contains the expected fields (idToken and localId), and return it as a FirebaseAuthResponseData object
            var responseData = ValidFirebaseAuthResponseData(response);

            // If the response data is null, it means the response format was invalid or missing required fields, so we throw a FirebaseException with details about the failure
            if (responseData is null)
                throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            return responseData;
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to sign in with email. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>Sign-In function for Firebase using a custom token</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseAuthResponseData> SignInWithCustomTokenAsync(string uid, string? optionalCustomToken = null)
    {
        if (string.IsNullOrEmpty(uid))
            throw new ArgumentException("Invalid parameters: uid");

        var customToken = optionalCustomToken ?? await GenerateCustomTokenForUnityAsync(uid);
        if (string.IsNullOrEmpty(customToken))
            throw new Exception("Custom token is null");

        var payload = new
        {
            token = customToken,
            returnSecureToken = true
        };

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:signInWithCustomToken?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            var responseData = ValidFirebaseAuthResponseData(response);
            if (responseData is null)
                throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            return responseData;
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to sign in with custom token. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }

    /// <summary>Sign-In function for Firebase using a custom token</summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    internal static async Task<FirebaseAuthResponseData> SignInWithOAuthCredential(string tokenType, string providerIDToken, string providerID, string domainExtension = ".com", ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(providerIDToken))
            throw new ArgumentException("Invalid parameters: access token");

        var payload = new
        {
            postBody = $"{tokenType}={providerIDToken}&providerId={providerID}{domainExtension}",
            requestUri = "https://playprodomino.com",
            returnSecureToken = true,
            returnIdpCredential = true
        };

        try
        {
            var response = await ExecuteRequestAsync($"v1/accounts:signInWithIdp?key={FirebaseBackend._firebaseConfigData.apiKey}", payload);
            var responseData = ValidFirebaseAuthResponseData(response);
            if (responseData is null)
                throw new FirebaseException($"Empty response received from Firebase API. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            return responseData;
        }
        catch (FirebaseException ex)
        {
            throw new FirebaseException($"\nFailed to sign in with OAuth credential. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}\n", ex, ex.content);
        }
    }
    #endregion

    #region Firestore
    /// <summary>
    /// Reads a document from Firestore
    /// </summary>
    internal static async Task<string?> GetDocumentAsync(string idToken, string refreshToken,
        string collection, string documentId, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Arguments cannot be null or empty");

        // Ensure the idToken is valid
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}/{documentId}";
        try
        {
            var response = await ExecuteRequestAsync(endpoint, null, Method.Get, customHeaders: headers, firebaseService: FirebaseService.Firestore);
            return response.Content;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error getting document from Firestore with endpont:\n{Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Reads all documents from a Firestore collection.
    /// </summary>
    internal static async Task<string?> GetCollectionAsync(
        string idToken,
        string refreshToken,
        string collection,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection))
            throw new ArgumentException("Arguments cannot be null or empty");

        // Ensure the idToken is valid
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}";
        try
        {
            var response = await ExecuteRequestAsync(
                endpoint,
                null,
                Method.Get,
                customHeaders: headers,
                firebaseService: FirebaseService.Firestore);

            if (string.IsNullOrWhiteSpace(response.Content))
                return null;

            // Firestore wraps results under a "documents" array — unwrap it for easier parsing
            var json = JObject.Parse(response.Content);
            var documents = json["documents"]?.ToString() ?? "[]";

            return documents;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error getting collection from Firestore: {Message}", ex.Message);
            return null;
        }
    }


    /// <summary>
    /// Creates a document in Firestore
    /// </summary>
    internal static async Task<string?> CreateDocumentAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string collection,
        object documentFields,
        string? documentId = null,
        ILogger? logger = null)
    {
        if (executionContext is null || gameApiClient is null || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(documentId) || documentFields is null)
            throw new ArgumentException("Arguments cannot be null or empty");

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        // Firestore REST expects {"fields": {...}}
        var payload = new
        {
            fields = documentFields
        };

        // Build endpoint with optional documentId
        var baseEndpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}";
        var endpoint = string.IsNullOrEmpty(documentId)
            ? baseEndpoint
            : $"{baseEndpoint}?documentId={documentId}";

        try
        {
            var response = await ExecuteRequestAsync(endpoint, payload, Method.Post, customHeaders: headers, firebaseService: FirebaseService.Firestore);
            return response.Content ?? "{}";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating document in Firestore: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Updates a document in Firestore
    /// </summary>
    internal static async Task<string> UpdateDocumentAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string collection, 
        string documentId, 
        object documentFields)
    {
        if (executionContext is null || gameApiClient is null || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(documentId) || documentFields is null)
            throw new ArgumentException("Arguments cannot be null or empty");

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        var payload = new
        {
            fields = documentFields
        };

        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}/{documentId}";
        var response = await ExecuteRequestAsync(endpoint, payload, Method.Patch, customHeaders: headers, firebaseService: FirebaseService.Firestore);
        return response.Content ?? "{}";
    }

    /// <summary>
    /// Partially updates (patches) specific fields in a Firestore document.
    /// </summary>
    internal static async Task<string> PatchDocumentAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string collection,
        string documentId,
        object documentFields,
        params string[] updateFieldPaths)
    {
        if (executionContext is null || gameApiClient is null || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(documentId) || documentFields is null || updateFieldPaths is null)
            throw new ArgumentException($"Arguments cannot be null or empty:\n\nExecutionContext is null: {executionContext is null}\nGameApiClient is null: {gameApiClient is null}");

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        // ✅ Firestore expects {"fields": {...}}. Wrap here again.
        var payload = new { fields = documentFields };

        // Build update mask query
        string maskParams = string.Join("&", updateFieldPaths.Select(f => $"updateMask.fieldPaths={f}"));
        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}/{documentId}?{maskParams}";

        var response = await ExecuteRequestAsync(endpoint, payload, Method.Patch, customHeaders: headers, firebaseService: FirebaseService.Firestore);
        return response.Content ?? "{}";
    }

    /// <summary>
    /// Deletes a document in Firestore
    /// </summary>
    internal static async Task<bool> DeleteDocumentAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string collection, 
        string documentId)
    {
        if (executionContext is null || gameApiClient is null || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(documentId))
            throw new ArgumentException("Arguments cannot be null or empty");

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents/{collection}/{documentId}";
        var response = await ExecuteRequestAsync(endpoint, default, Method.Delete, customHeaders: headers, firebaseService: FirebaseService.Firestore);
        return response.IsSuccessful;
    }

    /// <summary>
    /// Queries Firestore clubs ordered by score (descending) and returns the rank of a specific club.
    /// </summary>
    internal static async Task<int?> GetClubRankAsync(
        string idToken,
        string refreshToken,
        string collection,
        string targetClubId,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection))
            throw new ArgumentException("Invalid Firestore parameters");

        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        // Firestore runQuery endpoint
        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents:runQuery";

        // Query all clubs ordered by score descending
        var queryPayload = new
        {
            structuredQuery = new
            {
                from = new[]
                {
                    new { collectionId = collection }
                },
                orderBy = new[]
                {
                    new
                    {
                        field = new { fieldPath = "score" },
                        direction = "DESCENDING"
                    }
                },
                select = new
                {
                    fields = new[]
                    {
                        new { fieldPath = "score" },
                        new { fieldPath = "slogan" }
                    }
                }
            }
        };

        var response = await ExecuteRequestAsync(
            endpoint,
            queryPayload,
            Method.Post,
            customHeaders: headers,
            firebaseService: FirebaseService.Firestore);

        if (string.IsNullOrEmpty(response.Content))
            return null;

        var results = JArray.Parse(response.Content);

        // Iterate through results to find the target club
        int position = 1;
        foreach (var entry in results)
        {
            var documentName = entry["document"]?["name"]?.ToString();
            if (string.IsNullOrEmpty(documentName))
                continue;

            // Example document name: "projects/{projectId}/databases/(default)/documents/clubs/ClubName"
            if (documentName.EndsWith($"/{targetClubId}", StringComparison.OrdinalIgnoreCase))
                return position;

            position++;
        }

        // Not found
        return null;
    }

    /// <summary>
    /// Searches Firestore clubs by either normalizedName (if multi-word)
    /// or searchTokens (if single-word), returning up to the specified limit.
    /// Returns fully parsed FirestorePlayerClubData objects.
    /// </summary>
    internal static async Task<List<FirestoreClubData>> SearchClubsByNameAsync(
        string idToken,
        string refreshToken,
        string collection,
        string searchTerm,
        int limit = 10,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(searchTerm))
            throw new ArgumentException("Invalid Firestore parameters");

        // Refresh or obtain a valid Firebase access token
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        // Firestore REST endpoint
        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents:runQuery";

        // Normalize the search term using the shared helper
        var normalizedTerm = UGSApiHelper.NormalizeName(searchTerm);
        if (string.IsNullOrEmpty(normalizedTerm))
            return new List<FirestoreClubData>();

        // Determine whether it's a multi-word or single-word search
        bool isMultiWord = normalizedTerm.Contains(' ');

        object queryPayload;

        if (isMultiWord)
        {
            // Case 1: Multi-word search → use normalizedName prefix
            string startAt = normalizedTerm;
            string endAt = normalizedTerm + "\uf8ff";

            queryPayload = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = collection } },
                    orderBy = new[]
                    {
                        new
                        {
                            field = new { fieldPath = "normalizedName" },
                            direction = "ASCENDING"
                        }
                    },
                    where = new
                    {
                        compositeFilter = new
                        {
                            op = "AND",
                            filters = new object[]
                            {
                                new
                                {
                                    fieldFilter = new
                                    {
                                        field = new { fieldPath = "normalizedName" },
                                        op = "GREATER_THAN_OR_EQUAL",
                                        value = new { stringValue = startAt }
                                    }
                                },
                                new
                                {
                                    fieldFilter = new
                                    {
                                        field = new { fieldPath = "normalizedName" },
                                        op = "LESS_THAN_OR_EQUAL",
                                        value = new { stringValue = endAt }
                                    }
                                }
                            }
                        }
                    },
                    limit = limit
                }
            };

            logger?.LogInformation($"[Firestore] Multi-word search for '{normalizedTerm}' using normalizedName prefix.");
        } 
        
        else
        {
            // Case 2: Single-word search → use searchTokens array-contains
            queryPayload = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = collection } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "searchTokens" },
                            op = "ARRAY_CONTAINS",
                            value = new { stringValue = normalizedTerm }
                        }
                    },
                    limit = limit
                }
            };

            logger?.LogInformation($"[Firestore] Single-word search for '{normalizedTerm}' using searchTokens array.");
        }

        // Execute the query
        var response = await ExecuteRequestAsync(
            endpoint,
            queryPayload,
            Method.Post,
            customHeaders: headers,
            firebaseService: FirebaseService.Firestore);

        if (string.IsNullOrEmpty(response.Content))
            return new List<FirestoreClubData>();

        var rawResults = JArray.Parse(response.Content);
        var clubs = new List<FirestoreClubData>();

        foreach (var entry in rawResults)
        {
            var document = entry["document"];
            if (document == null)
                continue;

            try
            {
                var json = document.ToString(Formatting.None);
                var parsedClub = FirestoreClubData.ParseClubData(json);
                if (parsedClub != null)
                    clubs.Add(parsedClub);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error parsing Firestore club document: {ex.Message}");
            }
        }

        logger?.LogInformation($"[Firestore] Retrieved {clubs.Count} clubs matching search term '{normalizedTerm}'.");
        return clubs;
    }


    /// <summary>
    /// Retrieves the top clubs ordered by score in descending order (leaderboard style).
    /// Returns fully parsed FirestorePlayerClubData objects.
    /// </summary>
    internal static async Task<List<FirestoreClubData>> GetTopClubsByScoreAsync(
        string idToken,
        string refreshToken,
        string collection,
        int limit = 10,
        ILogger? logger = null)
    {
        // Validate parameters
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection))
            throw new ArgumentException("Invalid Firestore parameters");

        // Refresh or obtain a valid Firebase access token
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await GetValidFirebaseIdTokenAsync(idToken, refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        // Prepare authorization headers
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        // Firestore REST endpoint for queries
        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents:runQuery";

        // Build the Firestore structured query payload
        // This will request the top N clubs ordered by "score" in descending order.
        var queryPayload = new
        {
            structuredQuery = new
            {
                from = new[] { new { collectionId = collection } },
                orderBy = new[]
                {
                    new
                    {
                        field = new { fieldPath = "score" },
                        direction = "DESCENDING"
                    }
                },
                limit = limit
            }
        };

        // Execute Firestore query
        var response = await ExecuteRequestAsync(
            endpoint,
            queryPayload,
            Method.Post,
            customHeaders: headers,
            firebaseService: FirebaseService.Firestore);

        // Validate response content
        if (string.IsNullOrEmpty(response.Content))
            return new List<FirestoreClubData>();

        var rawResults = JArray.Parse(response.Content);
        var clubs = new List<FirestoreClubData>();

        // Parse each Firestore document result
        foreach (var entry in rawResults)
        {
            var document = entry["document"];
            if (document == null)
                continue;

            try
            {
                var json = document.ToString(Formatting.None);
                var parsedClub = FirestoreClubData.ParseClubData(json);
                if (parsedClub != null)
                    clubs.Add(parsedClub);
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error parsing club document: {ex.Message}");
            }
        }

        logger?.LogInformation($"Retrieved {clubs.Count} top clubs by score from Firestore.");

        return clubs;
    }

    /// <summary>
    /// Counts the total number of documents in a Firestore collection using the Aggregation Query API.
    /// Extremely cheap and efficient (does not read individual documents).
    /// </summary>
    internal static async Task<int> GetCollectionCountAsync(
        string idToken,
        string refreshToken,
        string collection,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(collection))
            throw new ArgumentException("Invalid arguments for counting collection documents.");

        // Refresh Firebase ID token to ensure validity
        var accessToken = string.Empty;
        (idToken, accessToken, refreshToken) = await RefreshFirebaseIdTokenAsync(refreshToken);
        if (string.IsNullOrEmpty(idToken))
            throw new ArgumentException("Invalid Firebase idToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {(string.IsNullOrEmpty(accessToken) ? idToken: accessToken)}" }
        };

        // Build Firestore aggregation query payload
        var payload = new
        {
            structuredAggregationQuery = new
            {
                aggregations = new[]
                {
                    new { count = new { }, alias = "count" } // The COUNT(*) operation
                },
                structuredQuery = new
                {
                    from = new[]
                    {
                        new { collectionId = collection }
                    }
                }
            }
        };

        var endpoint = $"v1/projects/{FirebaseBackend._firebaseConfigData.projectId}/databases/(default)/documents:runAggregationQuery";

        if (logger != null)
            logger.LogInformation($"Counting documents in Firestore collection: {collection}");

        try
        {
            var response = await ExecuteRequestAsync(
                endpoint,
                payload,
                Method.Post,
                customHeaders: headers,
                firebaseService: FirebaseService.Firestore);

            if (string.IsNullOrEmpty(response.Content))
                return 0;

            logger?.LogInformation("Firestore count response for {Collection}: {Response}", collection, response.Content);

            // Firestore returns an array of results
            var jsonArray = JArray.Parse(response.Content);
            var firstResult = jsonArray.FirstOrDefault()?["result"]?["aggregateFields"]?["count"]?["integerValue"]?.ToString();

            return int.TryParse(firstResult, out var count) ? count : 0;

        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"Error counting Firestore collection '{collection}': {ex.Message}");
            return 0;
        }
    }


    /// <summary>
    /// Builds the Firestore-compliant "iconData" array payload.
    /// Each data is serialized as a mapValue -> fields structure,
    /// compatible with Firestore's document JSON schema.
    /// </summary>
    internal static object BuildFirestoreIconData(FirestoreClubData clubData)
    {
        return new
        {
            mapValue = new
            {
                fields = new
                {
                    shieldId = new { stringValue = clubData.iconData!.shieldId },
                    textureId = new { stringValue = clubData.iconData.textureId },
                    centralImageId = new { stringValue = clubData.iconData.centralImageId },
                    shieldColorId = new { stringValue = clubData.iconData.shieldColorId },
                    textureColorId = new { stringValue = clubData.iconData.textureColorId },
                    centralImageColorId = new { stringValue = clubData.iconData.centralImageColorId },
                    backgroundColorId = new { stringValue = clubData.iconData.backgroundColorId }
                }
            }
        };
    }

    /// <summary>
    /// Builds the Firestore-compliant "members" array payload.
    /// Each member is serialized as a mapValue -> fields structure,
    /// compatible with Firestore's document JSON schema.
    /// </summary>
    internal static object BuildFirestoreMembersArray(IEnumerable<MemberData> members)
    {
        return new
        {
            arrayValue = new
            {
                values = members.Select(m => new
                {
                    mapValue = new
                    {
                        fields = new
                        {
                            // Firebase user ID (required for cross-reference)
                            firebaseMemberId = new { stringValue = m.firebaseMemberId },

                            // Unity ID (optional but useful for in-game lookups)
                            unityMemberId = new { stringValue = m.unityMemberId },

                            // Visible name in the UI
                            memberName = new { stringValue = m.memberName },

                            // Profile icon ID (stored as string)
                            profileIconId = new { stringValue = m.profileIconId },

                            // Number of victories (stored as integer string)
                            victories = new { integerValue = m.victories.ToString() },                            

                            // Join timestamp in Unix seconds
                            joinedDate = new { integerValue = m.joinedDate.ToString() },

                            // List of badge IDs (array of strings)
                            badges = new
                            {
                                arrayValue = new
                                {
                                    values = (m.badges ?? Array.Empty<string>())
                                        .Select(b => new
                                        {
                                            stringValue = b
                                        }).ToArray()
                                }
                            },

                            // Total number of achievements completed
                            totalAchievements = new { integerValue = m.totalAchievements.ToString() },

                            // Numeric representation of rank (Firestore requires integerValue as string)
                            rank = new { integerValue = ((int)m.rank).ToString() }
                        }
                    }
                }).ToArray()
            }
        };
    }

    /// <summary>
    /// Builds the Firestore-compliant "applicants" array payload.
    /// Each data is serialized as a mapValue -> fields structure,
    /// compatible with Firestore's document JSON schema.
    /// </summary>
    internal static object BuildFirestoreApplicantsArray(IEnumerable<ApplicantData> applicants)
    {
        return new
        {
            arrayValue = new
            {
                values = applicants.Select(a => new
                {
                    mapValue = new
                    {
                        fields = new
                        {
                            // Firebase user ID (required for cross-reference)
                            firebaseID = new { stringValue = a.firebaseID ?? string.Empty },

                            // Unity ID (optional but useful for in-game lookups)
                            unityID = new { stringValue = a.unityID ?? string.Empty },

                            // Visible name in the UI
                            applicantName = new { stringValue = a.applicantName },

                            // Profile icon ID (stored as string)
                            profileIconId = new { stringValue = a.profileIconId },

                            // Last registered Elo rating (stored as integer string)
                            eloRating = new { integerValue = a.eloRating.ToString() },

                            // Numeric representation of tier (Firestore requires integerValue as string)
                            bestLeaderboardTier = new { integerValue = ((int)a.bestLeaderboardTier).ToString() },

                            // The best leaderboard score achieved (stored as integer string)
                            bestLeaderboardScore = new { integerValue = a.bestLeaderboardScore.ToString() },
                        }
                    }
                }).ToArray()
            }
        };
    }

    /// <summary>
    /// Builds a Firestore-compliant object for a club chat document.
    /// </summary>
    internal static object BuildFirestoreClubChatArray(FirestoreClubChatData.MessageData[] messages)
    {
        return new
        {

            arrayValue = new
            {
                values = messages.Select(m => new
                {
                    mapValue = new
                    {
                        fields = new
                        {
                            // Unique message ID (e.g., "msg_1729873487321")
                            messageId = new { stringValue = m.messageId ?? string.Empty },

                            // Sender identifiers
                            senderId = new { stringValue = m.senderId ?? string.Empty },
                            senderName = new { stringValue = m.senderName ?? string.Empty },
                            profileIconId = new { stringValue = m.profileIconId ?? string.Empty },

                            // Message content
                            content = new { stringValue = m.content ?? string.Empty },

                            // Timestamp in Unix milliseconds
                            timestamp = new { timestampValue = DateTime.UtcNow.ToString("o") },
                        }
                    }
                }).ToArray()
            }
        };
    }
    #endregion

    #region Realtime Database
    /// <summary>
    /// Ensures that a user entry exists in Realtime Database.
    /// Creates or updates the node with normalized data for searching.
    /// </summary>
    internal static async Task EnsureUserExistsInRealtimeAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string unityUserId,
        string displayName,
        string profileIconID,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");

        // Create backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint($"users/{unityUserId}", backendIdToken);

        // Prepare normalized fields
        var normalizedName = NormalizeName(displayName);
        var searchTokens = GenerateSearchTokens(displayName);

        var userData = new
        {
            userId = unityUserId,
            displayName = displayName,
            profileIconID = profileIconID,
            normalizedName = normalizedName,
            searchTokens = searchTokens,
            createdAt = DateTime.UtcNow.ToBinary(),
            lastSeen = DateTime.UtcNow.ToBinary()
        };

        try
        {
            await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: userData,
                httpMethod: Method.Put,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );

            logger?.LogInformation($"User '{unityUserId}' written successfully in RTDB.");
        }
        catch (Exception ex)
        {
            logger?.LogError($"Failed to write RTDB user node: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Tries to get all purchase receipts for a player from Realtime Database.
    /// </summary>
    internal static async Task<PlayerPurchasesData> GetPlayerPurchaseDataAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string unityUserId,
        ILogger? logger = null)
    {
        // Defensive checks
        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");

        // Create backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        // Exchange for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        // Target specific purchase node
        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            $"{purchasesCollectionIdKey}/{unityUserId}",
            backendIdToken);

        try
        {
            var response = await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: null,
                httpMethod: Method.Get,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );

            // If node does not exist, RTDB returns "null"
            if (response is null ||
                !response.IsSuccessful ||
                string.IsNullOrWhiteSpace(response.Content) ||
                response.Content == "null")
            {
                logger?.LogInformation(
                    "No purchases found for user '{UserId}'",
                    unityUserId);
                return new();
            }

            // Try to deserialize the response content (if it fails, the external try-catch will handle it)
            var dict = JsonConvert.DeserializeObject<Dictionary<string, PlayerPurchaseReceiptData>>(response.Content); 
            
            // Calls a log to know if the data was deserialized correctly
            logger?.LogInformation($"User '{unityUserId}' has {dict?.Count ?? 0} purchases in RTDB."); 
            
            // Build the PlayerPurchasesData object (if it does not exist, return empty list to initialize it)
             var playerPurchasesData = new PlayerPurchasesData { receipts = dict?.Values.ToList() ?? new List<PlayerPurchasesData.PlayerPurchaseReceiptData>() }; 
            
            // Calls a log to know the model was created or got correctly
            logger?.LogInformation($"User '{unityUserId}' has {playerPurchasesData.receipts.Count} purchases in RTDB."); 
            
            // Find the specific purchase by externalOrderId
            return playerPurchasesData;
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to get RTDB purchases for user '{UserId}'",
                unityUserId);
            throw;
        }
    }

    /// <summary>
    /// Updates a specific purchase receipt in Realtime Database.
    /// </summary>
    internal static async Task PutPurchaseReceiptAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string unityUserId,
        string externalOrderId,
        object patchData,
        ILogger? logger = null)
    {
        // Defensive checks
        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");

        if (string.IsNullOrWhiteSpace(externalOrderId))
            throw new ArgumentException("Invalid external order ID");

        if (patchData is null)
            throw new ArgumentException("Patch data cannot be null");

        // Create Firebase backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        // Exchange for ID token
        var backendIdToken =
            await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
                customToken,
                FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        // Target specific purchase node
        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            $"{purchasesCollectionIdKey}/{unityUserId}/{externalOrderId}",
            backendIdToken);

        // Log intent
        logger?.LogInformation(
            "Creating purchase '{ExternalOrderId}' for user '{UserId}'",
            externalOrderId,
            unityUserId);

        try
        {
            await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: patchData,
                httpMethod: Method.Put,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );

            logger?.LogInformation(
                "Purchase '{ExternalOrderId}' patched successfully",
                externalOrderId);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to patch purchase '{ExternalOrderId}' for user '{UserId}'",
                externalOrderId,
                unityUserId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves top player leaderboard data for multiple leaderboards in parallel.
    /// </summary>
    /// <param name="executionContext">The execution context for the operation.</param>
    /// <param name="gameApiClient">The game API client used to perform requests.</param>
    /// <param name="leaderboardIds">An array of leaderboard IDs to query.</param>
    /// <param name="nationalityType">The nationality filter for leaderboard data.</param>
    /// <param name="limit">The maximum number of players to retrieve per leaderboard.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <returns>A dictionary mapping leaderboard IDs to lists of player leaderboard data.</returns>
    internal static async Task<Dictionary<string, List<RTDBPlayerLeaderboardData>>> GetMultipleLeaderboardsAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        string[] leaderboardIds,
        NationalityType nationalityType,
        int limit,
        ILogger? logger = null)
    {
        // Get token once
        var backendIdToken = await GetBackendIdTokenAsync(
            executionContext, gameApiClient);

        // Run all requests in parallel
        var tasks = leaderboardIds.Select(async leaderboardId =>
        {
            var result = await GetTopLeaderboardWithTokenAsync(
                backendIdToken,
                leaderboardId,
                nationalityType,
                limit,
                logger);

            return (leaderboardId, result);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(x => x.leaderboardId, x => x.result);
    }

    /// <summary>
    /// Gets the Top N leaderboard entries for a given mode, variant and country.
    /// </summary>
    internal static async Task<List<RTDBPlayerLeaderboardData>> GetTopLeaderboardWithTokenAsync(
        string backendIdToken,
        string leaderboardId,
        NationalityType nationalityType,
        int limit,
        ILogger? logger = null)
    {
        if (limit <= 0)
            throw new ArgumentException("Limit must be greater than zero");

        if (string.IsNullOrWhiteSpace(leaderboardId))
            throw new ArgumentException("Invalid leaderboard id");

        // Get string representation
        var nationality = nationalityType.ToString();

        // RTDB query parameters:
        // - orderBy="score"
        // - limitToLast=N  (because higher score = better)
        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            $"{leaderboardsCollectionIdKey}/{leaderboardId}/{nationality}" +
            $"?orderBy=\"score\"&limitToLast={limit}",
            backendIdToken);

        try
        {
            var response = await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: null,
                httpMethod: Method.Get,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );

            if (response is null ||
                !response.IsSuccessful ||
                string.IsNullOrWhiteSpace(response.Content) ||
                response.Content == "null")
            {
                logger?.LogInformation(
                    "Leaderboard empty: LeaderboardId={LeaderboardId}, Country={Country}",
                    leaderboardId, nationality);

                return [];
            }

            var rtdbPlayerLeaderboardDataCollection = default(Dictionary<string, RTDBPlayerLeaderboardData>);
            try
            {
                // Deserialize dictionary keyed by UID
                rtdbPlayerLeaderboardDataCollection = JsonConvert.DeserializeObject<Dictionary<string, RTDBPlayerLeaderboardData>>(response.Content) ?? [];
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Failed to deserialize leaderboard data for {LeaderboardId}/{Nationality}. Response: {Response}",
                    leaderboardId, nationality, response.Content);
            }

            // RTDB returns ascending order -> reverse for ranking
            var result = rtdbPlayerLeaderboardDataCollection
                ?.Select(kvp =>
                {
                    kvp.Value.userId = kvp.Key;
                    return kvp.Value;
                })
                .OrderByDescending(e => e.score)
                .ToList();

            logger?.LogInformation(
                "Fetched {Count} leaderboard entries for {LeaderboardId}/{Country}",
                result?.Count ?? 0, leaderboardId, nationality);

            return result ?? [];
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to fetch leaderboard for {LeaderboardId}/{Country}",
                leaderboardId, nationality);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the IDs of all leaderboards from the Firebase Realtime Database.
    /// </summary>
    /// <typeparam name="T">The type parameter for the leaderboard entity.</typeparam>
    /// <param name="executionContext">The execution context for the current operation.</param>
    /// <param name="gameApiClient">The game API client used for authentication and requests.</param>
    /// <param name="logger">Optional logger for logging information and errors.</param>
    /// <returns>An array of leaderboard IDs, or null if no data is found.</returns>
    /// <exception cref="Exception">Thrown if custom token generation or backend ID token creation fails, or if an error occurs during the request.</exception>
    internal static async Task<string[]> GetLeaderboardIdsAsync(
         IExecutionContext executionContext,
         IGameApiClient gameApiClient,
         ILogger? logger = null)
    {
        // Create backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        // Build endpoint with shallow query param
        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            $"{leaderboardsCollectionIdKey}?shallow=true",
            backendIdToken);

        try
        {
            var response = await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: null,
                httpMethod: Method.Get,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );

            if (!response.IsSuccessful ||
                string.IsNullOrWhiteSpace(response.Content) ||
                response.Content == "null")
            {
                logger?.LogInformation("No leaderboards found in RTDB.");
                return [];
            }

            // Deserialize into dictionary because shallow returns key:true pairs
            var dict = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response.Content);

            return dict?.Keys.ToArray() ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to retrieve leaderboard IDs.");
            throw;
        }
    }

    /// <summary>
    /// Retrieves leaderboard data entries for a specific player across multiple leaderboards and nationality from the
    /// Firebase Realtime Database.
    /// </summary>
    /// <param name="executionContext">The execution context for the current operation.</param>
    /// <param name="gameApiClient">The game API client used for backend communication.</param>
    /// <param name="nationality">The nationality to filter leaderboard entries.</param>
    /// <param name="unityUserId">The Unity user ID of the player whose leaderboard data is requested.</param>
    /// <param name="leaderboardsIds">An array of leaderboard IDs to query.</param>
    /// <param name="logger">Optional logger for logging information and errors.</param>
    /// <returns>An array of RTDBPlayerLeaderboardData objects for the player, or an empty array if no entries are found.</returns>
    /// <exception cref="ArgumentException">Thrown if the Unity user ID or leaderboard IDs are invalid.</exception>
    /// <exception cref="Exception">Thrown if token generation or backend communication fails.</exception>
    internal static async Task<Dictionary<string, RTDBPlayerLeaderboardData>> GetPlayerLeaderboardDataAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        NationalityType nationality,
        string? unityUserId,
        string[]? leaderboardsIds,
        ILogger? logger = null)
    {
        // Defensive checks
        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");

        if (leaderboardsIds is null or { Length: 0 })
            throw new ArgumentException("Invalid leaderboards ids");

        // Create Firebase backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        // For each leaderboard ID, build a task to get the player's entry
        var getLeaderboardTasks = leaderboardsIds.Select(id => GetLeaderboardEntryAsync(id)).ToList();

        // Await all tasks in parallel
        var results = await Task.WhenAll(getLeaderboardTasks);

        // Filter out null results (in case some leaderboards did not have an entry for the user)
        return results
            ?.Where(r => r.HasValue)
            ?.Select(r => r!.Value)
            ?.ToDictionary(x => x.leaderboardId, x => x.data) 
            ?? [];

        /// Get a single leaderboard entry for the user. Returns null if not found or on error.
        async Task<(string leaderboardId, RTDBPlayerLeaderboardData data)?> GetLeaderboardEntryAsync(string leaderboardId)
        {
            // Target leaderboard node
            var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
                 $"{leaderboardsCollectionIdKey}/{leaderboardId}/{nationality}/{unityUserId}",
                backendIdToken);

            try
            {
                var response = await ExecuteRequestAsync(
                    endpoint: endpoint,
                    payload: null,
                    httpMethod: Method.Get,
                    customHeaders: null,
                    firebaseService: FirebaseService.RealtimeDatabase
                );

                if (response is null ||
                    !response.IsSuccessful ||
                    string.IsNullOrWhiteSpace(response.Content) ||
                    response.Content == "null")
                {
                    logger?.LogInformation(
                        "Leaderboard entry not found: LeaderboardId={LeaderboardId}, Country={Country}, PlayerId={PlayerId}",
                        leaderboardId, nationality, unityUserId);

                    return default;
                }

                var rtdbPlayerLeaderboardData = default(RTDBPlayerLeaderboardData);
                try
                {
                    // Deserialize dictionary keyed by UID
                    rtdbPlayerLeaderboardData = JsonConvert.DeserializeObject<RTDBPlayerLeaderboardData>(response.Content);
                }
                catch (Exception ex)
                {
                    logger?.LogError(
                        ex,
                        "Failed to deserialize leaderboard data for {LeaderboardId}/{Nationality}/{PlayerId}. Response: {Response}",
                        leaderboardId, nationality, unityUserId, response.Content);
                }

                return rtdbPlayerLeaderboardData is not null 
                    ? (leaderboardId, rtdbPlayerLeaderboardData) 
                    : default;
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Failed to get leaderboard data for user '{UserId}' in leaderboard '{LeaderboardId}' with nationality '{Nationality}'",
                    unityUserId, leaderboardId, nationality);
                throw;
            }
        }
    }


    /// <summary>
    /// Creates or updates a player's score in a specific leaderboard (mode + variant + country).
    /// </summary>
    internal static async Task UpsertLeaderboardScoreAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        NationalityType nationality,
        string? leaderboardId,
        string? unityUserId,
        string? unityUsername,
        double score,
        ILogger? logger = null)
    {
        // Defensive checks
        if (string.IsNullOrWhiteSpace(leaderboardId))
            throw new ArgumentException("Invalid leaderboard id");

        if (string.IsNullOrWhiteSpace(unityUserId))
            throw new ArgumentException("Invalid Unity user ID");
       
        if (string.IsNullOrWhiteSpace(unityUsername))
            logger?.LogWarning("Unity username is empty for user '{unityUsername}'", unityUsername);

        // Create Firebase backend token
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        // Target leaderboard node
        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            $"{leaderboardsCollectionIdKey}/{leaderboardId}/{nationality}/{unityUserId}",
            backendIdToken);

        var payload = new
        {
            userId = unityUserId,
            score = score,
            username = unityUsername,
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        logger?.LogInformation(
            "Upserting leaderboard score: LeaderboardId={LeaderboardId}, Nationality={Nationality}, User={UserId}, Score={Score}",
            leaderboardId, nationality, unityUserId, score);

        try
        {
            await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: payload,
                httpMethod: Method.Put,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to upsert leaderboard score for user '{UserId}'",
                unityUserId);
            throw;
        }
    }

    internal static async Task PatchMultiLocationAsync(
        IExecutionContext executionContext,
        IGameApiClient gameApiClient,
        Dictionary<string, object?> updates,
        ILogger? logger = null)
    {
        if (updates == null || updates.Count == 0)
            return;

        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(
            gameApiClient, executionContext)
            ?? throw new Exception("Custom token generation failed");

        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey)
            ?? throw new Exception("Backend ID token creation failed");

        var endpoint = FirebaseTokenFactory.BuildRealtimeEndpoint(
            "/", // root
            backendIdToken);

        logger?.LogInformation("Final endpoint: {Endpoint}", endpoint);

        try
        {
            await ExecuteRequestAsync(
                endpoint: endpoint,
                payload: updates,
                httpMethod: Method.Patch,
                customHeaders: null,
                firebaseService: FirebaseService.RealtimeDatabase
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Multi-location PATCH failed.");
            throw;
        }
    }

    /// <summary>
    /// Normalizes a name for searching.
    /// - Converts to lowercase
    /// - Removes accents and diacritics
    /// - Keeps letters, digits and spaces only
    /// - Collapses multiple spaces
    /// </summary>
    internal static string NormalizeName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Normalize input: use only first name entry (ugs adds extra id)
        input = input?.Split('#')?.FirstOrDefault() ?? string.Empty;

        // Convert to lowercase using invariant culture
        string lower = input.ToLowerInvariant();

        // Decompose accented characters (e.g. á -> a + accent)
        string normalized = lower.Normalize(NormalizationForm.FormD);

        // Remove diacritic marks
        var noDiacritics = new string(
            normalized.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
            ).ToArray()
        );

        // Keep letters, numbers and spaces (unicode-aware)
        noDiacritics = Regex.Replace(noDiacritics, @"[^\p{L}\p{N}\s]", "");

        // Normalize spaces
        noDiacritics = Regex.Replace(noDiacritics.Trim(), @"\s+", " ");

        return noDiacritics;
    }

    /// <summary>
    /// Generates prefix tokens for incremental search.
    /// Example: "john 77" → ["j","jo","joh","john","77","7","77"]
    /// </summary>
    internal static List<string> GenerateSearchTokens(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new();

        string normalized = NormalizeName(name);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokens = new HashSet<string>();

        foreach (var word in words)
        {
            for (int i = 1; i <= word.Length; i++)
                tokens.Add(word.Substring(0, i));

            tokens.Add(word);
        }

        return tokens.ToList();
    }

    #endregion

    #region Functions
    /// <summary>
    /// Creates an external purchase order by invoking Firebase Functions.
    /// This method ONLY talks to the payment provider and does NOT apply business logic.
    /// </summary>
    internal static async Task<ProviderOrderResponse?> CreatePurchaseOrder(
        string productId,
        string price,
        string currency,
        string environment,
        string customId,
        IGameApiClient gameApiClient,
        IExecutionContext executionContext,
        ILogger? logger = null)
    {
        // Defensive validation (technical only)
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("productId is required.", nameof(productId));

        if (string.IsNullOrWhiteSpace(price))
            throw new ArgumentException("price is required.", nameof(price));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("currency is required.", nameof(currency));

        if (string.IsNullOrWhiteSpace(environment))
            throw new ArgumentException("environment is required.", nameof(environment));

        if (string.IsNullOrWhiteSpace(customId))
            throw new ArgumentException("customId is required.", nameof(customId));

        // Build payload for Firebase Functions
        var firebasePayload = new
        {
            productId = productId,
            price = price,
            currency = currency,
            environment = environment,
            customId = customId
        };

        RestResponse response;

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        try
        {
            // Call Firebase Function
            response = await ExecuteRequestAsync(
                endpoint: "CreatePurchaseOrder",
                payload: firebasePayload,
                httpMethod: Method.Post,
                customHeaders: headers,
                firebaseService: FirebaseService.FirebaseFunctions
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Error calling Firebase CreatePurchaseOrder. ProductId={ProductId}",
                productId);

            throw; // technical failure, let Cloud Code handle state
        }

        // Validate HTTP response
        if (!response.IsSuccessful)
        {
            logger?.LogError(
                "Firebase CreatePurchaseOrder failed. Status={StatusCode}, Content={Content}",
                response.StatusCode,
                response.Content);

            return null;
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            logger?.LogError(
                "Firebase CreatePurchaseOrder returned empty response. ProductId={ProductId}",
                productId);

            return null;
        }

        // Deserialize provider response
        ProviderOrderResponse? providerResponse;

        try
        {
            providerResponse =
                JsonConvert.DeserializeObject<ProviderOrderResponse>(response.Content);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to deserialize Firebase CreatePurchaseOrder response. Content={Content}",
                response.Content);

            throw;
        }

        // Validate provider response
        if (providerResponse is null
            || string.IsNullOrWhiteSpace(providerResponse.approvalUrl)
            || string.IsNullOrWhiteSpace(providerResponse.externalOrderId))
        {
            logger?.LogError(
                "Invalid provider response from Firebase. Content={Content}",
                response.Content);

            return null;
        }

        // Return valid response
        return providerResponse;
    }

    /// <summary>
    /// Deletes a Firebase account by invoking a Firebase Cloud Function.
    /// This method ONLY performs the technical call and applies NO business rules.
    /// </summary>
    internal static async Task<DeleteUserResponse?> DeleteFirebaseAccount(
        string firebaseUid,
        string nationality,
        IGameApiClient gameApiClient,
        IExecutionContext executionContext,
        ILogger? logger = null)
    {
        // Defensive validation (technical only)
        if (string.IsNullOrWhiteSpace(firebaseUid))
            throw new ArgumentException("firebaseUid is required.", nameof(firebaseUid));

        // Build payload for Firebase Functions
        var firebasePayload = new
        {
            firebaseUid = firebaseUid,
            unityPlayerId = executionContext.PlayerId,
            nationality = nationality
        };

        RestResponse response;

        // Create custom token for backend using JWT
        var customToken = await FirebaseTokenFactory.CreateCustomTokenAsync(gameApiClient, executionContext);
        if (string.IsNullOrEmpty(customToken))
            throw new ArgumentException("Invalid Firebase customToken");

        // Exchange custom token for backend ID token
        var backendIdToken = await FirebaseTokenFactory.ExchangeCustomTokenForIdTokenAsync(
            customToken,
            FirebaseBackend._firebaseConfigData.apiKey);
        if (string.IsNullOrEmpty(backendIdToken))
            throw new ArgumentException("Invalid Firebase backendIdToken");

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {backendIdToken}" }
        };

        try
        {
            // Call Firebase Function (single orchestrator)
            response = await ExecuteRequestAsync(
                endpoint: "deleteFirebaseAccount",
                payload: firebasePayload,
                httpMethod: Method.Post,
                customHeaders: headers,
                firebaseService: FirebaseService.FirebaseFunctions
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Error calling Firebase deleteFirebaseAccount. Uid={Uid}",
                firebaseUid);

            throw; // infrastructure failure
        }

        // Validate HTTP transport
        if (!response.IsSuccessful)
        {
            logger?.LogError(
                "Firebase deleteFirebaseAccount failed. Status={StatusCode}, Content={Content}",
                response.StatusCode,
                response.Content);

            return null; // Cloud Code infra error, not retryable here
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            logger?.LogError(
                "Firebase deleteFirebaseAccount returned empty response. Uid={Uid}",
                firebaseUid);

            return null;
        }

        // Deserialize response
        DeleteUserResponse? deleteResponse;

        try
        {
            deleteResponse =
                JsonConvert.DeserializeObject<DeleteUserResponse>(response.Content);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Failed to deserialize deleteFirebaseAccount response. Content={Content}",
                response.Content);

            throw;
        }

        // Backend already decided what to do
        if (deleteResponse is null)
        {
            logger?.LogError(
                "deleteFirebaseAccount returned null response. Content={Content}",
                response.Content);

            return null;
        }

        if (!deleteResponse.success)
        {
            logger?.LogWarning(
                "Delete account failed. Retryable={Retryable}, Step={Step}, Error={Error}",
                deleteResponse.retryable,
                deleteResponse.step,
                deleteResponse.error);
        }

        return deleteResponse;
    }


    /// <summary>
    /// Response structure for provider order
    /// </summary>
    internal class ProviderOrderResponse
    {
        public string approvalUrl;
        public string externalOrderId;

        public ProviderOrderResponse()
        {
            approvalUrl = string.Empty;
            externalOrderId = string.Empty;
        }
    }

    internal sealed class DeleteUserResponse
    {
        public bool success;
        public bool retryable;
        public string? step;
        public string? error;
        public string? uid;
    }


    #endregion
}
