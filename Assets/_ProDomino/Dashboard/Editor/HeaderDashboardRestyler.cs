using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.SidebarRestyler;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Header (UserControlCenter.prefab) and dashboard content (ProDomino_MainCanvas.prefab) for the
    // new Figma design. The header keeps every existing script and button: chips are restyled
    // around them, and new chips are wired to real data (rank per game mode, token balance).
    // The dashboard is rebuilt responsive: sections stretch to the available width, background
    // art is cropped instead of stretched, cards are rounded and the page scrolls.
    internal static class HeaderDashboardRestyler
    {
        private const string UccPath = "Assets/_ProDomino/Shared/Prefabs/UserControlCenter.prefab";
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";



        private static readonly Color ClassChipBg = ChipBg;
        private static readonly Color DropdownBg = PdUiKit.DropdownBg;
        private static readonly Color Muted = TextMuted;
        private static readonly Color AvatarBg = Hex("#C54216");

        // Header geometry, in pixels of the 1920x1080 reference: chips are 64 tall, 20 from the
        // top; the right group ends 24 px from the right edge with 12 px gaps.
        private const float ChipTop = 20f;
        private const float ChipHeight = 64f;
        private const float RightMargin = 24f;
        // Profile is wider than the Figma 159 px so typical usernames fit at full size.
        private const float ProfileWidth = 190f, FlagWidth = 89f, BellWidth = 64f, TokenMinWidth = 106f, Gap = 12f;
        private const float ClassChipAnchorX = 258f / 1920f; // lines up with the dashboard content

        private static Sprite chipSprite, rewardChipSprite, circleSprite, chevronSprite;
        private static TMP_FontAsset fontSemiBold;

        [MenuItem("ProDomino/Dashboard/Restyle Header + Dashboard + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            fontSemiBold = LoadFont("Montserrat-SemiBold");
            chipSprite = MakeRoundedSprite("Rounded_Chip_R10", 64, 64, 10, Hex("#040614"), Hex("#191A1D"));
            rewardChipSprite = MakeRoundedSprite("Rounded_RewardChip_R10", 97, 44, 10, Hex("#031173"), Hex("#E2E6FF"));
            circleSprite = MakeCircleSprite("Circle_128", 128);
            chevronSprite = MakeChevron("Chevron_Down", 32);

            RestyleHeader();
            RebuildDashboard();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CleanSceneOverrides();
            RenderOnly();
            RenderSceneOnly();
            Debug.Log("HEADER_DASHBOARD_DONE");
        }

        private const string ScenePath = "Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity";

        [MenuItem("ProDomino/Dashboard/Render MainSceneDomDemo Canvas To PNG")]
        public static void RenderSceneOnly()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pd_renders");
            System.IO.Directory.CreateDirectory(outDir);
            RenderSceneCanvas(ScenePath, CanvasPath, System.IO.Path.Combine(outDir, "scene_runtime.png"), true);
            RenderSceneCanvas(ScenePath, CanvasPath, System.IO.Path.Combine(outDir, "scene_editmode.png"), false);
        }

        // The game scene keeps its own copy of thousands of saved layout values (anchors, sizes,
        // font sizes) on the canvas instance. They override the restyled sidebar/header layout, so
        // those are reverted for everything under Background (sidebar, header). Script fields,
        // events, active states and the game panels under MiddleScreen are left untouched.
        private static Type[] SceneRevertibleTypes => RevertibleTypes
            .Concat(new[] { typeof(VerticalLayoutGroup), typeof(HorizontalLayoutGroup), typeof(CanvasGroup) })
            .ToArray();

        [MenuItem("ProDomino/Dashboard/Clean MainSceneDomDemo Layout Overrides")]
        public static void CleanSceneOverrides()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) { Debug.LogWarning("SCENE: scene not found."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var canvas = FindCanvasInstance(scene);
            if (canvas == null) { Debug.LogWarning("SCENE: ProDomino_MainCanvas instance not found in scene."); return; }
            var background = canvas.transform.Find("Background");
            if (background == null) { Debug.LogWarning("SCENE: Background not found under canvas instance."); return; }

            int reverted = 0;
            foreach (var comp in background.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || !SceneRevertibleTypes.Contains(comp.GetType())) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(comp) || !HasOverrides(comp)) continue;
                PrefabUtility.RevertObjectOverride(comp, InteractionMode.AutomatedAction);
                reverted++;
            }

            RestoreScenePanels(canvas);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"SCENE: reverted layout/visual overrides on {reverted} sidebar/header components and saved {ScenePath}.");
        }

        // An early dashboard experiment switched off every panel in InnerScreen (and the
        // Current_Party_Popup) directly in this scene, which hides the Play panel the dashboard
        // lives on. Those scene-only states are reverted so the panels follow the prefab again.
        // Other scene-only removals/states are only logged.
        private static void RestoreScenePanels(GameObject canvas)
        {
            foreach (var removed in PrefabUtility.GetRemovedGameObjects(canvas))
            {
                var asset = removed.assetGameObject;
                var path = asset ? AssetPath(asset.transform) : "(unknown)";
                bool isPanel = asset && asset.transform.parent && asset.transform.parent.name == "InnerScreen";
                if (isPanel || (asset && asset.name == "Current_Party_Popup"))
                {
                    removed.Revert();
                    Debug.Log($"SCENE: restored removed '{path}'.");
                }
                else
                    Debug.Log($"SCENE: left scene removal in place: '{path}'.");
            }

            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                var so = new SerializedObject(t.gameObject);
                var active = so.FindProperty("m_IsActive");
                if (active == null || !active.prefabOverride) continue;

                bool isPanel = t.parent && t.parent.name == "InnerScreen";
                if (isPanel || t.name == "Current_Party_Popup")
                {
                    PrefabUtility.RevertPropertyOverride(active, InteractionMode.AutomatedAction);
                    Debug.Log($"SCENE: reverted active-state override on '{AssetPath(t)}' (now {t.gameObject.activeSelf}).");
                }
                else
                    Debug.Log($"SCENE: kept active-state override on '{AssetPath(t)}' = {t.gameObject.activeSelf}.");
            }
        }

        internal static GameObject FindCanvasInstance(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)
                        && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == CanvasPath)
                        return t.gameObject;
            return null;
        }

        private static bool HasOverrides(Component comp)
        {
            var it = new SerializedObject(comp).GetIterator();
            while (it.Next(true))
                if (it.prefabOverride) return true;
            return false;
        }

        // ================================================================== header

        private static void RestyleHeader()
        {
            var root = PrefabUtility.LoadPrefabContents(UccPath);
            var rootRt = (RectTransform)root.transform;
            var saved = (rootRt.anchorMin, rootRt.anchorMax, rootRt.pivot, rootRt.anchoredPosition, rootRt.sizeDelta);
            Canvas tempCanvas = null;
            try
            {
                // Dropdowns get their own sorting canvas (like the existing header dropdowns). A
                // canvas only keeps nested settings while it has a parent canvas, so the prefab
                // root gets a temporary one while editing.
                if (!root.GetComponent<Canvas>())
                    tempCanvas = root.AddComponent<Canvas>();

                var ucc = root.transform;
                HideLegacyItems(ucc);
                StyleRankChip(ucc);
                StyleTokenChip(ucc);
                StyleBellChip(ucc);
                StyleFlagChip(ucc);
                StyleProfileChip(ucc);

                if (tempCanvas)
                {
                    UnityEngine.Object.DestroyImmediate(tempCanvas);
                    (rootRt.anchorMin, rootRt.anchorMax, rootRt.pivot, rootRt.anchoredPosition, rootRt.sizeDelta) = saved;
                }

                PrefabUtility.SaveAsPrefabAsset(root, UccPath);
                Debug.Log("HEADER: UserControlCenter saved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Items that are not part of the new header. They stay in the hierarchy with their
        // scripts running (so nothing that references them breaks) but are invisible and
        // don't take clicks. Re-enable by moving them out of Header_Legacy_Hidden.
        private static void HideLegacyItems(Transform ucc)
        {
            var hidden = ucc.Find("Header_Legacy_Hidden");
            if (hidden == null)
            {
                hidden = new GameObject("Header_Legacy_Hidden", typeof(RectTransform)).transform;
                hidden.SetParent(ucc, false);
            }
            Stretch((RectTransform)hidden);
            hidden.SetAsFirstSibling();
            var cg = GetOrAdd<CanvasGroup>(hidden);
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            foreach (var n in new[] { "GamesPlayedToday_Scalable", "UsersPlayingNow_Scalable", "MonthlySubscription_Button", "Missions_Scalable" })
            {
                var t = ucc.Find(n);
                if (t != null) t.SetParent(hidden, false);
            }
        }

        private static void StyleRankChip(Transform ucc)
        {
            var chip = Need(ucc, "PlayerBestRank_Scalable");
            var rt = (RectTransform)chip;
            rt.anchorMin = rt.anchorMax = new Vector2(ClassChipAnchorX, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -26f);
            rt.sizeDelta = new Vector2(200f, 53f);

            var bg = GetOrAdd<Image>(chip);
            bg.sprite = roundedWhite; bg.type = Image.Type.Sliced; bg.color = ClassChipBg;
            bg.pixelsPerUnitMultiplier = 1f; bg.raycastTarget = true;
            var button = GetOrAdd<Button>(chip);
            StyleButton(button, bg);

            var icon = (RectTransform)Need(chip, "PlayerBestRank_IconImage");
            MiddleLeft(icon, 12f, 26f, 26f);
            icon.GetComponent<Image>().preserveAspect = true;

            var title = Need(chip, "PlayerBestRank_RankText (TMP)");
            PlaceTopLeft((RectTransform)title, 46f, -7f, 124f, 21f);
            var titleTmp = Label(title.GetComponent<TextMeshProUGUI>(), fontMedium, 16f, Color.white);
            titleTmp.text = "Class C";

            var modeT = chip.Find("PlayerBestRank_ModeText");
            var mode = modeT ? modeT.GetComponent<TextMeshProUGUI>() : MakeText(chip, "PlayerBestRank_ModeText", "Block Game", fontMedium, 10f, Muted);
            PlaceTopLeft((RectTransform)mode.transform, 46f, -29f, 124f, 14f);
            Label(mode, fontMedium, 10f, Muted);

            Arrow(chip, "PlayerBestRank_Arrow", 12f, 14f);

            var old = chip.Find("PlayerBestRank_ModeDropdown");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var dropdown = new GameObject("PlayerBestRank_ModeDropdown", typeof(RectTransform));
            dropdown.transform.SetParent(chip, false);
            var drt = (RectTransform)dropdown.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0f);
            drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(0f, -6f);
            drt.sizeDelta = new Vector2(240f, 48f);
            AddSortingCanvas(dropdown, 1000);
            var dcg = dropdown.AddComponent<CanvasGroup>();
            dcg.alpha = 0f; dcg.interactable = false; dcg.blocksRaycasts = false;
            var dbg = dropdown.AddComponent<Image>();
            dbg.sprite = roundedWhite; dbg.type = Image.Type.Sliced; dbg.color = DropdownBg; dbg.pixelsPerUnitMultiplier = 1f;
            var vlg = dropdown.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 2f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            dropdown.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var item = new GameObject("ModeItem_Template", typeof(RectTransform));
            item.transform.SetParent(dropdown.transform, false);
            var itemImg = item.AddComponent<Image>();
            // Row highlight is the image colour; the tint hides it except on hover/press.
            itemImg.sprite = roundedWhite; itemImg.type = Image.Type.Sliced; itemImg.color = new Color(1f, 1f, 1f, 0.1f); itemImg.pixelsPerUnitMultiplier = 1f;
            var itemBtn = item.AddComponent<Button>();
            itemBtn.targetGraphic = itemImg;
            var colors = itemBtn.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            itemBtn.colors = colors;
            var itemLe = item.AddComponent<LayoutElement>();
            itemLe.minHeight = 36f; itemLe.preferredHeight = 36f;
            var itemLabel = MakeText(item.transform, "Label", "Block Game · 2P  <color=#B0B0B4>Class C</color>", fontMedium, 14f, Color.white);
            var lrt = (RectTransform)itemLabel.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-12f, 0f);
            itemLabel.richText = true;
            item.SetActive(false);

            var ctrl = chip.GetComponent("PlayerBestRankController");
            if (ctrl == null) throw new Exception("HEADER: PlayerBestRankController missing on PlayerBestRank_Scalable.");
            var so = new SerializedObject(ctrl);
            so.FindProperty("includeScoreInLabel").boolValue = false;
            so.FindProperty("gameModeLabel").objectReferenceValue = mode;
            so.FindProperty("modeDropdownButton").objectReferenceValue = button;
            so.FindProperty("modeDropdown").objectReferenceValue = dcg;
            so.FindProperty("modeDropdownItemTemplate").objectReferenceValue = itemBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleTokenChip(Transform ucc)
        {
            var old = ucc.Find("Tokens_Chip");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var chip = new GameObject("Tokens_Chip", typeof(RectTransform));
            chip.transform.SetParent(ucc, false);
            chip.transform.SetSiblingIndex(Need(ucc, "PlayerBestRank_Scalable").GetSiblingIndex() + 1);
            TopRight((RectTransform)chip.transform, RightMargin + ProfileWidth + Gap + FlagWidth + Gap + BellWidth + Gap, TokenMinWidth);

            var bg = chip.AddComponent<Image>();
            bg.sprite = chipSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f;
            var button = chip.AddComponent<Button>();
            StyleButton(button, bg);
            var cg = chip.AddComponent<CanvasGroup>();

            // Grows to the left for bigger balances.
            var hlg = chip.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 14, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            chip.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chip.AddComponent<LayoutElement>().minWidth = TokenMinWidth;

            var coin = MakeImage(chip.transform, "Coin_Icon", AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Icon_Coin.png"), Color.white, Image.Type.Simple);
            coin.GetComponent<Image>().preserveAspect = true;
            ((RectTransform)coin.transform).sizeDelta = new Vector2(32f, 32f);
            var coinLe = coin.AddComponent<LayoutElement>();
            coinLe.minWidth = 32f; coinLe.preferredWidth = 32f;

            var amount = MakeText(chip.transform, "Amount_Text", "0", fontSemiBold, 24f, Color.white);
            ((RectTransform)amount.transform).sizeDelta = new Vector2(48f, 30f);
            amount.alignment = TextAlignmentOptions.MidlineLeft;

            var comp = chip.AddComponent(FindType("ProDomino.Shop.HeaderTokenChip"));
            var so = new SerializedObject(comp);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("amountLabel").objectReferenceValue = amount;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("navigationButtonId").stringValue = "Shop";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleBellChip(Transform ucc)
        {
            var notif = Need(ucc, "Notifications_Scalable");
            var notifUi = Need(notif, "Notifications_UI");
            var openButton = Need(notifUi, "NotPressed_NotifUI").GetComponent<Button>();

            var old = ucc.Find("Bell_Chip");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var chip = new GameObject("Bell_Chip", typeof(RectTransform));
            chip.transform.SetParent(ucc, false);
            float right = RightMargin + ProfileWidth + Gap + FlagWidth + Gap;
            TopRight((RectTransform)chip.transform, right, BellWidth);
            var bg = chip.AddComponent<Image>();
            bg.sprite = chipSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f;
            StyleButton(chip.AddComponent<Button>(), bg);
            Forward(chip, openButton);
            chip.transform.SetSiblingIndex(notif.GetSiblingIndex());

            // The bell itself stays 32 px (its dropdown is sized relative to it) and sits centred
            // on the chip; the dropdown gets a fixed size under the chip's right edge.
            var nrt = (RectTransform)notif;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(-(right + BellWidth / 2f), -(ChipTop + ChipHeight / 2f));
            nrt.sizeDelta = new Vector2(32f, 32f);

            var pressed = (RectTransform)Need(notifUi, "Pressed_NotifUI");
            pressed.anchorMin = pressed.anchorMax = new Vector2(1f, 0f);
            pressed.pivot = new Vector2(1f, 1f);
            pressed.anchoredPosition = new Vector2(16f, -24f);
            pressed.sizeDelta = new Vector2(682f, 430f);
        }

        private static void StyleFlagChip(Transform ucc)
        {
            var nat = Need(ucc, "Nationality_Scalable");
            TopRight((RectTransform)nat, RightMargin + ProfileWidth + Gap, FlagWidth);

            // The dropdown's own button becomes the chip, so the whole chip opens the list.
            var btn = Need(nat, "Nationality_ScalableButton");
            DisableFitter(btn);
            Stretch((RectTransform)btn);
            var img = btn.GetComponent<Image>();
            img.sprite = chipSprite; img.type = Image.Type.Sliced; img.color = Color.white; img.pixelsPerUnitMultiplier = 1f;

            HideImage(btn, "Nationality_GlowImage");
            HideImage(btn, "Nationality_BorderIcon");

            var mask = (RectTransform)Need(btn, "Nationality_CountryMask");
            MiddleLeft(mask, 12f, 37f, 24f);
            var maskImg = mask.GetComponent<Image>();
            maskImg.sprite = roundedWhite; maskImg.type = Image.Type.Sliced; maskImg.pixelsPerUnitMultiplier = 2.5f;
            Stretch((RectTransform)Need(mask, "Nationality_CountryImage"));

            Arrow(btn, "Nationality_Arrow", 12f, 16f);

            var template = (RectTransform)Need(btn, "Template");
            template.anchorMin = template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(1f, 1f);
            template.anchoredPosition = new Vector2(0f, -8f);
            template.sizeDelta = new Vector2(240f, 264f);
        }

        private static void StyleProfileChip(Transform ucc)
        {
            var opt = Need(ucc, "Options_Scalable");
            TopRight((RectTransform)opt, RightMargin, ProfileWidth);

            var profile = Need(opt, "Options_Profile");
            DisableFitter(profile);
            Stretch((RectTransform)profile);

            var logged = Need(profile, "Logged_Interface");
            var avatarBtn = Need(logged, "OpenProfile_ScalableButton");

            var chipBgT = profile.Find("Profile_ChipBg");
            var chipBg = chipBgT ? chipBgT.gameObject : new GameObject("Profile_ChipBg", typeof(RectTransform));
            chipBg.transform.SetParent(profile, false);
            chipBg.transform.SetAsFirstSibling();
            Stretch((RectTransform)chipBg.transform);
            var bg = GetOrAdd<Image>(chipBg.transform);
            bg.sprite = chipSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f; bg.raycastTarget = true;
            StyleButton(GetOrAdd<Button>(chipBg.transform), bg);
            Forward(chipBg, avatarBtn.GetComponent<Button>());

            var notLogged = Need(profile, "NotLogged_Interface");
            Stretch((RectTransform)notLogged);
            foreach (Transform c in notLogged)
                if (c.name == "User_Button" && c.gameObject.activeSelf)
                    Stretch((RectTransform)c);

            Stretch((RectTransform)logged);

            // Round avatar on the left.
            DisableFitter(avatarBtn);
            MiddleLeft((RectTransform)avatarBtn, 12f, 48f, 48f);
            var avatarBg = avatarBtn.GetComponent<Image>();
            avatarBg.sprite = circleSprite; avatarBg.type = Image.Type.Simple; avatarBg.color = AvatarBg; avatarBg.preserveAspect = false;
            GetOrAdd<Mask>(avatarBtn).showMaskGraphic = true;
            HideImage(avatarBtn, "OpenProfile_GlowImage");
            HideImage(avatarBtn, "OpenProfile_BorderIcon");
            Stretch((RectTransform)Need(avatarBtn, "OpenProfile_AvatarImage"));

            // The alert badge would be clipped by the round mask, so it moves next to the avatar.
            var alert = avatarBtn.Find("AlertIcon") ?? logged.Find("AlertIcon");
            if (alert != null)
            {
                alert.SetParent(logged, false);
                var art = (RectTransform)alert;
                art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f);
                art.pivot = new Vector2(0.5f, 0.5f);
                art.anchoredPosition = new Vector2(12f + 48f - 6f, 18f);
                art.sizeDelta = new Vector2(16f, 16f);
                alert.SetAsLastSibling();
            }

            var name = (RectTransform)Need(logged, "Scalabe_Player_Label");
            name.anchorMin = Vector2.zero; name.anchorMax = Vector2.one;
            name.pivot = new Vector2(0f, 0.5f);
            name.offsetMin = new Vector2(68f, 0f); name.offsetMax = new Vector2(-32f, 0f);
            var nameTmp = Label(name.GetComponent<TextMeshProUGUI>(), fontSemiBold, 16f, Color.white);
            nameTmp.fontSizeMin = 12f;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;

            Arrow(logged, "Profile_Arrow", 12f, 16f);

            // Account menu opens under the chip, right-aligned, at its previous width.
            var menu = (RectTransform)Need(profile, "Options_Container");
            menu.anchorMin = menu.anchorMax = new Vector2(1f, 0f);
            menu.pivot = new Vector2(1f, 1f);
            menu.anchoredPosition = new Vector2(0f, -8f);
            menu.sizeDelta = new Vector2(230f, menu.sizeDelta.y);
            var csf = menu.GetComponent<ContentSizeFitter>();
            if (csf) csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            var menuBg = menu.Find("Background");
            if (menuBg && menuBg.TryGetComponent<LayoutElement>(out var menuBgLe) && menuBgLe.ignoreLayout)
            {
                Stretch((RectTransform)menuBg);
                var mimg = menuBg.GetComponent<Image>();
                mimg.sprite = roundedWhite; mimg.type = Image.Type.Sliced; mimg.color = DropdownBg; mimg.pixelsPerUnitMultiplier = 1f;
            }
        }

        // ================================================================== dashboard

        private static void RebuildDashboard()
        {
            var fRegular = DashboardPanelBuilder.LoadFont("Montserrat-Regular SDF");
            var fSemiBold = DashboardPanelBuilder.LoadFont("Montserrat-SemiBold SDF");
            var fBold = DashboardPanelBuilder.LoadFont("Montserrat-Bold SDF");
            var fExtraBold = DashboardPanelBuilder.LoadFont("Montserrat-ExtraBold SDF");

            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                RestoreRemovedPanels(root.transform);
                RevertHeaderLayoutOverrides(root.transform);

                var inner = FindDeep(root.transform, "InnerScreen") ?? throw new Exception("DASH: InnerScreen not found.");
                var old = inner.Find("Dashboard_Content");
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

                // Fills the area right of the sidebar and under the header (x 258..1896, y 109..1076
                // at 1920x1080), then scrolls vertically.
                var dash = new GameObject("Dashboard_Content", typeof(RectTransform));
                dash.transform.SetParent(inner, false);
                var drt = (RectTransform)dash.transform;
                drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
                drt.offsetMin = new Vector2(-17f, -26f);
                drt.offsetMax = new Vector2(6f, 41f);

                var viewport = new GameObject("Viewport", typeof(RectTransform));
                viewport.transform.SetParent(dash.transform, false);
                Stretch((RectTransform)viewport.transform);
                var vimg = viewport.AddComponent<Image>();
                vimg.color = new Color(0f, 0f, 0f, 0f);
                viewport.AddComponent<RectMask2D>();

                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                var crt = (RectTransform)content.transform;
                crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = new Vector2(0f, 1306f);

                var scroll = dash.AddComponent<ScrollRect>();
                scroll.content = crt;
                scroll.viewport = (RectTransform)viewport.transform;
                scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.inertia = true;
                scroll.scrollSensitivity = 40f;

                DashboardPanelBuilder.BuildChallengeBanner(content.transform, fBold, fRegular, fSemiBold);
                DashboardPanelBuilder.BuildStatPillsRow(content.transform, fBold, fRegular, fSemiBold);
                DashboardPanelBuilder.BuildQuickMatchSection(content.transform, fRegular, fSemiBold, fExtraBold);
                DashboardPanelBuilder.BuildPlayGamesSection(content.transform, fRegular, fExtraBold);

                MakeResponsive(content.transform);
                WireDashboard(root.transform, dash, content.transform);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
                Debug.Log("DASH: Dashboard_Content rebuilt.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // An earlier integration step deleted every panel next to the old game-mode screen
        // (game modes, quick match, shop, leaderboards, login, pop-ups...). They live in the nested
        // MiddleScreen_Scalable prefab, so the deletions are "removed object" overrides and can be
        // reverted, which restores them exactly with all their references.
        private static void RestoreRemovedPanels(Transform canvasRoot)
        {
            var middle = FindDeep(canvasRoot, "MiddleScreen_Scalable");
            if (middle == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(middle.gameObject))
            {
                Debug.LogWarning("DASH: MiddleScreen_Scalable prefab instance not found; nothing restored.");
                return;
            }

            // Only whole panels (direct children of InnerScreen) were deleted by that step; deeper
            // removals are the original developer's and stay removed.
            foreach (var removed in PrefabUtility.GetRemovedGameObjects(middle.gameObject))
            {
                var asset = removed.assetGameObject;
                if (asset == null || asset.transform.parent == null || asset.transform.parent.name != "InnerScreen")
                    continue;
                removed.Revert();
                Debug.Log($"DASH: restored panel '{asset.name}'.");
            }

            KeepDeveloperRemovals(middle);
        }

        // Removal overrides the original developer had on MiddleScreen_Scalable before the dashboard
        // work (placeholder club chat rows, extra video buttons, a music slider, spare scrollbars and
        // four components). Ids are from the canvas as it was before the panels were restored; the
        // top-level panel ids in the same list are skipped because those must stay restored.
        private static readonly long[] OriginalRemovedGameObjects =
        {
            1904432591918855094, 8144816453275619632, 25577236878434585, 7492210914985182332, 5861594181049248345,
            8957841941473873101, 523211915050571395, 5107701415562368950, 6554342244886152667, 5742783434221522124,
            4376888934921451577, 1617692333376255980, 8630222196027693899, 4314310414770121054, 8834964967302550681,
            9031843794553163750, 8659631810116771247, 849139038832825659, 1850165580243160410, 1794453525225422563,
            4767073025339361190, 3082040007152522600, 1734222314402454088, 6438560271411667654, 3917882579587495835,
            6206590110595682583, 9179976739497850995,
        };
        private static readonly long[] OriginalRemovedComponents =
        {
            6140513603371267994, 4388386413176719640, 6860656909666972489, 8804104032546767917,
        };

        private static void KeepDeveloperRemovals(Transform middle)
        {
            var goIds = OriginalRemovedGameObjects.ToHashSet();
            var compIds = OriginalRemovedComponents.ToHashSet();

            foreach (var comp in middle.GetComponentsInChildren<Component>(true).ToArray())
            {
                if (comp == null || comp is Transform || !TryGetAssetId(comp, out var id) || !compIds.Contains(id)) continue;
                Debug.Log($"DASH: re-removing original developer removal: component {comp.GetType().Name} on {AssetPath(comp.transform)}");
                UnityEngine.Object.DestroyImmediate(comp);
            }

            foreach (var t in middle.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null || t.parent == null || t.parent.name == "InnerScreen") continue;
                if (!TryGetAssetId(t.gameObject, out var id) || !goIds.Contains(id)) continue;
                Debug.Log($"DASH: re-removing original developer removal: {AssetPath(t)}");
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        // Local file id of the object this instance object comes from, inside MiddleScreen_Scalable.prefab.
        private static bool TryGetAssetId(UnityEngine.Object instanceObject, out long id)
        {
            id = 0;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
            return source != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out id);
        }

        private static string AssetPath(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        // The dashboard is the lobby view of the Play panel (GameModeConfig): the old selection UI
        // is hidden, the cards start matches through GameModeConfig, and the dashboard hides while
        // a match is on screen.
        private static void WireDashboard(Transform canvasRoot, GameObject dash, Transform content)
        {
            var inner = dash.transform.parent;
            var playPanel = inner.Find("GameModeSelectUI_NavPanel") ?? throw new Exception("DASH: GameModeSelectUI_NavPanel missing after restore.");
            if (!playPanel.TryGetComponent(FindType("GameModeConfig"), out var gameModeConfig))
                throw new Exception("DASH: GameModeConfig missing.");

            // Right after the Play panel, so login/pop-ups and other panels still draw above it.
            dash.transform.SetSiblingIndex(playPanel.GetSiblingIndex() + 1);

            // The old QuickMatch panel was the start-up screen; the dashboard replaces it. Navigation
            // hides it at runtime, this keeps it from showing through in the editor too.
            var quickMatch = inner.Find("QuickMatchUI_NavPanel");
            if (quickMatch && quickMatch.TryGetComponent<CanvasGroup>(out var quickMatchCg))
            {
                quickMatchCg.alpha = 0f; quickMatchCg.interactable = false; quickMatchCg.blocksRaycasts = false;
            }

            var legacySelection = playPanel.Find("Scroll View_SelectionUI");
            if (legacySelection)
            {
                var cg = GetOrAdd<CanvasGroup>(legacySelection);
                cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
            }
            var legacyBackground = playPanel.Find("UI_Base_General")?.GetComponent<Image>();
            if (legacyBackground) legacyBackground.enabled = false;
            var lobbySelection = legacySelection ? FindDeep(legacySelection, "SelectionUI")?.GetComponent<CanvasGroup>() : null;
            if (lobbySelection == null) Debug.LogWarning("DASH: SelectionUI CanvasGroup not found; dashboard won't auto-hide in matches.");

            var dashCg = GetOrAdd<CanvasGroup>(dash.transform);
            dashCg.alpha = 1f; dashCg.interactable = true; dashCg.blocksRaycasts = true;

            var playWin = CardButton(FindDeep(content, "PlayWinButton"), null);
            var ai = CardButton(FindDeep(content, "AI_Card"), "Background");
            var random = CardButton(FindDeep(content, "RandomPlayers_Card"), "Background");
            var competitive = CardButton(FindDeep(content, "Competitive_Card"), "Background");
            var block = CardButton(FindDeep(content, "Block_Card"), "Fill");
            var concentrate = CardButton(FindDeep(content, "Concentrate_Card"), "Fill");

            var overlay = BuildMatchmakingOverlay(dash.transform, out var title, out var details, out var timer, out var cancel);

            var type = FindType("ProDomino.Dashboard.DashboardController");
            var ctrl = dash.TryGetComponent(type, out var existing) ? existing : dash.AddComponent(type);
            var so = new SerializedObject(ctrl);
            so.FindProperty("dashboardCanvasGroup").objectReferenceValue = dashCg;
            so.FindProperty("gameModeConfig").objectReferenceValue = gameModeConfig;
            so.FindProperty("lobbySelectionCanvasGroup").objectReferenceValue = lobbySelection;
            so.FindProperty("legacyLobbyBackground").objectReferenceValue = legacyBackground;
            so.FindProperty("playAndWinButton").objectReferenceValue = playWin;
            so.FindProperty("aiMatchButton").objectReferenceValue = ai;
            so.FindProperty("randomPlayersButton").objectReferenceValue = random;
            so.FindProperty("competitiveButton").objectReferenceValue = competitive;
            so.FindProperty("blockButton").objectReferenceValue = block;
            so.FindProperty("concentrateButton").objectReferenceValue = concentrate;
            so.FindProperty("matchmakingOverlay").objectReferenceValue = overlay;
            so.FindProperty("matchmakingTitle").objectReferenceValue = title;
            so.FindProperty("matchmakingDetails").objectReferenceValue = details;
            so.FindProperty("matchmakingTimer").objectReferenceValue = timer;
            so.FindProperty("cancelMatchmakingButton").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();

            RouteLogoToDashboard(canvasRoot);
            Debug.Log("DASH: dashboard wired to GameModeConfig.");
        }

        // The ProDomino logo used to open the old QuickMatch panel (also the start-up panel). The
        // dashboard replaces it, so the logo now presses the sidebar "Dashboard" button instead.
        // With no "QuickMatch" button left, navigation starts on the first sidebar button (Dashboard).
        private static void RouteLogoToDashboard(Transform canvasRoot)
        {
            var logo = FindDeep(canvasRoot, "NavegationPanel_Logo");
            var playButton = FindDeep(canvasRoot, "NavegationPanel_Play_Button")?.GetComponent<Button>();
            if (logo == null || playButton == null) { Debug.LogWarning("DASH: logo or Dashboard button not found."); return; }

            var custom = logo.GetComponent("CustomButtonUI");
            if (custom != null)
            {
                var cso = new SerializedObject(custom);
                cso.FindProperty("toggleID").stringValue = string.Empty;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }
            Forward(logo.gameObject, playButton);
        }

        private static Button CardButton(Transform card, string tintChild)
        {
            if (card == null) return null;
            var button = GetOrAdd<Button>(card);
            var target = tintChild != null ? card.Find(tintChild)?.GetComponent<Graphic>() : card.GetComponent<Graphic>();
            button.targetGraphic = target ? target : card.GetComponent<Graphic>();
            button.transition = Selectable.Transition.ColorTint;
            var c = button.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.9f, 0.9f, 0.95f, 1f);
            c.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            c.fadeDuration = 0.08f;
            button.colors = c;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            var img = card.GetComponent<Image>();
            if (img) img.raycastTarget = true;
            return button;
        }

        private static CanvasGroup BuildMatchmakingOverlay(Transform dash, out TextMeshProUGUI title, out TextMeshProUGUI details,
            out TextMeshProUGUI timer, out Button cancel)
        {
            var old = dash.Find("Matchmaking_Overlay");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var overlay = MakeImage(dash, "Matchmaking_Overlay", null, new Color(0.004f, 0.004f, 0.047f, 0.78f), Image.Type.Simple);
            Stretch((RectTransform)overlay.transform);
            overlay.GetComponent<Image>().raycastTarget = true; // blocks the cards while searching
            var cg = overlay.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            var card = MakeImage(overlay.transform, "Card", roundedWhite, DropdownBg, Image.Type.Sliced);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(440f, 250f);

            title = MakeText(card.transform, "Title", "Finding a match…", fontBold, 26f, Color.white);
            title.alignment = TextAlignmentOptions.Center;
            CenterTop((RectTransform)title.transform, 32f, 400f, 34f);
            details = MakeText(card.transform, "Details", "Random players · 1 vs 1", fontMedium, 16f, Muted);
            details.alignment = TextAlignmentOptions.Center;
            CenterTop((RectTransform)details.transform, 74f, 400f, 22f);
            timer = MakeText(card.transform, "Timer", "00:00", fontSemiBold, 30f, Hex("#FDC653"));
            timer.alignment = TextAlignmentOptions.Center;
            CenterTop((RectTransform)timer.transform, 108f, 200f, 38f);

            var cancelGo = MakeImage(card.transform, "Cancel_Button", roundedWhite, Hex("#1C2233"), Image.Type.Sliced);
            CenterTop((RectTransform)cancelGo.transform, 176f, 180f, 46f);
            var cancelImg = cancelGo.GetComponent<Image>();
            cancelImg.raycastTarget = true;
            cancel = cancelGo.AddComponent<Button>();
            StyleButton(cancel, cancelImg);
            var label = MakeText(cancelGo.transform, "Label", "Cancel", fontSemiBold, 18f, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)label.transform);

            overlay.transform.SetAsLastSibling();
            return cg;
        }

        private static void CenterTop(RectTransform rt, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(w, h);
        }

        // The canvas stores old per-instance overrides on the header (positions, sprites, fonts)
        // that would hide the restyle. Only layout/visual components are reverted: script
        // components keep their overrides because they reference objects elsewhere in the canvas.
        private static readonly Type[] RevertibleTypes =
        {
            typeof(RectTransform), typeof(Image), typeof(TextMeshProUGUI), typeof(AspectRatioFitter),
            typeof(Mask), typeof(LayoutElement), typeof(ContentSizeFitter),
        };

        private static void RevertHeaderLayoutOverrides(Transform canvasRoot)
        {
            var ucc = FindDeep(canvasRoot, "UserControlCenter");
            if (ucc == null) { Debug.LogWarning("DASH: UserControlCenter not found in canvas."); return; }

            int reverted = 0;
            foreach (var chip in new[] { "PlayerBestRank_Scalable", "Tokens_Chip", "Bell_Chip", "Notifications_Scalable", "Nationality_Scalable", "Options_Scalable" })
            {
                var t = ucc.Find(chip);
                if (t == null) continue;
                foreach (var comp in t.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || !RevertibleTypes.Contains(comp.GetType())) continue;
                    if (!PrefabUtility.IsPartOfPrefabInstance(comp) || !HasOverrides(comp)) continue;
                    PrefabUtility.RevertObjectOverride(comp, InteractionMode.AutomatedAction);
                    reverted++;
                }
            }
            Debug.Log($"DASH: reverted canvas overrides on {reverted} header components.");
        }

        // The section builders place everything at fixed Figma pixel rects for a 1150 px column;
        // this re-anchors sections/cards so they stretch with the width while keeping heights.
        private static void MakeResponsive(Transform content)
        {
            var banner = Need(content, "ChallengeBanner");
            Row(banner, 0f, 360f);
            RoundCard(banner);
            Cover(Need(banner, "BackgroundImage"));
            var dots = (RectTransform)FindDeep(banner, "PaginationDots");
            dots.anchorMin = dots.anchorMax = new Vector2(0.5f, 0f);
            dots.pivot = new Vector2(0.5f, 0f);
            dots.anchoredPosition = new Vector2(0f, 18f);
            SetSprite(FindDeep(banner, "RewardChip"), rewardChipSprite);
            SetSprite(FindDeep(banner, "PlayWinButton"), roundedOrange);

            var stats = Need(content, "StatPillsRow");
            Row(stats, 384f, 113f);
            string[] pills = { "StatPill_GamesPlayedToday", "StatPill_UserPlayingNow", "StatPill_ActivePlayer" };
            for (int i = 0; i < pills.Length; i++)
            {
                var pill = Need(stats, pills[i]);
                Col(pill, i / 3f, (i + 1) / 3f, 16f * i / 3f, -16f * (2 - i) / 3f);
                RoundCard(pill);
                SetSprite(FindDeep(pill, "AvatarPill"), roundedWhite);
                SetSprite(FindDeep(pill, "OnlineDot"), circleSprite, Image.Type.Simple);
            }

            var quick = Need(content, "QuickMatchSection");
            Row(quick, 521f, 463f);
            Row(Need(quick, "SectionHeader"), 0f, 29f);
            var quickCards = Need(quick, "CardsRow");
            Row(quickCards, 45f, 418f);
            var left = Need(quickCards, "LeftColumn");
            Col(left, 0f, 0.5f, 0f, -8f);
            foreach (var (card, top) in new[] { ("AI_Card", 0f), ("RandomPlayers_Card", 217f) })
            {
                var c = Need(left, card);
                Row(c, top, 201f);
                RoundCard(c);
                Cover(Need(c, "Background"));
                var badge = c.Find("Badge");
                if (badge) MiddleRight((RectTransform)badge, 6f, 209f, 209f);
            }
            var competitive = Need(quickCards, "Competitive_Card");
            Col(competitive, 0.5f, 1f, 8f, 0f);
            RoundCard(competitive);
            Cover(Need(competitive, "Background"));
            var trophy = (RectTransform)Need(competitive, "Trophy");
            trophy.anchorMin = trophy.anchorMax = new Vector2(0.5f, 1f);
            trophy.pivot = new Vector2(0.5f, 1f);
            trophy.anchoredPosition = new Vector2(31f, -102f);

            var games = Need(content, "PlayGamesSection");
            Row(games, 1008f, 298f);
            Row(Need(games, "SectionHeader"), 0f, 29f);
            var gameCards = Need(games, "CardsRow");
            Row(gameCards, 45f, 253f);
            foreach (var (card, a0, a1, l, r) in new[] { ("Block_Card", 0f, 0.5f, 0f, -8f), ("Concentrate_Card", 0.5f, 1f, 8f, 0f) })
            {
                var c = Need(gameCards, card);
                Col(c, a0, a1, l, r);
                RoundCard(c);
                MiddleRight((RectTransform)Need(c, "Illustration"), 17f, 230f, 230f);
                var title = Need(c, "Title");
                ((RectTransform)title).sizeDelta = new Vector2(480f, ((RectTransform)title).sizeDelta.y);
                title.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        // Rounded corners via a mask; a gradient/sprite fill moves to a child so it is clipped.
        private static void RoundCard(Transform card)
        {
            var img = card.GetComponent<Image>();
            bool hasFill = img.sprite != null;
            if (hasFill)
            {
                var fill = MakeImage(card, "Fill", img.sprite, img.color, Image.Type.Simple);
                Stretch((RectTransform)fill.transform);
                fill.transform.SetAsFirstSibling();
                img.color = Color.white;
            }
            img.sprite = roundedWhite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            GetOrAdd<Mask>(card).showMaskGraphic = !hasFill;
        }

        // CSS "cover": keeps the art's aspect ratio and crops the overflow.
        private static void Cover(Transform t)
        {
            var img = t.GetComponent<Image>();
            if (img == null || img.sprite == null) return;
            img.preserveAspect = false;
            var rt = (RectTransform)t;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var fit = GetOrAdd<AspectRatioFitter>(t);
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = img.sprite.rect.width / img.sprite.rect.height;
        }

        // ================================================================== helpers

        private static void Row(Transform t, float top, float height)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(top + height));
            rt.offsetMax = new Vector2(0f, -top);
        }

        private static void Col(Transform t, float a0, float a1, float left, float right)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(a0, 0f); rt.anchorMax = new Vector2(a1, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(right, 0f);
        }

        private static void TopRight(RectTransform rt, float fromRight, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-fromRight, -ChipTop);
            rt.sizeDelta = new Vector2(width, ChipHeight);
        }

        private static void Arrow(Transform parent, string name, float fromRight, float size)
        {
            var t = parent.Find(name);
            var go = t ? t.gameObject : MakeImage(parent, name, chevronSprite, Color.white, Image.Type.Simple);
            var img = go.GetComponent<Image>();
            img.sprite = chevronSprite; img.color = Color.white; img.raycastTarget = false; img.preserveAspect = true;
            MiddleRight((RectTransform)go.transform, fromRight, size, size);
            go.transform.SetAsLastSibling();
        }

        private static void StyleButton(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            var c = button.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.88f, 0.9f, 1f, 1f);
            c.pressedColor = new Color(0.75f, 0.77f, 0.85f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            c.fadeDuration = 0.08f;
            button.colors = c;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        }

        // White "v" chevron for dropdown affordances.
        private static Sprite MakeChevron(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var a = new Vector2(size * 0.22f, size * 0.66f);
            var b = new Vector2(size * 0.5f, size * 0.36f);
            var c = new Vector2(size * 0.78f, size * 0.66f);
            float half = size * 0.055f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(half - d + 0.5f)));
            }
            return SaveSprite($"{GeneratedDir}/{name}.png", tex);
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + t * ab);
        }

        // Makes a chip background behave like a click on the button it wraps.
        private static void Forward(GameObject from, Button target)
        {
            var comp = AddByName(from.transform, "ProDomino.Shared.ForwardClick");
            if (!comp) return;
            var so = new SerializedObject(comp);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
