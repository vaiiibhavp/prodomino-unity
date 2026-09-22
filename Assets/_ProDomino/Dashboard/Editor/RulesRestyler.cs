using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;
using ProDomino.LearningTool;
using ProDomino.Shared;
using Timba.Database;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Restyles LearnTool_UI.prefab and accordion elements to strictly match
    /// the Figma reference (media_1790079058520.png and media_1790079087337.png):
    /// - Header Card:
    ///     - Rounded midnight card (#080B14) with subtle 1px border (#1E2538)
    ///     - Crisp angled domino pattern watermark on top-left
    ///     - Centered "Game Rules" title + "Do you need help for something or do you have some questions" subtitle
    /// - Accordion List:
    ///     - Game modes: Block, Concentrate, Draw, Five, French
    ///     - Collapsed: 50px height, subtle #1E2538 border, "+" toggle button
    ///     - Expanded: Glowing amber #F59E0B border, "-" toggle button,
    ///                 mode banner illustration + overview description + bullet-point rules
    /// </summary>
    public static class RulesRestyler
    {
        private const string RulesPrefabPath = "Assets/_ProDomino/LearningTool/Prefabs/LearnTool_UI.prefab";
        private const string MiddleScreenPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string DatabasePath = "Assets/_ProDomino/Shared/ScriptableObjects/LearningToolsDatabase.asset";
        private const string GameModesDir = "Assets/_ProDomino/_UI/Game_Modes";

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold, fExtraBold;
        private static Sprite screenCardBg, cardNormalBg, cardActiveBg, toggleBtnBg, watermarkSprite;
        private static Sprite bannerBlockBg, bannerConcentrateBg, bannerDrawBg, bannerFiveBg, bannerFrenchBg;

        private static Sprite spriteBlock, spriteConcentrate, spriteDraw, spriteFive, spriteFrench;

        [MenuItem("ProDomino/Dashboard/Restyle Rules Screen + Render")]
        public static void ApplyAndRender()
        {
            Debug.Log("[RulesRestyler] Starting Rules UI restyle...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            BuildCleanRulesScreenPrefab();
            EnsureInMiddleScreen();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderScreenshots();
            Debug.Log("[RulesRestyler] SUCCESS: Rules screen restyled and rendered successfully!");
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");
            fExtraBold = LoadFont("Montserrat-ExtraBold");

            screenCardBg = GetOrCreateScreenCardSprite();

            // Sliced sprites for Accordion Cards
            cardNormalBg = MakePanelSprite("Rules_Card_Normal", 48, 48, 10, Hex("#0D121F"), Hex("#080B14"), Hex("#1E2538"), 1f);
            cardActiveBg = MakePanelSprite("Rules_Card_Active", 48, 48, 10, Hex("#0D121F"), Hex("#080B14"), Hex("#F59E0B"), 1.5f);
            toggleBtnBg = MakePanelSprite("Rules_Toggle_Btn", 32, 32, 8, Hex("#161E30"), Hex("#101624"), Hex("#25314C"), 1f);

            // Mode Banner Gradient Backgrounds (200x120 matching Figma)
            bannerBlockBg = MakeGradientBox("Rules_Banner_Block", 200, 120, 10, Hex("#B91C1C"), Hex("#EA580C"));
            bannerConcentrateBg = MakeGradientBox("Rules_Banner_Concentrate", 200, 120, 10, Hex("#1E1B4B"), Hex("#0284C7"));
            bannerDrawBg = MakeGradientBox("Rules_Banner_Draw", 200, 120, 10, Hex("#0F172A"), Hex("#2563EB"));
            bannerFiveBg = MakeGradientBox("Rules_Banner_Five", 200, 120, 10, Hex("#064E3B"), Hex("#059669"));
            bannerFrenchBg = MakeGradientBox("Rules_Banner_French", 200, 120, 10, Hex("#3B0764"), Hex("#7C3AED"));

            // Procedural Domino Watermark matching Figma media_1790079087337.png
            watermarkSprite = GenerateDominoWatermark("Rules_Domino_Watermark", 300, 135);

            // Mode Illustration Sprites
            spriteBlock = EnsureSprite($"{GameModesDir}/Block_512.png");
            spriteConcentrate = EnsureSprite($"{GameModesDir}/Concentrate_512.png");
            spriteDraw = EnsureSprite($"{GameModesDir}/Draw_512.png");
            spriteFive = EnsureSprite($"{GameModesDir}/Five_512.png");
            spriteFrench = EnsureSprite($"{GameModesDir}/Frenck_512.png")
                           ?? EnsureSprite($"{GameModesDir}/French_512.png");
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

        private static Sprite MakeGradientBox(string name, int w, int h, int radius, Color left, Color right)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                var fill = Color.Lerp(left, right, t);

                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float inside = Mathf.Clamp01(radius - dist + 0.5f);

                fill.a *= inside;
                tex.SetPixel(x, y, fill);
            }
            return SaveSlicedSprite(path, tex, radius + 2);
        }

        private static Sprite GenerateDominoWatermark(string name, int w, int h)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, clear);

            // Draw angled domino tiles matching Figma media_1790079087337.png
            DrawAngledDomino(tex, 35, 105, 54, 108, -36f, Hex("#1E293B"), 0.60f, Hex("#94A3B8"), 0.85f, new[] { 1, 2 });
            DrawAngledDomino(tex, 95, 85, 54, 108, -36f, Hex("#1E293B"), 0.55f, Hex("#94A3B8"), 0.85f, new[] { 4, 3 });
            DrawAngledDomino(tex, 155, 65, 54, 108, -36f, Hex("#1E293B"), 0.50f, Hex("#94A3B8"), 0.80f, new[] { 6, 5 });
            DrawAngledDomino(tex, 55, 25, 54, 108, -36f, Hex("#1E293B"), 0.50f, Hex("#94A3B8"), 0.85f, new[] { 2, 4 });
            DrawAngledDomino(tex, 115, 10, 54, 108, -36f, Hex("#1E293B"), 0.45f, Hex("#94A3B8"), 0.75f, new[] { 5, 1 });
            DrawAngledDomino(tex, 215, 45, 54, 108, -36f, Hex("#1E293B"), 0.40f, Hex("#94A3B8"), 0.70f, new[] { 3, 2 });

            tex.Apply();
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawAngledDomino(Texture2D tex, int centerX, int centerY, int tileW, int tileH, float angleDeg, Color tileColor, float tileAlpha, Color pipColor, float pipAlpha, int[] pips)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            int halfW = tileW / 2;
            int halfH = tileH / 2;

            for (int dy = -halfH - 2; dy <= halfH + 2; dy++)
            for (int dx = -halfW - 2; dx <= halfW + 2; dx++)
            {
                int px = centerX + Mathf.RoundToInt(dx * cos - dy * sin);
                int py = centerY + Mathf.RoundToInt(dx * sin + dy * cos);

                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height)
                    continue;

                float cx = Mathf.Clamp(dx, -halfW + 4, halfW - 4);
                float cy = Mathf.Clamp(dy, -halfH + 4, halfH - 4);
                float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(cx, cy));
                float inside = Mathf.Clamp01(5f - dist);

                if (inside > 0f)
                {
                    Color c = tileColor;
                    float alpha = tileAlpha * inside;

                    // Thin divider line across the center (dy == 0)
                    if (Mathf.Abs(dy) <= 1)
                    {
                        c = pipColor;
                        alpha = pipAlpha * inside;
                    }

                    // Draw pips (dots) on each half
                    int numPips = dy < 0 ? (pips != null && pips.Length > 0 ? pips[0] : 0) : (pips != null && pips.Length > 1 ? pips[1] : 0);
                    float cyCenter = dy < 0 ? -halfH / 2f : halfH / 2f;

                    if (IsNearPip(dx, dy - cyCenter, numPips, halfW, halfH / 2f, out float pipDist))
                    {
                        if (pipDist < 3.2f)
                        {
                            c = Color.white;
                            alpha = Mathf.Max(alpha, pipAlpha * Mathf.Clamp01(3.2f - pipDist));
                        }
                    }

                    Color existing = tex.GetPixel(px, py);
                    Color blended = Color.Lerp(existing, c, alpha);
                    blended.a = Mathf.Max(existing.a, alpha);
                    tex.SetPixel(px, py, blended);
                }
            }
        }

        private static bool IsNearPip(float localX, float localY, int count, float halfW, float halfH, out float minDist)
        {
            minDist = float.MaxValue;
            if (count <= 0) return false;

            float ox = halfW * 0.45f;
            float oy = halfH * 0.45f;

            List<Vector2> positions = new List<Vector2>();
            if (count == 1) { positions.Add(Vector2.zero); }
            else if (count == 2) { positions.Add(new Vector2(-ox, -oy)); positions.Add(new Vector2(ox, oy)); }
            else if (count == 3) { positions.Add(new Vector2(-ox, -oy)); positions.Add(Vector2.zero); positions.Add(new Vector2(ox, oy)); }
            else if (count == 4) { positions.Add(new Vector2(-ox, -oy)); positions.Add(new Vector2(ox, -oy)); positions.Add(new Vector2(-ox, oy)); positions.Add(new Vector2(ox, oy)); }
            else if (count == 5) { positions.Add(new Vector2(-ox, -oy)); positions.Add(new Vector2(ox, -oy)); positions.Add(Vector2.zero); positions.Add(new Vector2(-ox, oy)); positions.Add(new Vector2(ox, oy)); }
            else if (count == 6) { positions.Add(new Vector2(-ox, -oy)); positions.Add(new Vector2(ox, -oy)); positions.Add(new Vector2(-ox, 0)); positions.Add(new Vector2(ox, 0)); positions.Add(new Vector2(-ox, oy)); positions.Add(new Vector2(ox, oy)); }

            foreach (var p in positions)
            {
                float d = Vector2.Distance(new Vector2(localX, localY), p);
                if (d < minDist) minDist = d;
            }
            return minDist < 3.2f;
        }

        private static void BuildCleanRulesScreenPrefab()
        {
            var db = AssetDatabase.LoadAssetAtPath<LearningToolsDatabase>(DatabasePath);
            if (db == null)
            {
                Debug.LogError($"[RulesRestyler] Failed to load database at {DatabasePath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(RulesPrefabPath);
            try
            {
                var rootRt = (RectTransform)root.transform;
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

                var learnUI = root.GetComponent<LearningToolUI>() ?? root.AddComponent<LearningToolUI>();

                // Clear all previous children
                var toDestroy = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                    toDestroy.Add(root.transform.GetChild(i).gameObject);
                foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);

                // Main Container
                var mainContent = CreateExplicitRect(root.transform, "MainContent", 0f, 0f, 1f, 1f);
                mainContent.offsetMin = new Vector2(28f, 20f);
                mainContent.offsetMax = new Vector2(-28f, -16f);

                // -----------------------------------------------------------------
                // 1. Top Card: Game Rules Header + Watermark (Height 120px)
                // -----------------------------------------------------------------
                var headerCard = CreateExplicitRect(mainContent, "Header_Card", 0f, 1f, 1f, 1f);
                headerCard.pivot = new Vector2(0.5f, 1f);
                headerCard.sizeDelta = new Vector2(0f, 120f);
                headerCard.anchoredPosition = new Vector2(0f, 0f);

                var hCardImg = headerCard.gameObject.AddComponent<Image>();
                hCardImg.sprite = screenCardBg;
                hCardImg.type = Image.Type.Sliced;
                hCardImg.color = Color.white;

                // Watermark in top-left
                var watermarkGo = CreateExplicitRect(headerCard, "Watermark", 0f, 0f, 0f, 1f);
                watermarkGo.pivot = new Vector2(0f, 0.5f);
                watermarkGo.sizeDelta = new Vector2(300f, 0f);
                watermarkGo.anchoredPosition = new Vector2(0f, 0f);
                var wImg = watermarkGo.gameObject.AddComponent<Image>();
                wImg.sprite = watermarkSprite;
                wImg.preserveAspect = true;
                wImg.raycastTarget = false;
                wImg.color = Color.white;

                // Header Texts (Centered)
                var titleText = CreateExplicitText(headerCard, "TitleText", "Game Rules", fBold, 24f, Color.white, TextAlignmentOptions.Center);
                var tRt = titleText.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0f, 0.5f);
                tRt.anchorMax = new Vector2(1f, 0.5f);
                tRt.pivot = new Vector2(0.5f, 0.5f);
                tRt.anchoredPosition = new Vector2(0f, 12f);
                tRt.sizeDelta = new Vector2(0f, 30f);

                var subText = CreateExplicitText(headerCard, "SubtitleText", "Do you need help for something or do you have some questions", fRegular, 13f, Hex("#94A3B8"), TextAlignmentOptions.Center);
                var sRt = subText.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0.5f);
                sRt.anchorMax = new Vector2(1f, 0.5f);
                sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = new Vector2(0f, -14f);
                sRt.sizeDelta = new Vector2(0f, 24f);

                // -----------------------------------------------------------------
                // 2. Scrollable Accordion Section
                // -----------------------------------------------------------------
                var scrollGo = CreateExplicitRect(mainContent, "Scroll_View_Rules", 0f, 0f, 1f, 1f);
                scrollGo.offsetMin = new Vector2(0f, 0f);
                scrollGo.offsetMax = new Vector2(0f, -135f);

                var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 25f;

                var viewport = CreateExplicitRect(scrollGo, "Viewport", 0f, 0f, 1f, 1f);
                viewport.gameObject.AddComponent<RectMask2D>();
                scrollRect.viewport = viewport;

                var content = CreateExplicitRect(viewport, "Content", 0f, 1f, 1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;

                var contentVlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 12f;
                contentVlg.padding = new RectOffset(0, 0, 2, 20);
                contentVlg.childControlWidth = true;
                contentVlg.childControlHeight = true;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;

                var contentCsf = content.gameObject.AddComponent<ContentSizeFitter>();
                contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                scrollRect.content = content;

                // -----------------------------------------------------------------
                // 3. Create Accordion Items for each game mode in order
                // -----------------------------------------------------------------
                var modeConfigs = new[]
                {
                    ("block", "Block", spriteBlock, bannerBlockBg),
                    ("concentrate", "Concentrate", spriteConcentrate, bannerConcentrateBg),
                    ("draw", "Draw", spriteDraw, bannerDrawBg),
                    ("five", "Five", spriteFive, bannerFiveBg),
                    ("french", "French", spriteFrench, bannerFrenchBg)
                };

                var itemsList = new List<RulesAccordionItem>();

                for (int i = 0; i < modeConfigs.Length; i++)
                {
                    var cfg = modeConfigs[i];
                    var itemData = db.GetItemById(cfg.Item1);

                    string desc = itemData?.descriptionText ?? "Mode overview description.";
                    string rawRules = itemData?.rulesText ?? "";
                    Sprite illu = cfg.Item3 ?? itemData?.spriteImg;

                    bool isDefaultExpanded = (i == 0); // "Block" is expanded by default in Figma

                    var accordionItem = CreateAccordionItem(
                        content,
                        cfg.Item1,
                        cfg.Item2,
                        illu,
                        cfg.Item4,
                        desc,
                        rawRules,
                        isDefaultExpanded
                    );

                    itemsList.Add(accordionItem);
                }

                // Wire up LearningToolUI serialized fields
                var uiSo = new SerializedObject(learnUI);
                var cgProp = uiSo.FindProperty("<RootCanvasGroup>k__BackingField") ?? uiSo.FindProperty("RootCanvasGroup");
                if (cgProp != null) cgProp.objectReferenceValue = cg;

                var dbProp = uiSo.FindProperty("learningToolsGameModeData");
                if (dbProp != null) dbProp.objectReferenceValue = db;

                var acProp = uiSo.FindProperty("accordionContainer");
                if (acProp != null) acProp.objectReferenceValue = content;

                var srProp = uiSo.FindProperty("scrollRect");
                if (srProp != null) srProp.objectReferenceValue = scrollRect;

                var itemsProp = uiSo.FindProperty("accordionItems");
                if (itemsProp != null)
                {
                    itemsProp.ClearArray();
                    for (int i = 0; i < itemsList.Count; i++)
                    {
                        itemsProp.InsertArrayElementAtIndex(i);
                        itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = itemsList[i];
                    }
                }

                uiSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, RulesPrefabPath);
                Debug.Log("[RulesRestyler] LearnTool_UI prefab successfully restyled to Figma accordion design!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RulesAccordionItem CreateAccordionItem(
            RectTransform parent,
            string modeId,
            string title,
            Sprite bannerIllustration,
            Sprite bannerBg,
            string description,
            string rawRules,
            bool isExpanded)
        {
            var itemGo = new GameObject($"Item_{title}", typeof(RectTransform));
            var itemRt = (RectTransform)itemGo.transform;
            itemRt.SetParent(parent, false);

            var itemCardImg = itemGo.AddComponent<Image>();
            itemCardImg.sprite = isExpanded ? cardActiveBg : cardNormalBg;
            itemCardImg.type = Image.Type.Sliced;
            itemCardImg.color = Color.white;

            var itemVlg = itemGo.AddComponent<VerticalLayoutGroup>();
            itemVlg.spacing = 0f;
            itemVlg.padding = new RectOffset(0, 0, 0, 0);
            itemVlg.childControlWidth = true;
            itemVlg.childControlHeight = true;
            itemVlg.childForceExpandWidth = true;
            itemVlg.childForceExpandHeight = false;

            var itemCsf = itemGo.AddComponent<ContentSizeFitter>();
            itemCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            itemCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var accordion = itemGo.AddComponent<RulesAccordionItem>();

            // -------------------------------------------------------------
            // Header Row (Height 50px)
            // -------------------------------------------------------------
            var headerGo = CreateExplicitRect(itemRt, "Header_Row", 0f, 0f, 1f, 1f);
            var headerLe = headerGo.gameObject.AddComponent<LayoutElement>();
            headerLe.minHeight = 50f;
            headerLe.preferredHeight = 50f;
            headerLe.flexibleHeight = 0f;

            var headerBtn = headerGo.gameObject.AddComponent<Button>();
            headerBtn.transition = Selectable.Transition.None;
            var headerTransparentImg = headerGo.gameObject.AddComponent<Image>();
            headerTransparentImg.color = Color.clear;
            headerTransparentImg.raycastTarget = true;

            // Title Label
            var titleTmp = CreateExplicitText(headerGo, "Title_Text", title, fBold, 17f, Color.white, TextAlignmentOptions.MidlineLeft);
            var ttRt = titleTmp.GetComponent<RectTransform>();
            ttRt.offsetMin = new Vector2(24f, 0f);
            ttRt.offsetMax = new Vector2(-60f, 0f);

            // Toggle Button Box (Right)
            var toggleBox = CreateExplicitRect(headerGo, "Toggle_Button", 1f, 0.5f, 1f, 0.5f);
            toggleBox.pivot = new Vector2(1f, 0.5f);
            toggleBox.anchoredPosition = new Vector2(-16f, 0f);
            toggleBox.sizeDelta = new Vector2(30f, 30f);

            var toggleImg = toggleBox.gameObject.AddComponent<Image>();
            toggleImg.sprite = toggleBtnBg;
            toggleImg.type = Image.Type.Sliced;
            toggleImg.color = Color.white;
            toggleImg.raycastTarget = false;

            var toggleIconTmp = CreateExplicitText(toggleBox, "Icon_Text", isExpanded ? "-" : "+", fBold, 18f, Color.white, TextAlignmentOptions.Center);
            toggleIconTmp.raycastTarget = false;

            // -------------------------------------------------------------
            // Expandable Content Container
            // -------------------------------------------------------------
            var contentGo = CreateExplicitRect(itemRt, "Content_Container", 0f, 0f, 1f, 1f);
            var contentVlg = contentGo.gameObject.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 14f;
            contentVlg.padding = new RectOffset(24, 24, 6, 18);
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var contentCsf = contentGo.gameObject.AddComponent<ContentSizeFitter>();
            contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Row 1: Banner + Description
            var bannerRow = CreateExplicitRect(contentGo, "Banner_Description_Row", 0f, 0f, 1f, 1f);
            var bannerRowHlg = bannerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bannerRowHlg.spacing = 24f;
            bannerRowHlg.childControlWidth = true;
            bannerRowHlg.childControlHeight = true;
            bannerRowHlg.childForceExpandWidth = false;
            bannerRowHlg.childForceExpandHeight = false;
            bannerRowHlg.childAlignment = TextAnchor.UpperLeft;

            var bannerRowLe = bannerRow.gameObject.AddComponent<LayoutElement>();
            bannerRowLe.minHeight = 120f;
            bannerRowLe.preferredHeight = 120f;
            bannerRowLe.flexibleHeight = 0f;

            // Left: Banner Container (200x120 matching Figma)
            var bannerBox = CreateExplicitRect(bannerRow, "Banner_Container", 0f, 0f, 0f, 1f);
            bannerBox.sizeDelta = new Vector2(200f, 120f);
            var bannerBoxLe = bannerBox.gameObject.AddComponent<LayoutElement>();
            bannerBoxLe.minWidth = 200f;
            bannerBoxLe.preferredWidth = 200f;
            bannerBoxLe.flexibleWidth = 0f;
            bannerBoxLe.minHeight = 120f;
            bannerBoxLe.preferredHeight = 120f;
            bannerBoxLe.flexibleHeight = 0f;

            var bannerBoxImg = bannerBox.gameObject.AddComponent<Image>();
            bannerBoxImg.sprite = bannerBg;
            bannerBoxImg.type = Image.Type.Sliced;
            bannerBoxImg.color = Color.white;

            // Mode 3D Illustration inside Banner
            var illuGo = CreateExplicitRect(bannerBox, "Banner_Illustration", 0.5f, 0.5f, 0.5f, 0.5f);
            illuGo.pivot = new Vector2(0.5f, 0.5f);
            illuGo.sizeDelta = new Vector2(95f, 95f);
            var illuImg = illuGo.gameObject.AddComponent<Image>();
            illuImg.sprite = bannerIllustration;
            illuImg.preserveAspect = true;
            illuImg.raycastTarget = false;

            // Right: Description Text (flexible width matching Figma)
            var descTmp = CreateExplicitText(bannerRow, "Description_Text", description, fMedium, 13f, Hex("#CBD5E1"), TextAlignmentOptions.TopLeft);
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            descTmp.overflowMode = TextOverflowModes.Overflow;
            descTmp.lineSpacing = 13f;

            var descLe = descTmp.gameObject.AddComponent<LayoutElement>();
            descLe.flexibleWidth = 1f;
            descLe.minWidth = 300f;
            descLe.minHeight = 120f;
            descLe.preferredHeight = 120f;
            descLe.flexibleHeight = 0f;

            // Row 2: Bullet-point Rules (tight clean line spacing matching Figma)
            var formattedRules = RulesAccordionItem.FormatBulletRules(rawRules);
            var rulesTmp = CreateExplicitText(contentGo, "Rules_Text", formattedRules, fRegular, 12f, Hex("#CBD5E1"), TextAlignmentOptions.TopLeft);
            rulesTmp.textWrappingMode = TextWrappingModes.Normal;
            rulesTmp.overflowMode = TextOverflowModes.Overflow;
            rulesTmp.lineSpacing = 12f;
            rulesTmp.paragraphSpacing = 5f;

            var rulesCsf = rulesTmp.gameObject.AddComponent<ContentSizeFitter>();
            rulesCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rulesCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            contentGo.gameObject.SetActive(isExpanded);

            // Setup component serialized fields
            var accSo = new SerializedObject(accordion);
            accSo.FindProperty("headerButton").objectReferenceValue = headerBtn;
            accSo.FindProperty("cardBackgroundImage").objectReferenceValue = itemCardImg;
            accSo.FindProperty("titleText").objectReferenceValue = titleTmp;
            accSo.FindProperty("toggleIconText").objectReferenceValue = toggleIconTmp;
            accSo.FindProperty("toggleButtonImage").objectReferenceValue = toggleImg;

            accSo.FindProperty("contentContainer").objectReferenceValue = contentGo;
            accSo.FindProperty("bannerContainerImage").objectReferenceValue = bannerBoxImg;
            accSo.FindProperty("bannerImage").objectReferenceValue = illuImg;
            accSo.FindProperty("descriptionText").objectReferenceValue = descTmp;
            accSo.FindProperty("rulesText").objectReferenceValue = rulesTmp;

            accSo.FindProperty("normalCardSprite").objectReferenceValue = cardNormalBg;
            accSo.FindProperty("activeCardSprite").objectReferenceValue = cardActiveBg;

            accSo.ApplyModifiedPropertiesWithoutUndo();

            accordion.Setup(
                modeId,
                title,
                bannerIllustration,
                bannerBg,
                description,
                rawRules,
                cardNormalBg,
                cardActiveBg,
                null
            );

            accordion.SetExpanded(isExpanded);

            return accordion;
        }

        private static void EnsureInMiddleScreen()
        {
            if (!File.Exists(MiddleScreenPath)) return;

            var middleScreen = PrefabUtility.LoadPrefabContents(MiddleScreenPath);
            try
            {
                var innerScreen = middleScreen.transform.Find("InnerScreen");
                if (innerScreen != null)
                {
                    var existing = innerScreen.GetComponentsInChildren<LearningToolUI>(true);
                    if (existing == null || existing.Length == 0)
                    {
                        var rulesAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RulesPrefabPath);
                        if (rulesAsset != null)
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rulesAsset, innerScreen);
                            instance.name = "LearnTool_UI";
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

                            Debug.Log("[RulesRestyler] Instantiated clean LearnTool_UI in MiddleScreen_Scalable.");
                        }
                    }
                    else
                    {
                        foreach (var inst in existing)
                        {
                            var iRt = (RectTransform)inst.transform;
                            iRt.anchorMin = Vector2.zero;
                            iRt.anchorMax = Vector2.one;
                            iRt.offsetMin = Vector2.zero;
                            iRt.offsetMax = Vector2.zero;

                            if (inst.TryGetComponent<CanvasGroup>(out var pcg))
                            {
                                pcg.alpha = 0f;
                                pcg.interactable = false;
                                pcg.blocksRaycasts = false;
                            }
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(middleScreen, MiddleScreenPath);
                Debug.Log("[RulesRestyler] Verified Rules screen in MiddleScreen_Scalable!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middleScreen);
            }
        }

        private static void RenderScreenshots()
        {
            var outDir = Path.Combine(Application.dataPath, "../pd_renders");
            Directory.CreateDirectory(outDir);

            // 1. Render Expanded Block state (media_1790079087337.png match)
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_rules.png"), 1920, 1080, true, root =>
            {
                ActivateRulesScreen(root, true);
            });

            // 2. Render Collapsed state (media_1790079058520.png match)
            SidebarRestyler.RenderCanvas(Path.Combine(outDir, "screen_rules_collapsed.png"), 1920, 1080, true, root =>
            {
                ActivateRulesScreen(root, false);
            });

            Debug.Log($"[RulesRestyler] Rendered Rules screenshots to {outDir}");
        }

        private static void ActivateRulesScreen(GameObject canvasRoot, bool expandFirst)
        {
            // Deactivate Dashboard Content
            var dash = PdUiKit.FindDeep(canvasRoot.transform, "Dashboard_Content");
            if (dash != null)
            {
                dash.gameObject.SetActive(false);
                if (dash.TryGetComponent<CanvasGroup>(out var dcg))
                {
                    dcg.alpha = 0f;
                    dcg.interactable = false;
                    dcg.blocksRaycasts = false;
                }
            }

            // Find target screen under InnerScreen and activate it
            var inner = PdUiKit.FindDeep(canvasRoot.transform, "InnerScreen");
            if (inner != null)
            {
                for (int i = 0; i < inner.childCount; i++)
                {
                    var child = inner.GetChild(i);
                    bool isTarget = child.name.IndexOf("Learn", StringComparison.OrdinalIgnoreCase) >= 0;
                    child.gameObject.SetActive(isTarget);

                    foreach (var cg in child.GetComponentsInChildren<CanvasGroup>(true))
                    {
                        cg.alpha = isTarget ? 1f : 0f;
                        cg.interactable = isTarget;
                        cg.blocksRaycasts = isTarget;
                    }

                    if (isTarget && child.TryGetComponent<LearningToolUI>(out var lt))
                    {
                        var items = child.GetComponentsInChildren<RulesAccordionItem>(true);
                        for (int j = 0; j < items.Length; j++)
                        {
                            items[j].SetExpanded(expandFirst && j == 0);
                            if (!expandFirst)
                            {
                                items[j].SetSelected(j == 0);
                            }
                        }
                    }
                }
            }

            // Highlight Rules button on the sidebar
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CustomButtonUI")).FirstOrDefault(t => t != null);
            if (type != null)
            {
                var preview = type.GetMethod("PreviewVisualState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var idProp = type.GetProperty("CustomButtonID");

                var navCtrl = PdUiKit.FindDeep(canvasRoot.transform, "NavegationPanelController");
                if (navCtrl != null)
                {
                    foreach (var b in navCtrl.GetComponentsInChildren(type, true))
                    {
                        string id = (string)idProp?.GetValue(b);
                        bool select = string.Equals(id, "Learn", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(id, "Rules", StringComparison.OrdinalIgnoreCase);
                        preview?.Invoke(b, new object[] { select });
                    }
                }
            }
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
