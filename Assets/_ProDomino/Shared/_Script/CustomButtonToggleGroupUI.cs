using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages a group of CustomButtonUI instances, allowing a limited number of buttons to be selected simultaneously,
/// while preserving the order of selection using fixed slots (nulls included).
/// </summary>
public class CustomButtonToggleGroupUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int maxSelectedButtons = 1; // Max number of buttons that can be selected at once
    [SerializeField] public bool isConfiguratingOnAwake = true; // Whether to auto-configure buttons on Awake

    [Header("State (Read-Only)")]
    [field: SerializeField] public bool isAtLeastOneButtonSelected { get; private set; } // Flag if any button is selected

    [SerializeField] private List<CustomButtonUI> externalButtons;

    /// <summary>
    /// Public read-only access to the selected buttons list (may contain nulls to preserve slot indices).
    /// </summary>
    public IReadOnlyList<CustomButtonUI> SelectedButtons => selectedButtons;

    // Internal collections
    private readonly List<CustomButtonUI> buttons = new(); // All buttons managed by this group
    private List<CustomButtonUI> selectedButtons = new(); // Slots for selected buttons

    // Callbacks
    private Action<List<string>> onCustomButtonsSelectedCallback;
    private Action<string> onCustomButtonSelectedCallback;

    /// <summary>
    /// Unity event that initializes the button group if configured to do so.
    /// </summary>
    private void Awake()
    {
        if (isConfiguratingOnAwake)
            Configure();
    }

    /// <summary>
    /// Finds and initializes all CustomButtonUI children (both toggleable and non-toggleable).
    /// </summary>
    public void Configure()
    {
        foreach (var button in GetComponentsInChildren<CustomButtonUI>(true))
        {
            if (buttons.Contains(button))
                continue;

            buttons.Add(button);
            button.Initialize(this);

            if (button.IsToggleable)
            {
                // Toggleable buttons subscribe to selection/deselection events
                button.onSelect.AddListener(() => OnToggleSelected(button));
                button.onDeselect.AddListener(() => OnToggleDeselected(button));
            } else
            {
                // Non-toggleable buttons use a simple click callback
                button.onClick.AddListener(() => OnButtonPress(button));
            }
        }

        foreach (CustomButtonUI button in externalButtons)
        {
            if (buttons.Contains(button))
                continue;

            buttons.Add(button);
            button.Initialize(this);

            if (button.IsToggleable)
            {
                // Toggleable buttons subscribe to selection/deselection events
                button.onSelect.AddListener(() => OnToggleSelected(button));
                button.onDeselect.AddListener(() => OnToggleDeselected(button));
            } else
            {
                // Non-toggleable buttons use a simple click callback
                button.onClick.AddListener(() => OnButtonPress(button));
            }
        }

        // Ensure slots are initialized
        SetSelectionLimit((byte)maxSelectedButtons);
    }

    /// <summary>
    /// Manually selects a button at a specific index, replacing any previously selected button at that slot.
    /// </summary>
    public bool SelectAtIndex(int index, CustomButtonUI button)
    {
        if (index < 0 || index >= maxSelectedButtons)
            return false;

        if (selectedButtons[index] != null)
            selectedButtons[index].Deselect();

        selectedButtons[index] = button;
        isAtLeastOneButtonSelected = selectedButtons.Any(b => b != null);

        InvokeCallback();
        return true;
    }

    /// <summary>
    /// Defines the maximum number of selectable buttons and ensures the slots are properly resized.
    /// </summary>
    public void SetSelectionLimit(byte maxSelection)
    {
        maxSelectedButtons = maxSelection;

        // Initialize or resize the fixed-size list with nulls
        if (selectedButtons == null)
        {
            selectedButtons = new List<CustomButtonUI>(new CustomButtonUI[maxSelection]);
        } 
        else
        {
            int current = selectedButtons.Count;

            if (current < maxSelection)
                selectedButtons.AddRange(Enumerable.Repeat<CustomButtonUI>(null, maxSelection - current));
            else if (current > maxSelection)
                selectedButtons = selectedButtons.Take(maxSelection).ToList();
        }
    }

    public void SetOneMandatorySelectionLimit(bool isAtLeastOneButtonSelected)
    {
        this.isAtLeastOneButtonSelected = isAtLeastOneButtonSelected;
    }

    /// <summary>
    /// Retrieves a button by its unique identifier.
    /// </summary>
    public CustomButtonUI GetButtonUI(string buttonID)
    {
        var button = buttons.FirstOrDefault(b => b.CustomButtonID == buttonID);
        if (button == null)
            Debug.LogWarning($"Button with ID '{buttonID}' not found.");

        return button;
    }

    /// <summary>
    /// Returns the first available button in the group.
    /// </summary>
    public CustomButtonUI GetFirstButtonUI()
    {
        var button = buttons.FirstOrDefault();
        if (button == null)
            Debug.LogWarning("First button not found.");

        return button;
    }

    /// <summary>
    /// Enables or disables interaction for all buttons in the group.
    /// </summary>
    /// <param name="isInteractable"></param>
    public void SetAllButtonsInteractable(bool isInteractable)
    {
        if (buttons is null or { Count: 0 })
            return;

        foreach (var button in buttons)
            button.SetButtonInteractable(isInteractable);
    }

    /// <summary>
    /// Sets a callback to receive the full list of selected button IDs.
    /// </summary>
    public void SetOnCustomButtonsSelectedCallback(Action<List<string>> callback)
    {
        onCustomButtonsSelectedCallback = callback;
    }

    /// <summary>
    /// Sets a callback to receive the first selected button ID.
    /// </summary>
    public void SetOnCustomButtonSelectedCallback(Action<string> callback)
    {
        onCustomButtonSelectedCallback = callback;
    }

    public bool CheckIfSelected(string customButtonId)
    {
        return SelectedButtons?.Any(x => x != null && x.CustomButtonID == customButtonId) ?? false;
    }

    /// <summary>
    /// Invokes all selection callbacks with the current state.
    /// </summary>
    private void InvokeCallback()
    {
        var selectedIDs = selectedButtons
            .Select(b => b?.CustomButtonID)
            .ToList();

        onCustomButtonsSelectedCallback?.Invoke(selectedIDs);
        onCustomButtonSelectedCallback?.Invoke(selectedIDs.FirstOrDefault());
    }

    /// <summary>
    /// Deselects all selected buttons and clears the internal selection slots.
    /// </summary>
    public void DeselectAll(bool isIgnoringCallback = false)
    {
        for (int i = 0; i < selectedButtons.Count; i++)
        {
            var button = selectedButtons[i];
            if (button != null)
            {
                button.Deselect();
                selectedButtons[i] = null;
            }
        }

        isAtLeastOneButtonSelected = false;
        if (!isIgnoringCallback)
            InvokeCallback();
    }

    /// <summary>
    /// Called when a non-toggleable button is pressed.
    /// </summary>
    private void OnButtonPress(CustomButtonUI button)
    {
        Debug.Log($"Pressed Button ID: {button.CustomButtonID}");

        onCustomButtonsSelectedCallback?.Invoke(new List<string> { button.CustomButtonID });
        onCustomButtonSelectedCallback?.Invoke(button.CustomButtonID);
    }

    /// <summary>
    /// Attempts to add a toggleable button to the first available selection slot.
    /// </summary>
    private void OnToggleSelected(CustomButtonUI button)
    {
        // Ignore if already selected
        if (selectedButtons.Contains(button))
            return;

        if (maxSelectedButtons is 1)
        {
            var buttonToDeselect = selectedButtons?.FirstOrDefault();
            buttonToDeselect?.Deselect();

            selectedButtons[0] = button;
        } 
        else
        {
            // Find first empty slot
            int indexToUse = selectedButtons.FindIndex(b => b == null);

            // No available slot
            if (indexToUse == -1)
            {
                Debug.LogWarning($"Selection limit reached ({maxSelectedButtons}). Please deselect a button before selecting '{button.CustomButtonID}'.");
                button.Deselect(); // Force unselect
                return;
            }
            selectedButtons[indexToUse] = button;
            
            Debug.Log($"Selected Toggle ID: {button.CustomButtonID} at slot {indexToUse}");
        }

        isAtLeastOneButtonSelected = selectedButtons.Any(b => b != null);
        InvokeCallback();
    }

    /// <summary>
    /// Called when a toggleable button is deselected manually.
    /// </summary>
    private void OnToggleDeselected(CustomButtonUI button)
    {
        int index = selectedButtons.IndexOf(button);
        if (index >= 0)
        {
            selectedButtons[index] = null;
            isAtLeastOneButtonSelected = selectedButtons.Any(b => b != null);

            InvokeCallback();
        }
    }
}
