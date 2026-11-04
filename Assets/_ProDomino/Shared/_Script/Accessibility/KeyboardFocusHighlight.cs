using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared.Accessibility
{
    /// <summary>
    /// Draws a focus ring around the element focused with the keyboard. Lives on its own overlay canvas (no
    /// raycaster, no raycast targets), so it never blocks or changes pointer interaction or the existing UI design.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyboardFocusHighlight : MonoBehaviour
    {
        /// <summary>Ring color. Bright gold reads well on the dark and the light screens of the game.</summary>
        public static Color RingColor = new(1f, .82f, .22f, 1f);
        /// <summary>Thin dark outline outside the ring, for contrast on light backgrounds.</summary>
        public static Color OutlineColor = new(0f, 0f, 0f, .55f);
        /// <summary>Gap between the element and the ring, in screen pixels.</summary>
        public static float Padding = 4f;

        private const int BorderPixels = 3;

        private RectTransform ring;
        private RectTransform outline;
        private Canvas canvas;

        internal static KeyboardFocusHighlight Create(Transform parent)
        {
            var go = new GameObject(nameof(KeyboardFocusHighlight), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvas.overrideSorting = true;
            canvas.enabled = false;

            var highlight = go.AddComponent<KeyboardFocusHighlight>();
            highlight.canvas = canvas;

            var sprite = CreateRingSprite();
            highlight.outline = CreateImage(go.transform, "Outline", sprite, OutlineColor);
            highlight.ring = CreateImage(go.transform, "Ring", sprite, RingColor);
            return highlight;
        }

        /// <summary>Positions the ring around <paramref name="target"/>, or hides it when null.</summary>
        internal void Follow(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy
                || !KeyboardNavigationController.TryGetScreenRect(target, out var rect)
                || rect.width < 1f || rect.height < 1f)
            {
                if (canvas.enabled)
                    canvas.enabled = false;
                return;
            }

            if (!canvas.enabled)
                canvas.enabled = true;

            // Overlay canvas without a CanvasScaler: 1 unit == 1 screen pixel.
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Place(ring, rect, Padding, scale);
            Place(outline, rect, Padding + 1f, scale);
        }

        private static void Place(RectTransform rt, Rect rect, float padding, float scale)
        {
            float pad = padding + BorderPixels;
            rt.anchoredPosition = new Vector2(rect.xMin - pad, rect.yMin - pad) / scale;
            rt.sizeDelta = new Vector2(rect.width + pad * 2f, rect.height + pad * 2f) / scale;
        }

        private static RectTransform CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        /// <summary>A small rounded ring texture, 9-sliced so the border keeps its thickness at any size.</summary>
        private static Sprite CreateRingSprite()
        {
            const int size = 16;
            const int slice = 6;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KeyboardFocusRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the rounded-rectangle edge (corner radius = slice).
                    float dx = Mathf.Max(slice - x - .5f, x + .5f - (size - slice), 0f);
                    float dy = Mathf.Max(slice - y - .5f, y + .5f - (size - slice), 0f);
                    float distanceFromCornerCenter = Mathf.Sqrt(dx * dx + dy * dy);
                    float edgeDistance = (dx > 0f && dy > 0f)
                        ? slice - distanceFromCornerCenter
                        : Mathf.Min(Mathf.Min(x + .5f, size - x - .5f), Mathf.Min(y + .5f, size - y - .5f));

                    float outer = Mathf.Clamp01(edgeDistance);                  // anti-aliased outer edge
                    float inner = Mathf.Clamp01(BorderPixels - edgeDistance + 1f); // anti-aliased inner edge
                    byte a = (byte)(255f * Mathf.Min(outer, inner));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
        }
    }
}
