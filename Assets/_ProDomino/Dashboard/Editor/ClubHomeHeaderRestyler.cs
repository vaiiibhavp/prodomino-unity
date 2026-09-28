using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.Dashboard.Editor.PdUiKit;

namespace ProDomino.Dashboard.Editor
{
    /// <summary>
    /// Rebuilds the club home screen header (Club_HomeScreen_DataUI) to the reference design:
    /// club icon, name with status dot, slogan, the Edit Club / Roles &amp; Permissions shortcuts and
    /// the right-hand stat blocks. Only layout and styling are authored here; the data still comes
    /// from ClubHomeScreenDataUI.
    /// </summary>
    internal static class ClubHomeHeaderRestyler
    {
        // The screen the game actually shows lives as a plain copy inside ClubUI_NavPanel; the
        // stand-alone Club_Home_Screen prefab is an unlinked twin and is kept in sync.
        private const string NavPanelPrefabPath = "Assets/_ProDomino/Prefabs/UI/ClubUI_NavPanel.prefab";
        private const string ScreenPrefabPath = "Assets/_ProDomino/Prefabs/UI/Club_Home_Screen.prefab";
        private const string MiddleScreenPrefabPath = "Assets/_ProDomino/Shared/Prefabs/MiddleScreen_Scalable.prefab";
        private const string EditIconPath = "Assets/_ProDomino/_UI/Icons/Icons_Base_128/Club_Member_Edition_Icon.png";
        private const string RolesIconPath = "Assets/_ProDomino/_UI/Icons/Icons_Club/Base_Shield_1.png";

        // The header rect is anchored in fractions of the screen; these are the resulting pixels.
        private const float HeaderWidth = 1590f;
        private const float HeaderHeight = 115.5f;

        private const float PadLeft = 20f;
        private const float PadRight = 24f;
        private const float IconSize = 76f;
        private const float TextLeft = PadLeft + IconSize + 20f;

        private static readonly Color Subtle = Hex("#8A90A6");
        private static readonly Color LinkText = Hex("#C7CBD9");

        private static TMP_FontAsset fRegular, fMedium, fSemiBold, fBold;
        private static Sprite headerCardBg, editIcon, rolesIcon;

        [MenuItem("ProDomino/Dashboard/Restyle Club Home Header + Render")]
        public static void ApplyAndRender()
        {
            Apply();
            Render();
        }

