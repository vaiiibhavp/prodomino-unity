using UnityEngine;
using UnityEngine.UI;

public class SimplyEmailVerifactionController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image image;

    private void Update()
    {
        if (!canvasGroup)
            return;

        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKey(KeyCode.Space))
        { 
            if (canvasGroup.alpha is not 1)
                canvasGroup.SetActive(true);
        }

        else if (canvasGroup.alpha is not 0)
            canvasGroup.SetActive(false);
    }

    public void CheckEmailVerification(bool isVerificated)
    { 
        if (image)
            image.color = isVerificated ? Color.green : Color.red;
    }
}
