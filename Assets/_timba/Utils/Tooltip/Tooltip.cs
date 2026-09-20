using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Timba;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Handles the visual display and configuration of a tooltip UI element.
/// </summary>
public class Tooltip : MonoBehaviour, IPointerExitHandler
{
    [Header("Visual Elements")]
    [SerializeField] protected TMP_Text tooltipText;        // The text component used to show the tooltip message
    [SerializeField] protected Image iconImage;         // The optional icon image next to the text
    [SerializeField] protected CanvasGroup canvasGroup; // Used for fade animation

    [Header("Tooltip Identifier")]
    [SerializeField] protected string tooltipId = "default"; // Unique ID used by TooltipController to identify this type

    private Action<Tooltip> onPointerExitAction; // Action to call when the pointer exits the tooltip
    private bool isInitialized = false; // Flag to check if the tooltip has been initialized
    /// <summary>
    /// The RectTransform component of this tooltip, used for positioning and sizing.
    /// </summary>
    public RectTransform RectTransform => (RectTransform)transform;

    /// <summary>
    /// The ID that identifies the type of this tooltip.
    /// </summary>
    public string TooltipId => tooltipId;

    /// <summary>
    /// CanvasGroup component used for fade animations and visibility control.
    /// </summary>
    public CanvasGroup CanvasGroup => canvasGroup;

    public virtual void Initialize(Action<Tooltip> onPointerExitAction)
    {
        if (isInitialized)
            return;

        this.onPointerExitAction = onPointerExitAction ?? throw new ArgumentNullException(nameof(onPointerExitAction));
    }

    /// <summary>
    /// Configures the tooltip content before displaying it.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="icon">The optional icon to show.</param>
    public virtual void Configure(string message = null, Sprite icon = null)
    {
        if (tooltipText != null)
            tooltipText.text = message ?? "";

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        // Ensure the tooltip is active and ready to be displayed
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        HideImmediate();
        transform.RefreshLayoutGroupsImmediateAndRecursive();
        transform.RefreshContentSizeFitterImmediateAndRecursive(this);
    }

    /// <summary>
    /// Immediately hides the tooltip and resets its visual state.
    /// </summary>
    public virtual void HideImmediate()
    {
        if (CanvasGroup != null)
            CanvasGroup.SetActive(false);

        transform.localScale = Vector3.zero;
    }

    /// <summary>
    /// Called when the pointer exits the tooltip area.
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExitAction?.Invoke(this);
    }
}

