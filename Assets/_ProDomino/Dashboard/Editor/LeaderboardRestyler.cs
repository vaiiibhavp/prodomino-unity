using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.Leaderboard;

namespace ProDomino.Dashboard.Editor
{
    // Restyles LeaderboardUI_NavPanel_New to match Figma reference (node 263:24215):
    // Header (Crown/Podium Icon + Title + "Your rank" pill badge + 4 dropdown filters),
    // Top 3 Podium (1st, 2nd, 3rd pedestals with glowing avatar frames, trophies, names, Elo score, and sunburst spotlight rays),
    // Table section with headers (#, Username, Elo Rank, W, L, 2nd, 3rd, W/L) and restyled row cards in a ScrollView,
    // Empty state (user avatar with red X + "Leaderboard is Empty"),
    // and completely deactivates the legacy LeaderboardUI_Old panel across prefabs and scenes.
    internal static class LeaderboardRestyler
    {
        private const string LeaderboardNewPath = "Assets/_ProDomino/LeaderboardSystem/Prefabs/LeaderboardUI_NavPanel_New.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/LeaderboardSystem/Prefabs/LeaderboardEntry_Prefab.prefab";
        private const string DropdownPrefabPath = "Assets/_ProDomino/Prefabs/UI/Dropdown_Choose_game_mode.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
        private const string IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Leaderboard";
        private const string DefaultAvatarPath = "Assets/_ProDomino/_Art/Avatars/Man_Avatar_1.png";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite crownSprite, trophyGold, trophySilver, trophyBronze, emptyStateSprite;
        private static Sprite glowGold, glowSilver, glowBronze, podiumBlockSprite, sunburstSprite;
        private static Sprite panelBg, rowSprite, dropdownBg, badgeBg, headerBg, defaultAvatarSprite;

