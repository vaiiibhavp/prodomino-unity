using HelperSharedLibrary;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Displays a pop-up UI for confirming and handling the removal of a club member, including initialization,
    /// configuration, and user interaction.
    /// </summary>
    public class ClubRemoveMemberPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private CustomButtonUI confirmRemoveMemberButton;
        [SerializeField] private CustomButtonUI cancelRemoveMemberButton;
        [SerializeField] private TMP_Text removeDescriptionLabel;

        private AsyncActionHandler<FirestoreClubData.MemberData> tryToRemoveMember;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;

        internal ClubMemberEntry SelectedMemberEntry { get; private set; }

        private void Start()
        {
            // Add button listeners to confirm rank change if its reference is assigned
            if (confirmRemoveMemberButton)
                confirmRemoveMemberButton.onClick.AddListener(ConfirmRemoveMember);
            else
                Debug.LogError("Confirm Change Button is not assigned in the inspector.", this);
            
            // Add button listeners to deny rank change if its reference is assigned
            if (cancelRemoveMemberButton)
                cancelRemoveMemberButton.onClick.AddListener(CancelRemoveMember);
            else
                Debug.LogError("Deny Change Button is not assigned in the inspector.", this);

            // Initially hide the rank prompt
            Hide();
        }

        /// <summary>
        /// Initializes the ClubRemoveMemberPopUp with the necessary callbacks.
        /// </summary>
        /// <param name="getCurrentPlayerMemberData">Function to get the current player's member data.</param>
        /// <param name="tryToRemoveMember">Async action handler to attempt to remove a member.</param>
        internal void Initialize
            (ref Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,
            AsyncActionHandler <FirestoreClubData.MemberData> tryToRemoveMember)
        {
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.tryToRemoveMember = tryToRemoveMember ?? throw new ArgumentNullException(nameof(tryToRemoveMember));
        }

        /// <summary>
        /// Configures the rank prompt for the specified club member entry.
        /// </summary>
        /// <param name="clubMemberEntry">The club member entry to configure the rank prompt for.</param>
        internal void Configure(ClubMemberEntry clubMemberEntry)
        { 
            if (clubMemberEntry is null)
            {
                Debug.LogError("ClubMemberEntry is null. Cannot configure the ClubRankPrompt.");
                return;
            }

            if (getCurrentPlayerMemberData is null)
            {
                Debug.LogError("GetCurrentPlayerMemberData callback is not set. Please initialize the ClubRankPrompt with a valid callback.");
                return;
            }

            var currentPlayerMemberData = getCurrentPlayerMemberData();
            if (currentPlayerMemberData is null)
            {
                Debug.LogError("Current player's MemberData is null. Cannot configure the ClubRankPrompt.");
                return;
            }

            // Set the currently selected member entry
            SelectedMemberEntry = clubMemberEntry;

            var isRemovingSelf = SelectedMemberEntry.MemberData.unityMemberId == currentPlayerMemberData.unityMemberId;

            // Reset all rank entries
            if (removeDescriptionLabel)
            {
                if (isRemovingSelf) 
                    removeDescriptionLabel.text = "Are you sure you want to leave the club?";
                else
                    removeDescriptionLabel.text = $"Are you sure you want to expel {SelectedMemberEntry.MemberData.memberName} from the club?";
            }
            else
                Debug.LogError("Remove Description Label is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Confirms the member removal operation
        /// </summary>
        private async void ConfirmRemoveMember()
        {
            if (tryToRemoveMember is null)
            { 
                Debug.LogError("TryToOverrideRank callback is not set. Please initialize the ClubRankPrompt with a valid callback.");
                return;
            }

            if (SelectedMemberEntry is null or { MemberData: null })
            {
                Debug.LogError("CurrentSelectedMemberEntry or its MemberData is null. Cannot override rank.");
                return;
            }

            // Block the UI while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                await tryToRemoveMember(SelectedMemberEntry.MemberData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to override the rank: {ex.Message}", this);

                // Unblock the UI in case of an error
                rootCanvasGroup.SetActive(true, isSettingAlpha: false);
            }
            finally
            {
                // Turn off the UI after the operation is complete
                Hide();
            }
        }

        /// <summary>
        /// Cancels the member removal operation and hides the pop-up.
        /// </summary>
        private void CancelRemoveMember()
        {
            SelectedMemberEntry = null;
            Hide();
        }

        /// <summary>
        /// Shows the rank prompt UI.
        /// </summary>
        internal void Show()
        {
            if (!rootCanvasGroup)
            {
                Debug.LogError("Root Canvas Group is not assigned in the inspector.", this);
                return;
            }

            rootCanvasGroup.SetActive(true);
        }

        /// <summary>
        /// Hides the rank prompt UI.
        /// </summary>
        internal void Hide()
        {
            if (!rootCanvasGroup)
            {
                Debug.LogError("Root Canvas Group is not assigned in the inspector.", this);
                return;
            }

            rootCanvasGroup.SetActive(false);
        }
    }
}
