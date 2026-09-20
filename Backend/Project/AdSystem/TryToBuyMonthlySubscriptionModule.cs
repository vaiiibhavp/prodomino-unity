/*
    Note: the subscription payment is handled via firebase real-time database,<br></br>
    because the payment handler (paypal) requires direct communication with firebase to<br></br>
 */

//using HelperSharedLibrary;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using Unity.Services.CloudCode.Apis;
//using Unity.Services.CloudCode.Core;
//using static Backend.UGSBackend;
//using static Backend.UGSBackend.ContextData;

//namespace Backend.Authentication;

/// <summary>
/// Module to try to buy monthly subscription
/// </summary>
//public class TryToBuyMonthlySubscriptionModule
//{
//    private readonly ILogger<TryToBuyMonthlySubscriptionModule> _logger;
//    private readonly IGameApiClient _gameApiClient;

//    public TryToBuyMonthlySubscriptionModule(ILogger<TryToBuyMonthlySubscriptionModule> logger, IGameApiClient gameApiClient)
//    {
//        _logger = logger;
//        _gameApiClient = gameApiClient;
//    }

//    /// <summary>
//    /// Try to update the monthly subscription
//    /// </summary>
//    [CloudCodeFunction(nameof(TryToBuyMonthlySubscription))]
//    public async Task TryToBuyMonthlySubscription(IExecutionContext executionContext)
//    {
//        if (executionContext is null)
//            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

//        // Configure the context data according to the execution context
//        var contextData = new ContextData(executionContext);

//        // Check if the execution context is null
//        BackendHelper.ContextValidation(contextData);

//        // Load current player Firebase data
//        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
//            contextData, _gameApiClient, executionContext,
//            null, null, false,
//            CloudSaveProperties.Ads.ToString());

//        await TryToBuyMonthlySubscription(contextData, executionContext, playerDataResponse);
//    }

//    /// <summary>
//    /// Try to buy the monthly subscription
//    /// </summary>
//    private async Task TryToBuyMonthlySubscription(ContextData contextData, IExecutionContext executionContext,
//        ResponseData?[]? loadDataResponse)
//    {
//        var adData = loadDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.Ads.ToString())?.value?.ToObject<PlayerAdData>();
//        if (adData is null)
//        {
//            _logger.LogWarning("Failed to load ads data.");
//            return;
//        }

//        try
//        {
//            var analyticsData = await UGSApiHelper.TryToBuyMonthlySubscription(contextData, _gameApiClient, executionContext, adData);
//            if (analyticsData is null)
//            {
//                _logger.LogWarning("Failed to update monthly subscription data.");
//                return;
//            }

//            _logger.LogInformation("Monthly subscription data updated successfully.");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError($"Failed to update monthly subscription data. Exception: {ex.Message}");
//        }
//    }
//}
