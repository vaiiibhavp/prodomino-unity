using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// - Header: Trophy Icon + "Achievements" title grouped together on the top left
    /// - 3 Metric Summary Cards:
    ///     [Achievements tab]:
    ///         1) Total Achievements (Horn icon, 12 / 20, Total Achievements)
    ///         2) Total Achievement Points (Star icon, 12, Total Achievement Points)
    ///         3) Current Rank (Class C crest, Class C, Current Rank)
    ///     [Challenges tab]:
    ///         1) Total Daily Challenges (Calendar icon, progress bar, 1/30)
    ///         2) Total Weekly Challenges (Calendar icon, progress bar, 1/4)
    ///         3) Total Monthly Challenges (Calendar icon, progress bar, 0/1)
    /// - Tab Bar & Filters Row:
    ///     Left: [ Achievements | Challenges ] interactive segmented pill button
    ///     Right: [ Sort By v ] [ Status v ] [ Game v ] dropdown pills
    /// - Table Section:
    ///     5-Column Header: Achievement/Challenge Info, Game, Achievement Points/Reward, Progress Bar, Action
    ///     Dual-content views (Achievements with star icons vs Challenges with coin icons)
    ///     Interactive AchievementsTabController component handling live tab switching
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
        private const string AchievIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Achievement";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite starSprite, hornSprite, calendarSprite, dominoIconSprite, searchIconSprite;
        private static Sprite trophyIcon, classCIcon, chevronDown, coinIcon;
        private static Sprite cardBg, tableBg, tableHeaderBg, tabActiveBg, tabInactiveBg;
        private static Sprite btnClaimBg, btnClaimedBg, btnInProgressBg, progressTrackBg, progressFillBg, dropdownPillBg;
        private static Sprite rowCardBg, rowDivider;

        private enum RowActionState { Claim, Claimed, InProgress }

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
            if (!SessionState.GetBool("PD_AchievementsRestyler_Ran_v3", false))
            {
                SessionState.SetBool("PD_AchievementsRestyler_Ran_v3", true);
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
            searchIconSprite = EnsureSprite($"{Base128IconsDir}/Search_Icon.png");

            starSprite = MakeStarSprite("Achiev_Star_Gold", 64, Hex("#FBBF24"));
            hornSprite = MakeHornSprite("Achiev_Horn_Gold", 64, Hex("#FBBF24"));
            calendarSprite = MakeCalendarSprite("Achiev_Calendar_Red", 64, Hex("#EF4444"));

            cardBg = MakePanelSprite("Achiev_CardBg", 48, 48, 12, Hex("#0E121D"), Hex("#090D17"), Hex("#1A2234"), 1f);
            tableBg = MakePanelSprite("Achiev_TableBg", 48, 48, 14, Hex("#0B0F19"), Hex("#070A12"), Hex("#161D2B"), 1f);
            tableHeaderBg = MakePanelSprite("Achiev_TableHeaderBg", 32, 32, 8, Hex("#0E1320"), Hex("#0B0F19"), Hex("#182030"), 1f);
            tabActiveBg = MakePanelSprite("Achiev_TabActiveBg", 32, 32, 8, Hex("#2563EB"), Hex("#1D4ED8"), Hex("#3B82F6"), 1f);
            tabInactiveBg = MakePanelSprite("Achiev_TabInactiveBg", 32, 32, 8, Hex("#0E1422"), Hex("#0A0F1A"), Hex("#1A2336"), 1f);
            dropdownPillBg = MakePanelSprite("Achiev_DropdownPillBg", 32, 32, 8, Hex("#0F1422"), Hex("#0B101D"), Hex("#232B3E"), 1f);

            btnClaimBg = MakePanelSprite("Achiev_BtnClaimBg", 32, 32, 15, Hex("#F59E0B"), Hex("#D97706"), Hex("#FCD34D"), 1f);
            btnClaimedBg = MakePanelSprite("Achiev_BtnClaimedBg", 32, 32, 15, Hex("#1B2232"), Hex("#141A27"), Hex("#334155"), 1f);
            btnInProgressBg = MakePanelSprite("Achiev_BtnInProgressBg", 32, 32, 15, Hex("#111622"), Hex("#0D121C"), Hex("#1E293B"), 1f);

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
                rt.sizeDelta = new Vector2(0f, 66f);

                var bg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                bg.sprite = rowCardBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;

                var elem = root.GetComponent<AchievementElement>() ?? root.AddComponent<AchievementElement>();

                // Clear legacy children
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // 1. Column: Achievement Info [0.015 .. 0.430]
                var colInfo = CreateExplicitRect(root.transform, "Col_Info", 0.015f, 0f, 0.430f, 1f);

                // 42x42 Icon Box
                var iconBox = CreateExplicitRect(colInfo, "IconBox", 0f, 0.5f, 0f, 0.5f);
                iconBox.pivot = new Vector2(0f, 0.5f);
                iconBox.sizeDelta = new Vector2(42f, 42f);
                iconBox.anchoredPosition = new Vector2(4f, 0f);
                var iconBoxImg = iconBox.gameObject.AddComponent<Image>();
                iconBoxImg.sprite = MakePanelSprite("Achiev_IconBoxBg", 32, 32, 8, Hex("#161D2C"), Hex("#0E131E"), Hex("#263248"), 1f);
                iconBoxImg.type = Image.Type.Sliced;

                var achievIconGo = CreateExplicitRect(iconBox, "AchievementIcon", 0.5f, 0.5f, 0.5f, 0.5f);
                achievIconGo.sizeDelta = new Vector2(32f, 32f);
                achievIconGo.anchoredPosition = Vector2.zero;
                var achievIconImg = achievIconGo.gameObject.AddComponent<Image>();
                achievIconImg.preserveAspect = true;

                // Title & Description (explicit positions)
                var titleTmp = CreateExplicitText(colInfo, "NameLabel", "Complete all tutorials for Block Mode", fSemiBold, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
                var titleRt = titleTmp.GetComponent<RectTransform>();
                titleRt.pivot = new Vector2(0f, 0.5f);
                titleRt.anchorMin = new Vector2(0f, 0.5f);
                titleRt.anchorMax = new Vector2(1f, 0.5f);
                titleRt.anchoredPosition = new Vector2(56f, 11f);
                titleRt.sizeDelta = new Vector2(-62f, 22f);
                titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                titleTmp.overflowMode = TextOverflowModes.Ellipsis;

                var descTmp = CreateExplicitText(colInfo, "DescLabel", "Complete all of the tutorials available for the Block Mode", fRegular, 10.5f, Hex("#8E9CAE"), TextAlignmentOptions.MidlineLeft);
                var descRt = descTmp.GetComponent<RectTransform>();
                descRt.pivot = new Vector2(0f, 0.5f);
                descRt.anchorMin = new Vector2(0f, 0.5f);
                descRt.anchorMax = new Vector2(1f, 0.5f);
                descRt.anchoredPosition = new Vector2(56f, -11f);
                descRt.sizeDelta = new Vector2(-62f, 18f);
                descTmp.textWrappingMode = TextWrappingModes.NoWrap;
                descTmp.overflowMode = TextOverflowModes.Ellipsis;

                // 2. Column: Game [0.430 .. 0.570]
                var colGame = CreateExplicitRect(root.transform, "Col_Game", 0.430f, 0f, 0.570f, 1f);
                var gameIconGo = CreateExplicitRect(colGame, "GameIcon", 0f, 0.5f, 0f, 0.5f);
                gameIconGo.pivot = new Vector2(0f, 0.5f);
                gameIconGo.sizeDelta = new Vector2(22f, 22f);
                gameIconGo.anchoredPosition = new Vector2(4f, 0f);
                var gameIconImg = gameIconGo.gameObject.AddComponent<Image>();
                gameIconImg.sprite = dominoIconSprite;
                gameIconImg.color = Hex("#FBBF24");
                gameIconImg.preserveAspect = true;

                var gameLabelTmp = CreateExplicitText(colGame, "GameLabel", "Block Game", fMedium, 12.5f, Hex("#E2E8F0"), TextAlignmentOptions.MidlineLeft);
                var glRt = gameLabelTmp.GetComponent<RectTransform>();
                glRt.pivot = new Vector2(0f, 0.5f);
                glRt.anchorMin = new Vector2(0f, 0.5f);
                glRt.anchorMax = new Vector2(1f, 0.5f);
                glRt.anchoredPosition = new Vector2(32f, 0f);
                glRt.sizeDelta = new Vector2(-36f, 24f);

                // 3. Column: Achievement Points / Reward [0.570 .. 0.710]
                var colPoints = CreateExplicitRect(root.transform, "Col_Points", 0.570f, 0f, 0.710f, 1f);
                var starGo = CreateExplicitRect(colPoints, "StarIcon", 0f, 0.5f, 0f, 0.5f);
                starGo.pivot = new Vector2(0f, 0.5f);
                starGo.sizeDelta = new Vector2(20f, 20f);
                starGo.anchoredPosition = new Vector2(4f, 0f);
                var starImg = starGo.gameObject.AddComponent<Image>();
                starImg.sprite = starSprite;
                starImg.color = Color.white;

                var pointsTmp = CreateExplicitText(colPoints, "PointsLabel", "200", fSemiBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                var pRt = pointsTmp.GetComponent<RectTransform>();
                pRt.pivot = new Vector2(0f, 0.5f);
                pRt.anchorMin = new Vector2(0f, 0.5f);
                pRt.anchorMax = new Vector2(1f, 0.5f);
                pRt.anchoredPosition = new Vector2(30f, 0f);
                pRt.sizeDelta = new Vector2(-34f, 24f);

                // 4. Column: Progress Bar [0.710 .. 0.860]
                var colProgress = CreateExplicitRect(root.transform, "Col_Progress", 0.710f, 0f, 0.860f, 1f);
                var track = CreateExplicitRect(colProgress, "ProgressTrack", 0f, 0.5f, 0f, 0.5f);
                track.pivot = new Vector2(0f, 0.5f);
                track.sizeDelta = new Vector2(85f, 9f);
                track.anchoredPosition = new Vector2(4f, 0f);
                var trackImg = track.gameObject.AddComponent<Image>();
                trackImg.sprite = progressTrackBg;
                trackImg.type = Image.Type.Sliced;

                var fill = CreateExplicitRect(track, "ProgressFill", 0f, 0f, 0.4f, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                var fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.sprite = progressFillBg;
                fillImg.type = Image.Type.Sliced;

                var reqTmp = CreateExplicitText(colProgress, "RequirementLabel", "2/5", fMedium, 12.5f, Hex("#CBD5E1"), TextAlignmentOptions.MidlineLeft);
                var reqRt = reqTmp.GetComponent<RectTransform>();
                reqRt.pivot = new Vector2(0f, 0.5f);
                reqRt.anchorMin = new Vector2(0f, 0.5f);
                reqRt.anchorMax = new Vector2(1f, 0.5f);
                reqRt.anchoredPosition = new Vector2(98f, 0f);
                reqRt.sizeDelta = new Vector2(-102f, 24f);

                // 5. Column: Action [0.860 .. 0.985]
                var colAction = CreateExplicitRect(root.transform, "Col_Action", 0.860f, 0f, 0.985f, 1f);

                // Button 1: Claim (Ready)
                var btnClaimGo = CreateExplicitRect(colAction, "ClaimButton", 0.5f, 0.5f, 0.5f, 0.5f);
                btnClaimGo.sizeDelta = new Vector2(84f, 30f);
                btnClaimGo.anchoredPosition = Vector2.zero;
                var btnClaimImg = btnClaimGo.gameObject.AddComponent<Image>();
                btnClaimImg.sprite = btnClaimBg;
                btnClaimImg.type = Image.Type.Sliced;
                var claimBtn = btnClaimGo.gameObject.AddComponent<Button>();
                var claimBtnText = CreateExplicitText(btnClaimGo, "ClaimText", "Claim", fBold, 12f, Hex("#0F172A"), TextAlignmentOptions.Center);

                // Container 2: Claimed (Completed)
                var containerClaimed = CreateExplicitRect(colAction, "CompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerClaimed.sizeDelta = new Vector2(84f, 30f);
                containerClaimed.anchoredPosition = Vector2.zero;
                var claimedImg = containerClaimed.gameObject.AddComponent<Image>();
                claimedImg.sprite = btnClaimedBg;
                claimedImg.type = Image.Type.Sliced;
                var claimedText = CreateExplicitText(containerClaimed, "ClaimedText", "Claimed", fSemiBold, 11.5f, Hex("#D97706"), TextAlignmentOptions.Center);
                containerClaimed.gameObject.SetActive(false);

                // Container 3: In Progress (Not ready)
                var containerInProgress = CreateExplicitRect(colAction, "IncompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerInProgress.sizeDelta = new Vector2(84f, 30f);
                containerInProgress.anchoredPosition = Vector2.zero;
                var inProgressImg = containerInProgress.gameObject.AddComponent<Image>();
                inProgressImg.sprite = btnInProgressBg;
                inProgressImg.type = Image.Type.Sliced;
                var inProgressText = CreateExplicitText(containerInProgress, "InProgressText", "In progress", fMedium, 11f, Hex("#64748B"), TextAlignmentOptions.Center);
                containerInProgress.gameObject.SetActive(false);

                // Wire Serialized Object properties on AchievementElement
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
                var tabCtrl = root.GetComponent<AchievementsTabController>() ?? root.AddComponent<AchievementsTabController>();
                var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

                if (root.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlgRoot)
                    UnityEngine.Object.DestroyImmediate(vlgRoot);

                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // MainContent Container
                var mainContent = CreateExplicitRect(root.transform, "MainContent", 0f, 0f, 1f, 1f);
                mainContent.offsetMin = new Vector2(32f, 20f);
                mainContent.offsetMax = new Vector2(-32f, -16f);

                // -----------------------------------------------------------------
                // 1. Header Section: [Trophy] Achievements (Top-left grouped)
                // -----------------------------------------------------------------
                var headerSection = CreateExplicitRect(mainContent, "Header_Section", 0f, 1f, 1f, 1f);
                headerSection.pivot = new Vector2(0f, 1f);
                headerSection.sizeDelta = new Vector2(0f, 36f);
                headerSection.anchoredPosition = new Vector2(0f, 0f);

                var trophyGo = CreateExplicitRect(headerSection, "TrophyIcon", 0f, 0.5f, 0f, 0.5f);
                trophyGo.pivot = new Vector2(0f, 0.5f);
                trophyGo.sizeDelta = new Vector2(26f, 26f);
                trophyGo.anchoredPosition = new Vector2(0f, 0f);
                var trophyImg = trophyGo.gameObject.AddComponent<Image>();
                trophyImg.sprite = trophyIcon;
                trophyImg.color = Hex("#FBBF24");
                trophyImg.preserveAspect = true;

                var titleText = CreateExplicitText(headerSection, "TitleText", "Achievements", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                var ttRt = titleText.GetComponent<RectTransform>();
                ttRt.pivot = new Vector2(0f, 0.5f);
                ttRt.anchorMin = new Vector2(0f, 0.5f);
                ttRt.anchorMax = new Vector2(0f, 0.5f);
                ttRt.anchoredPosition = new Vector2(34f, 0f);
                ttRt.sizeDelta = new Vector2(260f, 32f);

                // -----------------------------------------------------------------
                // 2. Summary Metric Cards Section (Top y = -46px, Height = 112px)
                // -----------------------------------------------------------------
                // 2a. Achievements Cards Container
                var cardsSectionAchiev = CreateExplicitRect(mainContent, "Cards_Section_Achievements", 0f, 1f, 1f, 1f);
                cardsSectionAchiev.pivot = new Vector2(0.5f, 1f);
                cardsSectionAchiev.sizeDelta = new Vector2(0f, 112f);
                cardsSectionAchiev.anchoredPosition = new Vector2(0f, -46f);

                var cardsHlgAchiev = cardsSectionAchiev.gameObject.AddComponent<HorizontalLayoutGroup>();
                cardsHlgAchiev.childAlignment = TextAnchor.MiddleCenter;
                cardsHlgAchiev.spacing = 18f;
                cardsHlgAchiev.childControlWidth = true;
                cardsHlgAchiev.childControlHeight = true;
                cardsHlgAchiev.childForceExpandWidth = true;
                cardsHlgAchiev.childForceExpandHeight = true;

                CreateMetricCard(cardsSectionAchiev, "Card_TotalAchievements", hornSprite, Hex("#FBBF24"), "12 / 20", "Total Achievements", out var totalAchievTmp);
                CreateMetricCard(cardsSectionAchiev, "Card_AchievementPoints", starSprite, Hex("#FBBF24"), "12", "Total Achievement Points", out var pointsTmp);
                CreateMetricCard(cardsSectionAchiev, "Card_CurrentRank", classCIcon, Color.white, "Class C", "Current Rank", out var rankTmp);

                // 2b. Challenges Cards Container (for Challenges tab)
                var cardsSectionChallenges = CreateExplicitRect(mainContent, "Cards_Section_Challenges", 0f, 1f, 1f, 1f);
                cardsSectionChallenges.pivot = new Vector2(0.5f, 1f);
                cardsSectionChallenges.sizeDelta = new Vector2(0f, 112f);
                cardsSectionChallenges.anchoredPosition = new Vector2(0f, -46f);

                var cardsHlgChal = cardsSectionChallenges.gameObject.AddComponent<HorizontalLayoutGroup>();
                cardsHlgChal.childAlignment = TextAnchor.MiddleCenter;
                cardsHlgChal.spacing = 18f;
                cardsHlgChal.childControlWidth = true;
                cardsHlgChal.childControlHeight = true;
                cardsHlgChal.childForceExpandWidth = true;
                cardsHlgChal.childForceExpandHeight = true;

                CreateChallengeMetricCard(cardsSectionChallenges, "Card_DailyChallenges", "Total Daily Challenges", "1/30", 1f / 30f);
                CreateChallengeMetricCard(cardsSectionChallenges, "Card_WeeklyChallenges", "Total Weekly Challenges", "1/4", 0.25f);
                CreateChallengeMetricCard(cardsSectionChallenges, "Card_MonthlyChallenges", "Total Monthly Challenges", "0/1", 0.05f);
                cardsSectionChallenges.gameObject.SetActive(false);

                // -----------------------------------------------------------------
                // 3. Tab Bar & Filters Row (Top y = -170px, Height = 40px)
                // -----------------------------------------------------------------
                var tabBarRow = CreateExplicitRect(mainContent, "TabBar_Row", 0f, 1f, 1f, 1f);
                tabBarRow.pivot = new Vector2(0.5f, 1f);
                tabBarRow.sizeDelta = new Vector2(0f, 40f);
                tabBarRow.anchoredPosition = new Vector2(0f, -170f);

                // Left: Tab segment pill [ Achievements | Challenges ]
                var tabSegment = CreateExplicitRect(tabBarRow, "TabSegment", 0f, 0.5f, 0f, 0.5f);
                tabSegment.pivot = new Vector2(0f, 0.5f);
                tabSegment.sizeDelta = new Vector2(250f, 38f);
                tabSegment.anchoredPosition = new Vector2(0f, 0f);
                var tabSegImg = tabSegment.gameObject.AddComponent<Image>();
                tabSegImg.sprite = tabInactiveBg;
                tabSegImg.type = Image.Type.Sliced;

                // Tab 1: Achievements (Active blue button)
                var tabAchiev = CreateExplicitRect(tabSegment, "Tab_Achievements", 0f, 0f, 0.5f, 1f);
                tabAchiev.offsetMin = new Vector2(2f, 2f);
                tabAchiev.offsetMax = new Vector2(-2f, -2f);
                var tabAchievImg = tabAchiev.gameObject.AddComponent<Image>();
                tabAchievImg.sprite = tabActiveBg;
                tabAchievImg.type = Image.Type.Sliced;
                var tabAchievBtn = tabAchiev.gameObject.AddComponent<Button>();
                var tabAchievTxt = CreateExplicitText(tabAchiev, "Label", "Achievements", fSemiBold, 12.5f, Color.white, TextAlignmentOptions.Center);

                // Tab 2: Challenges (Inactive dark button)
                var tabChallenges = CreateExplicitRect(tabSegment, "Tab_Challenges", 0.5f, 0f, 1f, 1f);
                tabChallenges.offsetMin = new Vector2(2f, 2f);
                tabChallenges.offsetMax = new Vector2(-2f, -2f);
                var tabChallengesImg = tabChallenges.gameObject.AddComponent<Image>();
                tabChallengesImg.sprite = tabInactiveBg;
                tabChallengesImg.type = Image.Type.Sliced;
                var tabChallengesBtn = tabChallenges.gameObject.AddComponent<Button>();
                var tabChallengesTxt = CreateExplicitText(tabChallenges, "Label", "Challenges", fMedium, 12.5f, Hex("#64748B"), TextAlignmentOptions.Center);

                // Right: Filters (Sort By, Status, Game)
                var filtersContainer = CreateExplicitRect(tabBarRow, "FiltersContainer", 1f, 0.5f, 1f, 0.5f);
                filtersContainer.pivot = new Vector2(1f, 0.5f);
                filtersContainer.sizeDelta = new Vector2(340f, 38f);
                filtersContainer.anchoredPosition = new Vector2(0f, 0f);

                var filtersHlg = filtersContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                filtersHlg.childAlignment = TextAnchor.MiddleRight;
                filtersHlg.spacing = 10f;
                filtersHlg.childControlWidth = false;
                filtersHlg.childControlHeight = false;

                CreateFilterPill(filtersContainer, "Filter_SortBy", "Sort By", 102f);
                CreateFilterPill(filtersContainer, "Filter_Status", "Status", 96f);
                CreateFilterPill(filtersContainer, "Filter_Game", "Game", 96f);

                // -----------------------------------------------------------------
                // 4. Table Section (Top y = -222px to bottom)
                // -----------------------------------------------------------------
                var tableSection = CreateExplicitRect(mainContent, "Table_Section", 0f, 0f, 1f, 1f);
                tableSection.offsetMin = Vector2.zero;
                tableSection.offsetMax = new Vector2(0f, -222f);

                var tableImg = tableSection.gameObject.AddComponent<Image>();
                tableImg.sprite = tableBg;
                tableImg.type = Image.Type.Sliced;
                tableImg.color = Color.white;

                // 4a. Table Header Bar (Height: 40px)
                var tableHeader = CreateExplicitRect(tableSection, "Table_Header", 0f, 1f, 1f, 1f);
                tableHeader.pivot = new Vector2(0.5f, 1f);
                tableHeader.sizeDelta = new Vector2(0f, 40f);
                tableHeader.anchoredPosition = new Vector2(0f, 0f);

                var thImg = tableHeader.gameObject.AddComponent<Image>();
                thImg.sprite = tableHeaderBg;
                thImg.type = Image.Type.Sliced;
                thImg.color = new Color(1f, 1f, 1f, 0.8f);

                var thInfoTmp = CreateHeaderCol(tableHeader, "TH_Info", "Achievement Info", 0.015f, 0.430f);
                CreateHeaderCol(tableHeader, "TH_Game", "Game", 0.430f, 0.570f);
                var thPointsTmp = CreateHeaderCol(tableHeader, "TH_Points", "Achievement Points", 0.570f, 0.710f);
                CreateHeaderCol(tableHeader, "TH_Progress", "Progress Bar", 0.710f, 0.860f);
                CreateHeaderCol(tableHeader, "TH_Action", "Action", 0.860f, 0.985f, TextAlignmentOptions.Center);

                // 4b. Table ScrollView (ScrollRect)
                var scrollView = CreateExplicitRect(tableSection, "ScrollView", 0f, 0f, 1f, 1f);
                scrollView.offsetMin = new Vector2(6f, 6f);
                scrollView.offsetMax = new Vector2(-6f, -44f);

                var scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.elasticity = 0.1f;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 35f;

                var viewport = CreateExplicitRect(scrollView, "Viewport", 0f, 0f, 1f, 1f);
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;
                var mask = viewport.gameObject.AddComponent<RectMask2D>();

                // 4c. Achievements Content
                var contentAchiev = CreateExplicitRect(viewport, "Content_Achievements", 0f, 1f, 1f, 1f);
                contentAchiev.pivot = new Vector2(0.5f, 1f);
                contentAchiev.sizeDelta = new Vector2(0f, 0f);

                var vlgContentAchiev = contentAchiev.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgContentAchiev.childAlignment = TextAnchor.UpperCenter;
                vlgContentAchiev.spacing = 6f;
                vlgContentAchiev.padding = new RectOffset(4, 4, 6, 6);
                vlgContentAchiev.childControlWidth = true;
                vlgContentAchiev.childControlHeight = false;
                vlgContentAchiev.childForceExpandWidth = true;
                vlgContentAchiev.childForceExpandHeight = false;

                var csfAchiev = contentAchiev.gameObject.AddComponent<ContentSizeFitter>();
                csfAchiev.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 4d. Challenges Content
                var contentChallenges = CreateExplicitRect(viewport, "Content_Challenges", 0f, 1f, 1f, 1f);
                contentChallenges.pivot = new Vector2(0.5f, 1f);
                contentChallenges.sizeDelta = new Vector2(0f, 0f);

                var vlgContentChal = contentChallenges.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgContentChal.childAlignment = TextAnchor.UpperCenter;
                vlgContentChal.spacing = 6f;
                vlgContentChal.padding = new RectOffset(4, 4, 6, 6);
                vlgContentChal.childControlWidth = true;
                vlgContentChal.childControlHeight = false;
                vlgContentChal.childForceExpandWidth = true;
                vlgContentChal.childForceExpandHeight = false;

                var csfChal = contentChallenges.gameObject.AddComponent<ContentSizeFitter>();
                csfChal.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Set default viewport content
                scrollRect.viewport = viewport;
                scrollRect.content = contentAchiev;

                // Load entry prefab
                var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
                var entryElem = entryPrefab.GetComponent<AchievementElement>();

                // Instantiate 5 sample rows for Achievements tab (Star icon)
                InstantiateSampleRow(contentAchiev, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentAchiev, entryPrefab, "Class_B_Block _Mode_Achiev_Icon",
                    "Reach class B in Block Mode",
                    "Reach class B in Block Mode",
                    "Block Game", starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentAchiev, entryPrefab, "Tutorial_Concentrate_Achiev_Icon",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Concentrate Game", starSprite, "200", "5/5", 1.0f, RowActionState.Claimed);

                InstantiateSampleRow(contentAchiev, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", starSprite, "200", "2/5", 0.4f, RowActionState.InProgress);

                InstantiateSampleRow(contentAchiev, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                // Instantiate 5 sample rows for Challenges tab (Gold Coin icon + Challenge titles)
                InstantiateSampleRow(contentChallenges, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Play 50 games across all block game mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentChallenges, entryPrefab, "Class_B_Block _Mode_Achiev_Icon",
                    "Reach class B in Block Mode",
                    "Reach class B in Block Mode",
                    "Block Game", coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentChallenges, entryPrefab, "Tutorial_Concentrate_Achiev_Icon",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Concentrate Game", coinIcon, "200", "5/5", 1.0f, RowActionState.Claimed);

                InstantiateSampleRow(contentChallenges, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", coinIcon, "200", "2/5", 0.4f, RowActionState.InProgress);

                InstantiateSampleRow(contentChallenges, entryPrefab, "Tutorial_Block_Achiev_Icon",
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                contentChallenges.gameObject.SetActive(false);

                // Wire Serialized Object properties on AchievementUI
                var soUI = new SerializedObject(achUI);
                soUI.FindProperty("<RootCanvasGroup>k__BackingField").objectReferenceValue = cg;
                soUI.FindProperty("totalAchievementsLabel").objectReferenceValue = totalAchievTmp;
                soUI.FindProperty("totalAchievementsPointsLabel").objectReferenceValue = pointsTmp;
                soUI.FindProperty("categoryAchievementCompletedLabel").objectReferenceValue = rankTmp;
                soUI.FindProperty("achievementElementPrefab").objectReferenceValue = entryElem;
                soUI.FindProperty("achievementElementParent").objectReferenceValue = contentAchiev;
                soUI.ApplyModifiedPropertiesWithoutUndo();

                // Wire Serialized Object properties on AchievementsTabController
                var soTab = new SerializedObject(tabCtrl);
                soTab.FindProperty("achievementsTabButton").objectReferenceValue = tabAchievBtn;
                soTab.FindProperty("challengesTabButton").objectReferenceValue = tabChallengesBtn;
                soTab.FindProperty("achievementsTabBg").objectReferenceValue = tabAchievImg;
                soTab.FindProperty("achievementsTabLabel").objectReferenceValue = tabAchievTxt;
                soTab.FindProperty("challengesTabBg").objectReferenceValue = tabChallengesImg;
                soTab.FindProperty("challengesTabLabel").objectReferenceValue = tabChallengesTxt;
                soTab.FindProperty("activeTabSprite").objectReferenceValue = tabActiveBg;
                soTab.FindProperty("inactiveTabSprite").objectReferenceValue = tabInactiveBg;
                soTab.FindProperty("achievementsCardsContainer").objectReferenceValue = cardsSectionAchiev.gameObject;
                soTab.FindProperty("challengesCardsContainer").objectReferenceValue = cardsSectionChallenges.gameObject;
                soTab.FindProperty("colInfoLabel").objectReferenceValue = thInfoTmp;
                soTab.FindProperty("colRewardLabel").objectReferenceValue = thPointsTmp;
                soTab.FindProperty("scrollRect").objectReferenceValue = scrollRect;
                soTab.FindProperty("achievementsTableContent").objectReferenceValue = contentAchiev.gameObject;
                soTab.FindProperty("challengesTableContent").objectReferenceValue = contentChallenges.gameObject;
                soTab.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[AchievementsRestyler] Achievements_Screen prefab restyled successfully with dual tabs!");
        }

        // -------------------------------------------------------------------------------------------------------------
        // UI BUILDING HELPERS
        // -------------------------------------------------------------------------------------------------------------
        private static void InstantiateSampleRow(Transform parent, GameObject prefab, string iconName, string title, string desc, string game, Sprite rewardSp, string points, string progressStr, float progressFill, RowActionState actionState)
        {
            var rowGo = UnityEngine.Object.Instantiate(prefab, parent, false);
            rowGo.name = $"Row_{title.Substring(0, Mathf.Min(16, title.Length)).Trim()}";

            var iconImg = rowGo.transform.Find("Col_Info/IconBox/AchievementIcon")?.GetComponent<Image>();
            if (iconImg)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{AchievIconsDir}/{iconName}.png");
                if (sp) iconImg.sprite = sp;
            }

            var titleTmp = rowGo.transform.Find("Col_Info/NameLabel")?.GetComponent<TMP_Text>();
            if (titleTmp) titleTmp.text = title;

            var descTmp = rowGo.transform.Find("Col_Info/DescLabel")?.GetComponent<TMP_Text>();
            if (descTmp) descTmp.text = desc;

            var gameTmp = rowGo.transform.Find("Col_Game/GameLabel")?.GetComponent<TMP_Text>();
            if (gameTmp) gameTmp.text = game;

            var gameIcon = rowGo.transform.Find("Col_Game/GameIcon")?.GetComponent<Image>();
            if (gameIcon && game.Contains("Concentrate") && searchIconSprite)
                gameIcon.sprite = searchIconSprite;

            var starImg = rowGo.transform.Find("Col_Points/StarIcon")?.GetComponent<Image>();
            if (starImg && rewardSp) starImg.sprite = rewardSp;

            var pointsTmp = rowGo.transform.Find("Col_Points/PointsLabel")?.GetComponent<TMP_Text>();
            if (pointsTmp) pointsTmp.text = points;

            var reqTmp = rowGo.transform.Find("Col_Progress/RequirementLabel")?.GetComponent<TMP_Text>();
            if (reqTmp) reqTmp.text = progressStr;

            var fillRt = rowGo.transform.Find("Col_Progress/ProgressTrack/ProgressFill") as RectTransform;
            if (fillRt) fillRt.anchorMax = new Vector2(progressFill, 1f);

            var claimBtn = rowGo.transform.Find("Col_Action/ClaimButton");
            var claimed = rowGo.transform.Find("Col_Action/CompletedContainer");
            var inProgress = rowGo.transform.Find("Col_Action/IncompletedContainer");

            if (claimBtn) claimBtn.gameObject.SetActive(actionState == RowActionState.Claim);
            if (claimed) claimed.gameObject.SetActive(actionState == RowActionState.Claimed);
            if (inProgress) inProgress.gameObject.SetActive(actionState == RowActionState.InProgress);
        }

        private static void CreateMetricCard(Transform parent, string name, Sprite icon, Color iconColor, string defaultVal, string subtext, out TMP_Text valueTmp)
        {
            var card = CreateExplicitRect(parent, name, 0f, 0f, 1f, 1f);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = cardBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var iconGo = CreateExplicitRect(card, "Icon", 0f, 0.5f, 0f, 0.5f);
            iconGo.pivot = new Vector2(0.5f, 0.5f);
            iconGo.sizeDelta = new Vector2(46f, 46f);
            iconGo.anchoredPosition = new Vector2(46f, 0f);
            var iconImg = iconGo.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = iconColor;
            iconImg.preserveAspect = true;

            valueTmp = CreateExplicitText(card, "ValueText", defaultVal, fBold, 25f, Color.white, TextAlignmentOptions.MidlineLeft);
            var vRt = valueTmp.GetComponent<RectTransform>();
            vRt.pivot = new Vector2(0f, 0.5f);
            vRt.anchorMin = new Vector2(0f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(88f, 15f);
            vRt.sizeDelta = new Vector2(-96f, 32f);

            var subTmp = CreateExplicitText(card, "Subtext", subtext, fMedium, 12.5f, Hex("#8E9CAE"), TextAlignmentOptions.MidlineLeft);
            var sRt = subTmp.GetComponent<RectTransform>();
            sRt.pivot = new Vector2(0f, 0.5f);
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.anchoredPosition = new Vector2(88f, -16f);
            sRt.sizeDelta = new Vector2(-96f, 22f);
        }

        private static void CreateChallengeMetricCard(Transform parent, string name, string title, string fraction, float fillAmount)
        {
            var card = CreateExplicitRect(parent, name, 0f, 0f, 1f, 1f);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = cardBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var iconGo = CreateExplicitRect(card, "Icon", 0f, 0.5f, 0f, 0.5f);
            iconGo.pivot = new Vector2(0.5f, 0.5f);
            iconGo.sizeDelta = new Vector2(44f, 44f);
            iconGo.anchoredPosition = new Vector2(46f, 0f);
            var iconImg = iconGo.gameObject.AddComponent<Image>();
            iconImg.sprite = calendarSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;

            var titleTmp = CreateExplicitText(card, "TitleText", title, fSemiBold, 13.5f, Hex("#E2E8F0"), TextAlignmentOptions.MidlineLeft);
            var tRt = titleTmp.GetComponent<RectTransform>();
            tRt.pivot = new Vector2(0f, 0.5f);
            tRt.anchorMin = new Vector2(0f, 0.5f);
            tRt.anchorMax = new Vector2(1f, 0.5f);
            tRt.anchoredPosition = new Vector2(88f, 15f);
            tRt.sizeDelta = new Vector2(-96f, 26f);

            var track = CreateExplicitRect(card, "ProgressTrack", 0f, 0.5f, 0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(140f, 8f);
            track.anchoredPosition = new Vector2(88f, -14f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = progressTrackBg;
            trackImg.type = Image.Type.Sliced;

            var fill = CreateExplicitRect(track, "ProgressFill", 0f, 0f, fillAmount, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = progressFillBg;
            fillImg.type = Image.Type.Sliced;

            var fracTmp = CreateExplicitText(card, "FractionText", fraction, fMedium, 12f, Hex("#94A3B8"), TextAlignmentOptions.MidlineLeft);
            var fRt = fracTmp.GetComponent<RectTransform>();
            fRt.pivot = new Vector2(0f, 0.5f);
            fRt.anchorMin = new Vector2(0f, 0.5f);
            fRt.anchorMax = new Vector2(1f, 0.5f);
            fRt.anchoredPosition = new Vector2(236f, -14f);
            fRt.sizeDelta = new Vector2(60f, 20f);
        }

        private static void CreateFilterPill(Transform parent, string name, string label, float width)
        {
            var pill = CreateExplicitRect(parent, name, 0f, 0.5f, 0f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 36f);
            var img = pill.gameObject.AddComponent<Image>();
            img.sprite = dropdownPillBg;
            img.type = Image.Type.Sliced;

            var txt = CreateExplicitText(pill, "Label", label, fMedium, 12f, Hex("#CBD5E1"), TextAlignmentOptions.MidlineLeft);
            var tRt = txt.GetComponent<RectTransform>();
            tRt.pivot = new Vector2(0f, 0.5f);
            tRt.anchorMin = new Vector2(0f, 0.5f);
            tRt.anchorMax = new Vector2(1f, 0.5f);
            tRt.anchoredPosition = new Vector2(12f, 0f);
            tRt.sizeDelta = new Vector2(-36f, 24f);

            var arrow = CreateExplicitRect(pill, "Arrow", 1f, 0.5f, 1f, 0.5f);
            arrow.pivot = new Vector2(1f, 0.5f);
            arrow.sizeDelta = new Vector2(10f, 10f);
            arrow.anchoredPosition = new Vector2(-12f, 0f);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = chevronDown;
            arrowImg.color = Hex("#94A3B8");
            arrowImg.preserveAspect = true;
        }

        private static TMP_Text CreateHeaderCol(Transform parent, string name, string title, float xMin, float xMax, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var col = CreateExplicitRect(parent, name, xMin, 0f, xMax, 1f);
            var txt = CreateExplicitText(col, "Title", title, fSemiBold, 12f, Hex("#8E9CAE"), align);
            var tRt = txt.GetComponent<RectTransform>();
            tRt.offsetMin = new Vector2(6f, 0f);
            tRt.offsetMax = new Vector2(-6f, 0f);
            return txt;
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

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                bool inside = false;
                if (nx >= 0.2f && nx <= 0.75f)
                {
                    float halfH = Mathf.Lerp(0.08f, 0.35f, (nx - 0.2f) / 0.55f);
                    if (Mathf.Abs(ny - 0.5f) <= halfH) inside = true;
                }
                if (nx >= 0.12f && nx < 0.2f && Mathf.Abs(ny - 0.5f) <= 0.12f) inside = true;
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
                if (nx >= 0.15f && nx <= 0.85f && ny >= 0.15f && ny <= 0.85f)
                    inside = true;
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
