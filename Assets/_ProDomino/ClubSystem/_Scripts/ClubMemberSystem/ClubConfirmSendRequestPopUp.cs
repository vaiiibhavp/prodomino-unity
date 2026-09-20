using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Displays a pop-up UI for confirming and sending a club joining request, allowing users to confirm or cancel the
    /// operation.
    /// </summary>
    public class ClubConfirmSendRequestPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private CustomButtonUI confirmSendRequestButton;
        [SerializeField] private CustomButtonUI cancelSendRequestButton;
        [SerializeField] private TMP_Text descriptionLabel;

        private AsyncFuncHandler<bool, FirestoreClubData> tryToSendRequestToClub;
        internal FirestoreClubData FirestoreClubData { get; private set; }

        private void Start()
        {
            // Add button listeners to confirm rank change if its reference is assigned
            if (confirmSendRequestButton)
                confirmSendRequestButton.onClick.AddListener(ConfirmSendClubRequest);
            else
                Debug.LogError("Confirm Change Button is not assigned in the inspector.", this);

            // Add button listeners to deny rank change if its reference is assigned
            if (cancelSendRequestButton)
                cancelSendRequestButton.onClick.AddListener(CancelSendClubRequest);
            else
                Debug.LogError("Deny Change Button is not assigned in the inspector.", this);

            // Initially hide the rank prompt
            Hide();
        }

        /// <summary>
        /// Initializes the pop-up with the specified callback for sending a club request.
        /// </summary>
        /// <param name="tryToSendRequestToClub">The callback function to attempt sending a club request.</param>
        /// <exception cref="ArgumentNullException"></exception>
        internal void Initialize(AsyncFuncHandler<bool, FirestoreClubData> tryToSendRequestToClub)
        {
            this.tryToSendRequestToClub = tryToSendRequestToClub ?? throw new ArgumentNullException(nameof(tryToSendRequestToClub));
        }

        /// <summary>
        /// Configures the rank prompt for the specified club member entry.
        /// </summary>
        /// <param name="firestoreClubData">The club data to configure the pop-up with.</param>
        /// <exception cref="ArgumentNullException"></exception>
        internal async void Configure(FirestoreClubData firestoreClubData)
        {
            FirestoreClubData = firestoreClubData ?? throw new ArgumentNullException(nameof(firestoreClubData));

            // Update the description label with the member's name
            if (descriptionLabel)
            {
                var descriptionText = await LocalizationHelper.Get(Consts.LocalizationKeys.SendClubJoiningRequest);
                descriptionLabel.text = descriptionText?.Replace("{CLUB}", FirestoreClubData.clubName);
            }
            else
                Debug.LogError("Remove Description Label is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Confirms the member removal operation
        /// </summary>
        private async void ConfirmSendClubRequest()
        {
            if (tryToSendRequestToClub is null)
            {
                Debug.LogError("tryToSendRequestToClub callback is not set. Please initialize the tryToSendRequestToClub with a valid callback.");
                return;
            }

            // Block the UI while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                var wasSentProperly = await tryToSendRequestToClub(FirestoreClubData);
                if (!wasSentProperly)
                    Debug.LogWarning("The request to join the club was not sent properly.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to send request to club: {ex.Message}", this);

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
        private void CancelSendClubRequest()
        {
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
