using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.GameModes
{
    /// <summary>
    /// A MonoBehaviour component that manages a visual timer bar with events for starting, updating, and finishing the
    /// timer, supporting UI updates and external event listeners.
    /// </summary>
    public class TimerBar : MonoBehaviour
    {
        [SerializeField] private Image timeBarImage;
        [SerializeField] private TMP_Text timeBarText;

        [Tooltip("No parameters")]
        [SerializeField] private UnityEvent onStartTimer;

        [Tooltip("Parameters: currentTimer (float), duration (float)")]
        [SerializeField] private UnityEvent<float, float> onUpdateTimeBar;

        [Tooltip("Parameter: isTimeout (bool)")]
        [SerializeField] private UnityEvent<bool> onFinishTimer;

        private float timeDuration;
        private float timeElapsed;
        private Coroutine timerCoroutine;

        private UnityAction<ScoreUI> refStartTimerAction;
        private UnityAction<float, float, ScoreUI> refUpdateTimerAction;
        private UnityAction<bool, ScoreUI> refFinishTimerAction;

        internal ScoreUI AttachedScoreUI { get; private set; }
        public float LeftingTime => Mathf.Max(0, timeDuration - timeElapsed);

        private void Awake()
        {
            // Add internal listeners to UnityEvents
            onStartTimer.AddListener(OnStartTimer);
            onUpdateTimeBar.AddListener(OnUpdateTimer);
            onFinishTimer.AddListener(OnFinishTimer);
        }

        private void OnDestroy()
        {
            // Remove internal listeners from UnityEvents
            onStartTimer.RemoveListener(OnStartTimer);
            onUpdateTimeBar.RemoveListener(OnUpdateTimer);
            onFinishTimer.RemoveListener(OnFinishTimer);
        }

        /// <summary>
        /// Initializes the TimerBar with a ScoreUI reference.
        /// </summary>
        /// <param name="scoreUI">The ScoreUI instance to attach to the TimerBar.</param>
        internal void Initialization(ScoreUI scoreUI)
        {
            // Validate the ScoreUI reference
            if (!scoreUI)
                throw new ArgumentNullException(nameof(scoreUI), "ScoreUI reference cannot be null.");

            // Attach the ScoreUI reference
            AttachedScoreUI = scoreUI;
        }

        /// <summary>
        /// Adds external listeners to the timer bar events.
        /// </summary>
        /// <param name="onStartTimer">The action to invoke when the timer starts.</param>
        /// <param name="onUpdateTimer">The action to invoke when the timer updates.</param>
        /// <param name="onFinishTimer">The action to invoke when the timer finishes.</param>
        internal void ConfigureExternalReferences
            (UnityAction<ScoreUI> onStartTimer, 
            UnityAction<float, float, ScoreUI> onUpdateTimer, 
            UnityAction<bool, ScoreUI> onFinishTimer)
        {
            refStartTimerAction = onStartTimer;
            refUpdateTimerAction = onUpdateTimer;
            refFinishTimerAction = onFinishTimer;
        }

        /// <summary>
        /// Starts the timer for the specified duration.
        /// </summary>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <param name="isForcingReset">Whether to force reset the timer if it's already running.</param>
        internal void StartTimer(float duration, bool isForcingReset = true)
        {
            // If a timer is already running and forcing reset is true, stop it first
            if (timerCoroutine != null && isForcingReset)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }

            // Start a new timer if none is running
            if (timerCoroutine is null)
            {
                // Initialize timer variables
                timeDuration = duration;
                timeElapsed = 0f;
                timeBarImage.fillAmount = 1f;

                // Start the timer coroutine
                timerCoroutine = StartCoroutine(Timer());
            }

            /// Sets the timer values and updates the time bar each frame until the duration is reached, then invokes the finish event.
            IEnumerator Timer()
            {
                onStartTimer?.Invoke();
                while (timeElapsed < timeDuration)
                {
                    var normalizedTime = timeElapsed > 0 ? Mathf.Clamp01(timeElapsed / timeDuration) : 0;
                    var inverseNormalizedTime = 1f - normalizedTime;

                    if (timeBarImage)
                        timeBarImage.fillAmount = inverseNormalizedTime;

                    if (timeBarText)
                        timeBarText.text = $"{Mathf.CeilToInt(timeDuration - timeElapsed)}";

                    onUpdateTimeBar?.Invoke(inverseNormalizedTime, timeDuration);

                    yield return null; // Wait for the next frame
                    timeElapsed += Time.deltaTime;
                }

                // Indicates the timer is finished due timeout
                onFinishTimer?.Invoke(true);
                timerCoroutine = null;
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        internal void StopTimer()
        {
            // Stop the timer coroutine if it's running
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }

            // If there is an image component, reset its fill amount
            if (timeBarImage)
                timeBarImage.fillAmount = 0f;

            // If there is a text component, clear it
            if (timeBarText)
                timeBarText.text = string.Empty;
        }


        /// <summary>
        /// Set a specific timer value.
        /// </summary>
        /// <param name="currentTimer">The current timer value in seconds.</param>
        /// <param name="duration">The duration of the timer in seconds.</param>
        internal void SetSpecificTimerValue(float currentTimer, float duration)
        {
            var normalizedTime = currentTimer > 0 ? Mathf.Clamp01(currentTimer / duration) : 0;

            if (timeBarImage)
                timeBarImage.fillAmount = normalizedTime;

            if (timeBarText)
                timeBarText.text = $"{Mathf.CeilToInt(currentTimer)}";
        }

        /// <summary>
        /// Sets the color of the time bar.
        /// </summary>
        /// <param name="color">The color to set for the time bar.</param>
        internal void SetColor(Color color)
        {
            if (timeBarImage)
                timeBarImage.color = color;
        }

        /// <summary>
        /// Call the unity event OnStartTimer externally.
        /// </summary>
        public void CallExternallyOnStartTimer() => onStartTimer?.Invoke();

        /// <summary>
        /// Call the unity event OnUpdateTimeBar externally.
        /// </summary>
        /// <param name="currentTimer">The current timer value in seconds.</param>
        /// <param name="duration">The duration of the timer in seconds.</param>
        public void CallExternallyOnUpdateTimeBar(float currentTimer, float duration) => onUpdateTimeBar?.Invoke(currentTimer, duration);

        /// <summary>
        /// Call the unity event OnFinishTimer externally.
        /// </summary>
        /// <param name="isTimeout">Indicates whether the timer finished due to a timeout.</param>
        public void CallExternallyOnFinishTimer(bool isTimeout) => onFinishTimer?.Invoke(isTimeout);

        /// <summary>
        /// Called when the timer starts.
        /// </summary>
        private void OnStartTimer()
        {
            // Invoke external listener if assigned
            if (refStartTimerAction is not null)
            {
                Debug.Log("TimerBar: OnSrefStartTimerActiontartTimer invoked.");
                refStartTimerAction.Invoke(AttachedScoreUI);
            } 
            else
                Debug.Log("TimerBar: OnStartTimer - No external listener assigned.");
        }

        /// <summary>
        /// Called when the timer finishes.
        /// </summary>
        /// <param name="isTimeout">Indicates whether the timer finished due to a timeout.</param>
        private void OnFinishTimer(bool isTimeOut)
        {
            // Invoke external listener if assigned
            if (refFinishTimerAction is not null)
            {
                Debug.Log("TimerBar: OnFinishTimer invoked.");
                refFinishTimerAction.Invoke(isTimeOut, AttachedScoreUI);
            } 
            else
                Debug.Log("TimerBar: OnFinishTimer - No external listener assigned.");
        }

        /// <summary>
        /// Called when the time bar is updated.
        /// </summary>
        /// <param name="currentTimer">The current timer value in seconds.</param>
        /// <param name="duration">The duration of the timer in seconds.</param>
        private void OnUpdateTimer(float currentTimer, float duration)
        {
            // Invoke external listener if assigned
            if (refUpdateTimerAction is not null)
                refUpdateTimerAction.Invoke(currentTimer, duration, AttachedScoreUI);
        }
    }
}
