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
#if UNITY_EDITOR
            // DEBUG: timeout after 3s in Editor so shop UI initializes even without backend auth
            var cts = new System.Threading.CancellationTokenSource();
            cts.CancelAfter(3000);
            try
            {
                await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } && IsAuthenticated
                    && gameManager is not null and { IsAlreadyInitialized: true }, cancellationToken: cts.Token);
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[ShopManager] Auth wait timed out in Editor — initializing with fallback data");
            }
            cts.Dispose();
#else
            await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true } && IsAuthenticated
                && gameManager is not null and { IsAlreadyInitialized: true });
#endif

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
#if !UNITY_EDITOR
            if (authManager is null or { IsAlreadyInitialized: false } || !IsAuthenticated || ShopUI == null)
#else
            if (ShopUI == null)
#endif
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

            CosmeticDataCollection ??= new();
            if (!CosmeticDataCollection.Keys.Any(x => x.type == CosmeticType.Tiles))
            {
                var tileCosmetics = new (string id, string name, CosmeticRarity rarity, uint price)[]
                {
                    ("Tiles_Default", "White Tile", CosmeticRarity.Common, 0),
                    ("tiles_orange", "Gold Tile", CosmeticRarity.Common, 5),
                    ("tiles_pink", "Pink Tile", CosmeticRarity.Mythic, 35),
                    ("tiles_black", "Black Tile", CosmeticRarity.Legendary, 105),
                    ("tiles_rainbow", "Special Tile", CosmeticRarity.Special, 250),
                };
                foreach (var t in tileCosmetics)
                {
                    var gcd = new GameCosmeticData(t.id, t.name, t.name, CosmeticType.Tiles, t.rarity, true, t.price, Currency.Token);
                    CosmeticDataCollection[gcd] = t.price == 0 ? new PlayerCosmeticData(t.id, 0, 0, Currency.None, CosmeticPurchaseMethod.Default) : null;
                }
            }

            if (!CosmeticDataCollection.Keys.Any(x => x.type == CosmeticType.Boards))
            {
                var boardCosmetics = new (string id, string name, CosmeticRarity rarity, uint price)[]
                {
                    ("Boards_Default", "Classic Board", CosmeticRarity.Common, 0),
                    ("table_green", "Emerald Board", CosmeticRarity.Common, 15),
                    ("table_grey", "Slate Board", CosmeticRarity.Rare, 40),
                    ("table_orange", "Amber Board", CosmeticRarity.Legendary, 120),
                    ("table_pink", "Ruby Board", CosmeticRarity.Mythic, 200),
                };
                foreach (var b in boardCosmetics)
                {
                    var gcd = new GameCosmeticData(b.id, b.name, b.name, CosmeticType.Boards, b.rarity, true, b.price, Currency.Token);
                    CosmeticDataCollection[gcd] = b.price == 0 ? new PlayerCosmeticData(b.id, 0, 0, Currency.None, CosmeticPurchaseMethod.Default) : null;
                }
            }

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
