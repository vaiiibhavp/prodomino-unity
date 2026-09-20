using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class EmojisPanel : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField tMP_InputField;

        [SerializeField]
        private Transform emojisBtnsContainer;

        [SerializeField]
        private CanvasGroup rootCanvasGroup;

        private bool emojiPanelIsActive = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            ConfigureEmojiBtns();
        }

        // Update is called once per frame
        void ConfigureEmojiBtns()
        {
            emojiPanelIsActive = false;
            ActiveEmojisPanel(false);

            Button[] buttons = emojisBtnsContainer.GetComponentsInChildren<Button>(true);

            foreach (Button btn in buttons)
            {
                TMP_Text tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
                string label = tmpText.text;

                btn.onClick.AddListener(() => 
                {
                    InsertEmoji(label);
                });
            }
        }

        private void InsertEmoji(string emojiText)
        {
            tMP_InputField.text += emojiText;
            tMP_InputField.caretPosition = tMP_InputField.text.Length;
        }

        public void ButtonActiveEmojisPanel()
        {
            emojiPanelIsActive = !emojiPanelIsActive;
            
            ActiveEmojisPanel(emojiPanelIsActive);
        }

        public void ActiveEmojisPanel(bool active)
        {
            rootCanvasGroup.interactable = active;
            rootCanvasGroup.alpha = active ? 1 : 0;
            rootCanvasGroup.blocksRaycasts = active;
        }
    }
}
