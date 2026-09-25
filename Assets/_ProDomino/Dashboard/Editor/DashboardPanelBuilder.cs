using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    // Every element below is placed with an explicit pixel rect (top-left anchored), copied
    // directly from the original Figma extraction. This intentionally does NOT rely on
    // Unity's LayoutGroup/ContentSizeFitter runtime computation: that was tried first and
    // silently failed to ever resolve (children stayed at their default 0x0 rect) when built
    // headlessly via -batchmode, most likely because Canvas/LayoutRebuilder machinery depends
    // on a render loop that batch mode doesn't run. Explicit rects have no such dependency.
    internal static class DashboardPanelBuilder
    {
        private const string ArtDir = "Assets/_ProDomino/_Art/Dashboard";
        private const string IconDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";
        private const string FontDir = "Assets/_ProDomino/_UI/Fonts/Dashboard";
        private const string GeneratedDir = "Assets/_ProDomino/Dashboard/Generated";
        private const string PrefabDir = "Assets/_ProDomino/Dashboard/Prefabs";
        private const string OutputPrefabPath = PrefabDir + "/Dashboard_Panel.prefab";

        [MenuItem("ProDomino/Dashboard/Build Dashboard Panel")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            EnsureFolder(GeneratedDir);
            EnsureFolder(PrefabDir);

            var fRegular = LoadFont("Montserrat-Regular SDF");
            var fSemiBold = LoadFont("Montserrat-SemiBold SDF");
            var fBold = LoadFont("Montserrat-Bold SDF");
            var fExtraBold = LoadFont("Montserrat-ExtraBold SDF");

            var root = new GameObject("Dashboard_Panel", typeof(RectTransform));
            SetRect(root, 0, 0, 1150, 1306);

            BuildChallengeBanner(root.transform, fBold, fRegular, fSemiBold);
            BuildStatPillsRow(root.transform, fBold, fRegular, fSemiBold);
            BuildQuickMatchSection(root.transform, fRegular, fSemiBold, fExtraBold);
            BuildPlayGamesSection(root.transform, fRegular, fExtraBold);

            PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Dashboard panel built at {OutputPrefabPath}");
        }

        // ---------------------------------------------------------------
        // Sections (each rect below is [x,y relative to its own parent, w, h], top-left origin)
        // ---------------------------------------------------------------

        internal static void BuildChallengeBanner(Transform parent, TMP_FontAsset bold, TMP_FontAsset regular, TMP_FontAsset semiBold)
        {
            var banner = CreateImage(parent, "ChallengeBanner", Hex("#000000"));
            SetRect(banner, 0, 0, 1150, 360);

            var bg = CreateImage(banner.transform, "BackgroundImage", Color.white, LoadArtSprite("ChallengeBanner_Background"));
            StretchFull(bg);
            var overlay = CreateImage(banner.transform, "Overlay", Color.white, GenerateGradient("Grad_BannerOverlay", Hex("#000051", 0.35f), Hex("#000033", 0.15f), false, 8, 128));
            StretchFull(overlay);

            var content = CreatePlain(banner.transform, "ContentColumn");
            SetRect(content, 48, 38, 617, 284);

            var textBlock = CreatePlain(content.transform, "TextBlock");
            SetRect(textBlock, 0, 0, 514, 143);
            CreateText(textBlock.transform, "Title", "Complete Your Monthly Challenge!!", bold, 42, Hex("#FFFFFF"), 0, 0, 481, 102);
            CreateText(textBlock.transform, "Subtitle", "Play 50 block games and earn big rewards!", regular, 24, Hex("#E6E6E7"), 0, 114, 514, 29);

            var progressRow = CreatePlain(content.transform, "ProgressRow");
            SetRect(progressRow, 0, 163, 617, 44);

            var progressBlock = CreatePlain(progressRow.transform, "ProgressBlock");
            SetRect(progressBlock, 0, 0, 500, 38);
            CreateText(progressBlock.transform, "ProgressLabel", "23/50", bold, 20, Hex("#FFFFFF"), 0, 0, 57, 24);

            var track = CreateImage(progressBlock.transform, "ProgressTrack", Hex("#01010C", 0.5f));
            SetRect(track, 0, 34, 500, 4);
            var fill = CreateImage(track.transform, "ProgressFill", Hex("#FDC653"));
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(23f / 50f, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var rewardChip = CreateImage(progressRow.transform, "RewardChip", Color.white, GenerateGradient("Grad_RewardChip", Hex("#031173"), Hex("#E2E6FF"), true, 64, 8));
            SetRect(rewardChip, 520, 0, 97, 44);
            CreateImage(rewardChip.transform, "CoinIcon", Color.white, LoadIconSprite("Icon_Coin")).SetRect(7, 6, 32, 32);
            CreateText(rewardChip.transform, "RewardValue", "300", bold, 24, Hex("#FFFFFF"), 43, 7, 47, 29);

            var playWinBtn = CreateImage(content.transform, "PlayWinButton", Color.white, GenerateGradient("Grad_ButtonPrimary", Hex("#FFA501"), Hex("#FDC653"), true, 64, 8));
            SetRect(playWinBtn, 0, 227, 217, 57);
            CreateText(playWinBtn.transform, "Label", "Play & Win", bold, 24, Hex("#01010C"), 40, 14, 137, 29);

            var dots = CreatePlain(banner.transform, "PaginationDots");
            SetRect(dots, 554, 330, 42, 12);
            var dotSprite = GenerateCircle("Circle_Dot", 32);
            CreateImage(dots.transform, "Dot1", Hex("#FDC553"), dotSprite).SetRect(0, 0, 12, 12);
            CreateImage(dots.transform, "Dot2", Hex("#01010C", 0.7f), dotSprite).SetRect(17, 1, 10, 10);
            CreateImage(dots.transform, "Dot3", Hex("#01010C", 0.7f), dotSprite).SetRect(32, 1, 10, 10);
        }

        internal static void BuildStatPillsRow(Transform parent, TMP_FontAsset bold, TMP_FontAsset regular, TMP_FontAsset semiBold)
        {
            var row = CreatePlain(parent, "StatPillsRow");
            SetRect(row, 0, 384, 1150, 113);

            BuildStatPill(row.transform, "StatPill_GamesPlayedToday", 0, "Games Played Today", "Users", "200",
                Hex("#000023"), Hex("#000089"), bold, regular, semiBold);
            BuildStatPill(row.transform, "StatPill_UserPlayingNow", 389, "User Playing Now", "Online", "20",
                Hex("#1A001F"), Hex("#520062"), bold, regular, semiBold);
            BuildStatPill(row.transform, "StatPill_ActivePlayer", 777, "Active Player", "Online", "1.5k",
                Hex("#290018"), Hex("#D6007F"), bold, regular, semiBold);
        }

        private static void BuildStatPill(Transform parent, string name, float x, string title, string subLabel, string value,
            Color colorA, Color colorB, TMP_FontAsset bold, TMP_FontAsset regular, TMP_FontAsset semiBold)
        {
            var pill = CreateImage(parent, name, Color.white, GenerateGradient("Grad_" + name, colorA, colorB, false, 8, 128));
            SetRect(pill, x, 0, 373, 113);

            CreateText(pill.transform, "Title", title, bold, 24, Hex("#FFFFFF"), 20, 14, 260, 29);

            var innerPill = CreateImage(pill.transform, "AvatarPill", Hex("#01010C", 0.4f));
            SetRect(innerPill, 20, 55, 143, 44);
            CreateImage(innerPill.transform, "Avatar1", Color.white, LoadArtSprite("StatPill_Avatar1")).SetRect(8, 6, 32, 32);
            CreateImage(innerPill.transform, "Avatar2", Color.white, LoadArtSprite("StatPill_Avatar2")).SetRect(26, 6, 32, 32);
            CreateImage(innerPill.transform, "Avatar3", Color.white, LoadArtSprite("StatPill_Avatar3")).SetRect(44, 6, 32, 32);

            var textCol = CreatePlain(innerPill.transform, "CountText");
            SetRect(textCol, 84, 5, 40, 35);
            CreateText(textCol.transform, "SubLabel", subLabel, regular, 12, Hex("#FFFFFF"), 0, 0, 40, 15);
            CreateText(textCol.transform, "Value", value, semiBold, 16, Hex("#FFFFFF"), 0, 14, 40, 20);

            CreateImage(innerPill.transform, "OnlineDot", Hex("#1DF324")).SetRect(67, 32, 6, 6);
        }

        internal static void BuildQuickMatchSection(Transform parent, TMP_FontAsset regular, TMP_FontAsset semiBold, TMP_FontAsset extraBold)
        {
            var section = CreatePlain(parent, "QuickMatchSection");
            SetRect(section, 0, 521, 1150, 463);

            BuildSectionHeader(section.transform, "SectionIcon_QuickMatch", "Start a game right away!", regular);

            var cardsRow = CreatePlain(section.transform, "CardsRow");
            SetRect(cardsRow, 0, 45, 1150, 418);

            var leftColumn = CreatePlain(cardsRow.transform, "LeftColumn");
            SetRect(leftColumn, 0, 0, 567, 418);

            BuildQuickMatchCard(leftColumn.transform, "AI_Card", 0, 0, "QuickMatch_AI_Background", "QuickMatch_AI_Badge",
                Hex("#1450D6", 0.5f), Hex("#0A2A70", 0.7f), "AI", 60, 24, 20, "Quick Matches", 24, 95, extraBold, semiBold, 567, 201);
            BuildQuickMatchCard(leftColumn.transform, "RandomPlayers_Card", 0, 217, "QuickMatch_RandomPlayers_Background", "QuickMatch_RandomPlayers_Illustration",
                Hex("#861218", 0.5f), Hex("#EC1F2B", 0.7f), "Random Players", 44, 24, 20, "Quick Matches", 24, 80, extraBold, semiBold, 567, 201);

            var competitive = CreateImage(cardsRow.transform, "Competitive_Card", Hex("#0A0A0A"));
            SetRect(competitive, 583, 0, 567, 418);
            var compBg = CreateImage(competitive.transform, "Background", Color.white, LoadArtSprite("QuickMatch_Competitive_Background"));
            StretchFull(compBg);
            var compOverlay = CreateImage(competitive.transform, "Overlay", Color.white, GenerateGradient("Grad_Competitive_Card_v2", Hex("#DB2743", 0.15f), Hex("#DB2743", 0.05f), false, 8, 8));
            StretchFull(compOverlay);
            CreateImage(competitive.transform, "Trophy", Color.white, LoadArtSprite("QuickMatch_Competitive_Trophy")).SetRect(220, 120, 300, 270);
            CreateText(competitive.transform, "Title", "Competitive", extraBold, 60, Hex("#FFFFFF"), 25, 24, 400, 73);
            CreateText(competitive.transform, "Subtitle", "Quick Matches", semiBold, 24, Hex("#FFFFFF"), 25, 99, 200, 29);
        }

        private static void BuildQuickMatchCard(Transform parent, string name, float x, float y, string bgArt, string badgeArt,
            Color overlayA, Color overlayB, string title, float titleSize, float titleX, float titleY,
            string subtitle, float subtitleSize, float subtitleY, TMP_FontAsset titleFont, TMP_FontAsset subFont, float w, float h)
        {
            var card = CreateImage(parent, name, Hex("#0A0A0A"));
            SetRect(card, x, y, w, h);

            var bg = CreateImage(card.transform, "Background", Color.white, LoadArtSprite(bgArt));
            StretchFull(bg);
            var overlay = CreateImage(card.transform, "Overlay", Color.white, GenerateGradient("Grad_" + name, overlayA, overlayB, false, 8, 8));
            StretchFull(overlay);

            var badge = LoadArtSprite(badgeArt);
            if (badge != null)
                CreateImage(card.transform, "Badge", Color.white, badge).SetRect(w - 209 - 6, (h - 209) / 2f, 209, 209);

            CreateText(card.transform, "Title", title, titleFont, titleSize, Hex("#FFFFFF"), titleX, titleY, w - 48, titleSize + 20);
            CreateText(card.transform, "Subtitle", subtitle, subFont, subtitleSize, Hex("#FFFFFF"), titleX, subtitleY, w - 48, subtitleSize + 6);
        }

        internal static void BuildPlayGamesSection(Transform parent, TMP_FontAsset regular, TMP_FontAsset extraBold)
        {
            var section = CreatePlain(parent, "PlayGamesSection");
            SetRect(section, 0, 1008, 1150, 298);

            BuildSectionHeader(section.transform, "SectionIcon_PlayGames", "Play Games", regular);

            var cardsRow = CreatePlain(section.transform, "CardsRow");
            SetRect(cardsRow, 0, 45, 1150, 253);

            BuildPlayGamesCard(cardsRow.transform, "Block_Card", 0, Hex("#99015B"), Hex("#FF0197"), "PlayGames_Block_Illustration", "Block", extraBold);
            BuildPlayGamesCard(cardsRow.transform, "Concentrate_Card", 583, Hex("#520062"), Hex("#A700C8"), "PlayGames_Concentrate_Illustration", "Concentrate", extraBold);
        }

        private static void BuildPlayGamesCard(Transform parent, string name, float x, Color colorA, Color colorB, string illustration, string title, TMP_FontAsset font)
        {
            var card = CreateImage(parent, name, Color.white, GenerateGradient("Grad_" + name, colorA, colorB, true, 64, 8));
            SetRect(card, x, 0, 567, 253);

            var illust = CreateImage(card.transform, "Illustration", Color.white, LoadArtSprite(illustration));
            illust.SetRect(320, 11, 230, 230);

            CreateText(card.transform, "Title", title, font, 52, Hex("#FFFFFF"), 32, 20, 260, 70);
        }

        private static void BuildSectionHeader(Transform parent, string iconName, string label, TMP_FontAsset font)
        {
            var header = CreatePlain(parent, "SectionHeader");
            SetRect(header, 0, 0, 1150, 29);
            CreateImage(header.transform, "Icon", Color.white, LoadIconSprite(iconName)).SetRect(0, 3, 24, 24);
            CreateText(header.transform, "Label", label, font, 24, Hex("#FFFFFF", 0.9f), 36, 0, 300, 29);
        }

        // ---------------------------------------------------------------
        // Low-level helpers
        // ---------------------------------------------------------------

        private static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        internal static TMP_FontAsset LoadFont(string name) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{name}.asset");

        private static Sprite LoadArtSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        private static Sprite LoadIconSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{name}.png");

        // Top-left anchored rect: (x,y) is the offset from the parent's top-left corner,
        // (w,h) is the exact pixel size. No layout system involved — this IS the final rect.
        internal static void SetRect(GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static GameObject CreatePlain(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = CreatePlain(parent, name);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string text, TMP_FontAsset font, float fontSize, Color color,
            float x, float y, float w, float h)
        {
            var go = CreatePlain(parent, name);
            SetRect(go, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string assetsPath)
        {
            if (AssetDatabase.IsValidFolder(assetsPath)) return;
            var parts = assetsPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAbsolutePath(string assetsRelativePath)
        {
            return Application.dataPath + assetsRelativePath.Substring("Assets".Length);
        }

        private static Sprite GenerateGradient(string fileName, Color colorA, Color colorB, bool horizontal, int width, int height)
        {
            string outPath = $"{GeneratedDir}/{fileName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            if (existing != null) return existing;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = horizontal ? (width > 1 ? (float)x / (width - 1) : 0f) : (height > 1 ? (float)y / (height - 1) : 0f);
                    tex.SetPixel(x, y, Color.Lerp(colorA, colorB, t));
                }
            }
            tex.Apply();

            File.WriteAllBytes(ToAbsolutePath(outPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
        }

        private static Sprite GenerateCircle(string fileName, int size)
        {
            string outPath = $"{GeneratedDir}/{fileName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            if (existing != null) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();

            File.WriteAllBytes(ToAbsolutePath(outPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
        }
    }

    internal static class DashboardPanelBuilderExtensions
    {
        // Small fluent helper so call sites can chain: CreateImage(...).SetRect(...)
        public static GameObject SetRect(this GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }
    }
}
