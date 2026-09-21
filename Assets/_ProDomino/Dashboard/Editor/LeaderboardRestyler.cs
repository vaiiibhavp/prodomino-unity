using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Restyles LeaderboardUI_NavPanel_New to the Figma leaderboard design (node 113:3365):
    // Header (Crown + Title + "Your rank 13th" badge + 4 dropdown filters),
    // Top 3 Podium (1st, 2nd, 3rd pedestals with glowing avatar frames, trophies, names, Elo score),
    // Table section with headers (#, Username, Elo Rank, W, L, 2nd, 3rd, W/L) and restyled row cards,
    // Empty state (user avatar with red X + "Leaderboard is Empty"),
    // and completely deactivates the legacy LeaderboardUI_Old panel across prefabs and scenes.
    internal static class LeaderboardRestyler
    {
        private const string LeaderboardNewPath = "Assets/_ProDomino/LeaderboardSystem/Prefabs/LeaderboardUI_NavPanel_New.prefab";
        private const string EntryPrefabPath = "Assets/_ProDomino/LeaderboardSystem/Prefabs/LeaderboardEntry_Prefab.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
        private const string IconsDir = "Assets/_ProDomino/_UI/Icons/Icons_Leaderboard";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite crownSprite, trophyGold, trophySilver, trophyBronze, emptyStateSprite;
        private static Sprite glowGold, glowSilver, glowBronze, podiumBlockSprite;
        private static Sprite panelBg, rowSprite, dropdownBg, badgeBg, headerBg;

        [MenuItem("ProDomino/Dashboard/Restyle Leaderboard + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            RestyleEntryPrefab();
            RestyleLeaderboardPrefab();
            DeactivateOldPanel();
            RevertStaleOverrides();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LeaderboardRestyler] SUCCESS: Leaderboard completely restyled to Figma design!");
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
            podiumBlockSprite = EnsureSprite($"{IconsDir}/Podium_Block.png");

            panelBg = MakePanelSprite("Lb_PanelBg", 48, 96, 12, Hex("#070B14"), Hex("#020409"), Hex("#151D2A"), 1);
            rowSprite = MakePanelSprite("Lb_RowBg", 32, 32, 8, Hex("#0B0E17"), Hex("#080B12"), Hex("#1A202C"), 1);
            dropdownBg = MakePanelSprite("Lb_DropdownBg", 32, 32, 8, Hex("#0E121D"), Hex("#090D15"), Hex("#222838"), 1);
            badgeBg = MakePanelSprite("Lb_BadgeBg", 32, 32, 8, Hex("#121622"), Hex("#0E111C"), Hex("#283044"), 1);
            headerBg = MakePanelSprite("Lb_HeaderBg", 32, 32, 4, Hex("#07090F"), Hex("#07090F"), Hex("#141924"), 1);
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

        // =========================================================================
        // RESTYLE LEADERBOARD PREFAB
        // =========================================================================
        private static void RestyleLeaderboardPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(LeaderboardNewPath);
            try
            {
                var rootRt = root.GetComponent<RectTransform>();
                if (rootRt != null)
                {
                    rootRt.anchorMin = Vector2.zero;
                    rootRt.anchorMax = Vector2.one;
                    rootRt.offsetMin = Vector2.zero;
                    rootRt.offsetMax = Vector2.zero;
                }

                var bg = GetOrAdd<Image>(root.transform);
                bg.sprite = panelBg;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
                bg.raycastTarget = true;

                // Existing components to wire
                var lbUi = root.GetComponent<MonoBehaviour>();
                var so = new SerializedObject(lbUi);

                // --- 1. HEADER AREA ---
                var headerTf = root.transform.Find("Header");
                if (headerTf == null)
                {
                    var headerGo = new GameObject("Header", typeof(RectTransform));
                    headerGo.transform.SetParent(root.transform, false);
                    headerTf = headerGo.transform;
                }
                var headerRt = (RectTransform)headerTf;
                headerRt.anchorMin = new Vector2(0, 1);
                headerRt.anchorMax = new Vector2(1, 1);
                headerRt.pivot = new Vector2(0.5f, 1);
                headerRt.anchoredPosition = new Vector2(0, -16);
                headerRt.sizeDelta = new Vector2(-40, 48);

                // Title & Crown
                var titleGo = GetOrCreateChild(headerTf, "TitleContainer");
                var titleRt = (RectTransform)titleGo.transform;
                titleRt.anchorMin = new Vector2(0, 0.5f);
                titleRt.anchorMax = new Vector2(0, 0.5f);
                titleRt.pivot = new Vector2(0, 0.5f);
                titleRt.anchoredPosition = new Vector2(0, 0);
                titleRt.sizeDelta = new Vector2(180, 40);

                var crownGo = GetOrCreateChild(titleGo.transform, "CrownIcon");
                var crownImg = GetOrAdd<Image>(crownGo.transform);
                crownImg.sprite = crownSprite;
                crownImg.color = Color.white;
                var crownRt = (RectTransform)crownGo.transform;
                crownRt.anchorMin = new Vector2(0, 0.5f);
                crownRt.anchorMax = new Vector2(0, 0.5f);
                crownRt.pivot = new Vector2(0, 0.5f);
                crownRt.anchoredPosition = new Vector2(0, 0);
                crownRt.sizeDelta = new Vector2(24, 24);

                var titleTextGo = GetOrCreateChild(titleGo.transform, "TitleText");
                var titleTmp = GetOrAdd<TextMeshProUGUI>(titleTextGo.transform);
                Text(titleTmp, "Leaderboard", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
                var titleTextRt = (RectTransform)titleTextGo.transform;
                titleTextRt.anchorMin = new Vector2(0, 0.5f);
                titleTextRt.anchorMax = new Vector2(0, 0.5f);
                titleTextRt.pivot = new Vector2(0, 0.5f);
                titleTextRt.anchoredPosition = new Vector2(30, 0);
                titleTextRt.sizeDelta = new Vector2(150, 32);

                // Your Rank Badge
                var badgeGo = GetOrCreateChild(headerTf, "YourRankBadge");
                var badgeImg = GetOrAdd<Image>(badgeGo.transform);
                badgeImg.sprite = badgeBg;
                badgeImg.type = Image.Type.Sliced;
                badgeImg.color = Color.white;
                var badgeRt = (RectTransform)badgeGo.transform;
                badgeRt.anchorMin = new Vector2(0, 0.5f);
                badgeRt.anchorMax = new Vector2(0, 0.5f);
                badgeRt.pivot = new Vector2(0, 0.5f);
                badgeRt.anchoredPosition = new Vector2(190, 0);
                badgeRt.sizeDelta = new Vector2(110, 36);

                var badgeIconGo = GetOrCreateChild(badgeGo.transform, "BadgeIcon");
                var bIconImg = GetOrAdd<Image>(badgeIconGo.transform);
                bIconImg.sprite = trophyGold;
                bIconImg.color = Color.white;
                var bIconRt = (RectTransform)badgeIconGo.transform;
                bIconRt.anchorMin = new Vector2(0, 0.5f);
                bIconRt.anchorMax = new Vector2(0, 0.5f);
                bIconRt.pivot = new Vector2(0, 0.5f);
                bIconRt.anchoredPosition = new Vector2(8, 0);
                bIconRt.sizeDelta = new Vector2(18, 18);

                var badgeTextGo = GetOrCreateChild(badgeGo.transform, "BadgeText");
                var badgeTmp = GetOrAdd<TextMeshProUGUI>(badgeTextGo.transform);
                Text(badgeTmp, "<size=70%><color=#8E95A5>Your rank</color></size>\n<b><color=#F59E0B>13th</color></b>", fSemiBold, 12f, Color.white, TextAlignmentOptions.MidlineLeft);
                var bTextRt = (RectTransform)badgeTextGo.transform;
                bTextRt.anchorMin = new Vector2(0, 0.5f);
                bTextRt.anchorMax = new Vector2(1, 0.5f);
                bTextRt.pivot = new Vector2(0, 0.5f);
                bTextRt.anchoredPosition = new Vector2(32, 0);
                bTextRt.sizeDelta = new Vector2(-36, 32);

                // Filter Dropdowns
                var filtersTf = root.transform.Find("Filters");
                if (filtersTf != null)
                {
                    filtersTf.SetParent(headerTf, false);
                    var fRt = (RectTransform)filtersTf;
                    fRt.anchorMin = new Vector2(1, 0.5f);
                    fRt.anchorMax = new Vector2(1, 0.5f);
                    fRt.pivot = new Vector2(1, 0.5f);
                    fRt.anchoredPosition = new Vector2(0, 0);
                    fRt.sizeDelta = new Vector2(440, 34);

                    var hlg = GetOrAdd<HorizontalLayoutGroup>(filtersTf);
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = true;
                    hlg.childForceExpandHeight = true;
                    hlg.spacing = 8f;

                    var ddList = filtersTf.GetComponentsInChildren<TMP_Dropdown>(true);
                    foreach (var dd in ddList)
                    {
                        RestyleDropdown(dd);
                    }
                }

                // --- 2. TOP 3 PODIUM ---
                var podiumGo = GetOrCreateChild(root.transform, "PodiumContainer");
                var podiumRt = (RectTransform)podiumGo.transform;
                podiumRt.anchorMin = new Vector2(0, 1);
                podiumRt.anchorMax = new Vector2(1, 1);
                podiumRt.pivot = new Vector2(0.5f, 1);
                podiumRt.anchoredPosition = new Vector2(0, -68);
                podiumRt.sizeDelta = new Vector2(-40, 240);

                // 2nd Place (Left)
                var p2 = BuildPodiumSlot(podiumGo.transform, "Podium_2nd", -150f, 105f, 2, "2nd", "David", 2200, trophySilver, glowSilver);
                // 1st Place (Center)
                var p1 = BuildPodiumSlot(podiumGo.transform, "Podium_1st", 0f, 130f, 1, "1st", "AlexStorm", 2240, trophyGold, glowGold);
                // 3rd Place (Right)
                var p3 = BuildPodiumSlot(podiumGo.transform, "Podium_3rd", 150f, 85f, 3, "3rd", "Robert", 2140, trophyBronze, glowBronze);

                // --- 3. TABLE SECTION ---
                var tableGo = GetOrCreateChild(root.transform, "TableContainer");
                var tableRt = (RectTransform)tableGo.transform;
                tableRt.anchorMin = Vector2.zero;
                tableRt.anchorMax = new Vector2(1, 1);
                tableRt.pivot = new Vector2(0.5f, 0);
                tableRt.offsetMin = new Vector2(20, 16);
                tableRt.offsetMax = new Vector2(-20, -315);

                // Move Rankings_Header into TableContainer
                var headerRowTf = root.transform.Find("Rankings_Header");
                if (headerRowTf != null)
                {
                    headerRowTf.SetParent(tableGo.transform, false);
                    var hrRt = (RectTransform)headerRowTf;
                    hrRt.anchorMin = new Vector2(0, 1);
                    hrRt.anchorMax = new Vector2(1, 1);
                    hrRt.pivot = new Vector2(0.5f, 1);
                    hrRt.anchoredPosition = new Vector2(0, 0);
                    hrRt.sizeDelta = new Vector2(0, 32);

                    var hrImg = GetOrAdd<Image>(headerRowTf);
                    hrImg.sprite = headerBg;
                    hrImg.type = Image.Type.Sliced;
                    hrImg.color = Color.white;

                    RestyleHeaderColumns(headerRowTf);
                }

                // Scroll View / Elements Parent
                var scrollRect = root.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null)
                {
                    scrollRect.transform.SetParent(tableGo.transform, false);
                    var sRt = (RectTransform)scrollRect.transform;
                    sRt.anchorMin = Vector2.zero;
                    sRt.anchorMax = Vector2.one;
                    sRt.offsetMin = Vector2.zero;
                    sRt.offsetMax = new Vector2(0, -36);
                }

                // --- 4. EMPTY STATE ---
                var emptyStateTf = root.transform.Find("Leaderboard_EmptyState") ?? root.transform.Find("NoLeaderboardEntries_Label");
                if (emptyStateTf == null)
                {
                    var emptyGo = new GameObject("Leaderboard_EmptyState", typeof(RectTransform));
                    emptyGo.transform.SetParent(root.transform, false);
                    emptyStateTf = emptyGo.transform;
                }
                emptyStateTf.gameObject.name = "Leaderboard_EmptyState";
                var esRt = (RectTransform)emptyStateTf;
                esRt.anchorMin = new Vector2(0.5f, 0.5f);
                esRt.anchorMax = new Vector2(0.5f, 0.5f);
                esRt.pivot = new Vector2(0.5f, 0.5f);
                esRt.anchoredPosition = new Vector2(0, -20);
                esRt.sizeDelta = new Vector2(480, 200);

                var esImgGo = GetOrCreateChild(emptyStateTf, "EmptyStateIcon");
                var esImg = GetOrAdd<Image>(esImgGo.transform);
                esImg.sprite = emptyStateSprite;
                esImg.color = Color.white;
                var esImgRt = (RectTransform)esImgGo.transform;
                esImgRt.anchorMin = new Vector2(0.5f, 1);
                esImgRt.anchorMax = new Vector2(0.5f, 1);
                esImgRt.pivot = new Vector2(0.5f, 1);
                esImgRt.anchoredPosition = new Vector2(0, 0);
                esImgRt.sizeDelta = new Vector2(80, 80);

                var esTitleGo = GetOrCreateChild(emptyStateTf, "EmptyStateTitle");
                var esTitleTmp = GetOrAdd<TextMeshProUGUI>(esTitleGo.transform);
                Text(esTitleTmp, "Leaderboard is Empty", fBold, 22f, Color.white, TextAlignmentOptions.Center);
                var esTitleRt = (RectTransform)esTitleGo.transform;
                esTitleRt.anchorMin = new Vector2(0.5f, 1);
                esTitleRt.anchorMax = new Vector2(0.5f, 1);
                esTitleRt.pivot = new Vector2(0.5f, 1);
                esTitleRt.anchoredPosition = new Vector2(0, -92);
                esTitleRt.sizeDelta = new Vector2(400, 30);

                var esSubGo = GetOrCreateChild(emptyStateTf, "EmptyStateSubtitle");
                var esSubTmp = GetOrAdd<TextMeshProUGUI>(esSubGo.transform);
                Text(esSubTmp, "No players have been ranked yet. Start playing to claim your spot on the leaderboard.", fRegular, 13f, Hex("#8E95A5"), TextAlignmentOptions.Center);
                var esSubRt = (RectTransform)esSubGo.transform;
                esSubRt.anchorMin = new Vector2(0.5f, 1);
                esSubRt.anchorMax = new Vector2(0.5f, 1);
                esSubRt.pivot = new Vector2(0.5f, 1);
                esSubRt.anchoredPosition = new Vector2(0, -125);
                esSubRt.sizeDelta = new Vector2(440, 40);

                emptyStateTf.gameObject.SetActive(false); // only visible when 0 entries

                // --- 5. WIRE SERIALIZED PROPERTIES ---
                SetField(so, "podiumContainer", podiumGo);
                SetField(so, "ownRankBadge", badgeGo);
                SetField(so, "ownRankBadgeText", badgeTmp);
                SetField(so, "tableContainer", tableGo);
                SetField(so, "noLeaderboardEntriesLabel", emptyStateTf.gameObject);

                WirePodiumSlot(so, "podium1st", p1);
                WirePodiumSlot(so, "podium2nd", p2);
                WirePodiumSlot(so, "podium3rd", p3);

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LeaderboardNewPath);
                Debug.Log("[LeaderboardRestyler] LeaderboardUI_NavPanel_New prefab saved successfully.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildPodiumSlot(Transform parent, string name, float xOffset, float pedestalH, int rank, string rankLabel, string defaultName, int defaultElo, Sprite trophy, Sprite glow)
        {
            var slotGo = GetOrCreateChild(parent, name);
            var slotRt = (RectTransform)slotGo.transform;
            slotRt.anchorMin = new Vector2(0.5f, 0);
            slotRt.anchorMax = new Vector2(0.5f, 0);
            slotRt.pivot = new Vector2(0.5f, 0);
            slotRt.anchoredPosition = new Vector2(xOffset, 0);
            slotRt.sizeDelta = new Vector2(130, pedestalH + 110);

            // Pedestal Block
            var blockGo = GetOrCreateChild(slotGo.transform, "PedestalBlock");
            var blockImg = GetOrAdd<Image>(blockGo.transform);
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
            var rankNumGo = GetOrCreateChild(blockGo.transform, "PedestalRankText");
            var rankNumTmp = GetOrAdd<TextMeshProUGUI>(rankNumGo.transform);
            float rankFontSize = rank == 1 ? 36f : 30f;
            Color rankColor = new Color(0.24f, 0.28f, 0.38f, 0.45f);
            Text(rankNumTmp, rankLabel, fExtraBold, rankFontSize, rankColor, TextAlignmentOptions.Bottom);
            var rankNumRt = (RectTransform)rankNumGo.transform;
            rankNumRt.anchorMin = Vector2.zero;
            rankNumRt.anchorMax = Vector2.one;
            rankNumRt.offsetMin = new Vector2(0, 10);
            rankNumRt.offsetMax = new Vector2(0, -10);

            // Elo Score Text on block
            var eloGo = GetOrCreateChild(blockGo.transform, "EloText");
            var eloTmp = GetOrAdd<TextMeshProUGUI>(eloGo.transform);
            Text(eloTmp, $"<b>{defaultElo}</b>\n<size=65%><color=#7A8499>Elo Number</color></size>", fSemiBold, 12f, Color.white, TextAlignmentOptions.Top);
            var eloRt = (RectTransform)eloGo.transform;
            eloRt.anchorMin = new Vector2(0, 1);
            eloRt.anchorMax = new Vector2(1, 1);
            eloRt.pivot = new Vector2(0.5f, 1);
            eloRt.anchoredPosition = new Vector2(0, -6);
            eloRt.sizeDelta = new Vector2(0, 36);

            // Trophy Icon
            var trophyGo = GetOrCreateChild(slotGo.transform, "TrophyIcon");
            var trophyImg = GetOrAdd<Image>(trophyGo.transform);
            trophyImg.sprite = trophy;
            trophyImg.color = Color.white;
            var trophyRt = (RectTransform)trophyGo.transform;
            trophyRt.anchorMin = new Vector2(0.5f, 0);
            trophyRt.anchorMax = new Vector2(0.5f, 0);
            trophyRt.pivot = new Vector2(0.5f, 0);
            trophyRt.anchoredPosition = new Vector2(0, pedestalH + 2);
            trophyRt.sizeDelta = new Vector2(20, 20);

            // Player Name Text
            var nameGo = GetOrCreateChild(slotGo.transform, "PlayerNameText");
            var nameTmp = GetOrAdd<TextMeshProUGUI>(nameGo.transform);
            Text(nameTmp, defaultName, fBold, 13f, Color.white, TextAlignmentOptions.Center);
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0);
            nameRt.pivot = new Vector2(0.5f, 0);
            nameRt.anchoredPosition = new Vector2(0, pedestalH + 24);
            nameRt.sizeDelta = new Vector2(0, 20);

            // Avatar Glow Frame
            var glowGo = GetOrCreateChild(slotGo.transform, "AvatarGlow");
            var glowImg = GetOrAdd<Image>(glowGo.transform);
            glowImg.sprite = glow;
            glowImg.color = Color.white;
            float glowSize = rank == 1 ? 74f : 66f;
            var glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = new Vector2(0.5f, 0);
            glowRt.anchorMax = new Vector2(0.5f, 0);
            glowRt.pivot = new Vector2(0.5f, 0);
            glowRt.anchoredPosition = new Vector2(0, pedestalH + 46);
            glowRt.sizeDelta = new Vector2(glowSize, glowSize);

            // Avatar Image (inside glow frame)
            var avGo = GetOrCreateChild(glowGo.transform, "AvatarImage");
            var avImg = GetOrAdd<Image>(avGo.transform);
            avImg.color = Color.white;
            float avSize = glowSize - 12f;
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
            (string name, string label, float x, float w, TextAlignmentOptions align)[] cols =
            {
                ("RankCol", "#", 12f, 40f, TextAlignmentOptions.Center),
                ("UserCol", "Username", 65f, 220f, TextAlignmentOptions.Left),
                ("EloCol", "Elo Rank", 300f, 100f, TextAlignmentOptions.Center),
                ("WCol", "W", 420f, 60f, TextAlignmentOptions.Center),
                ("LCol", "L", 490f, 60f, TextAlignmentOptions.Center),
                ("Col2nd", "2nd", 560f, 60f, TextAlignmentOptions.Center),
                ("Col3rd", "3rd", 630f, 60f, TextAlignmentOptions.Center),
                ("WLCol", "W/L", 700f, 70f, TextAlignmentOptions.Center)
            };

            foreach (var col in cols)
            {
                var colGo = GetOrCreateChild(headerTf, col.name);
                var tmp = GetOrAdd<TextMeshProUGUI>(colGo.transform);
                Text(tmp, col.label, fSemiBold, 11f, Hex("#7A8499"), col.align);
                var rt = (RectTransform)colGo.transform;
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(col.x, 0);
                rt.sizeDelta = new Vector2(col.w, 24);
            }
        }

        // =========================================================================
        // RESTYLE ENTRY PREFAB
        // =========================================================================
        private static void RestyleEntryPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(EntryPrefabPath);
            try
            {
                var rt = root.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 48f);
                }

                var bg = GetOrAdd<Image>(root.transform);
                bg.sprite = rowSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;

                var element = root.GetComponent<MonoBehaviour>();
                var so = new SerializedObject(element);

                // Ensure ranking trophy image exists
                var trophyGo = GetOrCreateChild(root.transform, "Ranking_Trophy");
                var trophyImg = GetOrAdd<Image>(trophyGo.transform);
                trophyImg.color = Color.white;
                var trophyRt = (RectTransform)trophyGo.transform;
                trophyRt.anchorMin = new Vector2(0, 0.5f);
                trophyRt.anchorMax = new Vector2(0, 0.5f);
                trophyRt.pivot = new Vector2(0.5f, 0.5f);
                trophyRt.anchoredPosition = new Vector2(32, 0);
                trophyRt.sizeDelta = new Vector2(22, 22);

                // Set trophy sprites and image on serialized object
                SetField(so, "rankingTrophyImage", trophyImg);
                SetField(so, "goldTrophySprite", trophyGold);
                SetField(so, "silverTrophySprite", trophySilver);
                SetField(so, "bronzeTrophySprite", trophyBronze);

                // Restyle fonts on all TMP components in the row
                var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    t.font = fMedium;
                    t.fontSize = 12.5f;
                    t.color = Color.white;
                }

                var userTmp = root.transform.Find("UserData/Username_Text")?.GetComponent<TextMeshProUGUI>()
                    ?? root.transform.Find("Username_Text")?.GetComponent<TextMeshProUGUI>();
                if (userTmp != null)
                {
                    userTmp.font = fBold;
                    userTmp.fontSize = 13.5f;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
                Debug.Log("[LeaderboardRestyler] LeaderboardEntry_Prefab saved successfully.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =========================================================================
        // DEACTIVATE OLD PANEL
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
                        p.gameObject.SetActive(false);
                        Debug.Log("[LeaderboardRestyler] Deactivated " + p.gameObject.name + " in MiddleScreen_Scalable.");
                    }

                    var newP = middleScreen.GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_New")
                        .ToArray();
                    foreach (var p in newP)
                    {
                        p.gameObject.SetActive(true);
                        Debug.Log("[LeaderboardRestyler] Activated " + p.gameObject.name + " in MiddleScreen_Scalable.");
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
                    .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_Old")
                    .ToArray();
                foreach (var p in oldPanels)
                {
                    p.gameObject.SetActive(false);
                    Debug.Log("[LeaderboardRestyler] Scene: Deactivated " + p.gameObject.name);
                }

                var newPanels = go.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(mb => mb != null && mb.GetType().Name == "LeaderboardUI_New")
                    .ToArray();
                foreach (var p in newPanels)
                {
                    p.gameObject.SetActive(true);
                    Debug.Log("[LeaderboardRestyler] Scene: Activated " + p.gameObject.name);
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

        private static void RestyleDropdown(TMP_Dropdown dd)
        {
            var bg = GetOrAdd<Image>(dd.transform);
            bg.sprite = dropdownBg;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var label = dd.captionText as TextMeshProUGUI;
            if (label != null)
            {
                label.font = fSemiBold;
                label.fontSize = 12f;
                label.color = Hex("#C8D0DF");
            }
        }

        private static void SetField(SerializedObject so, string propName, UnityEngine.Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.objectReferenceValue = value;
        }
    }
}