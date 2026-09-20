using UnityEngine;

public static class CanvasGroupExtensions 
{
    public static void SetActive(this CanvasGroup canvasGroup, bool isActive, bool isSettingAlpha = true, bool isSettingInteractable = true, bool isSettingBlocksRaycasts = true, float? optionalForcedAlpha = null)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("CanvasGroup is not assigned.");
            return;
        }
        if (isSettingAlpha)
            canvasGroup.alpha = isActive ? 1 : 0;

        if (isSettingInteractable)
            canvasGroup.interactable = canvasGroup.alpha is 1 ? isActive : false;

        if (isSettingBlocksRaycasts)
            canvasGroup.blocksRaycasts = canvasGroup.alpha is 1 ? isActive : false;

        canvasGroup.alpha = optionalForcedAlpha ?? canvasGroup.alpha;
    }
}