        [MenuItem("ProDomino/Dashboard/Restyle Club Home Header")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            PrepareAssets();

            foreach (var path in new[] { NavPanelPrefabPath, ScreenPrefabPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    BuildHeader(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[ClubHomeHeader] Header rebuilt: " + path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            ClearMiddleScreenHeaderOverrides();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void PrepareAssets()
        {
            fRegular = LoadFont("Montserrat-Regular");
            fMedium = LoadFont("Montserrat-Medium");
            fSemiBold = LoadFont("Montserrat-SemiBold");
            fBold = LoadFont("Montserrat-Bold");

            headerCardBg = MakePanelSprite("ClubHeader_CardBg", 64, 64, 14,
                Hex("#0D1120"), Hex("#080C18"), Hex("#1E2538"), 1.2f, true);
            editIcon = AssetDatabase.LoadAssetAtPath<Sprite>(EditIconPath);
            rolesIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RolesIconPath);
        }

        // ------------------------------------------------------------------ header

        private static void BuildHeader(GameObject root)
        {
            var header = FindDeep(root.transform, "Club_HomeScreen_DataUI")
                         ?? throw new Exception("Club_HomeScreen_DataUI not found in " + ScreenPrefabPath);

            var headerImg = GetOrAdd<Image>(header);
            headerImg.sprite = headerCardBg;
            headerImg.type = Image.Type.Sliced;
            headerImg.color = Color.white;
            headerImg.pixelsPerUnitMultiplier = 1f;

            // Deep lookups: after the first run these sit inside the layout containers.
            var nameContainer = FindDeep(header, "Club_Name_Container")
                                ?? throw new Exception("Club_Name_Container not found under Club_HomeScreen_DataUI.");
            var slogan = FindDeep(header, "Body_Text (TMP) (1)")
                         ?? throw new Exception("Body_Text (TMP) (1) not found under Club_HomeScreen_DataUI.");
            // After the first run the rank text lives inside the stat row, so it is searched deep.
            var rankText = FindDeep(header, "Total_Achievements_Text (TMP) (1)")
                           ?? throw new Exception("Total_Achievements_Text (TMP) (1) not found under Club_HomeScreen_DataUI.");
            var icon = FindDeep(header, "Club_Image")
                       ?? header.Cast<Transform>()
                           .FirstOrDefault(t => t != nameContainer && t != slogan && t != rankText &&
                                                t.name != "Header_Links" && t.name != "Header_Stats" &&
                                                t.name != "Header_Body");

            // The scene and MiddleScreen_Scalable carry stale RectTransform overrides on these
            // children (Club_Image height 0, slogan at y 0). Driving them from layout groups makes
            // the stored positions and sizes irrelevant: the layout rewrites them every rebuild.
            var body = GetOrCreate(header, "Header_Body",
                () => new GameObject("Header_Body", typeof(RectTransform)).transform);
            Reparent(body, header);
            body.gameObject.layer = header.gameObject.layer;
            Stretch((RectTransform)body);
            var bodyLayout = GetOrAdd<HorizontalLayoutGroup>(body);
            bodyLayout.enabled = true;
            bodyLayout.padding = new RectOffset((int)PadLeft, (int)PadRight, 0, 0);
            bodyLayout.spacing = 20f;
            bodyLayout.childAlignment = TextAnchor.MiddleLeft;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = false;

            var textColumn = GetOrCreate(body, "Header_TextColumn",
                () => new GameObject("Header_TextColumn", typeof(RectTransform)).transform);
            Reparent(textColumn, body);
            textColumn.gameObject.layer = header.gameObject.layer;
            var columnLayout = GetOrAdd<VerticalLayoutGroup>(textColumn);
            columnLayout.enabled = true;
            columnLayout.padding = new RectOffset(0, 0, 0, 0);
            columnLayout.spacing = 4f;
            columnLayout.childAlignment = TextAnchor.MiddleLeft;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            SetLayoutElement(textColumn, -1f, 84f, flexibleWidth: 1f);

            // ---- club icon: square, vertically centred on the left ----
            if (icon != null)
            {
                DisableFitter(icon);
                Reparent(icon, body);
                icon.SetSiblingIndex(0);
                icon.localScale = Vector3.one;
                SetLayoutElement(icon, IconSize, IconSize);
            }
            textColumn.SetSiblingIndex(1);

            // ---- club name + online dot ----
            Reparent(nameContainer, textColumn);
            nameContainer.SetSiblingIndex(0);
            SetLayoutElement(nameContainer, -1f, 34f);
            var nameLayout = GetOrAdd<HorizontalLayoutGroup>(nameContainer);
            nameLayout.enabled = true;
            nameLayout.padding = new RectOffset(0, 0, 0, 0);
            nameLayout.spacing = 10f;
            nameLayout.childAlignment = TextAnchor.MiddleLeft;
            nameLayout.childControlWidth = false;
            nameLayout.childControlHeight = false;
            nameLayout.childForceExpandWidth = false;
            nameLayout.childForceExpandHeight = false;

            var title = Need(nameContainer, "Club_Name_TItle_Text (TMP)").GetComponent<TextMeshProUGUI>();
            Text(title, null, fBold, 26f, Color.white, TextAlignmentOptions.MidlineLeft);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)title.transform).sizeDelta = new Vector2(0f, 34f);

            // ---- slogan ----
            Reparent(slogan, textColumn);
            slogan.SetSiblingIndex(1);
            SetLayoutElement(slogan, -1f, 20f);
            var sloganText = slogan.GetComponent<TextMeshProUGUI>();
            Text(sloganText, null, fRegular, 15f, Subtle, TextAlignmentOptions.MidlineLeft);
            sloganText.textWrappingMode = TextWrappingModes.NoWrap;
            sloganText.overflowMode = TextOverflowModes.Ellipsis;

            // ---- Edit Club / Roles & Permissions shortcuts ----
            // Deep search: a previous run may have left it under the text column.
            var links = FindDeep(header, "Header_Links")
                        ?? new GameObject("Header_Links", typeof(RectTransform)).transform;
            Reparent(links, textColumn);
            links.SetSiblingIndex(2);
            links.gameObject.layer = header.gameObject.layer;
            SetLayoutElement(links, -1f, 22f);
            var linkLayout = GetOrAdd<HorizontalLayoutGroup>(links);
            linkLayout.enabled = true;
            linkLayout.padding = new RectOffset(0, 0, 0, 0);
            linkLayout.spacing = 28f;
            linkLayout.childAlignment = TextAnchor.MiddleLeft;
            linkLayout.childControlWidth = false;
            linkLayout.childControlHeight = false;
            linkLayout.childForceExpandWidth = false;
            linkLayout.childForceExpandHeight = false;

            // Send_Button is the existing edit affordance next to the club name; it becomes the
            // "Edit Club" link so its Button (and anything that looks it up) is preserved.
            // On a re-run it already carries its new name.
            var sendButton = FindDeep(header, "Send_Button") ?? FindDeep(header, "EditClub_Button");
            if (sendButton != null)
            {
                Reparent(sendButton, links);
                sendButton.SetSiblingIndex(0);
                foreach (var f in sendButton.GetComponents<ContentSizeFitter>()) f.enabled = false;
            }

            // An earlier run may have created its own Edit link before Send_Button was reused.
            if (sendButton != null)
                foreach (var stale in links.Cast<Transform>()
                             .Where(c => c != sendButton && c.name == "EditClub_Button").ToList())
                    UnityEngine.Object.DestroyImmediate(stale.gameObject);

            var editButton = MakeLink(links, sendButton, "EditClub_Button", "Edit Club", editIcon, 104f);
            var rolesButton = MakeLink(links, null, "RolesPermissions_Button", "Roles & Permissions", rolesIcon, 178f);

            // ---- right-hand stat blocks ----
            var stats = FindDeep(header, "Header_Stats")
                        ?? new GameObject("Header_Stats", typeof(RectTransform)).transform;
            Reparent(stats, body);
            stats.SetSiblingIndex(2);
            stats.gameObject.layer = header.gameObject.layer;
            SetLayoutElement(stats, 520f, 64f);
            var statsLayout = GetOrAdd<HorizontalLayoutGroup>(stats);
            statsLayout.enabled = true;
            statsLayout.padding = new RectOffset(0, 0, 0, 0);
            statsLayout.spacing = 32f;
            statsLayout.childAlignment = TextAnchor.MiddleRight;
            statsLayout.childControlWidth = false;
            statsLayout.childControlHeight = false;
            statsLayout.childForceExpandWidth = false;
            statsLayout.childForceExpandHeight = false;

            var membersValue = MakeStat(stats, "Stat_TotalMembers", "0", "Total Members", 150f);
            var applicationsValue = MakeStat(stats, "Stat_Applications", "0", "Applications", 140f);

            // The rank keeps its original TMP (it is the one ClubHomeScreenDataUI writes to), moved
            // into the stat row with its own caption underneath.
            var rankBlock = MakeStatBlock(stats, "Stat_ClubRank", "Club Rank", 130f);
            var rankValue = rankText.GetComponent<TextMeshProUGUI>();
            Reparent(rankText, rankBlock);
            rankText.SetSiblingIndex(0);
            var rankRect = (RectTransform)rankText;
            rankRect.anchorMin = rankRect.anchorMax = rankRect.pivot = new Vector2(0.5f, 0.5f);
            StyleStatValue(rankValue);
            Slot(rankText, 130f, 34f);
            rankText.gameObject.SetActive(true);

            WireHeaderReferences(header, editButton, rolesButton, membersValue, applicationsValue, rankValue);

            foreach (var g in header.GetComponentsInChildren<LayoutGroup>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
        }

        // A layout group reads these instead of the child's stored RectTransform, so stale prefab
        // overrides on position and size no longer decide where anything sits.
        private static void SetLayoutElement(Transform t, float width, float height,
            float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            var le = GetOrAdd<LayoutElement>(t);
            le.enabled = true;
            le.ignoreLayout = false;
            le.minWidth = width; le.preferredWidth = width;
            le.minHeight = height; le.preferredHeight = height;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
        }

        // MiddleScreen_Scalable stores its own RectTransform overrides on the club header, which
        // would keep the old positions after the prefab is rebuilt; those are dropped here.
        private static void ClearMiddleScreenHeaderOverrides()
        {
            var navPanelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NavPanelPrefabPath);
            var headerSource = navPanelAsset ? FindDeep(navPanelAsset.transform, "Club_HomeScreen_DataUI") : null;
            if (headerSource == null)
            {
                Debug.LogWarning("[ClubHomeHeader] header not found in the nav panel asset; overrides left as-is.");
                return;
            }

            var headerObjects = new HashSet<UnityEngine.Object>();
            foreach (var t in headerSource.GetComponentsInChildren<Transform>(true))
            {
                headerObjects.Add(t.gameObject);
                foreach (var c in t.GetComponents<Component>())
                    if (c) headerObjects.Add(c);
            }

            var middle = PrefabUtility.LoadPrefabContents(MiddleScreenPrefabPath);
            try
            {
                var instanceRoot = middle.GetComponentsInChildren<Transform>(true)
                    .Select(t => t.gameObject)
                    .FirstOrDefault(go => PrefabUtility.IsAnyPrefabInstanceRoot(go) &&
                                          AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) == NavPanelPrefabPath);
                if (instanceRoot == null)
                {
                    Debug.LogWarning("[ClubHomeHeader] ClubUI_NavPanel instance not found in MiddleScreen_Scalable.");
                    return;
                }

                var mods = PrefabUtility.GetPropertyModifications(instanceRoot);
                if (mods == null) return;
                var kept = mods.Where(m => m.target == null || !headerObjects.Contains(m.target)).ToArray();
                if (kept.Length == mods.Length)
                {
                    Debug.Log("[ClubHomeHeader] No club header overrides on MiddleScreen_Scalable.");
                    return;
                }

                PrefabUtility.SetPropertyModifications(instanceRoot, kept);
                PrefabUtility.SaveAsPrefabAsset(middle, MiddleScreenPrefabPath);
                Debug.Log($"[ClubHomeHeader] Dropped {mods.Length - kept.Length} club-header overrides from MiddleScreen_Scalable.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(middle);
            }
        }

        // A text shortcut with a small leading icon, sized to its content. `existing` reuses a
        // GameObject that is already in the prefab instead of creating a new one.
        private static Button MakeLink(Transform parent, Transform existing, string name, string label, Sprite icon, float width)
        {
            var t = existing != null
                ? existing
                : GetOrCreate(parent, name, () => new GameObject(name, typeof(RectTransform)).transform);
            Reparent(t, parent);
            t.gameObject.name = name;
            t.gameObject.layer = parent.gameObject.layer;
            t.gameObject.SetActive(true);
            var rect = (RectTransform)t;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Slot(t, width, 22f);

            var bg = GetOrAdd<Image>(t);
            bg.sprite = null;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;

            var layout = GetOrAdd<HorizontalLayoutGroup>(t);
            layout.enabled = true;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconT = GetOrCreate(t, "Icon", () => new GameObject("Icon", typeof(RectTransform)).transform);
            Reparent(iconT, t);
            iconT.gameObject.layer = t.gameObject.layer;
            iconT.SetSiblingIndex(0);
            Slot(iconT, 14f, 14f);
            var iconImg = GetOrAdd<Image>(iconT);
            iconImg.sprite = icon;
            iconImg.color = LinkText;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelT = GetOrCreate(t, "Label", () => new GameObject("Label", typeof(RectTransform)).transform);
            Reparent(labelT, t);
            labelT.gameObject.layer = t.gameObject.layer;
            labelT.SetSiblingIndex(1);
            Slot(labelT, width - 20f, 20f);
            var labelTmp = GetOrAdd<TextMeshProUGUI>(labelT);
            Text(labelTmp, label, fSemiBold, 13f, LinkText, TextAlignmentOptions.MidlineLeft);
            labelTmp.fontStyle = FontStyles.Underline;
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            labelTmp.raycastTarget = false;

            var button = GetOrAdd<Button>(t);
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            return button;
        }

        // A right-hand stat: big value over a small caption.
        private static Transform MakeStatBlock(Transform parent, string name, string caption, float width)
        {
            var t = GetOrCreate(parent, name, () => new GameObject(name, typeof(RectTransform)).transform);
            Reparent(t, parent);
            t.gameObject.layer = parent.gameObject.layer;
            Slot(t, width, 60f);

            var layout = GetOrAdd<VerticalLayoutGroup>(t);
            layout.enabled = true;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var captionT = GetOrCreate(t, "Caption", () => new GameObject("Caption", typeof(RectTransform)).transform);
            Reparent(captionT, t);
            captionT.gameObject.layer = t.gameObject.layer;
            captionT.SetAsLastSibling();
            Slot(captionT, width, 18f);
            var captionTmp = GetOrAdd<TextMeshProUGUI>(captionT);
            Text(captionTmp, caption, fRegular, 13f, Subtle, TextAlignmentOptions.Midline);
            captionTmp.textWrappingMode = TextWrappingModes.NoWrap;
            captionTmp.raycastTarget = false;

            return t;
        }

        private static TextMeshProUGUI MakeStat(Transform parent, string name, string value, string caption, float width)
        {
            var block = MakeStatBlock(parent, name, caption, width);
            var valueT = GetOrCreate(block, "Value", () => new GameObject("Value", typeof(RectTransform)).transform);
            Reparent(valueT, block);
            valueT.gameObject.layer = block.gameObject.layer;
            valueT.SetSiblingIndex(0);
            Slot(valueT, width, 34f);

            var tmp = GetOrAdd<TextMeshProUGUI>(valueT);
            if (string.IsNullOrEmpty(tmp.text)) tmp.text = value;
            StyleStatValue(tmp);
            return tmp;
        }

        private static void StyleStatValue(TextMeshProUGUI tmp)
        {
            Text(tmp, null, fBold, 26f, Color.white, TextAlignmentOptions.Midline);
            // Values are filled at runtime and can be long ("Unranked"), so they shrink instead of
            // spilling over the neighbouring block.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = 26f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            if (tmp.TryGetComponent<ContentSizeFitter>(out var csf)) csf.enabled = false;

            // The authoring placeholder must stay one line or it covers the caption in the editor.
            if (tmp.text != null && tmp.text.Contains("\n"))
                tmp.text = tmp.text.Split('\n').Last().Trim();
        }

        // ClubHomeScreenDataUI is internal to another assembly, so its fields are assigned by name.
        private static void WireHeaderReferences(Transform header, Button editButton, Button rolesButton,
            TextMeshProUGUI membersValue, TextMeshProUGUI applicationsValue, TextMeshProUGUI rankValue)
        {
            var dataUi = header.GetComponents<MonoBehaviour>()
                .FirstOrDefault(m => m && m.GetType().Name == "ClubHomeScreenDataUI");
            if (dataUi == null)
            {
                Debug.LogWarning("[ClubHomeHeader] ClubHomeScreenDataUI component not found on the header.");
                return;
            }

            var so = new SerializedObject(dataUi);
            var assigned = new List<string>();
            foreach (var (field, value) in new (string, UnityEngine.Object)[]
                     {
                         ("editClubButton", editButton),
                         ("rolesAndPermissionsButton", rolesButton),
                         ("totalMembersText", membersValue),
                         ("applicationsText", applicationsValue),
                         ("clubRankText", rankValue),
                     })
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogWarning($"[ClubHomeHeader] field '{field}' not found on ClubHomeScreenDataUI (recompile?).");
                    continue;
                }
                prop.objectReferenceValue = value;
                assigned.Add(field);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[ClubHomeHeader] Assigned: " + string.Join(", ", assigned));
        }

