using ProDomino.Shared;
using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AccountSystem
{
    /// <summary>
    /// Represents a leaderboard entry for an account, displaying game mode and leaderboard scores and tiers.
    /// </summary>
    internal class LeaderboardAccountEntry : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text
            gameModeLabel,
            oneVsOneLabel,
            oneVsThreeLabel;

        internal bool WasConfiguredProperly { get; set; }

        /// <summary>
        /// Configures the display labels for the specified game mode and leaderboard scores and tiers.
        /// </summary>
        /// <param name="gameMode">The game mode to display.</param>
        /// <param name="oneVsOneHigherScore">The highest score for the 1v1 leaderboard, if available.</param>
        /// <param name="oneVsThreeHigherScore">The highest score for the 1v3 leaderboard, if available.</param>
        /// <param name="oneVsOneHigherTier">The highest tier for the 1v1 leaderboard, if available.</param>
        /// <param name="oneVsThreeHigherTier">The highest tier for the 1v3 leaderboard, if available.</param>
        internal void Configure
            (GameMode gameMode,
            double? oneVsOneHigherScore = null, double? oneVsThreeHigherScore = null,
            LeaderboardTier? oneVsOneHigherTier = null, LeaderboardTier? oneVsThreeHigherTier = null)
        {
            if (gameModeLabel)
                gameModeLabel.text = gameMode.ToString();
            else
                Debug.LogError($"Missing reference: {nameof(gameModeLabel)}");

            var spacingFunc = new Func<string, string>(input => Regex.Replace(input, "(?<!^)([A-Z])", " $1"));
            if (oneVsOneLabel)
                oneVsOneLabel.text = oneVsOneHigherTier.HasValue && oneVsOneHigherScore.HasValue 
                    ? $"{spacingFunc(oneVsOneHigherTier.ToString())} ({oneVsOneHigherScore})"
                    : "No Record";
            else
                Debug.LogError($"Missing reference: {nameof(oneVsOneLabel)}");
            
            if (oneVsThreeLabel)
                oneVsThreeLabel.text = oneVsOneHigherTier.HasValue && oneVsOneHigherScore.HasValue
                    ? $"{spacingFunc(oneVsThreeHigherTier.ToString())} ({oneVsThreeHigherScore})"
                    : "No Record";
            else
                Debug.LogError($"Missing reference: {nameof(oneVsThreeLabel)}");

            WasConfiguredProperly = true;
        }
    }
}
