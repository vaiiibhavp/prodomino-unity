using HelperSharedLibrary;
using TMPro;
using UnityEngine;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a leaderboard entry for a club in the search results, displaying the club's ranking.
    /// </summary>
    internal class ClubSearchLeaderboardEntry : ClubSearchEntry
    {
        [SerializeField] protected TMP_Text clubRankingLabel;

        /// <summary>
        /// Configures the club search entry with the provided club data.
        /// </summary>
        /// <param name="clubData">The club data to configure the entry with.</param>
        internal override void Configure(FirestoreClubData clubData)
        {
            base.Configure(clubData);

            // Set the club slogan label
            if (clubRankingLabel)
                clubRankingLabel.text = ClubData.clubRank.ToString();
            else
                Debug.LogWarning("Club Slogan Label is not assigned in the inspector.", this);
        }
    }
}
