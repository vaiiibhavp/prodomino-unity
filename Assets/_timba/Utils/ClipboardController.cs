using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ClipboardController : MonoBehaviour
{
    [SerializeField] private TMP_Text textLabel;
    [SerializeField] private UnityEvent<string> onCopyToClipboard;

    [DllImport("__Internal")]
    private static extern void CopyTextToClipboard(string text);

    public void CopyToClipboard()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (textLabel != null)
            CopyTextToClipboard(textLabel.text);
        else
            Debug.LogWarning("ClipboardController: textLabel is not assigned.");
#else
        GUIUtility.systemCopyBuffer = textLabel.text;
        Debug.Log("ClipboardController: Copied to clipboard: " + textLabel.text);
        
        onCopyToClipboard?.Invoke($"Player ID (<b>{textLabel.text}</b>) copied to clipboard");
#endif
    }
}
