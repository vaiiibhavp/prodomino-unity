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
#if UNITY_EDITOR
            var savedMockPurchases = gameManager.PlayerCosmeticDatas?.ToArray();
#endif
            // Refresh player data from the backend
            if (shouldRefreshdata)
                await gameManager.RefreshProtectedPlayerData();

#if UNITY_EDITOR
            if (savedMockPurchases is not null and { Length: > 0 })
                foreach (var mock in savedMockPurchases)
                    gameManager.AddMockPurchasedCosmetic(mock);
#endif

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
                AddFallbackCosmetics(CosmeticType.Tiles, tileCosmetics);
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
                AddFallbackCosmetics(CosmeticType.Boards, boardCosmetics);
            }

            // Frames tab (CosmeticType.Fund): board felt skins
            if (!CosmeticDataCollection.Keys.Any(x => x.type == CosmeticType.Fund))
            {
                var fundCosmetics = new (string id, string name, CosmeticRarity rarity, uint price)[]
                {
                    ("Fund_Default", "Classic Frame", CosmeticRarity.Common, 0),
                    ("tablefund_grey", "Slate Frame", CosmeticRarity.Common, 10),
                    ("tablefund_orange", "Amber Frame", CosmeticRarity.Rare, 30),
                    ("tablefund_red", "Ruby Frame", CosmeticRarity.Legendary, 90),
                    ("tablefund_darkgrey", "Graphite Frame", CosmeticRarity.Mythic, 150),
                    ("tablefund_lightblue", "Sky Frame", CosmeticRarity.Special, 220),
                };
                AddFallbackCosmetics(CosmeticType.Fund, fundCosmetics);
            }

            // Icons: every profile picture in the sprite dictionary, the two default avatars owned for free
            if (!CosmeticDataCollection.Keys.Any(x => x.type == CosmeticType.Icons))
            {
                var iconCosmetics = dictionaryService?.GetSpriteDataCollection(CosmeticType.Icons.ToString())
                    ?.Keys
                    .Select(id =>
                    {
                        var isDefault = id.StartsWith($"{CosmeticType.Icons}_{Consts.CollectionKeys.Default}");
                        var gender = id.Contains("_f_") || id.EndsWith("Female") ? "Female" : "Male";
                        var name = isDefault ? $"Default {gender} Avatar" : $"{gender} Avatar {id.Substring(id.LastIndexOf('_') + 1)}";
                        return (id, name, CosmeticRarity.Common, isDefault ? 0u : 10u);
                    })
                    .ToArray();

                if (iconCosmetics is not null and { Length: > 0 })
                    AddFallbackCosmetics(CosmeticType.Icons, iconCosmetics);
            }

            // Badges are earned through achievements, never bought: completed ones show as owned, the rest stay locked
            if (!CosmeticDataCollection.Keys.Any(x => x.type == CosmeticType.Badges) && gameManager.GameAchievementData is not null)
                foreach (var gameAchievementData in gameManager.GameAchievementData)
                {
                    if (gameAchievementData is null or { achievementType: AchievementType.None })
                        continue;

                    var id = gameAchievementData.achievement.ToString();
                    var playerAchievementData = gameManager.PlayerAchievementDatas?.FirstOrDefault(x => x.achievement == gameAchievementData.achievement);
                    var gcd = new GameCosmeticData(id, gameAchievementData.name ?? id, gameAchievementData.description, CosmeticType.Badges, CosmeticRarity.Common, false, 0, Currency.None);
                    CosmeticDataCollection[gcd] = playerAchievementData is { completed: true }
                        ? new PlayerCosmeticData(id, playerAchievementData.completedTime ?? 0, 0, Currency.None, CosmeticPurchaseMethod.Achievement)
                        : null;
                }

            CosmeticDataCollection = RemoveDuplicateSprites(CosmeticDataCollection);

            var currenciesCollection = gameManager.PlayerCurrencyCollection;
            if (currenciesCollection is not null && currenciesCollection.TryGetValue(Currency.Token, out var tokens))
                PlayerTokenCurrencyAmount = tokens;
#if UNITY_EDITOR
            else
            {
                PlayerTokenCurrencyAmount = 9999;
                Debug.Log("[ShopManager] Currency data unavailable in Editor — using fallback 9999 tokens");
            }
#else
            else
                Debug.LogWarning("Currency data is null or empty.");
#endif

            // Update the ShopUI with the cosmetic data collection
            if (ShopUI)
                ShopUI.Configure(PlayerTokenCurrencyAmount);
            else
                Debug.LogWarning("ShopUI is null. Cannot configure the UI with the refreshed data.");

        }
        /// <summary>
        /// Adds locally defined cosmetics for a category the backend returned nothing for, so its tab is not empty.
        /// Free entries are granted as defaults.
        /// </summary>
        private void AddFallbackCosmetics(CosmeticType type, (string id, string name, CosmeticRarity rarity, uint price)[] cosmetics)
        {
            foreach (var c in cosmetics)
            {
                var gcd = new GameCosmeticData(c.id, c.name, c.name, type, c.rarity, true, c.price, Currency.Token);
                CosmeticDataCollection[gcd] = c.price == 0 ? new PlayerCosmeticData(c.id, 0, 0, Currency.None, CosmeticPurchaseMethod.Default) : null;
            }
        }

        /// <summary>
        /// Several cosmetics resolve to the same sprite, most notably the "*_Default" entry, which points at
        /// the same asset as the first real skin of its category. The shop listed both, so the same artwork
        /// appeared twice in a tab. Keep one entry per sprite: the one the player already owns, otherwise the
        /// cheapest one, so the free default wins over its paid twin.
        /// </summary>
        private Dictionary<GameCosmeticData, PlayerCosmeticData> RemoveDuplicateSprites(Dictionary<GameCosmeticData, PlayerCosmeticData> collection)
        {
            if (collection is null or { Count: 0 } || dictionaryService == null)
                return collection;

            return collection
                // Badges are one per achievement, never duplicates, even when two achievements share an icon
                .GroupBy(entry => (entry.Key.type, sprite: entry.Key.type is CosmeticType.Badges ? null : dictionaryService.GetSprite(entry.Key.type.ToString(), entry.Key.id)))
                .SelectMany(group => group.Key.sprite == null
                    ? group
                    : group
                        .OrderByDescending(entry => entry.Value is not null)
                        .ThenBy(entry => entry.Key.price)
                        .Take(1))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
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
