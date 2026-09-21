using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProDomino.QuickMatchSystem;
using ProDomino.Shared;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Restyles QuickMatchUI_NavPanel to match the Figma Dashboard design:
    // 1. Monthly Challenge Hero Banner with background, gradient overlay, and progress bar
    // 2. Real-time Online Players Pill ("2.5K Online")
    // 3. Quick Match Section ("Start a game right away!") with AI, Random Players, and Competitive cards
    // 4. Play Games Section ("Play Games") with Block and Concentrate game cards
    // Integrates smoothly into MiddleScreen_Scalable and wires all QuickMatchController properties.
    internal static class PlayDashboardRestyler
    {
        private const string ArtDir = "Assets/_ProDomino/_Art/Dashboard";
        private const string IconDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";
        private const string GeneratedDir = "Assets/_ProDomino/Dashboard/Generated";
        private const string QuickMatchPrefabPath = "Assets/_ProDomino/QuickMatchSystem/Prefabs/QuickMatchUI_NavPanel.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite bannerBg, bannerOverlay;
        private static Sprite aiBg, aiBadge, randBg, randIllust, compBg, compTrophy;
        private static Sprite blockIllust, concIllust;
        private static Sprite avatar1, avatar2, avatar3;
        private static Sprite pillBg, cardRoundedBg, lockIcon;

        [MenuItem("ProDomino/Dashboard/Restyle Play Dashboard + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            BuildCleanQuickMatchPrefab();
            UpdateMiddleScreenInstance();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayDashboardRestyler] SUCCESS: Play / Quick Match Dashboard restyled cleanly to Figma design!");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            bannerBg = EnsureSprite($"{ArtDir}/ChallengeBanner_Background.png");
            aiBg = EnsureSprite($"{ArtDir}/QuickMatch_AI_Background.png");
            aiBadge = EnsureSprite($"{ArtDir}/QuickMatch_AI_Badge.png");
            randBg = EnsureSprite($"{ArtDir}/QuickMatch_RandomPlayers_Background.png");
            randIllust = EnsureSprite($"{ArtDir}/QuickMatch_RandomPlayers_Illustration.png");
            compBg = EnsureSprite($"{ArtDir}/QuickMatch_Competitive_Background.png");
            compTrophy = EnsureSprite($"{ArtDir}/QuickMatch_Competitive_Trophy.png");
            blockIllust = EnsureSprite($"{ArtDir}/PlayGames_Block_Illustration.png");
            concIllust = EnsureSprite($"{ArtDir}/PlayGames_Concentrate_Illustration.png");

            avatar1 = EnsureSprite($"{ArtDir}/StatPill_Avatar1.png");
            avatar2 = EnsureSprite($"{ArtDir}/StatPill_Avatar2.png");
            avatar3 = EnsureSprite($"{ArtDir}/StatPill_Avatar3.png");

            bannerOverlay = GenerateGradient("Grad_BannerOverlay", Hex("#000051", 0.85f), Hex("#000000", 0.95f), false, 16, 128);
            pillBg = MakePanelSprite("Play_PillBg", 32, 32, 16, Hex("#121724"), Hex("#0B0F19"), Hex("#232D42"), 1f);
            cardRoundedBg = MakePanelSprite("Play_CardRoundedBg", 32, 32, 14, Hex("#121724"), Hex("#0B0F19"), Hex("#1E2738"), 1f);
            lockIcon = EnsureSprite("Assets/_ProDomino/_UI/Icons/Icons_Leaderboard/Crown_Leaderboard.png");
        }

        private static Sprite EnsureSprite(string path)
        {
            if (!File.Exists(path)) return null;
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
        // BUILD CLEAN QUICK MATCH PREFAB
        // =========================================================================
        private static void BuildCleanQuickMatchPrefab()
        {
            var root = new GameObject("QuickMatchUI_NavPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(QuickMatchController));
            try
            {
                var rootRt = root.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;

                var cg = root.GetComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                var qmc = root.GetComponent<QuickMatchController>();
                var so = new SerializedObject(qmc);
                var cgProp = so.FindProperty("<RootCanvasGroup>k__BackingField");
                if (cgProp != null) cgProp.objectReferenceValue = cg;

                // --- 1. SCROLL VIEW ---
                var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
                scrollGo.transform.SetParent(root.transform, false);
                var scrollRt = (RectTransform)scrollGo.transform;
                scrollRt.anchorMin = Vector2.zero;
                scrollRt.anchorMax = Vector2.one;
                scrollRt.offsetMin = Vector2.zero;
                scrollRt.offsetMax = Vector2.zero;

                var scrollRect = scrollGo.GetComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.scrollSensitivity = 30f;

                var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                var vpRt = (RectTransform)viewportGo.transform;
                vpRt.anchorMin = Vector2.zero;
                vpRt.anchorMax = Vector2.one;
                vpRt.offsetMin = Vector2.zero;
                vpRt.offsetMax = Vector2.zero;

                var contentGo = new GameObject("Content", typeof(RectTransform));
                contentGo.transform.SetParent(viewportGo.transform, false);
                var contentRt = (RectTransform)contentGo.transform;
                contentRt.anchorMin = new Vector2(0.5f, 1);
                contentRt.anchorMax = new Vector2(0.5f, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(1150, 1340);

                scrollRect.viewport = vpRt;
                scrollRect.content = contentRt;

                // --- 2. MONTHLY CHALLENGE BANNER ---
                BuildChallengeBanner(contentGo.transform, 0f);

                // --- 3. ONLINE STATUS PILL ---
                BuildOnlineStatusPill(contentGo.transform, -375f);

                // --- 4. QUICK MATCH SECTION ---
                var (aiBtn, casualBtn, compBtn, compLock) = BuildQuickMatchSection(contentGo.transform, -435f);

                // --- 5. PLAY GAMES SECTION ---
                BuildPlayGamesSection(contentGo.transform, -925f);

                // Wire Serialized Properties
                SetField(so, "aiButton", aiBtn);
                SetField(so, "casualPlayerButton", casualBtn);
                SetField(so, "competitivePlayerButton", compBtn);
                SetField(so, "competitiveQuickMatchObject", compLock);

                // Party Object (optional indicator)
                var partyGo = new GameObject("PartyIndicator", typeof(RectTransform));
                partyGo.transform.SetParent(contentGo.transform, false);
                var partyRt = (RectTransform)partyGo.transform;
                partyRt.anchoredPosition = new Vector2(0, -415f);
                partyRt.sizeDelta = new Vector2(300, 30);
                partyGo.SetActive(false);
                SetField(so, "partyObject", partyGo);

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, QuickMatchPrefabPath);
                Debug.Log("[PlayDashboardRestyler] QuickMatchUI_NavPanel prefab built and saved cleanly.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =========================================================================
        // SECTION 1: CHALLENGE BANNER
        // =========================================================================
        private static void BuildChallengeBanner(Transform parent, float topY)
        {
            var banner = new GameObject("ChallengeBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            banner.transform.SetParent(parent, false);
            var bRt = (RectTransform)banner.transform;
            bRt.anchorMin = new Vector2(0.5f, 1);
            bRt.anchorMax = new Vector2(0.5f, 1);
            bRt.pivot = new Vector2(0.5f, 1);
            bRt.anchoredPosition = new Vector2(0, topY);
            bRt.sizeDelta = new Vector2(1150, 355);

            var bImg = banner.GetComponent<Image>();
            bImg.sprite = cardRoundedBg;
            bImg.type = Image.Type.Sliced;
            bImg.color = Hex("#0A0E18");

            // Art Background
            if (bannerBg != null)
            {
                var bgGo = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgGo.transform.SetParent(banner.transform, false);
                var bgImg = bgGo.GetComponent<Image>();
                bgImg.sprite = bannerBg;
                bgImg.preserveAspect = false;
                StretchFull(bgGo);
            }

            // Overlay Gradient
            if (bannerOverlay != null)
            {
                var ovGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ovGo.transform.SetParent(banner.transform, false);
                var ovImg = ovGo.GetComponent<Image>();
                ovImg.sprite = bannerOverlay;
                StretchFull(ovGo);
            }

            // Text content container
            var textGo = new GameObject("ContentColumn", typeof(RectTransform));
            textGo.transform.SetParent(banner.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0, 1);
            textRt.anchorMax = new Vector2(0, 1);
            textRt.pivot = new Vector2(0, 1);
            textRt.anchoredPosition = new Vector2(48, -38);
            textRt.sizeDelta = new Vector2(640, 280);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(textGo.transform, false);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            Text(titleTmp, "Complete Your Monthly Challenge!!", fBold, 38f, Color.white, TextAlignmentOptions.TopLeft);
            var tRt = (RectTransform)titleGo.transform;
            tRt.anchorMin = new Vector2(0, 1);
            tRt.anchorMax = new Vector2(1, 1);
            tRt.pivot = new Vector2(0, 1);
            tRt.anchoredPosition = Vector2.zero;
            tRt.sizeDelta = new Vector2(0, 95);

            // Subtitle
            var subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(textGo.transform, false);
            var subTmp = subGo.GetComponent<TextMeshProUGUI>();
            Text(subTmp, "Play 50 block games and earn big rewards!", fRegular, 20f, Hex("#E6E6E7"), TextAlignmentOptions.TopLeft);
            var sRt = (RectTransform)subGo.transform;
            sRt.anchorMin = new Vector2(0, 1);
            sRt.anchorMax = new Vector2(1, 1);
            sRt.pivot = new Vector2(0, 1);
            sRt.anchoredPosition = new Vector2(0, -105);
            sRt.sizeDelta = new Vector2(0, 32);

            // Progress Row
            var progRowGo = new GameObject("ProgressRow", typeof(RectTransform));
            progRowGo.transform.SetParent(textGo.transform, false);
            var prRt = (RectTransform)progRowGo.transform;
            prRt.anchorMin = new Vector2(0, 1);
            prRt.anchorMax = new Vector2(1, 1);
            prRt.pivot = new Vector2(0, 1);
            prRt.anchoredPosition = new Vector2(0, -155);
            prRt.sizeDelta = new Vector2(0, 44);

            var progLabelGo = new GameObject("ProgressLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            progLabelGo.transform.SetParent(progRowGo.transform, false);
            var plTmp = progLabelGo.GetComponent<TextMeshProUGUI>();
            Text(plTmp, "<b>23/50</b>", fBold, 18f, Color.white, TextAlignmentOptions.MidlineLeft);
            var plRt = (RectTransform)progLabelGo.transform;
            plRt.anchorMin = new Vector2(0, 0.5f);
            plRt.anchorMax = new Vector2(0, 0.5f);
            plRt.pivot = new Vector2(0, 0.5f);
            plRt.anchoredPosition = new Vector2(0, 0);
            plRt.sizeDelta = new Vector2(70, 30);

            // Progress Bar Track
            var trackGo = new GameObject("ProgressBarTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackGo.transform.SetParent(progRowGo.transform, false);
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.sprite = pillBg;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = Hex("#1B2335");
            var trRt = (RectTransform)trackGo.transform;
            trRt.anchorMin = new Vector2(0, 0.5f);
            trRt.anchorMax = new Vector2(0, 0.5f);
            trRt.pivot = new Vector2(0, 0.5f);
            trRt.anchoredPosition = new Vector2(80, 0);
            trRt.sizeDelta = new Vector2(420, 14);

            // Progress Bar Fill (23/50 = 46%)
            var fillGo = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = pillBg;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Hex("#F59E0B");
            var fRt = (RectTransform)fillGo.transform;
            fRt.anchorMin = new Vector2(0, 0);
            fRt.anchorMax = new Vector2(0.46f, 1);
            fRt.pivot = new Vector2(0, 0.5f);
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;

            // Claim Reward Button
            var claimBtnGo = new GameObject("ClaimButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            claimBtnGo.transform.SetParent(textGo.transform, false);
            var cBtnImg = claimBtnGo.GetComponent<Image>();
            cBtnImg.sprite = pillBg;
            cBtnImg.type = Image.Type.Sliced;
            cBtnImg.color = Hex("#2563EB");
            var cbRt = (RectTransform)claimBtnGo.transform;
            cbRt.anchorMin = new Vector2(0, 1);
            cbRt.anchorMax = new Vector2(0, 1);
            cbRt.pivot = new Vector2(0, 1);
            cbRt.anchoredPosition = new Vector2(0, -215);
            cbRt.sizeDelta = new Vector2(160, 42);

            var cbTextGo = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            cbTextGo.transform.SetParent(claimBtnGo.transform, false);
            var cbTmp = cbTextGo.GetComponent<TextMeshProUGUI>();
            Text(cbTmp, "View Details", fBold, 14f, Color.white, TextAlignmentOptions.Center);
            StretchFull(cbTextGo);
        }

        // =========================================================================
        // SECTION 2: ONLINE STATUS PILL
        // =========================================================================
        private static void BuildOnlineStatusPill(Transform parent, float topY)
        {
            var pillGo = new GameObject("OnlineStatusPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pillGo.transform.SetParent(parent, false);
            var pRt = (RectTransform)pillGo.transform;
            pRt.anchorMin = new Vector2(0, 1);
            pRt.anchorMax = new Vector2(0, 1);
            pRt.pivot = new Vector2(0, 1);
            pRt.anchoredPosition = new Vector2(0, topY);
            pRt.sizeDelta = new Vector2(210, 44);

            var pImg = pillGo.GetComponent<Image>();
            pImg.sprite = pillBg;
            pImg.type = Image.Type.Sliced;
            pImg.color = Hex("#0E1320");

            // Overlapping 3 Avatars
            AddPillAvatar(pillGo.transform, "Av1", avatar1, 10f);
            AddPillAvatar(pillGo.transform, "Av2", avatar2, 28f);
            AddPillAvatar(pillGo.transform, "Av3", avatar3, 46f);

            // Pulsing Green Online Dot
            var dotGo = new GameObject("GreenDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dotGo.transform.SetParent(pillGo.transform, false);
            var dImg = dotGo.GetComponent<Image>();
            dImg.color = Hex("#22C55E");
            var dRt = (RectTransform)dotGo.transform;
            dRt.anchorMin = new Vector2(0, 0.5f);
            dRt.anchorMax = new Vector2(0, 0.5f);
            dRt.pivot = new Vector2(0.5f, 0.5f);
            dRt.anchoredPosition = new Vector2(88, 0);
            dRt.sizeDelta = new Vector2(8, 8);

            // Online Text
            var textGo = new GameObject("OnlineText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(pillGo.transform, false);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            Text(tmp, "<b><color=#22C55E>2.5K</color></b> <color=#94A3B8>Online</color>", fSemiBold, 13f, Color.white, TextAlignmentOptions.MidlineLeft);
            var tRt = (RectTransform)textGo.transform;
            tRt.anchorMin = new Vector2(0, 0.5f);
            tRt.anchorMax = new Vector2(1, 0.5f);
            tRt.pivot = new Vector2(0, 0.5f);
            tRt.anchoredPosition = new Vector2(104, 0);
            tRt.sizeDelta = new Vector2(-108, 30);
        }

        private static void AddPillAvatar(Transform parent, string name, Sprite sprite, float xPos)
        {
            var avGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            avGo.transform.SetParent(parent, false);
            var img = avGo.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var rt = (RectTransform)avGo.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0);
            rt.sizeDelta = new Vector2(28, 28);
        }

        // =========================================================================
        // SECTION 3: QUICK MATCH SECTION
        // =========================================================================
        private static (CustomButtonUI ai, CustomButtonUI casual, CustomButtonUI comp, GameObject lockObj) BuildQuickMatchSection(Transform parent, float topY)
        {
            var section = new GameObject("QuickMatchSection", typeof(RectTransform));
            section.transform.SetParent(parent, false);
            var sRt = (RectTransform)section.transform;
            sRt.anchorMin = new Vector2(0.5f, 1);
            sRt.anchorMax = new Vector2(0.5f, 1);
            sRt.pivot = new Vector2(0.5f, 1);
            sRt.anchoredPosition = new Vector2(0, topY);
            sRt.sizeDelta = new Vector2(1150, 470);

            // Section Header
            var headerGo = new GameObject("SectionHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(section.transform, false);
            var hTmp = headerGo.GetComponent<TextMeshProUGUI>();
            Text(hTmp, "Start a game right away!", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            var hRt = (RectTransform)headerGo.transform;
            hRt.anchorMin = new Vector2(0, 1);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.pivot = new Vector2(0, 1);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(0, 32);

            // Left Column: AI Card (Top) + Random Players Card (Bottom)
            var (aiCard, aiBtn) = BuildMatchCard(section.transform, "AI_Card", 0, -45f, 565f, 200f, aiBg, aiBadge,
                Hex("#1450D6", 0.5f), Hex("#0A2A70", 0.7f), "AI", 52f, "Quick Matches", 20f, 209f);

            var (randCard, randBtn) = BuildMatchCard(section.transform, "RandomPlayers_Card", 0, -260f, 565f, 200f, randBg, randIllust,
                Hex("#861218", 0.5f), Hex("#EC1F2B", 0.7f), "Random Players", 40f, "Quick Matches", 20f, 190f);

            // Right Column: Competitive Hero Card (Tall)
            var (compCard, compBtn) = BuildMatchCard(section.transform, "Competitive_Card", 585f, -45f, 565f, 415f, compBg, compTrophy,
                Hex("#DB2743", 0.6f), Hex("#7B0C1E", 0.8f), "Competitive", 56f, "Quick Matches", 22f, 250f);

            // Competitive Lock / Unauthenticated Overlay
            var lockGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockGo.transform.SetParent(compCard.transform, false);
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.color = new Color(0, 0, 0, 0.75f);
            StretchFull(lockGo);

            var lockTextGo = new GameObject("LockText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lockTextGo.transform.SetParent(lockGo.transform, false);
            var ltTmp = lockTextGo.GetComponent<TextMeshProUGUI>();
            Text(ltTmp, "🔒 Sign in to unlock\nCompetitive Matches", fBold, 18f, Hex("#F3F4F6"), TextAlignmentOptions.Center);
            StretchFull(lockTextGo);

            lockGo.SetActive(false); // Managed at runtime by QuickMatchController

            return (aiBtn, randBtn, compBtn, lockGo);
        }

        private static (GameObject card, CustomButtonUI btn) BuildMatchCard(Transform parent, string name, float x, float y, float w, float h,
            Sprite bgSprite, Sprite badgeSprite, Color colA, Color colB, string title, float titleSize, string sub, float subSize, float badgeSize)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CustomButtonUI));
            card.transform.SetParent(parent, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = card.GetComponent<Image>();
            img.sprite = cardRoundedBg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            // Background Art
            if (bgSprite != null)
            {
                var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgGo.transform.SetParent(card.transform, false);
                var bImg = bgGo.GetComponent<Image>();
                bImg.sprite = bgSprite;
                bImg.preserveAspect = false;
                StretchFull(bgGo);
            }

            // Gradient Tint Overlay
            var gradOverlay = GenerateGradient("Grad_" + name, colA, colB, false, 16, 16);
            if (gradOverlay != null)
            {
                var ovGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ovGo.transform.SetParent(card.transform, false);
                var oImg = ovGo.GetComponent<Image>();
                oImg.sprite = gradOverlay;
                StretchFull(ovGo);
            }

            // Badge / Graphic Illustration
            if (badgeSprite != null)
            {
                var bGo = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bGo.transform.SetParent(card.transform, false);
                var bImg = bGo.GetComponent<Image>();
                bImg.sprite = badgeSprite;
                bImg.preserveAspect = true;
                var bRt = (RectTransform)bGo.transform;
                bRt.anchorMin = new Vector2(1, 0.5f);
                bRt.anchorMax = new Vector2(1, 0.5f);
                bRt.pivot = new Vector2(1, 0.5f);
                bRt.anchoredPosition = new Vector2(-15, 0);
                bRt.sizeDelta = new Vector2(badgeSize, badgeSize);
            }

            // Title Text
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(card.transform, false);
            var tTmp = tGo.GetComponent<TextMeshProUGUI>();
            Text(tTmp, title, fExtraBold, titleSize, Color.white, TextAlignmentOptions.TopLeft);
            var tRt = (RectTransform)tGo.transform;
            tRt.anchorMin = new Vector2(0, 1);
            tRt.anchorMax = new Vector2(0.7f, 1);
            tRt.pivot = new Vector2(0, 1);
            tRt.anchoredPosition = new Vector2(25, -20);
            tRt.sizeDelta = new Vector2(0, titleSize + 15);

            // Subtitle Text
            var sGo = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            sGo.transform.SetParent(card.transform, false);
            var sTmp = sGo.GetComponent<TextMeshProUGUI>();
            Text(sTmp, sub, fSemiBold, subSize, Hex("#CBD5E1"), TextAlignmentOptions.TopLeft);
            var sRt = (RectTransform)sGo.transform;
            sRt.anchorMin = new Vector2(0, 1);
            sRt.anchorMax = new Vector2(0.7f, 1);
            sRt.pivot = new Vector2(0, 1);
            sRt.anchoredPosition = new Vector2(25, -25 - titleSize);
            sRt.sizeDelta = new Vector2(0, subSize + 10);

            var btn = card.GetComponent<CustomButtonUI>();
            return (card, btn);
        }

        // =========================================================================
        // SECTION 4: PLAY GAMES SECTION
        // =========================================================================
        private static void BuildPlayGamesSection(Transform parent, float topY)
        {
            var section = new GameObject("PlayGamesSection", typeof(RectTransform));
            section.transform.SetParent(parent, false);
            var sRt = (RectTransform)section.transform;
            sRt.anchorMin = new Vector2(0.5f, 1);
            sRt.anchorMax = new Vector2(0.5f, 1);
            sRt.pivot = new Vector2(0.5f, 1);
            sRt.anchoredPosition = new Vector2(0, topY);
            sRt.sizeDelta = new Vector2(1150, 310);

            // Section Header
            var headerGo = new GameObject("SectionHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(section.transform, false);
            var hTmp = headerGo.GetComponent<TextMeshProUGUI>();
            Text(hTmp, "Play Games", fBold, 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            var hRt = (RectTransform)headerGo.transform;
            hRt.anchorMin = new Vector2(0, 1);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.pivot = new Vector2(0, 1);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(0, 32);

            // Block Card
            BuildGameModeCard(section.transform, "Block_Card", 0, -45f, 565f, 250f,
                Hex("#99015B"), Hex("#FF0197"), blockIllust, "Block");

            // Concentrate Card
            BuildGameModeCard(section.transform, "Concentrate_Card", 585f, -45f, 565f, 250f,
                Hex("#520062"), Hex("#A700C8"), concIllust, "Concentrate");
        }

        private static void BuildGameModeCard(Transform parent, string name, float x, float y, float w, float h,
            Color colA, Color colB, Sprite illust, string title)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var grad = GenerateGradient("Grad_" + name, colA, colB, true, 64, 16);
            var img = card.GetComponent<Image>();
            img.sprite = grad;
            img.color = Color.white;

            // Illustration
            if (illust != null)
            {
                var illGo = new GameObject("Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                illGo.transform.SetParent(card.transform, false);
                var illImg = illGo.GetComponent<Image>();
                illImg.sprite = illust;
                illImg.preserveAspect = true;
                var illRt = (RectTransform)illGo.transform;
                illRt.anchorMin = new Vector2(1, 0.5f);
                illRt.anchorMax = new Vector2(1, 0.5f);
                illRt.pivot = new Vector2(1, 0.5f);
                illRt.anchoredPosition = new Vector2(-15, 0);
                illRt.sizeDelta = new Vector2(230, 230);
            }

            // Title Text
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(card.transform, false);
            var tTmp = tGo.GetComponent<TextMeshProUGUI>();
            Text(tTmp, title, fExtraBold, 48f, Color.white, TextAlignmentOptions.TopLeft);
            var tRt = (RectTransform)tGo.transform;
            tRt.anchorMin = new Vector2(0, 1);
            tRt.anchorMax = new Vector2(0.6f, 1);
            tRt.pivot = new Vector2(0, 1);
            tRt.anchoredPosition = new Vector2(32, -25);
            tRt.sizeDelta = new Vector2(0, 65);
        }

        // =========================================================================
        // UPDATE MIDDLE SCREEN INSTANCE
        // =========================================================================
        private static void UpdateMiddleScreenInstance()
        {
            if (!File.Exists(MiddleScreenPath)) return;
            var middleScreen = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
            try
            {
                var innerScreen = middleScreen.transform.Find("InnerScreen");
                if (innerScreen != null)
                {
                    var existing = innerScreen.GetComponentsInChildren<QuickMatchController>(true)
                        .Select(c => c.gameObject)
                        .ToArray();

                    foreach (var go in existing)
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                    }

                    var qmAsset = AssetDatabase.LoadAssetAtPath<GameObject>(QuickMatchPrefabPath);
                    if (qmAsset != null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(qmAsset, innerScreen);
                        instance.name = "QuickMatchUI_NavPanel";
                        instance.SetActive(true);

                        var iRt = (RectTransform)instance.transform;
                        iRt.anchorMin = Vector2.zero;
                        iRt.anchorMax = Vector2.one;
                        iRt.offsetMin = Vector2.zero;
                        iRt.offsetMax = Vector2.zero;

                        var cg = instance.GetComponent<CanvasGroup>() ?? instance.AddComponent<CanvasGroup>();
                        cg.alpha = 1f;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;

                        Debug.Log("[PlayDashboardRestyler] Updated clean QuickMatchUI_NavPanel instance in MiddleScreen_Scalable.");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(middleScreen, MiddleScreenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleScreen);
            }
        }

        private static void StretchFull(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite GenerateGradient(string fileName, Color colorA, Color colorB, bool horizontal, int width, int height)
        {
            string outPath = $"{GeneratedDir}/{fileName}.png";
            if (!Directory.Exists(GeneratedDir)) Directory.CreateDirectory(GeneratedDir);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = horizontal ? (width > 1 ? (float)x / (width - 1) : 0f) : (height > 1 ? (float)y / (height - 1) : 0f);
                tex.SetPixel(x, y, Color.Lerp(colorA, colorB, t));
            }
            tex.Apply();

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
        }

        private static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        private static void SetField(SerializedObject so, string propName, UnityEngine.Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null)
                p.objectReferenceValue = value;
        }
    }
}
