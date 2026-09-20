using Cysharp.Threading.Tasks;
using ProDomino.HandleProcessesSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using System.Threading.Tasks;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.InAppPurchaseSystem
{
    public class MonthlySubscriptionPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Button confirmButton, denyButton;

        private InAppPurchaseManager inAppPurchaseManager;
        private HandleProcessesController handleProcessesController;
        private bool isPurchaseInProgress = false;

        private void Awake()
        {
            inAppPurchaseManager = ServiceLocator.Instance.GetService<InAppPurchaseManager>();
            handleProcessesController = ServiceLocator.Instance.GetService<HandleProcessesController>();
        
            // Assign button listeners
            if (confirmButton)
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            else
                Debug.LogWarning("Confirm Button is not assigned.");

            if (denyButton)
                denyButton.onClick.AddListener(OnDenyButtonClicked);
            else
                Debug.LogWarning("Deny Button is not assigned.");
        }

        private void Start()
        {
            //IAPManager.purchaseStartedEvent += OnPurchaseStarted;
            //IAPManager.purchaseSucceededEvent += OnPurchaseSucceeded;
            //IAPManager.purchaseFailedEvent += OnPurchaseFailed;
        }

        /// <summary>
        /// Sets the active state of the pop-up.
        /// </summary>
        /// <param name="isActive"></param>
        public async void SetActive(bool isActive)
        {
            // Ensure the GameObject is active
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // Check for null CanvasGroup
            if (!canvasGroup)
            { 
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }

            // Configure the description label with localized text
            if (descriptionLabel)
            {
                try
                {
                    // Localize the text of the button label
                    var localizedText = await LocalizationHelper.Get(Consts.LocalizationKeys.MonthlySubscriptionConfirmation);
                    ConfigureDescriptionLabel(localizedText);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error fetching localized text: " + ex);
                }
            }

            canvasGroup.SetActive(isActive);
        }

        /// <summary>
        /// Configures the description label with localized text.
        /// </summary>
        private void ConfigureDescriptionLabel(string localizedText)
        {
            // Check for null InAppPurchaseManager
            if (!inAppPurchaseManager)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: InAppPurchaseManager service not found.");
                return;
            }

            // Check for null references
            if (descriptionLabel is null)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: {nameof(descriptionLabel)} is null");
                return;
            }

            // Check for null or empty localized text
            if (string.IsNullOrEmpty(localizedText))
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: localizedText is null or empty");
                return;
            }

            // Get the IAP product and check for null
            var monthlySubscriptionProduct = inAppPurchaseManager.PurchaseConfig
                ?.products
                ?.FirstOrDefault(x => x.productId == Consts.CollectionKeys.MonthlySubscriptionProductId);

            // Check for null monthly subscription product
            if (monthlySubscriptionProduct is null)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: monthlySubscriptionProduct is null");
                return;
            }

            // Replace the price placeholder with the actual price
            var monthlySubscriptionPrice = $"{monthlySubscriptionProduct.price} {monthlySubscriptionProduct.currency}";
            var keyToReplace = "{PRICE}";
            localizedText = localizedText.Replace(keyToReplace, monthlySubscriptionPrice);

            // Set the button label text
            descriptionLabel.text = localizedText;
        }


        #region Button Listeners
        /// <summary>
        /// Handles the confirm button click event.
        /// </summary>
        private async void OnConfirmButtonClicked()
        {
            // Check for null InAppPurchaseManager
            if (!inAppPurchaseManager)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: InAppPurchaseManager service not found.");
                return;
            }

            // Initiate the purchase for the monthly subscription product
            await inAppPurchaseManager.TryToPurchase(Consts.CollectionKeys.MonthlySubscriptionProductId);
        }

        /// <summary>
        /// Handles the deny button click event.
        /// </summary>
        private void OnDenyButtonClicked()
        {
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }

            canvasGroup.SetActive(false);
        }
        #endregion

        #region Purchase Callbacks
        /// <summary>
        /// Handles the purchase started event.
        /// </summary>
        private async void OnPurchaseStarted(string productId)
        {
            // Prevent multiple simultaneous purchases
            if (isPurchaseInProgress)
            { 
                Debug.LogWarning($"{nameof(MonthlySubscriptionPopUp)}: A purchase is already in progress.");
                return;
            }

            Debug.Log($"<color=#9465b8>{nameof(MonthlySubscriptionPopUp)}:</color> Purchase started for product ID: {productId}");
            isPurchaseInProgress = true;

            // If there is a HandleProcessesController, use it to show loading until the purchase is complete
            if (handleProcessesController)
                await handleProcessesController.HandleProcess
                    (taskFactory: () => UniTask.WaitUntil(() => !isPurchaseInProgress),
                    taskId: $"Purchasing Product: {productId}",
                    showLoading: true);
        }

        /// <summary>
        /// Handles the purchase succeeded event.
        /// </summary>
        private void OnPurchaseSucceeded(string response)
        {
            isPurchaseInProgress = false;

            Debug.Log($"<color=#9465b8>{nameof(MonthlySubscriptionPopUp)}:</color> Purchase succeeded with response: {response}");
        }

        /// <summary>
        /// Handles the purchase failed event.
        /// </summary>
        private void OnPurchaseFailed(string errorMessage)
        {
            isPurchaseInProgress = false;

            Debug.LogError($"<color=#9465b8>{nameof(MonthlySubscriptionPopUp)}:</color> Purchase failed with error: {errorMessage}");
        }
        #endregion
    }
}
