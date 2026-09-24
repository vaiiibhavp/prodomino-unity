using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Restyles the in-match gameplay board, every mode, to the new Figma design (node 8-2,
    // 2026-09-24): dark navy board with a gold outer border + purple/blue neon inner glow, white
    // rounded domino tiles, dark pill HUD (opponent pill top, "You" pill bottom, Sorting/Pass/Timer
    // bar, chat + fullscreen icons). Plan: [[gameplay-screen-restyle-plan]] in project memory.
    //
    // Target prefabs: one ExtendedGameController per mode, each instantiated at runtime by
    // MenuControllerGameMode.StartDomino() into the Dashboard canvas (sidebar/top bar are
    // untouched Dashboard chrome). Concentrate uses Board/BoardFund + static
    // ConcentrateTiles28/56 preview racks; Block/Draw/Five/French share a GameBoard_Mask/Background
    // frame and spawn tiles at runtime from the single Domino_Slot prefab, so that prefab is
    // restyled once, directly, to cover every mode's actual played/hand tiles.
    internal static class GameplayBoardRestyler
    {
        private static readonly string[] BoardPaths =
        {
            "Assets/DominoTemplate_v2/Prefabs/ConcentrateExtendedGameController.prefab",
            "Assets/DominoTemplate_v2/Prefabs/BlockExtendedGameController.prefab",
            "Assets/DominoTemplate_v2/Prefabs/DrawExtendedGameController.prefab",
            "Assets/DominoTemplate_v2/Prefabs/FiveExtendedGameController.prefab",
            "Assets/DominoTemplate_v2/Prefabs/FrenchExtendedGameController.prefab",
        };
        private const string DominoSlotPath = "Assets/DominoTemplate_v2/Prefabs/Domino_Slot.prefab";
        private const string RenderPreviewPath = "Assets/DominoTemplate_v2/Prefabs/ConcentrateExtendedGameController.prefab";

        private static readonly Color BoardBg = Hex("#0A1128");        // dark navy board interior
        private static readonly Color BoardBorderGold = Hex("#FDC653");
        private static readonly Color BoardGlowPurple = Hex("#6A3DE8");
        private static readonly Color BoardGlowBlue = Hex("#3D7DE8");
        private static readonly Color PillBg = Hex("#0D1226");
        private static readonly Color PillBorder = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color TileFace = Color.white;
        private static readonly Color TileOutline = Hex("#D8DCE6");
        private static readonly Color TimerGreen = Hex("#3DDC64");
        private static readonly Color PassGray = Hex("#3A3F4E");

        internal static Sprite boardPanel, pillSprite, tileSprite, tileOutlineSprite, sortPillGold, passPillGray, timerPillGreen, chatCircle;
        internal static TMP_FontAsset fontRegular, fontMedium, fontBold, fontExtraBold;

        [MenuItem("ProDomino/Dashboard/Restyle Gameplay Board + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            RestyleBoard();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RenderOnly();
            Debug.Log("GAMEPLAY_BOARD_RESTYLE_DONE");
        }

        [MenuItem("ProDomino/Dashboard/Render Gameplay Board To PNG")]
        public static void RenderOnly()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "pd_renders");
            Directory.CreateDirectory(outDir);
            RenderBoard(RenderPreviewPath, Path.Combine(outDir, "gameplay_board.png"), 1920, 1080);
            Debug.Log("RENDER_DONE");
        }

        private static void PrepareAssets()
        {
            fontRegular = LoadFont("Montserrat-Regular");
            fontMedium = LoadFont("Montserrat-Medium");
            fontBold = LoadFont("Montserrat-Bold");
            fontExtraBold = LoadFont("Montserrat-ExtraBold");

            boardPanel = MakePanelSprite("Gameplay_BoardBg", 64, 64, 24, BoardBg, BoardBg, BoardBorderGold, 3f);
            pillSprite = MakeRoundedSprite("Gameplay_Pill", 48, 32, 16, PillBg, PillBg);
            tileSprite = MakeRoundedSprite("Gameplay_TileFace", 32, 48, 8, TileFace, TileFace);
            tileOutlineSprite = MakeRoundedSprite("Gameplay_TileOutline", 32, 48, 8, TileOutline, TileOutline);
            sortPillGold = MakeRoundedSprite("Gameplay_SortPill", 48, 32, 16, AccentStart, AccentEnd);
            passPillGray = MakeRoundedSprite("Gameplay_PassPill", 48, 32, 16, PassGray, PassGray);
            timerPillGreen = MakeRoundedSprite("Gameplay_TimerPill", 48, 32, 16, TimerGreen, TimerGreen);
            chatCircle = MakeCircleSprite("Gameplay_ChatCircle", 48);

            Require(boardPanel, "Gameplay_BoardBg");
            Require(pillSprite, "Gameplay_Pill");
            Require(tileSprite, "Gameplay_TileFace");
        }

        private static void RestyleBoard()
        {
            foreach (var path in BoardPaths) RestyleController(path);
            RestyleDominoSlot();
        }

        private static void RestyleController(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                StyleBoardPanel(root.transform);
                StyleTileRack(root.transform, "ConcentrateTiles28_Container");
                StyleTileRack(root.transform, "ConcentrateTiles56_Container");
                StyleTileRack(root.transform, "BoneyardTiles_Container");
                StyleOpponentPill(root.transform, "ScoreContainer_AI_Top");
                StylePlayerPill(root.transform);
                StyleMessageText(root.transform);
                StyleFullScreenButton(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"GAMEPLAY: {Path.GetFileNameWithoutExtension(path)} restyled.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // The single tile prefab every mode instantiates at runtime for played/hand tiles
        // (DeckController etc. clone this, not the static preview racks below).
        private static void RestyleDominoSlot()
        {
            var root = PrefabUtility.LoadPrefabContents(DominoSlotPath);
            try
            {
                var back = FindDeep(root.transform, "DominoView_BackImage")?.GetComponent<Image>();
                if (back != null) { back.sprite = tileSprite; back.type = Image.Type.Sliced; back.color = TileFace; back.pixelsPerUnitMultiplier = 1f; }

                var shadow = FindDeep(root.transform, "DominoView_Shadow")?.GetComponent<Image>();
                if (shadow != null) shadow.color = new Color(0f, 0f, 0f, 0.35f);

                var glow = FindDeep(root.transform, "DominoView_Glow")?.GetComponent<Image>();
                if (glow != null) glow.color = new Color(1f, 1f, 1f, 0.15f);

                PrefabUtility.SaveAsPrefabAsset(root, DominoSlotPath);
                Debug.Log("GAMEPLAY: Domino_Slot restyled.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Board fill + drop shadow, whichever structure this mode uses:
        //   Concentrate:        Board > Frame > BoardFund > Shadow
        //   Block/Draw/Five/French: GameBoard_Mask > Frame(nested prefab) > Background > ... > Shadow
        private static void StyleBoardPanel(Transform root)
        {
            var board = FindDeep(root, "Board");
            if (board != null)
            {
                var boardImg = board.GetComponent<Image>();
                if (boardImg != null) { boardImg.sprite = null; boardImg.color = new Color(0f, 0f, 0f, 0f); }
            }

            var fill = (board != null ? FindDeep(board, "BoardFund") : null) ?? FindDeep(root, "Background");
            if (fill == null) { Debug.LogWarning("GAMEPLAY: no board fill ('BoardFund'/'Background') found."); return; }

            var fillImg = fill.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.sprite = boardPanel;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = Color.white;
                fillImg.pixelsPerUnitMultiplier = 1f;
            }
            var shadow = FindDeep(fill, "Shadow");
            if (shadow != null)
            {
                var sImg = shadow.GetComponent<Image>();
                if (sImg != null) { sImg.sprite = null; sImg.color = new Color(0f, 0f, 0f, 0.45f); }
            }
        }

        // Domino tile racks: white rounded tile face + thin outline instead of the flat wood tile.
        private static void StyleTileRack(Transform root, string containerName)
        {
            var container = FindDeep(root, containerName);
            if (container == null) { Debug.LogWarning($"GAMEPLAY: '{containerName}' not found."); return; }
            var tiles = FindDeep(container, "TilesContainer");
            if (tiles == null) return;

            for (int i = 0; i < tiles.childCount; i++)
            {
                var tile = tiles.GetChild(i);
                var img = tile.GetComponent<Image>();
                if (img != null) { img.sprite = tileSprite; img.type = Image.Type.Sliced; img.color = TileFace; img.pixelsPerUnitMultiplier = 1f; }

                var hover = tile.Find("HoverStatusContainer_Cg");
                var innerImg = hover != null ? hover.Find("Image")?.GetComponent<Image>() : null;
                if (innerImg != null) { innerImg.color = new Color(1f, 1f, 1f, 0.06f); }
                // Outline sits inside HoverStatusContainer_Cg on the Concentrate racks but is a
                // direct sibling on BoneyardTiles_Container -- search wherever it actually is.
                var outline = FindDeep(tile, "Outline")?.GetComponent<Image>();
                if (outline != null) { outline.sprite = tileOutlineSprite; outline.type = Image.Type.Sliced; outline.color = TileOutline; }
            }
        }

        // Opponent bar (e.g. AI "Tracker") -> dark rounded pill with name + score.
        private static void StyleOpponentPill(Transform root, string containerName)
        {
            var ai = FindDeep(root, containerName);
            if (ai == null) { Debug.LogWarning($"GAMEPLAY: '{containerName}' not found."); return; }
            var bg = FindDeep(ai, "ScoreContainer_BackgroundImage")?.GetComponent<Image>();
            if (bg != null) { bg.sprite = pillSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f; }

            var outline = FindDeep(ai, "ScoreContainer_OutlineImage")?.GetComponent<Image>();
            if (outline != null) { outline.sprite = pillSprite; outline.type = Image.Type.Sliced; outline.color = PillBorder; }

            var glow = FindDeep(ai, "ScoreContainer_GlowImage")?.GetComponent<Image>();
            if (glow != null) glow.color = new Color(BoardGlowPurple.r, BoardGlowPurple.g, BoardGlowPurple.b, 0.25f);

            var name = FindDeep(ai, "ScoreContainer_UserNameText")?.GetComponent<TextMeshProUGUI>();
            if (name != null) { name.font = fontBold; name.fontSharedMaterial = fontBold.material; name.color = Color.white; }

            var score = FindDeep(ai, "ScoreContainer_ScoreText")?.GetComponent<TextMeshProUGUI>();
            if (score != null) { score.font = fontMedium; score.fontSharedMaterial = fontMedium.material; score.color = TextMuted; }
        }

        // "You" bar: score pill, Sorting pill, Pass pill, Timer badge, chat icon.
        private static void StylePlayerPill(Transform root)
        {
            var player = FindDeep(root, "ScoreContainer_Player");
            if (player == null) { Debug.LogWarning("GAMEPLAY: 'ScoreContainer_Player' not found."); return; }

            var scoreBg = FindDeep(player, "Score_BackgroundImage")?.GetComponent<Image>();
            if (scoreBg != null) { scoreBg.sprite = pillSprite; scoreBg.type = Image.Type.Sliced; scoreBg.color = Color.white; scoreBg.pixelsPerUnitMultiplier = 1f; }
            var scoreLabel = FindDeep(player, "Score_ScoreLabelText")?.GetComponent<TextMeshProUGUI>();
            if (scoreLabel != null) { scoreLabel.font = fontBold; scoreLabel.fontSharedMaterial = fontBold.material; scoreLabel.color = Color.white; }

            // "Sorting" button.
            var sortBtn = FindDeep(player, "TilesSort_Button");
            if (sortBtn != null) StylePillButton(sortBtn, sortPillGold, OnAccent, "Sorting");

            // "Pass" button.
            var passBtn = FindDeep(player, "ScoreContainer_PassButton");
            if (passBtn != null) StylePillButton(passBtn, passPillGray, Color.white, "Pass");

            // Hint button keeps its own label; still gets the dark pill treatment.
            var hintBtn = FindDeep(player, "ScoreContainer_HintButton");
            if (hintBtn != null) StylePillButton(hintBtn, pillSprite, Color.white, null);

            // Timer badge ("60 Sec").
            var timer = FindDeep(player, "ScoreContainer_TimerObject");
            if (timer != null)
            {
                var inTurnBg = FindDeep(timer, "Timer_InTurn")?.Find("Timer_BackgroundImage")?.GetComponent<Image>();
                if (inTurnBg != null) { inTurnBg.sprite = timerPillGreen; inTurnBg.type = Image.Type.Sliced; inTurnBg.color = Color.white; inTurnBg.pixelsPerUnitMultiplier = 1f; }
                var offTurnBg = FindDeep(timer, "Timer_OffTurn")?.Find("Timer_BackgroundImage")?.GetComponent<Image>();
                if (offTurnBg != null) { offTurnBg.sprite = pillSprite; offTurnBg.type = Image.Type.Sliced; offTurnBg.color = Color.white; offTurnBg.pixelsPerUnitMultiplier = 1f; }
            }
            var soloTimerText = FindDeep(player, "SoloTimer_Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            if (soloTimerText != null) { soloTimerText.font = fontBold; soloTimerText.fontSharedMaterial = fontBold.material; soloTimerText.color = Color.white; }

            // Chat icon: small dark circle.
            var chatBtn = FindDeep(player, "ScoreContainer_ChatButton");
            if (chatBtn != null)
            {
                var bgImg = FindDeep(chatBtn, "Bg_Image")?.GetComponent<Image>();
                if (bgImg != null) { bgImg.sprite = chatCircle; bgImg.type = Image.Type.Simple; bgImg.color = PillBg; }
            }
        }

        private static void StylePillButton(Transform button, Sprite sprite, Color textColor, string overrideText)
        {
            var bg = FindDeep(button, "Bg_Image")?.GetComponent<Image>();
            if (bg != null) { bg.sprite = sprite; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f; }
            var title = FindDeep(button, "Title_Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
            {
                title.font = fontBold; title.fontSharedMaterial = fontBold.material; title.color = textColor;
                if (!string.IsNullOrEmpty(overrideText)) title.text = overrideText;
            }
        }

        private static void StyleMessageText(Transform root)
        {
            var msg = FindDeep(root, "GameMessageText (TMP)")?.GetComponent<TextMeshProUGUI>();
            if (msg != null) { msg.font = fontMedium; msg.fontSharedMaterial = fontMedium.material; msg.color = Color.white; }
        }

        private static void StyleFullScreenButton(Transform root)
        {
            var btn = FindDeep(root, "FullScreen_Button");
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) { img.color = Color.white; }
        }

        // ------------------------------------------------------------------ render

private static void RenderBoard(string boardPath, string outPath, int width, int height)
        {
            string previousScene = EditorSceneManager.GetActiveScene().path;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("RenderCam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = PageBg;
                cam.orthographic = true;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;

                var canvasGo = new GameObject("RenderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(boardPath), canvasGo.transform);
                var instRt = (RectTransform)inst.transform;
                instRt.anchorMin = Vector2.zero; instRt.anchorMax = Vector2.one;
                instRt.offsetMin = Vector2.zero; instRt.offsetMax = Vector2.zero;
                cam.cullingMask = 1 << inst.layer;

                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var g in inst.GetComponentsInChildren<LayoutGroup>(true))
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                }

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Debug.Log($"RENDERED: {outPath}");
            }
            finally
            {
                // Never leave the Editor parked on the throwaway render scene: reopen whatever
                // scene was active before (falls back to the main scene if that's not on disk).
                string targetScene = !string.IsNullOrEmpty(previousScene) && File.Exists(previousScene)
                    ? previousScene
                    : "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";
                if (File.Exists(targetScene))
                    EditorSceneManager.OpenScene(targetScene, OpenSceneMode.Single);
            }
        }
    }
}
