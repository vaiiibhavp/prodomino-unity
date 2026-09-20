using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using static Backend.UGSBackend;
using static HelperSharedLibrary.AccountDeletionResponse;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend;

/// <summary>
/// Cloud Code module responsible for orchestrating a full account deletion flow.
/// This includes:
/// - Validation and security checks
/// - Firebase account deletion (Auth + data)
/// - Unity Gaming Services (UGS) data deletion
/// - UGS account deletion
///
/// This module intentionally contains NO business-side recovery logic.
/// All retries are time-bounded and defensive.
/// </summary>
public class DeleteAccountModule(ILogger<DeleteAccountModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<DeleteAccountModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// I create this boolean to avid delete the code when the email is not comming and the account couldn't be deleted (facebook im talking to you!)
    /// </summary>
    private readonly bool isValidatingEmail = false;

    /// <summary>
    /// Entry point for account deletion triggered by the player.
    /// This function performs:
    /// 1. Context and input validation
    /// 2. Ownership verification via confirmation email
    /// 3. Firebase account deletion (delegated to Firebase backend)
    /// 4. UGS data cleanup (Cloud Save)
    /// 5. Final UGS account removal
    ///
    /// The response is always encrypted and safe for client consumption.
    /// </summary>
    [CloudCodeFunction(nameof(DeleteAccount))]
    public async Task<string> DeleteAccount(
        IExecutionContext executionContext,
        string parametersEncryptedJson)
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

        // Decrypt and validate incoming parameters.
        // Any tampering or replay attack should fail at this stage.
        var data = BackendHelper.ValidateEncriptedParameters(
            parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);


        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data.");

        // Try to get product ID. If missing or invalid, throw error
        var confirmationEmail = isValidatingEmail && data.TryGetValue("confirmationEmail", out var confirmationEmailObj) && confirmationEmailObj is string confirmationEmailStr ? confirmationEmailStr : null;
        if (isValidatingEmail && string.IsNullOrEmpty(confirmationEmail))
            throw new ArgumentException("Invalid or missing confirmationEmail.");

        var leaderboardIds = data.TryGetValue("leaderboardIds", out var leaderboardIdsObj) && leaderboardIdsObj is JArray leaderboardIdsJArray
            ? leaderboardIdsJArray.ToObject<string[]>()
            : null;

        var emailKey = CloudSaveProperties.Email.ToString();
        var accountCreationAtKey = CloudSaveProperties.AccountCreatedAt.ToString();
        var firebaseIdKey = CloudSaveProperties.FirebaseID.ToString();
        var nationalityKey = CloudSaveProperties.Nationality.ToString();

        // Load current player data
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            emailKey, accountCreationAtKey, firebaseIdKey, nationalityKey);

        // Check if the email registered is null or empty
        var emailDataResponseObj = playerDataResponse?.FirstOrDefault(x => x?.key == emailKey)?.value?.ToObject<string>();
        var emailDataResponse = isValidatingEmail && emailDataResponseObj is string emailDataResponseStr ? emailDataResponseStr : null;
        if (isValidatingEmail && string.IsNullOrEmpty(emailDataResponse))
            throw new UGSException("Email data response is null or empty");
        
        // Check if the account creation date is null or empty
        var accountCreationAtObj = playerDataResponse?.FirstOrDefault(x => x?.key == accountCreationAtKey)?.value?.ToObject<string>();
        if (accountCreationAtObj is not string accountCreationAtString 
            || ParseAccountCreationAt(accountCreationAtString) is not DateTime accountCreationAtDateTime)
            throw new UGSException("Account creation At data response is null or empty");
        
        var firebaseIdObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIdKey)?.value?.ToObject<string>();
        if (firebaseIdObj is not string firebaseIdString)
            throw new UGSException("Firebase id is null or empty");

        var playerNationalityData = playerDataResponse
            ?.FirstOrDefault(x => x?.key == nationalityKey)
            ?.value
            ?.ToObject<NationalityData>();

        // Prepare response object
        var accountDeletionResponse = new AccountDeletionResponse();

        // Calculate when the account will be eligible for deletion based on its creation date
        var availableDateForDeletion = accountCreationAtDateTime.AddHours(24);

        // Prevent deletion of very recently created accounts.
        // This protects against abuse, bot churn, and accidental deletions.
        if ((DateTime.UtcNow - availableDateForDeletion).TotalHours <= 0)
        {
            var leftingHours = (availableDateForDeletion - DateTime.UtcNow).TotalHours;
            accountDeletionResponse = BuildDeletionResponse(
                DenyDeletionReason.AccountRecentlyCreated,
                $"You must wait {leftingHours:F2} hours before deleting your account");

            return EncryptResponse(executionContext, accountDeletionResponse);
        }

        // Validate that email used to confirmation is the same
        if (isValidatingEmail && emailDataResponse != confirmationEmail)
        {
            accountDeletionResponse = BuildDeletionResponse(
                DenyDeletionReason.IncorrectConfirmationEmail,
                "Incorrect confirmation email");

            // Target to encrypt response
            return EncryptResponse(executionContext, accountDeletionResponse);
        }

        var firebaseDataEliminationErrorResponse = await DeletingFirebaseData();
        if (!string.IsNullOrEmpty(firebaseDataEliminationErrorResponse))
            return firebaseDataEliminationErrorResponse;
        _logger.LogInformation($"Success deleting firebase account of the user with firebase id {firebaseIdString}. Proceding to delete UGS data...");

        var ugsDataEliminationErrorResponse = await DeletingUGSData();
        if (!string.IsNullOrEmpty(ugsDataEliminationErrorResponse))
            return ugsDataEliminationErrorResponse;
        _logger.LogInformation($"Success deleting UGS data of the user with unity id {playerId}. Proceding to delete UGS account...");

        var ugsScoresEliminationErrorResponse = await DeletePlayerScoresFromLeaderboardsAsync();
        if (!string.IsNullOrEmpty(ugsScoresEliminationErrorResponse))
            return ugsScoresEliminationErrorResponse;
        _logger.LogInformation($"Success deleting player scores from leaderboards of the user with unity id {playerId}. Proceding to delete UGS account...");

        var ugsAccountEliminationErrorResponse = await DeletingUGSAccount();
        if (!string.IsNullOrEmpty(ugsAccountEliminationErrorResponse))
            return ugsAccountEliminationErrorResponse;
        _logger.LogInformation($"Success deleting UGS account of the user with unity id {playerId}. Finishing process");

        return EncryptResponse(executionContext, new());

        /// Firebase deletion is treated as an external system dependency.
        /// We retry only while the backend explicitly marks the failure as retryable.
        async Task<string> DeletingFirebaseData()
        {
            var deleteFirebaseAccountResponse = default(FirebaseApiHelper.DeleteUserResponse?);
            var startingTime = DateTime.UtcNow;
            var attempts = 0;
            var delayMs = 500;
            do
            {
                _logger.LogInformation($"Starting attempt {attempts} trying to delete firebase account of user with firebase id {firebaseIdString}");
                deleteFirebaseAccountResponse =
                    await FirebaseApiHelper.DeleteFirebaseAccount
                    (firebaseIdString,
                    nationality: (playerNationalityData?.nationalityType ?? default).ToString(),
                    gameApiClient: _gameApiClient,
                    executionContext: executionContext, 
                    _logger);

                // Backend may return a logical failure with retry hints
                if (deleteFirebaseAccountResponse is not null &&
                    !string.IsNullOrEmpty(deleteFirebaseAccountResponse.error))
                    _logger.LogWarning(
                        $"Failed attempt {attempts} deleting firebase account. Error:\n{deleteFirebaseAccountResponse.error}");

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 4000); // cap at 4s
                attempts++;
            }
            while (
                deleteFirebaseAccountResponse is null or { success: false, retryable: true }
                && (DateTime.UtcNow - startingTime).TotalSeconds < 30
            );


            if (deleteFirebaseAccountResponse is null or { success: false })
            {
                var errorMessage = deleteFirebaseAccountResponse?.error ?? "Unknow";
                _logger.LogError($"Failed trying to delete firebase account of user with firebase id {firebaseIdString}. Error:\n\n{errorMessage}");

                accountDeletionResponse = BuildDeletionResponse(
                    DenyDeletionReason.FailedToDeleteFirebaseAccount,
                    "There is a problem trying to delete the account. Try again later");

                // Target to encrypt response
                return EncryptResponse(executionContext, accountDeletionResponse);
            }

            return string.Empty;
        }

        /// UGS Cloud Save data is deleted in phases.
        /// Each data scope is retried independently to avoid partial failures.
        async Task<string> DeletingUGSData()
        {
            var (isPublicDataDeleted, isProtectedDataDeleted, isPrivateDataDeleted) = (false, false, false);
            var startingTime = DateTime.UtcNow;
            var attempts = 0;
            var delayMs = 500;

            do
            {
                _logger.LogInformation($"Starting attempt {attempts} trying to delete UGS account data of user with unity id {playerId}. " +
                    $"\n\nCurrent status is: \nPublic: {isPublicDataDeleted}, Protected: {isProtectedDataDeleted}, Private: {isPrivateDataDeleted}");

                if (!isPublicDataDeleted)
                {
                    _logger.LogInformation($"Trying to delete public data of the user with unity id {playerId}");
                    isPublicDataDeleted = await DeletePublicData(executionContext);
                }

                if (!isProtectedDataDeleted)
                {
                    _logger.LogInformation($"Trying to delete protected data of the user with unity id {playerId}");
                    isProtectedDataDeleted = await DeleteProtectedData(executionContext);
                }

                if (!isPrivateDataDeleted)
                {
                    _logger.LogInformation($"Trying to delete private data of the user with unity id {playerId}");
                    isPrivateDataDeleted = await DeletePrivateData(executionContext);
                }

                if (!isPublicDataDeleted || !isProtectedDataDeleted || !isPrivateDataDeleted)
                    _logger.LogWarning($"Failed attempt {attempts} trying to delete UGS account data of user with unity id {playerId}. " +
                        $"\n\nCurrent status is: \nPublic: {isPublicDataDeleted}, Protected: {isProtectedDataDeleted}, Private: {isPrivateDataDeleted}");

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 4000); // cap at 4s
                attempts++;
            }
            while (
                (!isPublicDataDeleted || !isProtectedDataDeleted || !isPrivateDataDeleted)
                && (DateTime.UtcNow - startingTime).TotalSeconds < 30
            );

            // If after retries the data couldn't be deleted, log an error and return a failure response
            if (!isPublicDataDeleted || !isProtectedDataDeleted || !isPrivateDataDeleted)
            {
                var errorMessage = "Couldn't delete each player UGS data";
                _logger.LogError($"Failed trying to delete UGS account data of user with unity id {playerId}. Error:\n\n{errorMessage}");

                accountDeletionResponse = BuildDeletionResponse(
                    DenyDeletionReason.FailedToDeleteUGSAccount,
                    "There is a problem deleting your information. Try again later");

                // Target to encrypt response
                return EncryptResponse(executionContext, accountDeletionResponse);
            }

            return string.Empty;
        }

        /// <summary>
        /// Asynchronously deletes a player's scores from the specified leaderboards.
        /// </summary>
        async Task<string> DeletePlayerScoresFromLeaderboardsAsync()
        {
            if (leaderboardIds is null)
                throw new ArgumentNullException(nameof(leaderboardIds));

            var ugsScoresWereSuccessfullyDeleted = false;
            var startingTime = DateTime.UtcNow;
            var attempts = 0;
            var delayMs = 500;

            var leaderboardsSuccessfullyDeleted = new HashSet<string>();

            // Create delete tasks in parallel for efficiency
            var deleteTasks = leaderboardIds.Select(async leaderboardId =>
            {
                // Skip if this leaderboard was already successfully deleted in a previous attempt
                if (leaderboardsSuccessfullyDeleted.Contains(leaderboardId))
                    return;

                logger?.LogInformation(
                    "Deleting score for player {PlayerId} from leaderboard {LeaderboardId}",
                    executionContext.PlayerId, leaderboardId);

                // Official UGS Leaderboards delete score call
                try
                {
                    var wasDeletedSuccessfully = await UGSApiHelper.DeleteLeaderboardScore(
                        executionContext: executionContext, gameApiClient: _gameApiClient, 
                        leaderboardId: leaderboardId, 
                        _logger: logger);

                    // If successful, add to the set of successfully deleted leaderboards
                    if (wasDeletedSuccessfully)
                        lock (leaderboardsSuccessfullyDeleted)
                            leaderboardsSuccessfullyDeleted.Add(leaderboardId);
                }
                catch (UGSException) { }
            });
            do
            {
                _logger.LogInformation($"Starting attempt {attempts} trying to delete player scores from leaderboards. PlayerId: {executionContext.PlayerId}, LeaderboardIds: {string.Join(", ", leaderboardIds)}");

                // Await all deletions
                await Task.WhenAll(deleteTasks);

                // Check if all leaderboards were successfully deleted
                ugsScoresWereSuccessfullyDeleted = leaderboardsSuccessfullyDeleted.Count == leaderboardIds.Length;

                if (!ugsScoresWereSuccessfullyDeleted)
                    _logger.LogWarning($"Failed attempt {attempts} trying to delete player scores from leaderboards. PlayerId: {executionContext.PlayerId}, LeaderboardIds: {string.Join(", ", leaderboardIds)}. Successfully deleted from: {string.Join(", ", leaderboardsSuccessfullyDeleted)}");

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 4000); // cap at 4s
                attempts++;
            }
            while
                (!ugsScoresWereSuccessfullyDeleted && (DateTime.UtcNow - startingTime).TotalSeconds < 30);

            // If after retries the scores couldn't be deleted, log an error and return a failure response
            if (!ugsScoresWereSuccessfullyDeleted)
            {
                var errorMessage = $"Couldn't delete player scores from leaderboards";
                _logger.LogError($"Failed trying to delete player scores from leaderboards for user with unity id {executionContext.PlayerId}. Error:\n\n{errorMessage}");

                accountDeletionResponse = BuildDeletionResponse(
                    DenyDeletionReason.FailedToDeleteUGSAccount,
                    "There is a problem deleting your information. Try again later");

                // Target to encrypt response
                return EncryptResponse(executionContext, accountDeletionResponse);
            }

            return string.Empty;
        }

        /// Final irreversible step.
        /// Once this succeeds, the Unity account no longer exists.
        async Task<string> DeletingUGSAccount()
        {
            var ugsAccountWasSuccessfullyDeleted = false;
            var startingTime = DateTime.UtcNow;
            var attempts = 0;
            var delayMs = 500;

            do
            {
                _logger.LogInformation($"Starting attempt {attempts} trying to delete UGS account of user with unity id {playerId}");
                ugsAccountWasSuccessfullyDeleted = await DeleteAccount(executionContext);

                if (!ugsAccountWasSuccessfullyDeleted)
                    _logger.LogWarning($"Failed attempt {attempts} trying to delete UGS account of user with unity id {playerId}");

                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 4000); // cap at 4s
                attempts++;
            }
            while
                (!ugsAccountWasSuccessfullyDeleted && (DateTime.UtcNow - startingTime).TotalSeconds < 30);

            // If after retries the account couldn't be deleted, log an error and return a failure response
            if (!ugsAccountWasSuccessfullyDeleted)
            {
                var errorMessage =$"Couldn't delete UGS account of player";
                _logger.LogError($"Failed trying to delete UGS account of user with unity id {executionContext.PlayerId}. Error:\n\n{errorMessage}");

                accountDeletionResponse = BuildDeletionResponse(
                    DenyDeletionReason.FailedToDeleteFirebaseAccount,
                    "Your account couldn't be deleted. Try again later");

                // Target to encrypt response
                return EncryptResponse(executionContext, accountDeletionResponse);
            }

            return string.Empty;
        }
    }


    /// <summary>
    /// Encrypts the response payload using a key derived from the player's identity.
    /// This prevents response tampering and replay attacks on the client.
    /// </summary>
    private static string EncryptResponse(
        IExecutionContext executionContext,
        AccountDeletionResponse response)
    {
        // Derived key/iv for encrypting response
        var playerId = executionContext.PlayerId ?? string.Empty;
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Build response
        var responseJson = JsonConvert.SerializeObject(response);
        var encryptedResponse = SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv);
        return JsonConvert.SerializeObject(encryptedResponse);
    }

    /// <summary>
    /// Helper to build a deletion response
    /// </summary>
    private static AccountDeletionResponse BuildDeletionResponse(
        DenyDeletionReason reason,
        string message)
    {
        return new AccountDeletionResponse
        {
            reason = reason,
            message = message
        };
    }

    /// <summary>
    /// Parse ISO 8601 round-trip string back to DateTime (UTC)
    /// </summary>
    private DateTime? ParseAccountCreationAt(string date)
    {
        if (!DateTime.TryParse(
            date,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var createdAt))
        {
            _logger.LogError($"Invalid account creation date format: '{date}'");
            return null;
        }

        return createdAt;
    }


    #region Delete UGS Data Helpers
    private async Task<bool> DeleteData(IExecutionContext executionContext, string? saveType = null)
    {
        // Validate that the execution context is not null
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");
        
        // Perform additional context validation
        BackendHelper.ContextValidation(executionContext);

        try
        {
            // Determine the target path: either using a custom ID or the player's ID
            var target =  $"players/{executionContext.PlayerId}";

            // Determine the accessibility path (e.g., protected or private)
            var accessibility = string.IsNullOrEmpty(saveType) ? "/" : $"/{saveType}/";

            // Get the secret authorization header
            var authHeader = await UGSConfigData.GetAuthHeader(_gameApiClient, executionContext);

            // Initialize REST client for the Unity Cloud Save endpoint
            using (var restClient = new RestClient($"https://services.api.unity.com/cloud-save/v1/data/projects/{executionContext.ProjectId}/environments/{executionContext.EnvironmentId}/{target}{accessibility}items"))
            {
                var request = new RestRequest
                {
                    Method = Method.Delete,
                    RequestFormat = DataFormat.Json,
                };

                // Set required headers for the request
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", $"basic {authHeader}");

                // Execute the request asynchronously
                var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
                return response.IsSuccessful;
            }
        }
        catch (UGSException ex)
        {
            // Handle specific UGS exceptions and provide additional details
            _logger.LogError($"\nError deleting data in Cloud Save. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}", ex, ex.content);
        }
        catch (Exception ex)
        {
            // General error handling fallback
            _logger.LogError($"Error deleting data in Cloud Save. Details: {ex.Message}", ex);
        }

        return false;
    }
    
    private async Task<bool> DeletePublicData(IExecutionContext executionContext)
        => await DeleteData(executionContext, saveType: "public");

    private async Task<bool> DeleteProtectedData(IExecutionContext executionContext)
        => await DeleteData(executionContext, saveType: "protected");
    
    private async Task<bool> DeletePrivateData(IExecutionContext executionContext)
        => await DeleteData(executionContext, saveType: "");
    #endregion

    private async Task<bool> DeleteAccount(IExecutionContext executionContext)
    {
        // Validate that the execution context is not null
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");
        
        // Perform additional context validation
        BackendHelper.ContextValidation(executionContext);

        try
        {
            // Get the secret authorization header
            var authHeader = await UGSConfigData.GetAuthHeader(_gameApiClient, executionContext);

            // Initialize REST client for the Unity Cloud Save endpoint
            using (var restClient = new RestClient($"https://services.api.unity.com/player-identity/v1/projects/{executionContext.ProjectId}/users/{executionContext.PlayerId}"))
            {
                var request = new RestRequest
                {
                    Method = Method.Delete,
                    RequestFormat = DataFormat.Json,
                };

                // Set required headers for the request
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", $"basic {authHeader}");

                // Execute the request asynchronously
                var response = await restClient.ExecuteAsync(request).ConfigureAwait(false);
                return response.IsSuccessful;
            }
        }
        catch (UGSException ex)
        {
            // Handle specific UGS exceptions and provide additional details
            _logger.LogError($"\nError deleting UGS account. Status code: {ex.StatusCode}\n\nResponse:\n{ex.Message}", ex, ex.content);
        }
        catch (Exception ex)
        {
            // General error handling fallback
            _logger.LogError($"Error deleting UGS account. Details: {ex.Message}", ex);
        }

        return false;
    }
}
