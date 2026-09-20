using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleHelper : MonoBehaviour
{
    [SerializeField, Header(nameof(ToggleSprite) + "_Params")] 
    private Sprite onSprite;
    [SerializeField]
    private Sprite offSprite;

    [SerializeField, Header(nameof(ToggleSeparateEvents) + "_Params")] 
    private UnityEvent onToggleOn;
    [SerializeField] 
    private UnityEvent onToggleOff;

    [SerializeField, Header(nameof(ToggleTargetGrafic) + "_Params")] 
    private Image onImage;
    [SerializeField] private Image offImage;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    public void ToggleSprite(bool isOn)
    {
        if (onSprite == null || offSprite == null)
        {
            Debug.LogWarning("No sprites assigned to the ToggleHelper component.");
            return;
        }
        toggle.image.sprite = isOn ? onSprite : offSprite;
    }
    
    public void ToggleSeparateEvents(bool isOn)
    {
        if (onToggleOn == null || onToggleOff == null)
        {
            Debug.LogWarning("No events assigned to the ToggleHelper component.");
            return;
        }
        (isOn ? onToggleOn : onToggleOff)?.Invoke();
    }

    public void ToggleTargetGrafic(bool isOn)
    {
        if (onImage == null || offImage == null)
        {
            Debug.LogWarning("No images assigned to the ToggleHelper component.");
            return;
        }
        toggle.targetGraphic = isOn ? onImage : offImage;
    }
}
