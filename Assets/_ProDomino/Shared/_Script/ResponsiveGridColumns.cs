using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// Stretches the cells of a fixed column count GridLayoutGroup so the columns always fill the grid's width,
    /// instead of leaving empty space on the right on wide screens. The cell height is kept as authored.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class ResponsiveGridColumns : MonoBehaviour
    {
        private GridLayoutGroup grid;
        private RectTransform rectTransform;

        private void Awake()
        {
            grid = GetComponent<GridLayoutGroup>();
            rectTransform = (RectTransform)transform;
        }

        private void OnEnable() => Refresh();

        private void OnRectTransformDimensionsChange() => Refresh();

        private void Refresh()
        {
            if (!grid || grid.constraint is not GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount < 1)
                return;

            var columns = grid.constraintCount;
            var width = (rectTransform.rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
            if (width <= 0f || Mathf.Approximately(width, grid.cellSize.x))
                return;

            grid.cellSize = new Vector2(width, grid.cellSize.y);
        }
    }
}
