using HelperSharedLibrary;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Displays a pop-up UI for club administrators to accept or decline applicant requests to join a club, handling
    /// user input and asynchronous operations for member management.
    /// </summary>
    internal class ClubDetermineApplicantJoiningPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private CustomButtonUI confirmButton;
        [SerializeField] private CustomButtonUI cancelButton;
        [SerializeField] private TMP_Text descriptionLabel;

        private bool isAccepting;
        private AsyncActionHandler<FirestoreClubData.ApplicantData> tryToAcceptMemberRequest;
        private AsyncActionHandler<FirestoreClubData.ApplicantData> tryToDeclineMemberRequest;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;

        internal ClubApplicantEntry SelectedApplicantEntry { get; private set; }

        private void Start()
        {
            // Add button listeners to confirm rank change if its reference is assigned
            if (confirmButton)
                confirmButton.onClick.AddListener(DetermineIntentions);
            else
                Debug.LogError("Confirm Change Button is not assigned in the inspector.", this);

            // Add button listeners to deny rank change if its reference is assigned
            if (cancelButton)
                cancelButton.onClick.AddListener(Cancel);
            else
                Debug.LogError("Deny Change Button is not assigned in the inspector.", this);

            // Initially hide the rank prompt
            Hide();
        }

        /// <summary>
        /// Initializes the club rank prompt with the necessary callbacks.
        /// </summary>
        /// <param name="getCurrentPlayerMemberData">Function to retrieve the current player's member data.</param>
        /// <param name="tryToAcceptMemberRequest">Async action handler for accepting a member request.</param>
        /// <param name="tryToDeclineMemberRequest">Async action handler for declining a member request.</param>
        internal void Initialize
            (ref Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,
            AsyncActionHandler<FirestoreClubData.ApplicantData> tryToAcceptMemberRequest,
            AsyncActionHandler<FirestoreClubData.ApplicantData> tryToDeclineMemberRequest)

        {
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.tryToAcceptMemberRequest = tryToAcceptMemberRequest ?? throw new ArgumentNullException(nameof(tryToAcceptMemberRequest));
            this.tryToDeclineMemberRequest = tryToDeclineMemberRequest ?? throw new ArgumentNullException(nameof(tryToDeclineMemberRequest));
        }

        /// <summary>
        /// Configures the rank prompt for the specified club member entry.
        /// </summary>
        /// <param name="clubApplicantEntry">The club applicant entry to configure the prompt for.</param>
        /// <param name="isAccepting">Indicates whether the prompt is for accepting or declining the member request.</param>
        internal void Configure(ClubApplicantEntry clubApplicantEntry, bool isAccepting)
        {
            // Register whether we are accepting or declining the member request
            this.isAccepting = isAccepting;

            if (clubApplicantEntry is null)
            {
                Debug.LogError("clubApplicantEntry is null. Cannot configure the ClubRankPrompt.");
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
            SelectedApplicantEntry = clubApplicantEntry;

            // Reset all rank entries
            if (descriptionLabel)
            { 
                if (this.isAccepting)
                    descriptionLabel.text = $"Are you sure you want to accept {SelectedApplicantEntry.ApplicantData.applicantName} joining club request?";
                else
                    descriptionLabel.text = $"Are you sure you want to decline {SelectedApplicantEntry.ApplicantData.applicantName} joining club request?";
            }
            else
                Debug.LogError("Description Label is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Determines whether to accept or decline the member request based on user input.
        /// </summary>
        private void DetermineIntentions()
        { 
            if (isAccepting)
            {
                Debug.Log("Accepting member request.");
                ConfirmMemberRequest();
            } 
            else
            {
                Debug.Log("Declining member request.");
                DeclineMemberRequest();
            }
        }

        /// <summary>
        /// Confirms the member removal operation
        /// </summary>
        private async void ConfirmMemberRequest()
        {
            if (tryToAcceptMemberRequest is null)
            {
                Debug.LogError("tryToAcceptMemberRequest callback is not set. Please initialize the ClubRankPrompt with a valid callback.");
                return;
            }

            if (SelectedApplicantEntry is null or { ApplicantData: null })
            {
                Debug.LogError("SelectedApplicantEntry or its MemberData is null. Cannot accept member request.");
                return;
            }

            // Block the UI while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                await tryToAcceptMemberRequest(SelectedApplicantEntry.ApplicantData);
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
        private async void DeclineMemberRequest()
        {
            if (tryToDeclineMemberRequest is null)
            {
                Debug.LogError("tryToDeclineMemberRequest callback is not set. Please initialize the class with a valid callback.");
                return;
            }

            if (SelectedApplicantEntry is null or { ApplicantData: null })
            {
                Debug.LogError("SelectedApplicantEntry or its ApplicantData is null. Cannot decline member request.");
                return;
            }

            // Block the UI while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                await tryToDeclineMemberRequest(SelectedApplicantEntry.ApplicantData);
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
        private void Cancel()
        {
            SelectedApplicantEntry = null;
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
