using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// Stretches the cells of a fixed column count GridLayoutGroup so the columns always fill the grid's width,
    /// instead of leaving empty space on the right on wide screens. The cell height is kept as authored, unless
    /// <see cref="fitRowsToParentHeight"/> is set: then it shrinks so every row fits inside the parent (the scroll viewport),
    /// down to <see cref="minCellHeight"/>, below which the parent is expected to scroll.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class ResponsiveGridColumns : MonoBehaviour
    {
        [SerializeField] private bool fitRowsToParentHeight;
        [SerializeField] private float minCellHeight = 150f;

        private GridLayoutGroup grid;
        private RectTransform rectTransform;
        private float authoredCellHeight;
        private float lastParentHeight;

        private void Awake()
        {
            grid = GetComponent<GridLayoutGroup>();
            rectTransform = (RectTransform)transform;
            authoredCellHeight = grid.cellSize.y;
        }

        private void OnEnable() => Refresh();

        private void OnRectTransformDimensionsChange() => Refresh();

        private void OnTransformParentChanged() => Refresh();

        // A height-only change of the parent doesn't reach this rect (its height is driven by the grid), so poll it.
        private void LateUpdate()
        {
            if (!fitRowsToParentHeight || transform.parent is not RectTransform parent)
                return;

            if (Mathf.Approximately(parent.rect.height, lastParentHeight))
                return;

            lastParentHeight = parent.rect.height;
            Refresh();
        }

        private void Refresh()
        {
            if (!grid || grid.constraint is not GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount < 1)
                return;

            var columns = grid.constraintCount;
            var width = (rectTransform.rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
            if (width <= 0f)
                return;

            var height = fitRowsToParentHeight ? GetFittedCellHeight(columns) : grid.cellSize.y;
            if (Mathf.Approximately(width, grid.cellSize.x) && Mathf.Approximately(height, grid.cellSize.y))
                return;

            grid.cellSize = new Vector2(width, height);
        }

        private float GetFittedCellHeight(int columns)
        {
            if (transform.parent is not RectTransform parent)
                return authoredCellHeight;

            var activeChildren = 0;
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf)
                    activeChildren++;

            var rows = Mathf.CeilToInt(activeChildren / (float)columns);
            if (rows < 1 || parent.rect.height <= 0f)
                return authoredCellHeight;

            var available = (parent.rect.height - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;
            return Mathf.Clamp(available, Mathf.Min(minCellHeight, authoredCellHeight), authoredCellHeight);
        }
    }
}
