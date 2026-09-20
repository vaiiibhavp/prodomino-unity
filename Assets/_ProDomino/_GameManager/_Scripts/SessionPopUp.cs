using UnityEngine;

namespace ProDomino.GameSystem
{
    public class SessionPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public void Show()
        {
            if (!canvasGroup)
            { 
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }

            canvasGroup.SetActive(true);
        }

        public void Hide()
        {
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned.");
                return;
            }
            canvasGroup.SetActive(false);
        }
    }
}
