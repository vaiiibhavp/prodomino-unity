using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend;

public class EmailVerificationModule
{
    private readonly ILogger<EmailVerificationModule> _logger;
    private readonly IGameApiClient _gameApiClient;

    public EmailVerificationModule(ILogger<EmailVerificationModule> logger, IGameApiClient gameApiClient)
    {
        _logger = logger;
        _gameApiClient = gameApiClient;
    }

    /// <summary>
    /// Send an email verification to the user to verify it from firebase.<br></br><br></br>
    /// </summary>
    /// <param name="executionContext"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    [CloudCodeFunction(nameof(EmailVerification))]
    public async Task<string> EmailVerification(IExecutionContext executionContext)
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

        // Check if the email is registered in UGS and Firebase, if it is only registered in UGS, registered in firebase and send the email vefication
        try
        {
            var sendEmailResponse = await SendEmailVerification();
            if  (sendEmailResponse is null or { email: null or "" })
                throw new Exception("Email verification failed. Could not find the user in Firebase.");

            // Return the response with the updated achievements and game data
            var responseDataJson = JsonConvert.SerializeObject(sendEmailResponse);

            // Get the data use to encrypt the response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Encrypt the response data and return it as a JSON string
            var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseDataJson, derivedKey, derivedIv));
            return encryptedDataJson;
        }
        catch (Exception ex)
        {
            throw new Exception($"\nEmail verification failed.\n\nResponse:\n{ex.Message}", ex);
        }

        async Task<FirebaseSendEmailResponse> SendEmailVerification()
        {
            // Check if the execution context is null
            BackendHelper.ContextValidation(executionContext);
            var loadDataResponse = default(ResponseData?[]?);

            _logger?.LogInformation($"Checking email registration...");

            // Get the data from the encrypted parameters JSON
            loadDataResponse = await UGSApiHelper.ProtectedLoadData
                (_gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: null,
                isThrowingException: true,
                CloudSaveProperties.Email.ToString(),
                CloudSaveProperties.FirebaseIDToken.ToString(),
                CloudSaveProperties.FirebaseRefreshToken.ToString())
                .ConfigureAwait(false);

            // Validate entry data
            var emailDataResponseObj = loadDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.Email.ToString())?.value?.ToObject<string>();
            var firebaseIDTokenObj = loadDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.FirebaseIDToken.ToString())?.value?.ToObject<string>();
            var firebaseRefreshTokenObj = loadDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.FirebaseRefreshToken.ToString())?.value?.ToObject<string>();

            if (firebaseIDTokenObj is not string firebaseIDToken || string.IsNullOrEmpty(firebaseIDToken)
                || firebaseRefreshTokenObj is not string firebaseRefreshToken || string.IsNullOrEmpty(firebaseRefreshToken))
                throw new ArgumentException($"Firebase ID token or refresh token is missing.");

            // Get the email from UGS
            var lookUpResponse = await FirebaseApiHelper.LookUpAsync(firebaseIDToken, firebaseRefreshToken);
            if (lookUpResponse is null)
                throw new Exception($"Failed to look up the user in Firebase.");

            var user = lookUpResponse.users.FirstOrDefault();
            if (user is null)
                throw new Exception($"Failed to retrieve user data from Firebase.");
        
            // Register the email into Firebase if it was got from UGS and if not registered yet send the verification email
            if (string.IsNullOrEmpty(user.email) && emailDataResponseObj is string emailDataResponse && !string.IsNullOrEmpty(emailDataResponse))
            {
                _logger?.LogInformation($"Registering email {emailDataResponse} into Firebase...");

                // Register the email into Firebase
                await FirebaseApiHelper.UpdateFirebaseUserProfile(lookUpResponse.idToken, lookUpResponse.refreshToken, _logger,
                    ("email", emailDataResponse));
                lookUpResponse.users[0].email = emailDataResponse;
            }

            // If the email was registered and not verified yet, send the verification email
            if (user.emailVerified)
            {
                _logger?.LogInformation($"Email {user.email} is already verified.");
                return new FirebaseSendEmailResponse
                {
                    email = user.email,
                };
            } else
            {
                _logger?.LogInformation($"Sending verification email to {user.email}...");
                return await FirebaseApiHelper.SendVerifyEmailAsync(lookUpResponse.idToken, lookUpResponse.refreshToken, _logger);
            }
        }
    }
}
