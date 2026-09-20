using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using static Backend.UGSBackend;
using static Google.Apis.Requests.BatchRequest;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend;

internal class UGSApiHelper
{
    private static readonly Regex ValidNameRegex = new(@"^[^\s]+$");
    internal static readonly string[] NumberOfPlayers = ["1v1", "1v3"];

    private static string[] _gameModesNames = null!;
    internal static string[] GameModesNames = _gameModesNames ??= Enum.GetNames(typeof(GameModeFilter))
        .Where(x => x != GameModeFilter.None.ToString())
        .ToArray();


    #region Admin
    /// <summary>
    /// Saves the specified data to Unity Cloud Save for a player or custom target, with optional save type and player
    /// identification.
    /// </summary>
    /// <param name="gameApiClient">The game API client used to perform the save operation.</param>
    /// <param name="executionContextData">The execution context containing project, environment, and player information.</param>
    /// <param name="data">A dictionary of key-value pairs representing the data to be saved.</param>
    /// <param name="saveType">Optional. Specifies the accessibility of the saved data (e.g., protected, private).</param>
    /// <param name="customID">Optional. A custom identifier to target a game data record instead of the default player.</param>
    /// <param name="alternativePlayerID">Optional. An alternative player ID to use if not saving for the current player.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the execution context is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the data dictionary is null or empty.</exception>
    /// <exception cref="UGSException">Thrown if the save operation fails or an error occurs during the process.</exception>
    internal static async Task SaveData(IGameApiClient gameApiClient, IExecutionContext executionContextData,
        Dictionary<string, object> data, string? saveType = null, string? customID = null, string? alternativePlayerID = null)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Perform additional context validation
        BackendHelper.ContextValidation(executionContextData);

        // Validate that data is provided and not empty
        if (data is null or { Count: 0 })
            throw new ArgumentException("Invalid input data. Provide data");
        
        // Determine the target path: either using a custom ID or the player's ID
        var target = !string.IsNullOrEmpty(customID) ? $"custom/{customID}" : $"players/{alternativePlayerID ?? executionContextData.PlayerId}";

        // Determine the accessibility path (e.g., protected or private)
        var accessibility = string.IsNullOrEmpty(saveType) ? "" : $"/{saveType}/";

        // If custom ID is used and the saveType is 'protected', omit the accessibility segment (default)
        if (!string.IsNullOrEmpty(customID) && saveType is "protected")
            accessibility = "";

        // Prepare the data for Cloud Save: normalize each value into a Cloud Save-compatible format
        var serializedData = data.Select(x => new
        {
            key = x.Key,
            value = NormalizeToCloudSaveFormat(x.Value) // Ensure proper serialization of complex types
        }).ToList();

