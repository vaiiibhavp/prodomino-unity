using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using UnityEngine;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.FirestoreClubData;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages the club-related user interface, including navigation, data display, and interaction with club members,
    /// ranks, chat, applications, and club search screens.
    /// </summary>
    internal class ClubUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
        [SerializeField] private CustomButtonToggleGroupUI clubHomeScreenTabsGroupUI;

        [Header("Club UI Screens")]
        [SerializeField] private CanvasGroup clubSearchCanvasGroup;
        [SerializeField] private CanvasGroup clubDataConfigurationGroup;
        [SerializeField] private CanvasGroup ClubIconCreationGroup;
        [SerializeField] private CanvasGroup ClubHomeScreenGroup;

        [Header("Club Home Screen Tabs")]
        [SerializeField] private CanvasGroup clubMembersTabGroup;
        [SerializeField] private CanvasGroup clubRanksTabGroup;
        [SerializeField] private CanvasGroup clubChatTabGroup;
        [SerializeField] private CanvasGroup clubInvitePlayersTabGroup;
        [SerializeField] private CanvasGroup clubApplicationsTabGroup;

        [Header("Club Controllers")]
        [SerializeField] private ClubDataSettingsController clubDataSettingsController;
        [SerializeField] private ClubHomeScreenDataUI clubHomeScreenDataUI;
        [SerializeField] private ClubMemberController clubMemberController;
        [SerializeField] private ClubSearchController clubSearchController;
        [SerializeField] private ClubApplicantController clubApplicantController;
        [SerializeField] private ClubChatController clubChatController;

        private CanvasGroup[] allUIScreens;
        private CanvasGroup[] allHomeScreenTabs;

        private PromptFadeController promptFadeController;

        // Data retrieval functions
        private Func<MemberData> getPlayerMemberData;
        private Func<FirestoreClubData> getClubData;
        private Func<FirestoreClubChatData> getClubChatData;
        private AsyncActionHandler<bool> onSetActiveClubUI;

        // ClubDataSettingsController related actions
        private AsyncActionHandler<IconData, string, string, bool> onTryToUpdateClubData;

        // ClubMemberController related actions
        private AsyncFuncHandler<bool, ClubRanksTypes, MemberData> tryToOverrideRank;
        private AsyncFuncHandler<bool, MemberData> tryToRemoveMember;

        // ClubSearchController related actions
        private AsyncFuncHandler<FirestoreClubData[]> tryToGetLeaderboardEntries;
        private AsyncFuncHandler<FirestoreClubData[], string> tryToSearchClubsByName;
        private AsyncFuncHandler<bool, FirestoreClubData> tryToSendJoinRequest;

        // ClubApplicantController related actions
        private AsyncFuncHandler<bool, ApplicantData> tryToAcceptJoiningRequest;
        private AsyncFuncHandler<bool, ApplicantData> tryToDeclineJoiningRequest;

		// ClubChatController related actions
		private AsyncFuncHandler<bool, string> tryToSendClubChatMessage;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Club;
        public bool RequiresAuthentication => true;

        /// <summary>
        /// Check if the user is part of the club<br></br>
        /// This property checks if the user STILLS being a club's member (if the data is not null, but the member properry doesn't contains your anymore)
        /// </summary>
        internal bool IsUserInClub => getPlayerMemberData?.Invoke() is not null;

        /// <summary>
        /// The main data related of the club that the player has
        /// </summary>
        internal FirestoreClubData ClubData => getClubData?.Invoke();
        internal FirestoreClubChatData ClubChatData => getClubChatData?.Invoke();

        internal ClubUIScreen SelectedClubUIScreen { get; private set; }
        internal ClubHomeScreenTab SelectedClubHomeScreenTab { get; private set; }


        private void Awake()
        {
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            allUIScreens = new CanvasGroup[4]
            {
                clubSearchCanvasGroup,
                clubDataConfigurationGroup,
                ClubIconCreationGroup,
                ClubHomeScreenGroup
            };

            allHomeScreenTabs = new CanvasGroup[5]
            {
                clubMembersTabGroup,
                clubRanksTabGroup,
                clubChatTabGroup,
                clubInvitePlayersTabGroup,
                clubApplicationsTabGroup
            };

            if (clubHomeScreenTabsGroupUI)
                clubHomeScreenTabsGroupUI.SetOnCustomButtonSelectedCallback(OnSelectHomeScreenToggle);
            else
                Debug.LogWarning("ClubHomeScreenTabsGroupUI is null, cannot set the callback for tab selection");
        }

        private void Start()
        {
            // By default, turn off every screen
            UpdateScreen(ClubUIScreen.None);
        }

        /// <summary>
        /// Initialize the ClubUI with the given services and actions
        /// </summary>
        /// <param name="getConfigData">Function to get the configuration data.</param>
        /// <param name="getPlayerMemberData">Function to get the player's member data.</param>
        /// <param name="getClubData">Function to get the club data.</param>
        /// <param name="getClubChatData">Function to get the club chat data.</param>
        /// <param name="onSetActiveClubUI">Action to be called when the club UI is set active or inactive.</param>
        internal void Initialize
            (Func<ConfigData> getConfigData,
            Func<MemberData> getPlayerMemberData,
            Func<FirestoreClubData> getClubData,
            Func<FirestoreClubChatData> getClubChatData,
            AsyncActionHandler<bool> onSetActiveClubUI,

            // ClubDataSettingsController related actions
            AsyncActionHandler<IconData, string, string, bool> onTryToUpdateClubData,

            // ClubMemberController related actions
            AsyncFuncHandler<bool, ClubRanksTypes, MemberData> tryToOverrideRank,
            AsyncFuncHandler<bool, MemberData> tryToRemoveMember,

            // ClubSearchController related actions
            AsyncFuncHandler<FirestoreClubData[], string> tryToSearchClubsByName,
            AsyncFuncHandler<FirestoreClubData[]> tryToGetLeaderboardEntries,
            AsyncFuncHandler<bool, FirestoreClubData> tryToSendJoinRequest,

            // ClubApplicantController related actions
            AsyncFuncHandler<bool, ApplicantData> tryToAcceptJoiningRequest,
            AsyncFuncHandler<bool, ApplicantData> tryToDeclineJoiningRequest,

			// ClubChatController related actions
			AsyncFuncHandler<bool, string> tryToSendClubChatMessage,
            AsyncFuncHandler<bool> tryToSubscribeToClubChat,
            AsyncFuncHandler<bool> tryToUnsubscribeToClubChat,
            ref AsyncEventHandler<FirestoreClubChatData> refGetClubChatData)
		{
            // ClubDataProvider related actions
            this.getPlayerMemberData = getPlayerMemberData ?? throw new ArgumentNullException(nameof(getPlayerMemberData));
            this.getClubData = getClubData ?? throw new ArgumentNullException(nameof(getClubData));
            this.getClubChatData = getClubChatData ?? throw new ArgumentNullException(nameof(getClubChatData));
            this.onSetActiveClubUI = onSetActiveClubUI ?? throw new ArgumentNullException(nameof(onSetActiveClubUI));

            // ClubDataSettingsController related actions
            this.onTryToUpdateClubData = onTryToUpdateClubData ?? throw new ArgumentNullException(nameof(onTryToUpdateClubData));

            // ClubMemberController related actions
            this.tryToOverrideRank = tryToOverrideRank ?? throw new ArgumentNullException(nameof(tryToOverrideRank));
            this.tryToRemoveMember = tryToRemoveMember ?? throw new ArgumentNullException(nameof(tryToRemoveMember));

            // ClubSearchController related actions
            this.tryToSendJoinRequest = tryToSendJoinRequest ?? throw new ArgumentNullException(nameof(tryToSendJoinRequest));
            this.tryToGetLeaderboardEntries = tryToGetLeaderboardEntries ?? throw new ArgumentNullException(nameof(tryToGetLeaderboardEntries));
            this.tryToSearchClubsByName = tryToSearchClubsByName ?? throw new ArgumentNullException(nameof(tryToSearchClubsByName));

            // ClubApplicantController related actions
            this.tryToAcceptJoiningRequest = tryToAcceptJoiningRequest ?? throw new ArgumentNullException(nameof(tryToAcceptJoiningRequest));
            this.tryToDeclineJoiningRequest = tryToDeclineJoiningRequest ?? throw new ArgumentNullException(nameof(tryToDeclineJoiningRequest));

            // ClubChatController related actions
            this.tryToSendClubChatMessage = tryToSendClubChatMessage ?? throw new ArgumentNullException(nameof(tryToSendClubChatMessage));

            if (tryToSubscribeToClubChat is null)
                throw new ArgumentNullException(nameof(tryToSubscribeToClubChat));

            if (tryToUnsubscribeToClubChat is null)
                throw new ArgumentNullException(nameof(tryToUnsubscribeToClubChat));

            if (refGetClubChatData is null)
                throw new ArgumentNullException(nameof(refGetClubChatData));

            // Initialize the ClubDataSettingsController
            if (clubDataSettingsController)
                clubDataSettingsController.Initialize
                (
                    checkIfIsUserInClub: () => IsUserInClub,
                    goToIconCreation: GoToIconCreation,
                    goToDataSettingsScreen: GoToDataSettingsScreen,
                    tryToGoToHomeScreen: TryToGoToHomeScreen,
                    getConfigData: getConfigData,
                    getCurrentIconData: () => ClubData?.iconData,
                    getCurrentPlayerMemberData: this.getPlayerMemberData,
                    tryToUpdateClubData: OnSaveClubDataSelection
                );
            else
                Debug.LogError("ClubDataSettingsController is not assigned in the inspector.", this);

            // Initialize the ClubMemberController
            if (clubMemberController)
                clubMemberController.Initialize
                (
                    checkIfIsUserInClub: () => IsUserInClub,
                    openClubDataSettingsScreen: GoToDataSettingsScreen,
                    getClubData: () => ClubData,
                    getConfigData: getConfigData,
                    getCurrentPlayerMemberData: this.getPlayerMemberData,
                    tryToOverrideRank: OnTryToOverrideRank,
                    tryToRemoveMember: OnTryToRemoveMember
                );
            else
                Debug.LogError("ClubMemberController is not assigned in the inspector.", this);

            // Initialize the ClubSearchController
            if (clubSearchController)
                clubSearchController.Initialize
                (
                    tryToSearchClubsByName: OnTryToSearchClubsByName,
                    tryToSendJoiningRequest: OnTryToSendJoiningRequest,
                    getConfigData: getConfigData,
                    openClubDataSettings: GoToDataSettingsScreen
                );
            else
                Debug.LogError("ClubSearchController is not assigned in the inspector.", this);

            // Initialize the clubApplicantController
            if (clubApplicantController)
                clubApplicantController.Initialize
                (
                    getClubData: () => ClubData,
                    getConfigData: getConfigData,
                    getCurrentPlayerMemberData: this.getPlayerMemberData,
                    tryToAcceptJoiningRequest: OnTryToAcceptJoiningRequest,
                    tryToDeclineJoiningRequest: OnTryToDeclineJoiningRequest
                );
            else
                Debug.LogError("clubApplicantController is not assigned in the inspector.", this);

            // Initialize the clubChatController
            if (clubChatController)
                clubChatController.Initialize
                (
                    getClubData: () => ClubData,
                    getConfigData: getConfigData,
                    getCurrentPlayerMemberData: this.getPlayerMemberData,
					tryToSendClubChatMessage: OnTryToSendChatMessage,
					startListeningMessages: tryToSubscribeToClubChat,
                    stopListeningMessages: tryToUnsubscribeToClubChat,
                    refGetClubChatData: ref refGetClubChatData
                );
            else
                Debug.LogError("clubChatController is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Override of INavigationPanel.SetActiveNavigationPanel<br></br>
        /// Calls the onSetActiveClubUI action to notify the change
        /// </summary>
        /// <param name="isActive">Indicates whether the club UI is active or not.</param>
        async void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            RootCanvasGroup?.SetActive(isActive);
            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

            // Notify the change (this calls GameManager to try to update firestore player club data if the user is logged in)
            if (onSetActiveClubUI is not null)
                await onSetActiveClubUI(isActive);
            else
                Debug.LogWarning("onSetActiveClubUI is null, cannot notify the change");

            // If the panel is set active, show the appropriate screen
            if (isActive)
            {
                // If the user is not in a club, show the club search screen (temporary, should be club search)
                if (!IsUserInClub)
                {
                    UpdateScreen(ClubUIScreen.ClubSearch);

                    if (tryToGetLeaderboardEntries is not null)
                    { 
                        Debug.Log("User is not in a club, trying to get leaderboard entries...");

                        // Get the leaderboard entries and configure the club search controller
                        var leaderboardsEntries = await tryToGetLeaderboardEntries();
                        clubSearchController.ConfigureLeaderboardEntries(leaderboardsEntries);
                    }
                }

                // But if the user is in a club, show the club home screen with the members tab if no screen is selected
                else if (SelectedClubUIScreen is not ClubUIScreen.ClubHomeScreen)
                    UpdateScreen(ClubUIScreen.ClubHomeScreen, ClubHomeScreenTab.ClubMembers);
            }
            else
                UpdateScreen(ClubUIScreen.None);
        }

        /// <summary>
        /// Override of INavigationPanel.OnUpdateLoginStatus<br></br>
        /// When the user is logged in, it calls the onSetActiveClubUI action to notify the change
        /// </summary>
        /// <param name="isLogged">Indicates whether the user is logged in or not.</param>
        void INavigationPanel.OnUpdateLoginStatus(bool isLogged)
        {
            if (isLogged)
            { 
                if (onSetActiveClubUI is not null)
                    onSetActiveClubUI(true);
                else
                    Debug.LogWarning("onSetActiveClubUI is null, cannot notify the change");
            }
        }

        /// <summary>
        /// Event received when the club selector UI is set active or inactive
        /// </summary>
        /// <param name="clubUIScreen">The club UI screen to display.</param>
        /// <param name="clubHomeScreenTab">The club home screen tab to display.</param>
        private void UpdateScreen(ClubUIScreen clubUIScreen, ClubHomeScreenTab clubHomeScreenTab = ClubHomeScreenTab.None)
        {
            // Try to close a close-event when the subscreen changes
            if (SelectedClubHomeScreenTab is not ClubHomeScreenTab.None && SelectedClubHomeScreenTab != clubHomeScreenTab)
            {
                var onHideSubController = (Action)(SelectedClubHomeScreenTab switch
                {
                    ClubHomeScreenTab.ClubChat => clubChatController.OnCloseController,
                    _ => null,
                });

                onHideSubController?.Invoke();
            }

            SelectedClubUIScreen = clubUIScreen;
            SelectedClubHomeScreenTab = clubHomeScreenTab;

            // Get the screen to show
            var screenToShow = clubUIScreen switch
            {
                ClubUIScreen.ClubSearch => clubSearchCanvasGroup,
                ClubUIScreen.ClubDataConfiguration => clubDataConfigurationGroup,
                ClubUIScreen.ClubIconCreation => ClubIconCreationGroup,
                ClubUIScreen.ClubHomeScreen => ClubHomeScreenGroup,
                _ => null,
            };

            // If the screen to show is the home screen, get the tab to show
            var homeScreenTab = default(CanvasGroup);
            if (clubUIScreen is ClubUIScreen.ClubHomeScreen && clubHomeScreenTab is not ClubHomeScreenTab.None)
                homeScreenTab = clubHomeScreenTab switch
                {
                    ClubHomeScreenTab.ClubMembers => clubMembersTabGroup,
                    ClubHomeScreenTab.ClubRanks => clubRanksTabGroup,
                    ClubHomeScreenTab.ClubChat => clubChatTabGroup,
                    ClubHomeScreenTab.ClubInvitePlayers => clubInvitePlayersTabGroup,
                    ClubHomeScreenTab.ClubApplications => clubApplicationsTabGroup,
                    _ => null,
                };

            // Disable all screens except the one to show
            var screensToHide = (screenToShow != null ? allUIScreens.Except(new List<CanvasGroup>() { screenToShow }) : allUIScreens)?.ToList();
            screensToHide?.ForEach(x => x.SetActive(false));

            // Disable all home screen tabs except the one to show
            var homeScreensToHide = (homeScreenTab != null ? allHomeScreenTabs.Except(new List<CanvasGroup>() { homeScreenTab }) : allHomeScreenTabs)?.ToList();
            homeScreensToHide?.ForEach(x => x.SetActive(false));

            // Enable the screen to show
            screenToShow?.SetActive(true);

            // Enable the home screen tab to show
            homeScreenTab?.SetActive(true);

            // Update the button selection to match the default tab
            if (homeScreenTab)
            { 
                if (clubHomeScreenTabsGroupUI)
                    clubHomeScreenTabsGroupUI.GetButtonUI(clubHomeScreenTab.ToString())?.Select();
                else
                    Debug.LogWarning("ClubHomeScreenTabsGroupUI is null, cannot update the button selection");
            }

            var onShow = (Action)(clubUIScreen switch
            {
                ClubUIScreen.ClubDataConfiguration or ClubUIScreen.ClubIconCreation => clubDataSettingsController.OnOpenController,
                ClubUIScreen.ClubSearch => clubSearchController.OnOpenController,
                _ => null,
            });

            // Invoke the onShow action if exists
            onShow?.Invoke();

            var onShowSubController = (Action)(clubHomeScreenTab switch
            {
                ClubHomeScreenTab.ClubChat => clubChatController.OnOpenController,
                _ => null,
            });

            onShowSubController?.Invoke();

            // At the end, refresh the club data
            RefreshData();
        }

        /// <summary>
        /// Event received when the user discards the icon creation<br></br>
        /// By the moment, it only updates the screen to the club data configuration
        /// </summary>
        internal async void TryToGoToHomeScreen()
        {
            // If the user is in a club, go to the club home screen with the members tab; otherwise, go to the club search screen
            if (IsUserInClub)
                UpdateScreen(ClubUIScreen.ClubHomeScreen, ClubHomeScreenTab.ClubMembers);
            else
            { 
                UpdateScreen(ClubUIScreen.ClubSearch);

                if (tryToGetLeaderboardEntries is not null)
                {
                    Debug.Log("User is not in a club, trying to get leaderboard entries...");

                    // Get the leaderboard entries and configure the club search controller
                    var leaderboardsEntries = await tryToGetLeaderboardEntries();
                    clubSearchController.ConfigureLeaderboardEntries(leaderboardsEntries);
                }
            }
        }

        /// <summary>
        /// Event received when the user confirms the icon creation<br></br>
        /// By the moment, it does nothing
        /// </summary>
        internal void GoToIconCreation()
        {
            // Select the screen that will open the club icon selector
            UpdateScreen(ClubUIScreen.ClubIconCreation);
        }
        
        /// <summary>
        /// Event received when the user confirms the icon creation<br></br>
        /// By the moment, it only updates the screen to the club data configuration
        /// </summary>
        internal void GoToDataSettingsScreen()
        {
            // Select the screen before trying to update the club icon
            UpdateScreen(ClubUIScreen.ClubDataConfiguration);
        }

        /// <summary>
        /// Shows a prompt message using the PromptFadeController
        /// </summary>
        /// <param name="message">The message to display in the prompt.</param>
        internal void ShowPrompt(string message)
        {
            if (!promptFadeController)
            {
                Debug.LogWarning("PromptFadeController is null, cannot show error prompt");
                return;
            }

            promptFadeController.Fade(message, 5);
        }

        /// <summary>
        /// Refreshes the club data displayed in the UI components
        /// </summary>
        internal async void RefreshData()
        {
            // Configure the club member controller with the current club members
            var clubData = ClubData;

            if (clubData is not null)
            {
                var clubName = clubData?.clubName ?? "Unknown";
                var clubSlogan = clubData?.slogan ?? string.Empty;
                var clubRank = clubData?.clubRank.ToString() ?? "Unranked";
                var iconData = clubData.iconData ?? new IconData();
                var orderedMembers = clubData.members?.OrderBy(x => x.rank).ThenBy(x => x.memberName).ToArray();
                var applicants = clubData.applicants?.ToArray();

                // Configure the club member controller with the current club members
                if (clubMemberController)
                    await clubMemberController.Configure(orderedMembers);
                else
                    Debug.LogWarning("ClubMemberController is null, cannot configure the ClubMemberController");

                // Configure the club home screen data UI with the current club data
                if (clubHomeScreenDataUI)
                    clubHomeScreenDataUI.Configure(clubName, clubSlogan, clubRank, iconData);
                else
                    Debug.LogWarning("ClubHomeScreenDataUI is null, cannot configure the ClubHomeScreenDataUI");

                // Configure the club applicant controller with the current club applicants
                if (clubApplicantController)
                    await clubApplicantController.Configure(applicants);
                else
                    Debug.LogWarning("ClubApplicantController is null, cannot configure the ClubApplicantController");
            }

            var clubChatData = ClubChatData;
            if (clubChatData is not null)
            {
                // Configure the club applicant controller with the current club applicants
                if (clubChatController)
                    await clubChatController.Configure(clubChatData);
                else
                    Debug.LogWarning("clubChatController is null, cannot configure the clubChatController");
            }
        }

        /// <summary>
        /// Resets and configures club-related controllers and UI components to their default states.
        /// </summary>
        /// <returns>A UniTask representing the asynchronous operation.</returns>
        internal async UniTask CleanData()
        {
            // Configure the club member controller with the current club members
            if (clubMemberController)
                await clubMemberController.Configure(default);
            else
                Debug.LogWarning("ClubMemberController is null, cannot configure the ClubMemberController");

            // Configure the club member controller with the current club members
            if (clubDataSettingsController)
                clubDataSettingsController.CleanData();
            else
                Debug.LogWarning("ClubDataSettingsController is null, cannot configure the ClubDataSettingsController");

            // Configure the club home screen data UI with the current club data
            if (clubHomeScreenDataUI)
                clubHomeScreenDataUI.Configure(default, default, default, default);
            else
                Debug.LogWarning("ClubHomeScreenDataUI is null, cannot configure the ClubHomeScreenDataUI");

            // Configure the club applicant controller with the current club applicants
            if (clubApplicantController)
                await clubApplicantController.Configure(default);
            else
                Debug.LogWarning("ClubApplicantController is null, cannot configure the ClubApplicantController");

            // Configure the club applicant controller with the current club applicants
            if (clubChatController)
                await clubChatController.Configure(default);
            else
                Debug.LogWarning("clubChatController is null, cannot configure the clubChatController");
        }

        /// <summary>
        /// Event received when the club selector UI is set active or inactive
        /// </summary>
        /// <param name="toggleID">The ID of the toggle that was selected.</param>
        private void OnSelectHomeScreenToggle(string toggleID)
        {
            if (string.IsNullOrEmpty(toggleID) || !Enum.TryParse<ClubHomeScreenTab>(toggleID, out var ClubHomeScreenTab))
                return;

            UpdateScreen(ClubUIScreen.ClubHomeScreen, ClubHomeScreenTab);
        }

        /// <summary>
        /// Event received when the user clicks the save changes button<br></br>
        /// Invoke a callback to try to update the club data with the selected icon and slogan in firestore using cloud functions
        /// </summary>
        /// <param name="iconData">The selected icon data for the club.</param>
        /// <param name="clubName">The name of the club.</param>
        /// <param name="slogan">The slogan of the club.</param>
        /// <param name="isCreatingClub">Indicates whether a new club is being created.</param>
        private async UniTask OnSaveClubDataSelection(IconData iconData, string clubName, string slogan, bool isCreatingClub)
        {
            try
            {
                if (onTryToUpdateClubData is null)
                    throw new ArgumentException("onTryToUpdateClubData is null, cannot update club data");

                await onTryToUpdateClubData(iconData, clubName, slogan, isCreatingClub);
                if (ClubData is not null and { clubName: not null and not "" })
                    UpdateScreen(ClubUIScreen.ClubHomeScreen, ClubHomeScreenTab.ClubMembers);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error confirming club data selection: {ex.Message}");
                UpdateScreen(ClubUIScreen.ClubDataConfiguration);
            }
        }

        /// <summary>
        /// Event received when the user clicks the confirm rank change button in the ClubRankPrompt
        /// </summary>
        /// <param name="rankType">The type of rank to assign to the member.</param>
        /// <param name="memberData">The data of the member whose rank is being changed.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the rank change was successful.</returns>
        private async UniTask<bool> OnTryToOverrideRank(ClubRanksTypes rankType, MemberData memberData)
        {
            // Validate the member data
            if (memberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null. Cannot override rank.", this);
                return false;
            }

            // Validate the callback
            if (tryToOverrideRank is null)
            {
                Debug.LogError("openClubRankPrompt action is not assigned.", this);
                return false;
            }

            // Disable the root canvas group while processing
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var rankOverrided = false;

            // Try to override the rank calling the event previously assigned
            try
            {
                rankOverrided = await tryToOverrideRank(rankType, memberData);
                if (rankOverrided)
                    Debug.Log($"Rank overrided to {rankType} for member {memberData.memberName}.");
                else
                    Debug.LogWarning("Rank could not be overrided.");
            }

            // If there's an error, log it
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to override the rank: {ex.Message}", this);
            }

            // In any case, re-enable the root canvas group and refresh the data
            finally
            {
                // Re-enable the root canvas group after processing
                RootCanvasGroup?.SetActive(true);

                // Refresh the data to reflect any changes
                RefreshData();
            }

            return rankOverrided;
        }
        
        /// <summary>
        /// Event received when the user clicks the confirm rank change button in the ClubRankPrompt
        /// </summary>
        /// <param name="memberData">The data of the member to be removed.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the member was successfully removed.</returns>
        private async UniTask<bool> OnTryToRemoveMember(MemberData memberData)
        {
            // Validate the member data
            if (memberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null. Cannot remove member.", this);
                return false;
            }

            // Validate the callback
            if (tryToRemoveMember is null)
            {
                Debug.LogError("tryToRemoveMember action is not assigned.", this);
                return false;
            }

            // Disable the root canvas group while processing
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var memberRemoved = false;

            // Try to override the rank calling the event previously assigned
            try
            {
                memberRemoved = await tryToRemoveMember(memberData);
                if (memberRemoved)
                    Debug.Log($"Member {memberData.memberName} removed from the club.");
                else
                    Debug.LogWarning("Member could not be removed.");
            }

            // If there's an error, log it
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to remove the member: {ex.Message}", this);
            }

            // In any case, re-enable the root canvas group and refresh the data
            finally
            {
                // Re-enable the root canvas group after processing
                RootCanvasGroup?.SetActive(true);

                // Refresh the data to reflect any changes
                RefreshData();
            }

            return memberRemoved;
        }

        /// <summary>
        /// Event received when the user clicks the send joining request button in a ClubSearchEntry
        /// </summary>
        /// <param name="clubToSendRequest">The data of the club to which the joining request is being sent.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the joining request was successfully sent.</returns>
        private async UniTask<bool> OnTryToSendJoiningRequest(FirestoreClubData clubToSendRequest)
        {
            // Validate the club data
            if (clubToSendRequest is null or { normalizedName: null or "" })
            {
                Debug.LogError("ClubData is null. Cannot send joining request.", this);
                return false;
            }

            // Validate the callback
            if (tryToSendJoinRequest is null)
            {
                Debug.LogError("tryToSendJoinRequest action is not assigned.", this);
                return false;
            }

            // Disable the root canvas group while processing
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var sendJoinRequest = false;

            // Try to send the joining request calling the event previously assigned
            try
            {
                sendJoinRequest = await tryToSendJoinRequest(clubToSendRequest);
                if (sendJoinRequest)
                    Debug.Log($"Joining request sent to club {clubToSendRequest.clubName}.");
                else
                    Debug.LogWarning("Joining request could not be sent.");
            }

            // If there's an error, log it
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to send the joining request: {ex.Message}", this);
            }

            // In any case, re-enable the root canvas group and refresh the data
            finally
            {
                // Re-enable the root canvas group after processing
                RootCanvasGroup?.SetActive(true);

                // Refresh the data to reflect any changes
                RefreshData();
            }

            return sendJoinRequest;
        }

        /// <summary>
        /// Event received when the user clicks the accept joining request button in a ClubApplicantEntry
        /// </summary>
        /// <param name="applicantData">The data of the applicant whose joining request is being accepted.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the joining request was successfully accepted.</returns>
        private async UniTask<bool> OnTryToAcceptJoiningRequest(ApplicantData applicantData)
        {
            // Validate the member data
            if (applicantData is null or { unityID: null or "" })
            {
                Debug.LogError("ApplicantData is null. Cannot accept joining request.", this);
                return false;
            }

            // Validate the callback
            if (tryToAcceptJoiningRequest is null)
            {
                Debug.LogError("tryToAcceptJoiningRequest action is not assigned.", this);
                return false;
            }

            // Disable the root canvas group while processing
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var applicantAccepted = false;

            // Try to override the rank calling the event previously assigned
            try
            {
                applicantAccepted = await tryToAcceptJoiningRequest(applicantData);
                if (applicantAccepted)
                    Debug.Log($"Applicant {applicantData.applicantName} added to the club.");
                else
                    Debug.LogWarning("Applicant could not be accepted.");
            }

            // If there's an error, log it
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to accept the applicant: {ex.Message}", this);
            }

            // In any case, re-enable the root canvas group and refresh the data
            finally
            {
                // Re-enable the root canvas group after processing
                RootCanvasGroup?.SetActive(true);

                // Refresh the data to reflect any changes
                RefreshData();
            }

            return applicantAccepted;
        }

        /// <summary>
        /// Event received when the user clicks the decline joining request button in a ClubApplicantEntry
        /// </summary>
        /// <param name="applicantData">The data of the applicant whose joining request is being declined.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the joining request was successfully declined.</returns>
        private async UniTask<bool> OnTryToDeclineJoiningRequest(ApplicantData applicantData)
        {
            // Validate the member data
            if (applicantData is null or { unityID: null or "" })
            {
                Debug.LogError("ApplicantData is null. Cannot decline joining request.", this);
                return false;
            }

            // Validate the callback
            if (tryToDeclineJoiningRequest is null)
            {
                Debug.LogError("tryToAcceptJoiningRequest action is not assigned.", this);
                return false;
            }

            // Disable the root canvas group while processing
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var applicantAccepted = false;

            // Try to override the rank calling the event previously assigned
            try
            {
                applicantAccepted = await tryToDeclineJoiningRequest(applicantData);
                if (applicantAccepted)
                    Debug.Log($"Applicant {applicantData.applicantName} declined to be a member of the club.");
                else
                    Debug.LogWarning("Applicant could not be accepted due and error.");
            }

            // If there's an error, log it
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to decline the applicant: {ex.Message}", this);
            }

            // In any case, re-enable the root canvas group and refresh the data
            finally
            {
                // Re-enable the root canvas group after processing
                RootCanvasGroup?.SetActive(true);

                // Refresh the data to reflect any changes
                RefreshData();
            }

            return applicantAccepted;
        }

        /// <summary>
        /// Event received when the user tries to search clubs by name
        /// </summary>
        /// <param name="input">The input string to search clubs by name.</param>
        /// <returns>A UniTask representing the asynchronous operation, with an array of FirestoreClubData representing the clubs found.</returns>
        private async UniTask<FirestoreClubData[]> OnTryToSearchClubsByName(string input)
        {
            // Validate the user is not already in a club
            if (IsUserInClub)
            {
                Debug.LogError("User is already in a club. Cannot search for clubs.", this);
                return null;
            }

            // Validate the input
            if (string.IsNullOrEmpty(input))
            { 
                Debug.LogError("Input string is null or empty. Cannot search clubs by name.", this);
                return null;
            }

            // Validate the callback
            if (tryToSearchClubsByName is null)
            {
                Debug.LogError("tryToSearchClubsByName action is not assigned.", this);
                return null;
            }

            var clubsFound = default(FirestoreClubData[]);
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                clubsFound = await tryToSearchClubsByName(input);
                if (clubsFound is not null and { Length: > 0 })
                    Debug.Log($"Found {clubsFound.Length} clubs matching the search criteria.", this);
                else
                    Debug.LogWarning("No clubs found matching the search criteria.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while searching for clubs: {ex.Message}", this);
            }
            finally
            {
                RootCanvasGroup?.SetActive(true);
            }

            return clubsFound;
        }
        
        /// <summary>
        /// Event received when the user tries to send a chat message to the club
        /// </summary>
        /// <param name="input">The input string representing the chat message.</param>
        /// <returns>A UniTask representing the asynchronous operation, with a boolean result indicating whether the message was successfully sent.</returns>
        private async UniTask<bool> OnTryToSendChatMessage(string input)
        {
            // Validate the user is not in the club
            if (!IsUserInClub)
            {
                Debug.LogError("User is already in not in the club. Cannot send chat message.", this);
                return false;
            }

            // Validate the input
            if (string.IsNullOrEmpty(input))
            { 
                Debug.LogError("Input string is null or empty. Cannot search clubs by name.", this);
                return false;
            }

            // Validate the callback
            if (tryToSearchClubsByName is null)
            {
                Debug.LogError("tryToSearchClubsByName action is not assigned.", this);
                return false;
            }

            var wasSentProperly = false;
            RootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                wasSentProperly = await tryToSendClubChatMessage(input);
                if (wasSentProperly)
                    Debug.Log("The message was successfully sent");
                else
                    Debug.LogWarning($"The message couldn't been send", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while searching for clubs: {ex.Message}", this);
            }
            finally
            {
                RootCanvasGroup?.SetActive(true);
            }

            return wasSentProperly;
        }

        /// <summary>
        /// Represents the different UI screens available in the club management system.
        /// </summary>
        internal enum ClubUIScreen
        {
            None = 0,

            ClubSearch = 1,
            ClubDataConfiguration = 2,
            ClubIconCreation = 3,
            ClubHomeScreen = 4,
        }
        
        /// <summary>
        /// Represents the available tabs on a club home screen, such as members, ranks, chat, invites, and
        /// applications.
        /// </summary>
        internal enum ClubHomeScreenTab
        {
            None = 0,

            ClubMembers = 1,
            ClubRanks = 2,
            ClubChat = 3,
            ClubInvitePlayers = 4,
            ClubApplications = 5,
        }
    }
}
