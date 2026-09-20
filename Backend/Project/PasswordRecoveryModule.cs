using Backend.MissionSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend;

public class PasswordRecoveryModule(ILogger<PasswordRecoveryModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<PasswordRecoveryModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Sends a password recovery email to the user to change it from firebase.<br></br><br></br>
    /// From unity, this will return no-exception if the email was sent properly.<br></br>
    /// UGS password will change if in the sign-in it detects that the password is different from the one in Firebase.<br></br>
    /// </summary>
    /// <param name="executionContext"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    [CloudCodeFunction(nameof(PasswordRecovery))]
    public async Task<bool> PasswordRecovery(IExecutionContext executionContext, string parametersEncryptedJson)
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

        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data. Provide email, password, and username.");

        // Check if the data dictionary contains the required keys and values
        if (!data.TryGetValue("email", out var emailObj) || emailObj is not string email || string.IsNullOrEmpty(email))
            throw new ArgumentException($"Email cannot be empty.");

        // Send the password reset email to the user
        try
        {
            await FirebaseApiHelper.SendResetPasswordEmailAsync(email);
            _logger?.LogInformation("Password recovery email sent to {Email}.", email);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Password recovery email could not be sent to {Email}.", email);
            return false;
        }
    }
}
