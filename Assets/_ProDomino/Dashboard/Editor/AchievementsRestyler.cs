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
    ///         1) Total Achievements (Trophy icon, 12 / 20, Total Achievements)
    ///         2) Total Achievement Points (Star icon, 12, Total Achievement Points)
    ///         3) Current Rank (Class C crest, Class C, Current Rank)
    ///     [Challenges tab]:
    ///         1) Total Daily Challenges (Daily calendar icon with red header, binder rings & star, progress bar, 1/30)
    ///         2) Total Weekly Challenges (Weekly calendar icon with grid, progress bar, 1/4)
    ///         3) Total Monthly Challenges (Monthly calendar icon with dates, progress bar, 0/1)
    /// - Tab Bar & Filters Row:
    ///     Left: [ Achievements | Challenges ] interactive segmented pill button
    ///     Right: [ Sort By v ] [ Status v ] [ Game v ] dropdown pills
    /// - Table Section:
    ///     5-Column Header: Achievement/Challenge Info, Game, Achievement Points/Reward, Progress Bar, Action
    ///     Column 1: Pink/magenta quest badge icon + Title + Subtitle
    ///     Column 2: 3D Golden box (PlayGames_Block_Illustration) / 3D Magnifying glass (PlayGames_Concentrate_Illustration) + Game name
    ///     Column 3: 3D Domino Coin (Challenges) or Star (Achievements) + Reward count
    ///     Column 4: Pill progress track with gold fill + fraction label
    ///     Column 5: Solid yellow Claim button, dark bordered Claimed button, dark translucent In Progress button
    ///     Subtle 1px row dividers and dark card background matching Figma
    /// </summary>
    internal static class AchievementsRestyler
    {
        private const string ScreenPrefabPath = "Assets/_ProDomino/Prefabs/UI/Achievements_Screen.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/Prefabs/UI/Achiev_List_Container.prefab";
        private const string RankIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Rank";
        private const string DashboardIconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";
        private const string Base64IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Base_64";
        private const string Base128IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Base_128";
        private const string ArtDashboardDir = "Assets/_ProDomino/_Art/Dashboard";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite starSprite, hornSprite, questBadgeSprite;
        private static Sprite calDailySprite, calWeeklySprite, calMonthlySprite;
        private static Sprite trophyIcon, classCIcon, chevronDown, coinIcon;
        private static Sprite blockGameIllustration, concentrateIllustration;
        private static Sprite cardBg, tableBg, tableHeaderBg, tabActiveBg, tabInactiveBg, screenCardBg;
        private static Sprite btnClaimBg, btnClaimedBg, btnInProgressBg, progressTrackBg, progressFillBg, dropdownPillBg;
        private static Sprite rowDivider;

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
            if (!SessionState.GetBool("PD_AchievementsRestyler_Ran_v4", false))
            {
                SessionState.SetBool("PD_AchievementsRestyler_Ran_v4", true);
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

            // Coin Icon
            coinIcon = EnsureSprite($"{ArtDashboardDir}/Icon_Coin_Raster.png")
                       ?? EnsureSprite($"{DashboardIconsDir}/Icon_Coin.png");

            // 3D Game illustrations from Figma
            blockGameIllustration = EnsureSprite($"{ArtDashboardDir}/PlayGames_Block_Illustration.png");
            concentrateIllustration = EnsureSprite($"{ArtDashboardDir}/PlayGames_Concentrate_Illustration.png");

            starSprite = MakeStarSprite("Achiev_Star_Gold", 64, Hex("#FBBF24"));
            hornSprite = MakeHornSprite("Achiev_Horn_Gold", 64, Hex("#FBBF24"));

            // Figma Calendar icons with red header, rings, and glowing backdrops
            calDailySprite = EnsureSprite($"{GeneratedDir}/Achiev_Calendar_Daily.png")
                             ?? MakeCalendarSprite("Achiev_Calendar_Daily", 128, Hex("#EF4444"), "daily");
            calWeeklySprite = EnsureSprite($"{GeneratedDir}/Achiev_Calendar_Weekly.png")
                              ?? MakeCalendarSprite("Achiev_Calendar_Weekly", 128, Hex("#EF4444"), "weekly");
            calMonthlySprite = EnsureSprite($"{GeneratedDir}/Achiev_Calendar_Monthly.png")
                               ?? MakeCalendarSprite("Achiev_Calendar_Monthly", 128, Hex("#EF4444"), "monthly");

            // Figma Magenta Quest Badge / Book
            questBadgeSprite = EnsureSprite($"{GeneratedDir}/Achiev_Quest_Badge.png")
                               ?? MakeQuestBadgeSprite("Achiev_Quest_Badge", 128);

            // Container and panel sprites
            screenCardBg = GetOrCreateScreenCardSprite();
            cardBg = MakePanelSprite("Achiev_CardBg", 48, 48, 12, Hex("#0E1322"), Hex("#090E1A"), Hex("#1C263A"), 1f);
            tableBg = MakePanelSprite("Achiev_TableBg", 48, 48, 14, Hex("#070A12"), Hex("#05080E"), Hex("#141A28"), 0.8f);
            tableHeaderBg = MakePanelSprite("Achiev_TableHeaderBg", 32, 32, 8, Hex("#131A2B"), Hex("#0F1524"), Hex("#1E283C"), 1f);
            tabActiveBg = MakePanelSprite("Achiev_TabActiveBg", 32, 32, 8, Hex("#2563EB"), Hex("#1D4ED8"), Hex("#3B82F6"), 1f);
            tabInactiveBg = MakePanelSprite("Achiev_TabInactiveBg", 32, 32, 8, Hex("#0E1422"), Hex("#0A0F1A"), Hex("#1A2336"), 1f);
            dropdownPillBg = MakePanelSprite("Achiev_DropdownPillBg", 32, 32, 8, Hex("#0F1524"), Hex("#0B101D"), Hex("#222D42"), 1f);

            // Action Buttons
            btnClaimBg = MakePanelSprite("Achiev_BtnClaimBg", 32, 32, 14, Hex("#FBBF24"), Hex("#F59E0B"), Color.clear, 0f);
            btnClaimedBg = MakePanelSprite("Achiev_BtnClaimedBg", 32, 32, 14, Hex("#1E170E"), Hex("#17120A"), Hex("#B45309"), 1f);
            btnInProgressBg = MakePanelSprite("Achiev_BtnInProgressBg", 32, 32, 14, Hex("#131824"), Hex("#0F131D"), Hex("#1E293B"), 1f);

            // Progress Bar
            progressTrackBg = MakePanelSprite("Achiev_ProgressTrack", 32, 16, 8, Hex("#1A2234"), Hex("#141A28"), Hex("#263248"), 1f);
            progressFillBg = MakePanelSprite("Achiev_ProgressFill", 32, 16, 8, Hex("#FBBF24"), Hex("#F59E0B"), Color.clear, 0f);

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
                bg.color = Color.clear; // Transparent row background with bottom divider

                var elem = root.GetComponent<AchievementElement>() ?? root.AddComponent<AchievementElement>();

                // Clear legacy children
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // Subtle 1px Row Divider Line at Bottom
                var divider = CreateExplicitRect(root.transform, "RowDivider", 0f, 0f, 1f, 0f);
                divider.sizeDelta = new Vector2(0f, 1f);
                var divImg = divider.gameObject.AddComponent<Image>();
                divImg.color = Hex("#161F2E");

                // 1. Column: Achievement / Challenge Info [0.015 .. 0.430]
                var colInfo = CreateExplicitRect(root.transform, "Col_Info", 0.015f, 0f, 0.430f, 1f);

                // Vertical Quest Badge / Book Icon
                var achievIconGo = CreateExplicitRect(colInfo, "AchievementIcon", 0f, 0.5f, 0f, 0.5f);
                achievIconGo.pivot = new Vector2(0f, 0.5f);
                achievIconGo.sizeDelta = new Vector2(32f, 40f);
                achievIconGo.anchoredPosition = new Vector2(4f, 0f);
                var achievIconImg = achievIconGo.gameObject.AddComponent<Image>();
                achievIconImg.sprite = questBadgeSprite;
                achievIconImg.preserveAspect = true;

                // Title & Description (explicit positions)
                var titleTmp = CreateExplicitText(colInfo, "NameLabel", "Play 50 games across all block game mode", fSemiBold, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
                var titleRt = titleTmp.GetComponent<RectTransform>();
                titleRt.pivot = new Vector2(0f, 0.5f);
                titleRt.anchorMin = new Vector2(0f, 0.5f);
                titleRt.anchorMax = new Vector2(1f, 0.5f);
                titleRt.anchoredPosition = new Vector2(46f, 10f);
                titleRt.sizeDelta = new Vector2(-52f, 22f);
                titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                titleTmp.overflowMode = TextOverflowModes.Ellipsis;

                var descTmp = CreateExplicitText(colInfo, "DescLabel", "Complete all of the tutorials available for the Block Mode", fRegular, 10.5f, Hex("#8E9CAE"), TextAlignmentOptions.MidlineLeft);
                var descRt = descTmp.GetComponent<RectTransform>();
                descRt.pivot = new Vector2(0f, 0.5f);
                descRt.anchorMin = new Vector2(0f, 0.5f);
                descRt.anchorMax = new Vector2(1f, 0.5f);
                descRt.anchoredPosition = new Vector2(46f, -10f);
                descRt.sizeDelta = new Vector2(-52f, 20f);
                descTmp.textWrappingMode = TextWrappingModes.NoWrap;
                descTmp.overflowMode = TextOverflowModes.Ellipsis;

                // 2. Column: Game [0.430 .. 0.570]
                var colGame = CreateExplicitRect(root.transform, "Col_Game", 0.430f, 0f, 0.570f, 1f);
                var gameIconGo = CreateExplicitRect(colGame, "GameIcon", 0f, 0.5f, 0f, 0.5f);
                gameIconGo.pivot = new Vector2(0f, 0.5f);
                gameIconGo.sizeDelta = new Vector2(34f, 34f);
                gameIconGo.anchoredPosition = new Vector2(4f, 0f);
                var gameIconImg = gameIconGo.gameObject.AddComponent<Image>();
                gameIconImg.sprite = blockGameIllustration;
                gameIconImg.color = Color.white;
                gameIconImg.preserveAspect = true;

                var gameLabelTmp = CreateExplicitText(colGame, "GameLabel", "Block Game", fMedium, 12.5f, Hex("#E2E8F0"), TextAlignmentOptions.MidlineLeft);
                var glRt = gameLabelTmp.GetComponent<RectTransform>();
                glRt.pivot = new Vector2(0f, 0.5f);
                glRt.anchorMin = new Vector2(0f, 0.5f);
                glRt.anchorMax = new Vector2(1f, 0.5f);
                glRt.anchoredPosition = new Vector2(44f, 0f);
                glRt.sizeDelta = new Vector2(-48f, 28f);

                // 3. Column: Achievement Points / Reward [0.570 .. 0.710]
                var colPoints = CreateExplicitRect(root.transform, "Col_Points", 0.570f, 0f, 0.710f, 1f);
                var starGo = CreateExplicitRect(colPoints, "StarIcon", 0f, 0.5f, 0f, 0.5f);
                starGo.pivot = new Vector2(0f, 0.5f);
                starGo.sizeDelta = new Vector2(22f, 22f);
                starGo.anchoredPosition = new Vector2(4f, 0f);
                var starImg = starGo.gameObject.AddComponent<Image>();
                starImg.sprite = starSprite;
                starImg.color = Color.white;
                starImg.preserveAspect = true;

                var pointsTmp = CreateExplicitText(colPoints, "PointsLabel", "200", fSemiBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                var pRt = pointsTmp.GetComponent<RectTransform>();
                pRt.pivot = new Vector2(0f, 0.5f);
                pRt.anchorMin = new Vector2(0f, 0.5f);
                pRt.anchorMax = new Vector2(1f, 0.5f);
                pRt.anchoredPosition = new Vector2(32f, 0f);
                pRt.sizeDelta = new Vector2(-36f, 24f);

                // 4. Column: Progress Bar [0.710 .. 0.860]
                var colProgress = CreateExplicitRect(root.transform, "Col_Progress", 0.710f, 0f, 0.860f, 1f);
                var track = CreateExplicitRect(colProgress, "ProgressTrack", 0f, 0.5f, 0f, 0.5f);
                track.pivot = new Vector2(0f, 0.5f);
                track.sizeDelta = new Vector2(92f, 8f);
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

                var reqTmp = CreateExplicitText(colProgress, "RequirementLabel", "2/5", fMedium, 12f, Hex("#CBD5E1"), TextAlignmentOptions.MidlineLeft);
                var reqRt = reqTmp.GetComponent<RectTransform>();
                reqRt.pivot = new Vector2(0f, 0.5f);
                reqRt.anchorMin = new Vector2(0f, 0.5f);
                reqRt.anchorMax = new Vector2(1f, 0.5f);
                reqRt.anchoredPosition = new Vector2(104f, 0f);
                reqRt.sizeDelta = new Vector2(-108f, 24f);

                // 5. Column: Action [0.860 .. 0.985]
                var colAction = CreateExplicitRect(root.transform, "Col_Action", 0.860f, 0f, 0.985f, 1f);

                // Button 1: Claim (Ready) - Solid vibrant yellow pill with dark bold text
                var btnClaimGo = CreateExplicitRect(colAction, "ClaimButton", 0.5f, 0.5f, 0.5f, 0.5f);
                btnClaimGo.sizeDelta = new Vector2(82f, 28f);
                btnClaimGo.anchoredPosition = Vector2.zero;
                var btnClaimImg = btnClaimGo.gameObject.AddComponent<Image>();
                btnClaimImg.sprite = btnClaimBg;
                btnClaimImg.type = Image.Type.Sliced;
                var claimBtn = btnClaimGo.gameObject.AddComponent<Button>();
                var claimBtnText = CreateExplicitText(btnClaimGo, "ClaimText", "Claim", fBold, 12f, Hex("#0A0F1D"), TextAlignmentOptions.Center);

                // Container 2: Claimed (Completed) - Dark pill with amber border & text
                var containerClaimed = CreateExplicitRect(colAction, "CompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerClaimed.sizeDelta = new Vector2(82f, 28f);
                containerClaimed.anchoredPosition = Vector2.zero;
                var claimedImg = containerClaimed.gameObject.AddComponent<Image>();
                claimedImg.sprite = btnClaimedBg;
                claimedImg.type = Image.Type.Sliced;
                var claimedText = CreateExplicitText(containerClaimed, "ClaimedText", "Claimed", fSemiBold, 11.5f, Hex("#F59E0B"), TextAlignmentOptions.Center);
                containerClaimed.gameObject.SetActive(false);

                // Container 3: In Progress (Not ready) - Dark translucent pill with silver text
                var containerInProgress = CreateExplicitRect(colAction, "IncompletedContainer", 0.5f, 0.5f, 0.5f, 0.5f);
                containerInProgress.sizeDelta = new Vector2(82f, 28f);
                containerInProgress.anchoredPosition = Vector2.zero;
                var inProgressImg = containerInProgress.gameObject.AddComponent<Image>();
                inProgressImg.sprite = btnInProgressBg;
                inProgressImg.type = Image.Type.Sliced;
                var inProgressText = CreateExplicitText(containerInProgress, "InProgressText", "In progress", fMedium, 11f, Hex("#94A3B8"), TextAlignmentOptions.Center);
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
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                // Framed Screen Card Background (1px #1E2538 border, 14px rounded corners, deep midnight card fill)
                var bg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                bg.sprite = screenCardBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                if (root.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlgRoot)
                    UnityEngine.Object.DestroyImmediate(vlgRoot);

                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // MainContent Container (Padded inside the rounded card)
                var mainContent = CreateExplicitRect(root.transform, "MainContent", 0f, 0f, 1f, 1f);
                mainContent.offsetMin = new Vector2(24f, 20f);
                mainContent.offsetMax = new Vector2(-24f, -16f);

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

                CreateMetricCard(cardsSectionAchiev, "Card_TotalAchievements", trophyIcon, Hex("#FBBF24"), "12 / 20", "Total Achievements", out var totalAchievTmp);
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

                CreateChallengeMetricCard(cardsSectionChallenges, "Card_DailyChallenges", calDailySprite, "Total Daily Challenges", "1/30", 1f / 30f);
                CreateChallengeMetricCard(cardsSectionChallenges, "Card_WeeklyChallenges", calWeeklySprite, "Total Weekly Challenges", "1/4", 0.25f);
                CreateChallengeMetricCard(cardsSectionChallenges, "Card_MonthlyChallenges", calMonthlySprite, "Total Monthly Challenges", "0/1", 0.05f);
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
                thImg.color = new Color(1f, 1f, 1f, 0.95f);

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
                viewport.gameObject.AddComponent<RectMask2D>();

                // 4c. Achievements Content
                var contentAchiev = CreateExplicitRect(viewport, "Content_Achievements", 0f, 1f, 1f, 1f);
                contentAchiev.pivot = new Vector2(0.5f, 1f);
                contentAchiev.sizeDelta = new Vector2(0f, 0f);

                var vlgContentAchiev = contentAchiev.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgContentAchiev.childAlignment = TextAnchor.UpperCenter;
                vlgContentAchiev.spacing = 2f;
                vlgContentAchiev.padding = new RectOffset(4, 4, 4, 4);
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
                vlgContentChal.spacing = 2f;
                vlgContentChal.padding = new RectOffset(4, 4, 4, 4);
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
                InstantiateSampleRow(contentAchiev, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentAchiev, entryPrefab, questBadgeSprite,
                    "Reach class B in Block Mode",
                    "Reach class B in Block Mode",
                    "Block Game", blockGameIllustration, starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentAchiev, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Concentrate Game", concentrateIllustration, starSprite, "200", "5/5", 1.0f, RowActionState.Claimed);

                InstantiateSampleRow(contentAchiev, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, starSprite, "200", "2/5", 0.4f, RowActionState.InProgress);

                InstantiateSampleRow(contentAchiev, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, starSprite, "200", "2/5", 0.4f, RowActionState.Claim);

                // Instantiate 5 sample rows for Challenges tab (Gold Domino Coin + 3D Illustrations)
                InstantiateSampleRow(contentChallenges, entryPrefab, questBadgeSprite,
                    "Play 50 games across all block game mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentChallenges, entryPrefab, questBadgeSprite,
                    "Reach class B in Block Mode",
                    "Reach class B in Block Mode",
                    "Block Game", blockGameIllustration, coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                InstantiateSampleRow(contentChallenges, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Complete all of the tutorials available for the Concentrate Mode",
                    "Concentrate Game", concentrateIllustration, coinIcon, "200", "5/5", 1.0f, RowActionState.Claimed);

                InstantiateSampleRow(contentChallenges, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, coinIcon, "200", "2/5", 0.4f, RowActionState.InProgress);

                InstantiateSampleRow(contentChallenges, entryPrefab, questBadgeSprite,
                    "Complete all of the tutorials available for the Block Mode",
                    "Complete all of the tutorials available for the Block Mode",
                    "Block Game", blockGameIllustration, coinIcon, "200", "2/5", 0.4f, RowActionState.Claim);

                contentChallenges.gameObject.SetActive(false);

                // Wire Serialized Object properties on AchievementUI
                var soUI = new SerializedObject(achUI);
                var rootCgProp = soUI.FindProperty("<RootCanvasGroup>k__BackingField") ?? soUI.FindProperty("RootCanvasGroup") ?? soUI.FindProperty("_rootCanvasGroup");
                if (rootCgProp != null) rootCgProp.objectReferenceValue = cg;
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
        private static void InstantiateSampleRow(Transform parent, GameObject prefab, Sprite badgeSp, string title, string desc, string game, Sprite gameIllustrationSp, Sprite rewardSp, string points, string progressStr, float progressFill, RowActionState actionState)
        {
            var rowGo = UnityEngine.Object.Instantiate(prefab, parent, false);
            rowGo.name = $"Row_{title.Substring(0, Mathf.Min(16, title.Length)).Trim()}";

            var iconImg = rowGo.transform.Find("Col_Info/AchievementIcon")?.GetComponent<Image>();
            if (iconImg && badgeSp)
            {
                iconImg.sprite = badgeSp;
                iconImg.preserveAspect = true;
            }

            var titleTmp = rowGo.transform.Find("Col_Info/NameLabel")?.GetComponent<TMP_Text>();
            if (titleTmp) titleTmp.text = title;

            var descTmp = rowGo.transform.Find("Col_Info/DescLabel")?.GetComponent<TMP_Text>();
            if (descTmp) descTmp.text = desc;

            var gameTmp = rowGo.transform.Find("Col_Game/GameLabel")?.GetComponent<TMP_Text>();
            if (gameTmp) gameTmp.text = game;

            var gameIcon = rowGo.transform.Find("Col_Game/GameIcon")?.GetComponent<Image>();
            if (gameIcon && gameIllustrationSp)
            {
                gameIcon.sprite = gameIllustrationSp;
                gameIcon.color = Color.white;
                gameIcon.preserveAspect = true;
            }

            var starImg = rowGo.transform.Find("Col_Points/StarIcon")?.GetComponent<Image>();
            if (starImg && rewardSp)
            {
                starImg.sprite = rewardSp;
                starImg.preserveAspect = true;
            }

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
            iconGo.anchoredPosition = new Vector2(44f, 0f);
            var iconImg = iconGo.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = iconColor;
            iconImg.preserveAspect = true;

            valueTmp = CreateExplicitText(card, "ValueText", defaultVal, fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            var vRt = valueTmp.GetComponent<RectTransform>();
            vRt.pivot = new Vector2(0f, 0.5f);
            vRt.anchorMin = new Vector2(0f, 0.5f);
            vRt.anchorMax = new Vector2(1f, 0.5f);
            vRt.anchoredPosition = new Vector2(86f, 14f);
            vRt.sizeDelta = new Vector2(-92f, 30f);

            var subTmp = CreateExplicitText(card, "Subtext", subtext, fMedium, 12f, Hex("#8E9CAE"), TextAlignmentOptions.MidlineLeft);
            var sRt = subTmp.GetComponent<RectTransform>();
            sRt.pivot = new Vector2(0f, 0.5f);
            sRt.anchorMin = new Vector2(0f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.anchoredPosition = new Vector2(86f, -15f);
            sRt.sizeDelta = new Vector2(-92f, 22f);
        }

        private static void CreateChallengeMetricCard(Transform parent, string name, Sprite calendarSp, string title, string fraction, float fillAmount)
        {
            var card = CreateExplicitRect(parent, name, 0f, 0f, 1f, 1f);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = cardBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            // 1. Calendar Icon at Top-Left
            var iconGo = CreateExplicitRect(card, "Icon", 0f, 1f, 0f, 1f);
            iconGo.pivot = new Vector2(0f, 1f);
            iconGo.sizeDelta = new Vector2(40f, 40f);
            iconGo.anchoredPosition = new Vector2(22f, -14f);
            var iconImg = iconGo.gameObject.AddComponent<Image>();
            iconImg.sprite = calendarSp;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;

            // 2. Title Text in Middle
            var titleTmp = CreateExplicitText(card, "TitleText", title, fSemiBold, 12.5f, Color.white, TextAlignmentOptions.MidlineLeft);
            var tRt = titleTmp.GetComponent<RectTransform>();
            tRt.pivot = new Vector2(0f, 1f);
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.anchoredPosition = new Vector2(22f, -58f);
            tRt.sizeDelta = new Vector2(-44f, 22f);

            // 3. Progress Bar & Fraction at Bottom
            var track = CreateExplicitRect(card, "ProgressTrack", 0f, 1f, 0f, 1f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(145f, 8f);
            track.anchoredPosition = new Vector2(22f, -88f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = progressTrackBg;
            trackImg.type = Image.Type.Sliced;

            var fill = CreateExplicitRect(track, "ProgressFill", 0f, 0f, fillAmount, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = progressFillBg;
            fillImg.type = Image.Type.Sliced;

            var fracTmp = CreateExplicitText(card, "FractionText", fraction, fMedium, 11.5f, Hex("#94A3B8"), TextAlignmentOptions.MidlineLeft);
            var fRt = fracTmp.GetComponent<RectTransform>();
            fRt.pivot = new Vector2(0f, 0.5f);
            fRt.anchorMin = new Vector2(0f, 1f);
            fRt.anchorMax = new Vector2(0f, 1f);
            fRt.anchoredPosition = new Vector2(176f, -88f);
            fRt.sizeDelta = new Vector2(50f, 20f);
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
            tRt.anchoredPosition = new Vector2(14f, 0f);
            tRt.sizeDelta = new Vector2(-36f, 24f);

            var arrow = CreateExplicitRect(pill, "Arrow", 1f, 0.5f, 1f, 0.5f);
            arrow.pivot = new Vector2(1f, 0.5f);
            arrow.sizeDelta = new Vector2(12f, 12f);
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
            if (File.Exists(path))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }

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
            if (File.Exists(path))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }

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

        private static Sprite MakeCalendarSprite(string name, int size, Color headerColor, string type)
        {
            var path = $"{GeneratedDir}/{name}.png";
            if (File.Exists(path))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                Color c = Color.clear;

                // Soft radial glow
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= size * 0.44f)
                {
                    float glowAlpha = Mathf.Clamp01(1f - dist / (size * 0.44f)) * 0.35f;
                    c = new Color(headerColor.r, headerColor.g, headerColor.b, glowAlpha);
                }

                // Calendar body: 0.25 <= nx <= 0.75, 0.22 <= ny <= 0.78
                if (nx >= 0.25f && nx <= 0.75f && ny >= 0.22f && ny <= 0.78f)
                {
                    // Header band: 0.62 <= ny <= 0.78
                    if (ny >= 0.62f)
                        c = headerColor;
                    else
                        c = Color.Lerp(Color.white, Hex("#E2E8F0"), (0.62f - ny) / 0.40f);
                }

                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return SaveSprite(path, tex);
        }

        private static Sprite MakeQuestBadgeSprite(string name, int size)
        {
            var path = $"{GeneratedDir}/{name}.png";
            if (File.Exists(path))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color magenta = Hex("#E11D48");

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;

                Color c = Color.clear;
                // Vertical book: 0.30 <= nx <= 0.70, 0.20 <= ny <= 0.80
                if (nx >= 0.30f && nx <= 0.70f && ny >= 0.20f && ny <= 0.80f)
                {
                    c = magenta;
                    // Diamond emblem in center:
                    if (Mathf.Abs(nx - 0.5f) + Mathf.Abs(ny - 0.5f) <= 0.12f)
                        c = Hex("#FBBF24");
                }
                tex.SetPixel(x, y, c);
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
