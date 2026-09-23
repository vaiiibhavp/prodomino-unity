using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class SettingsController : MonoBehaviour
    {
        [Header("Root & Legacy References")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Sprite bgmOn, bgmOff;
        [SerializeField] private Sprite sfxOn, sfxOff;
        [SerializeField] private Button bgmButton, sfxButton;
        [SerializeField] private Image bgmImage, sfxImage;
        [SerializeField] private Slider bgmSlider, sfxSlider;
        [SerializeField] private TMP_Text gameVersionLabel;

        [Header("New Figma UI Elements")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TMP_Text volumePercentLabel;
        [SerializeField] private Button speakerButton;
        [SerializeField] private Image speakerIcon;
        [SerializeField] private Sprite speakerOnSprite;
        [SerializeField] private Sprite speakerMuteSprite;
        [SerializeField] private Button learningToPlayButton;
        [SerializeField] private Button eulaAgreementButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundCloseButton;

        private const string MasterVolumeKey = "MasterVolume";
        private const string SfxVolumeKey = "SFXVolume";
        private const string BgmVolumeKey = "BGMVolume";

        public bool IsVisible => rootCanvasGroup is not null and { alpha: not 0 };

        private void Awake()
        {
            UpdateVersionLabel();

            // Setup new Master Slider & controls
            if (masterSlider)
            {
                masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (speakerButton)
            {
                speakerButton.onClick.AddListener(ToggleMasterMute);
            }

            if (learningToPlayButton)
            {
                learningToPlayButton.onClick.AddListener(OnLearningToPlayClicked);
            }

            if (eulaAgreementButton)
            {
                eulaAgreementButton.onClick.AddListener(OnEulaAgreementClicked);
            }

            if (closeButton)
            {
                closeButton.onClick.AddListener(Hide);
            }

            if (backgroundCloseButton)
            {
                backgroundCloseButton.onClick.AddListener(Hide);
            }

            // Legacy slider listeners
            if (sfxSlider)
                sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            if (bgmSlider)
                bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

            if (sfxButton)
                sfxButton.onClick.AddListener(() => ToggleMuteStatus(true));

            if (bgmButton)
                bgmButton.onClick.AddListener(() => ToggleMuteStatus(false));

            LoadValues();
        }

        private void UpdateVersionLabel()
        {
            if (gameVersionLabel)
            {
                string v = Application.version;
                if (string.IsNullOrEmpty(v))
                    v = "0.7015";
                gameVersionLabel.text = $"VERSION {v.ToUpper()}";
            }
        }

        /// <summary>
        /// Loads the saved volume values from PlayerPrefs
        /// </summary>
        public void LoadValues()
        {
            var registeredMaster = PlayerPrefs.GetFloat(MasterVolumeKey, 0.3f);
            var registeredSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, registeredMaster);
            var registeredBgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, registeredMaster);

            if (masterSlider)
            {
                masterSlider.normalizedValue = registeredMaster;
                UpdateMasterDisplay(registeredMaster);
            }

            if (sfxSlider)
                sfxSlider.normalizedValue = registeredSfxVolume;

            if (bgmSlider)
                bgmSlider.normalizedValue = registeredBgmVolume;

            ApplySoundManagerVolumes(registeredMaster);
            CheckMuteIcons();
        }

        /// <summary>
        /// Saves the current volume values to PlayerPrefs
        /// </summary>
        public void SaveValues()
        {
            if (masterSlider)
                PlayerPrefs.SetFloat(MasterVolumeKey, masterSlider.normalizedValue);

            if (sfxSlider)
                PlayerPrefs.SetFloat(SfxVolumeKey, sfxSlider.normalizedValue);

            if (bgmSlider)
                PlayerPrefs.SetFloat(BgmVolumeKey, bgmSlider.normalizedValue);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Shows the settings menu
        /// </summary>
        internal void Show()
        {
            if (!rootCanvasGroup)
            {
                Debug.LogWarning($"Missing reference: {nameof(rootCanvasGroup)}");
                return;
            }

            LoadValues();
            UpdateVersionLabel();
            rootCanvasGroup.SetActive(true);
        }

        /// <summary>
        /// Hides the settings menu
        /// </summary>
        public void Hide()
        {
            if (!rootCanvasGroup)
            {
                Debug.LogWarning($"Missing reference: {nameof(rootCanvasGroup)}");
                return;
            }

            SaveValues();
            rootCanvasGroup.SetActive(false);
        }

        private void OnMasterVolumeChanged(float value)
        {
            UpdateMasterDisplay(value);
            ApplySoundManagerVolumes(value);

            if (sfxSlider && !Mathf.Approximately(sfxSlider.normalizedValue, value))
                sfxSlider.normalizedValue = value;

            if (bgmSlider && !Mathf.Approximately(bgmSlider.normalizedValue, value))
                bgmSlider.normalizedValue = value;

            CheckMuteIcons();
        }

        private void UpdateMasterDisplay(float value)
        {
            if (volumePercentLabel)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                volumePercentLabel.text = $"{percent}%";
            }

            if (speakerIcon && speakerOnSprite && speakerMuteSprite)
            {
                speakerIcon.sprite = value <= 0.001f ? speakerMuteSprite : speakerOnSprite;
            }
        }

        private void ApplySoundManagerVolumes(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetSFXVolume(value);
                SoundManager.Instance.SetMusicVolume(value);
            }
        }

        private void ToggleMasterMute()
        {
            if (!masterSlider) return;

            if (masterSlider.normalizedValue <= 0.001f)
            {
                float prev = PlayerPrefs.GetFloat(MasterVolumeKey, 0.3f);
                if (prev <= 0.001f) prev = 0.3f;
                masterSlider.normalizedValue = prev;
            }
            else
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, masterSlider.normalizedValue);
                masterSlider.normalizedValue = 0f;
            }

            CheckMuteIcons();
        }

        private void OnLearningToPlayClicked()
        {
            TriggerNavigation(NavigationPanelType.Learn);
        }

        private void OnEulaAgreementClicked()
        {
            TriggerNavigation(NavigationPanelType.HelpScreen);
        }

        private void TriggerNavigation(NavigationPanelType panelType)
        {
            Hide();
            try
            {
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "NavigationPanelController")
                    {
                        var method = mb.GetType().GetMethod("ExternalActivateNavigationPanel", new[] { typeof(NavigationPanelType) });
                        if (method != null)
                        {
                            method.Invoke(mb, new object[] { panelType });
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SettingsController] Error navigating to {panelType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the mute status of either SFX or BGM
        /// </summary>
        private void ToggleMuteStatus(bool isSFX)
        {
            var slider = isSFX ? sfxSlider : bgmSlider;
            var key = isSFX ? SfxVolumeKey : BgmVolumeKey;

            if (!slider) return;

            // If muted, unmute to last saved value
            if (slider.normalizedValue <= 0)
                slider.normalizedValue = PlayerPrefs.GetFloat(key, 0.5f);
            else
            {
                // Save current value before muting
                PlayerPrefs.SetFloat(key, slider.normalizedValue);
                slider.normalizedValue = 0;
            }

            CheckMuteIcons();
        }

        /// <summary>
        /// Checks and updates the mute icons based on the slider values
        /// </summary>
        private void CheckMuteIcons()
        {
            if (masterSlider)
            {
                UpdateMasterDisplay(masterSlider.normalizedValue);
            }

            if (sfxImage && sfxSlider && sfxOn && sfxOff)
                UpdateIcon(sfxImage, sfxOn, sfxOff, sfxSlider.normalizedValue);

            if (bgmImage && bgmSlider && bgmOn && bgmOff)
                UpdateIcon(bgmImage, bgmOn, bgmOff, bgmSlider.normalizedValue);

            void UpdateIcon(Image image, Sprite on, Sprite off, float value)
            {
                if (image && on && off)
                    image.sprite = value <= 0 ? off : on;
            }
        }

        private void OnSFXVolumeChanged(float normalizedValue)
        {
            SoundManager.Instance.SetSFXVolume(normalizedValue);
            CheckMuteIcons();
        }

        private void OnBGMVolumeChanged(float normalizedValue)
        {
            SoundManager.Instance.SetMusicVolume(normalizedValue);
            CheckMuteIcons();
        }
    }
}
