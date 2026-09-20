using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Shop
{
    public class ShopManager : SingleInstanceMonoBehaviour<ShopManager>, IService
    {
        private GameManager gameManager;
        private DictionaryService dictionaryService;
        private AnalyticsManager analyticsManager;
        private AuthManager authManager;

        private BackendBindings _module;
        private BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        public bool IsAlreadyInitialized { get; private set; }
        public bool IsAuthenticated => gameManager is not null and { IsAuthenticated: true };

        internal Dictionary<GameCosmeticData, PlayerCosmeticData> CosmeticDataCollection { get; private set; }

        private ShopUI _shopUI;
        internal ShopUI ShopUI
        {
            get
            {
                if (_shopUI == null)
                {
                    _shopUI = FindFirstObjectByType<ShopUI>();
                    if (_shopUI == null)
                        Debug.LogWarning($"{nameof(Shop.ShopUI)} not found in the scene");
                }

                return _shopUI;
            }
        }

        public uint PlayerTokenCurrencyAmount { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            CosmeticDataCollection = new();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();

            if (!gameManager || !authManager)
            {
                Debug.LogError("ShopManager: One or more required services are not available. Please ensure GameManager, DictionaryService, AuthManager and AnalyticsManager are initialized.");
                return;
            }

            ShopUI?.Awake_ShopUI();

            analyticsManager.Subscribe(OnUpdateCurrencyAmount);

            gameManager.HandleOnSignIn(async () =>
            {
                await RefreshData(false);
            });

            gameManager.HandleOnSignOut(async () =>
            {
                await RefreshData(false);
            });
        }

        private async void Start()
        {
            // Starts the ShopUI if it is not already started
            ShopUI?.Start_ShopUI();

            // Wait until the AuthManager and GameManager are initialized and the player is authenticated
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } && IsAuthenticated
                && gameManager is not null and { IsAlreadyInitialized: true });

            // Initialize the ShopUI with the cosmetic data collection and purchase function
            ShopUI?.Initialize
                (analyticsManager,
                dictionaryService,
                () => CosmeticDataCollection,
                () => PlayerTokenCurrencyAmount,
                PurchaseCosmetic);

            // Refresh the store data from the from the backend (using the GameManager)
            await RefreshData(false);

            ShopUI?.SelectFirstCategory();
        }

        private void Update()
        {
            // Only update if the player is authenticated or the ShopUI is not null
            if (authManager is null or { IsAlreadyInitialized: false } || !IsAuthenticated || ShopUI == null)
                return;

            ShopUI.Update_ShopUI();
        }


        private void OnDestroy()
        {
            // Unsubscribe from the analytics manager to avoid memory leaks
            if (analyticsManager != null)
                analyticsManager.Unsubscribe(OnUpdateCurrencyAmount);
        }

        private async UniTask RefreshData(bool shouldRefreshdata = true)
        {
            // Refresh player data from the backend
            if (shouldRefreshdata)
                await gameManager.RefreshProtectedPlayerData();

            // Initalize the cosmetic data collection with default values (null for each player cosmetic data)
            CosmeticDataCollection = gameManager.GameCosmeticData?.ToDictionary(x => x, x => default(PlayerCosmeticData));

            // Iterate for each player cosmetic data and check if the cosmetic data exists in the game cosmetics
            if (gameManager.PlayerCosmeticDatas is not null and { Length: > 0 })
                foreach (var playerCosmeticData in gameManager.PlayerCosmeticDatas)
                    if (gameManager.GameCosmeticData?.FirstOrDefault(x => x.id == playerCosmeticData.id) is GameCosmeticData matchedGameCosmetic)
                        CosmeticDataCollection[matchedGameCosmetic] = playerCosmeticData.Clone() as PlayerCosmeticData;

            // Filter out unavailable cosmetics. If the cosmetic is not available but has player data, keep it in the collection but preview it as purchased
            CosmeticDataCollection = CosmeticDataCollection
                ?.Where(x => x.Key.isAvailable || x.Value is not null)
                ?.ToDictionary(x => x.Key, x => x.Value);

            var currenciesCollection = gameManager.PlayerCurrencyCollection;
            if (currenciesCollection is not null && currenciesCollection.TryGetValue(Currency.Token, out var tokens))
                PlayerTokenCurrencyAmount = tokens;
            else
                Debug.LogWarning("Currency data is null or empty.");

            // Update the ShopUI with the cosmetic data collection
            if (ShopUI)
                ShopUI.Configure(PlayerTokenCurrencyAmount);
            else
                Debug.LogWarning("ShopUI is null. Cannot configure the UI with the refreshed data.");

        }
        /// <summary>
        /// Handles the purchase of a cosmetic by calling the backend module and updating the UI accordingly.
        /// </summary>
        /// <param name="cosmeticID"></param>
        /// <returns></returns>
        private async UniTask<PurchaseCosmeticResponse> PurchaseCosmetic(string cosmeticID)
        {
            if (string.IsNullOrEmpty(cosmeticID))
            {
                Debug.LogWarning("Cosmetic ID is null or empty. Cannot proceed with purchase.");
                return default;
            }

            var dataEncrypted = authManager.SerializeAndEncryptData(new()
            {
                ["cosmeticId"] = cosmeticID
            });

            var purchaseResult = await gameManager.HandleProcess_GameManagerProxy
                (uniTask: () => module.PurchaseCosmetic(dataEncrypted).AsUniTask(),
                taskId: nameof(module.PurchaseCosmetic),
                showLoading: true);

            if (purchaseResult == null)
            {
                Debug.LogWarning("Failed to deserialize Purchase data response.");
                return default;
            }

            // Deserialize and decrypt the purchase result
            var purchaseResponse = authManager.DeserializeAndDecryptData<PurchaseCosmeticResponse>(purchaseResult);
            if (purchaseResponse is null or { cosmeticPurchased: null })
            {
                Debug.LogWarning("Failed to deserialize Purchase data response.");
                return default;
            }

            // Validate which cosmetic was purchased and update the UI accordingly
            ShopUI?.ValidatePurchasedCosmetic(purchaseResponse.cosmeticPurchased);
            ShopUI?.CloseConfirmationPopUp();

            analyticsManager?.SendAnalytic(AnalyticType.ObtainCosmetic);
            return purchaseResponse;
        }

        /// <summary>
        /// Handles the update of the currency amount when a token is modified.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        private void OnUpdateCurrencyAmount(AnalyticType type, object data)
        {
            if (type is not AnalyticType.OnTokenModified and not AnalyticType.ObtainCosmetic)
                return;

            RefreshData().Forget();
        }
    }
}