        // Get the secret authorization header
        var authHeader = default(string);
        try
        {
            authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContextData);
        }
        catch (Exception ex)
        {
            throw new UGSException($"Error getting auth header for Cloud Save. \n\nResponse:\n{ex.Message}", ex);
        }

        // Initialize REST client for the Unity Cloud Save endpoint
        using var restClient = new RestClient($"https://services.api.unity.com/cloud-save/v1/data/projects/{executionContextData.ProjectId}/environments/{executionContextData.EnvironmentId}/{target}{accessibility}item-batch");

        // Prepare the request with necessary headers and body content
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"basic {authHeader}");

        // Add the serialized data to the request body
        var json = JsonConvert.SerializeObject(new { data = serializedData });
        request.AddBody(json);

        var response = default(RestResponse);
        try
        { 
            // Execute the request asynchronously
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // Check if the request was successful
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to save player data in Cloud Save. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);
        }
        catch (Exception ex)
        {
            // Handle specific UGS exceptions and provide additional details
            throw new UGSException($"Error saving data in Cloud Save. \n\nResponse:\n{ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves data with protected access using the specified game API client and execution context.
    /// </summary>
    /// <param name="gameApiClient">The game API client used to perform the save operation.</param>
    /// <param name="executionContext">The execution context for the save operation.</param>
    /// <param name="data">The data to be saved.</param>
    /// <param name="customID">An optional custom identifier for the save operation.</param>
    /// <param name="alternativePlayerID">An optional alternative player identifier.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    internal static async Task ProtectedSaveData(IGameApiClient gameApiClient, IExecutionContext executionContext, Dictionary<string, object> data, string? customID = null, string? alternativePlayerID = null)
        => await SaveData(gameApiClient, executionContext, data, saveType: "protected", customID: customID, alternativePlayerID: alternativePlayerID).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously loads data items from Unity Cloud Save for the specified player or custom ID using provided keys.
    /// </summary>
    /// <param name="gameApiClient">The game API client used to communicate with the Cloud Save service.</param>
    /// <param name="executionContextData">The execution context containing project, environment, and player information.</param>
    /// <param name="loadType">The data accessibility type (e.g., protected or private).</param>
    /// <param name="customID">The custom identifier for loading data associated with a custom entity.</param>
    /// <param name="alternativePlayerID">An alternative player ID to use instead of the one in the execution context.</param>
    /// <param name="isThrowingException">Indicates whether to throw exceptions on failed requests.</param>
    /// <param name="keysID">The keys of the data items to load.</param>
    /// <returns>An array of ResponseData objects containing the loaded data, or null if the request was unsuccessful and
    /// exceptions are not thrown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no keys are provided.</exception>
    /// <exception cref="UGSException">Thrown when there is an error communicating with or processing the response from the Cloud Save API.</exception>
    internal static async Task<ResponseData?[]?> LoadData(IGameApiClient gameApiClient, IExecutionContext executionContextData, 
        string? loadType = null, string? customID = null, string? alternativePlayerID = null, bool isThrowingException = true, params string[] keysID)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContextData);

        if (keysID is null or { Length: 0 })
            throw new ArgumentException("Invalid input data. Provide data");

        // Determine if the target is a custom ID or player ID (this determine if you will save the data inthe GameData or PlayerData)
        var target = !string.IsNullOrEmpty(customID) ? $"custom/{customID}" : $"players/{alternativePlayerID ?? executionContextData.PlayerId}";

        // If you are using the playerID, the accessibility is the loadType (protected or private)
        var accessibility = string.IsNullOrEmpty(loadType) ? "" : $"/{loadType}/";

        // If the customID has a value and the loadType is protected, the accessibility is empty (this is because protected is the default value)
        if (!string.IsNullOrEmpty(customID) && loadType is "protected")
            accessibility = "";

        // Get the secret authorization header
        var authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContextData);

        // Initialize REST client for the Unity Cloud Save endpoint
        using var restClient = new RestClient($"https://services.api.unity.com/cloud-save/v1/data/projects/{executionContextData.ProjectId}/environments/{executionContextData.EnvironmentId}/{target}{accessibility}/items");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Get,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"basic {authHeader}");

        // Add the keys to the request
        if (keysID is not null and { Length: > 0 })
            foreach (var key in keysID)
                if (!string.IsNullOrEmpty(key))
                    request.AddQueryParameter("keys", key);

        var response = default(RestResponse);
        try
        {
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (response is null or { Content: null or "" })
                throw new UGSException("Empty response received from Cloud Save API.");

            if (!response.IsSuccessful && isThrowingException)
                throw new UGSException($"Failed to load player data in Cloud Save. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            if (!response.IsSuccessful && !isThrowingException)
                return default;

        }
        catch (Exception ex)
        {
            throw new UGSException($"\nError loading data from Cloud Save. \n\nResponse:\n{ex.Message}", ex);
        }

        // Parse the response content to a JObject
        try
        {
            var resultContent = JsonConvert.DeserializeObject<CloudSaveResponse>(response.Content);
            if (resultContent is null or { results: null })
                throw new UGSException("Invalid response format from Cloud Save API.");

            return resultContent.results;
        }
        catch (Exception ex)
        {
            throw new UGSException($"Unable to deserialize data: \n\n{response.Content} \n\n{ex.Message}");
        }
    }
    
    /// <summary>
    /// Asynchronously loads protected data for the specified keys using the provided API client and execution context.
    /// </summary>
    /// <param name="api">The game API client used to perform the data load.</param>
    /// <param name="ctx">The execution context for the operation.</param>
    /// <param name="customID">An optional custom identifier for the data load.</param>
    /// <param name="alternativePlayerID">An optional alternative player identifier.</param>
    /// <param name="isThrowingException">Indicates whether to throw an exception on failure.</param>
    /// <param name="keysID">The keys identifying the data to load.</param>
    /// <returns>A task representing the asynchronous operation, containing an array of ResponseData objects or null.</returns>
    internal static async Task<ResponseData?[]?> ProtectedLoadData(IGameApiClient api, IExecutionContext ctx, string? customID = null, string? alternativePlayerID = null, bool isThrowingException = true, params string[] keysID) 
        => await LoadData(api, ctx, loadType: "protected", customID: customID, alternativePlayerID: alternativePlayerID, isThrowingException, keysID).ConfigureAwait(false);

    /// <summary>
    /// Retrieves remote configuration data from the Unity Remote Config service for the specified project and
    /// configuration key.
    /// </summary>
    /// <param name="gameApiClient">The game API client used to authenticate and send requests.</param>
    /// <param name="executionContext">The execution context containing project and environment information.</param>
    /// <param name="remoteConfigKey">The key identifying the remote configuration to retrieve.</param>
    /// <returns>A JSON string containing the remote configuration data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="UGSException">Thrown when the remote config data cannot be retrieved or the response is invalid.</exception>
    internal static async Task<string> GetRemoteConfig(IGameApiClient gameApiClient, IExecutionContext executionContext, string remoteConfigKey)
    {
        // Validate that the execution context is not null
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Get the secret authorization header
        var authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContext);

        // Initialize REST client for the Unity Remote Config endpoint
        using var restClient = new RestClient($"https://services.api.unity.com/remote-config/v1/projects/{executionContext.ProjectId}/configs/{remoteConfigKey}");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Get,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"basic {authHeader}");

        try
        {
            // Execute the request asynchronously and handle the response
            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to get remot config data. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            // Check if the response content is null or empty and throw an exception if it is
            return string.IsNullOrEmpty(response.Content)
                ? throw new UGSException("Empty response received from Remote Config API.")
                : response.Content;
        }
        catch (Exception ex)
        {
            throw new UGSException($"Error getting remote config data. \n\nResponse:\n{ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves user data for the specified username from the Unity Player Identity service.
    /// </summary>
    /// <param name="gameApiClient">The game API client used for authentication and requests.</param>
    /// <param name="executionContext">The execution context containing project and environment information.</param>
    /// <param name="username">The username of the user to retrieve.</param>
    /// <returns>A task representing the asynchronous operation, containing the user data if found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the execution context is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the username is null or empty.</exception>
    /// <exception cref="UGSException">Thrown if the Unity Player Identity service returns an error or invalid response.</exception>
    internal static async Task<UserData> GetUserByUsernameAsync(IGameApiClient gameApiClient, IExecutionContext executionContext, 
        string username)
    {
        // Validate that the execution context is not null
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Check if the username is null or empty
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("Username cannot be empty.");

        // Get the secret authorization header
        var authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContext);

        // Check if in UGS already exists an user with the same username
        using var listUsersRestClient = new RestClient($"https://services.api.unity.com/player-identity/v1/projects/{executionContext.ProjectId}/users");
        
        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Get,
            RequestFormat = DataFormat.Json,
        };

        // Add Sign-Up Username and Password data to the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"basic {authHeader}");

        // Specify the username to check
        request.AddQueryParameter("username", username);

        var response = await listUsersRestClient.ExecuteAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessful)
            throw new UGSException($"Get user failed. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

        if (string.IsNullOrEmpty(response.Content))
            throw new UGSException($"Empty response received from Player Auth API.", response.ErrorException);

        var UsersData = default(ListUsersResponse);
        try
        {
            UsersData = JsonConvert.DeserializeObject<ListUsersResponse>(response.Content);

            if (UsersData is null or { results: null or { Length: 0 } })
                throw new UGSException($"Invalid response format from Player Auth API.");
        }
        catch (Exception ex)
        {
            throw new UGSException($"Unable to deserialize data: \n\n{response.Content} \n\n{ex.Message}", ex, response.Content);
        }

        // Check if any user was found with the specified username
        var user = UsersData.results.FirstOrDefault();
        if (user is null)
            throw new UGSException($"User with username {username} not found.");

        // Returns the user with the specified username
        return user;
    }

    /// <summary>
    /// Changes the player's password using the Unity Game Services Player Identity API.
    /// </summary>
    /// <param name="gameApiClient">The game API client used to communicate with the backend service.</param>
    /// <param name="executionContext">The execution context containing project and player information.</param>
    /// <param name="newPassword">The new password to set for the player.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the execution context is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the new password is null, empty, or does not meet strength requirements.</exception>
    /// <exception cref="UGSException">Thrown if the password change request fails or an error occurs during the operation.</exception>
    internal static async Task ChangePassword(IGameApiClient gameApiClient, IExecutionContext executionContext, string? newPassword)
    {
        // Validate that the execution context is not null
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Check if the password is null or empty
        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("New password cannot be empty.");

        // Check if the password is using the correct structure
        if (!CredentialsValidator.IsValidPassword(newPassword))
            throw new ArgumentException($"The password is not strong enough");

        // Get the secret authorization header
        var authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContext);

        // Initialize REST client for the Unity Player Identity endpoint to change password
        using var restClient = new RestClient($"https://services.api.unity.com/player-identity/v1/projects/{executionContext.ProjectId}/users/{executionContext.PlayerId}/change-password");

        // Prepare the request with necessary headers and body content
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"basic {authHeader}");
        request.AddJsonBody(new { newPassword });

        try
        {
            // Execute the request asynchronously and handle the response
            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // Check if the request was successful
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to change password to {executionContext.PlayerId}. Status code: {response.StatusCode}\n\nContent:\n{response.Content}\n", response.ErrorException, response.Content);
        }
        catch (Exception ex)
        {
            throw new UGSException($"Error changing password. \n\nResponse:\n{ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes the current player's score from the specified leaderboard and returns the updated leaderboard data.
    /// </summary>
    /// <param name="executionContext">The execution context containing authentication and project information.</param>
    /// <param name="gameApiClient">The game API client used to perform the leaderboard score deletion.</param>
    /// <param name="leaderboardId">The unique identifier of the leaderboard from which to delete the score.</param>
    /// <param name="_logger">Optional logger for diagnostic and error messages.</param>
    /// <returns>A list of updated leaderboard data after the score has been deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="UGSException">Thrown when the leaderboard score deletion fails due to an API or server error.</exception>
    internal static async Task<bool> DeleteLeaderboardScore(IExecutionContext executionContext, IGameApiClient gameApiClient, string leaderboardId,
        ILogger? _logger = null)
    {
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Initialize REST client for the Unity Player Identity endpoint to get or set the username
        using var restClient = new RestClient($"https://services.api.unity.com/leaderboards/v1/projects/{executionContext.ProjectId}/environments/{executionContext.EnvironmentId}/leaderboards/{leaderboardId}/scores/players/{executionContext.PlayerId}");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Delete,
        };

        // Get the secret authorization header
        var authHeader = await UGSConfigData.GetAuthHeader(gameApiClient, executionContext);

        request.AddHeader("Authorization", $"basic {authHeader}");

        try
        {
            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // If the score was not found (404), it means there was no score to delete, which is effectively a successful outcome for this operation, so we can return true.
            if (response.StatusCode is >= HttpStatusCode.BadRequest and <= HttpStatusCode.NotFound)
            {
                _logger?.LogDebug(
                    "No score found for player {PlayerId} in leaderboard {LeaderboardId}",
                    executionContext.PlayerId, leaderboardId);

                return true;
            }

            // If the response is not successful and it's not a 404, log the error and throw an exception
            if (!response.IsSuccessful)
            {
                _logger?.LogError(
                    "Failed to delete score. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode,
                    response.Content);

                throw new UGSException(
                    $"Failed to delete leaderboard score. Status code: {response.StatusCode}\nResponse:\n{response.Content}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to delete score for player {PlayerId} in leaderboard {LeaderboardId}",
                executionContext.PlayerId, leaderboardId);

            throw new UGSException($"Failed to delete leaderboard score", ex);
        }
    }
    #endregion

    #region Server
    /// <summary>
    /// Registers a new user with the specified username and password using Unity Player Identity authentication.
    /// </summary>
    /// <param name="executionContextData">Execution context containing project and access token information.</param>
    /// <param name="username">Username for the new user account.</param>
    /// <param name="password">Password for the new user account.</param>
    /// <returns>Authentication response data containing tokens and user information if sign-up is successful.</returns>
    /// <exception cref="ArgumentNullException">Thrown if executionContextData is null.</exception>
    /// <exception cref="ArgumentException">Thrown if username or password is empty.</exception>
    /// <exception cref="UGSException">Thrown for errors during sign-up, such as invalid token, username conflict, empty response, or deserialization
    /// failure.</exception>
    internal static async Task<UGSAuthResponseData?> SignUpByCredentials(IExecutionContext executionContextData, 
        string username, string password)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContextData);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Username and password cannot be empty.");

        // Initialize REST client for the Unity Player Identity endpoint to sign up with credentials
        using var restClient = new RestClient("https://player-auth.services.api.unity.com/v1/authentication/usernamepassword/sign-up");

        // Prepare the request with necessary headers and body content
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json,
        };

        // Add Sign-Up Username and Password data to the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("ProjectId", executionContextData.ProjectId);
        request.AddHeader("UnityEnvironment", executionContextData.EnvironmentName);

        // This allows the sign-up to be associated with an existing player context, which can be useful for linking accounts
        request.AddHeader("Authorization", $"Bearer {executionContextData.AccessToken}");

        // Add the username and password to the request body as JSON
        request.AddJsonBody(new { username, password });

        var response = default(RestResponse);
        try
        {
            // Execute the request asynchronously and handle the response
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // Check if the response was successful
            if (!response.IsSuccessful)
            {
                // Player context not found (anonymous or invalid token)
                if (response.StatusCode == HttpStatusCode.NotFound &&
                    response.Content?.Contains("RESOURCE_NOT_FOUND") == true)
                    throw new UGSException($"Player context not found. This can happen if the token is invalid or belongs to an anonymous player that has been deleted. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

                // Check if the username is already taken
                if (response.StatusCode == HttpStatusCode.Conflict)
                    throw new UGSException($"Username '{username}' is already taken. Please choose a different username. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

                // For other errors, throw a generic exception with the response details
                throw new UGSException($"Sign-Up failed. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);
            }

            // Check if the response content is null or empty
            if (string.IsNullOrEmpty(response.Content))
                throw new UGSException("Empty response received from Player Auth API.");
        }
        catch (Exception ex)
        {
            throw new UGSException($"Failed Sign-Up With Credentials. Status code: {(ex is UGSException ugsEx ? ugsEx.StatusCode.ToString() : "N/A")}\n\nResponse:\n{ex.Message}\n", ex);
        }

        var authResponseData = default(UGSAuthResponseData);
        try
        {
            // Deserialize the response content to AuthResponseData
            authResponseData = JsonConvert.DeserializeObject<UGSAuthResponseData>(response.Content);
            if (authResponseData is null)
                throw new UGSException("Failed to deserialize the response content.");

            // Check if the user ID is null or empty
            if (string.IsNullOrEmpty(authResponseData.userId))
                throw new UGSException("User ID is null or empty.");

        }
        catch (Exception ex)
        {
            throw new UGSException($"Failed to process Sign-Up response. Error deserializing response content. \n\nResponse:\n{response.Content}\n\nError Details:\n{ex.Message}", ex, response.Content);
        }

        // Return the authentication response data containing tokens and user information
        return authResponseData;
    }

    /// <summary>
    /// Signs in a user using the provided username and password credentials via the Unity Player Identity service.
    /// </summary>
    /// <param name="executionContextData">The execution context containing project and environment information.</param>
    /// <param name="username">The username to authenticate.</param>
    /// <param name="password">The password associated with the username.</param>
    /// <returns>A task that represents the asynchronous operation, containing the authentication response data if successful;
    /// otherwise, null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the username or password is null or empty.</exception>
    /// <exception cref="Exception">Thrown when an unexpected error occurs during the sign-in process.</exception>
    /// <exception cref="UGSException">Thrown when the sign-in fails due to invalid credentials, player context issues, deserialization errors, or
    /// other service-related errors.</exception>
    internal static async Task<UGSAuthResponseData?> SignInByCredentials(IExecutionContext executionContextData,
        string username, string password)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContextData);

        // Check if the username or password is null or empty
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Username and password cannot be empty.");

        // Initialize REST client for the Unity Player Identity endpoint to sign in with credentials
        using var restClient = new RestClient("https://player-auth.services.api.unity.com/v1/authentication/usernamepassword/sign-in");

        // Prepare the request with necessary headers and body content
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json,
        };

        // Add Sign-In Username and Password data to the request
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("ProjectId", executionContextData.ProjectId);
        request.AddHeader("UnityEnvironment", executionContextData.EnvironmentName);
        request.AddJsonBody(new { username, password });

        var response = default(RestResponse);
        try
        {
            // Execute the request asynchronously and handle the response
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // Check if the response was successful
            if (!response.IsSuccessful)
            {
                // Player context not found (anonymous or invalid token)
                if (response.StatusCode == HttpStatusCode.NotFound &&
                    response.Content?.Contains("RESOURCE_NOT_FOUND") == true)
                    throw new UGSException($"Player context not found. This can happen if the token is invalid or belongs to an anonymous player that has been deleted. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

                // Check if the username or password is incorrect
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UGSException($"Invalid username or password. Please check your credentials and try again. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

                // For other errors, throw a generic exception with the response details
                throw new UGSException($"Sign-In failed. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);
            }

            // Check if the response content is null or empty
            if (string.IsNullOrEmpty(response.Content))
                throw new UGSException("Empty response received from Player Auth API.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed Sign-In With Credentials. Status code: {(ex is UGSException ugsEx ? ugsEx.StatusCode.ToString() : "N/A")}\n\nResponse:\n{ex.Message}\n", ex);
        }

        var authResponseData = default(UGSAuthResponseData);
        try
        {
            // Deserialize the response content to AuthResponseData
            authResponseData = JsonConvert.DeserializeObject<UGSAuthResponseData>(response.Content);
            if (authResponseData is null)
                throw new UGSException("Failed to deserialize the response content.");

            // Check if the user ID is null or empty
            if (string.IsNullOrEmpty(authResponseData.userId))
                throw new UGSException("User ID is null or empty.");

            return authResponseData;
        }
        catch (Exception ex)
        {
            throw new UGSException($"Failed to process Sign-In response. Error deserializing response content. \n\nResponse:\n{response.Content}\n\nError Details:\n{ex.Message}", ex, response.Content);
        }
    }
    
    /// <summary>
    /// Retrieves the username associated with the specified execution context or alternative player ID from the Unity
    /// Player Identity service.
    /// </summary>
    /// <param name="executionContextData">The execution context containing player and access token information.</param>
    /// <param name="alternativePlayerID">An optional alternative player ID to use instead of the one in the execution context.</param>
    /// <param name="alternativeAccessToken">An optional alternative access token to use for authentication.</param>
    /// <param name="_logger">An optional logger for logging information and warnings.</param>
    /// <returns>A task that represents the asynchronous operation, containing the username if found; otherwise, null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="UGSException">Thrown when the username cannot be retrieved or if the request fails.</exception>
    internal static async Task<string?> GetUsername(IExecutionContext executionContextData,
        string? alternativePlayerID = null, string? alternativeAccessToken = null,
        ILogger? _logger = null)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContextData);

        // Initialize REST client for the Unity Player Identity endpoint to get the username
        using var restClient = new RestClient($"https://social.services.api.unity.com/v1/names/{alternativePlayerID ?? executionContextData.PlayerId}");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Get,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Authorization", $"Bearer {(!string.IsNullOrEmpty(alternativeAccessToken) ? alternativeAccessToken : executionContextData.AccessToken)}");

        var response = default(RestResponse);
        try
        {
            // Execute the request asynchronously and handle the response
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to get or set username. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);
        }
        catch (Exception ex)
        {
            throw new UGSException($"Failed to get username. Status code: {(ex is UGSException ugsEx ? ugsEx.StatusCode.ToString() : "N/A")}\n\nResponse:\n{ex.Message}\n", ex);
        }

        // Check if the response content is null or empty and throw an exception if it is
        if (!string.IsNullOrEmpty(response.Content))
            try
            {
                // Deserialize the response content to a dictionary
                var deserializedResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);

                // Try to get the username from the response
                if (deserializedResponse != null && deserializedResponse.TryGetValue("name", out var name))
                {
                    _logger?.LogInformation("User of Id {PlayerId} has the username: {Username}\n\n{Token}", 
                        alternativePlayerID ?? executionContextData.PlayerId, name, !string.IsNullOrEmpty(alternativeAccessToken) ? alternativeAccessToken : executionContextData.AccessToken);
                    return name as string ?? string.Empty;
                }

                // If the username is not found in the response, log a warning
                else
                    _logger?.LogWarning("Username not found in the response.");
            }
            catch (Exception ex)
            {
                throw new UGSException($"Failed to get username. Error deserializing response content. \n\nResponse:\n{response.Content}\n\nError Details:\n{ex.Message}", ex, response.Content);
            }

        // If the response content is null or empty, or if the username is not found in the response, return null
        return null;
    }

    /// <summary>
    /// Sets the username for a Unity player by sending a request to the Unity Player Identity endpoint.
    /// </summary>
    /// <param name="executionContextData">The execution context containing player and authentication information.</param>
    /// <param name="newName">The new username to set.</param>
    /// <param name="alternativePlayerID">An optional alternative player ID to use instead of the one in the execution context.</param>
    /// <param name="alternativeAccessToken">An optional alternative access token to use for authentication.</param>
    /// <param name="_logger">An optional logger for logging errors and information.</param>
    /// <returns>The set username if successful; otherwise, null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the execution context is null.</exception>
    /// <exception cref="UGSException">Thrown when the request to set the username fails or the response cannot be deserialized.</exception>
    internal static async Task<string?> SetUsername(IExecutionContext executionContextData, 
        string newName, string? alternativePlayerID = null, string? alternativeAccessToken = null,
        ILogger? _logger = null)
    {
        // Validate that the execution context is not null
        if (executionContextData is null)
            throw new ArgumentNullException(nameof(executionContextData), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContextData);

        if (string.IsNullOrEmpty(newName))
        {
            _logger?.LogError("Invalid name. Name cannot be empty.");
            return null;
        }

        // Check if the name matches the required format
        else if (!ValidNameRegex.IsMatch(newName))
        {
            _logger?.LogError("Invalid name. It must not contain spaces or whitespace.");
            return null;
        }

        // Check if the name length is valid
        else if (newName.Length < 3 || newName.Length > 26)
        { 
            _logger?.LogError("Invalid name length. Name must be between 3 and 26 characters.");
            return null;
        }

        // Initialize REST client for the Unity Player Identity endpoint to get or set the username
        using var restClient = new RestClient($"https://social.services.api.unity.com/v1/names/{alternativePlayerID ?? executionContextData.PlayerId}");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json,
        };

        // Set required headers for the request
        request.AddHeader("Authorization", $"Bearer {(!string.IsNullOrEmpty(alternativeAccessToken) ? alternativeAccessToken : executionContextData.AccessToken)}");

        // The API expects the name to be in a JSON object with a "name" property, so we create a dictionary and serialize it to JSON
        var payloadCollection = new Dictionary<string, object> { { "name", newName! } };
        var payload = JsonConvert.SerializeObject(payloadCollection);

        // Add the JSON payload to the request body
        request.AddBody(payloadCollection, "application/json");

        var response = default(RestResponse);
        try
        {
            // Execute the request asynchronously and handle the response
            response = await restClient.ExecuteAsync(request).ConfigureAwait(false);

            // Check if the request was successful
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to get or set username. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);
        }
        catch (Exception ex)
        {
            throw new UGSException($"Failed to get or set username. Status code: {(ex is UGSException ugsEx ? ugsEx.StatusCode.ToString() : "N/A")}\n\nResponse:\n{ex.Message}\n", ex);
        }

        // Check if the response content is null or empty and throw an exception if it is
        if (!string.IsNullOrEmpty(response.Content))
            try
            {
                var deserializedResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);

                // Try to get the username from the response
                if (deserializedResponse != null && deserializedResponse.TryGetValue("name", out var name))
                {
                    _logger?.LogInformation("User of Id {playerId} set username to: {name}\n\n{Token}", 
                        alternativePlayerID ?? executionContextData.PlayerId, name, !string.IsNullOrEmpty(alternativeAccessToken) ? alternativeAccessToken : executionContextData.AccessToken);
                    return name as string ?? string.Empty;
                }

                // If the username is not found in the response, log a warning
                else
                    _logger?.LogWarning("Username not found in the response.");
            }
            catch (Exception ex)
            {
                throw new UGSException($"Failed to set username. Error deserializing response content. \n\nResponse:\n{response.Content}\n\nError Details:\n{ex.Message}", ex, response.Content);
            }

        // If the response content is null or empty, or if the username is not found in the response, return null
        return null;
    }


    internal static async Task<uint> GrantCurrencyAsync(IGameApiClient gameApiClient, IExecutionContext executionContext,
        Currency currencyId, uint amount)
    {
        var currencyDataResponse = await GetCurrency(gameApiClient, executionContext, currencyId, amount);

        // Update the currency amount
        if (currencyDataResponse.TryGetValue(currencyId, out var currentAmount))
            currencyDataResponse[currencyId] = currentAmount + amount;
        else
            currencyDataResponse[currencyId] = amount;

        // Save the updated currency data
        await ProtectedSaveData(gameApiClient, executionContext,
            new Dictionary<string, object>
            {
                [CloudSaveProperties.Currency.ToString()] = currencyDataResponse
            }).ConfigureAwait(false);

        // Return the new amount of the currency
        return currencyDataResponse[currencyId];
    }

    internal static async Task<(bool isSuccessful, uint newCurrency)> ConsumeCurrency(IGameApiClient gameApiClient, IExecutionContext executionContext,
        Currency currencyId, uint amount)
    {
        var currencyDataResponse = await GetCurrency(gameApiClient, executionContext, currencyId, amount);

        // Check if the currency exists and has enough amount
        if (!currencyDataResponse.TryGetValue(currencyId, out var currentAmount))
            throw new ArgumentException($"Currency {currencyId} does not exist in the player's data.");

        // Check if the currency has enough amount
        if (currentAmount < amount)
            return (false, currentAmount);

        // Deduct the amount from the currency
        currencyDataResponse[currencyId] = currentAmount - amount;

        // Save the updated currency data
        await ProtectedSaveData(gameApiClient, executionContext,
            new Dictionary<string, object>
            {
                [CloudSaveProperties.Currency.ToString()] = currencyDataResponse
            }).ConfigureAwait(false);

        // Return the remaining amount of the currency
        return (true, currencyDataResponse[currencyId]);
    }

    internal static async Task<Dictionary<Currency, uint>> GetCurrency(IGameApiClient gameApiClient, IExecutionContext executionContext,
        Currency currencyId, uint amount)
    {
        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        if (currencyId is Currency.None)
            throw new ArgumentException("Currency ID cannot be None.");

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        // Load the current currency data
        var loadDataResponse = await ProtectedLoadData
            (gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Currency.ToString())
            .ConfigureAwait(false);

        // Get the current currency data
        var currencyDataResponse = loadDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Currency.ToString())
            ?.value?.ToObject<Dictionary<Currency, uint>>();

        // If currency data is null, initialize it
        if (currencyDataResponse is null)
            currencyDataResponse = new();

        return currencyDataResponse;
    }

    /// <summary>
    /// Updates the days streak based on the last login date and current date
    /// </summary>
    internal static AnalyticsData UpdateDaysStreak(AnalyticsData? analyticsData)
    {
        if (analyticsData is null)
            analyticsData = new();

        // Get the current date in UTC
        var currentDate = DateTime.UtcNow.Date;

        // Check if the last login date is today
        if (analyticsData.lastLoginDate.Date == currentDate)
            return analyticsData; // No update needed, already logged in today

        // Check if the last login date was yesterday
        if (analyticsData.lastLoginDate.Date == currentDate.AddDays(-1))
            analyticsData.daysStreaked++; // Increment the streak
        else
            analyticsData.daysStreaked = 1; // Reset the streak

        // Update the last login date to today
        analyticsData.lastLoginDate = currentDate;
        return analyticsData;
    }

    /// <summary>
    /// Updates the days streak based on the last login date and current date<br></br>
    /// Note: this code was commented out because the subscription system is implemented via firebase realtime database<br></br>
    /// </summary>
    //internal static async Task<PlayerAdData> TryToBuyMonthlySubscription(ContextData contextData, IGameApiClient api, IExecutionContext ctx,
    //    PlayerAdData playerAdData)
    //{
    //    // Check if the execution context is null
    //    BackendHelper.ContextValidation(contextData);

    //    if (playerAdData is null)
    //        throw new ArgumentNullException(nameof(playerAdData), "Ad data cannot be null.");

    //    // Get the current date in UTC
    //    var currentDate = DateTime.UtcNow.Date;

    //    // Check if the subscription is still active
    //    if (playerAdData.subscriptionPaidDate.HasValue && (currentDate - playerAdData.subscriptionPaidDate.Value.Date).TotalDays <= 31)
    //        return playerAdData; // No update needed, subscription is still active

    //    // TODO: Process payment here (not implemented in this example)

    //    // Update the subscription paid date to today
    //    playerAdData.subscriptionPaidDate = currentDate;

    //    // Save the updated analytics data
    //    await ProtectedSaveData(contextData, api, ctx,
    //        new Dictionary<string, object>
    //        {
    //            [CloudSaveProperties.Ads.ToString()] = playerAdData
    //        }).ConfigureAwait(false);

    //    return playerAdData;
    //}

    #region Leaderboards

    /// <summary>
    /// Retrieves the leaderboard scores for a specific leaderboard ID<br></br>
    /// The environment is set to "production" by default. Couldn't be changed to "development" or "staging" as the API does not support it.<br></br><br></br>
    /// Production is necesary to be able to get the metadata of the leaderboard entries
    /// </summary>
    /// <param name="contextData"></param>
    /// <param name="leaderboardId"></param>
    /// <param name="maxPlayers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="UGSException"></exception>
    internal static async Task<List<LeaderboardData>> GetLeaderboardScores(IExecutionContext executionContext, string leaderboardId, int maxPlayers, 
        ILogger? _logger = null)
    {
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        if (string.IsNullOrEmpty(leaderboardId) || maxPlayers is 0)
            throw new ArgumentException($"The leaderboard ID cannot be empty and the max players must be greater than 0. Provided: leaderboardId = {leaderboardId}, maxPlayers = {maxPlayers}");

        // Initialize REST client for the Unity Player Identity endpoint to get or set the username
        using var restClient = new RestClient($"https://leaderboards.services.api.unity.com/v1/projects/{executionContext.ProjectId}/leaderboards/{leaderboardId}/scores");

        // Prepare the request with necessary headers and query parameters
        var request = new RestRequest
        {
            Method = Method.Get,
            RequestFormat = DataFormat.Json,
        };

        request.AddHeader("Authorization", $"Bearer {executionContext.AccessToken}");
        request.AddQueryParameter("limit", maxPlayers.ToString());

        try
        {
            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessful)
                throw new UGSException($"Failed to get leaderboard scores using leaderboardid {leaderboardId}. Status code: {response.StatusCode}\n\nContent:\n{response.Content}", response.ErrorException, response.Content);

            var parsed = JsonConvert.DeserializeObject<LeaderboardResponse>(response.Content!);
            return parsed?.results ?? new List<LeaderboardData>();
        }
        catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            // 404 means the player had no score in that leaderboard
            // This is expected and safe to ignore
            _logger?.LogDebug(
                "No score found for player {PlayerId} in leaderboard {LeaderboardId}",
                executionContext.PlayerId, leaderboardId);

            return new List<LeaderboardData>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to delete score for player {PlayerId} in leaderboard {LeaderboardId}",
                executionContext.PlayerId, leaderboardId);

            throw new UGSException($"Failed to get leaderboard scores using leaderboardid {leaderboardId}. Status code: {(ex is ApiException apiEx ? apiEx.Response.StatusCode.ToString() : "N/A")}\n\nResponse:\n{ex.Message}\n", ex);
        }
    }

    internal static async Task<List<((string leaderboardID, string numberOfPlayer, string gameMode) keys, LeaderboardData?)>> GetPlayerLeaderboards(IExecutionContext executionContext)
    {
        if (executionContext is null || GameModesNames is null || NumberOfPlayers is null)
            throw new ArgumentNullException("Execution context, game modes, or number of players cannot be null.");

        var tasks = new List<Task<((string leaderboardID, string numberOfPlayer, string gameMode) keys, LeaderboardData?)>>();
        foreach (var number in NumberOfPlayers)
            foreach (var gameMode in GameModesNames)
                tasks.Add(GetPlayerScore(executionContext, number, gameMode));

        // Invoke all tasks to get player scores for each leaderboard
        var leaderboardDatas = await Task.WhenAll(tasks);

        // Filter out null or invalid leaderboard data
        return leaderboardDatas
            .Where(x => x.keys.leaderboardID is not null && x.keys.numberOfPlayer is not null && x.keys.gameMode is not null)
            .ToList();
    }

    internal static async Task<Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement[]>>> TryToGetPlayerTierAchievements(IExecutionContext executionContext, ILogger logger)
    {
        // Invoke all tasks to get player scores for each leaderboard
        var leaderboardDatas = await GetPlayerLeaderboards(executionContext);

        // Check if the player has any achievements in the leaderboard data
        var collection = new Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement[]>>();
        if (leaderboardDatas is not null and { Count: > 0 })
            foreach (var ((leaderboardID, numberOfPlayer, gameMode), leaderboardData) in leaderboardDatas)
            {
                // Check if the player has an entry in the leaderboard data
                if (leaderboardData is null)
                    continue;

                // Check if the tier is valid
                if (!Enum.TryParse(leaderboardData.tier, out LeaderboardTier leaderboardTier) || leaderboardTier is LeaderboardTier.None)
                {
                    logger.LogWarning($"Leaderboard tier '{leaderboardData.tier}' is not valid");
                    continue;
                }

                // Determine the collection key based on the number of players and game mode
                var collectionKey = GenerateLeaderboardAchievementCollection(logger);

                // Try to get the game mode and the corresponding achievement tier collection
                if (Enum.TryParse(gameMode, out GameModeFilter gameModeFilter) && collectionKey.TryGetValue(gameModeFilter, out var achievementTierCollection))
                    // Check if the achievement tier collection is not null and has elements
                    if (achievementTierCollection is not null and { Count: > 0 })
                    {
                        // Get the achievements that the player must have completed for this leaderboard according to the tier
                        var tierAchievementsCollection = new Dictionary<LeaderboardTier, Achievement[]>();
                        for (var i = 0; i <= (int)leaderboardTier; i++)
                        {
                            var tier = (LeaderboardTier)i;

                            // Avoid adding achievements for the None tier
                            if (tier is LeaderboardTier.None)
                                continue;

                            var tierAchievement = achievementTierCollection.FirstOrDefault(x => x.Key == tier).Value;
                            logger.LogInformation($"Achievement tier object for tier {tier} in leaderboard {leaderboardID} for game mode {gameMode} is:\n\n {JsonConvert.SerializeObject(tierAchievement, Formatting.Indented)}");

                            // If the leaderboard ID is not in the collection, add it with the achievements
                            if (!tierAchievementsCollection.TryGetValue(tier, out Achievement[]? value))
                                tierAchievementsCollection.Add(tier, [tierAchievement]);

                            // Add the achievements according to the tier to the collection
                            else if (!value.Contains(tierAchievement))
                                tierAchievementsCollection[tier] = [.. value, tierAchievement];

                            // If the achievement is already in the collection, log a warning
                            else
                                logger.LogWarning($"Achievement {tierAchievement} for tier {tier} in leaderboard {leaderboardID} for game mode {gameMode} is already in the collection.");
                        }

                        // If the leaderboard ID is not in the collection, add it with the achievements
                        if (!collection.TryAdd(gameModeFilter, tierAchievementsCollection))
                            // If the leaderboard tier is not in the collection, add it with the achievements
                            if (!collection[gameModeFilter].TryGetValue(leaderboardTier, out Achievement[]? value))
                                collection[gameModeFilter] = tierAchievementsCollection;

                            // Override the achievements for the tier if they already exist
                            else
                            {
                                collection[gameModeFilter][leaderboardTier] = [.. value, .. tierAchievementsCollection[leaderboardTier]];
                                logger.LogInformation($"Achievements for tier {leaderboardTier} in leaderboard {leaderboardID} for game mode {gameMode} were overridden with new achievements.");
                            }
                    } else
                        logger.LogWarning($"No achievements defined for game mode '{gameMode}' in {numberOfPlayer} leaderboard {leaderboardID} with tier {leaderboardTier}.");
else
                    logger.LogWarning($"Game mode '{gameMode}' or number of players '{numberOfPlayer}' is not valid for leaderboard {leaderboardID}.");
            }

        // Return the collection of achievements according to the tier
        logger.LogInformation($"Found {collection.Count} game modes with achievements for player {executionContext.PlayerId}.\n\n{JsonConvert.SerializeObject(collection, Formatting.Indented)}");
        return collection;
    }

    internal static async Task<bool> TryToCompleteTierAchievement(IExecutionContext executionContext, ILogger logger, List<PlayerAchievementData> playerDataAchievements)
    {
        // Invoke all tasks to get player scores for each leaderboard
        var leaderboardDatas = await TryToGetPlayerTierAchievements(executionContext, logger);

        // Check if the player has any achievements in the leaderboard data
        if (leaderboardDatas is not null and { Count: > 0 })
            foreach (var (leaderboardID, collection) in leaderboardDatas)
                foreach (var (tier, achievements) in collection)
                {
                    // Check if leaderboard data is null or not
                    if (achievements is null or { Length: 0 })
                    {
                        logger.LogWarning($"Player leaderboard data for tier {tier} is registered, but is null or empty");
                        continue;
                    }

                    // Check if the player has all the achievements for this leaderboard and tier
                    var leftingAchievements = new List<Achievement>();
                    if (achievements.All(x => playerDataAchievements.Any(y => y.achievement == x)))
                        leftingAchievements = achievements
                            .Where(x => !playerDataAchievements.Any(y => y.achievement == x))
                            .ToList();

                    // If the player is missing achievements for this leaderboard and tier, proceed to add them
                    if (leftingAchievements is not null and { Count: > 0 })
                    {
                        // Log the achievements that the player does not have
                        logger.LogInformation($"Player {executionContext.PlayerId} is missing achievements {string.Join(", ", leftingAchievements)} for leaderboard {leaderboardID} with tier {tier}.");

                        // Update the player's achievements data with the missing achievements
                        foreach (var achievement in leftingAchievements)
                        {
                            // Crate a new completed PlayerAchievementData for the missing achievement
                            var newAchievement = new PlayerAchievementData
                            {
                                achievement = achievement,
                                completed = true,
                                completedTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(), // Set the completion date to now
                            };

                            // Add the new achievement to the player's data
                            playerDataAchievements.Add(newAchievement);
                        }

                        // Log the achievements that were added to the player data
                        logger.LogInformation($"Added achievements {string.Join(", ", leftingAchievements)} to player {executionContext.PlayerId} for leaderboard {leaderboardID} with tier {tier}.");

                        // Mark that the achievements were updated
                        return true;
                    }
                }

        // If no achievements were added, log a message
        return false;
    }

    private static Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement>> GenerateLeaderboardAchievementCollection(ILogger logger)
    {
        // Create the full dictionary to return
        var result = new Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement>>();

        // Loop through each game mode (e.g., French, Draw, Block, etc.)
        foreach (GameModeFilter mode in Enum.GetValues(typeof(GameModeFilter)))
        {
            // Avoid adding entries for None or All game modes
            if (mode is GameModeFilter.None or GameModeFilter.All)
                continue;

            var tierAchievementMap = new Dictionary<LeaderboardTier, Achievement>();

            foreach (LeaderboardTier tier in Enum.GetValues(typeof(LeaderboardTier)))
            {
                // Avoid adding achievements for the None tier
                if (tier is LeaderboardTier.None)
                    continue;

                // Construct the expected enum name, e.g., ReachClassA_Draw_1v3
                var enumName = $"Reach{tier}_{mode}";

                // Try to parse the enum by name (case-sensitive)
                if (Enum.TryParse(enumName, out Achievement achievement))
                {
                    // Avoid adding achievements that are None
                    if (achievement is Achievement.None)
                        continue;

                    tierAchievementMap[tier] = achievement;
                } else
                    logger.LogWarning($"Achievement '{enumName}' is not defined in the Achievement enum.");
            }

            result[mode] = tierAchievementMap;
        }

        return result;
    }


    private static async Task<((string leaderboardID, string numberOfPlayer, string gameMode) keys, LeaderboardData?)> GetPlayerScore(IExecutionContext executionContext, string numberOfPlayer, string gameMode)
    {
        var leaderboardID = $"{gameMode}{numberOfPlayer}";

        // Copy the leaderboardID, numberOfPlayer, and gameMode to a tuple for better readability and to avoid variable shadowing
        var (_leaderboardID, _numberOfPlayer, _gameMode) = (leaderboardID, numberOfPlayer, gameMode);
        var playerScoreData = await GetPlayerScore(executionContext, leaderboardID);

        return ((_leaderboardID, _numberOfPlayer, _gameMode), playerScoreData);
    }

    /// <summary>
    /// Retrieves the player score for a specific leaderboard ID<br></br>
    /// The environment is set to "production" by default. Couldn't be changed to "development" or "staging" as the API does not support it.<br></br><br></br>
    /// Production is necesary to be able to get the metadata of the leaderboard entries
    /// </summary>
    /// <param name="contextData"></param>
    /// <param name="leaderboardId"></param>
    /// <param name="maxPlayers"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="UGSException"></exception>
    internal static async Task<LeaderboardData?> GetPlayerScore(IExecutionContext executionContext, string leaderboardId)
    {
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        if (string.IsNullOrEmpty(leaderboardId))
            throw new ArgumentException($"The leaderboard ID cannot be empty. Provided: leaderboardId = {leaderboardId}");

        using (var restClient = new RestClient($"https://leaderboards.services.api.unity.com/v1/projects/{executionContext.ProjectId}/leaderboards/{leaderboardId}/scores/players/{executionContext.PlayerId}"))
        {
            var request = new RestRequest
            {
                Method = Method.Get,
                RequestFormat = DataFormat.Json,
            };

            request.AddHeader("Authorization", $"Bearer {executionContext.AccessToken}");
            request.AddQueryParameter("includeMetadata", true);

            var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
            if (response.IsSuccessful)
            {
                var parsed = JsonConvert.DeserializeObject<LeaderboardData>(response.Content!);
                return parsed;
            }
            return null;
        }
    }

    /// <summary>
    /// Adds a player's score to a specific leaderboard.<br></br>
    /// Its environment is set to "production" by default. Couldn't be changed to "development" or "staging" as the API does not support it.<br></br><br></br>
    /// Production is used to be able to register or override the metadata of the leaderboard entries.
    /// </summary>
    /// <param name="executionContext"></param>
    /// <param name="leaderboardId"></param>
    /// <param name="playerId"></param>
    /// <param name="score"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="UGSException"></exception>
    internal static async Task<LeaderboardData?> AddPlayerScore(IExecutionContext executionContext,
        double score, string leaderboardId, object? metadata = null)
    {
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        BackendHelper.ContextValidation(executionContext);

        if (string.IsNullOrEmpty(leaderboardId))
            throw new ArgumentException("Leaderboard ID cannot be null or empty.");

        // Construct the verified URL from Unity documentation
        var url = $"https://leaderboards.services.api.unity.com/v1/projects/{executionContext.ProjectId}/leaderboards/{leaderboardId}/scores/players/{executionContext.PlayerId}";

        using var client = new RestClient(url);
        var request = new RestRequest
        {
            Method = Method.Post,
            RequestFormat = DataFormat.Json
        };

        // Required headers for admin authentication
        request.AddHeader("Authorization", $"Bearer {executionContext.AccessToken}");
        request.AddHeader("Content-Type", "application/json");

        // Build body based on official schema
        var body = new Dictionary<string, object>
        {
            { "score", score }
        };

        if (metadata is not null)
            body["metadata"] = metadata;

        request.AddJsonBody(body);

        // Execute request and handle failure
        var response = await client.ExecuteAsync(request).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new UGSException(
                $"Failed to post score for player '{executionContext.PlayerId}' to leaderboard '{leaderboardId}'. Status code: {response.StatusCode}\nContent:\n{response.Content}",
                response.ErrorException,
                response.Content
            );

        var parsed = JsonConvert.DeserializeObject<LeaderboardData>(response.Content!);
        return parsed;
    }

    #endregion
    #endregion

    /// <summary>
    /// Normalizes the value into a format supported by Cloud Save:
    /// - Primitive types are passed as-is.
    /// - Complex types (objects or arrays) are converted into a JToken to ensure proper JSON formatting.
    /// </summary>
    private static object NormalizeToCloudSaveFormat(object value)
    {
        // Return primitive types and strings as-is
        if (value is string or ValueType)
            return value;

        // Convert complex objects (lists, dictionaries, etc.) to JToken for correct serialization
        return JToken.FromObject(value);
    }

    /// <summary>
    /// Normalizes a name by removing accents, diacritics, and special characters.
    /// Converts to lowercase and trims spaces.
    /// This is used before generating search tokens.
    /// </summary>
    internal static string NormalizeName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 1. Convert to lowercase
        string lower = input.ToLowerInvariant();

        // 2. Replace special accented characters manually
        lower = ParseSpecialCharacters(lower);

        // 3. Decompose accented characters
        string normalized = lower.Normalize(NormalizationForm.FormD);

        // 4. Remove diacritic marks using LINQ filtering
        var filtered = new string(normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        // 5. Remove any remaining special characters (keep letters, digits, and spaces)
        filtered = Regex.Replace(filtered, @"[^a-z0-9\s]", string.Empty);

        // 6. Trim and collapse multiple spaces into one
        filtered = Regex.Replace(filtered.Trim(), @"\s+", " ");

        return filtered;

        string ParseSpecialCharacters(string str)
        {
            return str
                .Replace("á", "a")
                .Replace("à", "a")
                .Replace("ä", "a")
                .Replace("â", "a")
                .Replace("ã", "a")
                .Replace("å", "a")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ë", "e")
                .Replace("ê", "e")
                .Replace("í", "i")
                .Replace("ì", "i")
                .Replace("ï", "i")
                .Replace("î", "i")
                .Replace("ó", "o")
                .Replace("ò", "o")
                .Replace("ö", "o")
                .Replace("ô", "o")
                .Replace("õ", "o")
                .Replace("ú", "u")
                .Replace("ù", "u")
                .Replace("ü", "u")
                .Replace("û", "u")
                .Replace("ñ", "n");
        }
    }

    /// <summary>
    /// Generates an array of searchable tokens from a club name.
    /// Each token includes full words and all incremental prefixes for each word.
    /// This enables Firestore "array-contains" search queries by prefix.
    /// </summary>
    /// <param name="name">The original club name to generate tokens for.</param>
    /// <returns>A list of normalized search tokens suitable for Firestore array indexing.</returns>
    internal static List<string> GenerateSearchTokens(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new List<string>();

        // Normalize and clean the name
        string normalized = NormalizeName(name).Trim();

        // Split by space, remove empties
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokens = new HashSet<string>();

        foreach (var word in words)
        {
            // Defensive clean-up of potential leftover Unicode tildes
            var cleanWord = word.Normalize(NormalizationForm.FormD)
                .Replace("\u0301", "") // acute accent
                .Replace("\u0300", "") // grave accent
                .Trim();

            // Skip if word is somehow empty
            if (string.IsNullOrEmpty(cleanWord))
                continue;

            // Add prefixes
            for (int i = 1; i <= cleanWord.Length; i++)
                tokens.Add(cleanWord.Substring(0, i));

            // Add full word itself
            tokens.Add(cleanWord);
        }

        return tokens.ToList();
    }
}
