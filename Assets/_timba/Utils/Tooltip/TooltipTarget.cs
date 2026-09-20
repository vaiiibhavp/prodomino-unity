using Timba.Patterns;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static TooltipController;

[RequireComponent(typeof(Graphic))]
public class TooltipTarget : MonoBehaviour, ITooltipTarget
{
    [field: SerializeField] public TooltipModel Model { get; private set; }

    [SerializeField] private UnityEvent onTooltipShown;
    [SerializeField] private UnityEvent onTooltipHidden;

    private Graphic _graphic;
    private TooltipController _tooltipController;

    public TooltipController TooltipController => _tooltipController = _tooltipController != null 
        ? _tooltipController 
        : ServiceLocator.Instance.GetService<TooltipController>();
    Graphic ITooltipTarget.TargetGraphic => _graphic = _graphic != null 
        ? _graphic 
        : GetComponent<Graphic>();

    public void SetMessage(string newMessage)
    {
        if (Model is null)
        {
            Debug.LogWarning($"Couldn't assign a new tooltip message: Model is null");
            return;
        }

        Model.OverrideMessage(newMessage);
    }

    public void OnTooltipShown()
    {
        onTooltipShown?.Invoke();
    }

    public void OnTooltipHidden()
    {
        onTooltipHidden?.Invoke();
    }
}
