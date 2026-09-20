using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using Newtonsoft.Json;
using System;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Shop
{ 
    internal class ShopElement : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Image elementImage;
        [SerializeField] private CustomButtonUI openConfirmationPopUpButton;
        [SerializeField] private UnityEvent onPurchaseSuccess;
        [SerializeField] private Color grayedColor = Color.gray;
        [SerializeField] private Image[] imagesToAvoidGrayedOut;

        private AsyncFuncHandler<PurchaseCosmeticResponse, string> purchaseShopElement;
        private Action<ShopElement> openConfirmationPopUp;
        internal bool IsAlreadyPurchased { get; private set; }
        internal GameCosmeticData GameCosmeticData { get; private set; }
        internal PlayerCosmeticData PlayerCosmeticData { get; private set; }

        private void Awake()
        {
            if (openConfirmationPopUpButton)
                openConfirmationPopUpButton.onClick.AddListener(OpenConfirmationPopUp);
            else
                Debug.LogWarning("Open confirmation pop-up button is not assigned in the ShopElement");
        }

        internal void Initialize
            (GameCosmeticData gameCosmeticData, 
            PlayerCosmeticData playerCosmeticData, 
            Sprite elementSprite,
            AsyncFuncHandler<PurchaseCosmeticResponse, string> purchaseShopElement,
            Action<ShopElement> openConfirmationPopUp)
        { 
            GameCosmeticData = gameCosmeticData;
            PlayerCosmeticData = playerCosmeticData;

            this.purchaseShopElement = purchaseShopElement;
            this.openConfirmationPopUp = openConfirmationPopUp;

            if (GameCosmeticData is null)
            {
                Debug.LogWarning("Game cosmetic data is null");
                return;
            }

            // Set the cost label if available
            if (costLabel)
                costLabel.text = GameCosmeticData.price.ToString();
            else
                Debug.LogWarning("Cost label is not assigned in the ShopElement");

            // Set the element image if available
            if (elementImage && elementSprite is not null)
                elementImage.sprite = elementSprite;
            else
                Debug.LogWarning("Element image or getElementImage function is not assigned");

            // If the PlayerCosmeticData is not null when the ShopElement is initializing, it means the cosmetic is already purchased
            SetPurchaseBlockability(PlayerCosmeticData is not null);
        }

        internal void SetPurchaseBlockability(bool isAlreadyPurchased)
        {
            IsAlreadyPurchased = isAlreadyPurchased;
            SetAccesibility(!isAlreadyPurchased);

            foreach (var image in GetComponentsInChildren<Image>())
                if (imagesToAvoidGrayedOut is null || !imagesToAvoidGrayedOut.Contains(image))
                    image.color = isAlreadyPurchased ? grayedColor : Color.white;

            if (isAlreadyPurchased)
            {
                onPurchaseSuccess?.Invoke();

                if (costLabel)
                    costLabel.text = "Purchased";
                else
                    Debug.LogWarning("Cost label is not assigned in the ShopElement");
            } 
        }

        private void OpenConfirmationPopUp()
        {
            if (openConfirmationPopUp is null)
            {
                Debug.LogWarning("Open confirmation pop-up action is not assigned in the ShopElement");
                return;
            }

            openConfirmationPopUp.Invoke(this);
        }

        internal async UniTask<PurchaseCosmeticResponse> Purchase()
        {
            if (IsAlreadyPurchased)
            {
                // Make sure the element is not purchased again
                SetPurchaseBlockability(true);
                return default;
            }

            if (GameCosmeticData is null)
            {
                Debug.LogWarning("Game cosmetic data is null, cannot purchase");
                return default;
            }

            if (purchaseShopElement is not null)
            {
                var purchaseResponse = await (purchaseShopElement?.Invoke(GameCosmeticData.id) ?? default);
                if (purchaseResponse is not null and { cosmeticPurchased: not null })
                {
                    SetPurchaseBlockability(true);
                    Debug.Log($"Purchase successful for {GameCosmeticData.id}:\n\nReceipt:\n{JsonConvert.SerializeObject(purchaseResponse.cosmeticPurchased, Formatting.Indented)}");

                    return purchaseResponse;
                } 
                else
                    Debug.LogWarning($"Purchase failed for {GameCosmeticData.id}");
            }

            SetPurchaseBlockability(false);
            return default;
        }

        internal void SetActive(bool isActive)
        {
            if (!gameObject)
                return;

            gameObject.SetActive(isActive);
        }

        internal void SetAccesibility(bool isAccesible)
        {
            if (!canvasGroup)
                return;

            canvasGroup.SetActive(isAccesible, isSettingAlpha: false);
        }

        internal Sprite GetElementImage()
        {
            if (!elementImage)
            { 
                Debug.LogWarning("Element image is not assigned in the ShopElement");
                return null;
            }

            return elementImage.sprite;
        }
    }
}
