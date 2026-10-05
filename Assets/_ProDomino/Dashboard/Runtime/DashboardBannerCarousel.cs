using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProDomino.Dashboard
{
    /// <summary>
    /// Slides the dashboard challenge banner through a list of slides. It cross-fades the background
    /// image, swaps the title/subtitle copy and keeps the pagination dots in sync. The banner also
    /// answers horizontal drags so the player can swipe between slides manually.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashboardBannerCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Serializable]
        public struct Slide
        {
            public Sprite background;
            public string title;
            public string subtitle;

            [Tooltip("Shows the progress row (bar, count and reward chip) for this slide.")]
            public bool showProgress;
            [Range(0f, 1f)] public float progress;
            public string progressText;
            public string rewardText;
        }

        [Header("Slides")]
        [SerializeField] private List<Slide> slides = new();

        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [Tooltip("Parent of the pagination dots. Each child is one dot.")]
        [SerializeField] private Transform paginationDots;

        [Header("Progress")]
        [SerializeField] private GameObject progressRow;
        [Tooltip("Fill of the progress track. Its anchorMax.x is driven by the slide progress.")]
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text rewardLabel;

        [Header("Behaviour")]
        [Tooltip("Seconds each slide stays on screen. Set to 0 to disable auto sliding.")]
        [SerializeField] private float slideDuration = 5f;
        [Tooltip("Seconds the cross-fade between two slides takes.")]
        [SerializeField] private float fadeDuration = 0.35f;
        [Tooltip("Horizontal drag distance, in pixels, needed to change slide.")]
        [SerializeField] private float swipeThreshold = 60f;

        [Header("Dots")]
        [SerializeField] private Color activeDotColor = new(0.99215686f, 0.77254903f, 0.3254902f, 1f);
        [SerializeField] private Color inactiveDotColor = new(0.003921569f, 0.003921569f, 0.047058824f, 0.7f);
        [SerializeField] private float activeDotSize = 12f;
        [SerializeField] private float inactiveDotSize = 10f;

        private readonly List<Image> dotImages = new();
        private int currentIndex;
        private float slideTimer;
        private float fadeTimer;
        private bool isFading;
        private bool isDragging;
        private float dragDelta;
        private Sprite fadeFromSprite;
        private List<Slide> defaultSlides;
        private string defaultTitle;
        private string defaultSubtitle;

        /// <summary>
        /// Amount of slides currently configured.
        /// </summary>
        public int SlideCount => slides.Count;

        private void Awake()
        {
            CacheDefaults();
            CacheDots();
        }

        // Slides and copy authored in the prefab, restored by ResetSlides
        private void CacheDefaults()
        {
            if (defaultSlides != null)
                return;

            defaultSlides = new List<Slide>(slides);
            defaultTitle = titleLabel ? titleLabel.text : null;
            defaultSubtitle = subtitleLabel ? subtitleLabel.text : null;
        }

        private void OnEnable()
        {
            currentIndex = 0;
            slideTimer = 0f;
            isFading = false;
            ApplySlide(currentIndex, isImmediate: true);
        }

        private void Update()
        {
            if (isFading)
                UpdateFade();

            if (isDragging || slides.Count < 2 || slideDuration <= 0f)
                return;

            slideTimer += Time.unscaledDeltaTime;
            if (slideTimer < slideDuration)
                return;

            slideTimer = 0f;
            GoToSlide(currentIndex + 1);
        }

        /// <summary>
        /// Shows the slide at the given index. The index wraps around both ends.
        /// </summary>
        public void GoToSlide(int index)
        {
            if (slides.Count is 0)
                return;

            currentIndex = ((index % slides.Count) + slides.Count) % slides.Count;
            slideTimer = 0f;
            ApplySlide(currentIndex, isImmediate: false);
        }

        /// <summary>
        /// Replaces the configured slides at runtime, for example with remote content.
        /// </summary>
        public void SetSlides(IEnumerable<Slide> newSlides)
        {
            slides.Clear();
            if (newSlides != null)
                slides.AddRange(newSlides);

            currentIndex = 0;
            slideTimer = 0f;
            ApplySlide(currentIndex, isImmediate: true);
        }

        /// <summary>
        /// Replaces the slide content but keeps the current slide and timer, so live data
        /// (for example challenge progress) can refresh without restarting the carousel.
        /// </summary>
        public void UpdateSlides(IReadOnlyList<Slide> newSlides)
        {
            slides.Clear();
            if (newSlides != null)
                slides.AddRange(newSlides);

            if (slides.Count > 0)
                currentIndex = Mathf.Clamp(currentIndex, 0, slides.Count - 1);

            ApplySlide(currentIndex, isImmediate: false);
        }

        /// <summary>
        /// Restores the slides authored in the prefab.
        /// </summary>
        public void ResetSlides()
        {
            CacheDefaults();
            SetSlides(defaultSlides);
        }

        private void ApplySlide(int index, bool isImmediate)
        {
            SyncDots();

            if (slides.Count is 0 || index < 0 || index >= slides.Count)
            {
                SetProgressVisible(false);
                return;
            }

            var slide = slides[index];

            if (titleLabel)
            {
                var title = string.IsNullOrEmpty(slide.title) ? defaultTitle : slide.title;
                if (!string.IsNullOrEmpty(title))
                    titleLabel.text = title;
            }

            if (subtitleLabel)
            {
                var subtitle = string.IsNullOrEmpty(slide.subtitle) ? defaultSubtitle : slide.subtitle;
                if (!string.IsNullOrEmpty(subtitle))
                    subtitleLabel.text = subtitle;
            }

            ApplyProgress(slide);

            // A running fade towards this same sprite keeps going (live data refresh)
            if (isFading && backgroundImage && backgroundImage.sprite == slide.background)
                return;

            if (backgroundImage && slide.background)
            {
                if (isImmediate || fadeDuration <= 0f)
                {
                    backgroundImage.sprite = slide.background;
                    SetBackgroundAlpha(1f);
                    isFading = false;
                }
                else
                {
                    fadeFromSprite = backgroundImage.sprite;
                    backgroundImage.sprite = slide.background;
                    fadeTimer = 0f;
                    isFading = fadeFromSprite != slide.background;
                    SetBackgroundAlpha(isFading ? 0f : 1f);
                }

                MatchBackgroundAspect(slide.background);
            }
        }

        // Slides use art of different aspect ratios. The envelope fitter must follow the current sprite, or the art stretches and crops
        private void MatchBackgroundAspect(Sprite sprite)
        {
            if (!backgroundImage.TryGetComponent<AspectRatioFitter>(out var fitter))
                return;

            var rect = sprite.rect;
            if (rect.height > 0f)
                fitter.aspectRatio = rect.width / rect.height;
        }

        private void ApplyProgress(Slide slide)
        {
            SetProgressVisible(slide.showProgress);
            if (!slide.showProgress)
                return;

            if (progressFill)
            {
                var anchorMax = progressFill.anchorMax;
                anchorMax.x = Mathf.Clamp01(slide.progress);
                progressFill.anchorMax = anchorMax;
            }

            if (progressLabel)
                progressLabel.text = slide.progressText ?? string.Empty;

            if (rewardLabel)
                rewardLabel.text = slide.rewardText ?? string.Empty;
        }

        private void SetProgressVisible(bool isVisible)
        {
            if (progressRow && progressRow.activeSelf != isVisible)
                progressRow.SetActive(isVisible);
        }

        private void UpdateFade()
        {
            fadeTimer += Time.unscaledDeltaTime;
            var progress = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeDuration);
            SetBackgroundAlpha(progress);

            if (progress >= 1f)
                isFading = false;
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (!backgroundImage)
                return;

            var color = backgroundImage.color;
            color.a = alpha;
            backgroundImage.color = color;
        }

        private void CacheDots()
        {
            dotImages.Clear();

            if (!paginationDots)
                return;

            for (var i = 0; i < paginationDots.childCount; i++)
            {
                var dot = paginationDots.GetChild(i).GetComponent<Image>();
                if (dot)
                    dotImages.Add(dot);
            }
        }

        private void SyncDots()
        {
            if (dotImages.Count is 0)
                CacheDots();

            for (var i = 0; i < dotImages.Count; i++)
            {
                var dot = dotImages[i];
                if (!dot)
                    continue;

                // Only keep as many dots as there are slides
                dot.gameObject.SetActive(i < slides.Count);

                var isActive = i == currentIndex;
                dot.color = isActive ? activeDotColor : inactiveDotColor;

                var size = isActive ? activeDotSize : inactiveDotSize;
                if (size > 0f)
                    dot.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (slides.Count < 2)
                return;

            isDragging = true;
            dragDelta = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            dragDelta += eventData.delta.x;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            isDragging = false;

            if (Mathf.Abs(dragDelta) < swipeThreshold)
                return;

            // Dragging to the left shows the next slide
            GoToSlide(currentIndex + (dragDelta < 0f ? 1 : -1));
        }
    }
}
