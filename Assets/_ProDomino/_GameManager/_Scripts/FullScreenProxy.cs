using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.GameSystem
{
    [RequireComponent(typeof(Button))]
    public class FullScreenProxy : MonoBehaviour
    {
        private FullScreenController fullScreenController;
        private FullScreenController FullScreenController => fullScreenController = fullScreenController != null 
            ? fullScreenController 
            : FindFirstObjectByType<FullScreenController>();
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnSetFullScreen);
        }

        private void Update()
        {
            if (FullScreenController is null)
                return;
            button.interactable = !FullScreenController.IsBlocked;
        }

        private void OnSetFullScreen()
        {
            // Get the refence once
            var fullScreenController = FullScreenController;
            if (!fullScreenController)
            {
                Debug.LogWarning($"Missing reference: {nameof(fullScreenController)}");
                return;
            }

            fullScreenController.SetFullscreen();
        }
    }
}
