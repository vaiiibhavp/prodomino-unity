using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages displaying, pooling, and animating tooltips across different types using a shared overlay canvas.
/// </summary>
public class TooltipController : SingleInstanceMonoBehaviour<TooltipController>, IService
{
    [Header("Canvas Settings")]
    [SerializeField] private Canvas tooltipCanvas;
    [SerializeField] private RectTransform instanceContainer;

    [Header("Tooltip Prefabs")]
    [SerializeField] private List<Tooltip> tooltipPrefabs;

    [Header("Tooltip Behavior")]
    [SerializeField, Tooltip("Default delay before showing tooltips.")]
    private float defaultDelay = 0.15f;

    [SerializeField, Tooltip("Duration of fade/scale animation in seconds.")]
    private float fadeDuration = 0.25f;

    [SerializeField] private AnimationCurve fadeCurve;
    [SerializeField] private AnimationCurve scaleCurve;

    private readonly Dictionary<string, Tooltip> pooledInstances = new();
    private Coroutine tooltipRoutine;
    private Coroutine animationCoroutine;

    public bool IsAlreadyInitialized => true;
    public Tooltip ActiveTooltip { get; private set; }
    public TooltipModel UsedTooltipModel { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        // Register each tooltip already in the instance container and hide them immediately by default
        if (instanceContainer != null)
            foreach (Transform child in instanceContainer)
            {
                var tooltip = child.GetComponent<Tooltip>();
                if (tooltip != null && !string.IsNullOrEmpty(tooltip.TooltipId))
                {
                    tooltip.HideImmediate();
                    pooledInstances[tooltip.TooltipId] = tooltip;
                }
            }
    }

    /// <summary>
    /// Displays a tooltip anchored to a UI element using a model.
    /// </summary>
    public void ShowAtUI(RectTransform uiTarget, TooltipModel model)
    {
        if (tooltipRoutine != null)
        {
            StopCoroutine(tooltipRoutine);
            tooltipRoutine = null;
        }

        UsedTooltipModel = model;

        var canvasRect = tooltipCanvas.GetComponent<RectTransform>();
        var camera = tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : tooltipCanvas.worldCamera;

        // Calculate the anchor position based on the target RectTransform and model settings
        var anchorWorldPos = GetAnchorWorldPosition(uiTarget, model.Anchor, model.Outside);
        var screenPos = RectTransformUtility.WorldToScreenPoint(camera, anchorWorldPos);

        // Convert screen position to local position in the canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            camera,
            out var localPoint
        );

