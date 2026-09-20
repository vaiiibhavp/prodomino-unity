using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a UI entry for a club applicant, providing functionality to display applicant information and handle
    /// accept or decline actions based on permissions.
    /// </summary>
    public class ClubApplicantEntry : MonoBehaviour
    {
        [Header("Action Buttons")]
        [SerializeField] private Button openAcceptMemberPopUpButton;
        [SerializeField] private Button openDeclineMemberPopUpButton;

        [Header("UI References")]
        [SerializeField] private TMP_Text clubPosition;
        [SerializeField] private TMP_Text playerNameLabel;
        [SerializeField] private TMP_Text idLabel;
        [SerializeField] private TMP_Text ratingEloLabel;
        [SerializeField] private TMP_Text bestLeaderboardTierLabel;
        [SerializeField] private Image playerImage;
        [SerializeField] private Image bestLeaderboardImage;

        private DictionaryService dictionaryService;
        private Func<ConfigData> getConfigData;
        private Func<FirestoreClubData> getClubData;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;
        private Action<ClubApplicantEntry> openAcceptMemberPopUp;
        private Action<ClubApplicantEntry> openDeclineMemberPopUp;

        internal ConfigData ConfigData => getConfigData?.Invoke();
        internal FirestoreClubData.ApplicantData ApplicantData { get; private set; }
        internal FirestoreClubData.MemberData CurrentPlayerMemberData => getCurrentPlayerMemberData?.Invoke();

        /// <summary>
        /// Initializes the member entry with necessary services and callbacks.
        /// </summary>
        /// <param name="dictionaryService">The dictionary service for retrieving localized strings.</param>
        /// <param name="getConfigData">Function to retrieve configuration data.</param>
        /// <param name="getClubData">Function to retrieve club data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to retrieve the current player's member data.</param>
        /// <param name="openAcceptMemberPopUp">Action to open the accept member pop-up.</param>
        /// <param name="openDeclineMemberPopUp">Action to open the decline member pop-up.</param>
        internal void Initialize
            (DictionaryService dictionaryService,
            Func<ConfigData> getConfigData,
            Func<FirestoreClubData> getClubData,
            Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,
            Action<ClubApplicantEntry> openAcceptMemberPopUp,
            Action<ClubApplicantEntry> openDeclineMemberPopUp)
        {
            this.dictionaryService = dictionaryService;
            this.getConfigData = getConfigData;
            this.getClubData = getClubData;
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData;
            this.openAcceptMemberPopUp = openAcceptMemberPopUp;
            this.openDeclineMemberPopUp = openDeclineMemberPopUp;

            // Add button listeners to open open member pop up if its reference is assigned
            if (openAcceptMemberPopUpButton)
                openAcceptMemberPopUpButton.onClick.AddListener(OpenAcceptMemberPopUp);
            else
                Debug.LogError("Open Member Button is not assigned in the inspector.", this);

            if (openDeclineMemberPopUpButton)
                openDeclineMemberPopUpButton.onClick.AddListener(OpenDeclineMemberPopUp);
            else
                Debug.LogError("Open Member Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Configures the member entry with the provided member data.
        /// </summary>
        /// <param name="applicantData">The data of the applicant to configure the entry for.</param>
        /// <param name="profileIconSprite">The profile icon sprite of the applicant.</param>
        /// <param name="clubIndex">The index of the applicant in the club list.</param>
        internal void Configure(FirestoreClubData.ApplicantData applicantData, Sprite profileIconSprite, int clubIndex)
        {
            ApplicantData = applicantData;

            // Set the text label of club position
            if (clubPosition)
                clubPosition.text = (clubIndex + 1).ToString();
            else
                Debug.LogError("Club Position Label is not assigned in the inspector.", this);

            // Determine the ratting elo of the player
            if (ratingEloLabel != null)
                ratingEloLabel.text = ApplicantData?.eloRating.ToString() ?? string.Empty;
            else
                Debug.LogError("Ranking Label is not assigned in the inspector.", this);

            // Set the name of the player
            if (playerNameLabel)
                playerNameLabel.text = ApplicantData?.applicantName ?? string.Empty;
            else
                Debug.LogError("Player Name Label is not assigned in the inspector.", this);

            // Set the Unity member ID
            if (idLabel)
                idLabel.text = ApplicantData?.unityID ?? string.Empty;
            else
                Debug.LogError("ID Label is not assigned in the inspector.", this);

            // Set the best ranking image
            if (bestLeaderboardTierLabel != null)
            {
                var tier = ApplicantData != null && Regex.Replace(ApplicantData.bestLeaderboardTier.ToString(), "(?<!^)([A-Z])", " $1") is var replaceData 
                    ? replaceData 
                    : string.Empty;

                bestLeaderboardTierLabel.text = $"{tier} {ApplicantData?.bestLeaderboardScore ?? -1}";
            }
            else
                Debug.LogError("Ranking Label is not assigned in the inspector.", this);

            // Fill in player image
            if (playerImage)
                playerImage.sprite = profileIconSprite;
            else
                Debug.LogError("Player Image is not assigned in the inspector.", this);

            // Fill in leaderboard image
            if (bestLeaderboardImage)
            {
                if (dictionaryService)
                {
                    var sprite = dictionaryService.GetSprite(Consts.CollectionKeys.Ranks, ApplicantData?.bestLeaderboardTier.ToString() ?? string.Empty);
                    if (sprite != null)
                        bestLeaderboardImage.sprite = sprite;
                    else
                        Debug.LogWarning($"Leaderboard rank icon with ID '{ApplicantData?.bestLeaderboardTier}' not found in the dictionary.");
                }
            } 
            else
                Debug.LogError("Best Leaderboard Image is not assigned in the inspector.", this);

            // Prepare the permission-based buttons
            var hasAcceptPermission = false;
            var hasDeclinePermission = false;

            // Check if the current player has permission to open members
            if (ConfigData is not null and { clubsConfig: not null and { clubPermissionDatas: not null } })
            {
                var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData?.rank);
                hasAcceptPermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.AcceptApplicant);
                hasDeclinePermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.DeclineApplicant);
            }

            // Determine if the open button should be shown according to permissions
            if (openAcceptMemberPopUpButton)
                openAcceptMemberPopUpButton.gameObject.SetActive(hasAcceptPermission);
            else
                Debug.LogError("Open Member Button is not assigned in the inspector.", this);
            
            // Determine if the open button should be shown according to permissions
            if (openDeclineMemberPopUpButton)
                openDeclineMemberPopUpButton.gameObject.SetActive(hasDeclinePermission);
            else
                Debug.LogError("Open Member Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Attempts to open the accept applicant pop-up for this entry.
        /// </summary>
        private void OpenAcceptMemberPopUp()
        {
            if (ConfigData is null)
            {
                Debug.LogError("ConfigData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            if (ApplicantData is null)
            {
                Debug.LogError("ApplicantData is null");
                return;
            }

            if (openAcceptMemberPopUp is null)
            {
                Debug.LogError("openAcceptMemberPopUp is not assigned");
                return;
            }

            if (CurrentPlayerMemberData is null)
            {
                Debug.LogError("CurrentPlayerMemberData is null, cannot compare ranks");
                return;
            }

            var clubData = getClubData?.Invoke();
            if (clubData is null)
            {
                Debug.LogError("ClubData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            // Prepare the permission-based buttons
            var hasAcceptMemberPermission = false;
            var isAlreadyMember = clubData.members.Any(m => m.unityMemberId == ApplicantData.unityID);
            var hasSpaceInClub = clubData.members.Count < ConfigData.clubsConfig.membersLimit;

            // Check if the current player has permission to open members
            if (ConfigData is not null and { clubsConfig: not null and { clubPermissionDatas: not null } })
            {
                var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData.rank);
                hasAcceptMemberPermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.AcceptApplicant);
            }

            // Try to open the open member pop-up for this member entry if the current player has permission to open members
            var willOpenAcceptPopUp = hasAcceptMemberPermission && !isAlreadyMember && hasSpaceInClub;
            if (willOpenAcceptPopUp)
                openAcceptMemberPopUp(this);
            else
                Debug.LogWarning("You do not have permission to accept request of this member.");
        }
        
        /// <summary>
        /// Attempts to open the decline applicant pop-up for this entry.
        /// </summary>
        private void OpenDeclineMemberPopUp()
        {
            if (ConfigData is null)
            {
                Debug.LogError("ConfigData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            if (ApplicantData is null)
            {
                Debug.LogError("ApplicantData is null");
                return;
            }

            if (openDeclineMemberPopUp is null)
            {
                Debug.LogError("openDeclineMemberPopUp is not assigned");
                return;
            }

            if (CurrentPlayerMemberData is null)
            {
                Debug.LogError("CurrentPlayerMemberData is null, cannot compare ranks");
                return;
            }

            var clubData = getClubData?.Invoke();
            if (clubData is null)
            {
                Debug.LogError("ClubData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            // Prepare the permission-based buttons
            var hasDeclineMemberPermission = false;
            var stillAsApplicant = clubData.applicants.Any(m => m.unityID == ApplicantData.unityID);

            // Check if the current player has permission to open members
            if (ConfigData is not null and { clubsConfig: not null and { clubPermissionDatas: not null } })
            {
                var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData.rank);
                hasDeclineMemberPermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.DeclineApplicant);
            }

            // Try to open the open member pop-up for this member entry if the current player has permission to open members
            var willOpenDeclinePopUp = hasDeclineMemberPermission && stillAsApplicant;
            if (willOpenDeclinePopUp)
                openDeclineMemberPopUp(this);
            else
                Debug.LogWarning("You do not have permission to decline request of this member.");
        }
    }
}
