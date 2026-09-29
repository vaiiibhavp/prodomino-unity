using System;
using UnityEngine;

namespace ProDomino.Shared
{
    public interface INavigationPanel
    {
        public NavigationPanelType NavigationPanelType { get; }
        public CanvasGroup RootCanvasGroup { get; }

        /// <summary>
        /// Indicates whether authentication is required to access this panel.
        /// </summary>
        public bool RequiresAuthentication { get; }

        /// <summary>
        /// An optional predicate that determines if the panel can be activated.
        /// </summary>
        public virtual bool? OptionalPredicate => null;

        /// <summary>
        /// Overlay panels open on top of the current screen (popup) instead of replacing it.
        /// The sidebar selection is restored to the previous panel after opening.
        /// </summary>
        public virtual bool IsOverlay => false;

        /// <summary>
        /// Overlay panels call this when they close themselves (e.g. popup close button).
        /// </summary>
        public virtual void SetOverlayClosedCallback(Action onClosed) { }

        /// <summary>
        /// Overlay panels report whether their popup is currently shown.
        /// </summary>
        public virtual bool IsOverlayOpen => false;

        public virtual void SetActiveNavigationPanel(bool isActive)
        { 
            if (this is MonoBehaviour mb && mb != null)
            {
                if (isActive && !mb.gameObject.activeSelf)
                    mb.gameObject.SetActive(true);
            }
            RootCanvasGroup?.SetActive(isActive);
            if (RootCanvasGroup != null)
            {
                try { RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive(); } catch { }
            }
        }

        public virtual void OnUpdateLoginStatus(bool isLogged) { }
    }
}
