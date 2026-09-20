using Cysharp.Threading.Tasks;
using ProDomino.GameSystem;
using ProDomino.InAppPurchaseSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.PlayerPurchasesData.PlayerPurchaseReceiptData;

namespace ProDomino.AdSystem
{
    /// <summary>
    /// Manages ad display logic, including interstitial and rewarded ads, handling platform checks, subscription
    /// validation, and ad interval enforcement.
    /// </summary>
    public class AdManager : SingleInstanceMonoBehaviour<AdManager>, IService
    {
        private GameManager gameManager;
        private InAppPurchaseManager inAppPurchaseManager;
        private Action onInterstitialClosed;
        private Action<bool> onRewardedClosed;
        public bool IsAlreadyInitialized => true;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        [DllImport("__Internal")] private static extern void ShowInterstitial(string objectName, string onInterstitialClosed);
        [DllImport("__Internal")] private static extern void ShowRewarded(string objectName, string onRewardedClosed);

        protected override void Awake()
        {
            base.Awake();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            inAppPurchaseManager = ServiceLocator.Instance.GetService<InAppPurchaseManager>();
        }

        /// <summary>
        /// Displays an interstitial ad if platform and player conditions allow, optionally considering ad interval
        /// timing.
        /// </summary>
        /// <param name="onInterstitialClosed">Callback invoked when the interstitial ad is closed.</param>
        /// <param name="isConsideringIntervals">Indicates whether to enforce interval checks before showing the ad.</param>
        public void ShowInterstitialAd(Action onInterstitialClosed = null, bool isConsideringIntervals = false)
        {
            if (!gameManager)
            {
                Debug.LogWarning("[AdManager] GameManager service not found. Cannot show interstitial ad.");
                return;
            }

            // Check if the platform supports JSlib (e.g., WebGL)
            if (GameManager.IsValidPlatformToUseJSlib())
            {
                // Check if the player has an active subscription
                if (ValidateIfSubscriptionIsActive())
                {
                    Debug.Log("[AdManager] Player has an active subscription. Skipping interstitial ad.");
                    return;
                }

                // Check if ads are temporarily disabled for the player
                if ((DateTime.UtcNow - gameManager.PlayerAdData?.lastTimeAdDisabled)?.TotalSeconds <= (gameManager.GameBackendConfigData?.adsConfig?.secondsDisabled ?? 1200))
                {
                    Debug.Log("[AdManager] Ads are temporarily disabled for the player. Skipping interstitial ad.");
                    return;
                }

                // If considering intervals, check if enough time has passed since the last ad was shown
                if (isConsideringIntervals && (DateTime.UtcNow - gameManager.PlayerAdData?.lastTimeAdSaw)?.TotalSeconds <= (gameManager.GameBackendConfigData?.adsConfig?.secondsOmmited ?? 120))
                {
                    Debug.Log("[AdManager] Not enough time has passed since the last ad was shown. Skipping interstitial ad.");
                    return;
                }

                Debug.Log("[AdManager] Showing interstitial ad...");

                this.onInterstitialClosed = onInterstitialClosed;
                ShowInterstitial(gameObject.name, nameof(OnInterstitialPlayed));
            } 
            else
            { 
                Debug.LogWarning("[AdManager] Interstitial ads are only available on WebGL platform.");
                onInterstitialClosed?.Invoke();
            }
        }

        /// <summary>
        /// Calls the rewarded ad display function.<br></br>
        /// Those functions are implemented in JSlib for WebGL builds.
        /// </summary>
        /// <param name="onRewardedClosed">Callback when the rewarded ad is closed. The bool parameter indicates if the ad was fully watched.</param>
        public void ShowRewardedAd(Action<bool> onRewardedClosed = null)
        {
            if (!gameManager)
            {
                Debug.LogWarning("[AdManager] GameManager service not found. Cannot show rewarded ad.");
                return;
            }

            // Check if the platform supports JSlib (e.g., WebGL)
            if (GameManager.IsValidPlatformToUseJSlib())
            {
                // If user has subscription, skip ads
                if (ValidateIfSubscriptionIsActive())
                {
                    Debug.Log("[AdManager] Player has an active subscription. Skipping rewarded ad.");
                    return;
                }

                // Check if ads are temporarily disabled for the player
                if ((DateTime.UtcNow - gameManager.PlayerAdData?.lastTimeAdDisabled)?.TotalSeconds <= (gameManager.GameBackendConfigData.adsConfig?.secondsDisabled ?? 1200))
                {
                    Debug.Log("[AdManager] Ads are temporarily disabled for the player. Skipping interstitial ad.");
                    return;
                }

                Debug.Log("[AdManager] Showing rewarded ad...");

                this.onRewardedClosed = onRewardedClosed;
                ShowRewarded(gameObject.name, nameof(OnRewardedClosed));
            }
            else
            { 
                Debug.LogWarning("[AdManager] Rewarded ads are only available on WebGL platform.");
                onRewardedClosed?.Invoke(true);
            }
        }

