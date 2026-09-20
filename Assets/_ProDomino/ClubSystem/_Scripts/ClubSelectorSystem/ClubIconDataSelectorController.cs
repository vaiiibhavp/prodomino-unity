using ProDomino.Shared;
using System;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.FirestoreClubData;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Controls the UI and logic for selecting and previewing club icon data, including shield, texture, central image,
    /// and background, and manages confirmation and cancellation of changes.
    /// </summary>
    internal class ClubIconDataSelectorController : MonoBehaviour
    {
        [SerializeField] private Preview preview;
        [SerializeField] private ClubDataSelectable baseShieldSelectorUI;
        [SerializeField] private ClubDataSelectable textureSelectorUI;
        [SerializeField] private ClubDataSelectable centralImageSelectorUI;
        [SerializeField] private ClubDataSelectable backgroundSelectorUI;
        [SerializeField] private CustomButtonUI confirmChangesButton;
        [SerializeField] private CustomButtonUI cancelChangesButton;

        private DictionaryService dictionaryService;
        private Action onConfirmChanges;
        private Action onCancelChanges;

        public IconData TemporalIconData { get; private set; }

        private void Awake()
        {
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            if (confirmChangesButton)
                confirmChangesButton.onClick.AddListener(OnConfirmChanges);
            else
                Debug.LogWarning("Couldn't subscribe to confirmChangesButton onClick event because its reference is null");

            if (cancelChangesButton)
                cancelChangesButton.onClick.AddListener(OnCancelChanges);
            else
                Debug.LogWarning("Couldn't subscribe to cancelChangesButton onClick event because its reference is null");
        }

        /// <summary>
        /// Initialize the selectors ui with the corresponding callbacks and fill the selectors with the corresponding data from the dictionary service
        /// </summary>
        /// <param name="onConfirmChanges">Action to perform when confirming changes.</param>
        /// <param name="onCancelChanges">Action to perform when canceling changes.</param>
        internal void Initialize
            (Action onConfirmChanges, 
            Action onCancelChanges)
        {
            this.onConfirmChanges = onConfirmChanges;
            this.onCancelChanges = onCancelChanges;

            if (!baseShieldSelectorUI || !textureSelectorUI || !centralImageSelectorUI || !backgroundSelectorUI)
            {
                Debug.LogWarning("Couldn't initialize selectors ui because there are null");
                return;
            }

            // Initialize preview with default values
            if (baseShieldSelectorUI)
            {
                var baseShieldSpriteCollection = dictionaryService.GetSpriteDataCollection($"{Consts.CollectionKeys.Club}_{ClubDataSelectableType.BaseShield}");
                var baseShieldColorCollection = dictionaryService.GetColorDataCollection($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}");
                baseShieldSelectorUI.Initialize(OnSelectEntry, ClubDataSelectableType.BaseShield, baseShieldSpriteCollection, baseShieldColorCollection);
            }

            if (textureSelectorUI)
            {
                var textureSpriteCollection = dictionaryService.GetSpriteDataCollection($"{Consts.CollectionKeys.Club}_{ClubDataSelectableType.Texture}");
                var textureColorCollection = dictionaryService.GetColorDataCollection($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}");
                textureSelectorUI.Initialize(OnSelectEntry, ClubDataSelectableType.Texture, textureSpriteCollection, textureColorCollection);
            }

            if (centralImageSelectorUI)
            {
                var centralImageSpriteCollection = dictionaryService.GetSpriteDataCollection($"{Consts.CollectionKeys.Club}_{ClubDataSelectableType.CentralImage}");
                var centralImageColorCollection = dictionaryService.GetColorDataCollection($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}");
                centralImageSelectorUI.Initialize(OnSelectEntry, ClubDataSelectableType.CentralImage, centralImageSpriteCollection, centralImageColorCollection);
            }

            if (backgroundSelectorUI)
            {
                var backgroundColorCollection = dictionaryService.GetColorDataCollection($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}");
                backgroundSelectorUI.Initialize(OnSelectEntry, ClubDataSelectableType.Background, null, backgroundColorCollection);
            }
        }

        /// <summary>
        /// Override the preview with the given icon data
        /// </summary>
        /// <param name="iconData">The icon data to use for the preview.</param>
        internal void OverridePreview(IconData iconData)
        {
            // Check if preview is null
            if (preview == null)
            {
                Debug.LogWarning("Couldn't override preview because its reference is null");
                return;
            }

            TemporalIconData = iconData?.Clone() as IconData;

            // Set base shield
            SetData(ClubDataSelectableType.BaseShield, iconData?.shieldId, iconData?.shieldColorId, Preview.byDefaultShieldSprite, Preview.byDefaultShieldColor);

            // Set texture
            SetData(ClubDataSelectableType.Texture, iconData?.textureId, iconData?.textureColorId, Preview.byDefaultTextureSprite, Preview.byDefaultTextureColor);

            // Set central image
            SetData(ClubDataSelectableType.CentralImage, iconData?.centralImageId, iconData?.centralImageColorId, Preview.byDefaultCentralImageSprite, Preview.byDefaultCentralImageColor);

            // Set background color
            SetData(ClubDataSelectableType.Background, null, iconData?.backgroundColorId, null, Preview.byDefaultbackgroundColor);

            void SetData(ClubDataSelectableType clubDataSelectableType, string iconId, string colorId, Sprite byDefaultSprite, Color byDefaultColor)
            {
                preview.SetPreviewData(clubDataSelectableType, iconId, colorId, byDefaultSprite, byDefaultColor);

                var selectorUI = (clubDataSelectableType switch
                {
                    ClubDataSelectableType.BaseShield => baseShieldSelectorUI,
                    ClubDataSelectableType.Texture => textureSelectorUI,
                    ClubDataSelectableType.CentralImage => centralImageSelectorUI,
                    ClubDataSelectableType.Background => backgroundSelectorUI,
                    _ => null
                });

                // Force selection in the selector UI
                if (selectorUI)
                {
                    var searchedSprite = dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_{clubDataSelectableType}", iconId);
                    var searchedColor = dictionaryService.GetColor($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}", colorId);

                    var textureEntryId = searchedSprite ? iconId : Preview.byDefaultCentralImageId;
                    var colorEntryId = searchedColor.HasValue ? colorId : Preview.byDefaultCentralImageId;

                    if (!string.IsNullOrEmpty(textureEntryId))
                        selectorUI.ForceSelection(textureEntryId, false);

                    if (!string.IsNullOrEmpty(colorEntryId))
                        selectorUI.ForceSelection(colorEntryId, true);
                }
                else
                    Debug.LogWarning($"Couldn't force selection in the selector UI because its reference for {clubDataSelectableType} is null");
            }
        }

        /// <summary>
        /// Clear the current selection
        /// </summary>
        internal void ClearSelection()
        {
            TemporalIconData = default;
            OverridePreview(null);

            if (preview != null)
                preview.Clear();
            else
                Debug.LogWarning("Couldn't clear selection because preview reference is null");
        }

        /// <summary>
        /// Fill any empty field in the TemporalIconData with default values
        /// </summary>
        internal void FillEmptyIconData()
        {
            TemporalIconData = new IconData
            (
                shieldId: TemporalIconData?.shieldId ?? Preview.byDefaultShieldId,
                shieldColorId: TemporalIconData?.shieldColorId ?? Preview.byDefaultShieldId,

                textureId: TemporalIconData?.textureId ?? string.Empty,
                textureColorId: TemporalIconData?.textureColorId ?? string.Empty,

                centralImageId: TemporalIconData?.centralImageId ?? Preview.byDefaultCentralImageId,
                centralImageColorId: TemporalIconData?.centralImageColorId ?? Preview.byDefaultCentralImageId,

                backgroundColorId: TemporalIconData?.backgroundColorId ?? Preview.byDefaultBackgroundColorId
            );
        }

        /// <summary>
        /// Called when a selector entry is selected
        /// </summary>
        /// <param name="selectedClubDataEntry">The selected club data entry.</param>
        private void OnSelectEntry(ClubIconDataEntry selectedClubDataEntry) 
        {
            if (preview == null)
            {
                Debug.LogWarning("Couldn't update preview because its reference is null");
                return;
            }

            if (!selectedClubDataEntry)
            {
                Debug.LogWarning("Couldn't update preview because selectedClubDataEntry is null");
                return;
            }

            // Ensure TemporalIconData is initialized
            TemporalIconData ??= new();

            // If the entry is a sprite selector, update the temporal icon data sprite
            if (!selectedClubDataEntry.IsColorSelector)
                switch (selectedClubDataEntry.ClubDataSelectableType)
                {
                    case ClubDataSelectableType.BaseShield:
                        TemporalIconData.shieldId = selectedClubDataEntry.EntryId;
                        break;
                    case ClubDataSelectableType.Texture:
                        TemporalIconData.textureId = selectedClubDataEntry.EntryId;
                        break;
                    case ClubDataSelectableType.CentralImage:
                        TemporalIconData.centralImageId = selectedClubDataEntry.EntryId;
                        break;
                    default:
                        throw new NotImplementedException();
                }

            // But, if the entry is a color selector, update the temporal icon data color
            else
                switch (selectedClubDataEntry.ClubDataSelectableType)
                {
                    case ClubDataSelectableType.BaseShield:
                        TemporalIconData.shieldColorId = selectedClubDataEntry.EntryId;
                        break;
                    case ClubDataSelectableType.Texture:
                        TemporalIconData.textureColorId = selectedClubDataEntry.EntryId;
                        break;
                    case ClubDataSelectableType.CentralImage:
                        TemporalIconData.centralImageColorId = selectedClubDataEntry.EntryId;
                        break;
                    case ClubDataSelectableType.Background:
                        TemporalIconData.backgroundColorId = selectedClubDataEntry.EntryId;
                        break;
                    default:
                        throw new NotImplementedException();
                }

            var iconId = selectedClubDataEntry.ClubDataSelectableType switch
            {
                ClubDataSelectableType.BaseShield => TemporalIconData.shieldId,
                ClubDataSelectableType.Texture => TemporalIconData.textureId,
                ClubDataSelectableType.CentralImage => TemporalIconData.centralImageId,
                _ => null
            };

            var colorId = selectedClubDataEntry.ClubDataSelectableType switch
            {
                ClubDataSelectableType.BaseShield => TemporalIconData.shieldColorId,
                ClubDataSelectableType.Texture => TemporalIconData.textureColorId,
                ClubDataSelectableType.CentralImage => TemporalIconData.centralImageColorId,
                ClubDataSelectableType.Background => TemporalIconData.backgroundColorId,
                _ => null
            };

            var defaultSprite = selectedClubDataEntry.ClubDataSelectableType switch
            {
                ClubDataSelectableType.BaseShield => Preview.byDefaultShieldSprite,
                ClubDataSelectableType.Texture => Preview.byDefaultTextureSprite,
                ClubDataSelectableType.CentralImage => Preview.byDefaultCentralImageSprite,
                _ => null
            };

            var defaultColor = selectedClubDataEntry.ClubDataSelectableType switch
            {
                ClubDataSelectableType.BaseShield => Preview.byDefaultShieldColor,
                ClubDataSelectableType.Texture => Preview.byDefaultTextureColor,
                ClubDataSelectableType.CentralImage => Preview.byDefaultCentralImageColor,
                ClubDataSelectableType.Background => Preview.byDefaultbackgroundColor,
                _ => Color.clear
            };

            // According the data filled inf the TemporalIconData, set the preview updating each part
            preview.SetPreviewData(selectedClubDataEntry.ClubDataSelectableType,
                iconId,
                colorId,
                defaultSprite,
                defaultColor);
        }

        /// <summary>
        /// Called when confirm changes button is clicked
        /// </summary>
        private void OnConfirmChanges()
        {
            if (onConfirmChanges is null)
            { 
                Debug.LogWarning("Couldn't invoke OnConfirmChanges callback because its reference is null");
                return;
            }

            onConfirmChanges();
        }

        /// <summary>
        /// Called when cancel changes button is clicked
        /// </summary>
        private void OnCancelChanges()
        {
            ClearSelection();

            if (onCancelChanges is null)
            { 
                Debug.LogWarning("Couldn't invoke OnCancelChanges callback because its reference is null");
                return;
            }

            onCancelChanges();
        }

        /// <summary>
        /// Call this method when the selector panel is opened<br></br> 
        /// If the TemporalIconData is null, it will clear the current selection
        /// </summary>
        internal void OnOpen()
        {
            if (TemporalIconData is null)
                ClearSelection();
        }

        internal enum ClubDataSelectableType
        { 
            None = 0,
            BaseShield = 1,
            Texture = 2,
            CentralImage = 3,
            Background = 4,
        }

        /// <summary>
        /// Manages the preview display of club shield, texture, central image, and background, including their sprites
        /// and colors.
        /// </summary>
        [Serializable]
        internal class Preview
        {
            [SerializeField] private Image baseShield;
            [SerializeField] private Image texture;
            [SerializeField] private Image centralImage;
            [SerializeField] private Image background;

            internal static string byDefaultShieldId = $"{ClubDataSelectableType.BaseShield}_Default";
            internal static string byDefaultCentralImageId = $"{ClubDataSelectableType.CentralImage}_Default";
            internal static string byDefaultBackgroundColorId = $"{ClubDataSelectableType.Background}_Default";

            internal static Sprite byDefaultShieldSprite;
            internal static Sprite byDefaultTextureSprite;
            internal static Sprite byDefaultCentralImageSprite;

            internal static Color byDefaultShieldColor;
            internal static Color byDefaultTextureColor;
            internal static Color byDefaultCentralImageColor;
            internal static Color byDefaultbackgroundColor;

            private static DictionaryService _dictionaryService;

            /// <summary>
            /// Initializes default sprites and colors using the provided dictionary service.
            /// </summary>
            /// <param name="dictionaryService">Dictionary service used to retrieve sprites and colors.</param>
            /// <exception cref="ArgumentNullException">Thrown if dictionaryService is null and no existing service is available.</exception>
            internal static void Initialize(DictionaryService dictionaryService)
            {
                var colorsKey = $"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Default}";

                _dictionaryService = _dictionaryService != null 
                    ? _dictionaryService 
                    : dictionaryService 
                    ?? throw new ArgumentNullException(nameof(dictionaryService));

                byDefaultShieldSprite = byDefaultShieldSprite != null 
                    ? byDefaultShieldSprite 
                    : _dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Default}", byDefaultShieldId);
                byDefaultShieldColor = _dictionaryService.GetColor(colorsKey, byDefaultShieldId) ?? Color.white;

                byDefaultTextureSprite = byDefaultTextureSprite != null 
                    ? byDefaultTextureSprite 
                    : null;
                byDefaultTextureColor = Color.clear;

                byDefaultCentralImageSprite = byDefaultCentralImageSprite != null 
                    ? byDefaultCentralImageSprite 
                    : _dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Default}", byDefaultCentralImageId);
                byDefaultCentralImageColor = _dictionaryService.GetColor(colorsKey, byDefaultCentralImageId) ?? Color.white;

                byDefaultbackgroundColor = _dictionaryService.GetColor(colorsKey, byDefaultBackgroundColorId) ?? Color.white;
            }

            /// <summary>
            /// Main method that sets the preview data for the specified club data type, icon, and color.
            /// </summary>
            /// <param name="clubDataSelectableType">The type of club data selectable.</param>
            /// <param name="iconId">The ID of the icon to use.</param>
            /// <param name="colorId">The ID of the color to use.</param>
            /// <param name="byDefaultSprite">The default sprite to use if no sprite is found.</param>
            /// <param name="byDefaultColor">The default color to use if no color is found.</param>
            /// <param name="isSettingSprite">Indicates whether to set the sprite.</param>
            /// <param name="isSettingColor">Indicates whether to set the color.</param>
            internal void SetPreviewData
                (ClubDataSelectableType clubDataSelectableType, 
                string iconId, 
                string colorId, 
                Sprite byDefaultSprite, 
                Color byDefaultColor,
                bool isSettingSprite = true,
                bool isSettingColor = true)
            {
                if (!_dictionaryService)
                { 
                    Debug.LogError("Couldn't set preview data because _dictionaryService reference is null");
                    return;
                }

                var searchedSprite = _dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_{clubDataSelectableType}", iconId);
                var searchedColor = _dictionaryService.GetColor($"{Consts.CollectionKeys.Club}_{Consts.CollectionKeys.Colors}", colorId);

                // Check fi the icon that removes the icon (contains "None") is selected
                var isEmptyIcon = !string.IsNullOrEmpty(iconId) && iconId.Contains("None");

                // Only modify the sprite if it's required
                if (isSettingSprite)
                {
                    // Get and invoke the corresponding action that sets the sprite (or clear if there is no sprite assigned)
                    ((Action<Sprite>)(clubDataSelectableType switch
                    {
                        ClubDataSelectableType.BaseShield => SetBaseShieldSprite,
                        ClubDataSelectableType.Texture => SetTextureSprite,
                        ClubDataSelectableType.CentralImage => SetCentralImageSprite,
                        _ => null
                    }))?.Invoke(isEmptyIcon
                        ? default
                        : (searchedSprite ?? byDefaultSprite));
                }

                // Only modify the color if it's required
                if (isSettingColor)
                {
                    // Get and invoke the corresponding action that sets the color (or clear if there is no sprite assigned)
                    ((Action<Color>)(clubDataSelectableType switch
                    {
                        ClubDataSelectableType.BaseShield => SetBaseShieldColor,
                        ClubDataSelectableType.Texture => SetTextureColor,
                        ClubDataSelectableType.CentralImage => SetCentralImageColor,
                        ClubDataSelectableType.Background => SetBackgroundColor,
                        _ => null,
                    }))?.Invoke(isEmptyIcon
                        ? Color.clear
                        : (searchedColor ?? byDefaultColor));
                }
            }

            /// <summary>
            /// Resets all shield, texture, central image, and background properties to their default values.
            /// </summary>
            public void Clear()
            {
                SetBaseShieldSprite(byDefaultShieldSprite);
                SetBaseShieldColor(byDefaultShieldColor);

                SetTextureSprite(byDefaultTextureSprite);
                SetTextureColor(byDefaultTextureColor);

                SetCentralImageSprite(byDefaultCentralImageSprite);
                SetCentralImageColor(byDefaultCentralImageColor);

                SetBackgroundColor(byDefaultbackgroundColor);
            }

            /// <summary>
            /// Sets the sprite for the base shield image if the reference exists.
            /// </summary>
            /// <param name="sprite">The sprite to assign to the base shield.</param>
            internal void SetBaseShieldSprite(Sprite sprite)
            {
                if (baseShield)
                    baseShield.sprite = sprite;
                else
                    Debug.LogWarning("Couldn't set base shield sprite because its image reference is null");
            }

            /// <summary>
            /// Sets the color of the base shield image if the reference is valid.
            /// </summary>
            /// <param name="color">The color to apply to the base shield.</param>
            internal void SetBaseShieldColor(Color color)
            {
                if (baseShield)
                    baseShield.color = color;
                else
                    Debug.LogWarning("Couldn't set base shield color because its image reference is null");
            }

            /// <summary>
            /// Assigns the specified sprite to the texture if the texture reference is valid.
            /// </summary>
            /// <param name="sprite">The sprite to assign to the texture.</param>
            internal void SetTextureSprite(Sprite sprite)
            {
                if (texture)
                    texture.sprite = sprite;
                else
                    Debug.LogWarning("Couldn't set texture sprite because its image reference is null");
            }

            /// <summary>
            /// Sets the color of the texture if it exists and has a sprite; otherwise sets it to clear.
            /// </summary>
            /// <param name="color">The color to apply to the texture.</param>
            internal void SetTextureColor(Color color)
            {
                if (texture)
                {
                    // Only set the color if there is a sprite assigned, otherwise set it to clear
                    if (texture.sprite)
                        texture.color = color;
                    else
                        texture.color = Color.clear;
                }
                else
                    Debug.LogWarning("Couldn't set texture color because its image reference is null");
            }

            /// <summary>
            /// Sets the sprite of the central image if the image reference is valid; otherwise logs a warning.
            /// </summary>
            /// <param name="sprite">The sprite to assign to the central image.</param>
            internal void SetCentralImageSprite(Sprite sprite)
            {
                if (centralImage)
                    centralImage.sprite = sprite;
                else
                    Debug.LogWarning("Couldn't set central image sprite because its image reference is null");
            }

            /// <summary>
            /// Sets the color of the central image if its reference is valid.
            /// </summary>
            /// <param name="color">The color to apply to the central image.</param>
            internal void SetCentralImageColor(Color color)
            {
                if (centralImage)
                    centralImage.color = color;
                else
                    Debug.LogWarning("Couldn't set central image color because its image reference is null");
            }

            /// <summary>
            /// Sets the background image color if the reference is valid.
            /// </summary>
            /// <param name="color">The color to apply to the background image.</param>
            internal void SetBackgroundColor(Color color)
            {
                if (background)
                    background.color = color;
                else
                    Debug.LogWarning("Couldn't set background color because its image reference is null");
            }
        }
    }
}
