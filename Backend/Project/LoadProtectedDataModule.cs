using HelperSharedLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Backend;

public class LoadProtectedDataModule
{
    private readonly IGameApiClient _gameApiClient;

    public LoadProtectedDataModule(IGameApiClient gameApiClient)
    {
        _gameApiClient = gameApiClient;
    }

    /// <summary>
    /// Function that Loads protected data in the database from Cloud Code server api<br></br>
    /// </summary>
    /// <param name="executionContext"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    [CloudCodeFunction(nameof(LoadProtectedData))]
    public async Task<string> LoadProtectedData(IExecutionContext executionContext, string parametersEncryptedJson)
    {
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Get the data from the encrypted parameters JSON
        var data = BackendHelper.ValidateEncriptedParameters
            (parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate entry data
        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data. Provide data");

        if (!data.TryGetValue("keys", out var keysObj))
            throw new ArgumentException("Keys field is missing.");

        var keys = default(string[]);
        if (keysObj is IEnumerable<string> keysIEnumerable && keysIEnumerable.Any())
            keys = keysIEnumerable?.ToArray();

        else if (keysObj is JArray keysJArray && keysJArray.Count > 0)
            keys = keysJArray?.ToObject<string[]>();

        // Check if the keys are null or empty
        if (keys is null || keys.Length == 0)
            throw new ArgumentException("Keys are empty.");

        try
        {
            var loadResponseData = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                keysID: [.. keys]).ConfigureAwait(false);
            if (loadResponseData is null)
                throw new Exception("Failed to load protected data.");

            var playerId = executionContext.PlayerId ?? string.Empty;
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            var responseDataJson = JsonConvert.SerializeObject(loadResponseData);
            var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseDataJson, derivedKey, derivedIv));
            return encryptedDataJson;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to Load protected data: {ex.Message}");
        }

    }
}
