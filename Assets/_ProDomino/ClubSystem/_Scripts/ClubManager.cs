using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Leaderboard;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using UnityEngine;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.FirestoreClubData;

namespace ProDomino.ClubSystem
{ 
    /// <summary>
    /// Manages club-related operations including creation, membership, applicant handling, rank management, club chat,
    /// and UI integration within the game.
    /// </summary>
    public class ClubManager : MonoBehaviour
    {
        [SerializeField] private string testUserUnityID = "user_to_add_or_remove";

        private GameManager gameManager;
        private AuthManager authManager;
        private PlayerBestRankController playerBestRankController;

        private BackendBindings _module;
        internal BackendBindings module => _module ??= CloudCodeService.Instance is not null ? new(CloudCodeService.Instance) : null;

        private ClubUI _clubUI;
        internal ClubUI ClubUI
        {
            get
            {
                if (_clubUI == null)
                {
                    _clubUI = FindFirstObjectByType<ClubUI>();
                    if (_clubUI == null)
                        Debug.LogWarning($"{nameof(ClubSystem.ClubUI)} not found in the scene");
                }

                return _clubUI;
            }
        }

        /// <summary>
        /// Gets or sets the FirestoreClubData from the GameManager.
        /// </summary>
        internal FirestoreClubData FirestoreClubData 
        {
            get => gameManager?.PlayerClubData;
            set => gameManager?.OverrideFirestorePlayerClubData(value);
        }

        /// <summary>
        /// Determine if the club data contains the player as a member
        /// </summary>
        internal bool IsClubMember => FirestoreClubData?.members?.Any(x => x.unityMemberId == authManager.UUID) ?? false;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
        }

        private async void Start()
        {
            // We need the PlayerBestRankController to send join requests using the best leaderboard ID
            playerBestRankController = playerBestRankController != null 
                ? playerBestRankController
                : FindFirstObjectByType<PlayerBestRankController>();

            // Wait until the AuthManager is initialized
            await UniTask.WaitUntil(() => 
                gameManager is not null and { IsAlreadyInitialized: true } 
                && authManager is not null and { IsAlreadyInitialized: true });

            // Initialize the Club UI
            ClubUI?.Initialize
                (getConfigData: () => gameManager.GameBackendConfigData,
                getPlayerMemberData: () => gameManager.PlayerClubData?.members?.FirstOrDefault(member => member.unityMemberId == authManager.UUID),
                getClubData : () => gameManager.PlayerClubData,
                getClubChatData : () => gameManager.PlayerClubChatData,
                onSetActiveClubUI: OnSetActiveClubUI,

                // Callbacks to manage club data
                TryToUpdateClubData,

                // ClubMemberController related actions
                TryToOverrideMemberRank,
                TryToRemoveMemberFromClub,

                // ClubSearchController related actions
                TryToSearchClubsByName,
                TryToGetLeaderboardEntries,
                TryToSendJoinRequest,

                // ClubApplicantController related actions
                TryToAcceptClubJoiningRequest,
                TryToDeclineClubJoiningRequest,

                // ClubChatController related actions
                TryToSendChatMessage,
                TryToSubscribeToClubChat,
                TryToUnsubscribeFromClubChat,
                ref gameManager.onGetClubChatData);

            // Simply function to refresh the club data when the UI is set active
            async UniTask OnSetActiveClubUI(bool isActive)
            {
                if (isActive)
                    await UniTask.WhenAll(RefreshData(), RefreshFirestoreClubChatData());
            }
        }

        /// <summary>
        /// Refreshes the club data by fetching the latest information from Firestore.
        /// </summary>
        private async UniTask RefreshData()
        { 
            if (!gameManager)
            {
                Debug.LogWarning("GameManager is null, cannot refresh club data.");
                return;
            }

            // Fetch the latest club data from Firestore
            await gameManager.RefreshFirestoreClubData();
        }

        /// <summary>
        /// Refreshes the club data by fetching the club chat from Firestore.
        /// </summary>
        private async UniTask RefreshFirestoreClubChatData()
        { 
            if (!gameManager)
            {
                Debug.LogWarning("GameManager is null, cannot refresh club chat data.");
                return;
            }

            // Fetch the latest club data from Firestore
            await gameManager.RefreshFirestoreClubChatData();
        }

