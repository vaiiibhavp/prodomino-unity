using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProDomino.Shared
{
    public class BasePopup : MonoBehaviour
    {
        /*
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button cancelButton;
        */
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            Close(); // Make sure it's hidden at the start
        }

        public void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void Close()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /*public void Setup(string message, Action onAccept = null, Action onCancel = null)
        {
            if (messageText != null)
                messageText.text = message;

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(() =>
                {
                    onAccept?.Invoke();
                    Close();
                });
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() =>
                {
                    onCancel?.Invoke();
                    Close();
                });
            }
        }*/
    }
}
