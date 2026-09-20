using Cysharp.Threading.Tasks;
using ProDomino.Shared;
using UnityEngine;

namespace ProDomino.GameSystem
{
    public class FullScreenController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup backgroundCanvasGroup;
        [SerializeField] private RectTransform innerScreenRectTransform;
        [SerializeField] private Transform layoutReference;

        // Stored original layout values
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPosition;
        private Vector2 originalSize;
        private bool saved = false;

        private RectTransformPanZoomController rectTransformPanZoomController;
        private RectTransformPanZoomController RectTransformPanZoomController => rectTransformPanZoomController = rectTransformPanZoomController != null 
            ? rectTransformPanZoomController 
            : FindFirstObjectByType<RectTransformPanZoomController>();

        public bool IsBlocked => RectTransformPanZoomController ? RectTransformPanZoomController.IsBlocked : false;

        private void Awake()
        {
            // Save original values (only once)
            if (!saved)
            {
                originalAnchorMin = innerScreenRectTransform.anchorMin;
                originalAnchorMax = innerScreenRectTransform.anchorMax;
                originalPosition = innerScreenRectTransform.anchoredPosition;
                originalSize = innerScreenRectTransform.sizeDelta;
                saved = true;
            }
        }

        /// <summary>
        /// Toggle the value according the current background status
        /// </summary>
        public void SetFullscreen()
        {
            SetFullscreen(backgroundCanvasGroup.alpha is 1);
        }

        /// <summary>
        /// Toggles fullscreen mode.
        /// </summary>
        /// <param name="fullscreen">True = fill parent, False = restore original layout.</param>
        public async void SetFullscreen(bool fullscreen)
        {
            if (RectTransformPanZoomController is null)
            { 
                Debug.LogWarning("Zoom controller not found, couldn't check blocked state.");
                return;
            }

            if (!innerScreenRectTransform || !backgroundCanvasGroup || IsBlocked) 
                return;

            if (fullscreen)
            {
                // Stretch to fill parent
                innerScreenRectTransform.anchorMin = Vector2.zero;
                innerScreenRectTransform.anchorMax = Vector2.one;
                innerScreenRectTransform.anchoredPosition = Vector2.zero;
                innerScreenRectTransform.sizeDelta = Vector2.zero;
            } 
            else if (saved)
            {
                // Restore
                innerScreenRectTransform.anchorMin = originalAnchorMin;
                innerScreenRectTransform.anchorMax = originalAnchorMax;
                innerScreenRectTransform.anchoredPosition = originalPosition;
                innerScreenRectTransform.sizeDelta = originalSize;
            }

            backgroundCanvasGroup.SetActive(!fullscreen);

            // Make sure layout groups are refreshed
            (layoutReference != null 
                ? layoutReference 
                : innerScreenRectTransform != null 
                    ? innerScreenRectTransform 
                    : transform)
            ?.RefreshLayoutGroupsImmediateAndRecursive();

            await UniTask.WaitForSeconds(0.1f);

            (layoutReference != null
                ? layoutReference
                : innerScreenRectTransform != null
                    ? innerScreenRectTransform
                    : transform)
            ?.RefreshLayoutGroupsImmediateAndRecursive();
        }
    }
}