        // Using the local point, start the tooltip display routine
        tooltipRoutine = StartCoroutine(ShowTooltipDelayed(localPoint, model));
    }

    /// <summary>
    /// Hides the currently active tooltip, if any.
    /// </summary>
    public void Hide()
    {
        if (tooltipRoutine != null)
        {
            StopCoroutine(tooltipRoutine);

            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            tooltipRoutine = null;
        }

        if (ActiveTooltip != null)
        {
            ActiveTooltip.HideImmediate();

            var id = ActiveTooltip.TooltipId;
            if (!pooledInstances.ContainsKey(id))
                pooledInstances[id] = ActiveTooltip;

            ActiveTooltip = null;
        }
    }

    /// <summary>
    /// Calculates anchor position of a target based on anchor and outside flag.
    /// </summary>
    private Vector3 GetAnchorWorldPosition(RectTransform target, TooltipAnchor anchor, bool outside)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        var offset = outside ? 1f : 0.5f;
        float scaledHeight = target.rect.height * target.lossyScale.y;
        float scaledWidth = target.rect.width * target.lossyScale.x;

        return anchor switch
        {
            TooltipAnchor.Top => Vector3.Lerp(corners[1], corners[2], 0.5f) + Vector3.up * scaledHeight * (offset - 0.5f),
            TooltipAnchor.Bottom => Vector3.Lerp(corners[0], corners[3], 0.5f) - Vector3.up * scaledHeight * (offset - 0.5f),
            TooltipAnchor.Left => Vector3.Lerp(corners[0], corners[1], 0.5f) - Vector3.right * scaledWidth * (offset - 0.5f),
            TooltipAnchor.Right => Vector3.Lerp(corners[2], corners[3], 0.5f) + Vector3.right * scaledWidth * (offset - 0.5f),
            TooltipAnchor.Center => target.position,
            _ => target.position,
        };
    }

    /// <summary>
    /// Delays display of tooltip and plays animation. Hides automatically if duration is set.
    /// </summary>
    private IEnumerator ShowTooltipDelayed(Vector2 localPosition, TooltipModel model)
    {
        UsedTooltipModel = model;

        // Hide the previous tooltip if it exists
        Hide();

        // Try to get the tooltip instance from the pool or instantiate it
        ActiveTooltip = GetTooltipInstance(model.TooltipId);
        if (ActiveTooltip == null)
        {
            Debug.LogWarning($"Failed to get tooltip instance for ID '{model.TooltipId}'.");
            yield break;
        }

        yield return new WaitForSecondsRealtime(model.DelayOrNull ?? defaultDelay);

        var rect = ActiveTooltip.GetComponent<RectTransform>();

        // Determine the pivot based on the anchor type
        rect.pivot = model.Anchor switch
        {
            TooltipAnchor.Top => new Vector2(0.5f, 0f),
            TooltipAnchor.Bottom => new Vector2(0.5f, 1f),
            TooltipAnchor.Left => new Vector2(1f, 0.5f),
            TooltipAnchor.Right => new Vector2(0f, 0.5f),
            TooltipAnchor.Center => new Vector2(0.5f, 0.5f),
            _ => rect.pivot
        };

        // Add the offset to the local position and configure the tooltip
        rect.localPosition = localPosition + model.Offset;
        ActiveTooltip.Configure(model.Message, model.Icon);

        animationCoroutine = StartCoroutine(PlayAnimation(ActiveTooltip));
        yield return animationCoroutine;

        if (model.DurationOrNull.HasValue)
        {
            yield return new WaitForSecondsRealtime(model.DurationOrNull.Value);
            Hide();
        }
    }

    /// <summary>
    /// Retrieves a tooltip instance from the pool or instantiates one if missing.
    /// </summary>
    private Tooltip GetTooltipInstance(string tooltipId)
    {
        if (pooledInstances.TryGetValue(tooltipId, out var tooltip))
        {
            tooltip.Initialize(OnTooltipHoverExit);
            return tooltip;
        }

        var tooltipPrefab = tooltipPrefabs.FirstOrDefault(x => x.TooltipId == tooltipId);
        if (tooltipPrefab)
        {
            var tooltipInstance = Instantiate(tooltipPrefab, tooltipCanvas.transform);
            tooltipInstance.Initialize(OnTooltipHoverExit);

            pooledInstances.Add(tooltipId, tooltipInstance);
            return tooltipInstance;
        }

        Debug.LogWarning($"Tooltip prefab with ID '{tooltipId}' not found.");
        return null;
    }

    /// <summary>
    /// Event handler for when the pointer exits a tooltip.
    /// </summary>
    /// <param name="tooltip"></param>
    private void OnTooltipHoverExit(Tooltip tooltip)
    {
        // If the tooltip is not the active one, do nothing
        if (tooltip != ActiveTooltip)
            return;

        Hide();
    }

    /// <summary>
    /// Try to retrieves a reference to a tooltip instance by its ID from the pool.
    /// </summary>
    /// <param name="tooltipId"></param>
    /// <returns></returns>
    public Tooltip GetTootipReference(string tooltipId)
    {
        if (pooledInstances.TryGetValue(tooltipId, out var tooltip))
            return tooltip;

        else
        {
            Debug.LogWarning($"Tooltip with ID '{tooltipId}' not found in pool.");
            return null;
        }
    }

    /// <summary>
    /// Plays fade and scale animation using animation curves over a fixed duration.
    /// </summary>
    private IEnumerator PlayAnimation(Tooltip tooltip)
    {
        var canvasGroup = tooltip.CanvasGroup;
        var rectTransform = tooltip.RectTransform;

        float time = 0f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0;

        if (rectTransform != null)
            rectTransform.localScale = Vector3.zero;

        while (time < fadeDuration)
        {
            var t = time / fadeDuration;
            var alpha = fadeCurve.Evaluate(t);
            var scale = scaleCurve.Evaluate(t);

            if (canvasGroup != null) 
                canvasGroup.alpha = alpha;

            if (rectTransform != null) 
                rectTransform.localScale = Vector3.one * scale;

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.SetActive(true);
        if (rectTransform != null) 
            rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Checks if the pointer is currently over any tooltip.
    /// </summary>
    public bool IsPointerOverTooltip()
    {
        if (UsedTooltipModel is null or { IsStillVisbleOnPointerOverTooltip: false })
            return false;

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            // Check if the object is a tooltip instance (optional: by tag or component)
            if (result.gameObject.TryGetComponent<Tooltip>(out _))
                return true;
        }

        return false;
    }


    public enum TooltipAnchor
    {
        Top,
        Bottom,
        Left,
        Right,
        Center
    }

    [Serializable]
    public class TooltipModel
    {
        [field: SerializeField] public string Message { get; private set; }
        [field: SerializeField] public string TooltipId { get; private set; } = "default";
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public TooltipAnchor Anchor { get; private set; } = TooltipAnchor.Top;
        [field: SerializeField] public bool Outside { get; private set; } = true;
        [field: SerializeField] public bool IsStillVisbleOnPointerOverTooltip { get; private set; }
        [field: SerializeField] public Vector2 Offset { get; private set; } = Vector2.zero;
        [field: SerializeField] public float Duration { get; private set; } = 0f;
        [field: SerializeField] public float Delay { get; private set; } = 0f;

        public float? DurationOrNull => Duration > 0 ? Duration : null;
        public float? DelayOrNull => Delay > 0 ? Delay : null;

        public void OverrideMessage(string message) 
        { 
            Message = message; 
        }
    }
}