using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Swaps a dropdown item's label color/weight when the item is selected (hovered or current value),
    /// so text stays readable on the highlighted item background.
    /// </summary>
    public class DropdownItemLabelTint : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color normalColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.07f, 0.07f, 0.09f, 1f);

        private void OnEnable() => Apply(false);

        public void OnSelect(BaseEventData eventData) => Apply(true);

        public void OnDeselect(BaseEventData eventData) => Apply(false);

        private void Apply(bool selected)
        {
            if (!label) return;
            label.color = selected ? selectedColor : normalColor;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
