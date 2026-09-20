using UnityEngine.EventSystems;
using UnityEngine.UI;
using static TooltipController;

public interface ITooltipTarget : IPointerEnterHandler, IPointerExitHandler
{
    TooltipController TooltipController { get; }
    Graphic TargetGraphic { get; }
    TooltipModel Model { get; }

    void OnPointerEnterProxy(PointerEventData eventData)
    {
        if (TargetGraphic != null)
        {
            TooltipController.ShowAtUI(TargetGraphic.rectTransform, Model);
            OnTooltipShown();
        }
    }

    void OnTooltipShown() { }
    void OnTooltipHidden() { }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        OnPointerEnterProxy(eventData);
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        // Prevent hiding if pointer is still over the tooltip
        if (TooltipController?.IsPointerOverTooltip() ?? true)
            return;

        TooltipController.Hide();
        OnTooltipHidden();
    }
}
