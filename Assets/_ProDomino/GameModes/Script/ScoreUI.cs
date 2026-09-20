using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.GameModes.ExtendedGameController;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Manages and displays the user interface for player scores, achievements, team indicators, prompts, and related
    /// UI elements in a game.
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup mainCanvasGroup;
        [SerializeField] private CanvasGroup glowCanvasGroup;
        [SerializeField] private RectTransform scaler;
        [SerializeField] private Image profileImage;
        [SerializeField] private GameObject userAchievementsContainer;
        [SerializeField] private Image[] achievementsImageCollection;
        [SerializeField] private TMP_Text usernameLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TimerBar timerBar;
        [SerializeField] private Image teamIndicatorImage;
        [SerializeField] private TMP_Text teamIndicatorLabel;
        [SerializeField] private bool isTeamA = false;
        public bool IsTeamA => isTeamA;
        [SerializeField] private Animator expandHandViewportAnimator;
        [SerializeField] private float sizeToScaleOnSelect = 1.1f;

        [Header("Prompt Fields")]
        [SerializeField] private Animation promptAnimation;
        [SerializeField] private TMP_Text promptLabel;

        private Coroutine managePromptsCoroutine;
        private Func<int> getCurrentTurnPlayerID;
        private Queue<IEnumerator> promptIEnumeratorsQueue;

        public TimerBar TimerBar => timerBar;
        public RectTransform Scaler => scaler;
        public UserProfile UserProfile { get; private set; }
        public int Score { get; private set; }
        public static ScoreUI SelectedScoreUI { get; private set; }
        public TMP_Text UsernameLabel => usernameLabel;

        private void Start()
        {
            // Initialize prompt queue
            promptIEnumeratorsQueue = new();

            // Initialize TimerBar
            if (timerBar)
                timerBar.Initialization(this);
            else
                Debug.LogWarning("TimerBar is not assigned in the ScoreUI.");

            // By default, disable the glow effect
            glowCanvasGroup?.SetActive(false);
        }

        /// <summary>
        /// Configures the user interface with the provided user profile, score limit, and a delegate to retrieve the
        /// current turn player's ID.
        /// </summary>
        /// <param name="userProfile">The user profile containing user information and achievements.</param>
        /// <param name="limitScore">The maximum score limit for the game.</param>
        /// <param name="getCurrentTurnPlayerID">A delegate that returns the current turn player's ID.</param>
        public void Configure(UserProfile userProfile, int limitScore, Func<int> getCurrentTurnPlayerID)
        {
            UserProfile = userProfile;
            this.getCurrentTurnPlayerID = getCurrentTurnPlayerID;

            var username = UserProfile.Username;
            var profileIcon = UserProfile.ProfileIcon;
            var achievements = UserProfile.Achievements;

            if (!string.IsNullOrEmpty(username) && usernameLabel)
                usernameLabel.text = username;

            if (profileIcon != null && profileImage)
                profileImage.sprite = profileIcon;

            if (achievementsImageCollection is not null and { Length: > 0 })
            { 
                for (int i = 0; i < achievementsImageCollection.Length; i++)
                {
                    var achievementImage = achievementsImageCollection[i];
                    var achievement = achievements?.ElementAtOrDefault(i);

                    if (achievementImage)
                    { 
                        achievementImage.gameObject.SetActive(achievement != null);
                 
                        if (achievement != null)
                            achievementImage.sprite = achievement;
                    }
                    else
                        Debug.LogWarning("Achievement Image is not assigned in the ScoreUI.");
                }

                // Set container active if any achievement is active
                if (userAchievementsContainer)
                    userAchievementsContainer.SetActive(achievementsImageCollection.Any(x => x.gameObject.activeSelf));
                else
                {
                    Debug.LogWarning("User Achievements Container is not assigned in the ScoreUI.");
                    userAchievementsContainer?.SetActive(false);
                }
            }

            if (teamIndicatorImage)
            { 
                teamIndicatorImage.gameObject.SetActive(userProfile.IsTeamA.HasValue);

                if (userProfile.IsTeamA.HasValue)
                {
                    isTeamA = userProfile.IsTeamA.Value;

                    // Set color according to team
                    var teamColor = userProfile.IsTeamA.Value ? Consts.Colors.TeamAColorValue : Consts.Colors.TeamBColorValue;
                    teamIndicatorImage.color = teamColor;
                }
            }

            // Set text according to team
            if (teamIndicatorLabel && userProfile.IsTeamA.HasValue)
                teamIndicatorLabel.text = userProfile.IsTeamA.Value ? "Team A" : "Team B";

            // Reset some UI elements to default state
            glowCanvasGroup?.SetActive(false);

            // "Try To stop the timer" (its truly intentions are to reset the timer parameters)
            TimerBar?.StopTimer();

            // Configure it according the defalt score of the game
            ConfigureScore(0, limitScore);

            SetActiveScoreUI(UserProfile != null);
        }

        /// <summary>
        /// Enables or disables the main score UI.
        /// </summary>
        /// <param name="isActive">True to activate the score UI; false to deactivate it.</param>
        public void SetActiveScoreUI(bool isActive)
        {
            if (mainCanvasGroup != null)
                mainCanvasGroup.SetActive(isActive);
        }

        /// <summary>
        /// Updates the selected score UI element by toggling its glow effect and adjusting its scale to indicate
        /// selection.
        /// </summary>
        public void SelectScoreUI()
        {
            if (SelectedScoreUI?.glowCanvasGroup != null)
            { 
                SelectedScoreUI.glowCanvasGroup?.SetActive(false);

                if (SelectedScoreUI?.Scaler != null)
                    SelectedScoreUI.Scaler.localScale = Vector3.one; // Reset scale to default
            }

            SelectedScoreUI = this;
            if (SelectedScoreUI.glowCanvasGroup)
                SelectedScoreUI.glowCanvasGroup.SetActive(true);

            if (SelectedScoreUI?.Scaler != null)
                SelectedScoreUI.Scaler.localScale = Vector3.one * sizeToScaleOnSelect; // Scale up the selected UI
        }

        /// <summary>
        /// Deselects the currently selected score UI, disables its glow effect, resets its scale, and clears the
        /// selection.
        /// </summary>
        public void DeselectScoreUI()
        {
            if (SelectedScoreUI == this)
            {
                SelectedScoreUI.glowCanvasGroup?.SetActive(false);

                 if (SelectedScoreUI?.Scaler != null)
                    SelectedScoreUI.Scaler.localScale = Vector3.one; // Reset scale to default

                SelectedScoreUI = null;
            }
        }

        /// <summary>
        /// Activates a prompt for the specified player, displaying the given text and managing prompt display order.
        /// </summary>
        /// <param name="registeredTurnPlayerID">The ID of the player for whom the prompt is activated.</param>
        /// <param name="promptText">The text to display in the prompt. Defaults to "Pass".</param>
        /// <param name="forcedMinWaitSeconds">Optional minimum time in seconds to wait before allowing the prompt to be dismissed.</param>
        /// <param name="onShown">Optional callback invoked when the prompt is shown.</param>
        public void ActivatePrompt(int registeredTurnPlayerID, string promptText = "Pass", float? forcedMinWaitSeconds = null, Action onShown = null)
        {
            if (!promptAnimation)
                return;

            // Enqueue the requested prompt activation
            promptIEnumeratorsQueue.Enqueue(PlayPrompt(registeredTurnPlayerID, promptText, forcedMinWaitSeconds, onShown));

            // If there aren't any prompt process, start one using the prompts registered
            if (managePromptsCoroutine is null)
            {
                // If there is prompts to show, start its corresponding procces
                if (promptIEnumeratorsQueue is not null and { Count: > 0 })
                    managePromptsCoroutine = StartCoroutine(ManagePrompts());

                // Else, force the nullification of the reference to allow call it again
                else  if (managePromptsCoroutine is not null)
                {
                    Debug.LogWarning("Forcing stop prompt coroutine");
                    managePromptsCoroutine = null;
                }
            }
        }

        /// <summary>
        /// Processes and manages a queue of prompt coroutines, yielding each in sequence until the queue is empty.
        /// </summary>
        /// <returns>An enumerator for coroutine execution of prompt processing.</returns>
        private IEnumerator ManagePrompts()
        {
            // If the prompt queue is null or empty, exit
            if (promptIEnumeratorsQueue is null or { Count: 0 })
                yield break;

            // Iterate until the prompt queue is empty
            while (promptIEnumeratorsQueue.Count > 0)
            {
                var currentPrompt = promptIEnumeratorsQueue.Dequeue();
                yield return currentPrompt;
            }

            // Once all prompts are processed, nullify the coroutine reference
            managePromptsCoroutine = null;
        }

        /// <summary>
        /// Displays a prompt with optional text and sound effect, waits for a minimum duration or until the turn
        /// changes, then hides the prompt.
        /// </summary>
        /// <param name="registeredTurnPlayerID">The player ID associated with the current turn.</param>
        /// <param name="promptText">The text to display in the prompt. Defaults to "Pass".</param>
        /// <param name="forcedMinWaitSeconds">Optional minimum number of seconds to wait before hiding the prompt.</param>
        /// <param name="onShown">Optional callback invoked after the prompt is shown.</param>
        /// <returns>An enumerator for coroutine execution.</returns>
        private IEnumerator PlayPrompt(int registeredTurnPlayerID, string promptText = "Pass", float? forcedMinWaitSeconds = null, Action onShown = null)
        {
            // Show immediately
            promptAnimation.gameObject.SetActive(true);
            promptLabel.text = promptText;

            // Play sound effect
            SoundManager.Instance.PlaySFX(IDAudioClip.passTurn);

            // Play optional callback
            onShown?.Invoke();

            // Start coroutine to hide after animation finishes
            if (promptAnimation.Play(Consts.Clips.ShowUI))
            {
                // Get current clip info
                var clipCount = promptAnimation.GetClipCount();
                if (clipCount > 0)
                {
                    // Wait until the turn is not fiinished yet
                    var registerTime = DateTime.UtcNow;
                    if (forcedMinWaitSeconds.HasValue)
                        yield return new WaitForSeconds(forcedMinWaitSeconds.Value);

                    yield return new WaitUntil(() => 
                        SelectedScoreUI != this 
                        || registeredTurnPlayerID != getCurrentTurnPlayerID()
                        || (DateTime.UtcNow - registerTime).TotalSeconds > 5);

                    if (promptAnimation.Play(Consts.Clips.HideUI))
                    {
                        // Wait for the end of frame to ensure the animation starts playing
                        yield return new WaitUntil(() => promptAnimation.IsPlaying(Consts.Clips.HideUI));

                        // Wait for clip duration
                        var duration = promptAnimation.GetClip(Consts.Clips.HideUI).length;
                        yield return new WaitForSeconds(duration);

                    }
                }
            }

            // Finally hide the object
            promptAnimation.gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures and displays the score, limit score, and optional target score on the score label.
        /// </summary>
        /// <param name="score">The current score value to display.</param>
        /// <param name="limitScore">The maximum or limit score to display alongside the current score.</param>
        /// <param name="targetScore">An optional target score to display if provided and greater than zero.</param>
        public void ConfigureScore(int score, int limitScore, int? targetScore = null)
        {
            Score = score;
            if (scoreLabel)
            {
                var scoreText = score.ToString();

                if (targetScore.HasValue && targetScore.Value > 0)
                    scoreText += $" | {targetScore.Value}";

                if (limitScore != 0)
                    scoreLabel.text = scoreText + "/" + limitScore;
                else
                    scoreLabel.text = scoreText;
            }
        }

        /// <summary>
        /// Expands or collapses the hand viewport using the associated animator.
        /// </summary>
        /// <param name="isExpanded">True to expand the viewport; false to collapse it.</param>
        public void ExpandHandViewport(bool isExpanded)
        {
            if (!expandHandViewportAnimator)
                return;

            expandHandViewportAnimator.SetBool(Consts.Bools.IsShowing, isExpanded);
        }
    }
}
