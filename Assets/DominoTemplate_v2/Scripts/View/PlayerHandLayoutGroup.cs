using UnityEngine;
using UnityEngine.UI;

namespace DominoTemplate.View
{
    /// <summary>
    /// Lays out the player's hand tiles at a fixed size. When the hand gets too wide, the row first grows
    /// into the width of <see cref="widthReference"/>, then tiles cascade (overlap) instead of shrinking.
    /// Child order is kept, so sorting and drag/drop behave as with a HorizontalLayoutGroup.
    /// </summary>
    [AddComponentMenu("Layout/Player Hand Layout Group")]
    public class PlayerHandLayoutGroup : LayoutGroup
    {
        [SerializeField] private float tileHeight = 60f;
        [SerializeField] private float tileAspect = 0.5f; // width / height
        [SerializeField] private float spacing = 4f;

        [Tooltip("Rect the row may extend across when tiles don't fit. Defaults to the parent.")]
        [SerializeField] private RectTransform widthReference = null;
        [SerializeField, Range(0.1f, 1f)] private float maxWidthFraction = 0.9f;

        [Tooltip("Smallest visible part of an overlapped tile, as a fraction of its width.")]
        [SerializeField, Range(0.2f, 1f)] private float minVisibleFraction = 0.55f;

        private float TileWidth => tileHeight * tileAspect;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            float width = padding.horizontal + RowWidth(StepFor(float.MaxValue));
            SetLayoutInputForAxis(width, width, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float height = padding.vertical + tileHeight;
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            float step = StepFor(MaxRowWidth());
            float rowWidth = RowWidth(step);
            float start = GetStartOffset(0, rowWidth);

            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 0, start + i * step, TileWidth);
        }

        public override void SetLayoutVertical()
        {
            float start = GetStartOffset(1, tileHeight);

            for (int i = 0; i < rectChildren.Count; i++)
                SetChildAlongAxis(rectChildren[i], 1, start, tileHeight);
        }

        // Distance between the left edges of neighbouring tiles that keeps the row within maxWidth,
        // never going below the minimum visible fraction (the row then simply gets wider).
        private float StepFor(float maxWidth)
        {
            float fullStep = TileWidth + spacing;
            int count = rectChildren.Count;

            if (count < 2 || RowWidth(fullStep) <= maxWidth)
                return fullStep;

            float fittedStep = (maxWidth - TileWidth) / (count - 1);
            return Mathf.Clamp(fittedStep, TileWidth * minVisibleFraction, fullStep);
        }

        private float RowWidth(float step) => rectChildren.Count == 0 ? 0f : TileWidth + (rectChildren.Count - 1) * step;

        // Widest row allowed before cascading, in this rect's local units.
        private float MaxRowWidth()
        {
            float ownWidth = rectTransform.rect.width - padding.horizontal;
            RectTransform reference = widthReference != null ? widthReference : rectTransform.parent as RectTransform;

            if (reference == null)
                return ownWidth;

            float scaleX = rectTransform.lossyScale.x;
            float referenceWidth = scaleX > 0f
                ? reference.rect.width * reference.lossyScale.x / scaleX
                : reference.rect.width;

            return Mathf.Max(ownWidth, referenceWidth * maxWidthFraction - padding.horizontal);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            tileHeight = Mathf.Max(1f, tileHeight);
            tileAspect = Mathf.Max(0.01f, tileAspect);
            base.OnValidate();
        }
#endif
    }
}
