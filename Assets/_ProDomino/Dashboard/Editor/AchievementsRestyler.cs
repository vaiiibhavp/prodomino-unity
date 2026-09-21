using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.AchievementSystem;
using ProDomino.Shared;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles Achievements_Screen.prefab and Achiev_List_Container.prefab to strictly match
    /// the Figma reference (node 188:47834, media_1790018335447.png):
    /// - Header: Trophy Icon + "Achievements" title
    /// - 3 Metric Summary Cards:
    ///     1) Total Achievements (Horn icon, 12/20, Total Achievements)
    ///     2) Total Achievement Points (Star icon, 12, Total Achievement Points)
    ///     3) Current Rank (Class C badge, Class C, Current Rank)
    /// - Tab Bar & Filters Row:
    ///     Left: [ Achievements | Challenges ] pill toggle
    ///     Right: [ Sort By v ] [ Status v ] [ Game v ] dropdown pills
    /// - Table Section:
    ///     5-Column Header: Achievement Info (42%), Game (14%), Achievement Points (14%), Progress Bar (16%), Action (14%)
    ///     Vertical ScrollView with smooth scrolling
    /// - Row Prefab (Achiev_List_Container.prefab):
    ///     Flat 5-column layout with proper icons, progress track, and 3 Action button states (Claim, Claimed, In progress)
    /// - Preserves all serialized references in AchievementUI.cs and AchievementElement.cs
    /// </summary>
    internal static class AchievementsRestyler
    {
        private const string ScreenPrefabPath = "Assets/_ProDomino/Prefabs/UI/Achievements_Screen.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/Prefabs/UI/Achiev_List_Container.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string RankIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Rank";
        private const string DashboardIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";
        private const string Base64IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Base_64";
        private const string Base128IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Base_128";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite starSprite, hornSprite, calendarSprite, dominoIconSprite;
        private static Sprite trophyIcon, classCIcon, chevronDown, coinIcon;
        private static Sprite cardBg, tableBg, tableHeaderBg, tabActiveBg, tabInactiveBg;
        private static Sprite btnClaimBg, btnClaimedBg, btnInProgressBg, progressTrackBg, progressFillBg, dropdownPillBg;
        private static Sprite rowCardBg, rowDivider;

        [MenuItem("ProDomino/Dashboard/Restyle Achievements + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            RestyleEntryPrefab();
            BuildCleanAchievementsScreenPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AchievementsRestyler] SUCCESS: Achievements screen completely restyled to Figma design!");
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            if (!SessionState.GetBool("PD_AchievementsRestyler_Ran_v1", false))
            {
                SessionState.SetBool("PD_AchievementsRestyler_Ran_v1", true);
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

            trophyIcon = EnsureSprite($"{DashboardIconsDir}/Nav_Achievements.png");
            classCIcon = EnsureSprite($"{RankIconsDir}/Class_C_Icon.png");
            chevronDown = EnsureSprite($"{Base64IconsDir}/Arrow_Dropdown_Icon.png");
            coinIcon = EnsureSprite($"{DashboardIconsDir}/Icon_Coin.png");
            dominoIconSprite = EnsureSprite($"{Base128IconsDir}/Winner_Icon.png");

            starSprite = MakeStarSprite("Achiev_Star_Gold", 64, Hex("#FBBF24"));
            hornSprite = MakeHornSprite("Achiev_Horn_Gold", 64, Hex("#FBBF24"));
            calendarSprite = MakeCalendarSprite("Achiev_Calendar_Red", 64, Hex("#EF4444"));

            cardBg = MakePanelSprite("Achiev_CardBg", 48, 48, 12, Hex("#0D111A"), Hex("#080C14"), Hex("#182030"), 1f);
            tableBg = MakePanelSprite("Achiev_TableBg", 48, 48, 14, Hex("#0B0F18"), Hex("#060910"), Hex("#161D2B"), 1f);
            tableHeaderBg = MakePanelSprite("Achiev_TableHeaderBg", 32, 32, 8, Hex("#0E1320"), Hex("#0B0F19"), Hex("#1A2234"), 1f);
            tabActiveBg = MakePanelSprite("Achiev_TabActiveBg", 32, 32, 8, Hex("#2563EB"), Hex("#1D4ED8"), Hex("#3B82F6"), 1f);
            tabInactiveBg = MakePanelSprite("Achiev_TabInactiveBg", 32, 32, 8, Hex("#0E1422"), Hex("#0A0F1A"), Hex("#1A2336"), 1f);
            dropdownPillBg = MakePanelSprite("Achiev_DropdownPillBg", 32, 32, 8, Hex("#0F1422"), Hex("#0B101D"), Hex("#232B3E"), 1f);

            btnClaimBg = MakePanelSprite("Achiev_BtnClaimBg", 32, 32, 16, Hex("#F59E0B"), Hex("#D97706"), Hex("#FCD34D"), 1f);
            btnClaimedBg = MakePanelSprite("Achiev_BtnClaimedBg", 32, 32, 16, Hex("#1B2232"), Hex("#141A27"), Hex("#334155"), 1f);
            btnInProgressBg = MakePanelSprite("Achiev_BtnInProgressBg", 32, 32, 16, Hex("#111622"), Hex("#0D121C"), Hex("#1E293B"), 1f);

            progressTrackBg = MakePanelSprite("Achiev_ProgressTrack", 32, 16, 8, Hex("#131926"), Hex("#131926"), Hex("#1E293B"), 1f);
            progressFillBg = MakePanelSprite("Achiev_ProgressFill", 32, 16, 8, Hex("#F59E0B"), Hex("#EA580C"), Color.clear, 0f);

            rowCardBg = MakePanelSprite("Achiev_RowBg", 32, 32, 8, Hex("#0C101A"), Hex("#080C14"), Hex("#161D2B"), 1f);
            rowDivider = MakePanelSprite("Achiev_RowDivider", 8, 8, 0, Hex("#161F2E"), Hex("#161F2E"), Color.clear, 0f);
        }

        private static Sprite EnsureSprite(string path)
        {
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

        // -------------------------------------------------------------------------------------------------------------
        // ROW PREFAB RESTYLING (Achiev_List_Container.prefab)
        // -------------------------------------------------------------------------------------------------------------
        private static void RestyleEntryPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(EntryPrefabPath);
            try
            {
                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(0f, 68f);

                // Configure or add root Image
                var bg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                bg.sprite = rowCardBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;

                var elem = root.GetComponent<AchievementElement>() ?? root.AddComponent<AchievementElement>();

                // Clear legacy nested children
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // 1. Column: Achievement Info [0.015 .. 0.43]
                var colInfo = CreateRect(root.transform, "Col_Info", 0.015f, 0f, 0.43f, 1f);

                // Icon box: 48x48 rounded container
                var iconBox = CreateRect(colInfo, "IconBox", 0f, 0.5f, 0f, 0.5f);
                iconBox.sizeDelta = new Vector2(48f, 48f);
                iconBox.anchoredPosition = new Vector2(24f, 0f);
                var iconBoxImg = iconBox.gameObject.AddComponent<Image>();
                iconBoxImg.sprite = MakePanelSprite("Achiev_IconBoxBg", 32, 32, 8, Hex("#161D2C"), Hex("#0E131E"), Hex("#263248"), 1f);
                iconBoxImg.type = Image.Type.Sliced;

                // Achievement Icon
                var achievIconGo = CreateRect(iconBox, "AchievementIcon", 0.5f, 0.5f, 0.5f, 0.5f);
                achievIconGo.sizeDelta = new Vector2(36f, 36f);
                achievIconGo.anchoredPosition = Vector2.zero;
                var achievIconImg = achievIconGo.gameObject.AddComponent<Image>();
                achievIconImg.preserveAspect = true;

                // Text Stack
                var textStack = CreateRect(colInfo, "TextStack", 0f, 0f, 1f, 1f);
                textStack.offsetMin = new Vector2(58f, 6f);
                textStack.offsetMax = new Vector2(-6f, -6f);
                var vlgText = textStack.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgText.childAlignment = TextAnchor.MiddleLeft;
                vlgText.spacing = 2f;
                vlgText.childControlWidth = true;
                vlgText.childControlHeight = false;
                vlgText.childForceExpandWidth = true;
                vlgText.childForceExpandHeight = false;

                var titleTmp = CreateText(textStack, "NameLabel", "Complete all tutorials for Block Mode", fSemiBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                titleTmp.overflowMode = TextOverflowModes.Ellipsis;
                var titleLe = titleTmp.gameObject.AddComponent<LayoutElement>();
                titleLe.preferredHeight = 22f;

                var descTmp = CreateText(textStack, "DescLabel", "Complete all of the tutorials available for the Block Mode", fRegular, 11f, Hex("#94A3B8"), TextAlignmentOptions.MidlineLeft);
                descTmp.textWrappingMode = TextWrappingModes.NoWrap;
                descTmp.overflowMode = TextOverflowModes.Ellipsis;
                var descLe = descTmp.gameObject.AddComponent<LayoutElement>();
                descLe.preferredHeight = 18f;

                // 2. Column: Game [0.43 .. 0.57]
                var colGame = CreateRect(root.transform, "Col_Game", 0.43f, 0f, 0.57f, 1f);
                var hlgGame = colGame.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlgGame.childAlignment = TextAnchor.MiddleLeft;
                hlgGame.spacing = 8f;
                hlgGame.childControlWidth = false;
                hlgGame.childControlHeight = false;

                var gameIconGo = CreateRect(colGame, "GameIcon", 0f, 0.5f, 0f, 0.5f);
                gameIconGo.sizeDelta = new Vector2(24f, 24f);
                var gameIconImg = gameIconGo.gameObject.AddComponent<Image>();
                gameIconImg.sprite = dominoIconSprite;
                gameIconImg.color = Hex("#FBBF24");
                gameIconImg.preserveAspect = true;

                var gameLabelTmp = CreateText(colGame, "GameLabel", "Block Game", fMedium, 13f, Hex("#E2E8F0"), TextAlignmentOptions.MidlineLeft);
                gameLabelTmp.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 24f);

                // 3. Column: Achievement Points [0.57 .. 0.71]
                var colPoints = CreateRect(root.transform, "Col_Points", 0.57f, 0f, 0.71f, 1f);
                var hlgPoints = colPoints.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlgPoints.childAlignment = TextAnchor.MiddleLeft;
                hlgPoints.spacing = 8f;
                hlgPoints.childControlWidth = false;
                hlgPoints.childControlHeight = false;

                var starGo = CreateRect(colPoints, "StarIcon", 0f, 0.5f, 0f, 0.5f);
                starGo.sizeDelta = new Vector2(20f, 20f);
                var starImg = starGo.gameObject.AddComponent<Image>();
                starImg.sprite = starSprite;
                starImg.color = Color.white;

                var pointsTmp = CreateText(colPoints, "PointsLabel", "200", fSemiBold, 14f, Color.white, TextAlignmentOptions.MidlineLeft);
                pointsTmp.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 24f);

                // 4. Column: Progress Bar [0.71 .. 0.87]
                var colProgress = CreateRect(root.transform, "Col_Progress", 0.71f, 0f, 0.87f, 1f);
                var hlgProgress = colProgress.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlgProgress.childAlignment = TextAnchor.MiddleLeft;
                hlgProgress.spacing = 10f;
                hlgProgress.childControlWidth = false;
                hlgProgress.childControlHeight = false;

                // Track
                var track = CreateRect(colProgress, "ProgressTrack", 0f, 0.5f, 0f, 0.5f);
                track.sizeDelta = new Vector2(90f, 10f);
                var trackImg = track.gameObject.AddComponent<Image>();
                trackImg.sprite = progressTrackBg;
                trackImg.type = Image.Type.Sliced;

                // Fill
                var fill = CreateRect(track, "ProgressFill", 0f, 0f, 0.45f, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                var fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.sprite = progressFillBg;
                fillImg.type = Image.Type.Sliced;

                var reqTmp = CreateText(colProgress, "RequirementLabel", "2/5", fMedium, 13f, Hex("#E2E8F0"), TextAlignmentOptions.MidlineLeft);
                reqTmp.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 24f);

                // 5. Column: Action [0.87 .. 0.985]
                var colAction = CreateRect(root.transform, "Col_Action", 0.87f, 0f, 0.985f, 1f);

                // Button 1: Claim (Ready)
                var btnClaimGo = CreateRect(colAction, "ClaimButton", 0.5f, 0.5f, 0.5f, 0.5f);
                btnClaimGo.sizeDelta = new Vector2(86f, 32f);
                btnClaimGo.anchoredPosition = Vector2.zero;
                var btnClaimImg = btnClaimGo.gameObject.AddComponent<Image>();
                btnClaimImg.sprite = btnClaimBg;
                btnClaimImg.type = Image.Type.Sliced;
                var claimBtn = btnClaimGo.gameObject.AddComponent<Button>();
                var claimBtnText = CreateText(btnClaimGo, "ClaimText", "Claim", fBold, 12.5f, Hex("#0F172A"), TextAlignmentOptions.Center);
                claimBtnText.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                claimBtnText.GetComponent<RectTransform>().offsetMax = Vector2.zero;

                // Container 2: Claimed (Completed)
                var containerClaimed = CreateRect(colAction, "CompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerClaimed.sizeDelta = new Vector2(86f, 32f);
                containerClaimed.anchoredPosition = Vector2.zero;
                var claimedImg = containerClaimed.gameObject.AddComponent<Image>();
                claimedImg.sprite = btnClaimedBg;
                claimedImg.type = Image.Type.Sliced;
                var claimedText = CreateText(containerClaimed, "ClaimedText", "Claimed", fSemiBold, 12f, Hex("#D97706"), TextAlignmentOptions.Center);
                claimedText.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                claimedText.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                containerClaimed.gameObject.SetActive(false);

                // Container 3: In Progress (Not ready)
                var containerInProgress = CreateRect(colAction, "IncompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerInProgress.sizeDelta = new Vector2(86f, 32f);
                containerInProgress.anchoredPosition = Vector2.zero;
                var inProgressImg = containerInProgress.gameObject.AddComponent<Image>();
                inProgressImg.sprite = btnInProgressBg;
                inProgressImg.type = Image.Type.Sliced;
                var inProgressText = CreateText(containerInProgress, "InProgressText", "In progress", fMedium, 11f, Hex("#64748B"), TextAlignmentOptions.Center);
                inProgressText.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                inProgressText.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                containerInProgress.gameObject.SetActive(false);

                // Serialized Fields Binding on AchievementElement
                var soElem = new SerializedObject(elem);
                soElem.FindProperty("nameLabel").objectReferenceValue = titleTmp;
                soElem.FindProperty("descriptionLabel").objectReferenceValue = descTmp;
                soElem.FindProperty("requirementLabel").objectReferenceValue = reqTmp;
                soElem.FindProperty("achievementIcon").objectReferenceValue = achievIconImg;
                soElem.FindProperty("claimButton").objectReferenceValue = claimBtn;
                soElem.FindProperty("completedContainer").objectReferenceValue = containerClaimed.gameObject;
                soElem.FindProperty("incompletedContainer").objectReferenceValue = containerInProgress.gameObject;
                soElem.FindProperty("rewardIcon").objectReferenceValue = starImg;
                soElem.FindProperty("rewardObject").objectReferenceValue = starGo.gameObject;
                soElem.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[AchievementsRestyler] Row prefab restyled successfully!");
        }

        // -------------------------------------------------------------------------------------------------------------
        // MAIN SCREEN PREFAB RESTYLING (Achievements_Screen.prefab)
        // -------------------------------------------------------------------------------------------------------------
        private static void BuildCleanAchievementsScreenPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var rt = root.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var achUI = root.GetComponent<AchievementUI>() ?? root.AddComponent<AchievementUI>();
                var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

                // Remove previous layout group on root if present
                if (root.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlgRoot)
                    UnityEngine.Object.DestroyImmediate(vlgRoot);

                // Collect legacy children to clear cleanly
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // Root Container layout
                var mainContent = CreateRect(root.transform, "MainContent", 0f, 0f, 1f, 1f);
                mainContent.offsetMin = new Vector2(32f, 20f);
                mainContent.offsetMax = new Vector2(-32f, -16f);

                // -----------------------------------------------------------------
                // 1. Header Section (Top: 0 to 40px)
                // -----------------------------------------------------------------
                var headerSection = CreateRect(mainContent, "Header_Section", 0f, 1f, 1f, 1f);
                headerSection.sizeDelta = new Vector2(0f, 40f);
                headerSection.anchoredPosition = new Vector2(0f, -20f);

                var headerHlg = headerSection.gameObject.AddComponent<HorizontalLayoutGroup>();
                headerHlg.childAlignment = TextAnchor.MiddleLeft;
                headerHlg.spacing = 10f;
                headerHlg.childControlWidth = false;
                headerHlg.childControlHeight = false;

                var trophyGo = CreateRect(headerSection, "TrophyIcon", 0f, 0.5f, 0f, 0.5f);
                trophyGo.sizeDelta = new Vector2(26f, 26f);
                var trophyImg = trophyGo.gameObject.AddComponent<Image>();
                trophyImg.sprite = trophyIcon;
                trophyImg.color = Hex("#FBBF24");
                trophyImg.preserveAspect = true;

                var titleText = CreateText(headerSection, "TitleText", "Achievements", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 32f);

                // -----------------------------------------------------------------
                // 2. Summary Metric Cards Section (y: -56 to -166px, height: 110px)
                // -----------------------------------------------------------------
                var cardsSection = CreateRect(mainContent, "Cards_Section", 0f, 1f, 1f, 1f);
                cardsSection.sizeDelta = new Vector2(0f, 110f);
                cardsSection.anchoredPosition = new Vector2(0f, -105f);

                var cardsHlg = cardsSection.gameObject.AddComponent<HorizontalLayoutGroup>();
                cardsHlg.childAlignment = TextAnchor.MiddleCenter;
                cardsHlg.spacing = 20f;
                cardsHlg.childControlWidth = true;
                cardsHlg.childControlHeight = true;
                cardsHlg.childForceExpandWidth = true;
                cardsHlg.childForceExpandHeight = true;

                // Card 1: Total Achievements
                var card1 = CreateMetricCard(cardsSection, "Card_TotalAchievements", hornSprite, Hex("#FBBF24"), "12 / 20", "Total Achievements", out var totalAchievTmp);

                // Card 2: Total Achievement Points
                var card2 = CreateMetricCard(cardsSection, "Card_AchievementPoints", starSprite, Hex("#FBBF24"), "12", "Total Achievement Points", out var pointsTmp);

                // Card 3: Current Rank
                var card3 = CreateMetricCard(cardsSection, "Card_CurrentRank", classCIcon, Color.white, "Class C", "Current Rank", out var rankTmp);

                // -----------------------------------------------------------------
                // 3. Tab Bar & Filters Row (y: -176 to -220px, height: 44px)
                // -----------------------------------------------------------------
                var tabBarRow = CreateRect(mainContent, "TabBar_Row", 0f, 1f, 1f, 1f);
                tabBarRow.sizeDelta = new Vector2(0f, 44f);
                tabBarRow.anchoredPosition = new Vector2(0f, -188f);

                // Left: Tab segment pill [ Achievements | Challenges ]
                var tabSegment = CreateRect(tabBarRow, "TabSegment", 0f, 0.5f, 0f, 0.5f);
                tabSegment.sizeDelta = new Vector2(240f, 40f);
                tabSegment.anchoredPosition = new Vector2(120f, 0f);
                var tabSegImg = tabSegment.gameObject.AddComponent<Image>();
                tabSegImg.sprite = tabInactiveBg;
                tabSegImg.type = Image.Type.Sliced;

                // Tab 1: Achievements (Active)
                var tabAchiev = CreateRect(tabSegment, "Tab_Achievements", 0f, 0f, 0.5f, 1f);
                tabAchiev.offsetMin = new Vector2(2f, 2f);
                tabAchiev.offsetMax = new Vector2(-2f, -2f);
                var tabAchievImg = tabAchiev.gameObject.AddComponent<Image>();
                tabAchievImg.sprite = tabActiveBg;
                tabAchievImg.type = Image.Type.Sliced;
                var tabAchievTxt = CreateText(tabAchiev, "Label", "Achievements", fSemiBold, 13f, Color.white, TextAlignmentOptions.Center);
                tabAchievTxt.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                tabAchievTxt.GetComponent<RectTransform>().offsetMax = Vector2.zero;

                // Tab 2: Challenges (Inactive)
                var tabChallenges = CreateRect(tabSegment, "Tab_Challenges", 0.5f, 0f, 1f, 1f);
                tabChallenges.offsetMin = new Vector2(2f, 2f);
                tabChallenges.offsetMax = new Vector2(-2f, -2f);
                var tabChallengesTxt = CreateText(tabChallenges, "Label", "Challenges", fMedium, 13f, Hex("#64748B"), TextAlignmentOptions.Center);
                tabChallengesTxt.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                tabChallengesTxt.GetComponent<RectTransform>().offsetMax = Vector2.zero;

                // Right: Filters (Sort By, Status, Game)
                var filtersContainer = CreateRect(tabBarRow, "FiltersContainer", 1f, 0.5f, 1f, 0.5f);
                filtersContainer.sizeDelta = new Vector2(360f, 40f);
                filtersContainer.anchoredPosition = new Vector2(-180f, 0f);

                var filtersHlg = filtersContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                filtersHlg.childAlignment = TextAnchor.MiddleRight;
                filtersHlg.spacing = 12f;
                filtersHlg.childControlWidth = false;
                filtersHlg.childControlHeight = false;

                CreateFilterPill(filtersContainer, "Filter_SortBy", "Sort By", 100f);
                CreateFilterPill(filtersContainer, "Filter_Status", "Status", 95f);
                CreateFilterPill(filtersContainer, "Filter_Game", "Game", 95f);

                // -----------------------------------------------------------------
                // 4. Table Section (Remaining vertical space: top: 222px, bottom: 0px)
                // -----------------------------------------------------------------
                var tableSection = CreateRect(mainContent, "Table_Section", 0f, 0f, 1f, 1f);
                tableSection.offsetMin = Vector2.zero;
                tableSection.offsetMax = new Vector2(0f, -222f);

                var tableImg = tableSection.gameObject.AddComponent<Image>();
                tableImg.sprite = tableBg;
                tableImg.type = Image.Type.Sliced;
                tableImg.color = Color.white;

                // 4a. Table Header Bar (Height: 44px)
                var tableHeader = CreateRect(tableSection, "Table_Header", 0f, 1f, 1f, 1f);
                tableHeader.sizeDelta = new Vector2(0f, 44f);
                tableHeader.anchoredPosition = new Vector2(0f, -22f);

                var thImg = tableHeader.gameObject.AddComponent<Image>();
                thImg.sprite = tableHeaderBg;
                thImg.type = Image.Type.Sliced;
                thImg.color = new Color(1f, 1f, 1f, 0.6f);

                CreateHeaderCol(tableHeader, "TH_Info", "Achievement Info", 0.015f, 0.43f);
                CreateHeaderCol(tableHeader, "TH_Game", "Game", 0.43f, 0.57f);
                CreateHeaderCol(tableHeader, "TH_Points", "Achievement Points", 0.57f, 0.71f);
                CreateHeaderCol(tableHeader, "TH_Progress", "Progress Bar", 0.71f, 0.87f);
                CreateHeaderCol(tableHeader, "TH_Action", "Action", 0.87f, 0.985f, TextAlignmentOptions.Center);

                // 4b. Table ScrollView (ScrollRect)
                var scrollView = CreateRect(tableSection, "ScrollView", 0f, 0f, 1f, 1f);
                scrollView.offsetMin = new Vector2(6f, 6f);
                scrollView.offsetMax = new Vector2(-6f, -48f);

                var scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.elasticity = 0.1f;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 35f;

                var viewport = CreateRect(scrollView, "Viewport", 0f, 0f, 1f, 1f);
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;
                var mask = viewport.gameObject.AddComponent<RectMask2D>();

                var content = CreateRect(viewport, "Content", 0f, 1f, 1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(0f, 0f);

                var vlgContent = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgContent.childAlignment = TextAnchor.UpperCenter;
                vlgContent.spacing = 8f;
                vlgContent.padding = new RectOffset(6, 6, 8, 8);
                vlgContent.childControlWidth = true;
                vlgContent.childControlHeight = false;
                vlgContent.childForceExpandWidth = true;
                vlgContent.childForceExpandHeight = false;

                var csf = content.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.viewport = viewport;
                scrollRect.content = content;

                // Load entry prefab to bind to AchievementUI
                var entryPrefab = AssetDatabase.LoadAssetAtPath<AchievementElement>(EntryPrefabPath);

                // Wire Serialized Object properties on AchievementUI
                var soUI = new SerializedObject(achUI);
                soUI.FindProperty("<RootCanvasGroup>k__BackingField").objectReferenceValue = cg;
                soUI.FindProperty("totalAchievementsLabel").objectReferenceValue = totalAchievTmp;
                soUI.FindProperty("totalAchievementsPointsLabel").objectReferenceValue = pointsTmp;
                soUI.FindProperty("categoryAchievementCompletedLabel").objectReferenceValue = rankTmp;
                soUI.FindProperty("achievementElementPrefab").objectReferenceValue = entryPrefab;
                soUI.FindProperty("achievementElementParent").objectReferenceValue = content;
                soUI.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[AchievementsRestyler] Achievements_Screen prefab restyled successfully!");
        }

        // -------------------------------------------------------------------------------------------------------------
        // UI BUILDING HELPERS
        // -------------------------------------------------------------------------------------------------------------
        private static RectTransform CreateMetricCard(Transform parent, string name, Sprite icon, Color iconColor, string defaultVal, string subtext, out TMP_Text valueTmp)
        {
            var card = CreateRect(parent, name, 0f, 0f, 1f, 1f);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = cardBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            // Icon on left: 44x44
            var iconGo = CreateRect(card, "Icon", 0f, 0.5f, 0f, 0.5f);
            iconGo.sizeDelta = new Vector2(44f, 44f);
            iconGo.anchoredPosition = new Vector2(36f, 0f);
            var iconImg = iconGo.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = iconColor;
            iconImg.preserveAspect = true;

            // Text stack on right
            var textStack = CreateRect(card, "TextStack", 0f, 0f, 1f, 1f);
            textStack.offsetMin = new Vector2(72f, 14f);
            textStack.offsetMax = new Vector2(-16f, -14f);

            var vlg = textStack.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.spacing = 3f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            valueTmp = CreateText(textStack, "ValueText", defaultVal, fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            var vLe = valueTmp.gameObject.AddComponent<LayoutElement>();
            vLe.preferredHeight = 30f;

            var subTmp = CreateText(textStack, "Subtext", subtext, fMedium, 12f, Hex("#94A3B8"), TextAlignmentOptions.MidlineLeft);
            var sLe = subTmp.gameObject.AddComponent<LayoutElement>();
            sLe.preferredHeight = 20f;

            return card;
        }

        private static void CreateFilterPill(Transform parent, string name, string label, float width)
        {
            var pill = CreateRect(parent, name, 0f, 0.5f, 0f, 0.5f);
            pill.sizeDelta = new Vector2(width, 34f);
            var img = pill.gameObject.AddComponent<Image>();
            img.sprite = dropdownPillBg;
            img.type = Image.Type.Sliced;

            var hlg = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 8f;
            hlg.padding = new RectOffset(12, 10, 0, 0);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            var txt = CreateText(pill, "Label", label, fMedium, 12.5f, Hex("#CBD5E1"), TextAlignmentOptions.MidlineLeft);
            txt.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 40f, 24f);

            var arrow = CreateRect(pill, "Arrow", 0f, 0.5f, 0f, 0.5f);
            arrow.sizeDelta = new Vector2(10f, 10f);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = chevronDown;
            arrowImg.color = Hex("#94A3B8");
            arrowImg.preserveAspect = true;
        }

        private static void CreateHeaderCol(Transform parent, string name, string title, float xMin, float xMax, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var col = CreateRect(parent, name, xMin, 0f, xMax, 1f);
            col.offsetMin = new Vector2(6f, 0f);
            col.offsetMax = new Vector2(-6f, 0f);
            CreateText(col, "Title", title, fSemiBold, 12f, Hex("#94A3B8"), align);
        }

        private static RectTransform CreateRect(Transform parent, string name, float axMin, float ayMin, float axMax, float ayMax)
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

        private static TMP_Text CreateText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
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

        // -------------------------------------------------------------------------------------------------------------
        // PROCEDURAL SPRITE GENERATORS
        // -------------------------------------------------------------------------------------------------------------
        private static Sprite MakeStarSprite(string name, int size, Color color)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float rOuter = size * 0.44f;
            float rInner = rOuter * 0.42f;

            Vector2[] pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = (i * 36f - 90f) * Mathf.Deg2Rad;
                float r = (i % 2 == 0) ? rOuter : rInner;
                pts[i] = center + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * r;
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                if (IsPointInPolygon(p, pts))
                    tex.SetPixel(x, y, color);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
            tex.Apply();
            return SaveSprite(path, tex);
        }

        private static Sprite MakeHornSprite(string name, int size, Color color)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            // Draw celebratory party horn / megaphone shape
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                // Simple megaphone silhouette: triangle widening from left (nx=0.25) to right (nx=0.75)
                bool inside = false;
                if (nx >= 0.2f && nx <= 0.75f)
                {
                    float halfH = Mathf.Lerp(0.08f, 0.35f, (nx - 0.2f) / 0.55f);
                    if (Mathf.Abs(ny - 0.5f) <= halfH) inside = true;
                }
                // Mouthpiece on left
                if (nx >= 0.12f && nx < 0.2f && Mathf.Abs(ny - 0.5f) <= 0.12f) inside = true;
                // Flare rim on right
                if (nx >= 0.75f && nx <= 0.85f && Mathf.Abs(ny - 0.5f) <= 0.38f) inside = true;

                tex.SetPixel(x, y, inside ? color : Color.clear);
            }
            tex.Apply();
            return SaveSprite(path, tex);
        }

        private static Sprite MakeCalendarSprite(string name, int size, Color color)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                bool inside = false;
                // Calendar body: rounded box between [0.15..0.85]
                if (nx >= 0.15f && nx <= 0.85f && ny >= 0.15f && ny <= 0.85f)
                {
                    // Top red banner
                    if (ny >= 0.65f)
                        inside = true;
                    else
                    {
                        // Grid dots inside bottom white part
                        inside = true;
                    }
                }
                tex.SetPixel(x, y, inside ? color : Color.clear);
            }
            tex.Apply();
            return SaveSprite(path, tex);
        }

        private static bool IsPointInPolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (((polygon[i].y > p.y) != (polygon[j].y > p.y)) &&
                    (p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
