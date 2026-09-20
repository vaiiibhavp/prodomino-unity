using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProDomino.ClubSystem.ClubIconDataSelectorController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages selectable club icon and color entries in the UI, allowing initialization, selection, and callback
    /// handling for club customization.
    /// </summary>
    public class ClubDataSelectable : MonoBehaviour
    {
        [SerializeField] private ClubIconDataEntry textureUISelectorPrefab, colorUISelectorPrefab;
        [SerializeField] private Transform textureUISelectorParent, colorUISelectorParent;
        [SerializeField] private CustomButtonToggleGroupUI textureToggleGroup;
        [SerializeField] private CustomButtonToggleGroupUI colorToggleGroup;

        private List<ClubIconDataEntry> textureUISelectors, colorUISelectors;

        private Action<ClubIconDataEntry> onEntrySelected;

        private void Awake()
        {
            if (textureToggleGroup)
                textureToggleGroup.SetOnCustomButtonSelectedCallback(UISelectorSelected);

            if (colorToggleGroup)
                colorToggleGroup.SetOnCustomButtonSelectedCallback(UISelectorSelected);

            // When the game starts, get all existing texture selectors
            if (textureUISelectorParent)
            {
                textureUISelectors = textureUISelectorParent.GetComponentsInChildren<ClubIconDataEntry>(true)?.ToList() ?? new();
                if (textureToggleGroup != null)
                    foreach (var selector in textureUISelectors)
                        selector.Initalize(false);
            }

            // When the game starts, get all existing color selectors
            if (colorUISelectorParent)
            { 
                colorUISelectors = colorUISelectorParent.GetComponentsInChildren<ClubIconDataEntry>(true)?.ToList() ?? new();
                if (colorToggleGroup != null)
                    foreach (var selector in colorUISelectors)
                        selector.Initalize(true);
            }
        }

        /// <summary>
        /// Initialize the ClubDataSelectable with the given data
        /// </summary>
        /// <param name="onEntrySelected">Action to be called when an entry is selected.</param>
        /// <param name="clubDataSelectableType">The type of data selectable for the club.</param>
        /// <param name="spriteCollection">Optional collection of sprites for the selectable entries.</param>
        /// <param name="colorCollection">Optional collection of colors for the selectable entries.</param>
        internal void Initialize
            (Action<ClubIconDataEntry> onEntrySelected,
            ClubDataSelectableType clubDataSelectableType,
            Dictionary<string, Sprite> spriteCollection = null,
            Dictionary<string, Color?> colorCollection = null)
        {
            this.onEntrySelected = onEntrySelected;

            // Ensure enough texture selectors
            if (spriteCollection is not null and { Count: > 0 })
            { 
                if (textureUISelectors.Count < spriteCollection.Count)
                {
                    var diff = spriteCollection.Count - textureUISelectors.Count;
                    for (int i = 0; i < diff; i++)
                    {
                        var newUISelector = Instantiate(textureUISelectorPrefab, textureUISelectorParent);
                        newUISelector.Initalize(false);

                        textureUISelectors.Add(newUISelector);
                    }
                }

                // Configure each UI selector
                if (textureUISelectors.Count > 0)
                    for (int i = 0; i < textureUISelectors.Count; i++)
                    {
                        var textureUISelector = textureUISelectors[i];
                        if (i < spriteCollection.Count)
                        {
                            var entry = spriteCollection.ElementAt(i);
                            textureUISelector.Configure(clubDataSelectableType, entry.Key, sprite: entry.Value);
                            textureUISelector.gameObject.SetActive(true);
                        } 
                        else
                            textureUISelector.gameObject.SetActive(false);
                    }
            }

            // Ensure enough color selectors
            if (colorCollection is not null and { Count: > 0 })
            { 
                if (colorUISelectors.Count < colorCollection.Count)
                {
                    var diff = colorCollection.Count - colorUISelectors.Count;
                    for (int i = 0; i < diff; i++)
                    {
                        var newUISelector = Instantiate(colorUISelectorPrefab, colorUISelectorParent);
                        newUISelector.Initalize(true);

                        colorUISelectors.Add(newUISelector);
                    }
                }

                if (colorUISelectors.Count > 0)
                    for (int i = 0; i < colorUISelectors.Count; i++)
                    {
                        var colorUISelector = colorUISelectors[i];
                        if (i < colorCollection.Count)
                        {
                            var entry = colorCollection.ElementAt(i);
                            colorUISelector.Configure(clubDataSelectableType, entry.Key, color: entry.Value);
                            colorUISelector.gameObject.SetActive(true);
                        } else
                            colorUISelector.gameObject.SetActive(false);
                    }
            }
        }

        /// <summary>
        /// Invoked when a selector is selected. Finds the corresponding entry and invokes the onEntrySelected callback.
        /// </summary>
        /// <param name="selectedClubDataEntryId">The ID of the selected club data entry.</param>
        private void UISelectorSelected(string selectedClubDataEntryId)
        {
            // Check if is deselecting
            if (string.IsNullOrEmpty(selectedClubDataEntryId))
                return;

            // Find the selected entry
            var searchedEntry = (colorToggleGroup.GetButtonUI(selectedClubDataEntryId) 
                ?? textureToggleGroup.GetButtonUI(selectedClubDataEntryId))
                ?.GetComponent<ClubIconDataEntry>();

            // Try to invoke the callback if the entry was found
            if (searchedEntry)
                onEntrySelected?.Invoke(searchedEntry);
            else
                Debug.LogWarning($"Couldn't find the selected entry with id {selectedClubDataEntryId}");
        }

        /// <summary>
        /// Force toggle group selection for a specific entry.
        /// </summary>
        /// <param name="entryIdToSelect">The ID of the entry to select.</param>
        /// <param name="isColorSelector">Indicates whether the entry is a color selector.</param>
        /// <param name="isInvokingCallback">Indicates whether to invoke the callback when selecting the entry.</param>
        internal void ForceSelection(string entryIdToSelect, bool isColorSelector, bool isInvokingCallback = false)
        {
            // Check if is deselecting
            if (string.IsNullOrEmpty(entryIdToSelect))
                return;

            // Find the selected entry
            var selector = isColorSelector ? colorToggleGroup : textureToggleGroup;
            selector?.GetButtonUI(entryIdToSelect)?.Select(isInvokingCallback);
        }
    }
}
