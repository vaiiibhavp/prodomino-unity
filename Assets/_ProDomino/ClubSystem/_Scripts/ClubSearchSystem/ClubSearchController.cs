using Cysharp.Threading.Tasks;
using HelperSharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages club search functionality, including searching for clubs, displaying search results and leaderboards,
    /// handling club joining requests, and opening club creation settings.
    /// </summary>
    internal class ClubSearchController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvas;
        [SerializeField] private TMP_InputField searchInputField;
        [SerializeField] private GameObject noClubsFoundLabel;
        [SerializeField] private Transform searchClubParent;
        [SerializeField] private Transform leaderboardClubParent;
        [SerializeField] private CustomButtonUI createClubButton;
        [SerializeField] private CustomButtonUI searchClubButton;
        [SerializeField] private ClubConfirmSendRequestPopUp clubSendRequestPopUp;

        [Header("Prefabs")]
        [SerializeField] private ClubSearchEntry clubSearchEntryPrefab;
        [SerializeField] private ClubSearchLeaderboardEntry clubSearchLeaderboardEntryPrefab;

        private AsyncFuncHandler<FirestoreClubData[], string> tryToSearchClubsByName;
        private AsyncFuncHandler<bool, FirestoreClubData> tryToSendJoiningRequest;
        private AsyncFuncHandler<bool, FirestoreClubData> tryToConfirmSendRequest;
        private Func<ConfigData> getConfigData;
        private Action openClubDataSettings;

        private List<ClubSearchEntry> searchEntries;
        private List<ClubSearchLeaderboardEntry> leaderboardEntries;

        private void Awake()
        {
            if (createClubButton)
                createClubButton.onClick.AddListener(OnCreateClubButtonClicked);
            else
                Debug.LogError("Create Club Button is not assigned in the inspector.", this);

            if (searchClubButton)
                searchClubButton.onClick.AddListener(OnSearchClubButtonClicked);
            else
                Debug.LogError("Search Club Button is not assigned in the inspector.", this);

            searchEntries = searchClubParent.GetComponentsInChildren<ClubSearchEntry>(true).ToList() ?? new();
            leaderboardEntries = leaderboardClubParent.GetComponentsInChildren<ClubSearchLeaderboardEntry>(true).ToList() ?? new();
        }

        private void Start()
        {
            ConfigureSearchEntries(Array.Empty<FirestoreClubData>());
            ConfigureLeaderboardEntries(Array.Empty<FirestoreClubData>());
        }

        private void Update()
        {
            if (searchInputField == null)
                return;

            // Enable or disable the search button based on whether there is text in the input field
            if (searchClubButton != null)
                searchClubButton.SetButtonInteractable(!string.IsNullOrWhiteSpace(searchInputField.text));
        }

        /// <summary>
        /// Initializes club-related functionality by assigning handlers for searching clubs, sending joining requests,
        /// retrieving configuration data, and opening club data settings.
        /// </summary>
        /// <param name="tryToSearchClubsByName">Handler for searching clubs by name.</param>
        /// <param name="tryToSendJoiningRequest">Handler for sending club joining requests.</param>
        /// <param name="getConfigData">Function to retrieve configuration data.</param>
        /// <param name="openClubDataSettings">Action to open club data settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the provided handlers or functions are null.</exception>
        internal void Initialize
            (AsyncFuncHandler<FirestoreClubData[], string> tryToSearchClubsByName,
            AsyncFuncHandler<bool, FirestoreClubData> tryToSendJoiningRequest,
            Func<ConfigData> getConfigData,
            Action openClubDataSettings)
        {
            this.tryToSearchClubsByName = tryToSearchClubsByName ?? throw new ArgumentNullException(nameof(tryToSearchClubsByName), "tryToSearchClubsByName function cannot be null.");
            this.tryToSendJoiningRequest = tryToSendJoiningRequest ?? throw new ArgumentNullException(nameof(tryToSendJoiningRequest), "tryToSendJoiningRequest action cannot be null.");
            this.tryToConfirmSendRequest = tryToSendJoiningRequest ?? throw new ArgumentNullException(nameof(tryToSendJoiningRequest), "tryToSendJoiningRequest action cannot be null.");
            this.getConfigData = getConfigData ?? throw new ArgumentNullException(nameof(getConfigData), "getConfigData function cannot be null.");
            this.openClubDataSettings = openClubDataSettings ?? throw new ArgumentNullException(nameof(openClubDataSettings), "openClubDataSettings action cannot be null.");

            // Initialize the Club Send Request Pop-Up
            if (clubSendRequestPopUp)
                clubSendRequestPopUp.Initialize(TryToConfirmSendRequest);
            else
                Debug.LogWarning("ClubRemovePopUp reference is missing. Ensure it is assigned in the inspector.", this);

            // Initialize existing search entries
            foreach (var entry in searchEntries)
                entry.Initialize(OpenConfirmationSendRequest, this.getConfigData);

            foreach (var entry in leaderboardEntries)
                entry.Initialize(OpenConfirmationSendRequest, this.getConfigData);
        }

        /// <summary>
        /// Configures the search results with the provided club data array.
        /// </summary>
        /// <param name="searchClubDatas">Array of club data to configure the search entries with.</param>
        internal void ConfigureSearchEntries(FirestoreClubData[] searchClubDatas)
        {
            // Configure search entries
            if (searchClubDatas != null && searchClubDatas.Length > 0)
            {
                // Ensure there are enough search entries
                while (searchEntries.Count < searchClubDatas.Length)
                {
                    var newEntry = Instantiate(clubSearchEntryPrefab, searchClubParent);
                    newEntry.Initialize(OpenConfirmationSendRequest, getConfigData);
                    searchEntries.Add(newEntry);
                }

                // Configure and show the required number of entries
                for (int i = 0; i < searchClubDatas.Length; i++)
                {
                    searchEntries[i].gameObject.SetActive(true);
                    searchEntries[i].Configure(searchClubDatas[i]);
                }

                // Hide any extra entries
                for (int i = searchClubDatas.Length; i < searchEntries.Count; i++)
                    searchEntries[i].gameObject.SetActive(false);
            } 
            
            else
                // Hide all entries if no data is provided
                foreach (var entry in searchEntries)
                    entry.gameObject.SetActive(false);

            // Show or hide the "No Clubs Found" label based on search results
            if (noClubsFoundLabel)
                noClubsFoundLabel.SetActive(searchClubDatas is null or { Length: 0 });
            else
                Debug.LogWarning("No Clubs Found Label is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Configures the leaderboard with the provided club data array.
        /// </summary>
        /// <param name="leaderboardClubDatas">Array of club data to configure the leaderboard entries with.</param>
        internal void ConfigureLeaderboardEntries(FirestoreClubData[] leaderboardClubDatas)
        {
            // Configure leaderboard entries
            if (leaderboardClubDatas != null && leaderboardClubDatas.Length > 0)
            {
                // Ensure there are enough leaderboard entries
                while (leaderboardEntries.Count < leaderboardClubDatas.Length)
                {
                    var newEntry = Instantiate(clubSearchLeaderboardEntryPrefab, leaderboardClubParent);
                    newEntry.Initialize(OpenConfirmationSendRequest, getConfigData);
                    leaderboardEntries.Add(newEntry);
                }

                // Configure and show the required number of entries
                for (int i = 0; i < leaderboardClubDatas.Length; i++)
                {
                    leaderboardEntries[i].gameObject.SetActive(true);
                    leaderboardEntries[i].Configure(leaderboardClubDatas[i]);
                }

                // Hide any extra entries
                for (int i = leaderboardClubDatas.Length; i < leaderboardEntries.Count; i++)
                    leaderboardEntries[i].gameObject.SetActive(false);
            } 
            
            else
                // Hide all entries if no data is provided
                foreach (var entry in leaderboardEntries)
                    entry.gameObject.SetActive(false);

            // Sort leaderboard entries by club rank
            leaderboardEntries.Sort((a, b) => a.ClubData?.clubRank.CompareTo(b.ClubData.clubRank) ?? 0);
            foreach (var entry in leaderboardEntries)
                entry.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Handles the Search Club button click event.
        /// </summary>
        private async void OnSearchClubButtonClicked()
        {
            // Validate the search input field
            if (searchInputField == null)
            {
                Debug.LogWarning("Search Input Field is not assigned in the inspector.", this);
                return;
            }

            // Validate the search input
            if (string.IsNullOrWhiteSpace(searchInputField.text))
            {
                Debug.LogWarning("Search input field is empty. Please enter a club name to search.", this);
                return;
            }

            // Ensure the search function is assigned
            if (tryToSearchClubsByName == null)
            {
                Debug.LogError("tryToSearchClubsByName function is not assigned.", this);
                return;
            }

            rootCanvas?.SetActive(false, isSettingAlpha: false);
            try
            {
                var clubsFound = await tryToSearchClubsByName(searchInputField.text);
                if (clubsFound is not null and { Length: > 0 })
                {
                    Debug.Log($"Found {clubsFound.Length} clubs matching the search criteria.", this);
                    ConfigureSearchEntries(clubsFound);
                } 
                else
                {
                    Debug.Log("No clubs found matching the search criteria.", this);
                    ConfigureSearchEntries(Array.Empty<FirestoreClubData>());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while searching for clubs: {ex.Message}", this);
            }
            finally
            {
                rootCanvas?.SetActive(true);
            }
        }

        /// <summary>
        /// Opens the confirmation pop-up to send a joining request to the specified club.
        /// </summary>
        /// <param name="firestoreClubData">The club data for which to send the joining request.</param>
        private void OpenConfirmationSendRequest(FirestoreClubData firestoreClubData)
        {
            if (firestoreClubData is null)
            {
                Debug.LogWarning("Cannot send joining request. FirestoreClubData is null.", this);
                return;
            }

            clubSendRequestPopUp.Configure(firestoreClubData);
            clubSendRequestPopUp.Show();
        }

        /// <summary>
        /// Attempts to confirm sending a joining request to the specified club.
        /// </summary>
        /// <param name="firestoreClubData">The club data for which to send the joining request.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the request was successfully sent.</returns>
        private async UniTask<bool> TryToConfirmSendRequest(FirestoreClubData firestoreClubData)
        {
            if (firestoreClubData is null)
            {
                Debug.LogWarning("Cannot send joining request. FirestoreClubData is null.", this);
                return false;
            }

            if (tryToConfirmSendRequest is null)
            {
                Debug.LogError("TryToConfirmSendRequest function is not assigned.", this);
                return false;
            }


            rootCanvas?.SetActive(false, isSettingAlpha: false);

            try
            {
                Debug.Log($"Attempting to send confirm request to club: {firestoreClubData.clubName}", this);
                return await tryToConfirmSendRequest(firestoreClubData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while sending joining request: {ex.Message}", this);
                return false;
            }
            finally
            {
                rootCanvas?.SetActive(true);
            }
        }

        /// <summary>
        /// Handles the Create Club button click event.
        /// </summary>
        private void OnCreateClubButtonClicked()
        {
            if (openClubDataSettings != null)
                openClubDataSettings.Invoke();
            else
                Debug.LogWarning("OpenClubDataSettings action is not assigned.", this);
        }

        /// <summary>
        /// Handles actions to perform when the controller is opened.
        /// </summary>
        internal void OnOpenController()
        {
            ConfigureLeaderboardEntries(Array.Empty<FirestoreClubData>());
        }
    }
}
