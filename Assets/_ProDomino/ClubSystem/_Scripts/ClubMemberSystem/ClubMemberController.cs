using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timba.Patterns;
using Timba.Utils;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.FirestoreClubData;
using static TooltipController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages club member entries, permissions, and UI interactions for club-related actions such as editing club
    /// data, overriding member ranks, and removing members.
    /// </summary>
    internal class ClubMemberController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Transform membersEntryParent;
        [SerializeField] private ClubMemberEntry memberEntryPrefab;
        [SerializeField] private Button modifyClubDataButton;
        [SerializeField] private TooltipModel clubRankTooltipModel;
        [SerializeField] private ClubRemoveMemberPopUp clubRemovePopUp;

        private bool hasEditLogoPermission;
        private bool hasEditSloganPermission;

        private GameManager gameManager;
        private AuthManager authManager;
        private TooltipController tooltipController;
        private DictionaryService dictionaryService;
        private List<ClubMemberEntry> clubMemberEntries;

        private ClubRankPrompt clubRankPrompt;
        private Action openClubDataSettingsScreen;
        private Func<bool> checkIfIsUserInClub;
        private Func<FirestoreClubData> getClubData;
        private Func<ConfigData> getConfigData;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;
        private AsyncFuncHandler<bool, ClubRanksTypes, FirestoreClubData.MemberData> tryToOverrideRank;
        private AsyncFuncHandler<bool, FirestoreClubData.MemberData> tryToRemoveMember;


        internal bool IsUserInClub => checkIfIsUserInClub?.Invoke() ?? false;
        internal FirestoreClubData ClubData => getClubData?.Invoke();

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            tooltipController = ServiceLocator.Instance.GetService<TooltipController>();

            clubMemberEntries = membersEntryParent.GetComponentsInChildren<ClubMemberEntry>(true)?.ToList() ?? new();
            if (clubMemberEntries is not null and { Count: > 0 })
                foreach (var entry in clubMemberEntries)
                    entry.Initialize
                        (authManager,
                        dictionaryService,
                        () => getConfigData?.Invoke(),
                        () => getCurrentPlayerMemberData?.Invoke(),
                        OnOpenClubRankPrompt, 
                        OnOpenRemoveMemberPopUp);

            if (modifyClubDataButton)
                modifyClubDataButton.onClick.AddListener(TryToOpenClubDataSettingsScreen);
            else
                Debug.LogError("Edit Icon Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Initializes the ClubMemberController with the given actions and functions.
        /// </summary>
        /// <param name="checkIfIsUserInClub">Function to check if the user is in a club.</param>
        /// <param name="openClubDataSettingsScreen">Action to open the club data settings screen.</param>
        /// <param name="getClubData">Function to get the current club data.</param>
        /// <param name="getConfigData">Function to get the configuration data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to get the current player's member data.</param>
        /// <param name="tryToOverrideRank">Function to attempt to override a member's rank.</param>
        /// <param name="tryToRemoveMember">Function to attempt to remove a member.</param>
        internal void Initialize
            (Func<bool> checkIfIsUserInClub, 
            Action openClubDataSettingsScreen,
            Func<FirestoreClubData> getClubData,
            Func<ConfigData> getConfigData,
            Func<MemberData> getCurrentPlayerMemberData,
            AsyncFuncHandler<bool, ClubRanksTypes, MemberData> tryToOverrideRank,
            AsyncFuncHandler<bool, MemberData> tryToRemoveMember)
        {
            this.checkIfIsUserInClub = checkIfIsUserInClub ?? throw new ArgumentNullException(nameof(checkIfIsUserInClub));
            this.openClubDataSettingsScreen = openClubDataSettingsScreen ?? throw new ArgumentNullException(nameof(openClubDataSettingsScreen));
            this.getClubData = getClubData ?? throw new ArgumentNullException(nameof(getClubData));
            this.getConfigData = getConfigData ?? throw new ArgumentNullException(nameof(getConfigData));
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.tryToOverrideRank = tryToOverrideRank ?? throw new ArgumentNullException(nameof(tryToOverrideRank));
            this.tryToRemoveMember = tryToRemoveMember ?? throw new ArgumentNullException(nameof(tryToRemoveMember));

            clubRankPrompt = tooltipController.GetTootipReference(Consts.CollectionKeys.ClubRankPromptID)?.GetComponent<ClubRankPrompt>();
            if (clubRankPrompt)
                clubRankPrompt.Initialize(dictionaryService, ref this.getCurrentPlayerMemberData, TryToOverrideRank);
            else
                Debug.LogWarning($"ClubRankPromptID with ID {Consts.CollectionKeys.ClubRankPromptID} not found. Please ensure it is set up in the TooltipController.");

            if (clubRemovePopUp)
                clubRemovePopUp.Initialize(ref this.getCurrentPlayerMemberData, TryToRemoveMember);
            else
                Debug.LogWarning("ClubRemovePopUp reference is missing. Ensure it is assigned in the inspector.", this);
        }

        /// <summary>
        /// Configures the club members list with the provided member data.
        /// </summary>
        /// <param name="memberDatas">An array of member data to configure the club members list.</param>
        internal async Task Configure(MemberData[] memberDatas)
        {
            // Check for necessary references
            if (!memberEntryPrefab || !membersEntryParent)
            {
                Debug.LogError("Member Entry Prefab or Members Entry Parent is not assigned in the inspector.", this);
                return;
            }

            // Get the current number of entries and calculate the difference to add new ones if needed
            var difference = (memberDatas?.Length ?? 0) - clubMemberEntries.Count;
            if (difference > 0)
                for (int i = 0; i < difference; i++)
                {
                    var newEntry = Instantiate(memberEntryPrefab, membersEntryParent);
                    newEntry.Initialize
                        (authManager,
                        dictionaryService,
                        () => getConfigData?.Invoke(),
                        () => getCurrentPlayerMemberData?.Invoke(),
                        OnOpenClubRankPrompt, 
                        OnOpenRemoveMemberPopUp);
                    clubMemberEntries.Add(newEntry);
                }

            // Configure each entry with the corresponding member data or deactivate if no data
            if (clubMemberEntries is not null and { Count: > 0 })
            {
                // By default, set all entries to inactive. They will be activated again if there is corresponding data for them.
                // This ensures that if the new member data array has fewer members than before, the extra entries will be hidden.
                clubMemberEntries.ForEach(entry => entry.gameObject.SetActive(false));

                // Cache profile icons for all members before configuring entries to improve performance and avoid multiple loads of the same icon
                if (memberDatas is not null)
                    await TryToCachePlayersProfileIcons(memberDatas);

                // Iterate through the entries and configure them with the corresponding member data. If there is no corresponding data for an entry, it will remain inactive.
                for (int i = 0; i < memberDatas.Length; i++)
                {
                    var dataExists = i < (memberDatas?.Length ?? 0);
                    var memberData = memberDatas?.ElementAtOrDefault(i);
                    var profileSprite = gameManager.GetSprite(memberData?.profileIconId, Consts.CollectionKeys.Icons);

                    clubMemberEntries[i].gameObject.SetActive(dataExists);
                    clubMemberEntries[i].Configure(memberData, profileSprite, i);
                }

                // Sort entries by rank and then by member name
                clubMemberEntries = clubMemberEntries
                    .Where(entry => entry.gameObject.activeSelf)
                    .OrderByDescending(entry => entry.MemberData?.rank ?? ClubRanksTypes.Member)
                    .ThenBy(entry => entry.MemberData?.memberName ?? string.Empty)
                    .ToList();

                // Reparent entries to reflect the new order in the hierarchy
                for (int i = 0; i < clubMemberEntries.Count; i++)
                    clubMemberEntries[i].transform.SetSiblingIndex(i);
            }

            // Update the modify club data button visibility based on user permissions
            if (modifyClubDataButton)
            {
                // First, update the permissions
                UpdatePermissionsToEdit();

                // Then, set the button active state based on whether the user is in a club and has any edit permissions
                modifyClubDataButton.gameObject.SetActive(IsUserInClub && (hasEditLogoPermission || hasEditSloganPermission));
            }
        }

        /// <summary>
        /// Updates the permissions for the current user to edit club data based on their rank.
        /// </summary>
        private void UpdatePermissionsToEdit()
        {
            // By default, assume no permissions
            hasEditLogoPermission = false;
            hasEditSloganPermission = false;

            // Get the current player's member data
            var currentMemberData = getCurrentPlayerMemberData?.Invoke();
            if (currentMemberData is null)
            {
                Debug.LogWarning("Current player member data is null. Cannot open club data settings screen.", this);
                return;
            }

            // Check if the event that provides config data is assigned
            if (getConfigData is null)
            {
                Debug.LogError("getConfigData function is not assigned.", this);
                return;
            }

            // Get the configuration data
            var configData = getConfigData();
            if (configData is null or { clubsConfig: null or { clubPermissionDatas: null or { Length: 0 } } })
            {
                Debug.LogError("Configuration data or club permissions data is missing. Cannot determine if the user has permission to change club data.", this);
                return;
            }

            // Get the permissions data for the current member's rank. If not found, log an error and return.
            var permissionsData = configData.clubsConfig.clubPermissionDatas.FirstOrDefault(x => x.clubRanksType.HasFlag(currentMemberData.rank));
            if (permissionsData is null)
            {
                Debug.LogError($"No permissions data found for the rank: {currentMemberData.rank}. Cannot determine if the user has permission to change club data.", this);
                return;
            }

            // Set permissions based on the retrieved data
            hasEditLogoPermission = permissionsData.clubPermissionsType.HasFlag(ClubPermissionsTypes.ChangeLogo);
            hasEditSloganPermission = permissionsData.clubPermissionsType.HasFlag(ClubPermissionsTypes.ChangeSlogan);
        }

        /// <summary>
        /// Calls the action to attempt to override the rank of a club member.
        /// </summary>
        /// <param name="newRank">The new rank to assign to the member.</param>
        /// <param name="memberData">The member data of the member whose rank is to be overridden.</param>
        private async UniTask TryToOverrideRank(ClubRanksTypes newRank, MemberData memberData)
        {
            if (memberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null. Cannot override rank.", this);
                return;
            }

            if (tryToOverrideRank is null)
            {
                Debug.LogError("openClubRankPrompt action is not assigned.", this);
                return;
            }

            // Block UI interactions while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                var wasOverridedSuccessfully = await tryToOverrideRank(newRank, memberData);
                if (wasOverridedSuccessfully)
                    Debug.Log("Rank override successfully made", this);
                else
                    Debug.LogWarning("Rank override was not successfully made.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error overriding rank: {ex.Message}", this);
            }
            finally
            {
                // Once done, re-enable UI interactions
                rootCanvasGroup.SetActive(true);
            }
        }

        /// <summary>
        /// Calls the action to attempt to remove a club member.
        /// </summary>
        /// <param name="memberData">The member data of the member to be removed.</param>
        private async UniTask TryToRemoveMember(MemberData memberData)
        {
            if (memberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null. Cannot override rank.", this);
                return;
            }

            if (tryToRemoveMember is null)
            {
                Debug.LogError("tryToRemoveMember action is not assigned.", this);
                return;
            }

            // Block UI interactions while processing
            rootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var wasRemovedSuccessfully = false;
            try
            {
                // Attempt to remove the member
                wasRemovedSuccessfully = await tryToRemoveMember(memberData);
                if (wasRemovedSuccessfully)
                {
                    // Reconfigure the list to reflect the removal
                    var currentMemberData = ClubData?.members;
                    await Configure(currentMemberData?.ToArray());

                    // If the user removed themselves, do not re-enable the UI because the entire screen was changed
                    if (IsUserInClub)
                        rootCanvasGroup?.SetActive(true);
                    else
                        Debug.Log("User has left the club. UI will not be re-enabled as the screen has changed.", this);
                }
                else
                    Debug.LogWarning("Member removal was not successful.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error removing member: {ex.Message}", this);
                
                // Once done, re-enable UI interactions
                rootCanvasGroup?.SetActive(true);
            }
            finally
            {
                // If the removal was not successful and we are still in the club, re-enable the UI
                if (!wasRemovedSuccessfully)
                    rootCanvasGroup?.SetActive(true);
            }
        }

        /// <summary>
        /// Opens the Club Data Settings Screen
        /// </summary>
        private void TryToOpenClubDataSettingsScreen()
        {
            // Check the event action
            if (openClubDataSettingsScreen is null)
            {
                Debug.LogError("OpenClubDataSettingsScreen action is not assigned.", this);
                return;
            }

            // Refresh permissions before attempting to open the settings screen
            UpdatePermissionsToEdit();

            // Check if the user has permission to edit club data
            if (!hasEditLogoPermission || !hasEditSloganPermission)
            {
                Debug.LogWarning("User does not have permission to edit club data.", this);
                return;
            }

            rootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                // Call the action to open the club data settings screen. In this screen the canvas group will be re-enabled when transitioning
                openClubDataSettingsScreen();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error opening club data settings screen: {ex.Message}", this);
                rootCanvasGroup?.SetActive(true);
            }
        }

        /// <summary>
        /// Asynchronously caches profile icon sprites for players in the provided leaderboard data.
        /// </summary>
        /// <param name="memberDatas">An array of member data containing profile icon information.</param>
        /// <returns>A UniTask representing the asynchronous caching operation.</returns>
        private async UniTask TryToCachePlayersProfileIcons(MemberData[] memberDatas)
        {
            if (memberDatas is null or { Length: 0 })
            {
                Debug.LogWarning("No member data provided for caching icons.");
                return;
            }

            var cacheIconTasks = memberDatas
                .Where(data => data is not null && !string.IsNullOrEmpty(data.profileIconId))
                .Select(data => gameManager.GetSpriteAsync(data.profileIconId, Consts.CollectionKeys.Icons))
                .ToArray();

            await UniTask.WhenAll(cacheIconTasks);
        }

        /// <summary>
        /// Handles the opening of the club rank prompt for a specific club member entry.
        /// </summary>
        /// <param name="clubMemberEntry">The club member entry for which to open the rank prompt.</param>
        private void OnOpenClubRankPrompt(ClubMemberEntry clubMemberEntry)
        {
            if (clubMemberEntry is null || clubMemberEntry.MemberData is null)
            {
                Debug.LogError("ClubMemberEntry or its MemberData is null. Cannot open rank prompt.", this);
                return;
            }

            if (!clubRankPrompt)
            {
                Debug.LogError("ClubRankPrompt reference is missing. Ensure it is set up in the TooltipController.", this);
                return;
            }

            // Configure and show the rank prompt for the selected member
            clubRankPrompt.Configure(clubMemberEntry);

            // Show the tooltip only if there are available ranks to display
            if (clubRankPrompt.AvailableRanksCount > 0)
                tooltipController.ShowAtUI(clubMemberEntry.TargetGraphic?.rectTransform, clubRankTooltipModel);
            else
                Debug.LogWarning("No available ranks to display in the ClubRankPrompt.", this);
        }

        /// <summary>
        /// Handles the removal of a club member.
        /// </summary>
        /// <param name="clubMemberEntry">The club member entry for which to open the removal pop-up.</param>
        private void OnOpenRemoveMemberPopUp(ClubMemberEntry clubMemberEntry)
        {
            if (clubMemberEntry is null || clubMemberEntry.MemberData is null || string.IsNullOrEmpty(clubMemberEntry.MemberData.unityMemberId))
            {
                Debug.LogError("ClubMemberEntry or its MemberData is null. Cannot remove member.", this);
                return;
            }

            if (!clubRemovePopUp)
            {
                Debug.LogError("ClubRemovePopUp reference is missing. Ensure it is assigned in the inspector.", this);
                return;
            }

            clubRemovePopUp.Configure(clubMemberEntry);
            clubRemovePopUp.Show();
        }
    }
}
