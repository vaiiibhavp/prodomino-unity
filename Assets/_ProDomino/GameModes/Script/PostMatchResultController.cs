using Cysharp.Threading.Tasks;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.Shared;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Manages the post-match result UI, including configuration, button listeners, and visibility for both single and
    /// team game modes.
    /// </summary>
    public class PostMatchResultController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Space(10), Header("PostMatchResultUIs")]
        [SerializeField] private PostMatchResultUI singlePostMatchResultUI;
        [SerializeField] private PostMatchResultUI teamPostMatchResultUI;


        [Space(10), Header("Colors")]
        [SerializeField] private Color wonColor = Color.green;
        [SerializeField] private Color drawColor = Color.yellow;
        [SerializeField] private Color lostColor = Color.red;

        [Space(10), Header("Events")]
        [SerializeField] private UnityEvent onNextRound;
        [SerializeField] private UnityEvent onNewGame;
        [SerializeField] private UnityEvent onRematch;
        [SerializeField] private UnityEvent onSaveReview;

        private NumberPlayers currentNumberPlayers;
        private AuthManager authManager;
        private FriendManager friendManager;
        private DictionaryService dictionaryService;
        private Sprite defaultGameModeIcon;

        // We use properties to get the references of the UI elements based on the current number of players, to manage independently the single and team match result UIs,
        // which can have different configurations and behaviors
        #region PostMatch Result UI Elements References
        private PostMatchResultElement PostMatchResultElementPrefab => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.PostMatchResultElementPrefab,
            _ => singlePostMatchResultUI?.PostMatchResultElementPrefab,
        };
        private Transform PostMatchResultParent => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.PostMatchResultParent,
            _ => singlePostMatchResultUI?.PostMatchResultParent,
        };
        
        private Image GameModeIcon => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.GameModeIcon,
            _ => singlePostMatchResultUI?.GameModeIcon,
        };
        private GameObject ButtonContainerBottom => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.ButtonContainerBottom,
            _ => singlePostMatchResultUI?.ButtonContainerBottom,
        };
       
        private Button NextRoundButton => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.NextRoundButton,
            _ => singlePostMatchResultUI?.NextRoundButton,
        };
        private Button ToLobbyButton => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.ToLobbyButton,
            _ => singlePostMatchResultUI?.ToLobbyButton,
        };
        private Button RematchButton => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.RematchButton,
            _ => singlePostMatchResultUI?.RematchButton,
        };
        private Button SaveReviewButton => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.SaveReviewButton,
            _ => singlePostMatchResultUI?.SaveReviewButton,
        };

        private TMP_Text NextRoundButtonText => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.NextRoundButtonText,
            _ => singlePostMatchResultUI?.NextRoundButtonText,
        };
        private TMP_Text RematchButtonText => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.RematchButtonText,
            _ => singlePostMatchResultUI?.RematchButtonText,
        };
        private TMP_Text GameModeText => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.GameModeText,
            _ => singlePostMatchResultUI?.GameModeText,
        };
        private TMP_Text HeaderText => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.HeaderText,
            _ => singlePostMatchResultUI?.HeaderText,
        };
        private TMP_Text PlayerResultStateText => currentNumberPlayers switch
        {
            NumberPlayers.twoVsTwo => teamPostMatchResultUI?.PlayerResultStateText,
            _ => singlePostMatchResultUI?.PlayerResultStateText,
        };
        #endregion

        public MatchResult[] LastMatchResults { get; private set; }

        private async void Start()
        {
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            friendManager = ServiceLocator.Instance.GetService<FriendManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            await UniTask.WaitUntil(() => authManager != null && authManager.IsAlreadyInitialized);

            // Search for all PostMatchResultElement components in children and initialize them
            // We do this in the start to ensure that if there are any PostMatchResultElement already in the scene (for example assigned in the editor)
            // they get initialized with the correct categories and friend system info from the beginning
            // This will get each element of both lists, but since they are separated in the hierarchy by singlePostMatchResultParent and teamPostMatchResultParent,
            // we can manage them independently afterwards if needed
            var postMatchResultElementList = new List<PostMatchResultElement>(GetComponentsInChildren<PostMatchResultElement>(true));
            foreach (var element in postMatchResultElementList)
            {
                element.Initialize(() => authManager.UUID);
                element.FriendInitialize
                    (friendManager.IsFriendOrRequested,
                    friendManager.OpenConfirmFrienshipPopUp,
                    ref friendManager.OnBeforeSendFriendRequest,
                    ref friendManager.OnFriendRequestSent);

                element.SetElementVisibility(false);
            }

            // Set the default game mode icon if not assigned
            if (GameModeIcon && defaultGameModeIcon == null)
                defaultGameModeIcon = GameModeIcon.sprite;

            // By default, hide the controller
            SetVisibility(false);
        }

        /// <summary>
        /// Configures the post-match result UI elements based on the provided game mode, type, player count, match
        /// results, winner, round, and game state.
        /// </summary>
        /// <param name="gameMode">Specifies the mode of the game being played.</param>
        /// <param name="gameType">Specifies the type of the game.</param>
        /// <param name="numberPlayers">Indicates the number of players in the match.</param>
        /// <param name="matchResults">Array containing the results for each match participant.</param>
        /// <param name="limitPoints">The point limit for the match.</param>
        /// <param name="playerWinner">Identifies the winner of the match or round.</param>
        /// <param name="currentRound">The current round number.</param>
        /// <param name="isGameOver">Indicates whether the game has ended.</param>
        public void Configure(
            GameMode gameMode, GameType gameType, NumberPlayers numberPlayers, 
            MatchResult[] matchResults, 
            int limitPoints, string playerWinner, int currentRound,
            bool isGameOver = true, 
            int teamARoundPoints = 0,
            int teamBRoundPoints = 0,
            int teamAAccumulatedPoints = 0,
            int teamBAccumulatedPoints = 0)
        {
            LastMatchResults = matchResults;
            currentNumberPlayers = numberPlayers;

            var shouldShowTeamUI = numberPlayers is NumberPlayers.twoVsTwo;
            // Set the visibility of the single and team post match result UIs based on the number of players
            if (singlePostMatchResultUI)
                singlePostMatchResultUI.CanvasGroup.SetActive(!shouldShowTeamUI);

            if (teamPostMatchResultUI)
                teamPostMatchResultUI.CanvasGroup.SetActive(shouldShowTeamUI);

            var postMatchResultElementList = PostMatchResultParent?.transform.GetComponentsInChildren<PostMatchResultElement>(true)?.ToList() ?? new List<PostMatchResultElement>();

            if (PostMatchResultElementPrefab != null)
            {
                // Instantiate the lefting PostMatchResultElement components if needed
                if (postMatchResultElementList.Count < matchResults.Length)
                {
                    int elementsToAdd = matchResults.Length - postMatchResultElementList.Count;
                    for (int i = 0; i < elementsToAdd; i++)
                    {
                        var newElement = Instantiate(PostMatchResultElementPrefab, PostMatchResultParent);
                        newElement.Initialize(() => authManager.UUID);

                        if (friendManager.IsAlreadyInitialized)
                        {
                            newElement.FriendInitialize
                                (friendManager.IsFriendOrRequested,
                                friendManager.OpenConfirmFrienshipPopUp,
                                ref friendManager.OnBeforeSendFriendRequest,
                                ref friendManager.OnFriendRequestSent);
                        }

                        postMatchResultElementList.Add(newElement);
                    }
                }
            }
            else
                Debug.LogWarning("PostMatchResultElement prefab is not assigned in PostMatchResultController.");

            // Configure each PostMatchResultElement with the match results
            for (int i = 0; i < postMatchResultElementList.Count; i++)
            {
                var postMatchEntry = postMatchResultElementList[i];
                var matchResult = i < matchResults.Length ? matchResults[i] : null;
                var isVisible = i < matchResults.Length;

                // In case the element already exists, we configure it with the new match result.
                // If not, it will be configured when instantiated in the previous step
                if (isVisible)
                {
                    // In 2v2 mode, we need to pass the team points and if the local player is in team A or not,
                    // to properly configure the elements with the correct information and colors
                    var teamRoundPoints = numberPlayers is NumberPlayers.twoVsTwo && (matchResult?.IsTeamA.HasValue ?? false)
                        ? (matchResult.IsTeamA.Value ? teamARoundPoints : teamBRoundPoints)
                        : default(int?);

                    var teamAccumulatedPoints = numberPlayers is NumberPlayers.twoVsTwo && (matchResult?.IsTeamA.HasValue ?? false)
                        ? (matchResult.IsTeamA.Value ? teamAAccumulatedPoints : teamBAccumulatedPoints)
                        : default(int?);

                    postMatchEntry.Configure(gameType, numberPlayers, matchResult, limitPoints, isGameOver,
                        isTeamA: numberPlayers is NumberPlayers.twoVsTwo ? matchResult?.IsTeamA : default, 
                        teamRoundPoints: numberPlayers is NumberPlayers.twoVsTwo? teamRoundPoints : default,
                        teamAccumulatedPoints: numberPlayers is NumberPlayers.twoVsTwo? teamAccumulatedPoints : default);
                }

                postMatchEntry.SetElementVisibility(isVisible);
            }

            // Indicates the game mode
            if (GameModeText)
                GameModeText.text = gameMode.ToString().CapitalizeFirstLetter();

            // Set the header text based on whether it's a game over or not
            if (HeaderText)
                HeaderText.text = isGameOver ? "Post Match Results" : $"Round {currentRound} Over";

            // Set the header text based on the game mode
            if (PlayerResultStateText)
            {
                // Determine if the player is the winner
                if (numberPlayers is not NumberPlayers.twoVsTwo)
                {
                    PlayerResultStateText.color = playerWinner == "Player" ? wonColor : playerWinner == "draw" ? drawColor : lostColor;
                    PlayerResultStateText.text = playerWinner == "Player" ? "Winner" : playerWinner == "draw" ? "It’s a draw" : "You Lose";
                } 
                else
                {
                    var orderAssignationLowerized = new List<string> { "player", "left", "top", "right" };
                    var playerWinnerArray = playerWinner.Split(' ');

                    // In some cases the match winner will contain not only the person that put the last tile, but the complete team
                    var WhoWin = playerWinnerArray
                        ?.Select(x => x.ToLower())
                        ?.FirstOrDefault(x => orderAssignationLowerized.Contains(x));

                    // In 2v2 mode, we check if the local player is in the winning team
                    var teamAWon = WhoWin is "player" or "top";
                    PlayerResultStateText.color = teamAWon ? Consts.Colors.TeamAColorValue : Consts.Colors.TeamBColorValue;
                    PlayerResultStateText.text = teamAWon ? "Team A Won!" : playerWinner == "draw" ? "It’s a draw" : "Team B Won!";
                }
            }

            // Determine the game mode icon
            if (GameModeIcon)
            {
                var searchedGameModeIcon = dictionaryService.GetSprite(Consts.CollectionKeys.GameMode, gameMode.ToString());
                GameModeIcon.sprite = searchedGameModeIcon ?? defaultGameModeIcon;
            }

            // Set the visibility of the entire bottom container (to alterate the layout group)
            // Ignores show it when the game mode is concentrate or replay
            ButtonContainerBottom?.SetActive(isGameOver && gameMode is not GameMode.concentrate and not GameMode.replay);

            // Set the visibility of the finish game buttons
            ToLobbyButton?.gameObject.SetActive(isGameOver && gameMode is not GameMode.replay);
            RematchButton?.gameObject.SetActive(isGameOver && gameMode is not GameMode.concentrate and not GameMode.replay);
            SaveReviewButton?.gameObject.SetActive(isGameOver && gameMode is not GameMode.replay && gameType is GameType.singlePlayerIA);

            // Set the visibility of the next round button
            NextRoundButton?.gameObject.SetActive(!isGameOver && gameMode is not GameMode.replay);
        }

        /// <summary>
        /// Adds the specified UnityAction listeners to the post match result UI buttons for both single and team modes.
        /// </summary>
        /// <param name="onNextRound">Action to invoke when the Next Round button is clicked.</param>
        /// <param name="onToLobby">Action to invoke when the To Lobby button is clicked.</param>
        /// <param name="onRematch">Action to invoke when the Rematch button is clicked.</param>
        /// <param name="onSaveReview">Action to invoke when the Save Review button is clicked.</param>
        public void AddListeners
            (UnityAction onNextRound = null,
            UnityAction onToLobby = null,
            UnityAction onRematch = null,
            UnityAction onSaveReview = null)
        {
            // Add listeners to the single post match result UI buttons if the UI is assigned
            if (singlePostMatchResultUI)
            {
                AddListener(singlePostMatchResultUI.NextRoundButton, onNextRound, "Next Round");
                AddListener(singlePostMatchResultUI.ToLobbyButton, onToLobby, "To Lobby");
                AddListener(singlePostMatchResultUI.RematchButton, onRematch, "Rematch");
                AddListener(singlePostMatchResultUI.SaveReviewButton, onSaveReview, "Save Review");
            } 
            else
                Debug.LogError("Single Post Match Result UI is not assigned in PostMatchResultController.");

            // Add listeners to the team post match result UI buttons if the UI is assigned
            if (teamPostMatchResultUI)
            {
                AddListener(teamPostMatchResultUI.NextRoundButton, onNextRound, "Next Round");
                AddListener(teamPostMatchResultUI.ToLobbyButton, onToLobby, "To Lobby");
                AddListener(teamPostMatchResultUI.RematchButton, onRematch, "Rematch");
                AddListener(teamPostMatchResultUI.SaveReviewButton, onSaveReview, "Save Review");
            }
            else
                Debug.LogError("Team Post Match Result UI is not assigned in PostMatchResultController.");

            void AddListener(Button button, UnityAction action, string buttonName)
            {
                if (button)
                    if (action != null)
                        button.onClick.AddListener(action);
                else
                    Debug.LogWarning($"{buttonName} button or listener is not assigned in PostMatchResultController.");
            }
        }

        /// <summary>
        /// Removes the specified UnityAction listeners from the post match result UI buttons for both single and team
        /// modes.
        /// </summary>
        /// <param name="onNextRound">The UnityAction to remove from the Next Round button.</param>
        /// <param name="onToLobby">The UnityAction to remove from the To Lobby button.</param>
        /// <param name="onRematch">The UnityAction to remove from the Rematch button.</param>
        /// <param name="onSaveReview">The UnityAction to remove from the Save Review button.</param>
        public void RemoveListeners
            (UnityAction onNextRound = null,
            UnityAction onToLobby = null, 
            UnityAction onRematch = null, 
            UnityAction onSaveReview = null)
        {
            // Remove listeners to the single post match result UI buttons if the UI is assigned
            if (singlePostMatchResultUI)
            {
                RemoveListener(singlePostMatchResultUI.NextRoundButton, onNextRound, "Next Round");
                RemoveListener(singlePostMatchResultUI.ToLobbyButton, onToLobby, "To Lobby");
                RemoveListener(singlePostMatchResultUI.RematchButton, onRematch, "Rematch");
                RemoveListener(singlePostMatchResultUI.SaveReviewButton, onSaveReview, "Save Review");
            }
            else
                Debug.LogError("Single Post Match Result UI is not assigned in PostMatchResultController.");

            // Remove listeners to the team post match result UI buttons if the UI is assigned
            if (teamPostMatchResultUI)
            {
                RemoveListener(teamPostMatchResultUI.NextRoundButton, onNextRound, "Next Round");
                RemoveListener(teamPostMatchResultUI.ToLobbyButton, onToLobby, "To Lobby");
                RemoveListener(teamPostMatchResultUI.RematchButton, onRematch, "Rematch");
                RemoveListener(teamPostMatchResultUI.SaveReviewButton, onSaveReview, "Save Review");
            }
            else
                Debug.LogError("Team Post Match Result UI is not assigned in PostMatchResultController.");

            void RemoveListener(Button button, UnityAction action, string buttonName)
            {
                if (button)
                    if (action != null)
                        button.onClick.RemoveListener(action);
                else
                    Debug.LogWarning($"{buttonName} button or listener is not assigned in PostMatchResultController.");
            }
        }

        /// <summary>
        /// Sets the visibility of the controller and updates the layout. Plays a sound effect when made visible.
        /// </summary>
        /// <param name="isVisible">True to show the controller; false to hide it.</param>
        public void SetVisibility(bool isVisible)
        { 
            if (!rootCanvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned in PostMatchResultController.");
                return;
            }

            // Set the visibility of the controller
            rootCanvasGroup.SetActive(isVisible);
            rootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

            if(isVisible)
                SoundManager.Instance.PlaySFX(IDAudioClip.roundOver);
        }

        /// <summary>
        /// Sets the text displayed on the Next Round button.
        /// </summary>
        /// <param name="text">The new text to display on the button.</param>
        public void SetNexRoundText(string text)
        {
            if (NextRoundButtonText)
                NextRoundButtonText.text = text;
        }

        /// <summary>
        /// Sets the text displayed on the rematch button.
        /// </summary>
        /// <param name="text">The new text to display on the rematch button.</param>
        public void SetRematchText(string text)
        {
            if (RematchButtonText)
                RematchButtonText.text = text;
        }

        /// <summary>
        /// Compares between the match results and the local player UID to determine if this player is the winner.
        /// </summary>
        /// <param name="matchResults"></param>
        /// <returns></returns>
        private bool IsThisPlayerTheWinner(MatchResult[] matchResults)
        {
            var localPlayerUID = authManager.PlayerInfo.Id;
            var searchedMatchResult = matchResults.FirstOrDefault(result => result.PlayerUID == localPlayerUID);
            if (searchedMatchResult == null)
                return false;
            return searchedMatchResult.Position == 1;
        }

        /// <summary>
        /// Represents the result of a player's match, including user information, position, score, ranked points, and
        /// team assignment.
        /// </summary>
        public class MatchResult
        {
            public string PlayerUsername { get; set; }
            public string PlayerUID { get; set; }
            public Sprite PlayerIcon { get; set; }

            public uint Position { get; set; }
            public int LastRoundScore { get; set; }
            public int RankedPoints { get; set; }

            public int PlayerIndex { get; set; }
            public bool? IsTeamA { get; set; }

            public MatchResult(string playerUsername, string playerUID, Sprite playerIcon, uint position, int lastRoundScore, int rankedPoints, int playerIndex, bool? isTeamA = null)
            {
                PlayerUsername = playerUsername;
                PlayerUID = playerUID;
                PlayerIcon = playerIcon;
                Position = position;
                LastRoundScore = lastRoundScore;
                RankedPoints = rankedPoints;
                PlayerIndex = playerIndex;
                IsTeamA = isTeamA;
            }
        }
    }
}
