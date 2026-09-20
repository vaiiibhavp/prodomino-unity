using Backend.MissionSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.ExceptionHelper;
using static HelperSharedLibrary.PlayerPurchasesData;
using static HelperSharedLibrary.PlayerPurchasesData.PlayerPurchaseReceiptData;
using static HelperSharedLibrary.PurchaseOrderResponse;

namespace Backend.IAPSystem;

public class RequestPurchaseModule(ILogger<RequestPurchaseModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<RequestPurchaseModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(RequestPurchase))]
    public async Task<string> RequestPurchase(
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

        // Get the data from the encrypted parameters JSON
        var data = BackendHelper.ValidateEncriptedParameters
            (parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data.");

        // Try to get product ID. If missing or invalid, throw error
        if (!data.TryGetValue("productId", out var productIdObj)
            || productIdObj is not string productId
            || string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Invalid or missing productId.");

        // Try to get optional environment override
        if (!data.TryGetValue("forcedEnvironment", out var environmentObj)
            || environmentObj is not string environment)
            environment = executionContext.EnvironmentName;

        _logger.LogInformation(
            "Purchase request received. PlayerId={PlayerId}, ProductId={productId}, Env={Env}",
            executionContext.PlayerId,
            productId,
            environment);

        // Load Game Data
        var gameDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Config.ToString());

        // Validate Game Data config, specifically purchase config and its products
        var configData = gameDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())?.value?.ToObject<ConfigData>();
        if (configData is null 
            or { purchaseConfig: null or { products: null or { Length: 0 } }}
            or { adsConfig: null })
            throw new UGSException("Game Data configuration invalid.");

        // Prepare response object
        var purchaseOrderResponse = new PurchaseOrderResponse();

        // Get the purchase config reference 
        var purchaseConfig = configData.purchaseConfig;

        // Validate that purchase system is enabled
        if (!purchaseConfig.enabled)
        {
            purchaseOrderResponse = BuildBlockedResponse(
                PurchaseOrderReason.PurchaseSystemDisabled,
                "Purchase system is disabled.",
                null);

            // Target to encrypt response
            return EncryptResponse(executionContext, purchaseOrderResponse);
        }

        // Try to find the product in the config
        var productToPurchase = purchaseConfig.products.FirstOrDefault(p => p.productId == productId);
        if (productToPurchase is null)
        {
            purchaseOrderResponse = BuildBlockedResponse(
                PurchaseOrderReason.ProductNotFound,
                $"Product '{productId}' not found.",
                null);

            // Target to encrypt response
            return EncryptResponse(executionContext, purchaseOrderResponse);
        }

        // Check if the product is active
        else if (!productToPurchase.active)
        {
            purchaseOrderResponse = BuildBlockedResponse(
                PurchaseOrderReason.ProductDisabled,
                $"Product '{productId}' is disabled.",
                null);

            // Target to encrypt response
            return EncryptResponse(executionContext, purchaseOrderResponse);
        }

        // Prepare keys for loading player data
        var purchaseKey = CloudSaveProperties.Purchase.ToString();

        // Load current player data
        var playerPurchasesData = await FirebaseApiHelper.GetPlayerPurchaseDataAsync
            (executionContext, _gameApiClient,
            unityUserId: executionContext.PlayerId!,
            _logger);

        // Check for expired pending receipts and update their status if necessary
        playerPurchasesData = await CheckForExpiredReceipts(executionContext, purchaseKey, purchaseConfig, playerPurchasesData);

        // Prepare variable to hold any previous purchase receipt for the product
        var purchasedProductReceipt = default(PlayerPurchaseReceiptData);

        // Check if the player has already purchased some products
        if (playerPurchasesData is not null and { receipts: not null and { Count: > 0 }})
        { 
            var isSubscriptionProduct = productToPurchase.isSubscription;
            purchasedProductReceipt = playerPurchasesData
                .receipts
                .FirstOrDefault(r => r.productId == productId);

            // Check if the product exists in the player purchases
            if (purchasedProductReceipt is not null)
                // If non-subscription product was already purchased, block repurchase
                if (purchasedProductReceipt.status is PurchaseStatus.Completed)
                    // If non-subscription product, block repurchase
                    if (!isSubscriptionProduct)
                    {
                        purchaseOrderResponse = BuildBlockedResponse(
                            PurchaseOrderReason.AlreadyPurchased,
                            $"Product '{productId}' has already been purchased.",
                            purchasedProductReceipt);

                        // Target to encrypt response
                        return EncryptResponse(executionContext, purchaseOrderResponse);
                    }

                    // If subscription product, check if expired
                    else
                    {
                        // Check if subscription is still active
                        var currentTime = DateTimeOffset.UtcNow;
                        var purchaseTime = purchasedProductReceipt.GetPurchasedAtUtcDateTime();
                        var daysConsumed = (currentTime - purchaseTime).TotalDays;

                        // If the days consumed are less than the subscription days, block repurchase
                        if (daysConsumed < productToPurchase.daysValidFor)
                        {
                            purchaseOrderResponse = BuildBlockedResponse(
                                PurchaseOrderReason.SubscriptionStillActive,
                                $"Subscription product '{productId}' is still active.",
                                purchasedProductReceipt);

                            // Target to encrypt response
                            return EncryptResponse(executionContext, purchaseOrderResponse);
                        }

                        // Continue with repurchase if subscription has expired
                        _logger.LogInformation(
                            "Subscription product '{ProductId}' has expired. Allowing repurchase.",
                            productId);
                    }

                // If purchase is pending, block repurchase
                else if (purchasedProductReceipt.status is PurchaseStatus.Pending)
                {
                    purchaseOrderResponse = BuildBlockedResponse(
                        PurchaseOrderReason.PurchasePending,
                        $"Product '{productId}' purchase is still pending.",
                        purchasedProductReceipt);

                    // Target to encrypt response
                    return EncryptResponse(executionContext, purchaseOrderResponse);
                }

            // If the product was not found in the player purchases, log info
            else
                _logger.LogInformation(
                    "Player has no previous purchase record for product '{ProductId}'. Proceeding with purchase.",
                    productId);
            
        }

        // If the player has no previous purchases, log info
        else
            _logger.LogInformation(
                "Player has no previous purchases. Proceeding with purchase of product '{ProductId}'.",
                productId);


        // Define a default firebase response that will be populated by the Firebase Function
        var providerOrderResponse = default(FirebaseApiHelper.ProviderOrderResponse);

        try
        {
            _logger.LogInformation("Creating purchase order for product '{ProductId}', 'purchaseKey={PurchaseKey}', env={Env}'",
                productId, purchaseKey, environment);

            // Call Firebase Functions (helper encapsulates HTTP + auth)
            providerOrderResponse = await FirebaseApiHelper.CreatePurchaseOrder
                (productId: productToPurchase.productId,
                price: productToPurchase.price,
                currency: productToPurchase.currency,
                environment: environment,
                customId: executionContext.PlayerId!,
                gameApiClient: _gameApiClient,
                executionContext: executionContext,
                logger: _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order");

            // Build error response
            purchaseOrderResponse = new PurchaseOrderResponse
            {
                state = PurchaseOrderState.Error,
                reason = PurchaseOrderReason.ProviderError,
                message = "Unable to create purchase order at this time."
            };

            // Target to encrypt response
            return EncryptResponse(executionContext, purchaseOrderResponse);
        }

        // Check if firebase response is valid
        if (providerOrderResponse != null)
        {
            _logger.LogInformation(
                "Purchase order created. PlayerId={PlayerId}, ProductId={ProductId}, OrderId={OrderId}",
                executionContext.PlayerId,
                productId,
                providerOrderResponse.externalOrderId);

            // Create a new purchase receipt for the product (this wiil be send to client as response)
            purchasedProductReceipt = BuildPurchaseReceiptData
                (providerOrderResponse.externalOrderId, // Got from provider response
                productToPurchase.productId,
                productToPurchase.price,
                productToPurchase.currency,
                productToPurchase.isSubscription,
                purchaseConfig.paymentProvider);

            // Create patch data for the new purchase receipt (this will be saved in firebase realtime db)
            var receiptDataRTDBFormat = ParsePurchaseReceiptData(purchasedProductReceipt);

            // If the response contains approval URL and external order ID, update the purchase receipt,
            // register it player purchases data and save it in Cloud Save
            try
            {
                _logger.LogInformation($"Trying to create (put) data in the path: " +
                    $"\n\n'purchases/{executionContext.PlayerId}/{providerOrderResponse.externalOrderId}'" +
                    $"\n\nData to put:\n{receiptDataRTDBFormat}");

                // Save updated player match data
                await FirebaseApiHelper.PutPurchaseReceiptAsync
                    (executionContext, _gameApiClient,
                    executionContext.PlayerId!,
                    providerOrderResponse.externalOrderId,
                    receiptDataRTDBFormat,
                    _logger);

                _logger.LogInformation("Player purchase data created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating (put) player purchase data:\n\n{ex.Message}");
            }

            // Build success response
            purchaseOrderResponse = new PurchaseOrderResponse
            {
                state = PurchaseOrderState.Pending,
                reason = PurchaseOrderReason.PurchasePending,
                message = "Purchase order created successfully.",
                playerPurchaseReceiptData = purchasedProductReceipt,
                approvalUrl = providerOrderResponse.approvalUrl,
                externalOrderId = providerOrderResponse.externalOrderId
            };
        }

        // Firebase response is invalid
        else
        {
            _logger.LogError("Invalid response from purchase provider.");

            // Build error response
            purchaseOrderResponse = new PurchaseOrderResponse
            {
                state = PurchaseOrderState.Error,
                reason = PurchaseOrderReason.ProviderError,
                message = "Invalid response from purchase provider."
            };
        }

        // Target to encrypt response
        return EncryptResponse(executionContext, purchaseOrderResponse);
    }

    /// <summary>
    /// Encrypts the purchase order response using derived key and IV from the execution context.
    /// </summary>
    private string EncryptResponse(
        IExecutionContext executionContext,
        PurchaseOrderResponse response)
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

    // Helper to build blocked purchase response
    private PurchaseOrderResponse BuildBlockedResponse(
        PurchaseOrderReason reason,
        string message,
        PlayerPurchaseReceiptData? playerPurchaseReceiptData)
    {
        return new PurchaseOrderResponse
        {
            state = reason switch
            {
                PurchaseOrderReason.PurchasePending => PurchaseOrderState.Pending,
                PurchaseOrderReason.AlreadyPurchased => PurchaseOrderState.Completed,
                _ => PurchaseOrderState.Blocked
            },
            reason = reason,
            message = message,
            playerPurchaseReceiptData = playerPurchaseReceiptData
        };
    }

    /// <summary>
    /// Builds a pending purchase receipt data object.
    /// </summary>
    private PlayerPurchaseReceiptData BuildPurchaseReceiptData
        (string externalOrderId, string productId, string price, string currency, bool isSubscription, PaymentProvider provider)
    {
        // Build pending purchase receipt data
        return new PlayerPurchaseReceiptData
        {
            externalOrderId = externalOrderId,
            productId = productId,
            price = price,
            currency = currency,
            status = PurchaseStatus.Pending,
            isSubscription = isSubscription,
            createdAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            provider = provider
        };
    }

    /// <summary>
    /// Builds patch data for a pending purchase receipt.
    /// Intended to be used with PATCH (partial update).
    /// </summary>
    private object ParsePurchaseReceiptData(PlayerPurchaseReceiptData playerPurchaseReceiptData)
    {
        // Build and return patch data object only with fields to update
        return new
        {
            playerPurchaseReceiptData.externalOrderId, // Got from provider response
            playerPurchaseReceiptData.productId, // Internal product identifier
            playerPurchaseReceiptData.price, // Got from product config
            playerPurchaseReceiptData.currency, // Got from product config
            playerPurchaseReceiptData.isSubscription, // Got from product config
            playerPurchaseReceiptData.status, // Set status to Pending
            playerPurchaseReceiptData.provider, // Payment provider (got from purchase config)
            playerPurchaseReceiptData.createdAtUtc // Set to current UTC time
        };
    }

    /// <summary>
    /// Checks for expired pending receipts and updates their status to Expired if they exceed the TTL.
    /// </summary>
    private async Task<PlayerPurchasesData?> CheckForExpiredReceipts
        (IExecutionContext executionContext,
        string purchaseKey,
        PurchaseConfig purchaseConfig,
        PlayerPurchasesData? playerPurchasesData)
    {
        // Validate input data
        if (playerPurchasesData is null or { receipts: null or { Count: 0 } })
            return playerPurchasesData;

        // Define TTL for pending receipts (in minutes)
        var now = DateTimeOffset.UtcNow;
        var ttlMinutes = purchaseConfig.subscriptionPendingTtlMinutes; // Wait until the subscription pending TTL minutes have passed

        var tasks = new List<Task>();

        foreach (var receipt in playerPurchasesData.receipts
                 .Where(r => r.status == PurchaseStatus.Pending))
        {
            var createdAt = receipt.GetCreatedAtUtcDateTime();
            var minutesElapsed = (now - createdAt).TotalMinutes;

            // If the pending receipt has exceeded the TTL, mark it as expired
            if (minutesElapsed > ttlMinutes)
            {
                receipt.status = PurchaseStatus.Expired;

                _logger.LogInformation(
                    "Expired pending purchase. PlayerId={PlayerId}, ProductId={ProductId}, OrderId={OrderId}",
                    executionContext.PlayerId,
                    receipt.productId,
                    receipt.externalOrderId);

                var receiptDataRTDBFormat = ParsePurchaseReceiptData(receipt);

                // Register the changes to be done later
                tasks.Add(
                    // Patch the player receipt
                    FirebaseApiHelper.PutPurchaseReceiptAsync
                        (executionContext, _gameApiClient,
                        executionContext.PlayerId!,
                        receipt.externalOrderId,
                        receiptDataRTDBFormat,
                        _logger)
                );
            }
        }

        // Wait until all the updates are done
        await Task.WhenAll(tasks);

        // Return updated player purchases data
        return playerPurchasesData;
    }
}