        // ------------------------------------------------------------------ render

        [MenuItem("ProDomino/Dashboard/Render Club Home Header")]
        public static void Render()
        {
            var outDir = Environment.GetEnvironmentVariable("PD_RENDER_DIR");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "pd_renders");
            Directory.CreateDirectory(outDir);
            RenderPrefab(Path.Combine(outDir, "club_home_header.png"), 1920, 1080);
            Debug.Log("RENDER_DONE");
        }

        private static void RenderPrefab(string outPath, int width, int height)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var cam = new GameObject("RenderCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PageBg;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;

            var canvasGo = new GameObject("RenderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            cam.cullingMask = 1 << canvasGo.layer;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath), canvasGo.transform);
            inst.SetActive(true);
            if (inst.TryGetComponent<CanvasGroup>(out var cg)) { cg.alpha = 1f; cg.blocksRaycasts = true; }

            var instRect = (RectTransform)inst.transform;
            instRect.anchorMin = new Vector2(0.5f, 0.5f);
            instRect.anchorMax = new Vector2(0.5f, 0.5f);
            instRect.anchoredPosition = Vector2.zero;
            instRect.localScale = Vector3.one;

            // Popups are authored on top of the screen; hide them so the header is visible.
            foreach (var n in new[] { "Invite_Player_Club_Popup", "Expel_Member_PopUp", "DetermineRequest_Applicant_PopUp",
                                      "Ranks_based_permission_editor", "Discard_Changes_Popup" })
            {
                var t = FindDeep(inst.transform, n);
                if (t) t.gameObject.SetActive(false);
            }

            foreach (var s in inst.GetComponentsInChildren<Selectable>(true))
            {
                s.interactable = true;
                if (s.targetGraphic && s.targetGraphic.canvasRenderer) s.targetGraphic.canvasRenderer.SetColor(Color.white);
            }

            for (int i = 0; i < 4; i++)
            {
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
    }
}
