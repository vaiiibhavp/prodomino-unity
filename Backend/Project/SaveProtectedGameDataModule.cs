using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend;

public class SaveProtectedGameDataModule(ILogger<SaveProtectedGameDataModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<SaveProtectedGameDataModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Function that saves protected data in the database from Cloud Code server api<br></br>
    /// </summary>
    /// <param name="executionContext"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    [CloudCodeFunction(nameof(SaveProtectedGameData))]
    public async Task SaveProtectedGameData(IExecutionContext executionContext, string parametersEncryptedJson)
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
            throw new ArgumentException("Invalid input data. Provide data");

        try
        {
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save protected data: {ex.Message}");
        }
    }
}
