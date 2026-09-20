using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Insights
{
    public class ExposedTile : CustomButtonUI
    {
        [SerializeField] private Image Image;
        private ExposedDomino _domino;

        public ExposedDomino Domino => _domino ??= new();
        public Sprite Sprite => Image.sprite;

        public void ConfigureDomino(int id, int topIndex, int bottomIndex, bool available, bool portraitOrientation, Sprite sprite)
        {
            SetCustomButtonID(id.ToString());
            Domino.Configure(id, topIndex, bottomIndex, available, portraitOrientation);
            if (Image)
                Image.sprite = sprite;
        }

        public void SetInteractibity(bool interactable)
        {
            if (button)
                button.interactable = interactable;

            if (canvasGroup_toggle != null)
                canvasGroup_toggle.SetActive(interactable, isSettingAlpha: false);

            if (canvasGroup_hover != null)
                canvasGroup_hover.SetActive(interactable, isSettingAlpha: false);
        }
    }
}
