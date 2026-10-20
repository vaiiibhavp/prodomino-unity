using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.PlayerPurchasesData.PlayerPurchaseReceiptData;
using static HelperSharedLibrary.PurchaseOrderResponse;

namespace ProDomino.InAppPurchaseSystem
{
    public class InAppPurchaseManager : SingleInstanceMonoBehaviour<InAppPurchaseManager>, IService
    {
        private GameManager gameManager;
        private AuthManager authManager;
        private PromptFadeController promptFadeController;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        public PlayerPurchasesData PlayerPurchasesData => gameManager?.PlayerPurchasesData;
        public PurchaseConfig PurchaseConfig => gameManager?.GameBackendConfigData?.purchaseConfig;

        public bool IsAlreadyInitialized => true;

        private string activeExternalOrderId;
        private bool isCurrentPurchasingDone;
        private string currentPurchaseFailReason;
        private Coroutine pollingPurchasesCoroutine;

        private MonthlySubscriptionPopUp monthlySubscriptionPopUp;
        private MonthlySubscriptionPopUp MonthlySubscriptionPopUp => monthlySubscriptionPopUp = monthlySubscriptionPopUp != null 
            ? monthlySubscriptionPopUp 
            : FindAnyObjectByType<MonthlySubscriptionPopUp>();

        [DllImport("__Internal")] private static extern void OpenPayPalWindow(string url, string objectName, string onPayPalWindowClosed);


        protected override void Awake()
        {
            base.Awake();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
        }

        /// <summary>
        /// Attemps to change the club data
        /// </summary>
        internal async UniTask TryToPurchase(string productId)
        {
            // Check if the required services are available
            if (!gameManager || !authManager)
            {
                Debug.LogError("GameManager or AuthManager service not found. Cannot proceed with purchase.");
                return;
            }

            // Check if the player is authenticated and verified
            if (!gameManager.IsAuthenticatedAndVerified)
            {
                Debug.LogWarning("Player is not authenticated and verified. Cannot proceed with purchase.");
                return;
            }

            // A previous PayPal payment is still being awaited in the background
            if (pollingPurchasesCoroutine is not null && !isCurrentPurchasingDone)
            {
                Debug.LogWarning("A purchase is already waiting for the PayPal result. Cannot proceed with purchase.");
                ShowPrompt($"You already have an active subscription pending process. Wait until is done");
                return;
            }

            // Check if the product ID is valid
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogWarning("Product ID is null or empty. Cannot proceed with purchase.");
                return;
            }

            // Check if the purchase configuration and products are available
            if (PurchaseConfig is null or { products: null or { Length: 0 } })
            {
                Debug.LogWarning("Purchase configuration is not available or has no products defined. Cannot proceed with purchase.");
                return;
            }

            // Try to find the product in the purchase configuration
            var productToPurchase = PurchaseConfig.products.FirstOrDefault(product => product.productId == productId);
            if (productToPurchase is null)
            {
                Debug.LogWarning($"Product with ID: {productId} not found in purchase configuration. Cannot proceed with purchase.");
                return;
            }

            // Check if the player already owns the product (for non-consumable products)
            if (PlayerPurchasesData is not null and { receipts: not null and { Count: > 0 } })
            {
                // Check if the player already owns a non-consumable product
                var recepitFound = PlayerPurchasesData.receipts.FirstOrDefault(receipt =>
                    receipt.productId == productId);

                // If the product is non-consumable and already owned, prevent repurchase
                if (recepitFound is not null && recepitFound.isSubscription)
                {
                    Debug.Log($"Receipt with order id {recepitFound.externalOrderId} found with status {recepitFound.status}");

                    if (recepitFound.status is PurchaseStatus.Completed)
                    { 
                        // Parse the purchase date and check if the subscription is still active
                        var purchaseDateTime = recepitFound.GetPurchasedAtUtcDateTime();
                        var daysConsumed = (DateTime.UtcNow - purchaseDateTime).TotalDays;
                        var isCurrentSubscriptionStillValid = daysConsumed <= productToPurchase.daysValidFor;

                        if (isCurrentSubscriptionStillValid)
                        {
                            var expirationTime = purchaseDateTime.AddDays(productToPurchase.daysValidFor);
                            var leftingHours = (DateTime.UtcNow - expirationTime).TotalHours;

                            Debug.LogWarning($"Player already has an active subscription for product ID: {productId}. Cannot proceed with purchase.");
                            ShowPrompt($"You already have an active subscription for this item. Its expires in <b>{Mathf.CeilToInt((float)leftingHours)} hours.</b>");
                            return;
                        }

                        Debug.Log($"Receipt with order id {recepitFound.externalOrderId} is expired");
                    } 
                    else
                    {
                        // Define TTL for pending receipts (in minutes)
                        var now = DateTimeOffset.UtcNow;
                        var ttlMinutes = gameManager.GameBackendConfigData.purchaseConfig.subscriptionPendingTtlMinutes; // Wait until the subscription pending TTL minutes have passed

                        var createdAt = recepitFound.GetCreatedAtUtcDateTime();
                        var minutesElapsed = (now - createdAt).TotalMinutes;

                        // If the pending receipt has exceeded the TTL, mark it as expired
                        if (minutesElapsed > ttlMinutes)
                        {
                            Debug.Log($"Expired pending purchase with order id {recepitFound.externalOrderId}. Could create a new one...");
                            ShowPrompt($"The pending process is expired. Creating a new one...");
                        } 
                        else
                        { 
                            Debug.LogWarning($"Player already has an active subscription pending process. Wait until is done");
                            ShowPrompt($"You already have an active subscription pending process. Wait until is done");
                            return;
                        }
                    }
                }
            }

