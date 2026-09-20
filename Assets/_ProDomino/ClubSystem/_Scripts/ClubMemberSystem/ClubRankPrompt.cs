using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timba.Utils;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Displays and manages a prompt for changing a club member's rank, including selection, confirmation, and
    /// visibility of available ranks.
    /// </summary>
    public class ClubRankPrompt : Tooltip
    {
        [Header("Rank Prompt Fields")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private ClubRankPrompEntry entryPrefab;
        [SerializeField] private Transform rankingsParent;
        [SerializeField] private CustomButtonUI confirmChngeButton;

        private List<ClubRankPrompEntry> rankEntries;
        private Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData;
        private AsyncActionHandler<ClubRanksTypes, FirestoreClubData.MemberData> tryToOverrideRank;

        internal ClubMemberEntry SelectedMemberEntry { get; private set; }
        internal ClubRankPrompEntry SelectedRankEntry { get; private set; }
        internal int AvailableRanksCount => rankEntries?.Count(x => x.gameObject.activeSelf) ?? 0;

        private void Awake()
        {
            rankEntries = rankingsParent?.GetComponentsInChildren<ClubRankPrompEntry>(true)?.ToList() ?? new();

            if (confirmChngeButton)
                confirmChngeButton.onClick.AddListener(TryToOverrideRank);
            else
                Debug.LogError("Confirm Change Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Initialize the rank prompt with the dictionary service and a callback for rank selection.
        /// </summary>
        /// <param name="dictionaryService">The dictionary service.</param>
        /// <param name="getCurrentPlayerMemberData">Function to get the current player's member data.</param>
        /// <param name="tryToOverrideRank">Async action handler to attempt to override the rank.</param>
        internal void Initialize
            (DictionaryService dictionaryService, 
            ref Func<FirestoreClubData.MemberData> getCurrentPlayerMemberData,
            AsyncActionHandler<ClubRanksTypes, FirestoreClubData.MemberData> tryToOverrideRank)
        {
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData ?? throw new ArgumentNullException(nameof(getCurrentPlayerMemberData));
            this.tryToOverrideRank = tryToOverrideRank ?? throw new ArgumentNullException(nameof(tryToOverrideRank));
            var rankTypes = Enum.GetValues(typeof(ClubRanksTypes));

            // Ensure we have the correct number of rank entries based on the number of rank types
            var difference = rankTypes.Length - rankEntries.Count;
            if (difference > 0)
                for (int i = 0; i < difference; i++)
                {
                    var rankEntry = Instantiate(entryPrefab, rankingsParent);
                    rankEntries.Add(rankEntry);
                }

            else if (difference < 0)
                for (int i = 0; i < Math.Abs(difference); i++)
                {
                    var rankToRemove = rankEntries.Last();
                    if (rankToRemove != null && rankToRemove.gameObject != null)
                        Destroy(rankToRemove.gameObject);

                    rankEntries.Remove(rankToRemove);
                }

            // Initialize each rank entry with the corresponding rank type, name, and sprite
            for (int i = 0; i < rankTypes.Length; i++)
            {
                var rankType = (ClubRanksTypes)rankTypes.GetValue(i);
                var rankEntry = rankEntries.ElementAtOrDefault(i);

                if (rankEntry == null)
                {
                    Debug.LogError($"Rank entry at index {i} is null. Skipping initialization for rank type {rankType}.");
                    break;
                }

                var rankName = Regex.Replace(rankType.ToString(), "(?<!^)(?=[A-Z][a-z])", " ");
                var rankSprite = dictionaryService.GetSprite($"{Consts.CollectionKeys.Club}_Ranks", rankType.ToString());

                rankEntry.Initialize(rankSprite, rankName, rankType, SelectRankEntry);
                
                rankEntries.Add(rankEntry);
            }

            // Set up the confirm change button to close the prompt
            rankEntries = rankEntries.OrderByDescending(e => e.RankType).ToList();

            // Reparent entries to reflect the new order in the hierarchy
            for (int i = 0; i < rankEntries.Count; i++)
                rankEntries[i].transform.SetSiblingIndex(i);
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

            // Set the currently selected member entry
            SelectedMemberEntry = clubMemberEntry;

            // Reset all rank entries
            ForceSelection(clubMemberEntry.MemberData.rank, isCallingCallback: true);

            // Set the visibility of rank prompt entries based on the current selected member's rank
            SetRankPrompEntriesVisibility();
        }

        /// <summary>
        /// Handles the selection of a rank entry.
        /// </summary>
        /// <param name="rankEntry">The rank entry that was selected.</param>
        private void SelectRankEntry(ClubRankPrompEntry rankEntry)
        {
            if (rankEntry is null)
            {
                Debug.LogError("Selected rank entry is null. Cannot proceed with rank selection.");
                return;
            }

            SelectedRankEntry = rankEntry;
        }

        /// <summary>
        /// Callback to attempt to override the rank of the currently selected member.
        /// </summary>
        private async void TryToOverrideRank()
        {
            // Validate the callback
            if (tryToOverrideRank is null)
            { 
                Debug.LogError("TryToOverrideRank callback is not set. Please initialize the ClubRankPrompt with a valid callback.");
                return;
            }

            // Validate the selected rank entry
            if (SelectedRankEntry == null || SelectedRankEntry.RankType is null)
            {
                Debug.LogError("No rank entry is selected or the selected rank entry's RankType is null. Cannot override rank.");
                return;
            }

            // Validate the selected member entry
            if (SelectedMemberEntry == null || SelectedMemberEntry.MemberData is  null)
            {
                Debug.LogError("CurrentSelectedMemberEntry or its MemberData is null. Cannot override rank.");
                return;
            }

            // Check if the selected rank is different from the member's current rank
            if (SelectedMemberEntry.MemberData.rank == SelectedRankEntry.RankType.Value)
            {
                Debug.LogWarning("The selected rank is the same as the member's current rank. No changes will be made.");
                return;
            }

            // Block the UI while processing
            rootCanvasGroup.SetActive(false, isSettingAlpha: false);
            try
            {
                await tryToOverrideRank(SelectedRankEntry.RankType.Value, SelectedMemberEntry.MemberData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred while trying to override the rank: {ex.Message}", this);

                // Revert the selection to the original rank in case of an error
                ForceSelection(SelectedMemberEntry.MemberData.rank, true);

                // Unblock the UI in case of an error
                rootCanvasGroup.SetActive(true, isSettingAlpha: false);
            }
            finally
            {
                // Turn off the UI after the operation is complete
                rootCanvasGroup.SetActive(false);
            }
        }

        /// <summary>
        /// Forces the selection of this rank entry.
        /// </summary>
        /// <param name="clubRanksType">The type of the club rank to select.</param>
        /// <param name="isCallingCallback">Indicates whether to call the selection callback.</param>
        internal void ForceSelection(ClubRanksTypes clubRanksType, bool isCallingCallback = false)
        {
            if (rankEntries is null || rankEntries.Count == 0)
            {
                Debug.LogError("Rank entries are not initialized.");
                return;
            }

            var entry = rankEntries.FirstOrDefault(e => e.RankType == clubRanksType);
            if (entry != null)
                entry.ForceSelection(isCallingCallback);
            else
                Debug.LogError($"No rank entry found for rank type: {clubRanksType}");
        }

        /// <summary>
        /// Sets the visibility of rank prompt entries based on the current selected member's rank.
        /// </summary>
        private void SetRankPrompEntriesVisibility()
        {
            var currentMemberData = getCurrentPlayerMemberData?.Invoke();
            if (currentMemberData is null)
            {
                Debug.LogError("current Member Data is null. Cannot set rank prompt entries visibility.");
                return;
            }

            if (rankEntries is null or { Count: 0 })
            {
                Debug.LogError("Rank entries are not initialized.");
                return;
            }

            // Only show entries for ranks lower than the current member's rank
            foreach (var rankEntry in rankEntries)
                rankEntry.gameObject.SetActive(rankEntry.RankType < currentMemberData.rank);
        }
    }
}
