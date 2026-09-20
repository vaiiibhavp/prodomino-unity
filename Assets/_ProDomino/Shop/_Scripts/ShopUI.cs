using HelperSharedLibrary;
using ProDomino.AnalyticsSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Shop
{
    internal class ShopUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
        [SerializeField] private Transform elementsParent;
        [SerializeField] private ShopElement shopElementPrefab;
        [SerializeField] private CustomButtonUI[] rarityFilters;
        [SerializeField] private CustomButtonToggleGroupUI categoriesToggleGroup;
        [SerializeField] private TMP_Text playerTokensAmountLabel;

        [Space(15), Header("Purchase PopUp")]
        [SerializeField] private CanvasGroup confirmPurchasePopUp;
        [SerializeField] private Image confirmPurchasePreviewImage;
        [SerializeField] private TMP_Text confirmPurchasePreviewHeaderLabel;
        [SerializeField] private TMP_Text confirmPurchasePreviewDescriptionLabel;
        [SerializeField] private TMP_Text confirmPurchasePreviewCostLabel;
        [SerializeField] private CustomButtonUI confirmPurchaseButton;
        [SerializeField] private CustomButtonUI cancelPurchaseButton;

        private string confirmPurchaseDefaultPreviewHeaderText;
        private string confirmPurchaseDefaultPreviewCostText;
        private List<ShopElement> shopElements;
        private AnalyticsManager analyticsManager;
        private DictionaryService dictionaryService;
        private Func<Dictionary<GameCosmeticData, PlayerCosmeticData>> getShopGameCosmeticCollection;
        private Func<uint> getPlayerTokenAmount;
        private AsyncFuncHandler<PurchaseCosmeticResponse, string> purchaseShopElement;
        private ShopElement lastRegisteredShopElement;
        private uint? lastRegisteredPlayerTokenAmount;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Shop;
        internal CosmeticRarity CurrentRarityFilter { get; private set; } = CosmeticRarity.None;
        internal CosmeticType CurrentCategory { get; private set; } =  CosmeticType.Tiles;
        internal ShopElement CurrentSelectedShopElement { get; private set; }
        internal uint PlayerTokenCurrencyAmount => getPlayerTokenAmount?.Invoke() ?? 0;
        internal bool CouldPurchaseCosmetic { get; private set; }
        public bool RequiresAuthentication => true;

        /// <summary>
        /// Awakes the ShopUI and initializes the shop elements and purchase pop-up buttons.
        /// </summary>
        /// <param name="dictionaryService"></param>
        internal void Awake_ShopUI()
        {
            shopElements = elementsParent?.GetComponentsInChildren<ShopElement>(true)?.ToList() ?? new();

            // Register the confirm purchase pop-up default preview text to reset it later (replacing a specific key)
            if (confirmPurchasePreviewHeaderLabel)
                confirmPurchaseDefaultPreviewHeaderText = confirmPurchasePreviewHeaderLabel.text;

            // Register the confirm purchase pop-up default cost text to reset it later (replacing a specific key)
            if (confirmPurchasePreviewCostLabel)
                confirmPurchaseDefaultPreviewCostText = confirmPurchasePreviewCostLabel.text;

            // Register the confirm purchase button to handle the purchase when clicked
            if (confirmPurchaseButton)
            { 
                confirmPurchaseButton.onClick.AddListener(OnPurchaseCosmetic);

                // Initially disable the button until a cosmetic is selected and can be purchased. To prevent accidental clicks.
                confirmPurchaseButton.SetButtonInteractable(false); 
            }
            else
                Debug.LogWarning("Confirm purchase button is not assigned in the ShopUI");

            // Register the cancel purchase button to close the confirmation pop-up when clicked
            if (cancelPurchaseButton)
                cancelPurchaseButton.onClick.AddListener(CloseConfirmationPopUp);
            else
                Debug.LogWarning("Cancel purchase button is not assigned in the ShopUI");

            // Register the categories buttons to set the category when clicked
            if (categoriesToggleGroup is not null)
                categoriesToggleGroup.SetOnCustomButtonSelectedCallback(SetCategory);
            else
                Debug.LogWarning("Categories are not assigned or empty in the ShopUI");
        }

        /// <summary>
        /// Starts the ShopUI by registering the rarity filters buttons and activating the "All" rarity filter by default.
        /// </summary>
        internal void Start_ShopUI()
        {
            // Register the rarity filters buttons to set the rarity filter when clicked
            if (rarityFilters is not null and { Length: > 0 })
                Array.ForEach(rarityFilters, x =>
                {
                    if (x != null && x.Button)                   
                        x.Button.onClick.AddListener(() => SetRarityFilter(x.CustomButtonID));                    
                });
            else
                Debug.LogWarning("Rarity filters are not assigned or empty in the ShopUI");

            // By default, activate the "All" rarity filter and disable others
            ActivateAllRarityOnly();
        }

        /// <summary>
        /// Updates the shop UI based on the current selected shop element and player token amount.
        /// </summary>
        internal void Update_ShopUI()
        {
            if (CurrentSelectedShopElement  // Check if there is a currently selected shop element
                && (lastRegisteredShopElement != CurrentSelectedShopElement // Check if the current selected shop element has changed since the last update
                || !lastRegisteredPlayerTokenAmount.HasValue || lastRegisteredPlayerTokenAmount != PlayerTokenCurrencyAmount)) // Check if the player token amount has changed since the last update
            {
                lastRegisteredPlayerTokenAmount = PlayerTokenCurrencyAmount;
                lastRegisteredShopElement = CurrentSelectedShopElement;

                // Check if the current selected shop element can be purchased.
                // First check if the cosmetic is already purchased or not, then check if the player has enough tokens to purchase it
                CouldPurchaseCosmetic = CurrentSelectedShopElement.PlayerCosmeticData is null 
                    && CurrentSelectedShopElement.GameCosmeticData.price <= PlayerTokenCurrencyAmount;

                // Update the purchase button state based on the current selected shop element and player token amount
                if (confirmPurchaseButton)
                    confirmPurchaseButton.SetButtonInteractable(CouldPurchaseCosmetic);
                else
                    Debug.LogWarning("Confirm purchase button is not assigned in the ShopUI");
            }
        }

        /// <summary>
        /// Initializes the shop UI with the provided game cosmetic data and purchase handler.
        /// </summary>
        /// <param name="getShopGameCosmeticCollection"></param>
        /// <param name="purchaseShopElement"></param>
        internal void Initialize
            (AnalyticsManager analyticsManager,
            DictionaryService dictionaryService,
            Func<Dictionary<GameCosmeticData, PlayerCosmeticData>> getShopGameCosmeticCollection,
            Func<uint> getPlayerTokenAmount,
            AsyncFuncHandler<PurchaseCosmeticResponse, string> purchaseShopElement)
        {
            this.analyticsManager = analyticsManager;
            this.dictionaryService = dictionaryService;
            this.getShopGameCosmeticCollection = getShopGameCosmeticCollection;
            this.getPlayerTokenAmount = getPlayerTokenAmount;
            this.purchaseShopElement = purchaseShopElement ?? throw new ArgumentNullException(nameof(purchaseShopElement));
        }

        internal void SelectFirstCategory()
        {
            // Try to select the first button in the categories toggle group if no button is selected
            if (categoriesToggleGroup)
                categoriesToggleGroup.GetFirstButtonUI()?.Select();
        }

        internal void Configure(uint playerTokenCurrencyAmount)
        {
            if (shopElements is null)
            {
                Debug.LogWarning("Shop elements are not assigned or empty in the ShopCategory");
                return;
            }

            /// Updates the player token currency amount in the UI.
            if (playerTokensAmountLabel)
                playerTokensAmountLabel.text = playerTokenCurrencyAmount.ToString();
            else
                Debug.LogWarning("Player tokens amount label is not assigned in the ShopUI");

            // Try to instantiate additional shop elements if the count of shopGameCosmeticDatas is less than the current count of shopElements
            var shopGameCosmeticCollection = getShopGameCosmeticCollection?.Invoke();
            if (shopGameCosmeticCollection is null || shopGameCosmeticCollection.Count == 0)
            {
                Debug.LogWarning("Shop game cosmetic collection is null or empty. Cannot configure shop elements.");
                return;
            }
            shopGameCosmeticCollection = shopGameCosmeticCollection
                ?.OrderBy(x => x.Value is not null)
                ?.ThenBy(x => x.Value?.cosmeticPurchaseMethod.GetValueOrDefault())
                ?.ThenBy(x => x.Value?.acquiredTime)
                ?.ToDictionary(x => x.Key, x => x.Value);

            var leftingInstancesCount = shopGameCosmeticCollection.Count - shopElements.Count;
            if (leftingInstancesCount > 0)
                for (int i = 0; i < leftingInstancesCount; i++)
                {
                    var shopElement = Instantiate(shopElementPrefab, elementsParent);
                    shopElements.Add(shopElement);
                }

            // If there are more shop elements than game cosmetic data, remove the excess elements
            else if (leftingInstancesCount < 0)
                for (int i = shopElements.Count - 1; i >= shopGameCosmeticCollection.Count; i--)
                {
                    Destroy(shopElements[i].gameObject);
                    shopElements.RemoveAt(i);
                }

            // Configure each shop element with the corresponding game cosmetic data
            if (shopElements is not null and { Count: > 0 })
                for (var i = 0; i < shopElements.Count; i++)
                {
                    var shopElement = shopElements[i];
                    var (gameCosmeticData, playerCosmeticData) = shopGameCosmeticCollection.ElementAtOrDefault(i);
                    if (shopElement == null || gameCosmeticData == null)
                    {
                        Debug.LogWarning($"Shop element at index {i} is null or gameCosmeticData is null.");
                        continue;
                    }

                    // Try to get the sprite from the dictionary service using the cosmetic ID
                    var searchedCosmeticSprite = dictionaryService.GetSprite(gameCosmeticData.type.ToString(), gameCosmeticData.id);

                    // Initialize the shop element with the cosmetic data and sprite
                    shopElement.Initialize
                        (gameCosmeticData,
                        playerCosmeticData,
                        searchedCosmeticSprite,
                        purchaseShopElement,
                        OpenConfirmationPopUp);
                }

            SetCategory(CurrentCategory.ToString());
            SetRarityFilter(CurrentRarityFilter.ToString());
        }

        /// <summary>
        /// Previews the purchase details of the currently selected shop element.
        /// </summary>
        private void PreviewPurchase()
        {
            if (!CurrentSelectedShopElement)
            {
                Debug.LogWarning("Current selected shop element is null. Cannot preview purchase.");
                return;
            }

            // Reset the confirm purchase pop-up preview text replacing the cosmetic name
            if (confirmPurchasePreviewHeaderLabel)
            { 
                var defaultPreviewText = confirmPurchaseDefaultPreviewHeaderText
                    ?.Replace("{cosmeticName}", CurrentSelectedShopElement.GameCosmeticData.name)
                    ?.Replace("{tokenAmount}", CurrentSelectedShopElement.GameCosmeticData.price.ToString());

                confirmPurchasePreviewHeaderLabel.text = defaultPreviewText;
            } 
            else
                Debug.LogWarning("Confirm purchase preview name label is not assigned in the ShopUI");

            // Set the confirm purchase pop-up preview description
            if (confirmPurchasePreviewDescriptionLabel)
                confirmPurchasePreviewDescriptionLabel.text = CurrentSelectedShopElement.GameCosmeticData.description;
            else
                Debug.LogWarning("Confirm purchase preview description label is not assigned in the ShopUI");

            // Set the confirm purchase pop-up preview image
            if (confirmPurchasePreviewImage)
                confirmPurchasePreviewImage.sprite = CurrentSelectedShopElement.GetElementImage();
            else
                Debug.LogWarning("Confirm purchase preview image is not assigned in the ShopUI");

            // Set the confirm purchase pop-up preview cost label
            if (confirmPurchasePreviewCostLabel)
            {
                var defaultPreviewText = confirmPurchaseDefaultPreviewCostText
                    ?.Replace("{tokenAmount}", CurrentSelectedShopElement.GameCosmeticData.price.ToString());

                confirmPurchasePreviewCostLabel.text = defaultPreviewText;
            }
            else
                Debug.LogWarning("Confirm purchase preview cost label is not assigned in the ShopUI");
        }

        /// <summary>
        /// Opens the confirmation pop-up for the selected shop element and previews the purchase details.
        /// </summary>
        /// <param name="element"></param>
        internal void OpenConfirmationPopUp(ShopElement element)
        {
            if (!confirmPurchasePopUp)
            {
                Debug.LogWarning("Confirm purchase pop-up is not assigned in the ShopUI");
                return;
            }

            // Register the current selected shop element
            CurrentSelectedShopElement = element;

            confirmPurchasePopUp.SetActive(true);
            PreviewPurchase();
        }

        /// <summary>
        /// Closes the confirmation pop-up and resets the current selected shop element.
        /// </summary>
        internal void CloseConfirmationPopUp()
        {
            if (!confirmPurchasePopUp)
            {
                Debug.LogWarning("Confirm purchase pop-up is not assigned in the ShopUI");
                return;
            }

            // Remove the current selected shop element
            CurrentSelectedShopElement = null;
            confirmPurchasePopUp.SetActive(false);
        }

        /// <summary>
        /// Sets the rarity filter based on the button ID.
        /// </summary>
        /// <param name="buttonID">The ID of the button pressed (must match enum name).</param>
        private void SetRarityFilter(string buttonID)
        {
            if (string.IsNullOrEmpty(buttonID))
            {
                Debug.LogWarning("Button ID is null or empty. Cannot set rarity filter.");
                return;
            }

            if (!Enum.TryParse<CosmeticRarity>(buttonID, out var parsedRarity))
            {
                Debug.LogWarning($"Failed to parse button ID '{buttonID}' to CosmeticRarity enum.");
                return;
            }

            // If parsedRarity is All or None Å® turn on All and disable others
            if (parsedRarity is CosmeticRarity.None)
            {
                if (CurrentRarityFilter is not CosmeticRarity.None)
                { 
                    ActivateAllRarityOnly();
                    DetermineElements();
                }
                return;
            }

            // If parsedRarity is not All/None/Current Å® add flag
            if (!CurrentRarityFilter.HasFlag(parsedRarity))
                AddRarityFilterFlag(parsedRarity);

            // If parsedRarity is not All or it's filled with individual flags, remove the individual flag
            else 
                RemoveRarityFilterFlag(parsedRarity);

            // If no filters remain, fall back to All
            if (CurrentRarityFilter is CosmeticRarity.None)
                ActivateAllRarityOnly();
            else
                SetButtonState(CosmeticRarity.None.ToString(), false);

            DetermineElements();
        }

        private void SetCategory(string buttonID)
        {
            if (string.IsNullOrEmpty(buttonID))
            {
                Debug.LogWarning("Custom button ID is null or empty. Cannot set category.");
                return;
            }

            if (!Enum.TryParse<CosmeticType>(buttonID, out var cosmeticType))
            {
                Debug.LogWarning($"Failed to parse button ID '{buttonID}' to CosmeticType enum.");
                return;
            }

            CurrentCategory = cosmeticType;

            // Set the active state of each shop element based on the current category and rarity filter
            DetermineElements();
        }

        private void DetermineElements()
        {
            if (shopElements is null || shopElements.Count == 0)
            {
                Debug.LogWarning("Shop elements are not assigned or empty in the ShopUI");
                return;
            }

            // Set the active state of each shop element based on the current rarity filter and category
            shopElements.ForEach(x => x.SetActive
                ((CurrentRarityFilter is CosmeticRarity.All or CosmeticRarity.None || (x.GameCosmeticData is not null && CurrentRarityFilter.HasFlag(x.GameCosmeticData.rarity)))
                && x.GameCosmeticData?.type == CurrentCategory));
        }

        /// <summary>
        /// Updates a UI toggle button visually.
        /// </summary>
        private void SetButtonState(string buttonID, bool isActive)
        {
            if (rarityFilters is null or { Length: 0 })
            {
                Debug.LogWarning("Rarity filters are not assigned or empty in the ShopUI");
                return;
            }

            // Get the button UI from the toggle group using the button ID
            var toggle = rarityFilters.FirstOrDefault(x => x.CustomButtonID == buttonID);
            if (toggle == null)
            {
                Debug.LogWarning($"Button with ID '{buttonID}' not found in the categories toggle group.");
                return;
            }

            // Invoke the appropriate action based on the toggle state
            (isActive ? (Action<bool>)toggle.Select : toggle.Deselect).Invoke(true);
        }

        /// <summary>
        /// Enables only the "All" rarity and disables others.
        /// </summary>
        private void ActivateAllRarityOnly()
        {
            DeactivateEveryRarity();
            SetButtonState(CosmeticRarity.None.ToString(), true);
        }

        /// <summary>
        /// Turns off all rarity filters and sets the current rarity filter to None.
        /// </summary>
        private void DeactivateEveryRarity()
        {
            CurrentRarityFilter = CosmeticRarity.None;

            foreach (CosmeticRarity rarity in Enum.GetValues(typeof(CosmeticRarity)))
                if (rarity != CosmeticRarity.None)
                    SetButtonState(rarity.ToString(), false);
        }

        /// <summary>
        /// Adds a rarity filter flag to the current rarity filter and updates the button state.
        /// </summary>
        /// <param name="rarity"></param>
        private void AddRarityFilterFlag(CosmeticRarity rarity)
        {
            CurrentRarityFilter |= rarity;
            SetButtonState(rarity.ToString(), true);
        }

        /// <summary>
        /// Removes a rarity filter flag from the current rarity filter and updates the button state.
        /// </summary>
        /// <param name="rarity"></param>
        private void RemoveRarityFilterFlag(CosmeticRarity rarity)
        {
            CurrentRarityFilter &= ~rarity;
            SetButtonState(rarity.ToString(), false);
        }

        /// <summary>
        /// Handles the purchase of the currently selected cosmetic.
        /// </summary>
        private async void OnPurchaseCosmetic()
        {
            if (!CurrentSelectedShopElement)
            {
                Debug.LogWarning("Current selected shop element is null. Cannot purchase the cosmetic.");
                return;
            }

            var purchaseResponse = await CurrentSelectedShopElement.Purchase();
            if (purchaseResponse is null)
            {
                Debug.LogWarning("Purchase response is null. Cannot proceed with purchase.");
                return;
            }

            // Send analytic for the token modification
            analyticsManager?.SendAnalytic(AnalyticType.OnTokenModified);

            // If the purchase was successful, close the confirmation pop-up and update the UI
        }

        /// <summary>
        /// Validates the purchased cosmetic by setting it as already purchased in the shop UI.
        /// </summary>
        /// <param name="cosmeticPurchased"></param>
        internal void ValidatePurchasedCosmetic(PlayerCosmeticData cosmeticPurchased)
        {
            if (cosmeticPurchased is null)
            {
                Debug.LogWarning("Cosmetic purchased data is null. Cannot validate the purchase.");
                return;
            }

            // Find the shop element that matches the purchased cosmetic data
            var purchasedElement = shopElements.FirstOrDefault(x => x.GameCosmeticData.id == cosmeticPurchased.id);
            if (purchasedElement is null)
            {
                Debug.LogWarning($"No shop element found for purchased cosmetic with ID: {cosmeticPurchased.id}");
                return;
            }

            // Set the purchased cosmetic as already purchased
            purchasedElement.SetPurchaseBlockability(true);
        }
    }
}
