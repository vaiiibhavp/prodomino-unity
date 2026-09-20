using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Linq;

// A customizable UI button that can function as a simple button or a selectable button.
// It changes colors and toggles a CanvasGroup when selected/deselected/hovered.
public class CustomButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Behavior")]
    [Tooltip("If true, the button can be toggled between selected and deselected states. If false, it acts as a normal button.")]

    [SerializeField] private bool isInteractable = true;
    public bool IsInteractable => isInteractable;

    [SerializeField] private bool isToggleable = true;
    public bool IsToggleable => isToggleable;

    [SerializeField] private bool blockClickHandler = false;
    public bool BlockClickHandler => blockClickHandler;

    [SerializeField] private string toggleID = "";
    public string CustomButtonID => toggleID;
    [SerializeField] GameObject tooltipContainer;
    public GameObject TooltipContainer => tooltipContainer;

    [Header("UI References")]
    [Tooltip("Canvas Group that will be toggled when the button is selected/deselected.")]
    [SerializeField] protected CanvasGroup canvasGroup_toggle;

    [Tooltip("Canvas Group that will be toggled when the button is hovered.")]
    [SerializeField] protected CanvasGroup canvasGroup_hover;

    [Tooltip("List of TextMeshPro elements that will change color when selected/deselected.")]
    [SerializeField] private List<TextMeshProUGUI> textElements_toggle;

    [Tooltip("List of Image elements that will change color when selected/deselected.")]
    [SerializeField] private List<Image> imageElements_toggle;

    [Header("Colors")]
    [Tooltip("Color to apply when the button is selected.")]
    [SerializeField] private Color selectedColor = Color.green;

    [Tooltip("Color to apply when the button is deselected.")]
    [SerializeField] private Color deselectedColor = Color.white;

    [Tooltip("Color to apply when the button is hovered.")]
    [SerializeField] private Color hoverColor = Color.yellow;

    [Header("Image States")]
    public List<ImageStates> imageStates = new List<ImageStates>();

    [Header("Events")]
    [Tooltip("Event triggered when the button is clicked.")]
    public UnityEvent onClick;

    [Tooltip("Event triggered when the button is selected.")]
    public UnityEvent onSelect;

    [Tooltip("Event triggered when the button is deselected.")]
    public UnityEvent onDeselect;

    protected Button button;
    public Button Button => button = button != null 
        ? button 
        : GetComponent<Button>();

    private CanvasGroup canvasGroup;
    private CustomButtonToggleGroupUI customButtonToggleGroupUI;

    public bool IsSelected { get; private set; }
    public bool IsPointerOver { get; private set; }
    public bool IsInteractableByDefault { get; private set; }
    public float Alpha
    {
        get => canvasGroup != null ? canvasGroup.alpha : 1f;
        set
        {
            if (canvasGroup != null)
                canvasGroup.alpha = value;
        }
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }

        if (canvasGroup_toggle != null)
            canvasGroup_toggle.alpha = 0;

        if (canvasGroup_hover != null)
            canvasGroup_hover.alpha = 0;

        IsInteractableByDefault = IsInteractable;

        SetButtonInteractable(isInteractable);
        SetupImageStates();
        Deselect(false);
    }

    public void Initialize(CustomButtonToggleGroupUI customButtonToggleGroupUI)
    {
        this.customButtonToggleGroupUI = customButtonToggleGroupUI;
    }

    public void SetCustomButtonID(string toggleID)
    {
        this.toggleID = toggleID;
    }

    public string GetMainText()
    {
        if (textElements_toggle is not null and { Count: > 0 })
            return textElements_toggle[0]?.text;

        return null;
    }
    
    public void SetMainText(string text)
    {
        if (textElements_toggle is not null and { Count: > 0 })
        {
            var mainText = textElements_toggle[0];
            if (mainText != null)
                mainText.text = text;

            else
                Debug.LogWarning($"Main text element is null in {gameObject.name}");
        }
    }

    // Handles the button click event.
    // Toggles selection state if toggleable, otherwise just invokes onClick.
    public void HandleClick()
    {
        if (button is { interactable: false } || BlockClickHandler)
            return;

        if (isToggleable)
        {
            // If the button is part of a toggle group, the group was marked to preserver at least one button active and it is already selected, do nothing.
            if (customButtonToggleGroupUI is not null and { isAtLeastOneButtonSelected: true }
                && IsSelected
                && customButtonToggleGroupUI.SelectedButtons.Count is 1
                && customButtonToggleGroupUI.SelectedButtons.Contains(this))
                return;

            if (IsSelected)
            {
                Deselect();
            }
            else
            {
                Select();
            }
        }
        onClick?.Invoke();
    }

    // Called when the button is pressed down (only for non-toggleable mode).
    public void OnPointerDown(PointerEventData eventData)
    {
        if (button is { interactable: false })
            return;

        if (!isToggleable)
        {
            UpdateVisuals(selectedColor);

            if (canvasGroup_toggle != null)
                canvasGroup_toggle.alpha = 1;

            if (canvasGroup_hover != null)
                canvasGroup_hover.alpha = 0;

            SelectedImageStates();
        }
    }

    // Called when the button is released (only for non-toggleable mode).
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isToggleable)
        {
            UpdateVisuals(deselectedColor);

            if (canvasGroup_toggle != null)
                canvasGroup_toggle.alpha = 0;

            if (canvasGroup_hover != null && IsPointerOver)
            {
                UpdateVisuals(hoverColor);
                canvasGroup_hover.alpha = 1;
            }

            DeselectedImageStates();
        }
    }

    // Called when the pointer enters the button area (hover effect).
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button is { interactable: false })
            return;

        IsPointerOver = true;

        if (isToggleable && IsSelected)
        {
            return;
        }

        UpdateVisuals(hoverColor);
        if (canvasGroup_hover != null)
        {
            canvasGroup_hover.alpha = 1;
        }

        HoverImageStates();
    }

    // Called when the pointer exits the button area (removes hover effect).
    public void OnPointerExit(PointerEventData eventData)
    {
        IsPointerOver = false;

        if (isToggleable && IsSelected)
        {
            return;
        }

        UpdateVisuals(IsSelected ? selectedColor : deselectedColor);
        if (canvasGroup_hover != null)
        {
            canvasGroup_hover.alpha = 0;
        }
        
        if (IsSelected)
        {
            SelectedImageStates();
        }
        else
        {
            DefaultImageStates();
        }
    }

    // Selects the button, changes colors, and enables the CanvasGroup.
    public void Select(bool isCallingEvent = true)
    {
        IsSelected = true;
        UpdateVisuals(selectedColor);
        if (canvasGroup_toggle != null)
        {
            canvasGroup_toggle.alpha = 1;
        }

        if (canvasGroup_hover != null)
        {
            canvasGroup_hover.alpha = 0;
        }

        SelectedImageStates();

        if (isCallingEvent)
            onSelect?.Invoke();
    }

    // Deselects the button, changes colors, and disables the CanvasGroup.
    public void Deselect(bool isCallingEvent = true)
    {
        IsSelected = false;
        UpdateVisuals(deselectedColor);
        if (canvasGroup_toggle != null)
        {
            canvasGroup_toggle.alpha = 0;
        }

        if (canvasGroup_hover != null)
        {
            canvasGroup_hover.alpha = 0;
        }

        DeselectedImageStates();

        if (isCallingEvent)
            onDeselect?.Invoke();
    }

    // Updates the visuals of the button by changing text and image colors.
    private void UpdateVisuals(Color color)
    {
        foreach (var text in textElements_toggle)
        {
            if (text != null)
                text.color = color;
        }
        foreach (var image in imageElements_toggle)
        {
            if (image != null)
                image.color = color;
        }
    }

    public void SetButtonInteractable(bool interactable, float auxAlpha = 0.5f)
    {
        // If the button is not interactable by default, do nothing.
        if (!IsInteractableByDefault)
            interactable = false;

        isInteractable = interactable;
        if (button != null)
        {
            button.interactable = isInteractable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = isInteractable;
            canvasGroup.alpha = isInteractable ? 1f : auxAlpha;
        }
    }

    public void SetButtonActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    public void SetButtonInteractableWithAlphaFull(bool interactable)
    {
        isInteractable = interactable;
        if (button != null)
        {
            button.interactable = isInteractable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = isInteractable;
            canvasGroup.alpha = 1;
        }
    }

    public void AddEventToListener(UnityAction action)
    {
        button.onClick.AddListener(action);
    }

    private void SetupImageStates()
    {
        foreach (ImageStates state in imageStates)
        {
            if (state.image != null)
            {
                state.OriginalAsset = state.image.sprite;
                state.OriginalColor = state.image.color;
            }
        }
    }

    private void SelectedImageStates()
    {
        foreach (ImageStates state in imageStates)
        {
            if (state != null && state.image != null)
            {
                state.Selected();
            }
        }
    }

    private void DeselectedImageStates()
    {
        foreach (ImageStates state in imageStates)
        {
            if (state != null && state.image != null)
            {
                state.Deselected();
            }
        }
    }

    private void HoverImageStates()
    {
        foreach (ImageStates state in imageStates)
        {
            if (state != null && state.image != null)
            {
                state.Hover();
            }
        }
    }

    private void DefaultImageStates()
    {
        foreach (ImageStates state in imageStates)
        {
            if (state != null && state.image != null)
            {
                state.Default();
            }
        }
    }

    /// <summary>
    /// Change the visual state of the button (without triggering events or changing internal state).
    /// Ideal for previewing the button state without affecting its actual selection state.
    /// </summary>
    public void PreviewVisualState(bool selected)
    {
        Color targetColor = selected ? selectedColor : deselectedColor;

        // Update the visuals without changing the internal state
        UpdateVisuals(targetColor);

        // Update the image states based on the selected state
        if (selected)
        {
            SelectedImageStates();
            if (canvasGroup_toggle != null)
                canvasGroup_toggle.alpha = 1;
        } else
        {
            DeselectedImageStates();
            if (canvasGroup_toggle != null)
                canvasGroup_toggle.alpha = 0;
        }

        // If not selected, hide the hover state
        if (canvasGroup_hover != null)
            canvasGroup_hover.alpha = 0;
    }
}

[System.Serializable]
public class ImageStates
{
    public Image image;
    public Sprite selectedStateAsset;
    public Color selectedStateColor = Color.white;
    public Sprite deselectedStateAsset;
    public Color deselectedStateColor = Color.white;
    public Sprite hoverStateAsset;
    public Color hoverStateColor = Color.white;
    private Sprite originalAsset;
    public Sprite OriginalAsset
    {
        get => originalAsset;
        set => originalAsset = value;
    }
    private Color originalColor;
    public Color OriginalColor
    {
        get => originalColor;
        set => originalColor = value;
    }

    public void Selected()
    {
        image.sprite = selectedStateAsset != null ? selectedStateAsset : originalAsset;
        image.color = selectedStateColor;
    }

    public void Deselected()
    {
        image.sprite = deselectedStateAsset != null ? deselectedStateAsset : originalAsset;
        image.color = deselectedStateColor;
    }

    public void Hover()
    {
        image.sprite = hoverStateAsset != null ? hoverStateAsset : originalAsset;
        image.color = hoverStateColor;
    }

    public void Default()
    {
        image.sprite = originalAsset;
        image.color = originalColor;
    }
}
