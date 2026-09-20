using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.ReplaySystem
{
    public class ReplayChangeNameDataPanel : MonoBehaviour
    {
        public TMP_InputField inputField;
        public Button buttonAccept;
        public Button buttonCancel;
        public CanvasGroup canvasGroup;


        public void OpenChangeNameDataPanel(string currentName, System.Action<string> onAccept, System.Action onCancel)
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            inputField.text = currentName;

            buttonAccept.onClick.RemoveAllListeners();
            buttonAccept.onClick.AddListener(() =>
            {
                onAccept?.Invoke(inputField.text);
                CloseChangeNameDataPanel();
            });

            buttonCancel.onClick.RemoveAllListeners();
            buttonCancel.onClick.AddListener(() =>
            {
                onCancel?.Invoke();
                CloseChangeNameDataPanel();
            });
        }

        public void CloseChangeNameDataPanel()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            buttonAccept.onClick.RemoveAllListeners();
            buttonCancel.onClick.RemoveAllListeners();
        }
    }
}
