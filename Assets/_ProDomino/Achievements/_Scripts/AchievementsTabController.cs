using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Manages the tab switching between [ Achievements ] and [ Challenges ] on Achievements_Screen,
    /// updating tab buttons, summary cards (Achievements stats vs Daily/Weekly/Monthly Challenges),
    /// column header titles ("Achievement Points" vs "Reward"), and table content.
    /// </summary>
    public class AchievementsTabController : MonoBehaviour
    {
        [Header("Tab Buttons")]
        [SerializeField] private Button achievementsTabButton;
        [SerializeField] private Button challengesTabButton;

        [Header("Tab Visuals")]
        [SerializeField] private Image achievementsTabBg;
        [SerializeField] private TMP_Text achievementsTabLabel;
        [SerializeField] private Image challengesTabBg;
        [SerializeField] private TMP_Text challengesTabLabel;
        [SerializeField] private Sprite activeTabSprite;
        [SerializeField] private Sprite inactiveTabSprite;
        [SerializeField] private Color activeTextColor = Color.white;
        [SerializeField] private Color inactiveTextColor = new Color(0.392f, 0.455f, 0.545f, 1f); // #64748B

        [Header("Card Containers")]
        [SerializeField] private GameObject achievementsCardsContainer;
        [SerializeField] private GameObject challengesCardsContainer;

        [Header("Table Headers")]
        [SerializeField] private TMP_Text colInfoLabel;
        [SerializeField] private TMP_Text colRewardLabel;

        [Header("Table Content")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject achievementsTableContent;
        [SerializeField] private GameObject challengesTableContent;

        public enum Tab { Achievements, Challenges }
        public Tab CurrentTab { get; private set; } = Tab.Achievements;

        private void Awake()
        {
            if (achievementsTabButton != null)
                achievementsTabButton.onClick.AddListener(() => SwitchTab(Tab.Achievements));

            if (challengesTabButton != null)
                challengesTabButton.onClick.AddListener(() => SwitchTab(Tab.Challenges));
        }

        private void Start()
        {
            SwitchTab(Tab.Achievements);
        }

        public void SwitchTab(Tab tab)
        {
            CurrentTab = tab;
            bool isAchiev = tab == Tab.Achievements;

            if (achievementsTabBg != null && activeTabSprite != null && inactiveTabSprite != null)
                achievementsTabBg.sprite = isAchiev ? activeTabSprite : inactiveTabSprite;
            if (achievementsTabLabel != null)
                achievementsTabLabel.color = isAchiev ? activeTextColor : inactiveTextColor;

            if (challengesTabBg != null && activeTabSprite != null && inactiveTabSprite != null)
                challengesTabBg.sprite = isAchiev ? inactiveTabSprite : activeTabSprite;
            if (challengesTabLabel != null)
                challengesTabLabel.color = isAchiev ? inactiveTextColor : activeTextColor;

            if (achievementsCardsContainer != null)
                achievementsCardsContainer.SetActive(isAchiev);
            if (challengesCardsContainer != null)
                challengesCardsContainer.SetActive(!isAchiev);

            if (colInfoLabel != null)
                colInfoLabel.text = isAchiev ? "Achievement Info" : "Challenge Info";
            if (colRewardLabel != null)
                colRewardLabel.text = isAchiev ? "Achievement Points" : "Reward";

            if (achievementsTableContent != null)
                achievementsTableContent.SetActive(isAchiev);
            if (challengesTableContent != null)
                challengesTableContent.SetActive(!isAchiev);

            if (scrollRect != null)
            {
                var targetContent = isAchiev ? achievementsTableContent : challengesTableContent;
                if (targetContent != null)
                    scrollRect.content = targetContent.GetComponent<RectTransform>();
            }
        }
    }
}
