using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    // Restyles the left sidebar of ProDomino_MainCanvas to the new Figma design (node 226-23039):
    // navy rounded panel, 190x44 rows, orange gradient active row, grouped sections,
    // Settings/Help block and the Invite Friends card. It then renders the canvas to PNG so the
    // result can be checked without opening the Editor. Safe to run more than once.
    internal static class SidebarRestyler
    {
        private const string NavButtonPath = "Assets/_ProDomino/NavigationSystem/Prefabs/NavegationPanel_Button.prefab";
        private const string CanvasPath = "Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab";
        private const string PartyEntryPath = "Assets/_ProDomino/FriendSystem/Prefabs/Party_User_DirectAccess.prefab";




        private static readonly Color PageBg = PdUiKit.PageBg;
        private static readonly Color PanelBg = PdUiKit.PanelBg;
        private static readonly Color GroupBg = Hex("#01010C");
        private static readonly Color TextInactive = TextMuted;
        private static readonly Color TextActive = OnAccent;
        private static readonly Color DividerColor = Divider;



        private const float IconLeft = 20f;
        private const float TextLeft = 50f;
        private const long RulesKeyId = 38;

        // Nav button instance name -> new icon file.
        private static readonly (string instance, string icon)[] NavIcons =
        {
            ("NavegationPanel_Play_Button", "Nav_Dashboard"),
            ("NavegationPanel_Leaderboard_Button", "Nav_Leaderboard"),
            ("NavegationPanel_Achievements_Button", "Nav_Achievements"),
            ("NavegationPanel_Club_Button", "Nav_Club"),
            ("NavegationPanel_Party", "Nav_Party"),
            ("NavegationPanel_Tournament_Button", "Nav_Tournament"),
            ("NavegationPanel_Shop_Button", "Nav_Shop"),
            ("NavegationPanel_Learn_Button", "Nav_Rules"),
            ("NavegationPanel_Review_Button", "Nav_Review"),
        };

        internal static Sprite roundedWhite, roundedOrange, roundedBlue, glowOrange;
        internal static TMP_FontAsset fontRegular, fontMedium, fontBold, fontExtraBold;

        [MenuItem("ProDomino/Dashboard/Restyle Sidebar + Render")]
        public static void ApplyAndRender()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();
            RestyleNavButtonPrefab();
            RestylePartyEntryPrefab();
            RestyleSidebarInCanvas();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RenderOnly();
            Debug.Log("SIDEBAR_RESTYLE_DONE");
        }

        [MenuItem("ProDomino/Dashboard/Render Main Canvas To PNG")]
        public static void RenderOnly()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "pd_renders");
            Directory.CreateDirectory(outDir);
            RenderCanvas(Path.Combine(outDir, "runtime.png"), 1920, 1080, true);
            RenderCanvas(Path.Combine(outDir, "editmode.png"), 1920, 1080, false);
            RenderCanvas(Path.Combine(outDir, "scrolled.png"), 1920, 1080, true, root =>
            {
                var scroll = FindDeep(root.transform, "Dashboard_Content")?.GetComponent<ScrollRect>();
                if (scroll) scroll.content.anchoredPosition = new Vector2(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            });
            RenderCanvas(Path.Combine(outDir, "dropdowns.png"), 1920, 1080, true, root =>
            {
                foreach (var n in new[] { "Options_Container", "PlayerBestRank_ModeDropdown" })
                    if (FindDeep(root.transform, n)?.GetComponent<CanvasGroup>() is CanvasGroup cg) cg.alpha = 1f;
                var item = FindDeep(root.transform, "ModeItem_Template");
                if (item)
                {
                    item.gameObject.SetActive(true);
                    UnityEngine.Object.Instantiate(item.gameObject, item.parent, false);
                    UnityEngine.Object.Instantiate(item.gameObject, item.parent, false);
                }
            });
            RenderCanvas(Path.Combine(outDir, "matchmaking.png"), 1920, 1080, true, root =>
            {
                if (FindDeep(root.transform, "Matchmaking_Overlay")?.GetComponent<CanvasGroup>() is CanvasGroup cg) cg.alpha = 1f;
            });
            Debug.Log("RENDER_DONE");
        }

        // ------------------------------------------------------------------ assets

        internal static void PrepareAssets()
        {
            roundedWhite = MakeRoundedSprite("Rounded_White_R10", 32, 32, 10, Color.white, Color.white);
            roundedOrange = MakeRoundedSprite("Rounded_Orange_R10", 190, 44, 10, Hex("#FFA501"), Hex("#FDC653"));
            roundedBlue = MakeRoundedSprite("Rounded_Blue_R10", 72, 28, 10, Hex("#416FC3"), Hex("#78ADFF"));
            glowOrange = MakeGlowSprite("Glow_Orange", 128, 64, Hex("#FF9A00"));
            fontRegular = LoadFont("Montserrat-Regular");
            fontMedium = LoadFont("Montserrat-Medium");
            fontBold = LoadFont("Montserrat-Bold");
            fontExtraBold = LoadFont("Montserrat-ExtraBold");
            Require(roundedWhite, "Rounded_White_R10");
            Require(roundedOrange, "Rounded_Orange_R10");
            Require(roundedBlue, "Rounded_Blue_R10");
            Require(glowOrange, "Glow_Orange");
        }

        // ------------------------------------------------------------------ nav button prefab

        private static void RestyleNavButtonPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(NavButtonPath);
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(190f, RowHeight);
                var le = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
                le.minHeight = RowHeight;
                le.preferredHeight = RowHeight;
                StyleNavButton(root.transform, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/Nav_Dashboard.png"));
                PrefabUtility.SaveAsPrefabAsset(root, NavButtonPath);
                Debug.Log("SIDEBAR: NavegationPanel_Button prefab restyled.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Applied to the base prefab AND re-applied to every instance, so older per-instance
        // overrides (Tournament's custom anchors, Learn's visible indicator, Party's white icon)
        // can't leave one row looking different from the rest.
        private static void StyleNavButton(Transform button, Sprite icon)
        {
            var toggleInd = Need(button, "NPButton_ToggleIndicator");
            var hoverInd = Need(button, "NPButton_Hoverindicator");
            var toggleGfx = Need(toggleInd, "NPButton_ToggleGraphic");
            var hoverGfx = Need(hoverInd, "NPButton_HoverGraphic");
            var iconT = Need(button, "NPButton_Icon");
            var textT = Need(button, "NPButton_Text (TMP)");

            Stretch((RectTransform)toggleInd);
            Stretch((RectTransform)hoverInd);
            Stretch((RectTransform)toggleGfx);
            Stretch((RectTransform)hoverGfx);
            // CustomButtonUI hides both at runtime anyway; hiding them in the asset too makes the
            // Edit-mode view match the deselected runtime look instead of stacking every state.
            toggleInd.GetComponent<CanvasGroup>().alpha = 0f;
            hoverInd.GetComponent<CanvasGroup>().alpha = 0f;

            var tg = toggleGfx.GetComponent<Image>();
            tg.sprite = roundedOrange; tg.type = Image.Type.Sliced; tg.preserveAspect = false;
            tg.pixelsPerUnitMultiplier = 1f; tg.color = Color.white; tg.raycastTarget = false;

            var hg = hoverGfx.GetComponent<Image>();
            hg.sprite = roundedWhite; hg.type = Image.Type.Sliced; hg.preserveAspect = false;
            hg.pixelsPerUnitMultiplier = 1f; hg.color = new Color(1f, 1f, 1f, 0.06f); hg.raycastTarget = false;

            var iconRt = (RectTransform)iconT;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(IconLeft, 0f);
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            var fitter = iconT.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;
            var iconImg = iconT.GetComponent<Image>();
            if (icon != null) iconImg.sprite = icon;
            iconImg.type = Image.Type.Simple;
            iconImg.color = TextInactive;
            iconImg.preserveAspect = true;

            StyleLabel(textT, fontMedium, 16f, TextInactive, TextLeft);

            var cb = button.GetComponent("CustomButtonUI");
            if (cb == null) return;
            var so = new SerializedObject(cb);
            so.FindProperty("selectedColor").colorValue = TextActive;
            so.FindProperty("deselectedColor").colorValue = TextInactive;
            so.FindProperty("hoverColor").colorValue = Color.white;
            var states = so.FindProperty("imageStates");
            for (int i = 0; i < states.arraySize; i++)
            {
                var e = states.GetArrayElementAtIndex(i);
                var img = e.FindPropertyRelative("image").objectReferenceValue as Image;
                if (img == null) continue;
                if (img.transform == iconT)
                {
                    e.FindPropertyRelative("selectedStateColor").colorValue = TextActive;
                    e.FindPropertyRelative("deselectedStateColor").colorValue = TextInactive;
                    e.FindPropertyRelative("hoverStateColor").colorValue = Color.white;
                }
                else if (img.transform == toggleGfx)
                {
                    e.FindPropertyRelative("selectedStateColor").colorValue = Color.white;
                    e.FindPropertyRelative("deselectedStateColor").colorValue = new Color(1f, 1f, 1f, 0f);
                    e.FindPropertyRelative("hoverStateColor").colorValue = new Color(1f, 1f, 1f, 0f);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Party member rows shown under the invite card while in a party.
        private static void RestylePartyEntryPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PartyEntryPath);
            try
            {
                var bgImg = Need(root.transform, "Background").GetComponent<Image>();
                bgImg.sprite = roundedWhite; bgImg.type = Image.Type.Sliced; bgImg.color = GroupBg;
                bgImg.pixelsPerUnitMultiplier = 1f;

                var user = Need(root.transform, "Username_Text (TMP)").GetComponent<TextMeshProUGUI>();
                user.font = fontBold; user.fontSharedMaterial = fontBold.material; user.color = Color.white;
                user.fontStyle = FontStyles.Normal;

                var id = Need(root.transform, "ID_Text (TMP)").GetComponent<TextMeshProUGUI>();
                id.font = fontRegular; id.fontSharedMaterial = fontRegular.material; id.color = TextInactive;
                id.fontStyle = FontStyles.Normal;

                PrefabUtility.SaveAsPrefabAsset(root, PartyEntryPath);
                Debug.Log("SIDEBAR: Party_User_DirectAccess prefab restyled.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static TextMeshProUGUI StyleLabel(Transform textT, TMP_FontAsset font, float size, Color color, float left)
        {
            var rt = (RectTransform)textT;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);
            var tmp = textT.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = size;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.lineSpacing = 0f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.margin = Vector4.zero;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ------------------------------------------------------------------ canvas sidebar

        private static void RestyleSidebarInCanvas()
        {
            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                var bg = Need(root.transform, "Background");
                var baseFrame = Need(bg, "Main_Menu_Base");
                var navCtrl = Need(bg, "NavegationPanelController");
                var layout = Need(navCtrl, "NavegationPanel_LayoutGroup");
                var lower = Need(bg, "LowerScreen");

                // The old gray L-shaped frame becomes a flat page background like the reference.
                var bgImg = bg.GetComponent<Image>();
                if (bgImg != null) bgImg.color = PageBg;
                baseFrame.GetComponent<Image>().color = PageBg;

                BuildSidebarPanel(bg, baseFrame, ((RectTransform)lower).anchorMin.y);
                RestyleNavList(layout);
                StyleLowerScreen(lower);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
                Debug.Log("SIDEBAR: canvas saved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Background is taller than the screen (its bottom sits below the visible area), so the
        // panel's bottom follows LowerScreen, whose bottom edge is the bottom of the screen.
        private static void BuildSidebarPanel(Transform bg, Transform baseFrame, float bottomAnchorY)
        {
            foreach (var n in new[] { "Sidebar_Panel", "Sidebar_GlowTop", "Sidebar_GlowBottom" })
            {
                var old = bg.Find(n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            // Same horizontal band as NavegationPanelController (0 .. 0.128 of the width).
            const float panelAnchorX = 0.1280724f;
            const float panelAnchorTop = 0.9085986f;

            // Glows sit behind the panel so only a soft orange halo shows past its edges.
            var glowTop = MakeImage(bg, "Sidebar_GlowTop", glowOrange, new Color(1f, 1f, 1f, 0.35f), Image.Type.Simple);
            var gtr = (RectTransform)glowTop.transform;
            gtr.anchorMin = gtr.anchorMax = new Vector2(0f, panelAnchorTop);
            gtr.pivot = new Vector2(0.5f, 0.5f);
            gtr.anchoredPosition = new Vector2(24f, -8f);
            gtr.sizeDelta = new Vector2(150f, 34f);

            var glowBottom = MakeImage(bg, "Sidebar_GlowBottom", glowOrange, new Color(1f, 1f, 1f, 0.45f), Image.Type.Simple);
            var gbr = (RectTransform)glowBottom.transform;
            gbr.anchorMin = gbr.anchorMax = new Vector2(panelAnchorX * 0.5f, bottomAnchorY);
            gbr.pivot = new Vector2(0.5f, 0.5f);
            gbr.anchoredPosition = new Vector2(0f, 2f);
            gbr.sizeDelta = new Vector2(220f, 24f);

            var panel = MakeImage(bg, "Sidebar_Panel", roundedWhite, PanelBg, Image.Type.Sliced);
            var pr = (RectTransform)panel.transform;
            pr.anchorMin = new Vector2(0f, bottomAnchorY);
            pr.anchorMax = new Vector2(panelAnchorX, panelAnchorTop);
            pr.offsetMin = new Vector2(8f, 4f);
            pr.offsetMax = new Vector2(-8f, -2f);

            int i = baseFrame.GetSiblingIndex();
            glowTop.transform.SetSiblingIndex(i + 1);
            glowBottom.transform.SetSiblingIndex(i + 2);
            panel.transform.SetSiblingIndex(i + 3);
        }

        private static void RestyleNavList(Transform layout)
        {
            // Undo groups from a previous run so the steps below always start from the original list.
            foreach (var g in new[] { "Group_Top", "Group_A", "Group_B" })
            {
                var old = layout.Find(g);
                if (old == null) continue;
                while (old.childCount > 0) old.GetChild(0).SetParent(layout, false);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var oldDivider = layout.Find("Divider_Sections_1");
            if (oldDivider != null) UnityEngine.Object.DestroyImmediate(oldDivider.gameObject);

            // Rows 190 wide: the layout group is 207 wide, so 8/9 px side padding.
            var vlg = layout.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(8, 9, 16, 0);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            foreach (var (inst, iconName) in NavIcons)
            {
                var t = FindDeep(layout, inst);
                if (t == null) { Debug.LogWarning($"SIDEBAR: nav button '{inst}' not found."); continue; }
                StyleNavButton(t, AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{iconName}.png"));
            }

            // Labels: "Play" -> "Dashboard" (its localization entry was updated too) and the Learn
            // button points at the existing "rules" entry.
            SetLabel(FindDeep(layout, "NavegationPanel_Play_Button"), "Dashboard", -1);
            SetLabel(FindDeep(layout, "NavegationPanel_Learn_Button"), "Rules", RulesKeyId);

            // Dashboard sits in its own row-sized container (like "Games" in the reference).
            var groupTop = MakeGroup(layout, "Group_Top", 0);
            var groupA = MakeGroup(layout, "Group_A", 8);
            var groupB = MakeGroup(layout, "Group_B", 8);
            MoveInto(layout, groupTop, "NavegationPanel_Play_Button");
            MoveInto(layout, groupA, "NavegationPanel_Leaderboard_Button", "NavegationPanel_Achievements_Button",
                "NavegationPanel_Club_Button", "NavegationPanel_Party", "NavegationPanel_Tournament_Button");
            MoveInto(layout, groupB, "NavegationPanel_Shop_Button", "NavegationPanel_Learn_Button",
                "NavegationPanel_Review_Button");

            var separator = Need(layout, "SeparatorInLayoutGroup");
            StyleSeparator(separator);
            var divider1 = UnityEngine.Object.Instantiate(separator.gameObject, layout, false);
            divider1.name = "Divider_Sections_1";

            string[] order =
            {
                "Group_Top", "Group_A", "Divider_Sections_1", "Group_B", "SeparatorInLayoutGroup",
                "InviteFriend_Container", "Party_LayoutGroup",
            };
            for (int i = 0; i < order.Length; i++)
            {
                var c = layout.Find(order[i]);
                if (c != null) c.SetSiblingIndex(i);
                else Debug.LogWarning($"SIDEBAR: expected '{order[i]}' under layout group.");
            }

            StyleInviteCard(layout);

            // Keeps a full 4-member party list above Settings/Help.
            var party = layout.Find("Party_LayoutGroup");
            if (party != null && party.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup pv) pv.spacing = 5f;
        }

        private static void SetLabel(Transform button, string text, long keyId)
        {
            if (button == null) return;
            var label = Need(button, "NPButton_Text (TMP)");
            label.GetComponent<TextMeshProUGUI>().text = text;
            var lse = label.GetComponent("LocalizeStringEvent");
            if (lse == null || keyId < 0) return;
            var so = new SerializedObject(lse);
            var key = so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId");
            if (key == null) { Debug.LogWarning("SIDEBAR: LocalizeStringEvent key property not found."); return; }
            key.longValue = keyId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject MakeGroup(Transform parent, string name, int verticalPadding)
        {
            var go = MakeImage(parent, name, roundedWhite, GroupBg, Image.Type.Sliced);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, verticalPadding, verticalPadding);
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return go;
        }

        private static void MoveInto(Transform layout, GameObject group, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeep(layout, n);
                if (t == null) { Debug.LogWarning($"SIDEBAR: '{n}' not found to group."); continue; }
                t.SetParent(group.transform, false);
                t.SetAsLastSibling();
            }
        }

        private static void StyleSeparator(Transform separator)
        {
            var le = separator.GetComponent<LayoutElement>() ?? separator.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 1f;
            le.preferredHeight = 1f;
            var line = separator.childCount > 0 ? separator.GetChild(0) : null;
            if (line == null) return;
            var fit = line.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.enabled = false;
            Stretch((RectTransform)line);
            var img = line.GetComponent<Image>();
            img.sprite = null; img.type = Image.Type.Simple; img.color = DividerColor;
        }

        // Figma "Invite Friends" card: title, subtitle and a small blue "Invite" button.
        private static void StyleInviteCard(Transform layout)
        {
            var container = FindDeep(layout, "InviteFriend_Container");
            var btn = container != null ? container.Find("InviteFriend_Button") : null;
            if (btn == null) { Debug.LogWarning("SIDEBAR: InviteFriend_Button not found."); return; }

            var le = container.GetComponent<LayoutElement>() ?? container.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 100f;
            le.preferredHeight = 100f;
            var card = container.GetComponent<Image>() ?? container.gameObject.AddComponent<Image>();
            card.sprite = roundedWhite; card.type = Image.Type.Sliced; card.color = GroupBg;
            card.raycastTarget = false; card.pixelsPerUnitMultiplier = 1f;

            foreach (var n in new[] { "Invite_Title", "Invite_Subtitle" })
            {
                var old = container.Find(n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var title = MakeText(container, "Invite_Title", "Invite Friends", fontExtraBold, 17f, Color.white);
            PlaceTopLeft((RectTransform)title.transform, 14f, -10f, 170f, 22f);
            var sub = MakeText(container, "Invite_Subtitle", "Invite your friends and earn bonus", fontRegular, 11f, TextInactive);
            sub.textWrappingMode = TextWrappingModes.Normal;
            sub.alignment = TextAlignmentOptions.TopLeft;
            PlaceTopLeft((RectTransform)sub.transform, 14f, -34f, 160f, 28f);

            var brt = (RectTransform)btn;
            brt.anchorMin = brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = new Vector2(14f, 10f);
            brt.sizeDelta = new Vector2(76f, 28f);
            btn.SetAsLastSibling();

            var bgImg = Need(btn, "Background").GetComponent<Image>();
            bgImg.sprite = roundedBlue; bgImg.type = Image.Type.Sliced; bgImg.color = Color.white;
            bgImg.pixelsPerUnitMultiplier = 1f;

            var label = Need(btn, "NPButton_Text (TMP)");
            var lrt = (RectTransform)label;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "Invite";
            tmp.font = fontBold; tmp.fontSharedMaterial = fontBold.material;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false; tmp.fontSize = 12f; tmp.fontStyle = FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.margin = Vector4.zero; tmp.lineSpacing = 0f;
            // The old "Invite a Friend +" string would overwrite the new short label at runtime.
            if (label.GetComponent("LocalizeStringEvent") is Behaviour lse) lse.enabled = false;
        }

        private static void StyleLowerScreen(Transform lower)
        {
            // LowerScreen spans the same 0..0.129 band; 190 px rows start 27.5 px in.
            var divider = lower.Find("Divider_Image");
            if (divider != null)
            {
                var drt = (RectTransform)divider;
                drt.anchorMin = new Vector2(0.111f, 1f);
                drt.anchorMax = new Vector2(0.877f, 1f);
                drt.pivot = new Vector2(0.5f, 1f);
                drt.anchoredPosition = Vector2.zero;
                drt.sizeDelta = new Vector2(0f, 1f);
                var dimg = divider.GetComponent<Image>();
                dimg.sprite = null; dimg.type = Image.Type.Simple; dimg.color = DividerColor;
            }

            var options = Need(lower, "Options");
            var ort = (RectTransform)options;
            ort.anchorMin = new Vector2(0.111f, 0f);
            ort.anchorMax = new Vector2(0.877f, 1f);
            ort.offsetMin = new Vector2(0f, 14f);
            ort.offsetMax = new Vector2(0f, -8f);
            var optImg = options.GetComponent<Image>() ?? options.gameObject.AddComponent<Image>();
            optImg.sprite = roundedWhite; optImg.type = Image.Type.Sliced; optImg.color = GroupBg;
            optImg.raycastTarget = false; optImg.pixelsPerUnitMultiplier = 1f;

            StyleOption(options, "Settings_Option", "Settings_Image", "Nav_Settings", true);
            StyleOption(options, "Help_Option", "Help_Image", "Nav_Help", false);
        }

        private static void StyleOption(Transform options, string optName, string iconName, string sprite, bool top)
        {
            var opt = Need(options, optName);
            var rt = (RectTransform)opt;
            rt.anchorMin = new Vector2(0f, top ? 0.5f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0.5f);
            rt.offsetMin = new Vector2(0f, top ? 0f : 4f);
            rt.offsetMax = new Vector2(0f, top ? -4f : 0f);

            var icon = Need(opt, iconName);
            var irt = (RectTransform)icon;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(IconLeft, 0f);
            irt.sizeDelta = new Vector2(IconSize, IconSize);
            var fit = icon.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.enabled = false;
            var img = icon.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{sprite}.png");
            img.color = TextInactive; img.preserveAspect = true;

            StyleLabel(Need(opt, "Text (TMP)"), fontMedium, 16f, TextInactive, TextLeft);

            var cb = opt.GetComponent("CustomButtonUI");
            if (cb == null) return;
            var so = new SerializedObject(cb);
            so.FindProperty("selectedColor").colorValue = Color.white;
            so.FindProperty("deselectedColor").colorValue = TextInactive;
            so.FindProperty("hoverColor").colorValue = Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ render

        // Renders the canvas as it appears inside a scene (with the scene's own overrides). The scene
        // is opened but never saved.
        internal static void RenderSceneCanvas(string scenePath, string canvasAssetPath, string outPath, bool simulateRuntime)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var inst = HeaderDashboardRestyler.FindCanvasInstance(scene);
            if (inst == null) { Debug.LogWarning($"RENDER: canvas instance not found in {scenePath}"); return; }
            RenderCanvas(outPath, 1920, 1080, simulateRuntime, null, inst);
        }

        internal static void RenderCanvas(string outPath, int width, int height, bool simulateRuntime, Action<GameObject> tweak = null, GameObject existingInstance = null)
        {
            if (existingInstance == null)
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

            var inst = existingInstance ? existingInstance : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath));
            cam.cullingMask = 1 << inst.layer;
            var canvas = inst.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            if (simulateRuntime) SimulateRuntime(inst);

            for (int i = 0; i < 3; i++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var g in inst.GetComponentsInChildren<LayoutGroup>(true))
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
                if (i == 0) tweak?.Invoke(inst);
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

            if (simulateRuntime) return;
            var names = (Environment.GetEnvironmentVariable("PD_RECT_NAMES") ?? "Sidebar_Panel,UserControlCenter,InnerScreen,Dashboard_Content").Split(',');
            foreach (var n in names)
            {
                var t = FindDeep(inst.transform, n.Trim()) as RectTransform;
                if (t == null) { Debug.Log($"SCREENRECT: {n} (not found)"); continue; }
                var c = new Vector3[4];
                t.GetWorldCorners(c);
                var a = cam.WorldToScreenPoint(c[0]);
                var b = cam.WorldToScreenPoint(c[2]);
                // Screen y is flipped so the numbers read like the PNG (0 = top).
                Debug.Log($"SCREENRECT: {n} x={a.x:F0}..{b.x:F0} y={height - b.y:F0}..{height - a.y:F0} pivot={t.pivot} active={t.gameObject.activeInHierarchy} parent={t.parent?.name}");
            }
        }

        // Runs each CustomButtonUI's Awake (hides state graphics, applies deselected colours),
        // then shows the Dashboard button as selected: what the player sees on the dashboard.
        private static void SimulateRuntime(GameObject root)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CustomButtonUI")).FirstOrDefault(t => t != null);
            if (type == null) { Debug.LogWarning("RENDER: CustomButtonUI type not found."); return; }
            var awake = type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            var preview = type.GetMethod("PreviewVisualState", BindingFlags.Instance | BindingFlags.Public);
            var idProp = type.GetProperty("CustomButtonID");
            foreach (var b in root.GetComponentsInChildren(type, true))
            {
                try { awake.Invoke(b, null); }
                catch (Exception e) { Debug.LogWarning($"RENDER: Awake failed on {b.name}: {e.InnerException?.Message}"); }
            }
            foreach (var b in root.GetComponentsInChildren(type, true))
                if ((string)idProp.GetValue(b) == "Play") preview.Invoke(b, new object[] { true });

            // Navigation opens the Play panel (dashboard) and hides every other panel.
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var panelType = mb.GetType().GetInterfaces().FirstOrDefault(i => i.Name == "INavigationPanel");
                if (panelType == null) continue;
                var kind = panelType.GetProperty("NavigationPanelType")?.GetValue(mb)?.ToString();
                if (panelType.GetProperty("RootCanvasGroup")?.GetValue(mb) is CanvasGroup cg && cg)
                    cg.alpha = kind == "Play" ? 1f : 0f;
            }

            // Run the dashboard's own show/hide rule once, as it would on the first frame.
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "DashboardController") continue;
                try
                {
                    mb.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(mb, null);
                    var dcg = mb.GetComponent<CanvasGroup>();
                    Debug.Log($"RENDER: DashboardController ran; dashboard alpha={(dcg ? dcg.alpha : -1f)}");
                }
                catch (Exception e) { Debug.LogWarning($"RENDER: DashboardController.LateUpdate failed: {e.InnerException?.Message ?? e.Message}"); }
            }
        }

    }
}
