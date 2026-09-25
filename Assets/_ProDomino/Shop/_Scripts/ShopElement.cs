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
    public class ShopElement : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private Image elementImage;
        [SerializeField] private RectTransform itemContainer;
        [SerializeField] private CustomButtonUI openConfirmationPopUpButton;
        [SerializeField] private UnityEvent onPurchaseSuccess;
        [SerializeField] private Color grayedColor = Color.gray;
        [SerializeField] private Image[] imagesToAvoidGrayedOut;
        [SerializeField] private Image rarityBadgeImage;
        [SerializeField] private TMP_Text rarityBadgeLabel;
        [SerializeField] private Sprite badgeCommonSprite;
        [SerializeField] private Sprite badgeMythicSprite;
        [SerializeField] private Sprite badgeLegendarySprite;
        [SerializeField] private Sprite badgeSpecialSprite;

        private AsyncFuncHandler<PurchaseCosmeticResponse, string> purchaseShopElement;
        private Action<ShopElement> openConfirmationPopUp;
        internal bool IsAlreadyPurchased { get; private set; }
        internal GameCosmeticData GameCosmeticData { get; private set; }
        internal PlayerCosmeticData PlayerCosmeticData { get; private set; }

        private void Awake()
        {
            ApplyContainerLayout();

            if (openConfirmationPopUpButton)
                openConfirmationPopUpButton.onClick.AddListener(OpenConfirmationPopUp);
            else
                Debug.LogWarning("Open confirmation pop-up button is not assigned in the ShopElement");
        }

        private void OnValidate()
        {
            ApplyContainerLayout();
        }

        public void ApplyContainerLayout()
        {
            if (itemContainer == null)
            {
                var ic = transform.Find("ItemContainer") as RectTransform;
                if (ic != null)
                    itemContainer = ic;
                else if (elementImage != null && elementImage.transform.parent is RectTransform parentRt)
                    itemContainer = parentRt;
            }

            if (itemContainer != null)
            {
                itemContainer.anchorMin = new Vector2(0.5f, 0.5f);
                itemContainer.anchorMax = new Vector2(0.5f, 0.5f);
                itemContainer.pivot = new Vector2(0.5f, 0.5f);
                itemContainer.anchoredPosition = new Vector2(0f, 7f);
                itemContainer.sizeDelta = new Vector2(130f, 130f);
            }

            if (elementImage != null)
            {
                elementImage.preserveAspect = true;

                var rt = elementImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
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

            UpdateRarity(GameCosmeticData.rarity);

            // Set the cost label if available
            if (costLabel)
                costLabel.text = GameCosmeticData.price.ToString();
            else
                Debug.LogWarning("Cost label is not assigned in the ShopElement");

            ApplyContainerLayout();

            // Set the element image if available
            if (elementImage)
            {
                if (elementSprite is not null)
                    elementImage.sprite = elementSprite;

                elementImage.preserveAspect = true;
            }
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
                var shopUI = GetComponentInParent<ShopUI>();
                if (shopUI != null)
                {
                    shopUI.OpenConfirmationPopUp(this);
                    return;
                }
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

        public void UpdateRarity(Enums.CosmeticRarity rarity)
        {
            if (rarityBadgeLabel != null)
            {
                rarityBadgeLabel.text = rarity.ToString();
                switch (rarity)
                {
                    case Enums.CosmeticRarity.Common:
                        rarityBadgeLabel.color = new Color32(15, 23, 42, 255);
                        if (rarityBadgeImage && badgeCommonSprite) rarityBadgeImage.sprite = badgeCommonSprite;
                        break;
                    case Enums.CosmeticRarity.Mythic:
                        rarityBadgeLabel.color = new Color32(59, 7, 100, 255);
                        if (rarityBadgeImage && badgeMythicSprite) rarityBadgeImage.sprite = badgeMythicSprite;
                        break;
                    case Enums.CosmeticRarity.Legendary:
                        rarityBadgeLabel.color = new Color32(69, 26, 3, 255);
                        if (rarityBadgeImage && badgeLegendarySprite) rarityBadgeImage.sprite = badgeLegendarySprite;
                        break;
                    case Enums.CosmeticRarity.Special:
                        rarityBadgeLabel.color = new Color32(67, 20, 7, 255);
                        if (rarityBadgeImage && badgeSpecialSprite) rarityBadgeImage.sprite = badgeSpecialSprite;
                        break;
                    default:
                        rarityBadgeLabel.color = Color.white;
                        break;
                }
            }
        }
    }
}
