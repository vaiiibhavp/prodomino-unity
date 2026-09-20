using UnityEngine;
using UnityEngine.UI;
using TMPro; // Asegúrate de tener esta línea si usas TextMeshPro
using UnityEngine.EventSystems;

public class HoverTextColorChanger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI textToChange;
    public Color normalColor = Color.white;
    public Color hoverColor = Color.black;

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
        if (textToChange != null)
        {
            textToChange.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (textToChange != null)
        {
            textToChange.color = normalColor;
        }
    }
}