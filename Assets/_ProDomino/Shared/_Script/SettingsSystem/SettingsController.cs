using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class SettingsController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Sprite bgmOn, bgmOff;
        [SerializeField] private Sprite sfxOn, sfxOff;
        [SerializeField] private Button bgmButton, sfxButton;
        [SerializeField] private Image bgmImage, sfxImage;
        [SerializeField] private Slider bgmSlider, sfxSlider;
        [SerializeField] private TMP_Text gameVersionLabel;

        private const string SfxVolumeKey = "SFXVolume";
        private const string BgmVolumeKey = "BGMVolume";

        public bool IsVisible => rootCanvasGroup is not null and { alpha: not 0 };

        private void Awake()
        {
            if (gameVersionLabel)
                gameVersionLabel.text = $"Version {Application.version}";
            else
                Debug.LogWarning($"Missing reference: {nameof(gameVersionLabel)}");

            if (sfxSlider)
                sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            else
                Debug.LogWarning($"Missing reference: {nameof(sfxSlider)}");

            if (bgmSlider)
                bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            else
                Debug.LogWarning($"Missing reference: {nameof(bgmSlider)}");

            if (sfxButton)
                sfxButton.onClick.AddListener(() => ToggleMuteStatus(true));
            else
                Debug.LogWarning($"Missing reference: {nameof(sfxButton)}");

            if (bgmButton)
                bgmButton.onClick.AddListener(() => ToggleMuteStatus(false));
            else
                Debug.LogWarning($"Missing reference: {nameof(bgmButton)}");

            LoadValues();
        }

        /// <summary>
        /// Loads the saved volume values from PlayerPrefs
        /// </summary>
        internal void LoadValues()
        {
            var registeredSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, .75f);
            var registeredBgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, .5f);

            var isSameSFX = registeredSfxVolume == sfxSlider?.normalizedValue;
            var isSameBGM = registeredBgmVolume == bgmSlider?.normalizedValue;

            if (sfxSlider)
                sfxSlider.normalizedValue = registeredSfxVolume;

            if (bgmSlider)
                bgmSlider.normalizedValue = registeredBgmVolume;

            // If the values are the same, manually invoke the events to ensure the sfx volumen updates accordingly
            if (sfxSlider && isSameSFX)
                sfxSlider.onValueChanged?.Invoke(registeredSfxVolume);

            // If the values are the same, manually invoke the events to ensure the bgm volumen updates accordingly
            if (bgmSlider && isSameBGM)
                bgmSlider.onValueChanged?.Invoke(registeredBgmVolume);
        }

        /// <summary>
        /// Saves the current volume values to PlayerPrefs
        /// </summary>
        internal void SaveValues()
        {
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
            if (sfxImage && sfxSlider && sfxOn && sfxOff)
                UpdateIcon(sfxImage, sfxOn, sfxOff, sfxSlider.normalizedValue);
            else
                Debug.LogWarning($"Missing sprite or image reference for {sfxImage?.name ?? "unknown"}");

            if (bgmImage && bgmSlider && bgmOn && bgmOff)
                UpdateIcon(bgmImage, bgmOn, bgmOff, bgmSlider.normalizedValue);
            else
                Debug.LogWarning($"Missing sprite or image reference for {bgmImage?.name ?? "unknown"}");

            void UpdateIcon(Image image, Sprite on, Sprite off, float value)
            {
                if (image && on && off)
                    image.sprite = value <= 0 ? off : on;
                else
                    Debug.LogWarning($"Missing sprite or image reference for {image?.name ?? "unknown"}");
            }
        }

        /// <summary>
        /// Called when the SFX volume slider value changes
        /// </summary>
        private void OnSFXVolumeChanged(float normalizedValue)
        {
            SoundManager.Instance.SetSFXVolume(normalizedValue);

            CheckMuteIcons();
        }

        /// <summary>
        /// Called when the BGM volume slider value changes
        /// </summary>
        private void OnBGMVolumeChanged(float normalizedValue)
        {
            SoundManager.Instance.SetMusicVolume(normalizedValue);

            CheckMuteIcons();
        }
    }
}
