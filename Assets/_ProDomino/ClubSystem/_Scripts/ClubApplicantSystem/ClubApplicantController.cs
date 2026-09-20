using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using UnityEngine;
using static HelperSharedLibrary.FirestoreClubData;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages the club applicant list UI, including initialization, configuration, and handling acceptance or decline
    /// of joining requests.
    /// </summary>
    public class ClubApplicantController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private ClubApplicantEntry appicantEntryPrefab;
        [SerializeField] private Transform membersEntryParent;
        [SerializeField] private ClubDetermineApplicantJoiningPopUp clubDetermineApplicantJoiningPopUp;

        private GameManager gameManager;
        private DictionaryService dictionaryService;
        private List<ClubApplicantEntry> applicantEntries;

        private Func<FirestoreClubData> getClubData;
        private Func<ConfigData> getConfigData;
        private Func<MemberData> getCurrentPlayerMemberData;
        private AsyncFuncHandler<bool, ApplicantData> tryToAcceptJoiningRequest;
        private AsyncFuncHandler<bool, ApplicantData> tryToDeclineJoiningRequest;

        internal FirestoreClubData ClubData => getClubData?.Invoke();

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            // Initialize existing applicant entries
            applicantEntries = membersEntryParent.GetComponentsInChildren<ClubApplicantEntry>(true).ToList() ?? new();
            if (applicantEntries is not null and { Count: > 0 })
                foreach (var entry in applicantEntries)
                    entry.Initialize
                        (dictionaryService,
                        () => getConfigData?.Invoke(),
                        () => getClubData?.Invoke(),
                        () => getCurrentPlayerMemberData?.Invoke(),
                        OnOpenApplicantJoiningPopUp_Accepting,
                        OnOpenApplicantJoiningPopUp_Declining);
        }

        private async void Start()
        {
            await Configure(Array.Empty<ApplicantData>());
        }

        /// <summary>
        /// Initializes the controller with functions for retrieving club, config, and member data, and handlers for
        /// accepting or declining joining requests.
        /// </summary>
        /// <param name="getClubData">Function to retrieve club data.</param>
        /// <param name="getConfigData">Function to retrieve configuration data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to retrieve the current player's member data.</param>
        /// <param name="tryToAcceptJoiningRequest">Handler for accepting a joining request.</param>
        /// <param name="tryToDeclineJoiningRequest">Handler for declining a joining request.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the provided functions or handlers are null.</exception>
        internal void Initialize
            (Func<FirestoreClubData> getClubData,
            Func<ConfigData> getConfigData,
            Func<MemberData> getCurrentPlayerMemberData,
            AsyncFuncHandler<bool, ApplicantData> tryToAcceptJoiningRequest,
            AsyncFuncHandler<bool, ApplicantData> tryToDeclineJoiningRequest)
        {
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.getClubData = getClubData ?? throw new ArgumentNullException(nameof(getClubData));
            this.getConfigData = getConfigData ?? throw new ArgumentNullException(nameof(getConfigData), "getConfigData function cannot be null.");
            this.tryToAcceptJoiningRequest = tryToAcceptJoiningRequest ?? throw new ArgumentNullException(nameof(ClubApplicantController.tryToAcceptJoiningRequest), "tryToSendJoiningRequest action cannot be null.");
            this.tryToDeclineJoiningRequest = tryToDeclineJoiningRequest ?? throw new ArgumentNullException(nameof(tryToDeclineJoiningRequest), "tryToDeclineJoiningRequest action cannot be null.");

            // Initialize existing search entries
            clubDetermineApplicantJoiningPopUp.Initialize
                (ref this.getCurrentPlayerMemberData,
                TryToAcceptJoiningRequest,
                TryToDeclineJoiningRequest);
        }

        /// <summary>
        /// Configures the club members list with the provided member data.
        /// </summary>
        /// <param name="applicantDatas">Array of applicant data to configure the club members list.</param>
        internal async UniTask Configure(ApplicantData[] applicantDatas)
        {
            // Check for necessary references
            if (!appicantEntryPrefab || !membersEntryParent)
            {
                Debug.LogError("Member Entry Prefab or Members Entry Parent is not assigned in the inspector.", this);
                return;
            }

            // Get the current number of entries and calculate the difference to add new ones if needed
            var difference = (applicantDatas?.Length ?? 0) - applicantEntries.Count;
            if (difference > 0)
                for (int i = 0; i < difference; i++)
                {
                    var newEntry = Instantiate(appicantEntryPrefab, membersEntryParent);
                    newEntry.Initialize
                        (dictionaryService,
                        () => getConfigData?.Invoke(),
                        () => getClubData?.Invoke(),
                        () => getCurrentPlayerMemberData?.Invoke(),
                        OnOpenApplicantJoiningPopUp_Accepting,
                        OnOpenApplicantJoiningPopUp_Declining);
                    applicantEntries.Add(newEntry);
                }

            // Configure each entry with the corresponding member data or deactivate if no data
            if (applicantEntries is not null and { Count: > 0 })
            {
                if (applicantDatas is not null)
                    await TryToCachePlayersProfileIcons(applicantDatas);

                for (int i = 0; i < applicantEntries.Count; i++)
                {
                    var dataExists = i < (applicantDatas?.Length ?? 0);
                    var applicantData = applicantDatas?.ElementAtOrDefault(i);
                    var profileSprite = gameManager.GetSprite(applicantData?.profileIconId, Consts.CollectionKeys.Icons);

                    applicantEntries[i].gameObject.SetActive(dataExists);
                    applicantEntries[i].Configure(applicantData, profileSprite, i);
                }
            }
        }

        /// <summary>
        /// Calls the action to attempt to accept a club joining request.
        /// </summary>
        /// <param name="applicantData">The data of the applicant to accept.</param>
        private async UniTask TryToAcceptJoiningRequest(FirestoreClubData.ApplicantData applicantData)
        {
            if (applicantData is null or { unityID: null or "" })
            {
                Debug.LogError("ApplicantData is null. Cannot accept new members.", this);
                return;
            }

            if (tryToAcceptJoiningRequest is null)
            {
                Debug.LogError("tryToAcceptJoiningRequest action is not assigned.", this);
                return;
            }

            // Block UI interactions while processing
            rootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var wasAcceptedSuccessfully = false;
            try
            {
                // Attempt to accept the applicant
                wasAcceptedSuccessfully = await tryToAcceptJoiningRequest(applicantData);
                if (wasAcceptedSuccessfully)
                {
                    // Reconfigure the list to reflect the removal
                    var currentApplicantDatas = ClubData?.applicants;
                    await Configure(currentApplicantDatas?.ToArray());
                } else
                    Debug.LogWarning("Applicant accepting process was not successful.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error accepting applicant: {ex.Message}", this);
            }
            finally
            {
                // Once done, re-enable UI interactions
                rootCanvasGroup?.SetActive(true);
            }
        }

        /// <summary>
        /// Calls the action to attempt to decline a club joining request.
        /// </summary>
        /// <param name="applicantData">The data of the applicant to decline.</param>
        private async UniTask TryToDeclineJoiningRequest(FirestoreClubData.ApplicantData applicantData)
        {
            if (applicantData is null or { unityID: null or "" })
            {
                Debug.LogError("ApplicantData is null. Cannot decline new members.", this);
                return;
            }

            if (tryToDeclineJoiningRequest is null)
            {
                Debug.LogError("tryToDeclineJoiningRequest action is not assigned.", this);
                return;
            }

            // Block UI interactions while processing
            rootCanvasGroup?.SetActive(false, isSettingAlpha: false);
            var wasDeclinedSuccessfully = false;
            try
            {
                // Attempt to decline the applicant
                wasDeclinedSuccessfully = await tryToDeclineJoiningRequest(applicantData);
                if (wasDeclinedSuccessfully)
                {
                    // Reconfigure the list to reflect the removal
                    var currentApplicantData = ClubData?.applicants;
                    await Configure(currentApplicantData?.ToArray());
                } else
                    Debug.LogWarning("Applicant decline process was not successful.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error decline applicant: {ex.Message}", this);
            }
            finally
            {
                // Once done, re-enable UI interactions
                rootCanvasGroup?.SetActive(true);
            }
        }

        /// <summary>
        /// Asynchronously caches profile icon sprites for players in the provided leaderboard data.
        /// </summary>
        /// <param name="applicantDatas">An array of applicant data containing profile icon information.</param>
        /// <returns>A UniTask representing the asynchronous caching operation.</returns>
        private async UniTask TryToCachePlayersProfileIcons(ApplicantData[] applicantDatas)
        {
            if (applicantDatas is null or { Length: 0 })
            {
                Debug.LogWarning("No applicant data provided for caching icons.");
                return;
            }

            var cacheIconTasks = applicantDatas
                .Where(data => data is not null && !string.IsNullOrEmpty(data.profileIconId))
                .Select(data => gameManager.GetSpriteAsync(data.profileIconId, Consts.CollectionKeys.Icons))
                .ToArray();

            await UniTask.WhenAll(cacheIconTasks);
        }

        /// <summary>
        /// Configures and shows the applicant joining pop-up.
        /// </summary>
        /// <param name="clubApplicantEntry">The club applicant entry to configure the pop-up for.</param>
        private void ConfigureApplicantJoiningPopUp(ClubApplicantEntry clubApplicantEntry, bool isAccepting)
        {
            if (clubApplicantEntry is null || clubApplicantEntry.ApplicantData is null || string.IsNullOrEmpty(clubApplicantEntry.ApplicantData.unityID))
            {
                Debug.LogError("ClubApplicantEntry or its ApplicantData is null. Cannot accept new applicants.", this);
                return;
            }

            if (!clubDetermineApplicantJoiningPopUp)
            {
                Debug.LogError("clubAcceptAplicantPopUp reference is missing. Ensure it is assigned in the inspector.", this);
                return;
            }

            clubDetermineApplicantJoiningPopUp.Configure(clubApplicantEntry, isAccepting);
            clubDetermineApplicantJoiningPopUp.Show();
        }

        /// <summary>
        /// Handles the removal of a club member.
        /// </summary>
        /// <param name="clubApplicantEntry">The club applicant entry to configure the pop-up for.</param>
        private void OnOpenApplicantJoiningPopUp_Accepting(ClubApplicantEntry clubApplicantEntry)
        {
            ConfigureApplicantJoiningPopUp(clubApplicantEntry, true);
        }

        /// <summary>
        /// Handles the removal of a club member.
        /// </summary>
        /// <param name="clubApplicantEntry">The club applicant entry to configure the pop-up for.</param>
        private void OnOpenApplicantJoiningPopUp_Declining(ClubApplicantEntry clubApplicantEntry)
        {
            ConfigureApplicantJoiningPopUp(clubApplicantEntry, false);
        }
    }
}
