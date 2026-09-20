using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace ProDomino.Shared
{
    public class PopupsManager : MonoBehaviour
    {
        public static PopupsManager Instance { get; private set; }
        [SerializeField] private CanvasGroup bg_canvasGroup;

        [SerializeField] private List<PopupEntry> popups = new List<PopupEntry>();
        private Dictionary<string, BasePopup> popupMap = new Dictionary<string, BasePopup>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            foreach (PopupEntry entry in popups)
            {
                BasePopup popup = entry.basePopup;
                if (popup != null)
                {
                    popupMap[entry.ID] = popup;
                    popup.Close(); // Make sure all popups are hidden at the start
                }
                else
                {
                    Debug.LogWarning($"Popup '{entry.ID}' does not have a BasePopup component");
                }
            }
        }

        /*
        public void ShowPopup(string id, string message = "", Action onAccept = null, Action onCancel = null)
        {
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                SetActiveBg(true);
                popup.Setup(message, onAccept, onCancel);
                popup.Show();
            }
            else
            {
                Debug.LogWarning($"No popup found with ID: {id}");
            }
        }
        */

        private async UniTaskVoid ShowPopup(string id, Func<UniTask> taskToRun)
        {
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                SetActiveBg(true);
                popup.Show();
                await taskToRun();
                popup.Close();
                SetActiveBg(false);
            }
            else
            {
                Debug.LogWarning($"No popup found with ID: {id}");
            }
        }

        public IEnumerator ShowPopup(BasePopup popup, IEnumerator subTask)
        {
            SetActiveBg(true);
            popup.Show();
            yield return StartCoroutine(subTask);
            popup.Close();
            SetActiveBg(false);
        }

        public void ShowPopup(string id, IEnumerator subTask)
        {
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                StartCoroutine(Show(popup, subTask));
            }
            else
            {
                Debug.LogWarning($"No popup found with ID: {id}");
            }
        }

        private IEnumerator Show(BasePopup popup, IEnumerator subTask)
        {
            SetActiveBg(true);
            popup.Show();
            yield return StartCoroutine(subTask);
            popup.Close();
            SetActiveBg(false);
        }

        public void ShowPopup(string id, float waitingTime)
        {
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                StartCoroutine(Show(popup, waitingTime));
            }
            else
            {
                Debug.LogWarning($"No popup found with ID: {id}");
            }
        }

        private IEnumerator Show(BasePopup popup, float waitingTime)
        {
            SetActiveBg(true);
            popup.Show();
            yield return new WaitForSeconds(waitingTime);
            popup.Close();
            SetActiveBg(false);
        }

        public void ShowPopup(string id)
        {
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                SetActiveBg(true);
                popup.Show();
            }
            else
            {
                Debug.LogWarning($"No popup found with ID: {id}");
            }
        }

        public void ClosePopup(string id)
        {
            SetActiveBg(false);
            if (popupMap.TryGetValue(id, out BasePopup popup))
            {
                popup.Close();
            }
        }

        public void CloseAll()
        {
            SetActiveBg(false);
            foreach (BasePopup popup in popupMap.Values)
            {
                popup.Close();
            }
        }
        
        private void SetActiveBg(bool active)
        {
            if (bg_canvasGroup != null)
            {
                bg_canvasGroup.alpha = active ? 1 : 0;
                bg_canvasGroup.blocksRaycasts = active;
                bg_canvasGroup.interactable = active;
            }
        }
    }
}
