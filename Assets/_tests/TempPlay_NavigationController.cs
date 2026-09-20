using ProDomino.Shared;
using UnityEngine;

public class TempPlay_NavigationController : MonoBehaviour, INavigationPanel
{
    public NavigationPanelType NavigationPanelType => NavigationPanelType.Play;
    public bool RequiresAuthentication => false;

    [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
}
