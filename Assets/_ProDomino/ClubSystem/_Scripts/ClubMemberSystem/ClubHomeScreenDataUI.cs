using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.FirestoreClubData;
using static ProDomino.ClubSystem.ClubIconDataSelectorController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages the UI elements and data configuration for the club home screen, including club name, slogan, rank, and
    /// icon preview.
    /// </summary>
    internal class ClubHomeScreenDataUI : MonoBehaviour
    {
        [SerializeField] private Preview iconPreview;
        [SerializeField] private TMP_Text clubNameInputField;
        [SerializeField] private TMP_Text clubSloganInputField;
        [SerializeField] private TMP_Text clubRankText;

        [Header("Header Actions")]
        [SerializeField] private Button editClubButton;
        [SerializeField] private Button rolesAndPermissionsButton;

        [Header("Header Stats")]
        [SerializeField] private TMP_Text totalMembersText;
        [SerializeField] private TMP_Text applicationsText;

        private Action onEditClub;
        private Action onOpenRolesAndPermissions;

        internal Preview IconPreview => iconPreview;

        private void Awake()
        {
            if (editClubButton)
                editClubButton.onClick.AddListener(OnEditClubClicked);

            if (rolesAndPermissionsButton)
                rolesAndPermissionsButton.onClick.AddListener(OnRolesAndPermissionsClicked);
        }

        private void OnDestroy()
        {
            if (editClubButton)
                editClubButton.onClick.RemoveListener(OnEditClubClicked);

            if (rolesAndPermissionsButton)
                rolesAndPermissionsButton.onClick.RemoveListener(OnRolesAndPermissionsClicked);
        }

        /// <summary>
        /// Assigns the callbacks invoked by the header shortcut buttons.
        /// </summary>
        /// <param name="onEditClub">Callback invoked by the "Edit Club" shortcut.</param>
        /// <param name="onOpenRolesAndPermissions">Callback invoked by the "Roles &amp; Permissions" shortcut.</param>
        internal void SetHeaderActions(Action onEditClub, Action onOpenRolesAndPermissions)
        {
            this.onEditClub = onEditClub;
            this.onOpenRolesAndPermissions = onOpenRolesAndPermissions;
        }

        /// <summary>
        /// Updates the header summary counters.
        /// </summary>
        /// <param name="totalMembers">Amount of members currently in the club.</param>
        /// <param name="applications">Amount of pending joining requests.</param>
        internal void ConfigureStats(int totalMembers, int applications)
        {
            if (totalMembersText)
                totalMembersText.text = totalMembers.ToString();

            if (applicationsText)
                applicationsText.text = applications.ToString();
        }

        private void OnEditClubClicked() => onEditClub?.Invoke();

        private void OnRolesAndPermissionsClicked() => onOpenRolesAndPermissions?.Invoke();

        /// <summary>
        /// Configures the club data entry fields and icon preview with the provided data.
        /// </summary>
        /// <param name="clubName">The name of the club.</param>
        /// <param name="clubSlogan">The slogan of the club.</param>
        /// <param name="clubRank">The rank of the club.</param>
        /// <param name="iconData">The icon data for the club.</param>
        internal void Configure(string clubName, string clubSlogan, string clubRank, IconData iconData)
        {
            // Set the club name input field
            if (clubNameInputField)
                clubNameInputField.text = clubName;
            else
                Debug.LogError("Club Name Input Field is not assigned in the inspector.", this);

            // Set the club slogan input field
            if (clubSloganInputField)
                clubSloganInputField.text = clubSlogan;
            else
                Debug.LogError("Club Slogan Input Field is not assigned in the inspector.", this);

            // Set the club rank text (the "Club Rank" caption is a static label of the header stat block)
            if (clubRankText)
                clubRankText.text = string.IsNullOrEmpty(clubRank) ? "-" : clubRank;
            else
                Debug.LogError("Club Rank Text is not assigned in the inspector.", this);

            // Configure the preview with the current club icon data or default values if not set
            if (IconPreview != null)
            {
                IconPreview.SetPreviewData(ClubDataSelectableType.BaseShield, iconData?.shieldId, iconData?.shieldColorId, Preview.byDefaultShieldSprite, Preview.byDefaultShieldColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.Texture, iconData?.textureId, iconData?.textureColorId, Preview.byDefaultTextureSprite, Preview.byDefaultTextureColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.CentralImage, iconData?.centralImageId, iconData?.centralImageColorId, Preview.byDefaultCentralImageSprite, Preview.byDefaultCentralImageColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.Background, default, iconData?.backgroundColorId, default, Preview.byDefaultbackgroundColor);
            }
            else
                Debug.LogError("Icon Preview is not assigned in the inspector.", this);

            transform.RefreshLayoutGroupsImmediateAndRecursive();
        }
    }
}