        /// <summary>
        /// Attemps to change the club data using the provided new data. If isCreatingClub is true, it will create a new club with the provided data, otherwise it will update the existing club data.
        /// </summary>
        /// <param name="newIconData">The new icon data for the club.</param>
        /// <param name="newName">The new name for the club.</param>
        /// <param name="newSlogan">The new slogan for the club.</param>
        /// <param name="isCreatingClub">Indicates whether a new club is being created.</param>
        private async UniTask TryToUpdateClubData(IconData newIconData = null, string newName = null, string newSlogan = null, bool isCreatingClub = false)
        {
            // Equivalent compact check: invalid when isCreatingClub == IsClubMember
            // Your couldn't update/create if you are:
            // 1. Creating while member
            // 2. Updating while not member
            if (gameManager is null || (isCreatingClub == IsClubMember))
            {
                Debug.LogWarning("You are trying to update club data but GameManager or you are not a club member and couldn't update its data", this);
                return;
            }

            if (newIconData is null && string.IsNullOrEmpty(newName) && string.IsNullOrEmpty(newSlogan))
            {
                Debug.LogWarning("No new data provided to update the club.");
                return;
            }

            // Prepare the data to be sent to the backend
            var newData = new Dictionary<string, object>()
            {
                ["isCreatingClub"] = isCreatingClub
            };

            // Check if the new name has value before assign it
            if (isCreatingClub && !string.IsNullOrEmpty(newName))
                newData["clubName"] = newName;

            // Check if the new icon data has value before assign it
            if (newIconData is not null)
            {
                // Only change the icon data if it's differente to the current one
                if (isCreatingClub || FirestoreClubData.iconData != newIconData)
                    newData["selectedClubIcon"] = newIconData;
                else
                    Debug.Log("The new icon data is the same as the current one, no update needed.");
            }

            // Check if the new slogan has value before assign it
            if (!string.IsNullOrEmpty(newSlogan))
            {
                // Only change the slogan if it's differente to the current one
                if (isCreatingClub || FirestoreClubData.slogan != newSlogan)
                    newData["selectedSlogan"] = newSlogan;
                else
                    Debug.Log("The new slogan is the same as the current one, no update needed.");
            }

            // Optional: if the user logged in using a provider, use its url as icon id
            if (!string.IsNullOrEmpty(authManager.ProviderURL))
                newData["optionalIconID"] = authManager.ProviderURL;

            var bannedNameReference = string.Empty;
            var bannedSloganReference = string.Empty;

            var isNameBanned = !string.IsNullOrEmpty(newName) ? UnityProfanityValidator.ContainsProfanity(newName, out bannedNameReference) : false;
            var isSloganBanned = !string.IsNullOrEmpty(newSlogan) ? UnityProfanityValidator.ContainsProfanity(newSlogan, out bannedSloganReference) : false;
            if (!isNameBanned && !isSloganBanned)
            { 
                // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
                var encryptedJsonData = authManager.SerializeAndEncryptData(newData);

                // Store the variables to use the trycat pattern to store the response
                var tryToUpdateClubDataRequest = default(string);
                var tryToUpdateClubDataResponse = default(FirestoreClubDataResponse);

                try
                {
                    // Use the backedn binding of the corresponding player to update the leaderboard score
                    tryToUpdateClubDataRequest = await gameManager.HandleProcess_GameManagerProxy
                        (uniTask: () => module.TryToUpdateClubData(encryptedJsonData).AsUniTask(),
                        taskId: nameof(module.TryToUpdateClubData),
                        showLoading: true,
                        returnExceptionOnError: true);

                    // Deserialize and decrypt the data
                    tryToUpdateClubDataResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToUpdateClubDataRequest);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception occurred while trying to update club data: {ex.Message}");
                }

                // Check if the response is positive
                if (tryToUpdateClubDataResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
                { 
                    Debug.Log($"Successfully <b>{(isCreatingClub ? "Created" : "Updated ")}</b> club data");

                    // If the club was created, refresh the protected player data to get the new club name
                    if (!string.IsNullOrEmpty(newName))
                        await gameManager.RefreshProtectedPlayerData();

                    // Refresh the club data from Firestore
                    FirestoreClubData = tryToUpdateClubDataResponse.playerClubData;

                    // Check if PlayerClubData is null after refreshing
                    if (FirestoreClubData is null)
                    {
                        Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                        return;
                    }

                    // Show an success prompt in the UI
                    ClubUI.ShowPrompt($"Successfully <b>{(isCreatingClub ? "Created" : "Updated ")}</b> club data");
                } 
                else
                {
                    if (tryToUpdateClubDataResponse is not null)
                        Debug.LogWarning($"Failed to <b>{(isCreatingClub ? "create" : "update")}</b> club data. " +
                            $"\n\n<b>Response code</b>: {tryToUpdateClubDataResponse.clubResponseCodeType}" +
                            $"\n<b>Message</b>: {tryToUpdateClubDataResponse.message}");
                    else
                        Debug.LogWarning($"Failed to update club data");

                    // Show an error prompt in the UI
                    ClubUI.ShowPrompt(tryToUpdateClubDataResponse.message ?? $"Failed to <b>{(isCreatingClub ? "create" : "update")}</b> club data.");
                }
            } 
            else
            {
                Debug.LogWarning($"You are using an invalid name or slogan: Name => {newName}({bannedNameReference})\nSlogan => {newSlogan}({bannedSloganReference})");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt($"Invalid {(isNameBanned ? "Name" : "")}{(isNameBanned && isSloganBanned ? " and " : "")}{(isSloganBanned ? "Slogan" : "")}");
            }

            // Refresh the UI data
            ClubUI.RefreshData();
        }

