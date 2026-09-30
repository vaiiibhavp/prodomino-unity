using System;
using System.Linq;
using ProDomino.Shared;
using UnityEngine;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Sidebar "Party" entry: opens the Create Party screen (PartyUI_NavPanel's PartyController) as an overlay,
    /// the same way the Friends List entry opens FriendList_Popup. The panel behind is hidden while it is open
    /// and restored with the sidebar highlight when it closes.
    /// </summary>
    [RequireComponent(typeof(PartyController))]
    public class PartyNavPanel : MonoBehaviour, INavigationPanel
    {
        [SerializeField] private PartyController partyController;
        [SerializeField] private CanvasGroup rootCanvasGroup;

        private Action onOverlayClosed;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Party;
        public bool RequiresAuthentication => false;
        public bool IsOverlay => true;

        public CanvasGroup RootCanvasGroup => rootCanvasGroup ? rootCanvasGroup : rootCanvasGroup = GetComponent<CanvasGroup>();
        private PartyController Controller => partyController ? partyController : partyController = GetComponent<PartyController>();

        public bool IsOverlayOpen => RootCanvasGroup && RootCanvasGroup.alpha > 0f;

        public void SetOverlayClosedCallback(Action onClosed)
        {
            onOverlayClosed = onClosed;
            Controller.OnFriendListVisibilityChanged -= OnVisibilityChanged;
            Controller.OnFriendListVisibilityChanged += OnVisibilityChanged;
        }

        public void SetActiveNavigationPanel(bool isActive)
        {
            // Closing an already hidden screen would raise a redundant close event
            if (!isActive && !IsOverlayOpen)
                return;

            Controller.SetVisibility(isActive);
        }

        private void OnVisibilityChanged(bool isVisible)
        {
            if (!isVisible)
            {
                onOverlayClosed?.Invoke();
                return;
            }

            // Opened from elsewhere (sidebar Invite card): select the Party entry so it highlights
            // and the navigation hides the panel behind, same as clicking Party itself
            if (!partyButton)
                partyButton = FindObjectsByType<CustomButtonUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.CustomButtonID == NavigationPanelType.Party.ToString());
            if (partyButton && !partyButton.IsSelected)
                partyButton.Select();
        }

        private CustomButtonUI partyButton;

        private void OnDestroy()
        {
            if (partyController)
                partyController.OnFriendListVisibilityChanged -= OnVisibilityChanged;
        }
    }
}
