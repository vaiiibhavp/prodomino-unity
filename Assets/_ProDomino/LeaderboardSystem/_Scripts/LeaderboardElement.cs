using HelperSharedLibrary;
using ProDomino.Shared;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.PlayerLeaderboardData;

namespace ProDomino.Leaderboard
{
    public class LeaderboardElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankingLabel;
        [SerializeField] private TMP_Text playerNameLabel;
        [SerializeField] private TMP_Text idLabel;

        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text tierInitialsLabel;
        [SerializeField] private TMP_Text eloLabel;
        [SerializeField] private TMP_Text victoriesLabel;
        [SerializeField] private TMP_Text losesLabel;
        [SerializeField] private TMP_Text secondPlaceLabel;
        [SerializeField] private TMP_Text thirdPlaceLabel;
        [SerializeField] private TMP_Text victoriesRateLabel;

        [SerializeField] private TMP_Text completedAchievementsLabel;

        [SerializeField] private Image playerImage;
        [SerializeField] private Image nationalityImage;
        [SerializeField] private Image[] achievementsImages;

        private Func<string, string, Sprite> getSprite;

        public PlayerLeaderboardData PlayerLeaderboardData { get; private set; }
        public LeaderboardInfo CurrentLeaderboardInfo { get; private set; }

        public void Initialize(Func<string, string, Sprite> getSprite)
        {
            this.getSprite = getSprite;
        }

        public void Configure(PlayerLeaderboardData playerLeaderboardData, string leaderboardId)
        {
            // By default, set all fields to default values to avoid leaving the UI in an inconsistent state if any of the checks fail
            SetDefaultValues();
            var errorMessage = string.Empty;

            // Check if the provided data is valid before proceeding with configuration
            if (playerLeaderboardData is null or { leaderboardInfos: null or { Count: 0 } })
                errorMessage = "LeaderboardModel or its properties are null";

            // Check if the provided leaderboard ID is valid
            if (string.IsNullOrEmpty(leaderboardId))
                errorMessage = "Leaderboard ID is null or empty";

            // Try to find the leaderboard info for the given ID in the player's leaderboard data
            PlayerLeaderboardData = playerLeaderboardData;
            CurrentLeaderboardInfo = playerLeaderboardData?.leaderboardInfos?.FirstOrDefault(x => x.leaderboardId == leaderboardId);

            // Check if we found the leaderboard info for the given ID
            if (CurrentLeaderboardInfo is null or { leaderboardData: null })
                errorMessage = $"No leaderboard info found for leaderboard ID: {leaderboardId}";

            // If there are any errors, log them and set default values to avoid leaving the UI in an inconsistent state
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError($"Cannot configure LeaderboardElement: {errorMessage}");
                return;
            }

            // Set text fields
            if (rankingLabel)
                rankingLabel.text = (CurrentLeaderboardInfo.leaderboardData.rank + 1).ToString(); // +1 because the rank is 0-based in the leaderboard system
            
            // Set player username
            if (playerNameLabel)
                playerNameLabel.text = CurrentLeaderboardInfo.leaderboardData.playerName;

            // Set the id of the player
            if (idLabel)
                idLabel.text = PlayerLeaderboardData.playerId;

            // Set the leaderboard score 
            if (scoreLabel)
                scoreLabel.text = CurrentLeaderboardInfo.leaderboardData.score.ToString();

            // Set the initials of the player leaderboard tier
            if (tierInitialsLabel)
                tierInitialsLabel.text = CurrentLeaderboardInfo.leaderboardData.tier.GetInitials();

            // Set the achievement that are already collected
            if (completedAchievementsLabel && PlayerLeaderboardData.analyticsData is not null)
            {
                completedAchievementsLabel.text = "Completed Achievements: ";

                // If the analytics data is available, show the number of achievements completed by the player
                var achievementsCompleted = PlayerLeaderboardData.analyticsData.achievementsClaimedCount;
                completedAchievementsLabel.text += $"<b>{achievementsCompleted}</b>";
            }

            // Set player image
            if (playerImage)
            {
                var profileIconId = PlayerLeaderboardData.playerProfileData?.profileIconID ?? string.Empty;

                // Get the profile icon ID from the player's profile data, which represents the specific profile icon that the player has chosen or earned.
                var profileIcon = getSprite?.Invoke
                    (profileIconId, // The profile icon ID is used as the key to retrieve the corresponding sprite from the collection
                    Consts.CollectionKeys.Icons); // The collection name is used to specify which collection of sprites to look into (in this case, the "Icons" collection)

                if (profileIcon)
                    playerImage.sprite = profileIcon;
            }

