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
