using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.LearningTool
{
    public class RulesAccordionItem : MonoBehaviour
    {
        [Header("Card / Header Elements")]
        [SerializeField] private Button headerButton;
        [SerializeField] private Image cardBackgroundImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI toggleIconText;
        [SerializeField] private Image toggleButtonImage;

        [Header("Content Elements")]
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private Image bannerContainerImage;
        [SerializeField] private Image bannerImage;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI rulesText;

        [Header("Styling")]
        [SerializeField] private Sprite normalCardSprite;
        [SerializeField] private Sprite activeCardSprite;

        public bool IsExpanded { get; private set; }
        public string ModeId { get; private set; }

        private Action<RulesAccordionItem> onToggleCallback;

        private void Awake()
        {
            if (headerButton != null)
            {
                headerButton.onClick.AddListener(OnHeaderClicked);
            }
        }

        public void BindToggleCallback(Action<RulesAccordionItem> onToggle)
        {
            onToggleCallback = onToggle;
        }

        public void Setup(
            string modeId,
            string title,
            Sprite banner,
            Sprite bannerBg,
            string description,
            string rules,
            Sprite normalBg,
            Sprite activeBg,
            Action<RulesAccordionItem> onToggle)
        {
            ModeId = modeId;
            normalCardSprite = normalBg;
            activeCardSprite = activeBg;
            onToggleCallback = onToggle;

            if (titleText != null)
                titleText.text = title;

            if (bannerContainerImage != null && bannerBg != null)
                bannerContainerImage.sprite = bannerBg;

            if (bannerImage != null && banner != null)
            {
                bannerImage.sprite = banner;
                bannerImage.preserveAspect = true;
            }

            if (descriptionText != null)
                descriptionText.text = description;

            if (rulesText != null)
                rulesText.text = FormatBulletRules(rules);

            // Default state: collapsed
            SetExpanded(false);
        }

        private void OnHeaderClicked()
        {
            onToggleCallback?.Invoke(this);
        }

        public void SetExpanded(bool expand)
        {
            IsExpanded = expand;

            if (contentContainer != null)
                contentContainer.gameObject.SetActive(expand);

            if (toggleIconText != null)
                toggleIconText.text = expand ? "-" : "+";

            if (cardBackgroundImage != null)
            {
                if (expand && activeCardSprite != null)
                    cardBackgroundImage.sprite = activeCardSprite;
                else if (!expand && normalCardSprite != null)
                    cardBackgroundImage.sprite = normalCardSprite;
            }

            LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
            if (transform.parent is RectTransform parentRt)
                LayoutRebuilder.MarkLayoutForRebuild(parentRt);
        }

        public void SetSelected(bool isSelected)
        {
            if (cardBackgroundImage != null)
            {
                cardBackgroundImage.sprite = (isSelected || IsExpanded) ? activeCardSprite : normalCardSprite;
            }
        }

        public static string FormatBulletRules(string rawRules)
        {
            if (string.IsNullOrEmpty(rawRules))
                return string.Empty;

            var normalized = rawRules.Replace("\r\n", "\n").Replace("\r", "\n");
            if (normalized.StartsWith("- "))
                normalized = normalized.Substring(2);
            else if (normalized.StartsWith("-"))
                normalized = normalized.Substring(1);

            var rawBullets = Regex.Split(normalized, @"\n\s*-\s*");
            var sb = new System.Text.StringBuilder();

            foreach (var bullet in rawBullets)
            {
                var cleaned = Regex.Replace(bullet.Trim(), @"\s+", " ");
                if (!string.IsNullOrEmpty(cleaned))
                {
                    if (sb.Length > 0)
                        sb.AppendLine("\n");
                    sb.Append("•  ").Append(cleaned);
                }
            }

            return sb.ToString();
        }
    }
}
