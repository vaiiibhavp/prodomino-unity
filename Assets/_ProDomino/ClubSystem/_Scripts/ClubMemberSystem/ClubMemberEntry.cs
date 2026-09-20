using Cysharp.Threading.Tasks;
using DG.Tweening.Core.Easing;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
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
    /// Represents a UI entry for a club member, displaying member information and providing actions such as rank
    /// override and removal based on permissions.
    /// </summary>
    internal class ClubMemberEntry : MonoBehaviour
    {
        [Header("Action Buttons")]
        [SerializeField] private Button openOverrideRankPromptButton;
        [SerializeField] private Button openRemoveMemberPopUpButton;

        [Header("UI References")]
        [SerializeField] private TMP_Text clubPosition;
        [SerializeField] private TMP_Text rankingLabel;
        [SerializeField] private TMP_Text playerNameLabel;
        [SerializeField] private TMP_Text idLabel;
        [SerializeField] private TMP_Text joinedDate;
        [SerializeField] private TMP_Text completedAchievementsLabel;
        [SerializeField] private Image playerImage;
        [SerializeField] private Image rankImage;
        [SerializeField] private Image[] achievementsImages;

        private AuthManager authManager;
        private DictionaryService dictionaryService;
        private Func<ConfigData> getConfigData;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;
        private Action<ClubMemberEntry> openClubRankPrompt;
        private Action<ClubMemberEntry> openRemoveMemberPopUp;

        internal ConfigData ConfigData => getConfigData?.Invoke();
        internal FirestoreClubData.MemberData MemberData { get; private set; }
        internal FirestoreClubData.MemberData CurrentPlayerMemberData => getCurrentPlayerMemberData?.Invoke();
        internal Graphic TargetGraphic => openOverrideRankPromptButton?.targetGraphic;

        /// <summary>
        /// Initializes the member entry with necessary services and callbacks.
        /// </summary>
        /// <param name="authManager">The authentication manager.</param>
        /// <param name="dictionaryService">The dictionary service.</param>
        /// <param name="getConfigData">Function to get the configuration data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to get the current player's member data.</param>
        /// <param name="openClubRankPrompt">Action to open the club rank prompt.</param>
        /// <param name="openRemoveMemberPopUp">Action to open the remove member pop-up.</param>
        internal void Initialize
            (AuthManager authManager,
            DictionaryService dictionaryService,
            Func<ConfigData> getConfigData,
            Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,
            Action<ClubMemberEntry> openClubRankPrompt,
            Action<ClubMemberEntry> openRemoveMemberPopUp)
        {
            this.authManager = authManager;
            this.dictionaryService = dictionaryService;
            this.getConfigData = getConfigData;
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData;
            this.openClubRankPrompt = openClubRankPrompt;
            this.openRemoveMemberPopUp = openRemoveMemberPopUp;

            // Add button listeners to open override rank prompt if its reference is assigned
            if (openOverrideRankPromptButton)
                openOverrideRankPromptButton.onClick.AddListener(OpenClubRankPrompt);
            else
                Debug.LogError("Override Rank Button is not assigned in the inspector.", this);

            // Add button listeners to open remove member pop up if its reference is assigned
            if (openRemoveMemberPopUpButton)
                openRemoveMemberPopUpButton.onClick.AddListener(OpenRemoveMemberPopUp);
            else
                Debug.LogError("Remove Member Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Configures the member entry with the provided member data.
        /// </summary>
        /// <param name="memberData">The member data to configure the entry with.</param>
        /// <param name="profileIconSprite">The profile icon sprite to display.</param>
        /// <param name="clubIndex">The index of the member in the club.</param>
        internal void Configure(FirestoreClubData.MemberData memberData, Sprite profileIconSprite, int clubIndex)
        {
            MemberData = memberData;

            // Set the text label of club position
            if (clubPosition)
                clubPosition.text = (clubIndex + 1).ToString();
            else
                Debug.LogError("Club Position Label is not assigned in the inspector.", this);

            // Determine the rank string with spaces
            if (rankingLabel != null)
                rankingLabel.text = MemberData != null && Regex.Replace(MemberData.rank.ToString(), "(?<!^)([A-Z])", " $1") is var replacedName 
                    ? replacedName 
                    : string.Empty;
            else
                Debug.LogError("Ranking Label is not assigned in the inspector.", this);

            // Set the name of the player
            if (playerNameLabel)
                playerNameLabel.text = MemberData?.memberName ?? string.Empty;
            else
                Debug.LogError("Player Name Label is not assigned in the inspector.", this);

            // Set the Unity member ID
            if (idLabel)
                idLabel.text = MemberData?.unityMemberId ?? string.Empty;
            else
                Debug.LogError("ID Label is not assigned in the inspector.", this);

            // Shows joined date in dd/MM/yyyy format
            if (joinedDate)
            { 
                // Convert Unix timestamp (seconds) to DateTime
                var joinedDateTime = MemberData != null ? DateTimeOffset.FromUnixTimeSeconds(MemberData.joinedDate).UtcDateTime : DateTime.MinValue;

                // Format as dd/MM/yyyy
                joinedDate.text = joinedDateTime.ToString("dd/MM/yyyy");
            }
            else
                Debug.LogError("Joined Date Label is not assigned in the inspector.", this);

            // Set completed achievements label
            if (completedAchievementsLabel)
                completedAchievementsLabel.text = "Completed Achievements: ";
            else
                Debug.LogError("Completed Achievements Label is not assigned in the inspector.", this);

            // Set completed count (si lo tienes en metadata)
            if (MemberData?.totalAchievements > 0)
                completedAchievementsLabel.text += $"<b>{MemberData?.totalAchievements ?? -1}</b>";
            else
                completedAchievementsLabel.text += "0";

            // Fill in player image
            if (playerImage)
                playerImage.sprite = profileIconSprite;
            else
                Debug.LogError("Player Image is not assigned in the inspector.", this);

            // Fill in rank image
            if (rankImage)
            {
                if (dictionaryService)
                {
                    var rankSprite = MemberData != null ? dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_Ranks", MemberData.rank.ToString()) : null;
                    if (rankSprite != null)
                        rankImage.sprite = rankSprite;
                    else
                        Debug.LogWarning($"Rank sprite with ID '{MemberData?.rank.ToString() ?? "-1"}' not found in the dictionary.");
                }
            } 
            else
                Debug.LogError("Rank  Image is not assigned in the inspector.", this);


            // Fill in achievements images
            if (achievementsImages is not null and { Length: > 0 })
            {
                // Get the sprites for the member's badges
                var badgesData = MemberData?.badges?.Select(badge => !string.IsNullOrEmpty(badge)
                        ? dictionaryService.GetSprite(Consts.CollectionKeys.Achievements, badge)
                        : default)
                    ?.ToArray();

                // Iterate and assign sprites to achievement images
                for (int i = 0; i < achievementsImages.Length; i++)
                {
                    if (MemberData?.badges != null && i < MemberData.badges.Length)
                    {
                        var sprite = badgesData.ElementAtOrDefault(i);
                        achievementsImages[i].enabled = true;

                        if (dictionaryService)
                            achievementsImages[i].sprite = sprite;
                    } 
                    else
                        achievementsImages[i].enabled = false;
                }
            }
            else
                Debug.LogError("Achievements Images array is not assigned or empty in the inspector.", this);

            // Prepare the permission-based buttons
            var isRemovingSelf = CurrentPlayerMemberData?.unityMemberId == MemberData?.unityMemberId;
            var isLowerRankThanSelf = CurrentPlayerMemberData?.rank < MemberData?.rank;
            var hasRemovePermission = false;

            // Check if the current player has permission to remove members
            if (ConfigData is not null and { clubsConfig: not null and { clubPermissionDatas: not null } })
            { 
                var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData?.rank);
                hasRemovePermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.RemoveMember);
            }

            var shouldShowRemoveButton = isRemovingSelf || (hasRemovePermission && !isLowerRankThanSelf);

            // Determine if the remove button should be shown according to permissions
            if (openRemoveMemberPopUpButton)
                openRemoveMemberPopUpButton.gameObject.SetActive(shouldShowRemoveButton);
            else
                Debug.LogError("Remove Member Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Attempts to open the override rank prompt for this entry.
        /// </summary>
        private void OpenClubRankPrompt()
        {
            if (ConfigData is null)
            {
                Debug.LogError("ConfigData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            if (MemberData is null)
            {
                Debug.LogError("MemberData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            if (openClubRankPrompt is null)
            {
                Debug.LogError("openClubRankPrompt is not assigned, cannot open rank prompt");
                return;
            }

            if (CurrentPlayerMemberData is null)
            {
                Debug.LogError("CurrentPlayerMemberData is null, cannot compare ranks");
                return;
            }

            // Try to open the rank prompt for this member entry if the current player has permission to override ranks
            var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData.rank);
            if (currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.OverrideRank))
            {
                var isChangingOwnRank = CurrentPlayerMemberData.unityMemberId == MemberData.unityMemberId;
                var isLowerRankThanSelf = CurrentPlayerMemberData.rank < MemberData.rank;

                if (!isChangingOwnRank && !isLowerRankThanSelf)
                    openClubRankPrompt(this);
                else
                    Debug.LogWarning("You cannot change your own rank or the rank of a member with an equal or higher rank.");
            } 
            else
                Debug.LogWarning("The current player does not have permission to change ranks.");
        }

        /// <summary>
        /// Attempts to open the remove member pop-up for this entry.
        /// </summary>
        private void OpenRemoveMemberPopUp()
        {
            if (ConfigData is null)
            {
                Debug.LogError("ConfigData is null, couldn't determine if the current player can change ranks.");
                return;
            }

            if (MemberData is null)
            {
                Debug.LogError("MemberData is null");
                return;
            }

            if (openRemoveMemberPopUp is null)
            {
                Debug.LogError("openClubRankPrompt is not assigned");
                return;
            }

            if (CurrentPlayerMemberData is null)
            {
                Debug.LogError("CurrentPlayerMemberData is null, cannot compare ranks");
                return;
            }

            // Prepare the permission-based buttons
            var isRemovingSelf = CurrentPlayerMemberData.unityMemberId == MemberData.unityMemberId;
            var isLowerRankThanSelf = CurrentPlayerMemberData.rank < MemberData.rank;
            var hasRemovePermission = false;

            // Check if the current player has permission to remove members
            if (ConfigData is not null and { clubsConfig: not null and { clubPermissionDatas: not null } })
            {
                var currentPlayerPermissions = ConfigData.clubsConfig.clubPermissionDatas.FirstOrDefault(p => p.clubRanksType == CurrentPlayerMemberData.rank);
                hasRemovePermission = currentPlayerPermissions is not null && currentPlayerPermissions.clubPermissionsType.HasFlag(Enums.ClubPermissionsTypes.RemoveMember);
            }

            // Try to open the remove member pop-up for this member entry if the current player has permission to remove members
            var shouldShowRemoveButton = isRemovingSelf || (hasRemovePermission && !isLowerRankThanSelf);
            if (shouldShowRemoveButton)
                openRemoveMemberPopUp(this);
            else
                Debug.LogWarning("You do not have permission to remove this member.");
        }
    }
}
