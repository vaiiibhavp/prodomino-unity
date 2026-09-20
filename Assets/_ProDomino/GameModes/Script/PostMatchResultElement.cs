using ProDomino.Shared;
using System;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static ProDomino.GameModes.PostMatchResultController;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Represents a UI element for displaying a player's post-match result, including player and team information,
    /// position, scores, and friend request functionality.
    /// </summary>
    public class PostMatchResultElement : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button addFriendButton;
        [SerializeField] private Color firstPositionColor = Color.black;

        [Space(10), Header("Player")]
        [SerializeField] private Image playerIconImage;
        [SerializeField] private TMP_Text playerUsernameLabel;
        [SerializeField] private TMP_Text playerUIDLabel;

        [Space(10), Header("Team")]
        [SerializeField] private Image teamIconImage;
        [SerializeField] private TMP_Text teamNameLabel;
        [SerializeField] private TMP_Text teamPointsLabel;

        [Space(15)]
        [SerializeField] private CanvasGroup firstPositionCanvasGroup;
        [SerializeField] private TMP_Text positionText;
        [SerializeField] private TMP_Text lastRoundScoreText;
        [SerializeField] private TMP_Text rankedPointsText;

        private Color? defaultPositionColor;
        private Func<string> getCurrentPlayerID;
        private AsyncFuncHandler<bool, string> checkFriend;

        public MatchResult MatchResult { get; private set; }

        private void Awake()
        {
            if (positionText)
                defaultPositionColor = positionText.color;

            if (addFriendButton)
                addFriendButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets the function used to retrieve the current player ID.
        /// </summary>
        /// <param name="getCurrentPlayerID">A delegate that returns the current player ID as a string.</param>
        public void Initialize
            (Func<string> getCurrentPlayerID)
        { 
            this.getCurrentPlayerID = getCurrentPlayerID;
        }

        /// <summary>
        /// Initializes friend-related functionality, setting up handlers and event listeners for friend requests.
        /// </summary>
        /// <param name="checkFriend">Asynchronous handler to check friend status.</param>
        /// <param name="onAddFriend">Action invoked when adding a friend.</param>
        /// <param name="onBeforeSendFriendRequest">Event triggered before sending a friend request.</param>
        /// <param name="onFriendRequestSent">Event triggered after a friend request is sent.</param>
        public void FriendInitialize
            (AsyncFuncHandler<bool, string> checkFriend,
            UnityAction<string, string> onAddFriend,
            ref UnityEvent<string> onBeforeSendFriendRequest,
            ref UnityEvent<string> onFriendRequestSent)
        { 
            this.checkFriend = checkFriend;

            // Set the button listener for adding a friend
            if (addFriendButton && onAddFriend != null)
                addFriendButton.onClick.AddListener(() => onAddFriend?.Invoke(MatchResult.PlayerUsername, MatchResult.PlayerUID));

            if (onBeforeSendFriendRequest is not null)
                onBeforeSendFriendRequest.AddListener(OnBeforeSendFriendRequest);

            if (onFriendRequestSent is not null)
                onFriendRequestSent.AddListener(OnFriendRequestSent);
        }

        /// <summary>
        /// Disables the add friend button before sending a friend request.
        /// </summary>
        /// <param name="playerID">The ID of the player to whom the friend request will be sent.</param>
        private void OnBeforeSendFriendRequest(string playerID)
        {

            if (!addFriendButton)
            {
                Debug.LogWarning("Couldn't block interactity of add friend button because the one is null");
                return;
            }

            addFriendButton.interactable = false;
        }

        /// <summary>
        /// Re-enables the add friend button after a friend request is sent.
        /// </summary>
        /// <param name="playerID">The ID of the player to whom the friend request was sent.</param>
        private void OnFriendRequestSent(string playerID)
        {
            if (!addFriendButton)
            {
                Debug.LogWarning("Couldn't activate interactity of add friend button because the one is null");
                return;
            }

            addFriendButton.interactable = true;
        }

        /// <summary>
        /// Configures the UI elements to display player and match information based on the provided game and match
        /// parameters.
        /// </summary>
        /// <param name="gameType">The type of game being played.</param>
        /// <param name="numberPlayers">The number of players or teams in the match.</param>
        /// <param name="matchResult">The result data for the player or team in the match.</param>
        /// <param name="limitPoints">The maximum points limit for the match.</param>
        /// <param name="isGameOver">Indicates whether the game is over.</param>
        /// <param name="isTeamA">Indicates if the player or team is part of Team A; null if not a team game.</param>
        /// <param name="teamRoundPoints">The points scored by the team in the current round, if applicable.</param>
        /// <param name="teamAccumulatedPoints">The total points accumulated by the team, if applicable.</param>
        public async void Configure(GameType gameType, NumberPlayers numberPlayers, MatchResult matchResult, int limitPoints, bool isGameOver,
            bool? isTeamA = null, int? teamRoundPoints = null, int? teamAccumulatedPoints = null)
        {
            MatchResult = matchResult;
            if (MatchResult is null)
            {
                Debug.LogWarning("Match result is null");
                return;
            }

            // Set the player icon and text labels
            if (playerIconImage)
                playerIconImage.sprite = matchResult.PlayerIcon;
            if (playerUsernameLabel)
                playerUsernameLabel.text = matchResult.PlayerUsername;
            if (playerUIDLabel)
                playerUIDLabel.text = matchResult.PlayerUID;

            // Set the text for each category based on the match result
            if (positionText)
            { 
                positionText.text = $"{matchResult.Position}{GetOrdinalSuffix(matchResult.Position)}";
                positionText.color = matchResult.Position == 1 ? firstPositionColor : defaultPositionColor ?? Color.white;
            }
            if (lastRoundScoreText)
                lastRoundScoreText.text = matchResult.LastRoundScore.ToString();

            if (rankedPointsText)
            {
                var pointsToDisplay = numberPlayers is NumberPlayers.twoVsTwo ? teamAccumulatedPoints : matchResult.RankedPoints;
                if (limitPoints != 0)
                    rankedPointsText.text = pointsToDisplay.ToString() + "/" + limitPoints;
                else
                    rankedPointsText.text = pointsToDisplay.ToString();
            }

            // Set the first position canvas group visibility
            if (firstPositionCanvasGroup)
                firstPositionCanvasGroup.alpha = matchResult.Position is 1 ? 1f : 0f;

            // Set the visibility of the first position canvas group
            var isFriend = true;
            if (checkFriend != null)
                isFriend = await checkFriend(MatchResult.PlayerUID);

            var isCurrentPlayer = getCurrentPlayerID != null && MatchResult.PlayerUID == getCurrentPlayerID.Invoke();
            if (addFriendButton)
                addFriendButton.gameObject.SetActive(gameType is GameType.casual or GameType.competitive && !isCurrentPlayer && !isFriend);

            // Set team info if it's a team game
            if (teamIconImage)
            {
                // Show team info only if isTeamA has a value (indicating it's a team game)
                var shouldShowTeamInfo = isTeamA.HasValue;
                teamIconImage.gameObject.SetActive(shouldShowTeamInfo);

                // Set team icon color based on the team (Team A or Team B) if it's a team game
                if (shouldShowTeamInfo)
                    teamIconImage.color = isTeamA.Value ? Consts.Colors.TeamAColorValue : Consts.Colors.TeamBColorValue;
            }

            // Set team points text if it's a team game and team points are provided
            if (teamNameLabel)
            {
                // Show team name only if isTeamA has a value (indicating it's a team game)
                var shouldShowTeamName = isTeamA.HasValue;
                teamNameLabel.gameObject.SetActive(shouldShowTeamName);

                // Set team name text based on the team (Team A or Team B) if it's a team game
                if (shouldShowTeamName)
                    teamNameLabel.text = isTeamA.Value ? "A" : "B";
            }

            // Set team points text if it's a team game and team points are provided
            if (teamPointsLabel)
            {
                // Show team points only if isTeamA has a value (indicating it's a team game) and teamPoints has a value (indicating points are provided)
                var shouldShowTeamPoints = isTeamA.HasValue && teamRoundPoints.HasValue;
                teamPointsLabel.gameObject.SetActive(shouldShowTeamPoints);

                // Set team points text based on the provided team points if it's a team game and points are provided
                if (shouldShowTeamPoints)
                    teamPointsLabel.text = teamRoundPoints.Value.ToString();
            }


            string GetOrdinalSuffix(uint number)
            {
                return number switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th"
                };
            }
        }

        /// <summary>
        /// Sets the visibility of the element by enabling or disabling the associated CanvasGroup.
        /// </summary>
        /// <param name="isVisible">True to make the element visible; false to hide it.</param>
        public void SetElementVisibility(bool isVisible)
        { 
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned in PostMatchResultElement.");
                return;
            }

            // Set the visibility of the element
            canvasGroup.SetActive(isVisible);
        }
    }
}