        [MenuItem("ProDomino/Dashboard/Restyle Leaderboard + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            RestyleEntryPrefab();
            BuildCleanLeaderboardPrefab();
            DeactivateOldPanel();
            RevertStaleOverrides();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LeaderboardRestyler] SUCCESS: Leaderboard completely restyled to Figma design!");
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnce()
        {
            if (!SessionState.GetBool("PD_LeaderboardRestyler_Ran_v5", false))
            {
                SessionState.SetBool("PD_LeaderboardRestyler_Ran_v5", true);
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

            crownSprite = EnsureSprite($"{IconsDir}/Crown_Leaderboard.png");
            trophyGold = EnsureSprite($"{IconsDir}/Trophy_Gold.png");
            trophySilver = EnsureSprite($"{IconsDir}/Trophy_Silver.png");
            trophyBronze = EnsureSprite($"{IconsDir}/Trophy_Bronze.png");
            emptyStateSprite = EnsureSprite($"{IconsDir}/Leaderboard_EmptyState.png");
            glowGold = EnsureSprite($"{IconsDir}/Avatar_Glow_Gold.png");
            glowSilver = EnsureSprite($"{IconsDir}/Avatar_Glow_Silver.png");
            glowBronze = EnsureSprite($"{IconsDir}/Avatar_Glow_Bronze.png");
            defaultAvatarSprite = EnsureSprite(DefaultAvatarPath);

            panelBg = GetOrCreateScreenCardSprite();
            rowSprite = MakePanelSprite("Lb_RowBg", 32, 32, 6, Hex("#0B0E17"), Hex("#080B12"), Hex("#1A202C"), 1f);
            dropdownBg = MakePanelSprite("Lb_DropdownBg", 32, 32, 8, Hex("#0E121D"), Hex("#090D15"), Hex("#222838"), 1f);
            badgeBg = MakePanelSprite("Lb_BadgeBg", 32, 32, 8, Hex("#121622"), Hex("#0E111C"), Hex("#283044"), 1f);
            headerBg = MakePanelSprite("Lb_HeaderBg", 32, 32, 6, Hex("#0D111A"), Hex("#090D15"), Hex("#141924"), 1f);

            podiumBlockSprite = MakePedestalSprite("Lb_PedestalBg", 48, 64, 8);
            sunburstSprite = MakeSunburstSprite("Lb_SunburstRays", 1024, 512);
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

        private static Sprite MakePedestalSprite(string name, int w, int h, int radius)
        {
            return MakePanelSprite(name, w, h, radius, Hex("#182133"), Hex("#0A0E17"), Hex("#2A364E"), 1.2f, true);
        }

        private static Sprite MakeSunburstSprite(string name, int w, int h)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Vector2 origin = new Vector2(w * 0.5f, h * 0.85f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x, y);
                Vector2 dir = p - origin;
                float dist = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                float rayPattern = Mathf.Sin(angle * 9f * Mathf.Deg2Rad);
                float ray = Mathf.Clamp01(rayPattern * 0.5f + 0.5f);

                float falloff = Mathf.Clamp01(1f - (dist / (w * 0.65f)));
                falloff = falloff * falloff;

                Color rayColor = Hex("#1B2945");
                Color c = rayColor;
                c.a = ray * falloff * 0.42f;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite(path, tex);
        }

        // =========================================================================
        // BUILD CLEAN LEADERBOARD PREFAB
        // =========================================================================
        private static void BuildCleanLeaderboardPrefab()
        {
            var root = new GameObject("LeaderboardUI_NavPanel_New", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LeaderboardUI_New));
            try
            {
                var rootRt = root.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                root.SetActive(true);
                var cg = root.GetComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                var bg = root.GetComponent<Image>();
                bg.sprite = panelBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                var lbUi = root.GetComponent<LeaderboardUI_New>();
                var so = new SerializedObject(lbUi);

                var cgProp = so.FindProperty("<RootCanvasGroup>k__BackingField") ?? so.FindProperty("RootCanvasGroup") ?? so.FindProperty("_rootCanvasGroup");
                if (cgProp != null)
                    cgProp.objectReferenceValue = cg;

                // --- 1. HEADER AREA ---
                var headerGo = new GameObject("Header", typeof(RectTransform));
                headerGo.transform.SetParent(root.transform, false);
                var headerRt = (RectTransform)headerGo.transform;
                headerRt.anchorMin = new Vector2(0, 1);
                headerRt.anchorMax = new Vector2(1, 1);
                headerRt.pivot = new Vector2(0.5f, 1);
                headerRt.anchoredPosition = new Vector2(0, -14);
                headerRt.sizeDelta = new Vector2(-40, 52);

                // Title Container (Left)
                var titleGo = new GameObject("TitleContainer", typeof(RectTransform));
                titleGo.transform.SetParent(headerGo.transform, false);
                var titleRt = (RectTransform)titleGo.transform;
                titleRt.anchorMin = new Vector2(0, 0.5f);
                titleRt.anchorMax = new Vector2(0, 0.5f);
                titleRt.pivot = new Vector2(0, 0.5f);
                titleRt.anchoredPosition = new Vector2(0, 0);
                titleRt.sizeDelta = new Vector2(180, 40);

                var crownGo = new GameObject("CrownIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                crownGo.transform.SetParent(titleGo.transform, false);
                var crownImg = crownGo.GetComponent<Image>();
                crownImg.sprite = crownSprite;
                crownImg.color = Color.white;
                var crownRt = (RectTransform)crownGo.transform;
                crownRt.anchorMin = new Vector2(0, 0.5f);
                crownRt.anchorMax = new Vector2(0, 0.5f);
                crownRt.pivot = new Vector2(0, 0.5f);
                crownRt.anchoredPosition = new Vector2(0, 0);
                crownRt.sizeDelta = new Vector2(24, 24);

                var titleTextGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleTextGo.transform.SetParent(titleGo.transform, false);
                var titleTmp = titleTextGo.GetComponent<TextMeshProUGUI>();
                Text(titleTmp, "Leaderboard", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                var titleTextRt = (RectTransform)titleTextGo.transform;
                titleTextRt.anchorMin = new Vector2(0, 0.5f);
                titleTextRt.anchorMax = new Vector2(0, 0.5f);
                titleTextRt.pivot = new Vector2(0, 0.5f);
                titleTextRt.anchoredPosition = new Vector2(32, 0);
                titleTextRt.sizeDelta = new Vector2(150, 32);

                // Your Rank Badge (Under/Next to Title)
                var badgeGo = new GameObject("YourRankBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.transform.SetParent(headerGo.transform, false);
                var badgeImg = badgeGo.GetComponent<Image>();
                badgeImg.sprite = badgeBg;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = Color.white;
                var badgeRt = (RectTransform)badgeGo.transform;
                badgeRt.anchorMin = new Vector2(0, 0.5f);
                badgeRt.anchorMax = new Vector2(0, 0.5f);
                badgeRt.pivot = new Vector2(0, 0.5f);
                badgeRt.anchoredPosition = new Vector2(195, 0);
                badgeRt.sizeDelta = new Vector2(115, 36);

                var badgeIconGo = new GameObject("BadgeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeIconGo.transform.SetParent(badgeGo.transform, false);
                var bIconImg = badgeIconGo.GetComponent<Image>();
                bIconImg.sprite = trophyGold;
                bIconImg.color = Color.white;
                var bIconRt = (RectTransform)badgeIconGo.transform;
                bIconRt.anchorMin = new Vector2(0, 0.5f);
                bIconRt.anchorMax = new Vector2(0, 0.5f);
                bIconRt.pivot = new Vector2(0, 0.5f);
                bIconRt.anchoredPosition = new Vector2(8, 0);
                bIconRt.sizeDelta = new Vector2(18, 18);

                var badgeTextGo = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeTextGo.transform.SetParent(badgeGo.transform, false);
                var badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
                Text(badgeTmp, "<size=70%><color=#8E95A5>Your rank</color></size>\n<b><color=#F59E0B>13th</color></b>", fSemiBold, 11f, Color.white, TextAlignmentOptions.MidlineLeft);
                var bTextRt = (RectTransform)badgeTextGo.transform;
                bTextRt.anchorMin = new Vector2(0, 0.5f);
                bTextRt.anchorMax = new Vector2(1, 0.5f);
                bTextRt.pivot = new Vector2(0, 0.5f);
                bTextRt.anchoredPosition = new Vector2(32, 0);
                bTextRt.sizeDelta = new Vector2(-36, 32);

                // Filter Dropdowns Container (Top-Right)
                var filtersGo = new GameObject("Filters", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                filtersGo.transform.SetParent(headerGo.transform, false);
                var fRt = (RectTransform)filtersGo.transform;
                fRt.anchorMin = new Vector2(1, 0.5f);
                fRt.anchorMax = new Vector2(1, 0.5f);
                fRt.pivot = new Vector2(1, 0.5f);
                fRt.anchoredPosition = new Vector2(0, 0);
                fRt.sizeDelta = new Vector2(460, 36);

                var hlg = filtersGo.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 8f;
                hlg.childAlignment = TextAnchor.MiddleRight;

                // Instantiate 4 dropdowns using Dropdown_Choose_game_mode.prefab
                var ddPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DropdownPrefabPath);
                var ddGameType = CreateDropdownInstance(ddPrefab, filtersGo.transform, "Dropdown_GameType", "Game Type", 110f);
                var ddRank = CreateDropdownInstance(ddPrefab, filtersGo.transform, "Dropdown_Rank", "Rank", 100f);
                var ddPlayers = CreateDropdownInstance(ddPrefab, filtersGo.transform, "Dropdown_Players", "Players", 95f);
                var ddCountry = CreateDropdownInstance(ddPrefab, filtersGo.transform, "Dropdown_Country", "Country", 105f);

                // --- 2. TOP 3 PODIUM ---
                var podiumGo = new GameObject("PodiumContainer", typeof(RectTransform));
                podiumGo.transform.SetParent(root.transform, false);
                var podiumRt = (RectTransform)podiumGo.transform;
                podiumRt.anchorMin = new Vector2(0, 1);
                podiumRt.anchorMax = new Vector2(1, 1);
                podiumRt.pivot = new Vector2(0.5f, 1);
                podiumRt.anchoredPosition = new Vector2(0, -75);
                podiumRt.sizeDelta = new Vector2(-40, 255);

                // Sunburst background rays behind podium
                var sunburstGo = new GameObject("SunburstRays", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sunburstGo.transform.SetParent(podiumGo.transform, false);
                var sunburstImg = sunburstGo.GetComponent<Image>();
                sunburstImg.sprite = sunburstSprite;
                sunburstImg.color = Color.white;
                sunburstImg.raycastTarget = false;
                var sunburstRt = (RectTransform)sunburstGo.transform;
                sunburstRt.anchorMin = new Vector2(0.5f, 0.5f);
                sunburstRt.anchorMax = new Vector2(0.5f, 0.5f);
                sunburstRt.pivot = new Vector2(0.5f, 0.5f);
                sunburstRt.anchoredPosition = Vector2.zero;
                sunburstRt.sizeDelta = new Vector2(980, 265);

                // 2nd Place (Left)
                var p2 = BuildPodiumSlot(podiumGo.transform, "Podium_2nd", -205f, 115f, 160f, 2, "2nd", "David", 2200, trophySilver, glowSilver);
                // 1st Place (Center - Highest)
                var p1 = BuildPodiumSlot(podiumGo.transform, "Podium_1st", 0f, 150f, 180f, 1, "1st", "AlexStorm", 2240, trophyGold, glowGold);
                // 3rd Place (Right - Shortest)
                var p3 = BuildPodiumSlot(podiumGo.transform, "Podium_3rd", 205f, 90f, 160f, 3, "3rd", "Robert", 2140, trophyBronze, glowBronze);

                // --- 3. TABLE SECTION ---
                var tableGo = new GameObject("TableContainer", typeof(RectTransform));
                tableGo.transform.SetParent(root.transform, false);
                var tableRt = (RectTransform)tableGo.transform;
                tableRt.anchorMin = Vector2.zero;
                tableRt.anchorMax = new Vector2(1, 1);
                tableRt.pivot = new Vector2(0.5f, 0);
                tableRt.offsetMin = new Vector2(20, 16);
                tableRt.offsetMax = new Vector2(-20, -345);

                // Rankings_Header into TableContainer
                var headerRowGo = new GameObject("Rankings_Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                headerRowGo.transform.SetParent(tableGo.transform, false);
                var hrRt = (RectTransform)headerRowGo.transform;
                hrRt.anchorMin = new Vector2(0, 1);
                hrRt.anchorMax = new Vector2(1, 1);
                hrRt.pivot = new Vector2(0.5f, 1);
                hrRt.anchoredPosition = new Vector2(0, 0);
                hrRt.sizeDelta = new Vector2(0, 32);

                var hrImg = headerRowGo.GetComponent<Image>();
                hrImg.sprite = headerBg;
                hrImg.type = Image.Type.Sliced;
                hrImg.color = Color.white;

                RestyleHeaderColumns(headerRowGo.transform);

                // Scroll View
                var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
                scrollGo.transform.SetParent(tableGo.transform, false);
                var sRt = (RectTransform)scrollGo.transform;
                sRt.anchorMin = Vector2.zero;
                sRt.anchorMax = Vector2.one;
                sRt.offsetMin = Vector2.zero;
                sRt.offsetMax = new Vector2(0, -36);

                var scrollRect = scrollGo.GetComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.scrollSensitivity = 25f;

                // Viewport
                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                var vpRt = (RectTransform)viewportGo.transform;
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.offsetMin = Vector2.zero;
                vpRt.offsetMax = Vector2.zero;

                // Content
                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                var cRt = (RectTransform)contentGo.transform;
                cRt.anchorMin = new Vector2(0, 1);
                cRt.anchorMax = new Vector2(1, 1);
                cRt.pivot = new Vector2(0.5f, 1);
                cRt.anchoredPosition = Vector2.zero;
                cRt.sizeDelta = new Vector2(0, 0);

                var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var csf = contentGo.GetComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRect.content = cRt;
                scrollRect.viewport = vpRt;

                // Populate 20 instances of LeaderboardEntry_Prefab in Content
                var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
                if (entryPrefab != null)
                {
                    string[] sampleNames = { "AlexStorm", "David", "Robert", "Arlene", "Bessie", "Esther", "Floyd", "Devon", "Cody", "Kathryn", "Eleanor", "Jerome", "Savannah", "Courtney", "Wade", "Marvin", "Darlene", "Leslie", "Guy", "Kristin" };
                    int[] sampleElos = { 1845, 2012, 1788, 1420, 1380, 1340, 1290, 1250, 1210, 1180, 1150, 1120, 1090, 1060, 1030, 1000, 970, 940, 910, 880 };

                    for (int i = 0; i < 20; i++)
                    {
                        var entryGo = UnityEngine.Object.Instantiate(entryPrefab, contentGo.transform);
                        entryGo.name = $"LeaderboardEntry_{i}";
                        var entryRt = (RectTransform)entryGo.transform;
                        entryRt.sizeDelta = new Vector2(0, 46f);

                        // Configure initial preview data
                        var entryElement = entryGo.GetComponent<LeaderboardElement>();
                        var soEntry = new SerializedObject(entryGo.GetComponent<MonoBehaviour>());

                        var rankLbl = soEntry.FindProperty("rankingLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var trophyImg = soEntry.FindProperty("rankingTrophyImage")?.objectReferenceValue as Image;
                        var nameLbl = soEntry.FindProperty("playerNameLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var eloLbl = soEntry.FindProperty("eloLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var wLbl = soEntry.FindProperty("victoriesLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var lLbl = soEntry.FindProperty("losesLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var col2Lbl = soEntry.FindProperty("secondPlaceLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var col3Lbl = soEntry.FindProperty("thirdPlaceLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var wlRateLbl = soEntry.FindProperty("victoriesRateLabel")?.objectReferenceValue as TextMeshProUGUI;
                        var pImg = soEntry.FindProperty("playerImage")?.objectReferenceValue as Image;

                        int rankNum = i + 1;
                        if (rankNum <= 3 && trophyImg != null)
                        {
                            trophyImg.gameObject.SetActive(true);
                            trophyImg.sprite = rankNum switch { 1 => trophyGold, 2 => trophySilver, _ => trophyBronze };
                            if (rankLbl != null) rankLbl.gameObject.SetActive(false);
                        }
                        else
                        {
                            if (trophyImg != null) trophyImg.gameObject.SetActive(false);
                            if (rankLbl != null)
                            {
                                rankLbl.gameObject.SetActive(true);
                                rankLbl.text = rankNum.ToString();
                            }
                        }

                        if (nameLbl != null) nameLbl.text = sampleNames[i];
                        if (eloLbl != null) eloLbl.text = sampleElos[i].ToString();
                        if (wLbl != null) wLbl.text = (180 - i * 6).ToString();
                        if (lLbl != null) lLbl.text = (20 + i * 2).ToString();
                        if (col2Lbl != null) col2Lbl.text = (45 - i).ToString();
                        if (col3Lbl != null) col3Lbl.text = (30 - i).ToString();
                        if (wlRateLbl != null) wlRateLbl.text = (8.5f - i * 0.3f).ToString("0.00");
                        if (pImg != null && defaultAvatarSprite != null) pImg.sprite = defaultAvatarSprite;

                        // Keep rows active so initial display shows full list
                        entryGo.SetActive(true);
                    }
                }

                // --- 4. EMPTY STATE ---
                var emptyStateGo = new GameObject("Leaderboard_EmptyState", typeof(RectTransform));
                emptyStateGo.transform.SetParent(root.transform, false);
                var esRt = (RectTransform)emptyStateGo.transform;
                esRt.anchorMin = new Vector2(0.5f, 0.5f);
                esRt.anchorMax = new Vector2(0.5f, 0.5f);
                esRt.pivot = new Vector2(0.5f, 0.5f);
                esRt.anchoredPosition = new Vector2(0, -20);
                esRt.sizeDelta = new Vector2(480, 200);

                var esImgGo = new GameObject("EmptyStateIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                esImgGo.transform.SetParent(emptyStateGo.transform, false);
                var esImg = esImgGo.GetComponent<Image>();
                esImg.sprite = emptyStateSprite;
                esImg.color = Color.white;
                var esImgRt = (RectTransform)esImgGo.transform;
                esImgRt.anchorMin = new Vector2(0.5f, 1);
                esImgRt.anchorMax = new Vector2(0.5f, 1);
                esImgRt.pivot = new Vector2(0.5f, 1);
                esImgRt.anchoredPosition = new Vector2(0, 0);
                esImgRt.sizeDelta = new Vector2(72, 72);

                var esTitleGo = new GameObject("EmptyStateTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                esTitleGo.transform.SetParent(emptyStateGo.transform, false);
                var esTitleTmp = esTitleGo.GetComponent<TextMeshProUGUI>();
                Text(esTitleTmp, "Leaderboard is Empty", fBold, 20f, Color.white, TextAlignmentOptions.Center);
                var esTitleRt = (RectTransform)esTitleGo.transform;
                esTitleRt.anchorMin = new Vector2(0.5f, 1);
                esTitleRt.anchorMax = new Vector2(0.5f, 1);
                esTitleRt.pivot = new Vector2(0.5f, 1);
                esTitleRt.anchoredPosition = new Vector2(0, -85);
                esTitleRt.sizeDelta = new Vector2(400, 28);

                var esSubGo = new GameObject("EmptyStateSubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                esSubGo.transform.SetParent(emptyStateGo.transform, false);
                var esSubTmp = esSubGo.GetComponent<TextMeshProUGUI>();
                Text(esSubTmp, "No players have been ranked yet. Start playing to claim\nyour spot on the leaderboard.", fRegular, 13f, Hex("#8E95A5"), TextAlignmentOptions.Center);
                var esSubRt = (RectTransform)esSubGo.transform;
                esSubRt.anchorMin = new Vector2(0.5f, 1);
                esSubRt.anchorMax = new Vector2(0.5f, 1);
                esSubRt.pivot = new Vector2(0.5f, 1);
                esSubRt.anchoredPosition = new Vector2(0, -118);
                esSubRt.sizeDelta = new Vector2(440, 40);

                emptyStateGo.SetActive(false); // only visible when 0 entries

                // --- 5. WIRE SERIALIZED PROPERTIES ---
                SetField(so, "elementsParent", contentGo.transform);
                SetField(so, "gameModesFiltersDropdown", ddGameType);
                SetField(so, "tierFiltersDropdown", ddRank);
                SetField(so, "playerAmountFiltersDropdown", ddPlayers);
                SetField(so, "nationalityFiltersDropdown", ddCountry);

                SetField(so, "podiumContainer", podiumGo);
                SetField(so, "ownRankBadge", badgeGo);
                SetField(so, "ownRankBadgeText", badgeTmp);
                SetField(so, "tableContainer", tableGo);
                SetField(so, "ownRankLabel", null);
                SetField(so, "noLeaderboardEntriesLabel", emptyStateGo);

                WirePodiumSlot(so, "podium1st", p1);
                WirePodiumSlot(so, "podium2nd", p2);
                WirePodiumSlot(so, "podium3rd", p3);

                so.ApplyModifiedPropertiesWithoutUndo();

                // Save as clean standalone prefab asset
                PrefabUtility.SaveAsPrefabAsset(root, LeaderboardNewPath);
                Debug.Log("[LeaderboardRestyler] LeaderboardUI_NavPanel_New prefab built and saved cleanly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TMP_Dropdown CreateDropdownInstance(GameObject prefab, Transform parent, string name, string caption, float width)
        {
            var go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = name;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, 36f);

            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width - 10f;
            le.preferredHeight = 36f;
            le.minHeight = 36f;

            var dd = go.GetComponent<TMP_Dropdown>();
            if (dd != null)
            {
                if (dd.captionText is TextMeshProUGUI tmp)
                {
                    tmp.text = caption;
                    tmp.font = fSemiBold;
                    tmp.fontSize = 12f;
                    tmp.color = Hex("#C8D0DF");
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            var bg = go.transform.Find("Background")?.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = dropdownBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }

            var outline = go.transform.Find("Outline")?.GetComponent<Image>();
            if (outline != null)
            {
                outline.color = Hex("#222838");
            }

            var arrow = go.transform.Find("Arrow")?.GetComponent<Image>();
            if (arrow != null)
            {
                arrow.color = Hex("#8E95A5");
            }

            return dd;
        }

        private static GameObject BuildPodiumSlot(Transform parent, string name, float xOffset, float pedestalH, float slotWidth, int rank, string rankLabel, string defaultName, int defaultElo, Sprite trophy, Sprite glow)
        {
            var slotGo = new GameObject(name, typeof(RectTransform));
            slotGo.transform.SetParent(parent, false);
            var slotRt = (RectTransform)slotGo.transform;
            slotRt.anchorMin = new Vector2(0.5f, 0);
            slotRt.anchorMax = new Vector2(0.5f, 0);
            slotRt.pivot = new Vector2(0.5f, 0);
            slotRt.anchoredPosition = new Vector2(xOffset, 0);
            slotRt.sizeDelta = new Vector2(slotWidth, pedestalH + 130);

            // Pedestal Block
            var blockGo = new GameObject("PedestalBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blockGo.transform.SetParent(slotGo.transform, false);
            var blockImg = blockGo.GetComponent<Image>();
            blockImg.sprite = podiumBlockSprite;
            blockImg.type = Image.Type.Sliced;
            blockImg.color = Color.white;
            var blockRt = (RectTransform)blockGo.transform;
            blockRt.anchorMin = new Vector2(0, 0);
            blockRt.anchorMax = new Vector2(1, 0);
            blockRt.pivot = new Vector2(0.5f, 0);
            blockRt.anchoredPosition = Vector2.zero;
            blockRt.sizeDelta = new Vector2(0, pedestalH);

            // Large Rank Label on block (e.g. "1st")
            var rankNumGo = new GameObject("PedestalRankText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rankNumGo.transform.SetParent(blockGo.transform, false);
            var rankNumTmp = rankNumGo.GetComponent<TextMeshProUGUI>();
            float rankFontSize = rank == 1 ? 40f : 34f;
            Color rankColor = rank switch
            {
                1 => Hex("#8A7A4A"),
                2 => Hex("#6B7A94"),
                _ => Hex("#7D5A42")
            };
            rankColor.a = 0.65f;
            Text(rankNumTmp, rankLabel, fExtraBold, rankFontSize, rankColor, TextAlignmentOptions.Bottom);
            var rankNumRt = (RectTransform)rankNumGo.transform;
            rankNumRt.anchorMin = Vector2.zero;
            rankNumRt.anchorMax = Vector2.one;
            rankNumRt.offsetMin = new Vector2(0, 8);
            rankNumRt.offsetMax = new Vector2(0, -10);

            // Elo Score Text on block
            var eloGo = new GameObject("EloText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            eloGo.transform.SetParent(blockGo.transform, false);
            var eloTmp = eloGo.GetComponent<TextMeshProUGUI>();
            Text(eloTmp, $"<b>{defaultElo}</b>\n<size=65%><color=#7A8499>Elo Number</color></size>", fSemiBold, 12.5f, Color.white, TextAlignmentOptions.Top);
            var eloRt = (RectTransform)eloGo.transform;
            eloRt.anchorMin = new Vector2(0, 1);
            eloRt.anchorMax = new Vector2(1, 1);
            eloRt.pivot = new Vector2(0.5f, 1);
            eloRt.anchoredPosition = new Vector2(0, -6);
            eloRt.sizeDelta = new Vector2(0, 36);

            // Trophy Icon
            var trophyGo = new GameObject("TrophyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trophyGo.transform.SetParent(slotGo.transform, false);
            var trophyImg = trophyGo.GetComponent<Image>();
            trophyImg.sprite = trophy;
            trophyImg.color = Color.white;
            trophyImg.preserveAspect = true;
            var trophyRt = (RectTransform)trophyGo.transform;
            trophyRt.anchorMin = new Vector2(0.5f, 0);
            trophyRt.anchorMax = new Vector2(0.5f, 0);
            trophyRt.pivot = new Vector2(0.5f, 0);
            trophyRt.anchoredPosition = new Vector2(0, pedestalH + 3);
            trophyRt.sizeDelta = rank == 1 ? new Vector2(22, 22) : new Vector2(18, 18);

            // Player Name Text
            var nameGo = new GameObject("PlayerNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(slotGo.transform, false);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            Text(nameTmp, defaultName, fBold, rank == 1 ? 15f : 13.5f, Color.white, TextAlignmentOptions.Center);
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0);
            nameRt.anchoredPosition = new Vector2(0, pedestalH + 28);
            nameRt.sizeDelta = new Vector2(0, 20);

            // Avatar Glow Frame
            var glowGo = new GameObject("AvatarGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowGo.transform.SetParent(slotGo.transform, false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = glow;
            glowImg.color = Color.white;
            float glowSize = rank == 1 ? 84f : 74f;
            var glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = new Vector2(0.5f, 0);
            glowRt.anchorMax = new Vector2(0.5f, 0);
            glowRt.pivot = new Vector2(0.5f, 0);
            glowRt.anchoredPosition = new Vector2(0, pedestalH + 52);
            glowRt.sizeDelta = new Vector2(glowSize, glowSize);

            // Avatar Image (inside glow frame)
            var avGo = new GameObject("AvatarImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            avGo.transform.SetParent(glowGo.transform, false);
            var avImg = avGo.GetComponent<Image>();
            avImg.sprite = defaultAvatarSprite;
            avImg.color = Color.white;
            float avSize = rank == 1 ? 72f : 62f;
            var avRt = (RectTransform)avGo.transform;
            avRt.anchorMin = new Vector2(0.5f, 0.5f);
            avRt.anchorMax = new Vector2(0.5f, 0.5f);
            avRt.pivot = new Vector2(0.5f, 0.5f);
            avRt.anchoredPosition = Vector2.zero;
            avRt.sizeDelta = new Vector2(avSize, avSize);

            return slotGo;
        }

        private static void WirePodiumSlot(SerializedObject so, string propName, GameObject slotGo)
        {
            var p = so.FindProperty(propName);
            if (p == null) return;
            p.FindPropertyRelative("root").objectReferenceValue = slotGo;
            p.FindPropertyRelative("avatarImage").objectReferenceValue = slotGo.transform.Find("AvatarGlow/AvatarImage")?.GetComponent<Image>();
            p.FindPropertyRelative("avatarGlow").objectReferenceValue = slotGo.transform.Find("AvatarGlow")?.GetComponent<Image>();
            p.FindPropertyRelative("playerNameText").objectReferenceValue = slotGo.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
            p.FindPropertyRelative("trophyImage").objectReferenceValue = slotGo.transform.Find("TrophyIcon")?.GetComponent<Image>();
            p.FindPropertyRelative("eloText").objectReferenceValue = slotGo.transform.Find("PedestalBlock/EloText")?.GetComponent<TextMeshProUGUI>();
            p.FindPropertyRelative("pedestalRankText").objectReferenceValue = slotGo.transform.Find("PedestalBlock/PedestalRankText")?.GetComponent<TextMeshProUGUI>();
        }

        // =========================================================================
        // RESTYLE TABLE HEADER
        // =========================================================================
        private static void RestyleHeaderColumns(Transform headerTf)
        {
            (string name, string label, float minX, float maxX, TextAlignmentOptions align)[] cols =
            {
                ("RankCol", "#", 0.015f, 0.065f, TextAlignmentOptions.Center),
                ("UserCol", "Username", 0.075f, 0.36f, TextAlignmentOptions.MidlineLeft),
                ("EloCol", "Elo Rank", 0.37f, 0.48f, TextAlignmentOptions.Center),
                ("WCol", "W", 0.49f, 0.57f, TextAlignmentOptions.Center),
                ("LCol", "L", 0.58f, 0.66f, TextAlignmentOptions.Center),
                ("Col2nd", "2nd", 0.67f, 0.75f, TextAlignmentOptions.Center),
                ("Col3rd", "3rd", 0.76f, 0.84f, TextAlignmentOptions.Center),
                ("WLCol", "W/L", 0.85f, 0.985f, TextAlignmentOptions.Center)
            };

            foreach (var col in cols)
            {
                var colGo = new GameObject(col.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                colGo.transform.SetParent(headerTf, false);
                var tmp = colGo.GetComponent<TextMeshProUGUI>();
                Text(tmp, col.label, fSemiBold, 11.5f, Hex("#7A8499"), col.align);
                var rt = (RectTransform)colGo.transform;
                rt.anchorMin = new Vector2(col.minX, 0);
                rt.anchorMax = new Vector2(col.maxX, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        // =========================================================================
        // RESTYLE ENTRY PREFAB
        // =========================================================================
        private static void RestyleEntryPrefab()
        {
            var root = new GameObject("LeaderboardEntry_Prefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LeaderboardElement));
            try
            {
                var rt = root.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 46f);

                var bg = root.GetComponent<Image>();
                bg.sprite = rowSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;

                var element = root.GetComponent<LeaderboardElement>();
                var so = new SerializedObject(element);

                // 1. Ranking Trophy & Text (# column: 0.015 to 0.065)
                var trophyGo = new GameObject("Ranking_Trophy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                trophyGo.transform.SetParent(root.transform, false);
                var trophyImg = trophyGo.GetComponent<Image>();
                trophyImg.sprite = trophyGold;
                trophyImg.color = Color.white;
                trophyImg.preserveAspect = true;
                trophyImg.type = Image.Type.Simple;
                var trophyRt = (RectTransform)trophyGo.transform;
                trophyRt.anchorMin = new Vector2(0.015f, 0.5f);
                trophyRt.anchorMax = new Vector2(0.065f, 0.5f);
                trophyRt.pivot = new Vector2(0.5f, 0.5f);
                trophyRt.anchoredPosition = Vector2.zero;
                trophyRt.sizeDelta = new Vector2(20, 20);

                var rankGo = new GameObject("Ranking_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rankGo.transform.SetParent(root.transform, false);
                var rankTmp = rankGo.GetComponent<TextMeshProUGUI>();
                Text(rankTmp, "4", fSemiBold, 13f, Hex("#8E95A5"), TextAlignmentOptions.Center);
                var rankRt = (RectTransform)rankGo.transform;
                rankRt.anchorMin = new Vector2(0.015f, 0);
                rankRt.anchorMax = new Vector2(0.065f, 1);
                rankRt.pivot = new Vector2(0.5f, 0.5f);
                rankRt.offsetMin = Vector2.zero;
                rankRt.offsetMax = Vector2.zero;

                // 2. Avatar & Username (Username column: 0.075 to 0.36)
                var avatarGo = new GameObject("Player_Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                avatarGo.transform.SetParent(root.transform, false);
                var avImg = avatarGo.GetComponent<Image>();
                avImg.sprite = defaultAvatarSprite;
                avImg.color = Color.white;
                avImg.preserveAspect = true;
                var avRt = (RectTransform)avatarGo.transform;
                avRt.anchorMin = new Vector2(0.075f, 0.5f);
                avRt.anchorMax = new Vector2(0.075f, 0.5f);
                avRt.pivot = new Vector2(0, 0.5f);
                avRt.anchoredPosition = Vector2.zero;
                avRt.sizeDelta = new Vector2(28, 28);

                var nameGo = new GameObject("Player_Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                nameGo.transform.SetParent(root.transform, false);
                var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
                Text(nameTmp, "PlayerName", fSemiBold, 13.5f, Color.white, TextAlignmentOptions.MidlineLeft);
                var nameRt = (RectTransform)nameGo.transform;
                nameRt.anchorMin = new Vector2(0.075f, 0);
                nameRt.anchorMax = new Vector2(0.36f, 1);
                nameRt.pivot = new Vector2(0, 0.5f);
                nameRt.offsetMin = new Vector2(36, 0);
                nameRt.offsetMax = Vector2.zero;

                // 3. Stats Columns
                var eloTmp = CreateStatCol(root.transform, "Elo_Text", 0.37f, 0.48f, "1850");
                var wTmp = CreateStatCol(root.transform, "Wins_Text", 0.49f, 0.57f, "120");
                var lTmp = CreateStatCol(root.transform, "Loses_Text", 0.58f, 0.66f, "35");
                var c2Tmp = CreateStatCol(root.transform, "SecondPlace_Text", 0.67f, 0.75f, "24");
                var c3Tmp = CreateStatCol(root.transform, "ThirdPlace_Text", 0.76f, 0.84f, "18");
                var wlTmp = CreateStatCol(root.transform, "WinLossRatio_Text", 0.85f, 0.985f, "3.42");

                // Wire Serialized Properties on LeaderboardElement
                SetField(so, "rankingTrophyImage", trophyImg);
                SetField(so, "rankingLabel", rankTmp);
                SetField(so, "playerNameLabel", nameTmp);
                SetField(so, "playerImage", avImg);
                SetField(so, "defaultAvatarSprite", defaultAvatarSprite);
                SetField(so, "goldTrophySprite", trophyGold);
                SetField(so, "silverTrophySprite", trophySilver);
                SetField(so, "bronzeTrophySprite", trophyBronze);
                SetField(so, "eloLabel", eloTmp);
                SetField(so, "victoriesLabel", wTmp);
                SetField(so, "losesLabel", lTmp);
                SetField(so, "secondPlaceLabel", c2Tmp);
                SetField(so, "thirdPlaceLabel", c3Tmp);
                SetField(so, "victoriesRateLabel", wlTmp);

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
                Debug.Log("[LeaderboardRestyler] LeaderboardEntry_Prefab rebuilt cleanly from scratch!");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TextMeshProUGUI CreateStatCol(Transform parent, string name, float minX, float maxX, string defaultText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            Text(tmp, defaultText, fMedium, 13f, Hex("#D1D5DB"), TextAlignmentOptions.Center);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(minX, 0);
            rt.anchorMax = new Vector2(maxX, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return tmp;
        }

        // =========================================================================
        // DEACTIVATE OLD PANEL & UPDATE MIDDLE SCREEN
        // =========================================================================
        private static void DeactivateOldPanel()
        {
            // 1. In MiddleScreen_Scalable.prefab
            if (File.Exists(MiddleScreenPath))
            {
                var middleScreen = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
                try
                {
                    var oldP = middleScreen.GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_Old")
                        .ToArray();
                    foreach (var p in oldP)
                    {
                        p.enabled = false;
                        p.gameObject.SetActive(false);
                        Debug.Log("[LeaderboardRestyler] Deactivated " + p.gameObject.name + " in MiddleScreen_Scalable.");
                    }

                    var innerScreen = middleScreen.transform.Find("InnerScreen");
                    if (innerScreen != null)
                    {
                        var existingNew = innerScreen.GetComponentsInChildren<MonoBehaviour>(true)
                            .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_New")
                            .Select(mb => mb.gameObject)
                            .ToArray();

                        foreach (var go in existingNew)
                        {
                            UnityEngine.Object.DestroyImmediate(go);
                        }

                        var lbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LeaderboardNewPath);
                        if (lbAsset != null)
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(lbAsset, innerScreen);
                            instance.name = "LeaderboardUI_NavPanel_New";
                            instance.SetActive(true);

                            var iRt = (RectTransform)instance.transform;
                            iRt.anchorMin = Vector2.zero;
                            iRt.anchorMax = Vector2.one;
                            iRt.offsetMin = Vector2.zero;
                            iRt.offsetMax = Vector2.zero;

                            var cg = instance.GetComponent<CanvasGroup>() ?? instance.AddComponent<CanvasGroup>();
                            cg.alpha = 0f;
                            cg.interactable = false;
                            cg.blocksRaycasts = false;

                            Debug.Log("[LeaderboardRestyler] Instantiated clean LeaderboardUI_NavPanel_New in MiddleScreen_Scalable.");
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(middleScreen, MiddleScreenPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(middleScreen);
                }
            }

            // 2. In ProDomino_MainCanvas.prefab
            if (File.Exists(CanvasPath))
            {
                var canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
                try
                {
                    var oldP = canvas.GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_Old")
                        .ToArray();
                    foreach (var p in oldP)
                    {
                        p.enabled = false;
                        p.gameObject.SetActive(false);
                        Debug.Log("[LeaderboardRestyler] Deactivated " + p.gameObject.name + " in MainCanvas.");
                    }

                    PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(canvas);
                }
            }
        }

        // =========================================================================
        // REVERT STALE OVERRIDES IN SCENE
        // =========================================================================
        private static void RevertStaleOverrides()
        {
            if (!File.Exists(ScenePath)) return;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (var go in scene.GetRootGameObjects())
            {
                var oldPanels = go.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(mb => mb != null && (mb.GetType().Name == "LeaderboardUI_Old" || mb.name.Contains("LeaderboardUI_Old")))
                    .ToArray();
                foreach (var p in oldPanels)
                {
                    p.enabled = false;
                    p.gameObject.SetActive(false);
                    Debug.Log("[LeaderboardRestyler] Scene: Deactivated " + p.gameObject.name);
                }

                var newPanels = go.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_New")
                    .ToArray();
                foreach (var p in newPanels)
                {
                    p.enabled = true;
                    p.gameObject.SetActive(true);
                    var cg = p.GetComponent<CanvasGroup>() ?? p.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                    Debug.Log("[LeaderboardRestyler] Scene: Configured " + p.gameObject.name);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetField(SerializedObject so, string propName, UnityEngine.Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.objectReferenceValue = value;
        }
    }
}