using System;
using System.Collections.Generic;
using Timba.Database;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ProDomino.Shared
{
    /// <summary>
    /// UI panel for displaying the Help Center screen.
    /// Manages interactive FAQ accordion items loaded dynamically from HelpScreenDatabase.
    /// Default state: hidden until explicitly activated by NavigationPanelController.
    /// </summary>
    public class HelpScreenUI : MonoBehaviour, INavigationPanel
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        public CanvasGroup RootCanvasGroup
        {
            get
            {
                if (rootCanvasGroup == null)
                    rootCanvasGroup = GetComponent<CanvasGroup>();
                return rootCanvasGroup;
            }
        }

        public NavigationPanelType NavigationPanelType => NavigationPanelType.HelpScreen;

        public bool RequiresAuthentication => false;

        [Header("Database & Item Setup")]
        [SerializeField] private HelpScreenDatabase helpScreenDatabase;
        [SerializeField] private HelpAccordionItem helpAccordionItemPrefab;
        [SerializeField] private Transform questionParent;

        [Header("Accordion Item Sprites")]
        [SerializeField] private Sprite normalCardSprite;
        [SerializeField] private Sprite activeCardSprite;
        [SerializeField] private Sprite normalToggleSprite;
        [SerializeField] private Sprite activeToggleSprite;

        private readonly List<HelpAccordionItem> activeItems = new List<HelpAccordionItem>();

        private void Awake()
        {
            // By default, the Help screen must be completely hidden so Dashboard is shown
            HidePanel();
        }

        private void HidePanel()
        {
            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.alpha = 0f;
                RootCanvasGroup.interactable = false;
                RootCanvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        private void ShowPanel()
        {
            gameObject.SetActive(true);
            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.alpha = 1f;
                RootCanvasGroup.interactable = true;
                RootCanvasGroup.blocksRaycasts = true;
                RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
            }
            LoadDatabase();
        }

        /// <summary>
        /// Loads help screen entries from the database and populates the UI.
        /// </summary>
        public void LoadDatabase()
        {
            if (helpScreenDatabase == null || questionParent == null || helpAccordionItemPrefab == null)
            {
                // Try to find components dynamically if serialized references were lost
                if (questionParent == null)
                {
                    var scroll = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
                    if (scroll != null && scroll.content != null)
                        questionParent = scroll.content;
                }
                if (helpScreenDatabase == null || questionParent == null || helpAccordionItemPrefab == null)
                    return;
            }

            // Clear any existing items
            foreach (Transform child in questionParent)
            {
                Destroy(child.gameObject);
            }
            activeItems.Clear();

            // Determine language properly (fix previously inverted logic)
            string lang = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : "en";
            bool isSpanish = lang.StartsWith("es", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < helpScreenDatabase.Items.Count; i++)
            {
                var data = helpScreenDatabase.Items[i];
                string question = isSpanish ? data.title_es : data.title_en;
                string answer = isSpanish ? data.description_es : data.description_en;

                if (string.IsNullOrEmpty(question))
                    question = data.title_en;
                if (string.IsNullOrEmpty(answer))
                    answer = data.description_en;

                var instance = Instantiate(helpAccordionItemPrefab, questionParent);
                // First item starts expanded by default matching Figma reference
                bool startExpanded = (i == 0);

                instance.Setup(
                    i,
                    question,
                    answer,
                    normalCardSprite,
                    activeCardSprite,
                    normalToggleSprite,
                    activeToggleSprite,
                    OnItemToggled,
                    startExpanded);

                activeItems.Add(instance);
            }
        }

        private void OnItemToggled(HelpAccordionItem toggledItem)
        {
            bool willExpand = !toggledItem.IsExpanded;

            // Accordion behavior: only one item expanded at a time
            foreach (var item in activeItems)
            {
                if (item == toggledItem)
                {
                    item.SetExpanded(willExpand);
                }
                else
                {
                    item.SetExpanded(false);
                }
            }
        }

        /// <summary>
        /// Sets the active state of the navigation panel.
        /// Called by NavigationPanelController when user clicks navigation buttons.
        /// </summary>
        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            if (isActive)
            {
                ShowPanel();
            }
            else
            {
                HidePanel();
            }
        }

        // Editor support methods
        public void SetReferences(
            HelpScreenDatabase db,
            HelpAccordionItem prefab,
            Transform parent,
            Sprite normalCard,
            Sprite activeCard,
            Sprite normalToggle,
            Sprite activeToggle)
        {
            helpScreenDatabase = db;
            helpAccordionItemPrefab = prefab;
            questionParent = parent;
            normalCardSprite = normalCard;
            activeCardSprite = activeCard;
            normalToggleSprite = normalToggle;
            activeToggleSprite = activeToggle;
        }

        public IReadOnlyList<HelpAccordionItem> GetActiveItems() => activeItems;
    }
}
