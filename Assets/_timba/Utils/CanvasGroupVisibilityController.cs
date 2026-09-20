using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CanvasGroupVisibilityController : MonoBehaviour
{
    [SerializeField] private RectTransform panelRectTransform;
    [SerializeField] private CanvasGroup canvasGroupToControl;
    [SerializeField] private bool isClosingPopUpClickingPanelToo = true;
    [SerializeField] private bool isClosingPopUpClickingEverywhere;
    
    private EventSystem _eventSystem;
    public EventSystem EventSystem => _eventSystem ??= FindFirstObjectByType<EventSystem>();

    private GraphicRaycaster _graphicRaycaster;
    private GraphicRaycaster GraphicRaycaster => _graphicRaycaster ?? GetComponentInParent<GraphicRaycaster>();

    private void Update()
    {
        if (GraphicRaycaster == null 
            || EventSystem == null 
            || panelRectTransform == null 
            || canvasGroupToControl == null 
            || canvasGroupToControl.alpha is 0)
            return;

        // Check for input: mouse click (WebGL/PC) or single touch (Mobile)
        bool inputDetected = false;
        Vector2 inputPosition = Vector2.zero;

#if UNITY_EDITOR || UNITY_WEBGL || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            inputDetected = true;
            inputPosition = Input.mousePosition;
        }
#elif UNITY_IOS || UNITY_ANDROID
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputDetected = true;
            inputPosition = Input.GetTouch(0).position;
        }
#endif

        if (inputDetected && (isClosingPopUpClickingEverywhere || IsClickOutsidePanel(inputPosition)))
            HideCanvasGroup();
    }

    private bool IsClickOutsidePanel(Vector2 screenPosition)
    {
        // 1. Check if pointer/touch is over any UI element
        if (EventSystem.IsPointerOverGameObject())
        {
            PointerEventData pointerData = new PointerEventData(EventSystem)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            GraphicRaycaster.Raycast(pointerData, results);

            foreach (var result in results)
            {
                // If click was on the panel or one of its children, it's not outside
                if (!isClosingPopUpClickingPanelToo && (result.gameObject == panelRectTransform.gameObject || result.gameObject.transform.IsChildOf(panelRectTransform)))
                {
                    return false;
                }

                // If the click was over another UI element (like a modal or overlay), ignore it
                if (result.gameObject != null && result.gameObject.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            // Pointer over UI, but not on the panel or its children
            return true;
        } else
        {
            // Click/touch occurred on empty space (not over any UI)
            return true;
        }
    }

    private void HideCanvasGroup()
    {
        canvasGroupToControl?.SetActive(false);
    }

    public void ShowCanvasGroup()
    {
        canvasGroupToControl?.SetActive(true);

    }
}