            // Prepare the data to be sent to the backend
            var newData = new Dictionary<string, object>()
            {
                ["productId"] = productId
#if UNITY_EDITOR || DEBUG
                // In editor and debug builds, force the sandbox environment for testing
                , ["forcedEnvironment"] = "sandbox"
#endif
            };

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(newData);

            // Store the variables to use the trycat pattern to store the response
            var tryToRequestPurchaseSolicitude = default(string);
            var tryToRequestPurchaseResponse = default(PurchaseOrderResponse);

            try
            {
                // Use the backedn binding of the corresponding player
                tryToRequestPurchaseSolicitude = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.RequestPurchase(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.RequestPurchase),
                    showLoading: true,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                tryToRequestPurchaseResponse = authManager.DeserializeAndDecryptData<PurchaseOrderResponse>(tryToRequestPurchaseSolicitude);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to update club data: {ex.Message}");
            }

            // Check if the response is positive
            if (tryToRequestPurchaseResponse is not null and {
                state: PurchaseOrderState.Pending, 
                playerPurchaseReceiptData: not null,
                approvalUrl: not null and not ""
            })
            {
                Debug.Log($"Successfully sent purchase request for product ID: {productId}");

                // Register the id of the receipt to purchase
                activeExternalOrderId = tryToRequestPurchaseResponse.externalOrderId;

                // Main flag to inform the purchase still active.
                // Must be reset before the polling starts, otherwise a previous purchase's "done" state ends the new polling instantly.
                isCurrentPurchasingDone = false;
                currentPurchaseFailReason = null;

                // If there is a polling coroutine, stop it
                if (pollingPurchasesCoroutine is not null)
                {
                    StopCoroutine(pollingPurchasesCoroutine);
                    pollingPurchasesCoroutine = null;
                }

                // Using the approval URL, open the purchase window
#if UNITY_EDITOR
                Application.OpenURL(tryToRequestPurchaseResponse.approvalUrl);
#else
                OpenPayPalWindow(tryToRequestPurchaseResponse.approvalUrl, gameObject.name, nameof(OnPayPalWindowClosed));
#endif
                // Start a polling coroutine to check each few seconds if the purchases status is changed.
                // Also runs on WebGL as a fallback: the PayPal return page can lose window.opener, so its postMessage may never arrive.
                pollingPurchasesCoroutine = StartCoroutine(PollingPurchases());

                // Wait for the PayPal result in the background. The loading screen only covers the purchase request above:
                // the user completes (or abandons) the payment in an external window, which can take up to the pending TTL.
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: async () =>
                    {
                        var promptMessage = string.Empty;
                        try
                        {
                            // Wait until the purchased status is changed to done
                            await UniTask.WaitUntil(() => isCurrentPurchasingDone)
                                .Timeout(TimeSpan.FromMinutes(gameManager?.GameBackendConfigData?.purchaseConfig?.subscriptionPendingTtlMinutes ?? 180));

                            // Cancelled, blocked or rejected purchases end the wait too, but must not report success
                            if (currentPurchaseFailReason is not null)
                                throw new Exception(currentPurchaseFailReason);

                            // Turn off the opUp when the subscriptions is done
                            MonthlySubscriptionPopUp?.SetActive(false);

                            promptMessage = $"{productToPurchase.displayName} was purchase successfully";
                        }
                        catch (Exception ex)
                        {
                            // Indicates the purchase process if finally done (this is only called for error)
                            isCurrentPurchasingDone = true;

                            Debug.LogError($"Couldn't purchase the item: \n\n{ex.Message}");
                            promptMessage = $"Couldn't purchase the item: {productToPurchase.displayName}";
                        }
                        finally
                        {
                            // If there is a polling coroutine, stop it
                            if (pollingPurchasesCoroutine is not null)
                            {
                                StopCoroutine(pollingPurchasesCoroutine);
                                pollingPurchasesCoroutine = null;
                            }

                            // Show the feedback when the process is done
                            promptFadeController?.Fade(promptMessage);

                            // Refresh the purchases
                            await gameManager.RefreshRealtimeDatabasePurchasesData();
                        }
                    },
                    taskId: nameof(module.RequestPurchase),
                    showLoading: false,
                    shouldIgnoreTryAgainProcess: true,
                    shouldRetrySomeTimes: false,
                    isUsingTimeOut: false);

            } else
            {
                var failMessage = "Purchase request failed.";
                if (tryToRequestPurchaseResponse is null)
                {
                    Debug.LogWarning("Purchase request response is null.");
                    failMessage = "Purchase request failed: No response from server.";
                } 
                else if (tryToRequestPurchaseResponse.state is not PurchaseOrderState.Pending)
                {
                    Debug.LogWarning($"Purchase request failed with state: {tryToRequestPurchaseResponse.state}");
                    failMessage = $"Purchase request failed: {tryToRequestPurchaseResponse.state}";
                } 
                else if (tryToRequestPurchaseResponse.playerPurchaseReceiptData is null)
                {
                    Debug.LogWarning("Purchase request failed: Player purchase receipt data is null.");
                    failMessage = "Purchase request failed: Invalid receipt data.";
                } 
                else if (tryToRequestPurchaseResponse.approvalUrl is null or "")
                {
                    Debug.LogWarning("Purchase request failed: Approval URL is null or empty.");
                    failMessage = "Purchase request failed: Invalid approval URL.";
                }

                // Show an error prompt in the UI
                ShowPrompt(failMessage);
            }
        }

        /// <summary>
        /// Shows a prompt to the user indicating the result of the purchase attempt
        /// </summary>
        private void ShowPrompt(string message)
        {
            // Check if the PromptFadeController service is available
            if (!promptFadeController)
            {
                Debug.LogWarning("PromptFadeController service not found. Cannot show prompt.");
                return;
            }

            // Use the PromptFadeController to show the message
            promptFadeController.Fade(message);
        }

        /// <summary>
        /// Polling use to refresh RTDB purchases data each 5 seconds<br></br>
        /// Ends when the active receipt leaves the Pending status or the purchase is flagged as done elsewhere
        /// </summary>
        private IEnumerator PollingPurchases()
        {
            var isReceiptResolved = false;

            try
            {
                while (!isCurrentPurchasingDone)
                {
                    yield return new WaitForSeconds(5);

                    // Wait for the refresh to really finish (yielding the UniTask struct directly only skips one frame,
                    // while the purchases data is still reset to null)
                    // Silent refresh: a background poll must not flash the loading screen every few seconds
                    yield return gameManager.RefreshRealtimeDatabasePurchasesData(showLoading: false).ToCoroutine();

                    // Search the receipt with the same external order id that the active one
                    var currentPurchaseReceipt = PlayerPurchasesData?.receipts
                        ?.FirstOrDefault(x => x.externalOrderId == activeExternalOrderId);

                    // Check if the active purchase receipt still pending or not
                    if (currentPurchaseReceipt is not null and { status: not PurchaseStatus.Pending })
                    {
                        Debug.Log($"Recipt with external order id finish with a status of {currentPurchaseReceipt.status.ToString()}");

                        if (currentPurchaseReceipt.status is not PurchaseStatus.Completed)
                            currentPurchaseFailReason ??= $"Purchase finished with status {currentPurchaseReceipt.status}";

                        isReceiptResolved = true;
                        break;
                    }
                }
            }
            finally
            {
                // The polling died before resolving the receipt (exception), so the purchase can't be reported as successful
                if (!isReceiptResolved && !isCurrentPurchasingDone)
                    currentPurchaseFailReason ??= "Purchase status polling stopped unexpectedly";

                // Always release the waiting loading screen
                isCurrentPurchasingDone = true;
            }
        }

        /// <summary>
        /// Event called from external library when the paypal window is closed<br></br>
        /// Used only in Web Gl
        /// </summary>
        /// <param name="result"></param>
        public void OnPayPalWindowClosed(string result)
        {
            Debug.Log($"PayPal window closed with result: {result}");

            if (result is "PAYPAL_CANCEL" or "PAYPAL_BLOCKED")
                currentPurchaseFailReason ??= result == "PAYPAL_CANCEL"
                    ? "Purchase was cancelled"
                    : "PayPal window could not be opened (popup blocked)";

            isCurrentPurchasingDone = true;
        }
    }
}
