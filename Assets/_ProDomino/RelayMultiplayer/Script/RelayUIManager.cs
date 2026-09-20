#if false
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public RelayManager relayManager;
    public InputField joinCodeInput;
    public Text joinCodeDisplay;

    public async void OnCreateRelayPressed()
    {
        string joinCode = await relayManager.CreateRelay();
        if (joinCode != null)
        {
            joinCodeDisplay.text = $"Join Code: {joinCode}";
        }
    }

    public async void OnJoinRelayPressed()
    {
        string code = joinCodeInput.text;
        await relayManager.TryToJoinRelay(code);
    }
}
#endif