using UnityEngine;
using UnityEngine.UI;
using static ProDomino.ClubSystem.ClubIconDataSelectorController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a UI entry for club icon selection, supporting both color and texture selectors.
    /// </summary>
    internal class ClubIconDataEntry : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private CustomButtonUI customToggle;

        internal string EntryId { get; private set; }
        internal bool IsColorSelector { get; private set; }
        internal ClubDataSelectableType ClubDataSelectableType { get; private set; }

        /// <summary>
        /// Initialize the entry specifying if it's a color selector or a texture selector, this is important to know because the way the entry will be configured is different depending on the type of selector it is
        /// </summary>
        /// <param name="isColorSelector">Indicates whether the entry is a color selector.</param>
        internal void Initalize(bool isColorSelector)
        {
            IsColorSelector = isColorSelector;
        }

        /// <summary>
        /// Configure the entry with the given data
        /// </summary>
        /// <param name="clubDataSelectableType">The type of club data selectable.</param>
        /// <param name="entryId">The ID of the entry.</param>
        /// <param name="sprite">The sprite to display, if applicable.</param>
        /// <param name="color">The color to display, if applicable.</param>
        internal void Configure(ClubDataSelectableType clubDataSelectableType, 
            string entryId, 
            Sprite sprite = null, 
            Color? color = null)
        { 
            EntryId = entryId;
            ClubDataSelectableType = clubDataSelectableType;

            // Override the id of the toggle
            if (customToggle)
                customToggle.SetCustomButtonID(entryId);
            else
                Debug.LogWarning("Coudln't configure ClubHomeScreenDataUI because its CustomToggle reference is null");

            if (image)
            { 
                if (sprite)
                    image.sprite = sprite;

                if (color.HasValue)
                    image.color = color.Value;
            }
            else
                Debug.LogWarning("Coudln't configure ClubHomeScreenDataUI because its image reference is null");
        }

        /// <summary>
        /// Gets the sprite of the entry if it's a texture selector, otherwise returns null since color selectors don't use sprites.
        /// </summary>
        /// <returns>The sprite of the entry if it's a texture selector, otherwise null.</returns>
        internal Sprite GetSprite() => image ? image.sprite : null;

        /// <summary>
        /// Retrieves the color of the image if available; otherwise returns white.
        /// </summary>
        /// <returns>The color of the image or Color.white if the image is null.</returns>
        internal Color GetColor() => image ? image.color : Color.white;
    }
}
