using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Timba.Utils
{
    [RequireComponent(typeof(TMP_InputField))]
    public class InputfieldVisionController : MonoBehaviour
    {
        [SerializeField] private Toggle toggleVision;
        [SerializeField] private CanvasGroup onEnabledCanvasGroup;
        [SerializeField] private CanvasGroup onDisabledCanvasGroup;
        private TMP_InputField inputField;

        private void Awake()
        {
            inputField = GetComponent<TMP_InputField>();

            if (toggleVision)
                toggleVision.onValueChanged.AddListener(SetInputFieldVisibility);
            else
                Debug.LogError("Toggle component not assigned.");
        }

        private void Start()
        {
            if (toggleVision)
                SetInputFieldVisibility(toggleVision.isOn);
        }

        public void SetInputFieldVisibility(bool isVisible)
        {
            if (inputField == null)
            {
                Debug.LogError("TMP_InputField component not found.");
                return;
            }

            if (toggleVision == null)
            {
                Debug.LogError("Toggle component not found.");
                return;
            }

            inputField.contentType = isVisible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            inputField.ForceLabelUpdate();

            if (onEnabledCanvasGroup)
                onEnabledCanvasGroup.alpha = isVisible ? 1 : 0;

            if (onDisabledCanvasGroup)
                onDisabledCanvasGroup.alpha = isVisible ? 0 : 1;
        }
    }
}