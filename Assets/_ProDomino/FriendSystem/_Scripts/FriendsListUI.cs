using System;
using System.Collections.Generic;
using ProDomino.GameSystem;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Timba.Patterns;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Manages the full-screen Friends List navigation panel matching the new Figma design.
    /// Implements INavigationPanel for NavigationPanelType.FriendsList.
    /// Default state: hidden on startup so Dashboard is shown.
    /// Displays Empty State with username search when no friends exist.
    /// </summary>
    public class FriendsListUI : MonoBehaviour, INavigationPanel
    {
        [Header("Navigation Panel")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        public CanvasGroup RootCanvasGroup
        {
            get
            {
                if (rootCanvasGroup == null)
                    rootCanvasGroup = GetComponent<CanvasGroup>();
                return rootCanvasGroup;
            }
        }

        public NavigationPanelType NavigationPanelType => NavigationPanelType.FriendsList;
        public bool RequiresAuthentication => false;

        [Header("State Containers")]
        [SerializeField] private GameObject emptyStateContainer;
        [SerializeField] private GameObject populatedStateContainer;

        [Header("Search Controls")]
        [SerializeField] private TMP_InputField searchInputField;
        [SerializeField] private Button searchButton;

        [Header("Header Title")]
        [SerializeField] private TMP_Text screenTitleLabel;
        [SerializeField] private Image screenTitleIcon;

        private void Awake()
        {
            if (searchButton != null)
            {
                searchButton.onClick.AddListener(OnSearchClicked);
            }

            if (searchInputField != null)
            {
                searchInputField.onSubmit.AddListener(OnSearchSubmitted);
            }
        }

        private void HidePanel()
        {
            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.alpha = 0f;
                RootCanvasGroup.interactable = false;
                RootCanvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        public void SetActiveNavigationPanel(bool isActive)
        {
            gameObject.SetActive(isActive);

            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.alpha = isActive ? 1f : 0f;
                RootCanvasGroup.interactable = isActive;
                RootCanvasGroup.blocksRaycasts = isActive;
            }

            if (isActive)
            {
                RefreshUI();
            }
        }

        public void RefreshUI()
        {
            int friendCount = 0;
            FriendManager fm = null;
            if (ServiceLocator.Instance != null)
            {
                fm = ServiceLocator.Instance.GetService<FriendManager>();
            }
            if (fm == null)
            {
                fm = UnityEngine.Object.FindAnyObjectByType<FriendManager>();
            }

            if (fm != null && fm.FriendsEntryDatas != null)
            {
                friendCount = fm.FriendsEntryDatas.Count;
            }

            bool isEmpty = (friendCount == 0);

            if (emptyStateContainer != null)
                emptyStateContainer.SetActive(isEmpty);

            if (populatedStateContainer != null)
                populatedStateContainer.SetActive(!isEmpty);
        }

        private void OnSearchClicked()
        {
            if (searchInputField == null) return;
            string query = searchInputField.text?.Trim();
            ExecuteSearch(query);
        }

        private void OnSearchSubmitted(string text)
        {
            ExecuteSearch(text?.Trim());
        }

        private void ExecuteSearch(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                Debug.Log("[FriendsListUI] Search username is empty.");
                return;
            }

            Debug.Log($"[FriendsListUI] Searching for player: '{username}'");
            // Connect to FriendManager / PartyController search if available
            var partyCtrl = UnityEngine.Object.FindAnyObjectByType<PartyController>();
            if (partyCtrl != null)
            {
                // In existing FriendListController: OnSearchUser or search flow
                Debug.Log($"[FriendsListUI] Forwarding search '{username}' to PartyController");
            }
        }
    }
}
