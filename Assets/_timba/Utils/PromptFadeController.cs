using System.Collections;
using TMPro;
using UnityEngine;

public class PromptFadeController : MonoBehaviour, IService
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text label;
    private Coroutine fadeCoroutine;

    public bool IsAlreadyInitialized => true;

    private void Awake()
    {
        if (canvasGroup)
            canvasGroup.alpha = 0f;
        else
            Debug.LogWarning("CanvasGroup is not assigned.");
    }

    public void Fade(string message, float secondsShown = 2, float fadeInDuration = .5f, float fadeOutDuration = 1)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("CanvasGroup is not assigned.");
            return;
        }

        if (string.IsNullOrEmpty(message) || label == null)
        {
            Debug.LogWarning("Label is not assigned or message is empty.");
            return;
        }

        label.text = message;
        
    fade:
        if (fadeCoroutine is null)
            fadeCoroutine = StartCoroutine(FadeCoroutine());
        else
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
            goto fade;
        }

        IEnumerator FadeCoroutine()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.gameObject.SetActive(true);

            yield return Fade(true, fadeInDuration);
            yield return new WaitForSeconds(secondsShown);

            yield return Fade(false, fadeInDuration);
            canvasGroup.gameObject.SetActive(false);

            fadeCoroutine = null;
        }
    }

    private IEnumerator Fade(bool isIn, float fadeTransitionDuration)
    {
        float startAlpha = canvasGroup.alpha;
        float endAlpha = isIn ? 1 : 0f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeTransitionDuration);
            yield return null;
        }
        canvasGroup.alpha = endAlpha;
    }
}
