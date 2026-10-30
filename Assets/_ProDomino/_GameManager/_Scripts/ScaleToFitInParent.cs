using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// Fits a fixed-aspect panel inside its parent like an AspectRatioFitter in "Fit In Parent"
    /// mode, but keeps the size it has outside fullscreen and scales it up in fullscreen.
    /// Children authored with fixed pixel sizes (the player hand) then stay in step with
    /// stretched ones (the board frame), so toggling fullscreen never changes the inner layout.
    /// Outside fullscreen it gives exactly the same size and position as the AspectRatioFitter.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class ScaleToFitInParent : MonoBehaviour
    {
        // Named like AspectRatioFitter's field so prefabs converted from one keep their value.
        [SerializeField] private float m_AspectRatio = 1.6680223f;

        private RectTransform self;
        private FullScreenController fullScreenController;

        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (!self) self = (RectTransform)transform;
            if (self.parent is not RectTransform parent || m_AspectRatio <= 0f) return;

            var area = parent.rect.size;
            if (area.x <= 1f || area.y <= 1f) return;

            // The size the parent has outside fullscreen: the inner screen at its normal size.
            // Fixed offsets between the inner screen and our parent carry over unchanged.
            var normalArea = area;
            var controller = Controller();
            var inner = controller ? controller.InnerScreen : null;
            if (inner && parent.IsChildOf(inner))
                normalArea -= inner.rect.size - controller.NormalInnerSize;
            if (normalArea.x <= 1f || normalArea.y <= 1f) normalArea = area;

            var design = FitSize(normalArea);
            var actual = FitSize(area);
            float scale = actual.x / design.x;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;

            var centre = new Vector2(0.5f, 0.5f);
            if (self.anchorMin != centre) self.anchorMin = centre;
            if (self.anchorMax != centre) self.anchorMax = centre;
            if (self.pivot != centre) self.pivot = centre;
            if (self.anchoredPosition != Vector2.zero) self.anchoredPosition = Vector2.zero;
            if ((self.sizeDelta - design).sqrMagnitude > 0.0001f) self.sizeDelta = design;

            var wanted = new Vector3(scale, scale, 1f);
            if ((self.localScale - wanted).sqrMagnitude > 0.0000001f) self.localScale = wanted;
        }

        private Vector2 FitSize(Vector2 area) =>
            area.x / area.y > m_AspectRatio
                ? new Vector2(area.y * m_AspectRatio, area.y)
                : new Vector2(area.x, area.x / m_AspectRatio);

        private FullScreenController Controller()
        {
            if (!Application.isPlaying) return null;
            if (!fullScreenController) fullScreenController = FindFirstObjectByType<FullScreenController>();
            return fullScreenController;
        }
    }
}