        #region Not Logged into a Club Methods
        /// <summary>
        /// Attempts to send a join request to the specified club. 
        /// </summary>
        /// <param name="toRequestClubData">The club data to which the join request will be sent.</param>
        /// <returns>Returns true if the join request was successfully sent, otherwise false.</returns>
        private async UniTask<bool> TryToSendJoinRequest(FirestoreClubData toRequestClubData)
        {
            // Check if GameManager is null or if the player is already in a club
            if (gameManager is null || IsClubMember)
            {
                Debug.LogWarning("You are trying to send join request to clubs but you are already in a club.", this);
                return default;
            }

            // Validate the input club data
            if (toRequestClubData is null)
            {
                Debug.LogWarning("Club data to send join request is null.");
                return false;
            }

            // Check if the player has already sent a join request to the club
            if (toRequestClubData.applicants.Any(x => x.unityID == authManager.UUID))
            {
                Debug.LogWarning($"You have already sent a join request to the club {toRequestClubData.clubName}.");
                ClubUI.ShowPrompt($"You have already sent a join request to the club {toRequestClubData.clubName}.");
                return false;
            }

            // Try to get the best leaderboard ID that the player has and use it for the join request
            var bestLeaderboardId = string.Empty;
            if (playerBestRankController)
                bestLeaderboardId = playerBestRankController.PlayerBestRankEntry.Key ?? string.Empty;

            var newData = new Dictionary<string, object>
            {
                ["toRequestClubName"] = toRequestClubData.clubName,
                ["bestLeaderboardId"] = bestLeaderboardId,
            };

            // Optional: if the user logged in using a provider, use its url as icon id
            if (!string.IsNullOrEmpty(authManager.ProviderURL))
                newData["optionalIconID"] = authManager.ProviderURL;

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(newData);

            // Store the variables to use the trycat pattern to store the response
            var tryToSendJoinRequestRqst = default(string);
            var TryToSendJoinRequestResponse = default(FirestoreClubDataResponse);

            try
            {
                // Use the backedn binding of the corresponding player 
                tryToSendJoinRequestRqst = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToSendClubJoiningRequest(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.TryToSendClubJoiningRequest),
                    showLoading: false,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                TryToSendJoinRequestResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToSendJoinRequestRqst);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to send join request: {ex.Message}");
            }

            // Check if the response is positive
            if (TryToSendJoinRequestResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
            {
                Debug.Log($"Successfully sent join request to {toRequestClubData.clubName}");

                // Because we are not into the club, we couldn't save its reference
                var tempfirestorePlayerClubData = TryToSendJoinRequestResponse.playerClubData;

                // Check if PlayerClubData is null after refreshing
                if (tempfirestorePlayerClubData is null)
                {
                    Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                    return false;
                }

                // Search inf the member list for the accepted applicant data
                var acceptedApplicantMemberData = tempfirestorePlayerClubData.applicants.FirstOrDefault(member => member.unityID == authManager.UUID);
                if (acceptedApplicantMemberData is null)
                {
                    Debug.LogWarning($"User {authManager.UUID} was added registered as an applicant into the club but its data is not found in the applicants list.");
                    return false;
                }

                // Show an success prompt in the UI
                ClubUI.ShowPrompt($"Club join request was sent to {toRequestClubData.clubName}");

                return true;
            } 
            else
            {
                if (TryToSendJoinRequestResponse is not null)
                    Debug.LogWarning($"Failed to <b>Send</b> join request to {toRequestClubData.clubName} " +
                        $"\n\n<b>Response code</b>: {TryToSendJoinRequestResponse.clubResponseCodeType}" +
                        $"\n<b>Message</b>: {TryToSendJoinRequestResponse.message}");
                else
                    Debug.LogWarning($"Failed to send join request to {toRequestClubData.clubName}");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt(TryToSendJoinRequestResponse.message ?? $"Failed to send join request to the club {toRequestClubData.clubName}.");
            }

            // Refresh the UI data
            ClubUI.RefreshData();

            return false;
        }

