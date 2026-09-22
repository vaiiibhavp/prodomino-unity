using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// Interactive FAQ Accordion item for the Help Center screen matching the Figma design:
    /// - Collapsed: Dark navy background (#0D1522), white text, '+' icon.
    /// - Expanded: Vibrant Gold gradient background (#FFA800 -> #FFC107), dark text, answer body, '−' icon.
    /// </summary>
    public class HelpAccordionItem : MonoBehaviour
    {
        [Header("Header / Card Elements")]
        [SerializeField] private Button headerButton;
        [SerializeField] private Image cardBackgroundImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image toggleButtonImage;
        [SerializeField] private TextMeshProUGUI toggleIconText;

        [Header("Answer Body Elements")]
        [SerializeField] private RectTransform answerContainer;
        [SerializeField] private TextMeshProUGUI answerText;

        [Header("Card Background Sprites")]
        [SerializeField] private Sprite normalCardSprite;
        [SerializeField] private Sprite activeCardSprite;

        [Header("Toggle Button Sprites")]
        [SerializeField] private Sprite normalToggleSprite;
        [SerializeField] private Sprite activeToggleSprite;

        [Header("Colors")]
        [SerializeField] private Color normalTitleColor = new Color(0.95f, 0.96f, 0.98f, 1f); // Crisp white/off-white
        [SerializeField] private Color activeTitleColor = new Color(0.06f, 0.09f, 0.16f, 1f); // Deep dark navy/black
        [SerializeField] private Color activeAnswerColor = new Color(0.12f, 0.16f, 0.23f, 1f); // Dark charcoal legible

        public bool IsExpanded { get; private set; }
        public int ItemIndex { get; private set; }

        private Action<HelpAccordionItem> onToggleCallback;

        private void Awake()
        {
            if (headerButton != null)
            {
                headerButton.onClick.AddListener(OnHeaderClicked);
            }
        }

        public void Setup(
            int index,
            string question,
            string answer,
            Sprite normalBg,
            Sprite activeBg,
            Sprite normalToggle,
            Sprite activeToggle,
            Action<HelpAccordionItem> onToggle,
            bool startExpanded = false)
        {
            ItemIndex = index;
            normalCardSprite = normalBg;
            activeCardSprite = activeBg;
            normalToggleSprite = normalToggle;
            activeToggleSprite = activeToggle;
            onToggleCallback = onToggle;

            if (titleText != null)
                titleText.text = question;

            if (answerText != null)
                answerText.text = answer;

            SetExpanded(startExpanded);
        }

        private void OnHeaderClicked()
        {
            onToggleCallback?.Invoke(this);
        }

        public void SetExpanded(bool expand)
        {
            IsExpanded = expand;

            if (answerContainer != null)
                answerContainer.gameObject.SetActive(expand);

            if (toggleIconText != null)
                toggleIconText.text = expand ? "-" : "+";

            if (cardBackgroundImage != null)
            {
                if (expand && activeCardSprite != null)
                    cardBackgroundImage.sprite = activeCardSprite;
                else if (!expand && normalCardSprite != null)
                    cardBackgroundImage.sprite = normalCardSprite;
            }

            if (toggleButtonImage != null)
            {
                if (expand && activeToggleSprite != null)
                    toggleButtonImage.sprite = activeToggleSprite;
                else if (!expand && normalToggleSprite != null)
                    toggleButtonImage.sprite = normalToggleSprite;
            }

            if (titleText != null)
                titleText.color = expand ? activeTitleColor : normalTitleColor;

            if (answerText != null)
                answerText.color = activeAnswerColor;

            // Rebuild layout for this item and parent container
            LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
            if (transform.parent is RectTransform parentRect)
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }
    }
}
