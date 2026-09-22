using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.Shop;
using ProDomino.Shared;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles Shop_Screen.prefab and Shop_Element.prefab to strictly match
    /// the Figma reference (node 44:4, media_1790059786035.png):
    /// - Header: Golden Shop Icon + "Shop" title grouped together on top left
    /// - Tab Bar & Filters Row:
    ///     Left: [ Tiles | Icons | Frames | Boards | Badges ] interactive segmented pill tabs
    ///           Selected tab ("Tiles" by default) has bright blue pill background with white text
    ///           Unselected tabs have subtle dark background with muted text
    ///     Right: [ All v ] rarity dropdown pill with chevron down icon
    ///            Under the hood, preserves all 7 rarity button triggers (All, Common, Uncommon, Rare, Special, Legendary, Mythic)
    /// - Item Cards Grid (ShopElement):
    ///     Responsive 5-column grid in a ScrollView
    ///     Each card (172 x 225):
    ///         - Dark rounded card background (#0E1320 -> #080B14) with subtle slate border
    ///         - Top-right: Colored rarity badge pill (Common: Blue, Mythic: Purple/Pink, Legendary: Gold, Special: Magenta)
    ///         - Center: Domino cosmetic preview sprite
    ///         - Bottom: Pill bar with gold Domino Coin icon + price or "Purchase"/"Purchased" text
    ///         - Entire card is clickable to open the confirmation popup
    /// - Confirm Purchase Pop-Up:
    ///     Modal dialog with dark backdrop, preview image, item name, cost, Confirm & Cancel buttons
    /// </summary>
    internal static class ShopRestyler
    {
        private const string MainPrefabPath = "Assets/_ProDomino/Prefabs/UI/Shop_Screen.prefab";
        private const string ElementPrefabPath = "Assets/_ProDomino/Shop/Prefabs/Shop_Element.prefab";
        private const string DashboardIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";
        private const string Base64IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Base_64";
        private const string ArtDashboardDir = "Assets/_ProDomino/_Art/Dashboard";
        private const string TilesArtDir = "Assets/_ProDomino/_Art/Tiles_Domino";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite shopIcon, coinIcon, chevronDown;
        private static Sprite cardBg, cardFooterBg, tabActiveBg, tabInactiveBg, dropdownPillBg, btnBuyPill, screenCardBg;
        private static Sprite badgeCommon, badgeMythic, badgeLegendary, badgeSpecial;
        private static Sprite popupPanelBg, btnGoldConfirm, btnDarkCancel;

        // Preview domino sprites for template items
        private static Sprite tileDefault, tileOrange, tilePink, tileBlack, tileRainbow;

        [MenuItem("ProDomino/Dashboard/Restyle Shop + Render")]
        public static void ApplyAndRender()
        {
            PrepareAssets();
            RestyleElementPrefab();
            BuildCleanShopScreenPrefab();
            EnsureHiddenInMiddleScreen();
            AssetDatabase.SaveAssets();
            Debug.Log("[ShopRestyler] SUCCESS: Shop screen completely restyled to Figma design and hidden by default!");
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            if (!SessionState.GetBool("PD_ShopRestyler_Ran_v4", false))
            {
                SessionState.SetBool("PD_ShopRestyler_Ran_v4", true);
                EditorApplication.delayCall += () =>
                {
                    ApplyAndRender();
                };
            }
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            shopIcon = EnsureSprite($"{DashboardIconsDir}/Nav_Shop.png");
            chevronDown = EnsureSprite($"{Base64IconsDir}/Arrow_Dropdown_Icon.png");
            coinIcon = EnsureSprite($"{ArtDashboardDir}/Icon_Coin_Raster.png")
                       ?? EnsureSprite($"{DashboardIconsDir}/Icon_Coin.png");

            // Preview domino tiles for initial card templates (Domino/Tile_Default_27.png)
            tileDefault = EnsureSprite($"{TilesArtDir}/Domino/Tile_Default_27.png");
            tileOrange = EnsureSprite($"{TilesArtDir}/Tiles_Orange/Tile_Orange_27.png");
            tilePink = EnsureSprite($"{TilesArtDir}/Tiles_Pink/Tile_Pink_27.png");
            tileBlack = EnsureSprite($"{TilesArtDir}/Tiles_Black/Tile_Black_27.png");
            tileRainbow = EnsureSprite($"{TilesArtDir}/Tiles_Rainbow/Tile_Rainbow_27.png");

            // UI Sprites
            screenCardBg = GetOrCreateScreenCardSprite();
            cardBg = MakePanelSprite("Shop_CardBg", 48, 64, 12, Hex("#0D111C"), Hex("#080B14"), Hex("#1E273A"), 1f);
            cardFooterBg = MakeBottomRoundedSprite("Shop_CardFooterBg", 32, 38, 12, Hex("#1E2536"), Hex("#283246"), 1f);
            tabActiveBg = MakePanelSprite("Shop_TabActiveBg", 32, 32, 10, Hex("#3B82F6"), Hex("#1D4ED8"), Hex("#60A5FA"), 1f);
            tabInactiveBg = MakePanelSprite("Shop_TabInactiveBg", 32, 32, 10, Hex("#0C101C"), Hex("#080B14"), Color.clear, 0f);
            dropdownPillBg = MakePanelSprite("Shop_DropdownPillBg", 32, 32, 10, Hex("#121827"), Hex("#0B101D"), Hex("#222E46"), 1f);
            btnBuyPill = MakePanelSprite("Shop_BtnBuyPill", 32, 32, 8, Hex("#151D2D"), Hex("#0E1320"), Hex("#222E46"), 1f);

            // Rarity badges (Matching Figma: vibrant solid badges with dark text)
            badgeCommon = MakePanelSprite("Shop_Badge_Common", 32, 18, 9, Hex("#B2C2D8"), Hex("#B2C2D8"), Color.clear, 0f);
            badgeMythic = MakePanelSprite("Shop_Badge_Mythic", 32, 18, 9, Hex("#C084FC"), Hex("#C084FC"), Color.clear, 0f);
            badgeLegendary = MakePanelSprite("Shop_Badge_Legendary", 32, 18, 9, Hex("#FBBF24"), Hex("#FBBF24"), Color.clear, 0f);
            badgeSpecial = MakePanelSprite("Shop_Badge_Special", 32, 18, 9, Hex("#FB923C"), Hex("#FB923C"), Color.clear, 0f);

            popupPanelBg = MakePanelSprite("Shop_PopupBg", 48, 48, 14, Hex("#0E1322"), Hex("#080C16"), Hex("#1E293B"), 1.2f);
            btnGoldConfirm = MakePanelSprite("Shop_BtnGoldConfirm", 32, 32, 10, Hex("#FBBF24"), Hex("#F59E0B"), Color.clear, 0f);
            btnDarkCancel = MakePanelSprite("Shop_BtnDarkCancel", 32, 32, 10, Hex("#1E2638"), Hex("#141A28"), Hex("#2E3C54"), 1f);
        }

        private static Sprite EnsureSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // =============================================================================================================
        // 1. RESTYLE SHOP ELEMENT CARD PREFAB (Shop_Element.prefab)
        // =============================================================================================================
        private static void RestyleElementPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ElementPrefabPath);
            try
            {
                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(172f, 225f);

                // Root card background
                var rootImg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                rootImg.sprite = cardBg;
                rootImg.type = Image.Type.Sliced;
                rootImg.color = Color.white;
                rootImg.raycastTarget = true;

                var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // Clickable button on the whole card
                var rootBtn = root.GetComponent<Button>() ?? root.AddComponent<Button>();
                var customBtn = root.GetComponent<CustomButtonUI>() ?? root.AddComponent<CustomButtonUI>();
                var btnSo = new SerializedObject(customBtn);
                btnSo.FindProperty("isToggleable").boolValue = false;
                btnSo.FindProperty("isInteractable").boolValue = true;
                btnSo.ApplyModifiedPropertiesWithoutUndo();

                var elem = root.GetComponent<ShopElement>() ?? root.AddComponent<ShopElement>();

                // Clear old hierarchy cleanly
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // -----------------------------------------------------------------
                // Rarity Badge Pill (Top-Right: anchor (1,1), pivot (1,1))
                // -----------------------------------------------------------------
                var badgeGo = CreateExplicitRect(root.transform, "Rarity_Badge", 1f, 1f, 1f, 1f);
                badgeGo.pivot = new Vector2(1f, 1f);
                badgeGo.anchoredPosition = new Vector2(-6f, -6f);
                badgeGo.sizeDelta = new Vector2(58f, 18f);

                var badgeImg = badgeGo.gameObject.AddComponent<Image>();
                badgeImg.sprite = badgeCommon;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = Color.white;
                badgeImg.raycastTarget = false;

                var badgeTmp = CreateExplicitText(badgeGo, "BadgeText", "Common", fBold, 10.5f, Hex("#0F172A"), TextAlignmentOptions.Center);
                badgeTmp.textWrappingMode = TextWrappingModes.NoWrap;

                // -----------------------------------------------------------------
                // Center Cosmetic Item Preview Image
                // -----------------------------------------------------------------
                var itemContainer = CreateExplicitRect(root.transform, "ItemContainer", 0f, 0f, 1f, 1f);
                itemContainer.offsetMin = new Vector2(0f, 38f);
                itemContainer.offsetMax = new Vector2(0f, 0f);

                var previewImgGo = CreateExplicitRect(itemContainer, "Element_Image", 0.5f, 0.5f, 0.5f, 0.5f);
                previewImgGo.pivot = new Vector2(0.5f, 0.5f);
                previewImgGo.sizeDelta = new Vector2(88f, 134f);
                previewImgGo.anchoredPosition = new Vector2(0f, -2f);

                var elementImageComp = previewImgGo.gameObject.AddComponent<Image>();
                elementImageComp.sprite = tileDefault;
                elementImageComp.preserveAspect = true;
                elementImageComp.raycastTarget = false;

                // -----------------------------------------------------------------
                // Bottom Bar: Full-width [Coin] [Price / "Purchase"] Bar
                // -----------------------------------------------------------------
                var bottomBar = CreateExplicitRect(root.transform, "Bottom_Bar", 0f, 0f, 1f, 0f);
                bottomBar.pivot = new Vector2(0.5f, 0f);
                bottomBar.offsetMin = new Vector2(0f, 0f);
                bottomBar.offsetMax = new Vector2(0f, 38f);
                bottomBar.sizeDelta = new Vector2(0f, 38f);

                var bottomBarImg = bottomBar.gameObject.AddComponent<Image>();
                bottomBarImg.sprite = cardFooterBg;
                bottomBarImg.type = Image.Type.Sliced;
                bottomBarImg.color = Color.white;
                bottomBarImg.raycastTarget = false;

                // Centered price group inside bottom bar
                var priceGroup = CreateExplicitRect(bottomBar, "PriceGroup", 0f, 0f, 1f, 1f);
                var hlg = priceGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 6f;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                // Coin icon
                var coinGo = CreateExplicitRect(priceGroup, "CoinIcon", 0.5f, 0.5f, 0.5f, 0.5f);
                coinGo.sizeDelta = new Vector2(16f, 16f);

                var coinImg = coinGo.gameObject.AddComponent<Image>();
                coinImg.sprite = coinIcon;
                coinImg.preserveAspect = true;
                coinImg.raycastTarget = false;

                // Cost / Status Label
                var costTmp = CreateExplicitText(priceGroup, "Cost_Label", "Purchase", fBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                var costCsf = costTmp.gameObject.AddComponent<ContentSizeFitter>();
                costCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                costCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // -----------------------------------------------------------------
                // Wire Serialized Properties on ShopElement
                // -----------------------------------------------------------------
                var elemSo = new SerializedObject(elem);
                elemSo.FindProperty("canvasGroup").objectReferenceValue = cg;
                elemSo.FindProperty("costLabel").objectReferenceValue = costTmp;
                elemSo.FindProperty("elementImage").objectReferenceValue = elementImageComp;
                elemSo.FindProperty("openConfirmationPopUpButton").objectReferenceValue = customBtn;
                elemSo.FindProperty("grayedColor").colorValue = new Color(0.45f, 0.45f, 0.5f, 0.75f);

                elemSo.FindProperty("rarityBadgeImage").objectReferenceValue = badgeImg;
                elemSo.FindProperty("rarityBadgeLabel").objectReferenceValue = badgeTmp;
                elemSo.FindProperty("badgeCommonSprite").objectReferenceValue = badgeCommon;
                elemSo.FindProperty("badgeMythicSprite").objectReferenceValue = badgeMythic;
                elemSo.FindProperty("badgeLegendarySprite").objectReferenceValue = badgeLegendary;
                elemSo.FindProperty("badgeSpecialSprite").objectReferenceValue = badgeSpecial;

                var avoidProp = elemSo.FindProperty("imagesToAvoidGrayedOut");
                avoidProp.arraySize = 4;
                avoidProp.GetArrayElementAtIndex(0).objectReferenceValue = rootImg;
                avoidProp.GetArrayElementAtIndex(1).objectReferenceValue = bottomBarImg;
                avoidProp.GetArrayElementAtIndex(2).objectReferenceValue = coinImg;
                avoidProp.GetArrayElementAtIndex(3).objectReferenceValue = badgeImg;

                elemSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ElementPrefabPath);
                Debug.Log("[ShopRestyler] Shop_Element prefab restyled successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =============================================================================================================
        // 2. BUILD CLEAN SHOP SCREEN PREFAB (Shop_Screen.prefab)
        // =============================================================================================================
        private static void BuildCleanShopScreenPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(MainPrefabPath);
            try
            {
                var rootRt = root.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                if (root.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlg)
                    UnityEngine.Object.DestroyImmediate(vlg);

                // Framed Screen Card Background (1px #1E2538 border, 14px rounded corners, deep midnight card fill)
                var bg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                bg.sprite = screenCardBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                var shopUI = root.GetComponent<ShopUI>() ?? root.AddComponent<ShopUI>();

                // Clear previous children
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // Main Content Container (Padded inside the rounded card)
                var mainContent = CreateExplicitRect(root.transform, "MainContent", 0f, 0f, 1f, 1f);
                mainContent.offsetMin = new Vector2(24f, 20f);
                mainContent.offsetMax = new Vector2(-24f, -16f);

                // -----------------------------------------------------------------
                // 1. Header Section: [Shop Icon] Shop (Top-Left) + [Tokens Badge] (Top-Right)
                // -----------------------------------------------------------------
                var headerSection = CreateExplicitRect(mainContent, "Header_Section", 0f, 1f, 1f, 1f);
                headerSection.pivot = new Vector2(0f, 1f);
                headerSection.sizeDelta = new Vector2(0f, 36f);
                headerSection.anchoredPosition = new Vector2(0f, 0f);

                var shopIconGo = CreateExplicitRect(headerSection, "ShopIcon", 0f, 0.5f, 0f, 0.5f);
                shopIconGo.pivot = new Vector2(0f, 0.5f);
                shopIconGo.sizeDelta = new Vector2(26f, 26f);
                shopIconGo.anchoredPosition = new Vector2(0f, 0f);
                var shopIconImg = shopIconGo.gameObject.AddComponent<Image>();
                shopIconImg.sprite = shopIcon;
                shopIconImg.color = Hex("#FDC553");
                shopIconImg.preserveAspect = true;

                var titleText = CreateExplicitText(headerSection, "TitleText", "Shop", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                var ttRt = titleText.GetComponent<RectTransform>();
                ttRt.pivot = new Vector2(0f, 0.5f);
                ttRt.anchorMin = new Vector2(0f, 0.5f);
                ttRt.anchorMax = new Vector2(0f, 0.5f);
                ttRt.anchoredPosition = new Vector2(34f, 0f);
                ttRt.sizeDelta = new Vector2(200f, 32f);

                // Currency Badge (Top-Right)
                var currencyBadge = CreateExplicitRect(headerSection, "Currency_Badge", 1f, 0.5f, 1f, 0.5f);
                currencyBadge.pivot = new Vector2(1f, 0.5f);
                currencyBadge.anchoredPosition = new Vector2(0f, 0f);
                currencyBadge.sizeDelta = new Vector2(110f, 34f);

                var cbImg = currencyBadge.gameObject.AddComponent<Image>();
                cbImg.sprite = dropdownPillBg;
                cbImg.type = Image.Type.Sliced;
                cbImg.color = Color.white;

                var cbCoin = CreateExplicitRect(currencyBadge, "CoinIcon", 0f, 0.5f, 0f, 0.5f);
                cbCoin.pivot = new Vector2(0f, 0.5f);
                cbCoin.anchoredPosition = new Vector2(10f, 0f);
                cbCoin.sizeDelta = new Vector2(20f, 20f);
                var cbCoinImg = cbCoin.gameObject.AddComponent<Image>();
                cbCoinImg.sprite = coinIcon;
                cbCoinImg.preserveAspect = true;

                var tokenTmp = CreateExplicitText(currencyBadge, "TokenAmountText", "380", fBold, 14f, Hex("#FDC553"), TextAlignmentOptions.MidlineLeft);
                var tokenRt = tokenTmp.GetComponent<RectTransform>();
                tokenRt.anchorMin = new Vector2(0f, 0f);
                tokenRt.anchorMax = new Vector2(1f, 1f);
                tokenRt.offsetMin = new Vector2(36f, 0f);
                tokenRt.offsetMax = new Vector2(-8f, 0f);

                // -----------------------------------------------------------------
                // 2. Tab Bar & Filter Row (y = -48px, height 40px)
                // -----------------------------------------------------------------
                var tabRow = CreateExplicitRect(mainContent, "Tab_Row", 0f, 1f, 1f, 1f);
                tabRow.pivot = new Vector2(0.5f, 1f);
                tabRow.sizeDelta = new Vector2(0f, 40f);
                tabRow.anchoredPosition = new Vector2(0f, -48f);

                // 2a. Left: 5 Category Segmented Tabs
                var tabsContainer = CreateExplicitRect(tabRow, "Category_Buttons_Container", 0f, 0f, 0f, 1f);
                tabsContainer.pivot = new Vector2(0f, 0.5f);
                tabsContainer.anchoredPosition = new Vector2(0f, 0f);
                tabsContainer.sizeDelta = new Vector2(560f, 0f);

                var tabsHlg = tabsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                tabsHlg.childAlignment = TextAnchor.MiddleLeft;
                tabsHlg.spacing = 6f;
                tabsHlg.childControlWidth = true;
                tabsHlg.childControlHeight = true;
                tabsHlg.childForceExpandWidth = true;
                tabsHlg.childForceExpandHeight = true;

                var toggleGroup = tabsContainer.gameObject.AddComponent<CustomButtonToggleGroupUI>();

                // Build 5 tabs: Tiles, Icons, Frames (Fund), Boards, Badges
                var tabTiles = CreateCategoryTab(tabsContainer, "Tiles_CustomButton", "Tiles", "Tiles", true);
                var tabIcons = CreateCategoryTab(tabsContainer, "Icons_CustomButton", "Icons", "Icons", false);
                var tabFrames = CreateCategoryTab(tabsContainer, "Board_Fund_CustomButton", "Fund", "Frames", false);
                var tabBoards = CreateCategoryTab(tabsContainer, "Board_CustomButton", "Boards", "Boards", false);
                var tabBadges = CreateCategoryTab(tabsContainer, "Badges_CustomButton", "Badges", "Badges", false);

                // 2b. Right: "All" Rarity Dropdown Pill
                var rarityDropdown = CreateExplicitRect(tabRow, "Rarity_Dropdown_Container", 1f, 0.5f, 1f, 0.5f);
                rarityDropdown.pivot = new Vector2(1f, 0.5f);
                rarityDropdown.anchoredPosition = new Vector2(0f, 0f);
                rarityDropdown.sizeDelta = new Vector2(130f, 38f);

                var rdImg = rarityDropdown.gameObject.AddComponent<Image>();
                rdImg.sprite = dropdownPillBg;
                rdImg.type = Image.Type.Sliced;
                rdImg.color = Color.white;

                var allLabel = CreateExplicitText(rarityDropdown, "Label", "All", fSemiBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                var allRt = allLabel.GetComponent<RectTransform>();
                allRt.anchorMin = new Vector2(0f, 0f);
                allRt.anchorMax = new Vector2(1f, 1f);
                allRt.offsetMin = new Vector2(16f, 0f);
                allRt.offsetMax = new Vector2(-32f, 0f);

                var chevron = CreateExplicitRect(rarityDropdown, "Chevron", 1f, 0.5f, 1f, 0.5f);
                chevron.pivot = new Vector2(1f, 0.5f);
                chevron.anchoredPosition = new Vector2(-12f, 0f);
                chevron.sizeDelta = new Vector2(14f, 14f);
                var chevImg = chevron.gameObject.AddComponent<Image>();
                chevImg.sprite = chevronDown;
                chevImg.color = Hex("#8E9CAE");
                chevImg.preserveAspect = true;

                // Make the dropdown container clickable as the "All" rarity button
                var allBtnComp = rarityDropdown.gameObject.AddComponent<Button>();
                var allCustomBtn = rarityDropdown.gameObject.AddComponent<CustomButtonUI>();
                var allSo = new SerializedObject(allCustomBtn);
                allSo.FindProperty("toggleID").stringValue = CosmeticRarity.None.ToString();
                allSo.FindProperty("isToggleable").boolValue = true;
                allSo.ApplyModifiedPropertiesWithoutUndo();

                // Hidden Container for remaining rarity filter buttons so ShopUI logic works flawlessly
                var hiddenFilters = CreateExplicitRect(rarityDropdown, "Hidden_Rarity_Filters", 0f, 0f, 0f, 0f);
                hiddenFilters.sizeDelta = Vector2.zero;
                hiddenFilters.gameObject.SetActive(false);

                var rarityBtnList = new List<CustomButtonUI> { allCustomBtn };
                string[] otherRarities = new string[] { "Common", "Uncommon", "Rare", "Special", "Legendary", "Mythic" };
                foreach (var rName in otherRarities)
                {
                    var rGo = new GameObject($"{rName}_CustomButton", typeof(RectTransform), typeof(Button), typeof(CustomButtonUI));
                    rGo.transform.SetParent(hiddenFilters, false);
                    var rCb = rGo.GetComponent<CustomButtonUI>();
                    var rSo = new SerializedObject(rCb);
                    rSo.FindProperty("toggleID").stringValue = rName;
                    rSo.FindProperty("isToggleable").boolValue = true;
                    rSo.ApplyModifiedPropertiesWithoutUndo();
                    rarityBtnList.Add(rCb);
                }

                // -----------------------------------------------------------------
                // 3. Main Item Cards Scroll View & Grid (y = -100px downwards)
                // -----------------------------------------------------------------
                var scrollGo = CreateExplicitRect(mainContent, "Scroll_View_Items", 0f, 0f, 1f, 1f);
                scrollGo.offsetMin = new Vector2(0f, 0f);
                scrollGo.offsetMax = new Vector2(0f, -100f);

                var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                var viewport = CreateExplicitRect(scrollGo, "Viewport", 0f, 0f, 1f, 1f);
                viewport.gameObject.AddComponent<RectMask2D>();
                scrollRect.viewport = viewport;

                var content = CreateExplicitRect(viewport, "Content", 0f, 1f, 1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.sizeDelta = new Vector2(0f, 500f);
                scrollRect.content = content;

                var glg = content.gameObject.AddComponent<GridLayoutGroup>();
                glg.cellSize = new Vector2(172f, 225f);
                glg.spacing = new Vector2(16f, 16f);
                glg.padding = new RectOffset(4, 4, 4, 16);
                glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
                glg.startAxis = GridLayoutGroup.Axis.Horizontal;
                glg.childAlignment = TextAnchor.UpperLeft;
                glg.constraint = GridLayoutGroup.Constraint.Flexible;

                var csf = content.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Instantiate initial template items for edit-mode visualization
                var elemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElementPrefabPath);
                if (elemPrefab != null)
                {
                    CreateTemplateCard(content, elemPrefab, "Item_1", tileDefault, "Common", badgeCommon, Hex("#0F172A"), "Purchase", true);
                    CreateTemplateCard(content, elemPrefab, "Item_2", tileOrange, "Common", badgeCommon, Hex("#0F172A"), "5", false);
                    CreateTemplateCard(content, elemPrefab, "Item_3", tilePink, "Mythic", badgeMythic, Hex("#3B0764"), "35", false);
                    CreateTemplateCard(content, elemPrefab, "Item_4", tileBlack, "Legendary", badgeLegendary, Hex("#451A03"), "105", false);
                    CreateTemplateCard(content, elemPrefab, "Item_5", tileRainbow, "Special", badgeSpecial, Hex("#431407"), "250", false);
                }

                // -----------------------------------------------------------------
                // 4. Confirm Purchase Modal Pop-Up
                // -----------------------------------------------------------------
                var popupGo = CreateExplicitRect(root.transform, "ConfirmPurchase_PopUp", 0f, 0f, 1f, 1f);
                popupGo.offsetMin = Vector2.zero;
                popupGo.offsetMax = Vector2.zero;

                var popupCg = popupGo.gameObject.AddComponent<CanvasGroup>();
                popupCg.alpha = 0f;
                popupCg.interactable = false;
                popupCg.blocksRaycasts = false;
                popupGo.gameObject.SetActive(false);

                // Dark raycast blocker
                var backdrop = CreateExplicitRect(popupGo, "Panel", 0f, 0f, 1f, 1f);
                var bdImg = backdrop.gameObject.AddComponent<Image>();
                bdImg.color = new Color(0f, 0f, 0f, 0.85f);
                bdImg.raycastTarget = true;

                // Modal Card Container
                var modal = CreateExplicitRect(popupGo, "ConfirmPurchase_Container", 0.5f, 0.5f, 0.5f, 0.5f);
                modal.pivot = new Vector2(0.5f, 0.5f);
                modal.sizeDelta = new Vector2(380f, 440f);
                modal.anchoredPosition = Vector2.zero;

                var modalImg = modal.gameObject.AddComponent<Image>();
                modalImg.sprite = popupPanelBg;
                modalImg.type = Image.Type.Sliced;
                modalImg.color = Color.white;

                // Title / Header Text
                var popupHeaderTmp = CreateExplicitText(modal, "ConfirmPurchase_HeaderText", "Purchase Cosmetic", fBold, 18f, Color.white, TextAlignmentOptions.Center);
                var pHeaderRt = popupHeaderTmp.GetComponent<RectTransform>();
                pHeaderRt.anchorMin = new Vector2(0f, 1f);
                pHeaderRt.anchorMax = new Vector2(1f, 1f);
                pHeaderRt.pivot = new Vector2(0.5f, 1f);
                pHeaderRt.anchoredPosition = new Vector2(0f, -20f);
                pHeaderRt.sizeDelta = new Vector2(-40f, 32f);

                // Preview Box
                var previewBox = CreateExplicitRect(modal, "PreviewBox", 0.5f, 0.5f, 0.5f, 0.5f);
                previewBox.pivot = new Vector2(0.5f, 0.5f);
                previewBox.anchoredPosition = new Vector2(0f, 36f);
                previewBox.sizeDelta = new Vector2(160f, 180f);

                var previewBoxImg = previewBox.gameObject.AddComponent<Image>();
                previewBoxImg.sprite = cardBg;
                previewBoxImg.type = Image.Type.Sliced;

                var previewImgGo = CreateExplicitRect(previewBox, "ConfirmPurchase_ImagePreview", 0.5f, 0.5f, 0.5f, 0.5f);
                previewImgGo.pivot = new Vector2(0.5f, 0.5f);
                previewImgGo.sizeDelta = new Vector2(96f, 128f);
                var previewImg = previewImgGo.gameObject.AddComponent<Image>();
                previewImg.sprite = tileDefault;
                previewImg.preserveAspect = true;

                // Description Label
                var descTmp = CreateExplicitText(modal, "ConfirmPurchase_DescriptionText", "Equip this stylish custom domino tile in all games!", fRegular, 12f, Hex("#8E9CAE"), TextAlignmentOptions.Center);
                var descRt = descTmp.GetComponent<RectTransform>();
                descRt.anchorMin = new Vector2(0f, 0.5f);
                descRt.anchorMax = new Vector2(1f, 0.5f);
                descRt.pivot = new Vector2(0.5f, 0.5f);
                descRt.anchoredPosition = new Vector2(0f, -74f);
                descRt.sizeDelta = new Vector2(-48f, 36f);

                // Cost Row: [Coin] [CostText]
                var costRow = CreateExplicitRect(modal, "CostRow", 0.5f, 0f, 0.5f, 0f);
                costRow.pivot = new Vector2(0.5f, 0f);
                costRow.anchoredPosition = new Vector2(0f, 78f);
                costRow.sizeDelta = new Vector2(120f, 28f);

                var cCoin = CreateExplicitRect(costRow, "Coin", 0f, 0.5f, 0f, 0.5f);
                cCoin.pivot = new Vector2(0f, 0.5f);
                cCoin.anchoredPosition = new Vector2(10f, 0f);
                cCoin.sizeDelta = new Vector2(22f, 22f);
                var cCoinImg = cCoin.gameObject.AddComponent<Image>();
                cCoinImg.sprite = coinIcon;
                cCoinImg.preserveAspect = true;

                var popupCostTmp = CreateExplicitText(costRow, "ConfirmPurchase_CostText", "100", fBold, 16f, Hex("#FDC553"), TextAlignmentOptions.MidlineLeft);
                var pcRt = popupCostTmp.GetComponent<RectTransform>();
                pcRt.anchorMin = new Vector2(0f, 0f);
                pcRt.anchorMax = new Vector2(1f, 1f);
                pcRt.offsetMin = new Vector2(40f, 0f);
                pcRt.offsetMax = Vector2.zero;

                // Action Buttons Row (Cancel / Confirm)
                var btnCancelGo = CreateExplicitRect(modal, "Cancel_CustomButton", 0f, 0f, 0.5f, 0f);
                btnCancelGo.pivot = new Vector2(0.5f, 0f);
                btnCancelGo.anchoredPosition = new Vector2(18f, 20f);
                btnCancelGo.sizeDelta = new Vector2(-28f, 42f);

                var cancelImg = btnCancelGo.gameObject.AddComponent<Image>();
                cancelImg.sprite = btnDarkCancel;
                cancelImg.type = Image.Type.Sliced;

                var cancelTmp = CreateExplicitText(btnCancelGo, "Text", "Cancel", fSemiBold, 14f, Color.white, TextAlignmentOptions.Center);
                var cancelBtn = btnCancelGo.gameObject.AddComponent<Button>();
                var cancelCustomBtn = btnCancelGo.gameObject.AddComponent<CustomButtonUI>();
                var cSo = new SerializedObject(cancelCustomBtn);
                cSo.FindProperty("isToggleable").boolValue = false;
                cSo.ApplyModifiedPropertiesWithoutUndo();

                var btnConfirmGo = CreateExplicitRect(modal, "Purchase_CustomButton", 0.5f, 0f, 1f, 0f);
                btnConfirmGo.pivot = new Vector2(0.5f, 0f);
                btnConfirmGo.anchoredPosition = new Vector2(-18f, 20f);
                btnConfirmGo.sizeDelta = new Vector2(-28f, 42f);

                var confirmImg = btnConfirmGo.gameObject.AddComponent<Image>();
                confirmImg.sprite = btnGoldConfirm;
                confirmImg.type = Image.Type.Sliced;

                var confirmTmp = CreateExplicitText(btnConfirmGo, "Text", "Confirm", fBold, 14f, Hex("#01010C"), TextAlignmentOptions.Center);
                var confirmBtn = btnConfirmGo.gameObject.AddComponent<Button>();
                var confirmCustomBtn = btnConfirmGo.gameObject.AddComponent<CustomButtonUI>();
                var confSo = new SerializedObject(confirmCustomBtn);
                confSo.FindProperty("isToggleable").boolValue = false;
                confSo.ApplyModifiedPropertiesWithoutUndo();

                // -----------------------------------------------------------------
                // 5. Wire Serialized Properties on ShopUI
                // -----------------------------------------------------------------
                var uiSo = new SerializedObject(shopUI);

                var rootCgProp = uiSo.FindProperty("<RootCanvasGroup>k__BackingField") ?? uiSo.FindProperty("RootCanvasGroup");
                if (rootCgProp != null) rootCgProp.objectReferenceValue = cg;

                uiSo.FindProperty("elementsParent").objectReferenceValue = content;
                uiSo.FindProperty("shopElementPrefab").objectReferenceValue = elemPrefab != null ? elemPrefab.GetComponent<ShopElement>() : null;
                uiSo.FindProperty("categoriesToggleGroup").objectReferenceValue = toggleGroup;
                uiSo.FindProperty("playerTokensAmountLabel").objectReferenceValue = tokenTmp;

                var rarityProp = uiSo.FindProperty("rarityFilters");
                rarityProp.arraySize = rarityBtnList.Count;
                for (int i = 0; i < rarityBtnList.Count; i++)
                    rarityProp.GetArrayElementAtIndex(i).objectReferenceValue = rarityBtnList[i];

                uiSo.FindProperty("confirmPurchasePopUp").objectReferenceValue = popupCg;
                uiSo.FindProperty("confirmPurchasePreviewImage").objectReferenceValue = previewImg;
                uiSo.FindProperty("confirmPurchasePreviewHeaderLabel").objectReferenceValue = popupHeaderTmp;
                uiSo.FindProperty("confirmPurchasePreviewDescriptionLabel").objectReferenceValue = descTmp;
                uiSo.FindProperty("confirmPurchasePreviewCostLabel").objectReferenceValue = popupCostTmp;
                uiSo.FindProperty("confirmPurchaseButton").objectReferenceValue = confirmCustomBtn;
                uiSo.FindProperty("cancelPurchaseButton").objectReferenceValue = cancelCustomBtn;

                uiSo.ApplyModifiedPropertiesWithoutUndo();

                // Configure ToggleGroup buttons
                var tgSo = new SerializedObject(toggleGroup);
                var extProp = tgSo.FindProperty("externalButtons");
                if (extProp != null) extProp.arraySize = 0;
                tgSo.FindProperty("maxSelectedButtons").intValue = 1;
                tgSo.FindProperty("isConfiguratingOnAwake").boolValue = true;
                tgSo.ApplyModifiedPropertiesWithoutUndo();

                toggleGroup.Configure();

                PrefabUtility.SaveAsPrefabAsset(root, MainPrefabPath);
                Debug.Log("[ShopRestyler] Shop_Screen prefab restyled successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureHiddenInMiddleScreen()
        {
            const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
            if (!File.Exists(MiddleScreenPath)) return;

            var middleScreen = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
            try
            {
                var shopPanels = middleScreen.GetComponentsInChildren<ShopUI>(true);
                foreach (var p in shopPanels)
                {
                    if (p.TryGetComponent<CanvasGroup>(out var pcg))
                    {
                        pcg.alpha = 0f;
                        pcg.interactable = false;
                        pcg.blocksRaycasts = false;
                    }

                    var pSo = new SerializedObject(p);
                    var cgProp = pSo.FindProperty("<RootCanvasGroup>k__BackingField") ?? pSo.FindProperty("RootCanvasGroup");
                    if (cgProp != null && pcg != null)
                        cgProp.objectReferenceValue = pcg;
                    pSo.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(middleScreen, MiddleScreenPath);
                Debug.Log("[ShopRestyler] Shop panel hidden by default in MiddleScreen_Scalable!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleScreen);
            }
        }

        // =============================================================================================================
        // HELPER BUILDERS
        // =============================================================================================================
        private static CustomButtonUI CreateCategoryTab(Transform parent, string name, string toggleId, string label, bool isActive)
        {
            var tabGo = CreateExplicitRect(parent, name, 0f, 0f, 0f, 0f);
            var btn = tabGo.gameObject.AddComponent<Button>();
            var cb = tabGo.gameObject.AddComponent<CustomButtonUI>();

            var img = tabGo.gameObject.AddComponent<Image>();
            img.sprite = isActive ? tabActiveBg : tabInactiveBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var txt = CreateExplicitText(tabGo, "Label", label, fSemiBold, 13.5f, isActive ? Color.white : Hex("#8E9CAE"), TextAlignmentOptions.Center);
            txt.raycastTarget = false;

            var so = new SerializedObject(cb);
            so.FindProperty("toggleID").stringValue = toggleId;
            so.FindProperty("isToggleable").boolValue = true;
            so.FindProperty("isInteractable").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            return cb;
        }

        private static void CreateTemplateCard(Transform parent, GameObject prefab, string name, Sprite tileSprite, string rarity, Sprite badgeSprite, Color badgeTextColor, string priceText, bool isPurchased)
        {
            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = name;

            var elem = go.GetComponent<ShopElement>();
            var elemImg = go.transform.Find("ItemContainer/Element_Image")?.GetComponent<Image>();
            if (elemImg != null && tileSprite != null) elemImg.sprite = tileSprite;

            var badgeImg = go.transform.Find("Rarity_Badge")?.GetComponent<Image>();
            if (badgeImg != null && badgeSprite != null) badgeImg.sprite = badgeSprite;

            var badgeTxt = go.transform.Find("Rarity_Badge/BadgeText")?.GetComponent<TextMeshProUGUI>();
            if (badgeTxt != null)
            {
                badgeTxt.text = rarity;
                badgeTxt.color = badgeTextColor;
            }

            var costTxt = go.transform.Find("Bottom_Bar/PriceGroup/Cost_Label")?.GetComponent<TextMeshProUGUI>();
            if (costTxt != null)
            {
                costTxt.text = priceText;
                costTxt.color = isPurchased ? Hex("#8E9CAE") : Color.white;
            }
        }

        private static Sprite MakeBottomRoundedSprite(string name, int w, int h, int r, Color fill, Color border, float borderWidth = 1f)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                // Only round bottom corners: when py < r and (px < r or px > w - r)
                float inside = 1f;
                if (py < r)
                {
                    float cx = Mathf.Clamp(px, r, w - r);
                    float cy = r;
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    inside = Mathf.Clamp01(r - dist + 0.5f);
                }

                Color c = fill;
                if (borderWidth > 0f)
                {
                    float distLeft = px;
                    float distRight = w - px;
                    float distTop = h - py;
                    float distBottom = py;
                    float edgeDist = Mathf.Min(distLeft, distRight, distTop, distBottom);
                    if (py < r && (px < r || px > w - r))
                    {
                        float cx = Mathf.Clamp(px, r, w - r);
                        float cy = r;
                        float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                        edgeDist = r - dist;
                    }
                    float borderMask = Mathf.Clamp01(borderWidth + 0.5f - edgeDist) * border.a;
                    c = Color.Lerp(fill, border, borderMask);
                }
                c.a *= inside;
                tex.SetPixel(x, y, c);
            }
            return SaveSlicedSprite(path, tex, r + 2);
        }

        private static RectTransform CreateExplicitRect(Transform parent, string name, float axMin, float ayMin, float axMax, float ayMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text CreateExplicitText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
