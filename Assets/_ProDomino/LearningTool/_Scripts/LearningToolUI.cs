using ProDomino.Shared;
using System.Collections.Generic;
using Timba.Database;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.LearningTool.LearningToolManager;

namespace ProDomino.LearningTool
{
    /// <summary>
    /// UI panel for displaying game rules and learning tool information in Figma accordion format.
    /// </summary>
    public class LearningToolUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Learn;
        public bool RequiresAuthentication => false;

        [Header("Data")]
        [SerializeField] private LearningToolsDatabase learningToolsGameModeData;

        [Header("Accordion Items")]
        [SerializeField] private RectTransform accordionContainer;
        [SerializeField] private List<RulesAccordionItem> accordionItems = new List<RulesAccordionItem>();
        [SerializeField] private ScrollRect scrollRect;

        private void Awake()
        {
            InitItems();
        }

        private void Start()
        {
            InitItems();
            // Default to expanding the first item (e.g. Block)
            if (accordionItems != null && accordionItems.Count > 0)
            {
                ExpandItem(accordionItems[0]);
            }
        }

        private void InitItems()
        {
            if (accordionItems == null || accordionItems.Count == 0)
            {
                accordionItems = new List<RulesAccordionItem>(GetComponentsInChildren<RulesAccordionItem>(true));
            }
            foreach (var item in accordionItems)
            {
                if (item != null)
                {
                    item.BindToggleCallback(OnItemHeaderClicked);
                }
            }
        }

        /// <summary>
        /// Activates or deactivates the navigation panel.
        /// </summary>
        public void SetActiveNavigationPanel(bool isActive)
        {
            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.SetActive(isActive);
                RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
            }

            if (isActive)
            {
                InitItems();
                if (accordionItems != null && accordionItems.Count > 0)
                {
                    bool hasExpanded = false;
                    foreach (var it in accordionItems)
                    {
                        if (it != null && it.IsExpanded) { hasExpanded = true; break; }
                    }
                    if (!hasExpanded)
                    {
                        ExpandItem(accordionItems[0]);
                    }
                }

                if (scrollRect != null)
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        /// <summary>
        /// Registers and sets up an accordion item.
        /// </summary>
        public void RegisterAccordionItem(RulesAccordionItem item)
        {
            if (item != null && !accordionItems.Contains(item))
            {
                accordionItems.Add(item);
            }
        }

        /// <summary>
        /// Called when an accordion item header is clicked.
        /// Toggles that item and collapses other items.
        /// </summary>
        public void OnItemHeaderClicked(RulesAccordionItem clickedItem)
        {
            if (clickedItem == null) return;

            if (clickedItem.IsExpanded)
            {
                clickedItem.SetExpanded(false);
            }
            else
            {
                ExpandItem(clickedItem);
            }

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// Expands the specified item and collapses all others.
        /// </summary>
        public void ExpandItem(RulesAccordionItem targetItem)
        {
            foreach (var item in accordionItems)
            {
                if (item != null)
                {
                    item.SetExpanded(item == targetItem);
                }
            }
        }

        /// <summary>
        /// Expands a game mode by its string ID (e.g. "block", "concentrate").
        /// </summary>
        public void ExpandModeById(string modeId)
        {
            foreach (var item in accordionItems)
            {
                if (item != null && string.Equals(item.ModeId, modeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    ExpandItem(item);
                    break;
                }
            }
        }
    }
}
