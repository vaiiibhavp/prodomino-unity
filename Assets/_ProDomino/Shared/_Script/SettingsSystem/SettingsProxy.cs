using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    [RequireComponent(typeof(Button))]
    public class SettingsProxy : MonoBehaviour
    {
        private Button button;

        private static SettingsController _settingsController;
        private static SettingsController settingsController => _settingsController = _settingsController != null 
            ? _settingsController 
            : FindFirstObjectByType<SettingsController>();


        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClicked);
        }

        private void OnButtonClicked()
        {
            if (!settingsController)
            {
                Debug.LogWarning($"No {nameof(SettingsController)} found in the scene.");
                return;
            }

            // Toggle visibility according to current state
            (!settingsController.IsVisible ? (Action)settingsController.Show : settingsController.Hide)();
        }
    }
}