            // Set player nationality
            if (nationalityImage)
            {
                var nationalitySprite = getSprite?.Invoke(PlayerLeaderboardData.playerNationality.ToString(), Consts.CollectionKeys.Nationality);

                if (nationalitySprite)
                    nationalityImage.sprite = nationalitySprite;
            }

            // Set the ELO
            if (eloLabel && PlayerLeaderboardData.playerMatchData is not null)
                eloLabel.text = PlayerLeaderboardData.playerMatchData.elo.ToString();

            if (PlayerLeaderboardData.analyticsData is not null)
            { 
                if (victoriesLabel)
                    victoriesLabel.text = PlayerLeaderboardData.analyticsData.competitiveVictoriesCount.ToString();

                if (losesLabel)
                    losesLabel.text = PlayerLeaderboardData.analyticsData.competitiveDefeatsCount.ToString();

                if (secondPlaceLabel)
                    secondPlaceLabel.text = PlayerLeaderboardData.analyticsData.competitive2ndPositionCount.ToString();

                if (thirdPlaceLabel)
                    thirdPlaceLabel.text = PlayerLeaderboardData.analyticsData.competitive3rdPositionCount.ToString();

                if (victoriesRateLabel)
                    victoriesRateLabel.text = PlayerLeaderboardData.analyticsData.CompetitiveWLRatio.ToString("P2"); // Format as percentage with 2 decimal places
            }

            // Fill in achievements images
            if (achievementsImages is not null and { Length: > 0 } && PlayerLeaderboardData.playerProfileData is not null)
            {
                // Get the badge IDs from the player's profile data, which represent the achievements or badges that the player has earned.
                var badgeds = PlayerLeaderboardData.playerProfileData.badgesIDs;

                // Get the sprites for the badges by using the badge IDs to retrieve the corresponding sprites from the collection.
                var badgesSprites = badgeds?
                    .Select(badgeId => getSprite?.Invoke(badgeId, Consts.CollectionKeys.Achievements)) // The badge ID is used as the key to retrieve the corresponding sprite from the collection, and the collection name is used to specify which collection of sprites to look into (in this case, the "Badges" collection)
                    .Where(sprite => sprite != null) // Filter out any null sprites that couldn't be found in the collection
                    .ToList();

                if (badgesSprites is not null and { Count: > 0 })
                    for (int i = 0; i < badgesSprites.Count; i++)
                    {
                        var achievementsImage = achievementsImages.ElementAtOrDefault(i);
                        var badgeSprite = badgesSprites.ElementAtOrDefault(i);

                        // If we have both the image component and the corresponding badge sprite, set the sprite and enable the image.
                        // Otherwise, if we have the image component but no corresponding badge sprite, disable the image.
                        if (achievementsImage && badgeSprite)
                        {
                            achievementsImage.sprite = badgeSprite;
                            achievementsImage.enabled = true;
                        } 
                        else if (achievementsImage)
                            achievementsImage.enabled = false;
                    }
            }
        }

        public void SetActive(bool isActive)
        {
            if (gameObject)
                gameObject.SetActive(isActive);
        }

        private void SetDefaultValues()
        {
            if (rankingLabel)
                rankingLabel.text = "-";
            if (playerNameLabel)
                playerNameLabel.text = "-";
            if (idLabel)
                idLabel.text = "-";
            if (scoreLabel)
                scoreLabel.text = "-";
            if (eloLabel)
                eloLabel.text = "-";
            if (victoriesLabel)
                victoriesLabel.text = "-";
            if (losesLabel)
                losesLabel.text = "-";
            if (secondPlaceLabel)
                secondPlaceLabel.text = "-";
            if (thirdPlaceLabel)
                thirdPlaceLabel.text = "-";
            if (victoriesRateLabel)
                victoriesRateLabel.text = "-";
            if (completedAchievementsLabel)
                completedAchievementsLabel.text = "Completed Achievements: -";
            if (playerImage)
                playerImage.sprite = null;
            if (nationalityImage)
                nationalityImage.sprite = null;
        }
    }
}
