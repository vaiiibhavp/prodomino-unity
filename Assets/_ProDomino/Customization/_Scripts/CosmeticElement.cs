using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;
using static TooltipController;

namespace ProDomino.CustomizationSystem
{
    /// <summary>
    /// Represents a UI element for displaying and managing cosmetic items, including icon, purchase status, and tooltip
    /// integration.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    internal class CosmeticElement : MonoBehaviour, ITooltipTarget
    {
        [field: SerializeField] public TooltipModel Model { get; private set; }
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject noPurchasedObject;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CustomButtonUI customButtonUI;

        private Graphic graphic;
        private Func<CosmeticType, string, Sprite> getCosmeticIcon;
        private Func<string, CosmeticPurchaseMethod[]> getPurchaseMethod;
        
        internal bool WasPurchase => PlayerCosmeticData?.cosmeticPurchaseMethod != null;
        internal GameCosmeticData GameCosmeticData { get; private set; }
        internal PlayerCosmeticData PlayerCosmeticData { get; private set; }
        public TooltipController TooltipController { get; private set; }
        public Graphic TargetGraphic => graphic = graphic != null 
            ? graphic 
            : GetComponent<Graphic>();

        /// <summary>
        /// Configures the controller with tooltip handling, cosmetic icon retrieval, and purchase method lookup
        /// functions.
        /// </summary>
        /// <param name="tooltipController">Controller used for displaying tooltips.</param>
        /// <param name="getCosmeticIcon">Function to retrieve cosmetic icons based on type and identifier.</param>
        /// <param name="getPurchaseMethod">Function to retrieve available purchase methods for a cosmetic item.</param>
        /// <exception cref="ArgumentNullException">Thrown if any argument is null.</exception>
        internal void Initialize
            (TooltipController tooltipController, 
            Func<CosmeticType, string, Sprite> getCosmeticIcon,
            Func<string, CosmeticPurchaseMethod[]> getPurchaseMethod)
        {
            TooltipController = tooltipController ?? throw new ArgumentNullException(nameof(tooltipController));
            this.getCosmeticIcon = getCosmeticIcon ?? throw new ArgumentNullException(nameof(getCosmeticIcon));
            this.getPurchaseMethod = getPurchaseMethod ?? throw new ArgumentNullException(nameof(getPurchaseMethod));
        }

        /// <summary>
        /// Configures cosmetic UI elements using provided game and player cosmetic data, and optionally sets a custom
        /// icon.
        /// </summary>
        /// <param name="gameCosmeticData">Game cosmetic data to configure the UI.</param>
        /// <param name="playerCosmeticData">Player cosmetic data to configure the UI.</param>
        /// <param name="optionalIcon">Optional sprite to use as the cosmetic icon.</param>
        /// <exception cref="ArgumentNullException">Thrown if gameCosmeticData is null.</exception>
        internal void Configure(GameCosmeticData gameCosmeticData, PlayerCosmeticData playerCosmeticData, Sprite optionalIcon = null)
        {
            GameCosmeticData = gameCosmeticData ?? throw new ArgumentNullException(nameof(gameCosmeticData));
            PlayerCosmeticData = playerCosmeticData;

            if (customButtonUI)
                customButtonUI.SetCustomButtonID(GameCosmeticData.id);
            else
                Debug.LogWarning("customButtonUI is not set. Please assign an customButtonUI component to assign the id.");

            if (iconImage)
            {
                var iconSprite = optionalIcon ?? getCosmeticIcon?.Invoke(GameCosmeticData.type, GameCosmeticData.id);
                iconImage.sprite = iconSprite;
            } else
                Debug.LogWarning("iconImage is not set. Please assign an Image component to display the cosmetic icon.");

            // Set the tooltip model with the cosmetic data
            SetPurchaseStatus(WasPurchase);
        }

        /// <summary>
        /// Sets the active state of the associated GameObject and updates the CanvasGroup if available.
        /// </summary>
        /// <param name="isActive">Indicates whether the GameObject should be active.</param>
        internal void SetActive(bool isActive)
        {
            if (gameObject)
            {
                if (canvasGroup)
                    canvasGroup.SetActive(WasPurchase, isSettingAlpha: false, isSettingBlocksRaycasts: false);
                else
                    Debug.LogWarning("canvasGroup is not set. Please assign a CanvasGroup component to control the element's interactivity and visibility.");

                gameObject.SetActive(isActive);
            }
            else
                Debug.LogWarning("GameObject is not set. Please assign a GameObject to control its active state.");
        }

        /// <summary>
        /// Updates the purchase status by toggling the visibility of related UI elements.
        /// </summary>
        /// <param name="isPurchased">Indicates whether the item has been purchased.</param>
        private void SetPurchaseStatus(bool isPurchased)
        {
            if (noPurchasedObject)
                noPurchasedObject.SetActive(!isPurchased);
            else
                Debug.LogWarning("purchasedObject is not set. Please assign a GameObject to indicate purchase status.");

            if (canvasGroup)
                canvasGroup.SetActive(isPurchased, isSettingAlpha: false, isSettingBlocksRaycasts: false);
            else
                Debug.LogWarning("canvasGroup is not set. Please assign a CanvasGroup component to control the element's interactivity and visibility.");
        }

        /// <summary>
        /// Displays a tooltip for the target graphic when the pointer enters, if cosmetic data is available and
        /// purchased.
        /// </summary>
        /// <param name="eventData">Pointer event data associated with the enter event.</param>
        public void OnPointerEnterProxy(PointerEventData eventData)
        {
            if (TargetGraphic != null && PlayerCosmeticData is not null and { cosmeticPurchaseMethod: not CosmeticPurchaseMethod.None })
            {
                TooltipController.ShowAtUI(TargetGraphic.rectTransform, Model);
                OnTooltipShown();
            }
        }

        /// <summary>
        /// Handles the display of a tooltip by configuring it with cosmetic purchase methods based on the tooltip ID
        /// and purchase state.
        /// </summary>
        public void OnTooltipShown()
        {
            if (Model.TooltipId is Consts.CollectionKeys.TooltipLinkID)
            {
                // Configure the tooltip link passing two ways to get the cosmetic purchase methods.
                if (TooltipController.GetTootipReference(Model.TooltipId) is TooltipLink tooltipLink)
                    tooltipLink?.Configure(GetCosmeticPurchaseMethods);
                else
                    Debug.LogWarning($"Tooltip with ID '{Model.TooltipId}' is not found. Please check the tooltip configuration.");
            } 
            else
                Debug.LogWarning("Tooltip ID is set to 'TooltipLinkID'. This tooltip should be configured with a TooltipLink component.");

            // Get the possible cosmetic purchase methods
            (CosmeticPurchaseMethod[] cosmeticPurchaseMethods, bool wasAlreadyPurchased) GetCosmeticPurchaseMethods()
            {
                var cosmeticPurchase = new List<CosmeticPurchaseMethod>();
                if (WasPurchase)
                {
                    var purchasedMethod = PlayerCosmeticData.cosmeticPurchaseMethod.Value;
                    cosmeticPurchase.Add(purchasedMethod);
                    Model.OverrideMessage($"Obtained from <b>{purchasedMethod}</b>");
                } 
                else
                { 
                    cosmeticPurchase = getPurchaseMethod?.Invoke(GameCosmeticData.id)?.ToList();
                    Model.OverrideMessage($"Could be obtained from:");
                }

                // Filter out None purchase methods
                return (cosmeticPurchase?.ToArray(), WasPurchase);
            }
        }
    }
}
