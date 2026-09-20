using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.NavigationSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.CustomizationSystem
{
    /// <summary>
    /// Manages the customization UI for player cosmetics, including selection, preview, application, and data
    /// synchronization with backend services.
    /// </summary>
    public class CustomizationController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private NavigationPanelController navigationPanelController;
        [SerializeField] private CosmeticElement cosmeticElementPrefab;
        [SerializeField] private CustomButtonToggleGroupUI cosmeticsToggleGroup;
        [SerializeField] private CustomButtonToggleGroupUI cosmeticTypeToggleGroup;
        [SerializeField] private Transform cosmeticsParent;

        [Header("Previews")]
        [SerializeField] private GameObject cosmeticPreviewContainer;
        [SerializeField] private GameObject badgesPreviewContainer;
        [SerializeField] private Image cosmeticPreviewImage;
        [SerializeField] private Image[] badgesPreviewImages;

        [Header("Buttons")]
        [SerializeField] private CustomButtonUI applyCosmeticButton;
        [SerializeField] private CustomButtonUI resetCosmeticButton;

        private GameManager gameManager;
        private AuthManager authManager;
        private AnalyticsManager analyticsManager;
        private DictionaryService dictionaryService;
        private TooltipController tooltipController;
        private List<CosmeticElement> cosmeticInstances;
        private Sprite defaultCosmeticPreviewImage;
        private Sprite defaultBadgePreviewSprite;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        private Dictionary<GameCosmeticData, PlayerCosmeticData> _playerCosmeticDataCollection;
        public Dictionary<GameCosmeticData, PlayerCosmeticData> PlayerCosmeticDataCollection => _playerCosmeticDataCollection ??= new();
        internal List<CosmeticElement> SelectedCosmeticElements { get; private set; }

        public CosmeticType CurrentCosmeticType { get; private set; }

        public bool IsAuthenticated => gameManager?.IsAuthenticated ?? false;

        protected void Awake()
        {
            cosmeticInstances = GetComponentsInChildren<CosmeticElement>(true)?.ToList() ?? new();
            SelectedCosmeticElements = new();
            _playerCosmeticDataCollection = new();

            // By default set the current cosmetic type to Icons
            CurrentCosmeticType = CosmeticType.Icons;

            // Validate that the required components are assigned
            if (cosmeticsToggleGroup)
                cosmeticsToggleGroup.SetOnCustomButtonsSelectedCallback(OnSelectCosmetic);
            else
                Debug.LogWarning("cosmeticsToggleGroup is not assigned. Please assign it in the CustomizationController.");
            
            
            // Validate that the required components are assigned
            if (cosmeticTypeToggleGroup)
                cosmeticTypeToggleGroup.SetOnCustomButtonSelectedCallback(OnCosmeticTypeSelected);
            else
                Debug.LogWarning("CosmeticTypeToggleGroup is not assigned. Please assign it in the CustomizationController.");

            // Register the default preview image
            if (cosmeticPreviewImage)
                defaultCosmeticPreviewImage = cosmeticPreviewImage.sprite;
            else
                Debug.LogWarning("CosmeticPreviewImage is not assigned. Please assign it in the CustomizationController.");
            
            
            // Register the default preview image
            if (badgesPreviewImages is not null and { Length: > 0 })
                defaultBadgePreviewSprite = badgesPreviewImages.FirstOrDefault(x => x != null)?.sprite;
            else
                Debug.LogWarning("CosmeticPreviewImage is not assigned. Please assign it in the CustomizationController.");

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            tooltipController = ServiceLocator.Instance.GetService<TooltipController>();

            // Validate that all required services are available
            if (!gameManager || !authManager || !analyticsManager || !dictionaryService || !tooltipController)
            {
                Debug.LogError("CustomizationController: One or more required services are not available. Please ensure AuthManager, AnalyticsManager, MissionManager, DictionaryService and TooltipController are initialized.");
                return;
            }

            gameManager.HandleOnSignIn(async () =>
            {
                await RefreshData(false);
            });
            
            gameManager.HandleOnSignOut(async () =>
            {
                await RefreshData(false);
            });

            // Initialize the cosmetic instances
            if (cosmeticInstances is not null and { Count: > 0 })
                for (var i = 0; i < cosmeticInstances.Count; i++)
                {
                    cosmeticInstances[i].Initialize
                        (tooltipController,
                        (_cosmeticType, _id) => dictionaryService.GetSprite(_cosmeticType.ToString(), _id) ?? dictionaryService.GetSpriteNoCollection(_id),
                        GetPurchaseMethod);
                }
        }

        public async void Start()
        {
            // By default, set the visibility to false
            SetVisibility(false);

            // Register the apply cosmetic button to confirm the selection of a cosmetic
            if (applyCosmeticButton)
                applyCosmeticButton.AddEventToListener(ConfirmSelection);
            else
                Debug.LogWarning("ApplyCosmeticButton is not assigned. Please assign it in the CustomizationController.");

            // Set the reset cosmetic button to return to the previous screen
            if (resetCosmeticButton)
                resetCosmeticButton.AddEventToListener(Return);
            else
                Debug.LogWarning("ResetCosmeticButton is not assigned. Please assign it in the CustomizationController.");

            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => gameManager is not null and { IsAlreadyInitialized: true } && IsAuthenticated);

            analyticsManager.Subscribe(UpdatePlayerCosmeticData);

            var tooltipLink = (TooltipLink)tooltipController.GetTootipReference(Consts.CollectionKeys.TooltipLinkID);
            if (tooltipLink)
                tooltipLink.Initialize(RedirectUser);
            else
                Debug.LogWarning($"TooltipLink with ID {Consts.CollectionKeys.TooltipLinkID} not found. Please ensure it is set up in the TooltipController.");

            // Try to update the profile (create it if it does not exist) and update the player cosmetic data from the backend
            await UpdateProgress();

            // Refresh the player cosmetic data from the data already obtained from the backend (using the GameManager)
            var shouldRefereshOnSettingDefaults = gameManager.PlayerCosmeticDatas is null or { Length: 0 };
            await RefreshData(shouldRefereshOnSettingDefaults);

            // By default, select the first cosmetic type button in the toggle group
            if (cosmeticTypeToggleGroup)
                cosmeticTypeToggleGroup.GetFirstButtonUI()?.Select();
            else
                Debug.LogWarning("CosmeticTypeToggleGroup is not assigned. Please assign it in the CustomizationController.");
        }

        private void Update()
        {
            if (!applyCosmeticButton)
                return;

            if (SelectedCosmeticElements is not null and { Count: > 0 } && !applyCosmeticButton.IsInteractable)
                applyCosmeticButton.SetButtonInteractableWithAlphaFull(true);
            
            else if (SelectedCosmeticElements is null or { Count: 0 } && applyCosmeticButton.IsInteractable)
                applyCosmeticButton.SetButtonInteractableWithAlphaFull(false);
        }

        private void OnDestroy()
        {
            // Unsubscribe from the analytics manager to avoid memory leaks
            if (analyticsManager is not null)
                analyticsManager.Unsubscribe(UpdatePlayerCosmeticData);
        }

        /// <summary>
        /// Refreshes the player cosmetic data by fetching it from the backend and updating the UI accordingly.
        /// </summary>
        /// <param name="shouldRefreshdata">Indicates whether the data should be refreshed from the backend.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async UniTask RefreshData(bool shouldRefreshdata = true) 
        {
            // Refresh player data from the backend
            if (shouldRefreshdata)
                await gameManager.RefreshProtectedPlayerData();

            // Clear existing collections
            _playerCosmeticDataCollection?.Clear();

            // Initialize collections
            UpdateCosmeticDataCollection(ref _playerCosmeticDataCollection);

            UpdateInstances();
            UpdateElements();

            void UpdateCosmeticDataCollection(ref Dictionary<GameCosmeticData, PlayerCosmeticData> cosmeticDataCollection)
            {
                var gameCosmeticDatas = gameManager.GameCosmeticData;
                if (gameCosmeticDatas is null or { Length: 0 })
                {
                    Debug.LogWarning("Game cosmetic data lists is null or empty.");
                    return;
                }

                var playerCosmeticDatas = gameManager.PlayerCosmeticDatas;
                if (playerCosmeticDatas is null or { Length: 0 })
                    Debug.LogWarning("No player cosmetic data found in the response. Cannot validate cosmetics.");

                // Iterate for each game cosmetic and get its corresponding player cosmetic data; later add it to the collection
                foreach (var gameCosmeticData in gameCosmeticDatas)
                {
                    if (gameCosmeticData is null or { type: CosmeticType.None })
                        continue;

                    var playerCosmeticData = playerCosmeticDatas?.FirstOrDefault(m => m.id == gameCosmeticData.id);
                    cosmeticDataCollection?.Add(gameCosmeticData, playerCosmeticData);
                }

                var playerAchievementDatas = gameManager.PlayerAchievementDatas;
                foreach (var gameAchievementData in gameManager.GameAchievementData)
                {
                    if (gameAchievementData is null or { achievementType: AchievementType.None })
                        continue;

                    var playerAchievementData = playerAchievementDatas?.FirstOrDefault(x => x.achievement == gameAchievementData.achievement);

                    var newGameCosmeticData = new GameCosmeticData
                        (id: gameAchievementData.achievement.ToString(),
                        name: gameAchievementData.achievement.ToString().CapitalizeFirstLetter(),
                        description: null,
                        type: CosmeticType.Badges,
                        rarity: CosmeticRarity.None,
                        isAvailable: false,
                        price: 0,
                        currency: Currency.None);

                    var newPlayerCosmeticData = default(PlayerCosmeticData);
                    if (playerAchievementData is not null)
                        newPlayerCosmeticData = new PlayerCosmeticData
                            (id: playerAchievementData.achievement.ToString(),
                            adquiredTime: playerAchievementData.completedTime ?? 0,
                            ammoutPaid: 0,
                            costType: Currency.None,
                            cosmeticPurchaseMethod: CosmeticPurchaseMethod.None);

                    cosmeticDataCollection?.Add(newGameCosmeticData, newPlayerCosmeticData);
                }
            }
        }

        /// <summary>
        /// Sets the visibility of the customization UI by enabling or disabling the CanvasGroup component.
        /// </summary>
        /// <param name="isVisible">Indicates whether the customization UI should be visible.</param>
        public void SetVisibility(bool isVisible)
        {
            if (!canvasGroup)
            {
                Debug.LogWarning($"Cannot set visibility, CanvasGroup is not assigned.");
                return;
            }

            canvasGroup.SetActive(isVisible);
        }

        /// <summary>
        /// Updates the cosmetic instances based on the player cosmetic data collection.
        /// </summary>
        private void UpdateInstances()
        {
            var playerCosmeticDataCollection = PlayerCosmeticDataCollection
                ?.OrderByDescending(x => x.Value?.cosmeticPurchaseMethod.GetValueOrDefault())
                ?.ToList();

            if (playerCosmeticDataCollection is null or { Count: 0 })
            {
                Debug.LogWarning("Cosmetic data collection is null or empty.");
                return;
            }

            // Check if exists a ProviderIcon in the AuthManager. If it does, create a cosmetic instance for it (to be able to display it in the customization UI and select it)
            if (authManager is not null and { ProviderIcon: not null })
            {
                var providerInstance = default(CosmeticElement);

                // Check if there are any existing cosmetic instances; if not, create a new one
                providerInstance = cosmeticInstances.FirstOrDefault() ?? Instantiate(cosmeticElementPrefab, cosmeticsParent);
                providerInstance.Initialize
                    (tooltipController,
                    (_cosmeticType, _id) => dictionaryService.GetSprite(_cosmeticType.ToString(), _id) ?? dictionaryService.GetSpriteNoCollection(_id),
                    GetPurchaseMethod);

                cosmeticInstances.Add(providerInstance);
                providerInstance.SetActive(true);

                // Configure the new instance with the provider icon
                // Create a new GameCosmeticData and PlayerCosmeticData for the provider icon
                providerInstance.Configure
                    (new GameCosmeticData("providerIcon", "Media Icon", "Icon obtained via media providers", CosmeticType.Icons, CosmeticRarity.Common, true, 0, Currency.None), 
                    new PlayerCosmeticData("providerIcon", ((DateTimeOffset)DateTime.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds(), 0, Currency.None, CosmeticPurchaseMethod.None),
                    authManager.ProviderIcon);
            }

            // Ensure we have enough cosmetic instances to display all cosmetics
            for (var i = 0; i < playerCosmeticDataCollection.Count; i++)
                if (i >= cosmeticInstances.Count)
                {
                    var newInstance = Instantiate(cosmeticElementPrefab, cosmeticsParent);
                    newInstance.Initialize
                        (tooltipController,
                        (_cosmeticType, _id) => dictionaryService.GetSprite(_cosmeticType.ToString(), _id) ?? dictionaryService.GetSpriteNoCollection(_id),
                        GetPurchaseMethod);
                    cosmeticInstances.Add(newInstance);
                }

            // Deactivate unused daily cosmetic instances
            cosmeticInstances.ForEach(instance => instance.SetActive(false));

            // Configure the toggle group with the possible new cosmetic instances
            if (cosmeticsToggleGroup)
                cosmeticsToggleGroup.Configure();
            else
                Debug.LogWarning("CosmeticsToggleGroup is not assigned. Please assign it in the CustomizationController.");

            // Iterate through the cosmetics and ensure we have enough instances
            foreach (var (gameCosmeticData, playerCosmeticData) in playerCosmeticDataCollection)
            {
                var instance = cosmeticInstances?.FirstOrDefault(i => !i.gameObject.activeSelf);
                if (instance != null)
                {
                    instance.Configure(gameCosmeticData, playerCosmeticData);
                    instance.SetActive(true);
                }
            }

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Returns the purchase methods for a given cosmetic ID by checking if it can be obtained in the shop or achievements.
        /// </summary>
        /// <param name="cosmeticID">The ID of the cosmetic item.</param>
        /// <returns>An array of purchase methods available for the cosmetic item.</returns>
        private CosmeticPurchaseMethod[] GetPurchaseMethod(string cosmeticID)
        {
            // Find if the cosmetic can be obtained in the shop or achievements
            var couldBeObtainInShop = gameManager?.GameCosmeticData?.Any(x => x.id == cosmeticID && x.isAvailable);
            var couldBeObtainInAchievements = gameManager?.GameAchievementData?.Any(x => x.rewardCosmeticsIDs.Any(y => y == cosmeticID));

            // Register the purchase methods based on the availability
            var purchaseMethods = new List<CosmeticPurchaseMethod>();
            if (couldBeObtainInShop is true)
                purchaseMethods.Add(CosmeticPurchaseMethod.Shop);
            if (couldBeObtainInAchievements is true)
                purchaseMethods.Add(CosmeticPurchaseMethod.Achievement);

            // Return the array of purchase methods, filtering out None methods
            return purchaseMethods?.ToArray();
        }

        /// <summary>
        /// Redirects the user to the specified navigation panel type.
        /// </summary>
        /// <param name="navigationPanelType">The type of navigation panel to redirect the user to.</param>
        private void RedirectUser(NavigationPanelType navigationPanelType)
        {
            if (!navigationPanelController)
            {
                Debug.LogWarning("NavigationPanelController is not set. Please assign it in the CustomizationController.");
                return;
            }

            // Redirect the user to the specified navigation panel type
            navigationPanelController.ExternalActivateNavigationPanel(navigationPanelType);

            // Hide the active tooltip if it exists
            tooltipController.Hide();

            // Turn off the customization UI when redirecting the user
            SetVisibility(false);
        }

        /// <summary>
        /// Confirms the selection of the currently selected cosmetic element by updating the player profile with the selected cosmetic data.
        /// </summary>
        private async void ConfirmSelection()
        {
            if (SelectedCosmeticElements is null or { Count: 0 })
            {
                Debug.LogWarning("No cosmetic element selected. Cannot confirm selection.");
                return;
            }

            applyCosmeticButton.SetButtonInteractable(false);
            resetCosmeticButton.SetButtonInteractable(false);

            // Update the player profile with the selected cosmetic
            await UpdateProgress();

            // Once the profile is updated (or an exception was thrown), set the buttons to be interactable again
            applyCosmeticButton.SetButtonInteractable(true);
            resetCosmeticButton.SetButtonInteractable(true);
        }

        /// <summary>
        /// Returns to the previous screen by deselecting the selected cosmetic element and hiding the customization UI.
        /// </summary>
        private void Return()
        {
            SetVisibility(false);
        }

        /// <summary>
        /// Updates the player profile with the selected cosmetic by calling the backend module.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async UniTask UpdateProgress()
        {
            // Register the selected cosmetic element data to be sent to the backend (or not register anything to only try to set default properties )
            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["cosmeticsIDs"] = SelectedCosmeticElements?.Select(x => x?.GameCosmeticData?.id)?.ToArray(),
                ["cosmeticType"] = CurrentCosmeticType
            });

            // Use try catch to control the exception and be able to turn on the buttons again
            try
            {
                var jsonResponseEncrypted = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.UpdateProfile(dataEncrypted).AsUniTask(), 
                    taskId: nameof(module.UpdateProfile), 
                    showLoading: true,
                    returnExceptionOnError: true);

                if (!string.IsNullOrEmpty(jsonResponseEncrypted))
                {
                    var profileUpdateResponse = authManager.DeserializeAndDecryptData<ProfileResponse>(jsonResponseEncrypted);
                    if (profileUpdateResponse is not null)
                    {
                        gameManager.UpdatePlayerProfileData(profileUpdateResponse.playerProfileData);
                        analyticsManager?.SendAnalytic(AnalyticType.SelectCosmetic);
                    } 
                    else
                        Debug.LogWarning("Failed to deserialize profile data response.");
                } 
                else
                    Debug.LogWarning("Failed to update profile with selected cosmetic. Response is null.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while updating profile with selected cosmetic: {ex.Message}");
            }
        }

        /// <summary>
        /// Determine the visibility of the cosmetic elements according the current cosmetic type
        /// </summary>
        private void UpdateElements()
        {
            // Filter the cosmetic instances based on the selected cosmetic type
            if (cosmeticInstances is not null and { Count: > 0 })
                cosmeticInstances.ForEach(x =>
                {
                    // Validate that the cosmetic instance and its data are not null
                    if (x == null)
                        Debug.LogError("One of the cosmetic instances is null. Please ensure all cosmetic instances are properly initialized.");

                    // Validate that the cosmetic instance has its GameCosmeticData assigned
                    if (x.GameCosmeticData is null)
                        Debug.LogWarning("One of the cosmetic instances has no GameCosmeticData assigned. Please ensure all cosmetic instances are properly configured.");

                    x?.SetActive(x?.GameCosmeticData?.type == CurrentCosmeticType);
                });
            else
                Debug.LogWarning("No cosmetic element selected. Cannot update elements.");
        }

        /// <summary>
        /// When the player obtains a cosmetic, refresh the data to update the UI
        /// </summary>
        /// <param name="type">The type of analytic event.</param>
        /// <param name="data">Additional data associated with the analytic event.</param>
        public void UpdatePlayerCosmeticData(AnalyticType type, object data)
        {
            if (type is not AnalyticType.ObtainCosmetic)
                return;

            RefreshData().Forget();
        }

        /// <summary>
        /// Event invoked when a cosmetic element is selected by the player.
        /// It handles the selection of a cosmetic, updates the player profile with the selected cosmetic, and sends an analytic event.
        /// </summary>
        /// <param name="cosmeticsIDs">The list of selected cosmetic IDs.</param>
        private void OnSelectCosmetic(List<string> cosmeticsIDs)
        {
            if (gameManager is null or { IsAlreadyInitialized: false })
                return;

            if (cosmeticsIDs is null or { Count: 0 })
            {
                Debug.LogWarning("No cosmetics ids where selected properly");
                return;
            }

            for (int i = 0; i < cosmeticsIDs.Count; i++)
            { 
                var cosmeticID = cosmeticsIDs[i];

                if (SelectedCosmeticElements.Count > i)
                    SelectedCosmeticElements[i] = !string.IsNullOrEmpty(cosmeticID) 
                        ? cosmeticInstances?.FirstOrDefault(x => x.GameCosmeticData.id == cosmeticID)
                        : null;
            }

            if (SelectedCosmeticElements is null or { Count: 0 })
            {
                Debug.LogWarning("No cosmetics elements where selected properly");
                return;
            }

            if (CurrentCosmeticType is not CosmeticType.Badges)
            {
                if (!cosmeticPreviewImage)
                {
                    Debug.LogWarning("Cosmetic preview image is not set. Please assign an Image component to display the cosmetic preview.");
                    return;
                }

                var firstElement = SelectedCosmeticElements.FirstOrDefault();
                if (firstElement is not null and { WasPurchase: true })
                {
                    // Get the cosmetic icon from the game manager and set it to the cosmetic preview image
                    var cosmeticIcon = dictionaryService.GetSprite(firstElement.GameCosmeticData.type.ToString(), firstElement.GameCosmeticData.id);
                    cosmeticPreviewImage.sprite = cosmeticIcon ?? defaultCosmeticPreviewImage;
                } 
                else
                    cosmeticPreviewImage.sprite = cosmeticPreviewImage.sprite ?? defaultCosmeticPreviewImage;
            } 
            else if (CurrentCosmeticType is CosmeticType.Badges)
            {
                if (badgesPreviewImages is null or { Length: 0 })
                { 
                    Debug.LogWarning("Badges previews images are not set. Please assign Images components to display the cosmetic previews.");
                    return;
                }

                for (var i = 0; i < badgesPreviewImages.Length; i++)
                {
                    var preview = badgesPreviewImages[i];
                    var selectedElement = SelectedCosmeticElements.ElementAtOrDefault(i);

                    // If the badges elements are not selected yet, just continue
                    if (selectedElement is not null and { WasPurchase: true })
                    { 
                        // Assign the badge icon from the game manager and set it to the cosmetic preview image
                        var cosmeticIcon = dictionaryService.GetSprite(Consts.CollectionKeys.Achievements, selectedElement.GameCosmeticData.id);
                        preview.sprite = cosmeticIcon ?? defaultBadgePreviewSprite;
                    }
                    else
                        preview.sprite = preview.sprite ?? defaultBadgePreviewSprite;
                }
            }
        }

        /// <summary>
        /// Event invoked when a cosmetic type button is selected in the toggle group.
        /// It filters the cosmetic instances based on the selected cosmetic type.
        /// </summary>
        /// <param name="buttonID">The ID of the button that was selected.</param>
        private void OnCosmeticTypeSelected(string buttonID)
        {
            if (gameManager is null or { IsAlreadyInitialized: false })
                return;

            if (string.IsNullOrEmpty(buttonID))
            {
                Debug.LogWarning("Button ID is null or empty. Cannot set cosmetic type filter.");
                return;
            }

            if (!Enum.TryParse<CosmeticType>(buttonID, out var parsedCosmeticType))
            {
                Debug.LogWarning($"Failed to parse button ID '{buttonID}' to cosmetic type enum.");
                return;
            }

            CurrentCosmeticType = parsedCosmeticType;

            UpdateElements();
            UpdatePreviews();

            for (int i = 0; i < SelectedCosmeticElements.Count; i++)
            {
                var cosmeticElement = SelectedCosmeticElements[i];
                if (cosmeticElement == null)
                    continue;

                cosmeticInstances.FirstOrDefault(x => x.GameCosmeticData?.id == cosmeticElement.GameCosmeticData?.id);
            }

            /// Update the visibility of the cosmetic and badges previews depending on the selected cosmetic type, and reset their images to the default ones if the selected cosmetics are not purchased
            void UpdatePreviews()
            {
                var isSimpleCosmeticSelected = CurrentCosmeticType is not CosmeticType.Badges;
                cosmeticPreviewContainer.gameObject.SetActive(isSimpleCosmeticSelected);
                badgesPreviewContainer.gameObject.SetActive(!isSimpleCosmeticSelected);

                var cosmeticPreviewCount = (byte)(isSimpleCosmeticSelected ? 1 : badgesPreviewImages?.Length ?? 3);

                // Reset every possible selection that toggle group could has
                cosmeticsToggleGroup.DeselectAll();

                // Depending of the cosmetic type, alters the limit of the selection and its requirement to select one element
                cosmeticsToggleGroup.SetSelectionLimit(cosmeticPreviewCount);
                cosmeticsToggleGroup.SetOneMandatorySelectionLimit(isSimpleCosmeticSelected);

                // Initialize or resize the fixed-size list with nulls
                SelectedCosmeticElements = new(new CosmeticElement[cosmeticPreviewCount]);

                // Check if the element is really a cosmetic (not a badge)
                if (isSimpleCosmeticSelected)
                {
                    // Get the profile icon from the game manager and set it to the cosmetic preview image
                    var (id, icon) = GetCosmeticTypeCurrentIcon();

                    if (cosmeticPreviewImage)
                        cosmeticPreviewImage.sprite = icon ?? defaultCosmeticPreviewImage;
                    else
                        Debug.LogWarning("Cosmetic preview image is not set. Please assign an Image component to display the cosmetic preview.");


                    // Select by default the element previously registered
                    if (!string.IsNullOrEmpty(id))
                        cosmeticsToggleGroup.GetButtonUI(id)?.Select();
                } 
                
                else
                {
                    var badges = gameManager.GetBadges();
                    if (badgesPreviewImages is not null and { Length: > 0 })
                        for (var i = 0; i < badgesPreviewImages.Length; i++)
                        {
                            var preview = badgesPreviewImages[i];

                            var badgeTuple = badges?.ElementAtOrDefault(i);
                            preview.sprite = badgeTuple?.icon ?? defaultBadgePreviewSprite;
                        }
                    else
                        Debug.LogWarning("Badges preview images are not set. Please assign Images components to display the badges previews.");

                    // Select by default the elements previously registered
                    if (badges is not null and { Length: > 0 })
                        foreach (var badgeTuple in badges)
                            if (badgeTuple is not null and { id: not null and string badgeID })
                                cosmeticsToggleGroup.GetButtonUI(badgeID)?.Select();
                }
            }

            /// Depending on the current cosmetic type, gets the current selected cosmetic ID and icon to be displayed in the preview
            (string id, Sprite icon) GetCosmeticTypeCurrentIcon()
            {
                return CurrentCosmeticType switch
                {
                    CosmeticType.Icons => gameManager.GetProfilePicture(),
                    CosmeticType.Boards => gameManager.GetBoard(),
                    CosmeticType.Fund => gameManager.GetBoardFund(),
                    CosmeticType.Tiles => gameManager.GetTilePreview(),
                    _ => ("", defaultCosmeticPreviewImage)
                };
            }
        }
    }
}
