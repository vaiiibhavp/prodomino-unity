using Cysharp.Threading.Tasks;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameSystem;
using ProDomino.NavigationSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using UnityEngine;

namespace ProDomino.QuickMatchSystem
{ 
    public class QuickMatchController : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }

        [SerializeField] private NavigationPanelController navigationPanelController;
        [SerializeField] private GameModeConfig gameModeConfig;
        [SerializeField] private CustomButtonUI 
            aiButton, casualPlayerButton, competitivePlayerButton;
        [SerializeField] private GameObject competitiveQuickMatchObject;
        [SerializeField] private GameObject partyObject;

        private GameManager gameManager;
        private AuthManager authManager;
        private DifficultyLevel[] difficulties;
        private const GameMode gameModeAI = GameMode.french;
        private const NumberPlayers numberPlayers = NumberPlayers.oneVsOne;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.QuickMatch;
        public bool RequiresAuthentication => false;
        public bool IsInParty => gameModeConfig?.IsPartyRelay ?? false; //PartyController.IsRelay;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();

            difficulties = Enum.GetValues(typeof(DifficultyLevel))
                .Cast<DifficultyLevel>()
                .ToArray();

            gameManager.HandleOnSignIn(OnSignIn);
            gameManager.HandleOnSignOut(OnSignOut);

            if (aiButton)
                aiButton.onClick.AddListener(StartQuickAIMatch);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] AI Button is not assigned in the inspector.");

            if (casualPlayerButton)
                casualPlayerButton.onClick.AddListener(StartQuickCasualMatch);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Casual Player Button is not assigned in the inspector.");

            if (competitivePlayerButton)
                competitivePlayerButton.onClick.AddListener(StartQuickCompetitiveMatch);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Competitive Player Button is not assigned in the inspector.");
        }

        private void Start()
        {
            PartyController.HandleOnJoinParty(OnJoinParty);
            PartyController.HandleOnLeaveParty(OnLeaveParty);

            UpdateUI();
        }

        private void OnDestroy()
        {
            PartyController.UnHandleOnJoinParty(OnJoinParty);
            PartyController.UnHandleOnLeaveParty(OnLeaveParty);
        }

        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            RootCanvasGroup?.SetActive(isActive);
            UpdateUI();

            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Starts a quick AI match.
        /// </summary>
        private void StartQuickAIMatch()
        { 
            if (!gameModeConfig || !navigationPanelController)
            {
                Debug.LogError($"[{nameof(QuickMatchController)}] required variables are not assigned in the inspector.");
                return;
            }

            if (IsInParty)
            {
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Cannot start VSAI match: user is in a party.");
                UpdateUI();
                return;
            }

            // Check if there is a current match. If not, start a new one
            if (!gameModeConfig.IsInMatch)
            {                  
                var randomDifficulty = difficulties.ElementAtOrDefault(UnityEngine.Random.Range(0, difficulties.Length));
                gameModeConfig.SetExternalGameData(gameModeAI, GameType.singlePlayerIA, numberPlayers, randomDifficulty);
                gameModeConfig.RunGameMode();
            }
            navigationPanelController.CustomButtonToggleGroupUI.DeselectAll();
            navigationPanelController.ExternalActivateNavigationPanel(NavigationPanelType.Play);
        }

        /// <summary>
        /// Starts a quick casual match.
        /// </summary>
        private void StartQuickCasualMatch()
        {
            if (!gameModeConfig || !navigationPanelController)
            {
                Debug.LogError($"[{nameof(QuickMatchController)}] required variables are not assigned in the inspector.");
                return;
            }

            // Check if there is a current match. If not, start a new one
            if (!gameModeConfig.IsInMatch)
            {
                var minNumberOfPlayers = numberPlayers;

                // Adjust number of players if in party
                if (IsInParty)
                    minNumberOfPlayers = PartyController.PartyCount switch
                    {
                        2 => NumberPlayers.oneVsOne,
                        3 or 4 => NumberPlayers.oneVsThree,
                        _ => numberPlayers
                    };

                gameModeConfig.SetExternalGameData(gameModeAI, GameType.casual, numberPlayers);
                gameModeConfig.RunGameMode();
            }
            navigationPanelController.CustomButtonToggleGroupUI.DeselectAll();
            navigationPanelController.ExternalActivateNavigationPanel(NavigationPanelType.Play);
        }

        /// <summary>
        /// Starts a quick competitive match if the user is authenticated.
        /// </summary>
        private void StartQuickCompetitiveMatch()
        {
            if (!gameModeConfig || !navigationPanelController)
            {
                Debug.LogError($"[{nameof(QuickMatchController)}] required variables are not assigned in the inspector.");
                return;
            }

            if (gameManager is null || !gameManager.IsAuthenticated)
            {
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Cannot start competitive match: user is not authenticated.");
                UpdateUI();
                return;
            }

            if (!gameModeConfig.IsInMatch)
            {
                var minNumberOfPlayers = numberPlayers;

                // Adjust number of players if in party
                if (IsInParty)
                    minNumberOfPlayers = PartyController.PartyCount switch
                    {
                        2 => NumberPlayers.oneVsOne,
                        3 or 4 => NumberPlayers.oneVsThree,
                        _ => numberPlayers
                    };

                gameModeConfig.SetExternalGameData(gameModeAI, GameType.competitive, minNumberOfPlayers);
                gameModeConfig.RunGameMode();
            }

            navigationPanelController.CustomButtonToggleGroupUI.DeselectAll();
            navigationPanelController.ExternalActivateNavigationPanel(NavigationPanelType.Play);
        }

        /// <summary>
        /// Updates the UI elements based on the authentication state.
        /// </summary>
        private void UpdateUI()
        {
            var isLeader = PartyController.MainPartyMembers?.PartyEntries.Any(x => x.IsLeader && x.PartyEntryData?.PlayerID == authManager.UUID) ?? false;

            // Disable VSAI button if in party
            var shouldEnableVSAIButton = !IsInParty;

            // Only enable casual button if not in party or is the leader
            var shouldEnableCasualButton = !IsInParty || isLeader;

            // If hte user is logged in, enable the competitive button; otherwise, disable it
            var shouldEnableCompetitiveButton = gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true} 
                && !IsInParty;

            // Determine if the VSAI button should be interactable
            if (aiButton)
                aiButton.SetButtonInteractable(shouldEnableVSAIButton);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] AI Button is not assigned in the inspector.");

            // Determine if the casual button should be interactable
            if (casualPlayerButton)
                casualPlayerButton.SetButtonInteractable(shouldEnableCasualButton);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Casual Player Button is not assigned in the inspector.");

            // Determine if the competitive button should be interactable
            if (competitivePlayerButton)
                competitivePlayerButton.SetButtonInteractable(shouldEnableCompetitiveButton);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Competitive Player Button is not assigned in the inspector.");


            // Show or hide the competitive quick match object based on authentication state
            if (competitiveQuickMatchObject)
                competitiveQuickMatchObject.SetActive(!shouldEnableCompetitiveButton);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Competitive Quick Match Object is not assigned in the inspector.");

            // Show or hide the party object based on party state
            if (partyObject)
                partyObject.SetActive(IsInParty);
            else
                Debug.LogWarning($"[{nameof(QuickMatchController)}] Party Object is not assigned in the inspector.");
        }

        private void OnSignIn()
        {
            UpdateUI();
        }

        private void OnSignOut()
        {
            UpdateUI();
        }

        private UniTask OnLeaveParty(string playerId)
        {
            UpdateUI();
            return UniTask.CompletedTask;
        }

        private UniTask OnJoinParty(string playerId)
        {
            UpdateUI();
            return UniTask.CompletedTask;
        }
    }
}
