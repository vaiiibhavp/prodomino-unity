using Timba.Patterns;
using UnityEngine;

public class PrompFadeProxy : MonoBehaviour
{
    [SerializeField] private string message;
    [SerializeField] private float secondsShown = 1f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 1f;

    private PromptFadeController promptFadeController;

    private void Awake()
    {
        promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
        if (promptFadeController == null)
        {
            Debug.LogError("PromptFadeController not found in the scene.");
            return;
        }
    }

    /// <summary>
    /// Triggers the fade effect with the specified message and durations.<br></br>
    /// This method is called from unity event system.
    /// </summary>
    public void Fade()
    {
        if (promptFadeController == null)
        {
            Debug.LogError("PromptFadeController not found in the scene.");
            return;
        }

        promptFadeController.Fade(message, secondsShown, fadeInDuration, fadeOutDuration);
    }
    
    /// <summary>
    /// Triggers the fade effect with the specified message and durations.<br></br>
    /// This method is called from unity event system.
    /// </summary>
    public void Fade(string message)
    {
        if (promptFadeController == null)
        {
            Debug.LogError("PromptFadeController not found in the scene.");
            return;
        }

        promptFadeController.Fade(message, secondsShown, fadeInDuration, fadeOutDuration);
    }
}