        /// <summary>
        /// Attempts to search clubs by name.
        /// Note: The user must not be a member of any club
        /// </summary>
        /// <param name="input">The name or partial name of the clubs to search for.</param>
        /// <returns>Returns an array of clubs matching the search criteria, or null if no clubs are found.</returns>
        private async UniTask<FirestoreClubData[]> TryToSearchClubsByName(string input)
        {
            // Check if GameManager is null or check if the user is already a member
            if (gameManager is null || IsClubMember)
            {
                Debug.LogWarning("You are trying to search clubs but you are already in a club.", this);
                return default;
            }

            // Validate the search input
            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("Search input is null or empty. Cannot search clubs.");
                return default;
            }

            // Use the game manager to search clubs by name
            var clubsFound = await gameManager.TryToSearchClubsByName(input);

            if (clubsFound is null or { Length: 0 })
            { 
                Debug.LogWarning("No clubs found matching the search criteria.");
                ClubUI.ShowPrompt("No clubs found matching the search criteria.");
            }

            return clubsFound;
        }

        /// <summary>
        /// Attempts to get the club leaderboard entries.
        /// Note: The user must not be a member of any club
        /// </summary>
        /// <returns> Returns an array of club leaderboard entries, or null if no entries are found.</returns>
        private async UniTask<FirestoreClubData[]> TryToGetLeaderboardEntries()
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || IsClubMember)
            {
                Debug.LogWarning("You are trying to search clubs but you are already in a club.", this);
                return default;
            }

            // Use the game manager to search clubs by name
            var leaderboard = await gameManager.TryToGetClubLeaderboard();
            if (leaderboard is null or { Length: 0 })
            {
                Debug.LogWarning("No club leaderboard found");
                ClubUI.ShowPrompt("No club leaderboard found.");
            }

            return leaderboard;
        }
        #endregion

        #region Already Logged into a Club methods
        /// <summary>
        /// Attempts to add a applicant to the club using their Unity ID.
        /// </summary>
        /// <param name="toAddApplicantData">The applicant data of the user to be added to the club.</param>
        /// <returns>Returns true if the applicant was successfully added, otherwise false.</returns>
        private async UniTask<bool> TryToAcceptClubJoiningRequest(ApplicantData toAddApplicantData)
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to accept a new applicant but the references are null or you aren't a member of any club", this);
                return false;
            }

            var toAddApplicantId = toAddApplicantData?.unityID;

            // Validate the input ApplicantData
            if (toAddApplicantData is null || string.IsNullOrEmpty(toAddApplicantId))
            {
                Debug.LogError("New applicant id is null or its unityApplicantId is null or empty. Cannot accept applicant joining request.");
                return false;
            }

            // Get the game rank permissions from the config data
            var globalPermissions = gameManager.GameBackendConfigData.clubsConfig.clubPermissionDatas;

            // Get my rank in the club
            var myRank = FirestoreClubData.members.FirstOrDefault(member => member.unityMemberId == authManager.UUID)?.rank;

            // Get the permissions for my rank and check if I have permission to accept applicants
            var myRankPermission = globalPermissions?.FirstOrDefault(perm => perm.clubRanksType == myRank);
            if ((!myRankPermission?.clubPermissionsType.HasFlag(ClubPermissionsTypes.AcceptApplicant)) ?? true)
            {
                Debug.LogWarning($"You don't have permission to accept new applicants. Your rank: {myRank}");
                return false;
            }

            var newData = new Dictionary<string, object>
            {
                ["toAddUserUnityID"] = toAddApplicantId,
            };

            // Optional: if the user logged in using a provider, use its url as icon id
            if (!string.IsNullOrEmpty(toAddApplicantData.profileIconId))
                newData["optionalIconID"] = toAddApplicantData.profileIconId;

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(newData);

            // Store the variables to use the trycat pattern to store the response
            var tryToAcceptClubJoiningRequest = default(string);
            var tryToAcceptClubJoiningResponse = default(FirestoreClubDataResponse);

            try
            {
                // Use the backedn binding of the corresponding player to update the leaderboard score
                tryToAcceptClubJoiningRequest = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToAcceptClubJoiningRequest(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.TryToAcceptClubJoiningRequest),
                    showLoading: false,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                tryToAcceptClubJoiningResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToAcceptClubJoiningRequest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to accept applicant: {ex.Message}");
            }

            // Check if the response is positive
            if (tryToAcceptClubJoiningResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
            { 
                Debug.Log($"Successfully added user {toAddApplicantId} to the club.");

                // Refresh the club data from Firestore
                FirestoreClubData = tryToAcceptClubJoiningResponse.playerClubData;

                // Check if PlayerClubData is null after refreshing
                if (FirestoreClubData is null)
                {
                    Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                    return false;
                }

                // Search inf the member list for the accepted applicant data
                var acceptedApplicantMemberData = FirestoreClubData.members.FirstOrDefault(member => member.unityMemberId == toAddApplicantId);
                if (acceptedApplicantMemberData is null)
                {
                    Debug.LogWarning($"User {toAddApplicantId} was added to the club but its data is not found in the applicants list.");
                    return false;
                }

                // Show an success prompt in the UI
                ClubUI.ShowPrompt($"{acceptedApplicantMemberData.memberName} was accepted in the club.");

                return true;
            }
            else
            {
                if (tryToAcceptClubJoiningResponse is not null)
                    Debug.LogWarning($"Failed to <b>Add</b> player with id {toAddApplicantId} to club. " +
                        $"\n\n<b>Response code</b>: {tryToAcceptClubJoiningResponse.clubResponseCodeType}" +
                        $"\n<b>Message</b>: {tryToAcceptClubJoiningResponse.message}");
                else
                    Debug.LogWarning($"Failed to add user {toAddApplicantId} to the club.");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt(tryToAcceptClubJoiningResponse.message ?? $"Failed to add user {toAddApplicantId} to the club.");
            }

            // Refresh the UI data
            ClubUI.RefreshData();

            return false;
        }

        /// <summary>
        /// Attempts to decline an applicant's request to join the club using their Unity ID.
        /// </summary>
        /// <param name="toDeclineApplicantData">The applicant data of the user to be declined from the club.</param>
        /// <returns>Returns true if the applicant was successfully declined, otherwise false.</returns>
        private async UniTask<bool> TryToDeclineClubJoiningRequest(ApplicantData toDeclineApplicantData)
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to decline an applicant joining request but the references are null or you aren't a member of any club", this);
                return false;
            }

            var toDeclineApplicantId = toDeclineApplicantData?.unityID;

            // Validate the input ApplicantData
            if (string.IsNullOrEmpty(toDeclineApplicantId))
            {
                Debug.LogError("New applicant id is null or its unityApplicantId is null or empty. Cannot decline applicant joining request.");
                return false;
            }

            // Get the game rank permissions from the config data
            var globalPermissions = gameManager.GameBackendConfigData.clubsConfig.clubPermissionDatas;

            // Get my rank in the club
            var myRank = FirestoreClubData.members.FirstOrDefault(member => member.unityMemberId == authManager.UUID)?.rank;

            // Get the permissions for my rank and check if I have permission to remove applicants
            var myRankPermission = globalPermissions?.FirstOrDefault(perm => perm.clubRanksType == myRank);
            if ((!myRankPermission?.clubPermissionsType.HasFlag(ClubPermissionsTypes.RemoveMember)) ?? true)
            {
                Debug.LogWarning($"You don't have permission to decline new applicants. Your rank: {myRank}");
                return false;
            }

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(new Dictionary<string, object>
            {
                ["toDeclineUserUnityID"] = toDeclineApplicantId,
            });

            // Store the variables to use the trycat pattern to store the response
            var tryToDeclineClubJoiningRequest = default(string);
            var tryToDeclineClubJoiningResponse = default(FirestoreClubDataResponse);

            try
            {
                // Use the backedn binding of the corresponding player to update the leaderboard score
                tryToDeclineClubJoiningRequest = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToDeclineClubJoiningRequest(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.TryToDeclineClubJoiningRequest),
                    showLoading: false,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                tryToDeclineClubJoiningResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToDeclineClubJoiningRequest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to decline applicant: {ex.Message}");
            }

            // Check if the response is positive
            if (tryToDeclineClubJoiningResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
            { 
                Debug.Log($"Successfully declined user {toDeclineApplicantId} to joining to the club.");

                // Refresh the club data from Firestore
                FirestoreClubData = tryToDeclineClubJoiningResponse.playerClubData;

                // Check if PlayerClubData is null after refreshing
                if (FirestoreClubData is null)
                {
                    Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                    return false;
                }

                // Search inf the member list for the accepted applicant data
                var declinedApplicantApplicantData = FirestoreClubData.applicants.FirstOrDefault(member => member.unityID == toDeclineApplicantId);
                if (declinedApplicantApplicantData is not null)
                {
                    Debug.LogWarning($"User {toDeclineApplicantId} was declined to be a member of the club but its data still remains in the applicants list.");
                    return false;
                }

                // Show an success prompt in the UI
                ClubUI.ShowPrompt($"{toDeclineApplicantData.applicantName} was declined to join the club.");

                return true;
            }
            else
            {
                if (tryToDeclineClubJoiningResponse is not null)
                    Debug.LogWarning($"Failed to <b>Add</b> player with id {toDeclineApplicantId} to club. " +
                        $"\n\n<b>Response code</b>: {tryToDeclineClubJoiningResponse.clubResponseCodeType}" +
                        $"\n<b>Message</b>: {tryToDeclineClubJoiningResponse.message}");
                else
                    Debug.LogWarning($"Failed to add user {toDeclineApplicantId} to the club.");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt(tryToDeclineClubJoiningResponse.message ?? $"Failed to add user {toDeclineApplicantId} to the club.");
            }

            // Refresh the UI data
            ClubUI.RefreshData();

            return false;
        }

        /// <summary>
        /// Attempts to remove a member from the club using their Unity ID.
        /// </summary>
        /// <param name="toRemoveMemberData">The member data of the user to be removed from the club.</param>
        /// <returns>Returns true if the member was successfully removed, otherwise false.</returns>
        private async UniTask<bool> TryToRemoveMemberFromClub(MemberData toRemoveMemberData)
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to remove a member but the references are null or you aren't a member of any clubl", this);
                return false;
            }

            // Validate the input MemberData
            if (toRemoveMemberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null or its unityMemberId is null or empty. Cannot remove member.");
                return false;
            }

            // Get the game rank permissions from the config data
            var globalPermissions = gameManager.GameBackendConfigData.clubsConfig.clubPermissionDatas;

            // Get my rank in the club
            var myRank = FirestoreClubData.members.FirstOrDefault(member => member.unityMemberId == authManager.UUID)?.rank;

            // Check if the user is trying to remove themselves
            var isRemovingSelf = toRemoveMemberData.unityMemberId == authManager.UUID;

            // Get the permissions for my rank and check if I have permission to remove members
            var myRankPermission = globalPermissions?.FirstOrDefault(perm => perm.clubRanksType == myRank);
            if (!isRemovingSelf && ((!myRankPermission?.clubPermissionsType.HasFlag(ClubPermissionsTypes.RemoveMember)) ?? true))
            {
                Debug.LogWarning($"You don't have permission to remove members. Your rank: {myRank}");
                return false;
            }

            // Check if the new rank is different to the current one
            if (!isRemovingSelf && myRank.Value <= toRemoveMemberData.rank)
            {
                Debug.LogWarning($"You can only remove members with a lower rank than yours. Your rank: {myRank}, Target rank: {toRemoveMemberData.rank}");
                return false;
            }

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(new Dictionary<string, object>
            {
                ["toRemoveUserUnityID"] = toRemoveMemberData.unityMemberId,
            });

            // Store the variables to use the trycat pattern to store the response
            var tryToRemoveMemberToClubRequest = default(string);
            var tryToRemoveMemberToClubResponse = default(FirestoreClubDataResponse);

            try
            {
                // Use the backedn binding of the corresponding player to remove the player of the club
                tryToRemoveMemberToClubRequest = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToRemoveMemberToClub(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.TryToRemoveMemberToClub),
                    showLoading: false,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                tryToRemoveMemberToClubResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToRemoveMemberToClubRequest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to remove member: {ex.Message}");
            }

            // Check if the response is positive
            if (tryToRemoveMemberToClubResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
            {
                Debug.Log($"Successfully removed user {toRemoveMemberData.unityMemberId} from the club.");

                // Refresh the club data from Firestore
                FirestoreClubData = tryToRemoveMemberToClubResponse.playerClubData;

                // Check if PlayerClubData is null after refreshing
                if (FirestoreClubData is null && !isRemovingSelf)
                {
                    Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                    return false;
                }

                // If the user removed themselves from the club, clear the local PlayerClubData
                else if (isRemovingSelf)
                { 
                    gameManager.OverrideFirestorePlayerClubName(null);
                    gameManager.StartFetchingClubLeaderboard();

                    ClubUI.TryToGoToHomeScreen();
                }

                // Clean each stored club data
                await ClubUI.CleanData();

                // Show an success prompt in the UI
                ClubUI.ShowPrompt($"{toRemoveMemberData.memberName} was removed from the club.");

                return true;
            }
            else
            {
                if (tryToRemoveMemberToClubResponse is not null)
                    Debug.LogWarning($"Failed to <b>Remove</b> player with id {toRemoveMemberData.firebaseMemberId} to club. " +
                        $"\n\n<b>Response code</b>: {tryToRemoveMemberToClubResponse.clubResponseCodeType}" +
                        $"\n<b>Message</b>: {tryToRemoveMemberToClubResponse.message}");
                else
                    Debug.LogWarning($"Failed to remove user {toRemoveMemberData.unityMemberId} from the club.");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt(tryToRemoveMemberToClubResponse.message ?? $"Failed to remove user {toRemoveMemberData.unityMemberId} from the club.");
            }

            return false;
        }

        /// <summary>
        /// Attempts to override a member rank in the club using their Unity ID.
        /// </summary>
        /// <param name="newRank">The new rank to assign to the member.</param>
        /// <param name="toOverrideRankMemberData">The member data of the user whose rank is to be overridden.</param>
        /// <returns>Returns true if the member's rank was successfully overridden, otherwise false.</returns>
        private async UniTask<bool> TryToOverrideMemberRank(ClubRanksTypes newRank, MemberData toOverrideRankMemberData)
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to override member rank but the references are null or you aren't a member of any club", this);
                return false;
            }

            // Validate the input MemberData
            if (toOverrideRankMemberData is null or { unityMemberId: null or "" })
            {
                Debug.LogError("MemberData is null or its unityMemberId is null or empty. Cannot override rank.");
                return false;
            }

            // Get the game rank permissions from the config data
            var globalPermissions = gameManager.GameBackendConfigData.clubsConfig.clubPermissionDatas;

            // Get my rank in the club
            var myRank = FirestoreClubData.members.FirstOrDefault(member => member.unityMemberId == authManager.UUID)?.rank;

            // Get the permissions for my rank and check if I have permission to override ranks
            var myRankPermission = globalPermissions?.FirstOrDefault(perm => perm.clubRanksType == myRank);
            if ((!myRankPermission?.clubPermissionsType.HasFlag(ClubPermissionsTypes.OverrideRank)) ?? true)
            {
                Debug.LogWarning($"You don't have permission to override member ranks. Your rank: {myRank}");
                return false;
            }

            // Check if the new rank is different to the current one
            if (myRank.Value <= toOverrideRankMemberData.rank)
            {
                Debug.LogWarning($"You can only override ranks lower than yours. Your rank: {myRank}, Target rank: {toOverrideRankMemberData.rank}");
                return false;
            }

            // Create the leaderboard ID based on the game mode and number of players and serialize the data to be sent to the backend
            var encryptedJsonData = authManager.SerializeAndEncryptData(new Dictionary<string, object>
            {
                ["toChangeRankID"] = toOverrideRankMemberData.unityMemberId,
                ["newRank"] = newRank
            });

            // Save local old rank for logging purposes
            var oldRank = toOverrideRankMemberData.rank;

            // Store the variables to use the trycat pattern to store the response
            var tryToOverrideRankRequest = default(string);
            var tryToOverrideRankResponse = default(FirestoreClubDataResponse);

            try
            {
                // Use the backend binding of the corresponding player to change its rank
                tryToOverrideRankRequest = await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToOverrideRank(encryptedJsonData).AsUniTask(),
                    taskId: nameof(module.TryToOverrideRank),
                    showLoading: false,
                    returnExceptionOnError: true);

                // Deserialize and decrypt the data
                tryToOverrideRankResponse = authManager.DeserializeAndDecryptData<FirestoreClubDataResponse>(tryToOverrideRankRequest);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to override member rank: {ex.Message}");
            }

            // Check if the response is positive
            if (tryToOverrideRankResponse is not null and { clubResponseCodeType: ClubResponseCodeType.Success })
            { 
                Debug.Log($"Successfully override rank of user {toOverrideRankMemberData.memberName}({toOverrideRankMemberData.unityMemberId}) from <b>{oldRank}</b> to <b>{oldRank}</b>");

                // Refresh the club data from Firestore
                FirestoreClubData = tryToOverrideRankResponse.playerClubData;

                // Check if PlayerClubData is null after refreshing
                if (FirestoreClubData is null)
                {
                    Debug.LogWarning("PlayerClubData is null after updating club data, cannot update local data.");
                    return false;
                }

                // Show an success prompt in the UI
                ClubUI.ShowPrompt($"Successfully override rank of user {toOverrideRankMemberData.memberName} to {newRank}.");

                return true;
            } 
            else
            {
                if (tryToOverrideRankResponse is not null)
                    Debug.LogWarning($"Failed to <b>Change rank</b> of player with id {toOverrideRankMemberData.firebaseMemberId} from <b>{oldRank}</b> to <b>{oldRank}" +
                        $"\n\n<b>Response code</b>: {tryToOverrideRankResponse.clubResponseCodeType}" +
                        $"\n<b>Message</b>: {tryToOverrideRankResponse.message}");
                else
                    Debug.LogWarning($"Failed to override rank of user {toOverrideRankMemberData.memberName}({toOverrideRankMemberData.unityMemberId}) from <b>{oldRank}</b> to <b>{oldRank}</b>");

                // Show an error prompt in the UI
                ClubUI.ShowPrompt(tryToOverrideRankResponse.message ?? $"Failed to override rank of user {toOverrideRankMemberData.memberName}.");
            }

            return false;
        }

        /// <summary>
        /// Attempts to send a club chat message
        /// </summary>
        /// <param name="input">The message to send in the club chat.</param>
        /// <returns>Returns true if the message was successfully sent, otherwise false.</returns>
        private async UniTask<bool> TryToSendChatMessage(string input)
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to send a club chat message but you are not in a club.", this);
                return default;
            }

            // Validate the search input
            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("Search input is null or empty. Cannot search clubs.");
                return default;
            }

            // Use the game manager to search clubs by name
            var wasMessageSentProperly = await gameManager.TryToSendClubChatMessage(input);
            if (!wasMessageSentProperly)
            {
                Debug.LogWarning("The message couldn't been sent");
                ClubUI.ShowPrompt("The message couldn't been sent");
            }

            return wasMessageSentProperly;
        }
        
        /// <summary>
        /// Attempts to subscribe the player to club chat in firestore
        /// </summary>
        /// <returns>Returns true if the player was successfully subscribed to club chat, otherwise false.</returns>
        private async UniTask<bool> TryToSubscribeToClubChat()
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to subscribe to club chat but you are not in a club.", this);
                return default;
            }

            // Use the game manager to search clubs by name
            var wasSubscribeProperly = await gameManager.TryToSubscribeToClubChat();
            if (!wasSubscribeProperly)
            {
                Debug.LogWarning("The player couldn't subscribe to club chat");
                ClubUI.ShowPrompt("Unknow issue: the player can't check for new Club Messages for a while");
            }

            return wasSubscribeProperly;
        }
        
        /// <summary>
        /// Attempts to unsubscribe the player from the club chat in firestore
        /// </summary>
        /// <returns>Returns true if the player was successfully unsubscribed from club chat, otherwise false.</returns>
        private async UniTask<bool> TryToUnsubscribeFromClubChat()
        {
            // Check if GameManager is null or check if the user is not a member
            if (gameManager is null || !IsClubMember)
            {
                Debug.LogWarning("You are trying to unsubscribe to club chat but you are not in a club.", this);
                return default;
            }

            // Use the game manager to search clubs by name
            var wasUnsubscribeProperly = await gameManager.TryToUnsubscribeFromClubChat();
            if (!wasUnsubscribeProperly)
                Debug.LogWarning("The player couldn't unsubscribe from club chat");

            return wasUnsubscribeProperly;
        }
        #endregion
    }
}
