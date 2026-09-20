using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard.Editor
{
    // Shared pieces of the Figma design system, used by every screen restyler: the colour and
    // metric tokens, the fonts, the generated sprites, and the hierarchy/layout/skin helpers.
    //
    // Screen restylers keep only what is specific to their screen and take everything else from
    // here (`using static ProDomino.Dashboard.Editor.PdUiKit;`), so a token change lands on every
    // screen at once.
    internal static class PdUiKit
    {
        public const string GeneratedDir = "Assets/_ProDomino/Dashboard/Generated";
        public const string FontDir = "Assets/_ProDomino/_UI/Fonts/Dashboard";
        public const string IconDir = "Assets/_ProDomino/_UI/Icons/Icons_Dashboard";

        // ---------------------------------------------------------------- colour tokens
        // Design System → Color palette.
        public static readonly Color PageBg = Hex("#01010C");        // app background
        public static readonly Color PanelBg = Hex("#010818");       // sidebar / panel body
        public static readonly Color CardTop = Hex("#27272C");       // card gradient, top
        public static readonly Color CardBottom = Hex("#01010C");    // card gradient, bottom
        public static readonly Color CardBorder = Hex("#37373D");
        public static readonly Color FieldFill = Hex("#212129");
        public static readonly Color FieldBorder = Hex("#2E2E38");
        public static readonly Color ChipBg = Hex("#040717");
        public static readonly Color DropdownBg = Hex("#0A0E1C");

        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextLabel = Hex("#E6E6E7");
        public static readonly Color TextMuted = Hex("#B0B0B4");
        public static readonly Color TextPlaceholder = Hex("#55555C");
        public static readonly Color OnAccent = Hex("#01010C");      // text on an accent surface

        public static readonly Color Accent = Hex("#FDC553");        // gold
        public static readonly Color AccentStart = Hex("#FFA501");   // gold gradient, left
        public static readonly Color AccentEnd = Hex("#FDC653");     // gold gradient, right
        public static readonly Color AccentRim = Hex("#FFD98A");
        public static readonly Color AccentShadow = Hex("#A15800");
        public static readonly Color Info = Hex("#416FC3");
        public static readonly Color InfoEnd = Hex("#78ADFF");
        public static readonly Color Danger = Hex("#FF6B6B");
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.12f);

        // ---------------------------------------------------------------- metric tokens
        public const float Radius = 10f;          // buttons, fields, rows
        public const float CardRadius = 12f;      // pop-up cards
        public const float RowHeight = 44f;       // sidebar / list rows
        public const float FieldHeight = 48f;
        public const float ButtonHeight = 56f;
        public const float IconSize = 20f;

        // ---------------------------------------------------------------- fonts
        private static readonly Dictionary<string, TMP_FontAsset> Fonts = new();

        public static TMP_FontAsset Regular => LoadFont("Montserrat-Regular");
        public static TMP_FontAsset Medium => LoadFont("Montserrat-Medium");
        public static TMP_FontAsset SemiBold => LoadFont("Montserrat-SemiBold");
        public static TMP_FontAsset Bold => LoadFont("Montserrat-Bold");
        public static TMP_FontAsset ExtraBold => LoadFont("Montserrat-ExtraBold");

        public static TMP_FontAsset LoadFont(string name)
        {
            if (Fonts.TryGetValue(name, out var cached) && cached) return cached;
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{name} SDF.asset");
            Require(f, name);
            Fonts[name] = f;
            return f;
        }

        // ---------------------------------------------------------------- generated sprites
        // A rounded rectangle with an optional gradient fill and border, written to the project as a
        // 9-sliced sprite. `vertical` runs the gradient top -> bottom, otherwise left -> right.
        public static Sprite MakePanelSprite(string name, int w, int h, int radius, Color top, Color bottom,
            Color border, float borderWidth, bool vertical = true)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // texture y is bottom-up; a vertical design gradient runs top -> bottom
                float t = vertical
                    ? (h > 1 ? 1f - (float)y / (h - 1) : 0f)
                    : (w > 1 ? (float)x / (w - 1) : 0f);
                var fill = Color.Lerp(top, bottom, t);

                float sd = radius - CornerDistance(x + 0.5f, y + 0.5f, w, h, radius);
                float inside = Mathf.Clamp01(sd + 0.5f);
                var c = fill;
                if (borderWidth > 0f && border.a > 0f)
                {
                    float borderMask = Mathf.Clamp01(borderWidth + 0.5f - sd) * border.a;
                    c = Color.Lerp(fill, border, borderMask);
                    c.a = Mathf.Max(fill.a, borderMask);
                }
                c.a *= inside;
                tex.SetPixel(x, y, c);
            }
            return SaveSlicedSprite(path, tex, radius + 2);
        }

        // Horizontal gradient with rounded corners. The gradient stretches with the centre slice,
        // so the full sprite width is used for the ramp.
        public static Sprite MakeRoundedSprite(string name, int w, int h, int radius, Color left, Color right)
        {
            var path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = w > 1 ? (float)x / (w - 1) : 0f;
                var c = Color.Lerp(left, right, t);
                c.a = CornerAlpha(x + 0.5f, y + 0.5f, w, h, radius);
                tex.SetPixel(x, y, c);
            }
            return SaveSlicedSprite(path, tex, radius + 1);
        }

        public static Sprite MakeCircleSprite(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                var c = Color.white;
                c.a = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{GeneratedDir}/{name}.png", tex);
        }

        // Soft radial glow, used behind active rows and icons.
        public static Sprite MakeGlowSprite(string name, int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f;
                float ny = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                var c = color; c.a = a * a;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{GeneratedDir}/{name}.png", tex);
        }

        // Distance from the rounded-rect corner centre (0 in the straight parts).
        public static float CornerDistance(float px, float py, int w, int h, float r)
        {
            float cx = Mathf.Clamp(px, r, w - r);
            float cy = Mathf.Clamp(py, r, h - r);
            return Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
        }

        public static float CornerAlpha(float px, float py, int w, int h, float r) =>
            Mathf.Clamp01(r - CornerDistance(px, py, w, h, r) + 0.5f);

        public static Sprite SaveSlicedSprite(string path, Texture2D tex, int border)
        {
            tex.Apply();
            WritePng(path, tex);
            var imp = ImportAsSprite(path);
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Sprite SaveSprite(string path, Texture2D tex)
        {
            tex.Apply();
            WritePng(path, tex);
            ImportAsSprite(path).SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static TextureImporter ImportAsSprite(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = 100;
            imp.spriteBorder = Vector4.zero;
            return imp;
        }

        public static void WritePng(string assetPath, Texture2D tex)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedDir))
                AssetDatabase.CreateFolder("Assets/_ProDomino/Dashboard", "Generated");
            File.WriteAllBytes(Application.dataPath + assetPath.Substring("Assets".Length), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        // ---------------------------------------------------------------- hierarchy
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        public static Transform Need(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t == null) throw new Exception($"UI: required child '{name}' not found under '{parent.name}'.");
            return t;
        }

        public static void Require(UnityEngine.Object o, string what)
        {
            if (o == null) throw new Exception($"UI: could not load {what}.");
        }

        public static T GetOrAdd<T>(Transform t) where T : Component =>
            t.TryGetComponent<T>(out var c) ? c : t.gameObject.AddComponent<T>();

        public static Transform GetOrCreate(Transform parent, string name, Func<Transform> create)
        {
            var existing = parent.Find(name);
            return existing ? existing : create();
        }

        public static void Reparent(Transform t, Transform parent)
        {
            if (t.parent != parent) t.SetParent(parent, false);
            t.localScale = Vector3.one;
        }

        // Runtime types live in other assemblies, so they are looked up by name.
        public static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(x => x != null)
            ?? throw new Exception($"UI: type {fullName} not found (compile error?).");

        public static Component AddByName(Transform t, string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName)).FirstOrDefault(x => x != null);
            if (type == null) { Debug.LogWarning($"UI: {fullName} not found (compile error?)."); return null; }
            return t.TryGetComponent(type, out var existing) ? existing : t.gameObject.AddComponent(type);
        }

        public static void Hide(Transform root, params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeep(root, n);
                if (t) t.gameObject.SetActive(false);
            }
        }

        public static void DisableFitter(Transform t)
        {
            if (t.TryGetComponent<AspectRatioFitter>(out var f)) f.enabled = false;
        }

        // Switches off every layout group in a subtree, so only the new layout drives it.
        public static void KillLayout(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (c is VerticalLayoutGroup or HorizontalLayoutGroup or ContentSizeFitter or AspectRatioFitter)
                    ((Behaviour)c).enabled = false;
            }
        }

        // ---------------------------------------------------------------- placement
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // Top-left origin, the way the design measures things.
        public static void TL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void TLCentered(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void MiddleLeft(RectTransform rt, float x, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void MiddleRight(RectTransform rt, float fromRight, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-fromRight, 0f);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---------------------------------------------------------------- flow layout
        // A stacked group with the design's gap, sized from its children: content that grows (a
        // validation message, a list) pushes the rest of the screen instead of overlapping it.
        public static Transform Column(Transform parent, string name, float spacing, float width)
        {
            var t = GetOrCreate(parent, name, () => new GameObject(name, typeof(RectTransform)).transform);
            Reparent(t, parent);
            ((RectTransform)t).sizeDelta = new Vector2(width, ((RectTransform)t).sizeDelta.y);

            var vlg = GetOrAdd<VerticalLayoutGroup>(t);
            vlg.enabled = true;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = spacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

            var csf = GetOrAdd<ContentSizeFitter>(t);
            csf.enabled = true;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }

        // A fixed-height band inside a column, holding elements side by side at set offsets.
        public static Transform Band(Transform parent, string name, float width, float height)
        {
            var t = GetOrCreate(parent, name, () => new GameObject(name, typeof(RectTransform)).transform);
            Reparent(t, parent);
            foreach (var layout in t.GetComponents<LayoutGroup>()) layout.enabled = false;
            if (t.TryGetComponent<ContentSizeFitter>(out var csf)) csf.enabled = false;
            Slot(t, width, height);
            return t;
        }

        // Stacked in a column, or placed at a fixed offset inside a band.
        public static void Place(Transform parent, Transform t, float x, float w, float h)
        {
            if (!t) return;
            Reparent(t, parent);
            if (parent.TryGetComponent<VerticalLayoutGroup>(out var vlg) && vlg.enabled) Slot(t, w, h);
            else TL((RectTransform)t, x, 0f, w, h);
        }

        // One row of a column: the layout positions it, this only fixes its size.
        public static void Slot(Transform t, float w, float h)
        {
            var rt = (RectTransform)t;
            rt.sizeDelta = new Vector2(w, h);
            if (t.TryGetComponent<LayoutElement>(out var le))
            {
                le.ignoreLayout = false;
                le.preferredWidth = w; le.preferredHeight = h;
                le.minWidth = -1f; le.minHeight = -1f;
                le.flexibleWidth = -1f; le.flexibleHeight = -1f;
            }
        }

        // ---------------------------------------------------------------- content
        public static GameObject MakeImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.type = type; img.raycastTarget = false;
            img.pixelsPerUnitMultiplier = 1f;
            return go;
        }

        public static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font; tmp.fontSharedMaterial = font.material;
            tmp.fontSize = size; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        // Fixed-size text, as the design specifies it.
        public static void Text(TextMeshProUGUI t, string value, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            if (!t) return;
            if (value != null) t.text = value;
            t.font = font; t.fontSharedMaterial = font.material;
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.fontStyle = FontStyles.Normal;
            t.color = color;
            t.alignment = align;
            t.margin = Vector4.zero;
            t.lineSpacing = 0f;
        }

        // Text that shrinks to fit its box: used for names and counters that can be long.
        public static TextMeshProUGUI Label(TextMeshProUGUI tmp, TMP_FontAsset font, float size, Color color)
        {
            tmp.font = font; tmp.fontSharedMaterial = font.material;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = Mathf.Min(10f, size); tmp.fontSizeMax = size; tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.margin = Vector4.zero;
            tmp.lineSpacing = 0f;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void SetSprite(Transform t, Sprite sprite, Image.Type type = Image.Type.Sliced)
        {
            if (t == null || !t.TryGetComponent<Image>(out var img)) return;
            img.sprite = sprite; img.type = type; img.pixelsPerUnitMultiplier = 1f;
        }

        public static void HideImage(Transform parent, string child)
        {
            var t = parent.Find(child);
            if (t && t.TryGetComponent<Image>(out var img)) img.enabled = false;
        }

        // Keeps a clickable area but draws nothing.
        public static void Transparent(Transform t)
        {
            var img = GetOrAdd<Image>(t);
            img.sprite = null;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
        }

        // ---------------------------------------------------------------- interaction
        // The old buttons tint their graphic with dark "normal" colours, which would hide the new
        // sprites; keep white as the base and only darken on hover/press.
        public static void NeutralTint(Transform button, Graphic target)
        {
            if (!button.TryGetComponent<Selectable>(out var selectable)) return;
            selectable.targetGraphic = target;
            selectable.transition = Selectable.Transition.ColorTint;
            var c = selectable.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.92f, 0.92f, 0.95f, 1f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            c.fadeDuration = 0.08f;
            selectable.colors = c;
            var nav = selectable.navigation; nav.mode = Navigation.Mode.None; selectable.navigation = nav;
        }

        public static void AddSortingCanvas(GameObject go, int order)
        {
            var canvas = go.AddComponent<Canvas>();
            var so = new SerializedObject(canvas);
            so.FindProperty("m_OverrideSorting").boolValue = true;
            so.FindProperty("m_SortingOrder").intValue = order;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<GraphicRaycaster>();
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
