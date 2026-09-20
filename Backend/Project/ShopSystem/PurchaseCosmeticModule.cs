using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.ShopSystem;

public class PurchaseCosmeticModule(ILogger<PurchaseCosmeticModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<PurchaseCosmeticModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(PurchaseCosmetic))]
    public async Task<string?> PurchaseCosmetic(IExecutionContext executionContext, string parametersEncryptedJson)
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

        if (!data.TryGetValue("cosmeticId", out var cosmeticIdObj) || cosmeticIdObj is not string cosmeticId || string.IsNullOrEmpty(cosmeticId))
            throw new ArgumentException("Cosmetic ID is missing or invalid.");

        _logger.LogInformation($"Player {executionContext.PlayerId} is attempting to purchase cosmetic with ID: {cosmeticId}");

        // Load available cosmetics from GameData
        var gameDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: true,
            CloudSaveProperties.Cosmetics.ToString());

        _logger.LogInformation($"Game Data Response: {JsonConvert.SerializeObject(gameDataResponse)}");

        // Get the game cosmetics data from the response
        var gameCosmetics = gameDataResponse?
            .FirstOrDefault(x => x?.key == CloudSaveProperties.Cosmetics.ToString())
            ?.value?.ToObject<GameCosmeticData[]>();

        if (gameCosmetics is null || gameCosmetics.Length == 0)
            throw new UGSException("No cosmetic data found in Game Data.");

        var selectedCosmetic = gameCosmetics.FirstOrDefault(c => c.id == cosmeticId && c.isAvailable);
        if (selectedCosmetic is null)
            throw new UGSException($"Cosmetic with ID '{cosmeticId}' not found or not available.");

        // Load player's owned cosmetics
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Cosmetics.ToString());

        var ownedCosmetics = playerDataResponse?
            .FirstOrDefault(x => x?.key == CloudSaveProperties.Cosmetics.ToString())
            ?.value?.ToObject<PlayerCosmeticData[]>() ?? Array.Empty<PlayerCosmeticData>();

        if (ownedCosmetics.Any(c => c.id == cosmeticId))
            throw new UGSException("Player already owns this cosmetic.");

        // Deduct currency 
        var (isSuccessful, newValue) = await UGSApiHelper.ConsumeCurrency(_gameApiClient, executionContext,
            selectedCosmetic.currency, selectedCosmetic.price);

        if (!isSuccessful)
        {
            _logger.LogWarning($"Failed to deduct currency for cosmetic purchase. Player ID: {executionContext.PlayerId}, Cosmetic ID: {cosmeticId}");
            return null; 
        }
        var newPurchase = new PlayerCosmeticData
        (
            id: cosmeticId,
            adquiredTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ammoutPaid: selectedCosmetic.price,
            costType: selectedCosmetic.currency,
            cosmeticPurchaseMethod: CosmeticPurchaseMethod.Shop
        );

        var updatedCosmetics = ownedCosmetics.Append(newPurchase).ToArray();

        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext,
            new()
            {
                [CloudSaveProperties.Cosmetics.ToString()] = updatedCosmetics
            });

        var response = new PurchaseCosmeticResponse(newPurchase, newValue);
        var responseJson = JsonConvert.SerializeObject(response);

        var derivedKey = SecurityHelper.DeriveKey(executionContext.PlayerId!, executionContext.AccessToken!);
        var derivedIv = SecurityHelper.DeriveIV(executionContext.PlayerId!, executionContext.AccessToken!);

        var encryptedResponse = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv));
        return encryptedResponse;
    }
}
