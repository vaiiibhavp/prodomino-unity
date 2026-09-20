using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.NavigationSystem
{
    public class NavigationPanelController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CustomButtonToggleGroupUI customButtonToggleGroupUI;
        [SerializeField] private Transform navigationPanelsRoot;
        [SerializeField] private CustomButtonUI[] externalNavigationButtons;
        [SerializeField] private Button learnButton;

        private INavigationPanel[] navigationPanels;
        private Dictionary<INavigationPanel, bool> panelsBlockedByDefault;
        private GameManager gameManager;
        private AuthManager authManager;

        public CustomButtonToggleGroupUI CustomButtonToggleGroupUI => customButtonToggleGroupUI;
        public NavigationPanelType NavigationPanelType { get; private set; }
        public INavigationPanel ActiveNavigationPanel => navigationPanels?.FirstOrDefault(panel => panel.NavigationPanelType == NavigationPanelType);
        
        private void Awake()
        {
            // Get the authentication manager from the service locator
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();

            // Get all INavigationPanel components in children
            navigationPanels = navigationPanelsRoot?.GetComponentsInChildren<INavigationPanel>();

            // Register at start the default navigations panels block state; This will be used to determine if the button could change its interactibility state
            panelsBlockedByDefault = navigationPanels?.ToDictionary(
                panel => panel,
                panel => 
                {
                    if (panel is null)
                    { 
                        Debug.LogWarning("Null INavigationPanel found in navigationPanels array.");
                        return true;
                    }

                    var customButtonUI = customButtonToggleGroupUI?.GetButtonUI(panel.NavigationPanelType.ToString())
                        ?? externalNavigationButtons?.FirstOrDefault(x => x?.CustomButtonID == panel.NavigationPanelType.ToString());
                    return !customButtonUI?.IsInteractable ?? true;
                }
            ) ?? new Dictionary<INavigationPanel, bool>();

            // By default, block every navigation button that requires authentication
            SetBlockToRequiredNavigationButtons(false);

            // Set the button toggle group UI callback
            customButtonToggleGroupUI.SetOnCustomButtonSelectedCallback(OnNavigationButtonSelected);

            // If external navigation buttons are provided, add listeners to them
            if (externalNavigationButtons is not null and { Length: > 0 })
                foreach (var button in externalNavigationButtons)
                    button.AddEventToListener(() =>
                    {
                        var id = button.CustomButtonID;
                        OnNavigationButtonSelected(id);
                    });

            // Add listener for learn button if provided
            if (learnButton)
                learnButton.onClick.AddListener(() =>
                {
                    var id = NavigationPanelType.Learn.ToString();
                    OnLearnButtonPressed();
                });

            // Add listener for sign in event
            gameManager?.HandleOnSignIn(OnSignIn);
            authManager?.HandleOnCheckEmailVerification(OnCheckEmailVerification);

            // Add listener for sign out event
            gameManager?.HandleOnSignOut(OnSignOut);

            // Wait until the GameManager is initialized
            if (gameManager.IsAlreadyInitialized)
                OnSignIn();
            else
                OnSignOut();
        }

        /// <summary>
        /// Activates the Learn navigation panel if it exists and is not blocked by default; otherwise, logs a warning.
        /// </summary>
        private void OnLearnButtonPressed()
        {
            // Find the selected INavigationPanel by ID. If not found, activate the inner screen
            var selectedModule = navigationPanels.FirstOrDefault(x => x.NavigationPanelType == NavigationPanelType.Learn);
            if (selectedModule != null)
                selectedModule.SetActiveNavigationPanel(true);
            else
                Debug.LogWarning($"No INavigationPanel found for type: {NavigationPanelType.Learn}");
        }

        private void Start()
        {
            // Set the default active navigation panel
            if (NavigationPanelType is NavigationPanelType.None)
            {
                var defaultPanelType = NavigationPanelType.QuickMatch.ToString();
                var customButtonUI = customButtonToggleGroupUI.GetButtonUI(defaultPanelType)
                    ?? externalNavigationButtons?.FirstOrDefault(x => x.CustomButtonID == defaultPanelType) 
                    ?? customButtonToggleGroupUI.GetFirstButtonUI();

                if (customButtonUI != null)
                    customButtonUI.Select();
            }
        }

        private void OnDestroy()
        {
            // Remove listeners to avoid memory leaks
            gameManager?.UnHandleOnSignIn(OnSignIn);
            authManager?.UnHandleOnCheckEmailVerification(OnCheckEmailVerification);
            gameManager?.UnHandleOnSignOut(OnSignOut);
        }

        /// <summary>
        /// Activates the selected INavigationPanel and deactivates all others.
        /// </summary>
        /// <param name="panelType"></param>
        private void SetActiveNavigationPanel(NavigationPanelType panelType)
        {
            if (navigationPanels is null or { Length: 0 })
            {
                Debug.LogWarning("No INavigationPanels components found.");
                return;
            }

            // Deactivate all modules. One failing panel must not stop the selected one from opening.
            foreach (var panel in navigationPanels)
            {
                try { panel.SetActiveNavigationPanel(false); }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Find the selected INavigationPanel by ID. If not found, activate the inner screen
            var selectedModule = navigationPanels.FirstOrDefault(x => x.NavigationPanelType == panelType);
            if (selectedModule != null)
            {
                if (panelsBlockedByDefault.TryGetValue(selectedModule, out var isBlockByDefault) && !isBlockByDefault)
                { 
                    NavigationPanelType = panelType;
                    selectedModule.SetActiveNavigationPanel(true);
                }
                else
                    Debug.LogWarning($"NavigationPanel {panelType} is blocked by default and cannot be activated.");
            }
            else
                Debug.LogWarning($"No INavigationPanel found for type: {panelType}");
        }

        /// <summary>
        /// Calls the external activation of a navigation panel by its type.<br></br>
        /// This call the function from the CustomButtonToggleGroupUI to select the button UI associated with the navigation panel type.
        /// </summary>
        /// <param name="navigationPanelType"></param>
        public void ExternalActivateNavigationPanel(NavigationPanelType navigationPanelType)
        {
            var customButtonUI = customButtonToggleGroupUI.GetButtonUI(navigationPanelType.ToString());

            if (customButtonUI != null)
                customButtonUI.Select();
            else
                Debug.LogWarning($"CustomButtonUI for NavigationPanelType {navigationPanelType} not found.");
        }

        /// <summary>
        /// Method used to block or unblock the navigation panel interaction.
        /// </summary>
        /// <param name="isInteractable"></param>
        internal void SetInteractable(bool isInteractable)
        {
            canvasGroup.SetActive(isInteractable, isSettingAlpha: false);
        }

        private void SetBlockToRequiredNavigationButtons(bool isInteractable)
        {
            if (navigationPanels is not null and { Length: > 0 })
                foreach (var panel in navigationPanels)
                    if (panel.RequiresAuthentication)
                    {
                        var customButtonUI = customButtonToggleGroupUI.GetButtonUI(panel.NavigationPanelType.ToString());
                        customButtonUI?.SetButtonInteractable
                            (customButtonUI.IsInteractableByDefault 
                            && isInteractable 
                            && (!panel.OptionalPredicate.HasValue || panel.OptionalPredicate.Value));
                        
                        if (customButtonUI && customButtonUI.TooltipContainer && customButtonUI.TooltipContainer.TryGetComponent<Image>(out var tooltipImage))
                            tooltipImage.raycastTarget = !isInteractable;
                    }
        }

        /// <summary>
        /// Called when a button is selected in the CustomButtonToggleGroupUI.
        /// </summary>
        /// <param name="id"></param>
        private void OnNavigationButtonSelected(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            // Find the selected LearningToolModule by ID
            if (Enum.TryParse<NavigationPanelType>(id, out var navigationPanelType))
                SetActiveNavigationPanel(navigationPanelType);
            else
                Debug.LogWarning($"NavigationPanelType {id} not found.");
        }


        /// <summary>
        /// Called when the user signs in.
        /// </summary>
        private void OnSignIn()
        {
            // Only set the block to required navigation buttons if the user is authenticated
            if (gameManager is not null)
            { 
                var enableNavigationButtons = gameManager.IsAuthenticatedAndVerified;
                SetBlockToRequiredNavigationButtons(enableNavigationButtons);

                if (enableNavigationButtons)
                    ActiveNavigationPanel?.OnUpdateLoginStatus(true);
            }
        }

        /// <summary>
        /// Called when the user signs out.
        /// </summary>
        private void OnSignOut()
        {
            Debug.Log("//-- OnSignOut Navigation");

            SetBlockToRequiredNavigationButtons(false);

            // If the user is signed out, set the active navigation panel to Play by default
            if (ActiveNavigationPanel is not null)
            {
                if (ActiveNavigationPanel.RequiresAuthentication)
                { 
                    var customButtonUI = customButtonToggleGroupUI.GetButtonUI(NavigationPanelType.Play.ToString())
                       ?? customButtonToggleGroupUI.GetFirstButtonUI();

                    if (customButtonUI != null)
                        customButtonUI.Select();
                }

                ActiveNavigationPanel.OnUpdateLoginStatus(false);
            }
        }

        /// <summary>
        /// Called when email verification is checked.
        /// </summary>
        private void OnCheckEmailVerification(bool isVerified)
        {
            if (isVerified)
                OnSignIn();
        }
    }
}