        /// <summary>
        /// Using the purchase data, validate if the player has an active subscription
        /// </summary>
        private bool ValidateIfSubscriptionIsActive()
        {
            if (!gameManager || !inAppPurchaseManager)
            {
                Debug.LogWarning("[AdManager] GameManager or InAppPurchaseManager service not found. Cannot validate subscription status.");
                return false;
            }

            var subscriptionProductId = Consts.CollectionKeys.MonthlySubscriptionProductId;
            var purchaseConfig = gameManager.GameBackendConfigData?.purchaseConfig;

            // Check if the purchase configuration and products are available
            if (purchaseConfig is null or { products: null or { Length: 0 } })
            {
                Debug.LogWarning("[AdManager] Purchase configuration is not available or has no products defined. Cannot proceed with purchase.");
                return false;
            }

            // Try to find the product in the purchase configuration
            var monthlySubscriptionProduct = purchaseConfig.products.FirstOrDefault(product => product.productId == subscriptionProductId);
            if (monthlySubscriptionProduct is null)
            {
                Debug.LogWarning($"[AdManager] Product with ID: {subscriptionProductId} not found in purchase configuration. Cannot proceed with purchase.");
                return false;
            } 
            else if (!monthlySubscriptionProduct.isSubscription)
            {
                Debug.LogWarning($"[AdManager] Product with ID: {subscriptionProductId} is not marked as a subscription. Cannot proceed with purchase.");
                return false;
            }

            // Try to find the player's purchase data for the subscription product
            var monthlySubscriptionPurchaseData = inAppPurchaseManager.PlayerPurchasesData
                ?.receipts?
                .FirstOrDefault(purchase => purchase.productId == subscriptionProductId);

            // Check if the player owns the subscription product
            if (monthlySubscriptionPurchaseData is null)
            {
                Debug.LogWarning($"[AdManager] Player does not own the subscription product with ID: {subscriptionProductId}.");
                return false;
            }
            else if (monthlySubscriptionPurchaseData.status is not PurchaseStatus.Completed)
            {
                Debug.LogWarning($"[AdManager] Player's subscription product with ID: {subscriptionProductId} is not completed. Current status: {monthlySubscriptionPurchaseData.status}.");
                return false;
            }

            // Parse the purchase date and check if the subscription is still active
            var purchaseDateTime = monthlySubscriptionPurchaseData.GetPurchasedAtUtcDateTime();
            var isCurrentSubscriptionStillValid = (DateTime.UtcNow - purchaseDateTime).TotalDays <= monthlySubscriptionProduct.daysValidFor;

            // Check if the player has an active subscription
            if (isCurrentSubscriptionStillValid)
            {
                Debug.Log("[AdManager] Player has an active subscription.");
                return true;
            }
            return true;
        }

        /// <summary>
        /// Callback when the interstitial ad attempt finished
        /// </summary>
        /// <param name="wasShownProperlyStr">String indicating if the ad was shown properly ("true" or "false").</param>
        private async void OnInterstitialPlayed(string wasShownProperlyStr)
        {
            // Parse the string result to a boolean
            var wasShownProperly = wasShownProperlyStr is "true";

            // If the ad wasn't shown properly, exit early
            if (onInterstitialClosed is null)
            {
                Debug.LogWarning("[AdManager] No interstitial closed callback registered.");
                return;
            }

            // Register that the player saw an ad
            if (wasShownProperly)
            { 
                Debug.Log("[AdManager] Interstitial ad was shown properly. Proceeding to register ad view.");

                // Register that the player saw an ad
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToRegisterLastAdSaw().AsUniTask(),
                    taskId: nameof(module.TryToRegisterLastAdSaw),
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: false);
            }

            Debug.Log("[AdManager] Interstitial ad closed.");
            onInterstitialClosed.Invoke();
        }

        /// <summary>
        /// Handles the closure of a rewarded ad, processes ad completion, and triggers related actions.
        /// </summary>
        /// <param name="result">Indicates whether the rewarded ad was completed ('true') or not.</param>
        private async void OnRewardedClosed(string result)
        {
            var completed = result is "true";

            // Register that the player saw an ad
            if (completed)
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToDisableAdsTemporarily().AsUniTask(),
                    taskId: nameof(module.TryToDisableAdsTemporarily),
                    shouldIgnoreTryAgainProcess: true,
                    showLoading: false);

            Debug.Log($"[AdManager] Rewarded ad closed. Was Completed: {completed}");
            onRewardedClosed?.Invoke(completed);
        }
    }
}