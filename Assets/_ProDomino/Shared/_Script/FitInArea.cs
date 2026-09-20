using UnityEngine;

namespace ProDomino.Shared
{
    /// <summary>
    /// Scales a fixed-size panel down uniformly so it always fits inside the area it is centred in,
    /// with a margin. Used by the auth cards: they keep their design size when there is room and
    /// shrink instead of running off a short or narrow window. Never scales above 1.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class FitInArea : MonoBehaviour
    {
        [Tooltip("Space kept free around the panel, in canvas units.")]
        [SerializeField] private Vector2 margin = new Vector2(32f, 32f);

        [Tooltip("Smallest scale allowed, so the panel never becomes unreadable.")]
        [SerializeField, Range(0.1f, 1f)] private float minScale = 0.35f;

        private RectTransform self;

        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (!self) self = (RectTransform)transform;
            var area = Area();
            if (!area) return;

            // Measured in world units, because the panel can sit under parents with a scale of
            // their own: what matters is how big it ends up next to the screen.
            float fromParents = Mathf.Approximately(transform.localScale.x, 0f)
                ? 1f
                : self.lossyScale.x / transform.localScale.x;
            if (fromParents <= 0f) return;

            var size = self.rect.size * fromParents;
            var room = (area.rect.size - margin * 2f) * area.lossyScale.x;
            if (size.x <= 1f || size.y <= 1f || room.x <= 1f || room.y <= 1f) return;

            float scale = Mathf.Clamp(Mathf.Min(1f, room.x / size.x, room.y / size.y), minScale, 1f);
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return;

            var wanted = new Vector3(scale, scale, 1f);
            if ((transform.localScale - wanted).sqrMagnitude > 0.0000001f)
                transform.localScale = wanted;
        }

        // The area the panel is centred in — its parent, or the canvas when it has no rect parent.
        private RectTransform Area()
        {
            if (transform.parent is RectTransform parent) return parent;
            var canvas = GetComponentInParent<Canvas>();
            return canvas && canvas.rootCanvas ? (RectTransform)canvas.rootCanvas.transform : null;
        }
    }
}
