using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(GridLayoutGroup))]
public class AdjustGridLayoutCellSize : MonoBehaviour
{
    public enum Axis { X, Y };
    public enum RatioMode { Free, Fixed };

    [SerializeField] Axis expand;
    [SerializeField] RatioMode ratioMode;
    [SerializeField] float cellRatio = 1;

    new RectTransform transform;

    private GridLayoutGroup grid;
    protected GridLayoutGroup Grid => grid ??= GetComponent<GridLayoutGroup>();

    void Awake()
    {
        transform = (RectTransform)base.transform;
    }

    // Start is called before the first frame update
    void Start()
    {
        UpdateCellSize();
    }

    void OnRectTransformDimensionsChange()
    {
        UpdateCellSize();
    }

#if UNITY_EDITOR
    [ExecuteAlways]
    void Update()
    {
        UpdateCellSize();
    }
#endif

    void OnValidate()
    {
        transform = (RectTransform)base.transform;
        UpdateCellSize();
    }

    void UpdateCellSize()
    {
        if (!Grid || Grid.padding is null || !transform)
            return;

        var count = Grid.constraintCount;
        if (expand == Axis.X)
        {
            float spacing = (count - 1) * Grid.spacing.x;
            float contentSize = transform.rect.width - Grid.padding.left - Grid.padding.right - spacing;
            float sizePerCell = contentSize / count;
            Grid.cellSize = new Vector2(sizePerCell, ratioMode == RatioMode.Free ? Grid.cellSize.y : sizePerCell * cellRatio);

        } else //if (expand == Axis.Y)
        {
            float spacing = (count - 1) * Grid.spacing.y;
            float contentSize = transform.rect.height - Grid.padding.top - Grid.padding.bottom - spacing;
            float sizePerCell = contentSize / count;
            Grid.cellSize = new Vector2(ratioMode == RatioMode.Free ? Grid.cellSize.x : sizePerCell * cellRatio, sizePerCell);
        }
    }
}