using UnityEngine;
using UnityEngine.UI;
using TMPro; // Asegúrate de tener esta línea si usas TextMeshPro
using UnityEngine.EventSystems;

public class HoverTextColorChanger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public TextMeshProUGUI textToChange;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.black;
    [Tooltip("Also apply hoverColor while this object is the EventSystem selection (e.g. the current dropdown option).")]
    public bool applyOnSelect;

    private bool isHovered;
    private bool isSelected;

    void Start()
    {
        // Asegurarse de que el texto existe
        if (textToChange == null)
        {
            textToChange = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        if (applyOnSelect) Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        if (applyOnSelect) Refresh();
    }

    private void Refresh()
    {
        if (textToChange != null)
        {
            textToChange.color = isHovered || (applyOnSelect && isSelected) ? hoverColor : normalColor;
        }
    }
}
