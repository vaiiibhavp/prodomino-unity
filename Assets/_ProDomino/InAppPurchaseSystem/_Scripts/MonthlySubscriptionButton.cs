using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.InAppPurchaseSystem
{
    [RequireComponent(typeof(Button))]
    public class MonthlySubscriptionButton : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text buttonLabel;

        private GameManager gameManager;
        private Button openPopUpButton;

        private MonthlySubscriptionPopUp monthlySubscriptionPopUp;
        protected MonthlySubscriptionPopUp MonthlySubscriptionPopUp => monthlySubscriptionPopUp = monthlySubscriptionPopUp != null 
            ? monthlySubscriptionPopUp 
            : FindFirstObjectByType<MonthlySubscriptionPopUp>();

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();

            openPopUpButton = GetComponent<Button>();
            openPopUpButton.onClick.AddListener(OpenMonthlySubscriptionPopUp);

            if (gameManager)
            {
                gameManager.HandleOnSignIn(OnSignedIn);
                gameManager.HandleOnSignOut(OnSignedOut);
            }

            if (!canvasGroup) 
            {
                Debug.LogError("Canvas group reference is missing, try to get from children");
                canvasGroup = GetComponentInChildren<CanvasGroup>();
            }

            // By default, turn off the button
            if (canvasGroup)
                canvasGroup.SetActive(false);
        }

        private void OnDestroy()
        {
            if (gameManager)
            {
                gameManager.UnHandleOnSignIn(OnSignedIn);
                gameManager.UnHandleOnSignOut(OnSignedOut);
            }
        }

        /// <summary>
        /// Configures the button label with localized text and price.
        /// </summary>
        /// <param name="localizedText"></param>
        private void ConfigureButtonLabel(string localizedText)
        {
            // Check for null references
            if (buttonLabel is null)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionButton)}: {nameof(buttonLabel)} is null");
                return;
            }

            // Check for null or empty localized text
            if (string.IsNullOrEmpty(localizedText))
            {
                Debug.LogError($"{nameof(MonthlySubscriptionPopUp)}: localizedText is null or empty");
                return;
            }

            // Get the IAP product and check for null
            var monthlySubscriptionProduct = gameManager.GameBackendConfigData?.purchaseConfig?.products
                ?.FirstOrDefault(x => x.productId == Consts.CollectionKeys.MonthlySubscriptionProductId);

            if (monthlySubscriptionProduct is null)
            {
                Debug.LogError($"{nameof(MonthlySubscriptionButton)}: monthlySubscriptionProduct is null");
                return;
            }

            // Replace the price placeholder with the actual price
            var keyToReplace = "{PRICE}";
            localizedText = localizedText.Replace(keyToReplace, monthlySubscriptionProduct.price);

            // Set the button label text
            buttonLabel.text = localizedText;
        }

        /// <summary>
        /// Opens the monthly subscription pop-up.
        /// </summary>
        private void OpenMonthlySubscriptionPopUp()
        {
            if (!MonthlySubscriptionPopUp)
            {
                Debug.LogWarning($"{nameof(MonthlySubscriptionButton)}: {nameof(MonthlySubscriptionPopUp)} is null");
                return;
            }

            MonthlySubscriptionPopUp.SetActive(true);
        }

        private async void OnSignedIn()
        {
            if (gameManager is null or { IsAuthenticatedAndVerified: false } 
                || gameManager.GameBackendConfigData is null 
                    or { purchaseConfig: null 
                    or { enabled: false } 
                    or { products: null 
                    or { Length: 0} } })
                return;

            try
            {
                // Localize the text of the button label
                var localizedText = await LocalizationHelper.Get(Consts.LocalizationKeys.MonthlySubscriptionButton);
                ConfigureButtonLabel(localizedText);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error fetching localized text: " + ex);
            }

            if (canvasGroup)
                canvasGroup.SetActive(true);
        }

        private void OnSignedOut()
        {
            if (canvasGroup)
                canvasGroup.SetActive(false);
        }
    }
}
