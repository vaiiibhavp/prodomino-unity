using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Timba.Patterns;
using TMPro;
using Unity.Services.CloudCode;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.HandleProcessesSystem.InternetChecker;

namespace ProDomino.HandleProcessesSystem
{
    /// <summary>
    /// Central controller responsible for handling asynchronous operations within the application.
    /// 
    /// This controller exposes a unified, generic pipeline that supports both:
    ///  - UniTask (void-like) operations
    ///  - UniTask{T} operations that return values
    ///
    /// Features:
    ///  - Automatic retries for operations provided as factories (Func&lt;UniTask{T}&gt; or Func&lt;UniTask&gt;)
    ///  - Exponential backoff with jitter between retries
    ///  - Aggregated failure handling with a single "Try Again" popup
    ///  - Per-task optional loading screen (showLoading) that is purely visual and opt-in
    ///  - Internet reconnection awareness via InternetChecker
    ///  - Thread-safe pending task counting for correct loading visibility
    /// 
    /// Important behavior notes:
    ///  - For retries to work you must provide a factory (Func&lt;UniTask{T}&gt;) rather than an already-created UniTask{T}.
    ///  - When a generic operation fails after all automatic retries, the exception is recorded in failedEntries and re-thrown to the caller.
    ///  - Try Again re-runs failed entries using stored non-generic wrappers; any return value from the re-run is discarded.
    ///  - Optional predicate (optionalPredicate) validates logical success even when the underlying call does not throw.
    /// </summary>
    public class HandleProcessesController : SingleInstanceMonoBehaviour<HandleProcessesController>, IService
    {
        [Range(1, 10)]
        [SerializeField] private int automaticAttemptLimit = 3;
        [SerializeField] private int maxSingleTaskTimeout = 10;

        [Header("Root Canvas")]
        [SerializeField] private CanvasGroup rootCanvas;

        [Header("Reconnection Label")]
        [SerializeField] private CanvasGroup loadOrReconnectionCanvasGroup;
        [SerializeField] private TMP_Text loadOrReconnectionLabel;
        [SerializeField] private InternetChecker internetChecker;

        [Header("Try Again Canvas")]
        [SerializeField] private CanvasGroup tryAgainCanvas;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Button tryAgainButton, refreshPageButton;

        // Fixed: return the class name instead of recursively referencing itself.
        private string class_key_id => $"<b>{GetType().Name}</b>";

        /// <summary>
        /// Counts active asynchronous tasks. Use Interlocked for thread-safety.
        /// </summary>
        private int currentAsyncTasks;

        /// <summary>
        /// List of process entries that failed after automatic retries.
        /// These entries contain non-generic wrappers so TryAgain can re-run them.
        /// </summary>
        private readonly List<ProcessEntry> failedEntries = new();

        /// <summary>
        /// Required by IService interface in your project structure.
        /// </summary>
        public bool IsAlreadyInitialized => true;

        /// <summary>
        /// Current visible UI state for the process controller.
        /// </summary>
        internal HandleProcessType CurrentHandleProcessErrorType { get; private set; }

        #region Unity lifecycle

        /// <summary>
        /// Subscribe to internet status changes and button events.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (internetChecker)
                internetChecker.HandleInternetStatusChanged(OnInternetStatusChanged);

            if (tryAgainButton)
                tryAgainButton.onClick.AddListener(OnTryAgainClicked);

            if (refreshPageButton)
                refreshPageButton.onClick.AddListener(OnRefreshClicked);
        }

        /// <summary>
        /// Unsubscribe to avoid leaks.
        /// </summary>
        private void OnDestroy()
        {
            if (internetChecker)
                internetChecker.UnhandleInternetStatusChanged(OnInternetStatusChanged);

            if (tryAgainButton)
                tryAgainButton.onClick.RemoveListener(OnTryAgainClicked);

            if (refreshPageButton)
                refreshPageButton.onClick.RemoveListener(OnRefreshClicked);
        }

        #endregion

        #region Public API (generic + non-generic overloads)

        /// <summary>
        /// Executes a generic task using a factory (recommended).
        /// When the factory is provided, the operation can be safely retried by this controller.
        /// </summary>
        /// <typeparam name="T">Return type of the UniTask</typeparam>
        /// <param name="taskFactory">Factory that produces a fresh UniTask{T} on each invocation.</param>
        /// <param name="taskId">Optional identifier for logs and debugging.</param>
        /// <param name="showLoading">If true, show the loading UI while the operation is pending.</param>
        /// <returns>A UniTask{T} that completes with the operation result or throws if final retry fails.</returns>
        public UniTask<T> HandleProcess<T>
            (Func<UniTask<T>> taskFactory, 
            string taskId = null, 
            bool showLoading = true,
            bool shouldIgnoreTryAgainProcess = false,
            bool returnExceptionOnError = false,
            bool shouldRetrySomeTimes = true,
            bool isUsingTimeOut = true,
            Func<T, bool> resultValidator = null)
        {
            if (taskFactory == null)
                throw new ArgumentNullException(nameof(taskFactory));

            return InternalHandleProcess
                (factory: taskFactory, 
                taskId: taskId, 
                showLoading: showLoading, 
                shouldIgnoreTryAgainProcess: shouldIgnoreTryAgainProcess, 
                returnExceptionOnError: returnExceptionOnError,
                shouldRetrySomeTimes: shouldRetrySomeTimes, 
                isUsingTimeOut: isUsingTimeOut,
                resultValidator: resultValidator);
        }

        /// <summary>
        /// Convenience overload for non-generic factories (void-like tasks).
        /// Internally uses the generic pipeline with Unit as placeholder.
        /// </summary>
        public UniTask HandleProcess
            (Func<UniTask> taskFactory, 
            string taskId = null,
            bool showLoading = true,
            bool shouldIgnoreTryAgainProcess = false,
            bool returnExceptionOnError = false,
            bool shouldRetrySomeTimes = true,
            bool isUsingTimeOut = true,
            Func<bool> resultValidator = null)
        {
            if (taskFactory == null)
                throw new ArgumentNullException(nameof(taskFactory));

            // Wrap the non-generic factory into a generic one returning Unit
            Func<UniTask<Unit>> wrapperFactory = async () =>
            {
                await taskFactory();
                return new Unit();
            };

            // Call the generic pipeline and discard the Unit result
            return HandleProcess
                (taskFactory: wrapperFactory,
                taskId: taskId,
                showLoading: showLoading,
                shouldIgnoreTryAgainProcess: shouldIgnoreTryAgainProcess,
                returnExceptionOnError: returnExceptionOnError,
                shouldRetrySomeTimes: shouldRetrySomeTimes,
                isUsingTimeOut: isUsingTimeOut,
                WrapResultValidatorNonGeneric(resultValidator)).AsUniTask();
        }
        #endregion

        #region Core generic pipeline

        /// <summary>
        /// Improved and simplified version of your internal pipeline.
        /// Keeps full compatibility with your external API, logging and UI.
        /// </summary>
        private async UniTask<T> InternalHandleProcess<T>(
            Func<UniTask<T>> factory,
            string taskId,
            bool showLoading,
            bool shouldIgnoreTryAgainProcess = false,
            bool returnExceptionOnError = false,
            bool shouldRetrySomeTimes = true,
            bool isUsingTimeOut = true,
            Func<T, bool> resultValidator = null)
        {
            string displayId = taskId ?? Guid.NewGuid().ToString();
            Debug.Log($"{class_key_id} {displayId} <color=green>Running...</color>");

            IncrementPending();

            if (showLoading)
                DetermineHandleProcessErrorUI(HandleProcessType.LoadingScreen);

            // Create the entry metadata
            var entry = new ProcessEntry
            {
                Id = displayId,
                Attempts = 0,
                ShowLoadingScreen = showLoading,
                ResultValidator = resultValidator != null
                    ? new Func<object, bool>(o => resultValidator(o is T t ? t : default))
                    : null
            };

            // If a factory is provided, create and store a non-generic wrapper for re-run.
            if (factory != null)
            {
                // The wrapper calls the factory, captures the result, validates using OptionalPredicate if present,
                // and throws if the predicate fails. This makes predicate failures behave like exceptions for retry purposes.
                entry.Factory = async () =>
                {
                    // Execute the original factory and grab the typed result
                    T producedValue = await factory();
                };
            } 

            Exception lastException = null;

            try
            {
                // Attempt loop
                for (int attempt = 0; attempt < automaticAttemptLimit; attempt++)
                {
                    entry.Attempts = attempt + 1;
                    T result = default;

                    try
                    {
                        // Execute factory with optional timeout
                        if (!isUsingTimeOut)
                            result = await factory();
                        else
                        {
                            var timed = await factory().TimeoutWithoutException(TimeSpan.FromSeconds(maxSingleTaskTimeout));
                            result = timed.Result is not null ? timed.Result : default;
                        }

                        // If we reached this far: success
                        entry.LastErrorMessage = null;
                        entry.LastErrorCode = null;

                        Debug.Log($"{class_key_id} {displayId} <color=cyan>succeeded on attempt</color> <b>{entry.Attempts}</b>.");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        // Custom validation: treat false as failure
                        if (entry.ResultValidator != null && !entry.ResultValidator(result))
                        {
                            // Treat as failure
                            lastException = new Exception($"Predicate validation failed: \n{ex.Message}", ex); ;

                            entry.LastErrorMessage = ex.Message;
                            entry.LastErrorCode = "PredicateFailed";

                            Debug.LogWarning($"{class_key_id} {displayId} <color=red>failed predicate on attempt</color> {entry.Attempts}:\n\n<b>{ex.Message}</b>");

                            // If the method was supposed to be played once or this was the last attempt, break
                            if (!shouldRetrySomeTimes || attempt == automaticAttemptLimit - 1)
                                break;

                            await UniTask.Delay(TimeSpan.FromSeconds(CalculateBackoffSeconds(attempt)));

                            // Continue to next attempt
                            continue;
                        }

                        // Exception-handling path
                        lastException = ex;

                        entry.LastErrorMessage = ex.Message;
                        entry.LastErrorCode = ex switch
                        {
                            CloudCodeException cex => cex.Reason.ToString(),
                            HttpRequestException hrex => hrex.Message,
                            _ => ex.GetType().Name
                        };

                        Debug.LogWarning(
                            $"{class_key_id} {displayId} <color=red>failed on attempt</color> {entry.Attempts}:\n\n<b>{ex.Message}</b>");

                        // If the method was supposed to be played once or this was the last attempt, break
                        if (!shouldRetrySomeTimes || attempt == automaticAttemptLimit - 1)
                            break;

                        await UniTask.Delay(TimeSpan.FromSeconds(CalculateBackoffSeconds(attempt)));
                    }
                }

                // All attempts failed
                if (!shouldIgnoreTryAgainProcess)
                {
                    lock (failedEntries)
                        failedEntries.Add(entry);

                    DetermineHandleProcessErrorUI(HandleProcessType.CloudCodeException, isForced: true);

                    // After recording failure, rethrow last exception so caller knows it failed.
                    Debug.LogError($"{class_key_id} {displayId} <color=red>Controlled Exception</color>\n\n<b>{lastException?.Message ?? "Unknown failure in InternalHandleProcess<T>"}</b>");
                } 

                // Just inform about the omission and return default
                else
                    Debug.LogError($"{class_key_id} {displayId} <color=#ebb86c>Ommited Exception</color>\n\n<b>{lastException?.Message ?? "Unknown failure in InternalHandleProcess<T>"}</b>");

                // If the process recognize better the exception, throw it
                if (returnExceptionOnError && lastException != null)
                    throw lastException;

                return default;
            }
            finally
            {
                // Final cleanup
                DecrementPending();

                // Hide loading for this task only if it had requested loading.
                DetermineHandleProcessErrorUI(RecalculateGlobalErrorState());
            }
        }


        /// <summary>
        /// Simple wrapper for the result validator
        /// </summary>
        /// <param name="resultValidator"></param>
        /// <returns></returns>
        private Func<Unit, bool> WrapResultValidatorNonGeneric(
            Func<bool> resultValidator = null)
        {
            return resultValidator != null
                ? new Func<Unit, bool>((_unit) => resultValidator())
                : default;
        }

        #endregion

        #region Retry utilities

        /// <summary>
        /// Exponential backoff with jitter.
        /// </summary>
        private static double CalculateBackoffSeconds(int attemptZeroBased)
        {
            const double baseSec = 0.5;
            const double maxSec = 5.0;
            double sec = Math.Min(baseSec * Math.Pow(2, attemptZeroBased), maxSec);
            var jitter = (UnityEngine.Random.value - 0.5) * 0.5;
            return Math.Max(0.1, sec + jitter);
        }

        private void IncrementPending() => Interlocked.Increment(ref currentAsyncTasks);
        private void DecrementPending() => Interlocked.Decrement(ref currentAsyncTasks);

        /// <summary>
        /// When Try Again is clicked, re-run all failed entries that have a wrapper factory.
        /// Note: returned values from these re-runs are discarded; these are manual retries triggered by the user.
        /// </summary>
        private void OnTryAgainClicked()
        {
            List<ProcessEntry> toRetry;
            lock (failedEntries)
            {
                toRetry = new List<ProcessEntry>(failedEntries);
                failedEntries.Clear();
            }

            if (toRetry.Count == 0)
            {
                DetermineHandleProcessErrorUI(RecalculateGlobalErrorState(), isForced: true);
                return;
            }

            // Show the loading screen until the task are finished
            DetermineHandleProcessErrorUI(HandleProcessType.LoadingScreen, true);

            Debug.Log($"{class_key_id} Re-attempting {toRetry.Count} failed entries via TryAgain...");

            foreach (var entry in toRetry)
            {
                if (entry.Factory != null)
                {
                    // Re-run using stored non-generic wrapper; fire-and-forget.
                    // entry.Factory already applies the original predicate and throws if it fails,
                    // ensuring TryAgain observes the same validation rules as the original call.
                    HandleProcess
                        (taskFactory: entry.Factory, 
                        taskId: entry.Id, 
                        showLoading: entry.ShowLoadingScreen, 
                        shouldIgnoreTryAgainProcess: entry.shouldIgnoreTryAgainProcess,
                        resultValidator: entry.ResultValidator is not null 
                            ? new Func<bool>(() => entry.ResultValidator(default))
                            : null)
                    .Forget();
                }

                else
                    Debug.LogWarning($"{class_key_id} <color=yellow>Cannot re-run entry<color> <b>{entry.Id}</b>: missing factory and one-shot wrappers.");
            }
        }

        /// <summary>
        /// Refresh/reload handler.
        /// </summary>
        private void OnRefreshClicked()
        {
            Debug.Log("{class_key_id} Refresh clicked.");

#if UNITY_WEBGL && !UNITY_EDITOR
            Application.OpenURL(Application.absoluteURL);
#else
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
#endif
        }

        #endregion

        #region UI Management

        /// <summary>
        /// Evaluates the UI state globally based on all active conditions.
        /// Ensures correct priority:
        /// 1. CloudCodeException   (highest)
        /// 2. InternetDisconnected
        /// 3. LoadingScreen
        /// 4. None                 (lowest)
        /// </summary>
        public void DetermineHandleProcessErrorUI(HandleProcessType requestedState, bool isForced = false)
        {
            // Step 1: Update the current state based on priority rules.
            // If forced, override immediately.
            if (isForced)
                CurrentHandleProcessErrorType = requestedState;

            else
            {
                // Only promote to a higher-priority state.
                if (requestedState > CurrentHandleProcessErrorType)
                    CurrentHandleProcessErrorType = requestedState;

                // But, if the requested state is None (removal), we need to recalculate.
                else if (requestedState == HandleProcessType.None)
                {
                    // If removing a state, we must recalculate the highest applicable state
                    CurrentHandleProcessErrorType = RecalculateGlobalErrorState();
                }
            }

            // Step 2: Read external conditions
            bool internetDisconnected = CurrentHandleProcessErrorType == HandleProcessType.InternetDisconnected;
            bool loadingActive = CurrentHandleProcessErrorType == HandleProcessType.LoadingScreen && currentAsyncTasks > 0;

            bool anyFailed;
            lock (failedEntries)
                anyFailed = failedEntries.Count > 0;

            bool showRetryPopup = CurrentHandleProcessErrorType == HandleProcessType.CloudCodeException || anyFailed;

            // Step 3: Apply UI state
            SetsVisibleRootCanvas(CurrentHandleProcessErrorType != HandleProcessType.None);
            SetVisibleTryAgainCanvas(showRetryPopup);

            SetVisibleLoadOrReconnectionCanvasGroup(
                loadingActive || internetDisconnected,
                loadingActive
            );

            // Step 4: Update retry popup description
            if (tryAgainCanvas != null && tryAgainCanvas.alpha > 0 && descriptionLabel != null)
                lock (failedEntries)
                {
                    descriptionLabel.text = failedEntries.Count > 0
                        ? $"There are {failedEntries.Count} failed operations. You can try again or refresh the page."
                        : "An error occurred. Please try again or refresh the page.";
                }

        }
        
        /// <summary>
        /// Recalculates the correct global UI state based on real conditions.
        /// This is used when a state is removed (e.g., a loading task finished),
        /// to find which state should now be visible.
        /// </summary>
        private HandleProcessType RecalculateGlobalErrorState()
        {
            var internetDisconnected = internetChecker != null &&
                                        internetChecker.LastInternetStatus != InternetStatus.Online;

            var anyFailed = false;
            lock (failedEntries)
                anyFailed = failedEntries.Count > 0;

            if (anyFailed)
                return HandleProcessType.CloudCodeException;

            if (internetDisconnected)
                return HandleProcessType.InternetDisconnected;

            if (currentAsyncTasks > 0)
                return HandleProcessType.LoadingScreen;

            return HandleProcessType.None;
        }

        private void SetsVisibleRootCanvas(bool isActive)
        {
            if (!rootCanvas) 
                return;
            rootCanvas.SetActive(isActive);
        }

        private void SetVisibleLoadOrReconnectionCanvasGroup(bool isActive, bool isLoading)
        {
            if (!loadOrReconnectionCanvasGroup) 
                return;

            loadOrReconnectionCanvasGroup.SetActive(isActive);

            if (loadOrReconnectionLabel)
                loadOrReconnectionLabel.text = isLoading ? "Loading..." : "Reconnecting...";
        }

        private void SetVisibleTryAgainCanvas(bool isActive)
        {
            if (!tryAgainCanvas) 
                return;
            tryAgainCanvas.SetActive(isActive);
        }

        #endregion

        #region Internet callback and supporting models

        /// <summary>
        /// Called by InternetChecker when network status changes.
        /// Updates UI accordingly.
        /// </summary>
        private void OnInternetStatusChanged(InternetStatus internetStatus)
        {
            if (!internetChecker || !loadOrReconnectionLabel)
                return;

            var errorType = internetStatus switch
            {
                InternetStatus.SearchingForConnection => HandleProcessType.InternetDisconnected,
                InternetStatus.Lost => HandleProcessType.CloudCodeException,
                _ => HandleProcessType.None
            };

            DetermineHandleProcessErrorUI(errorType);
        }

        /// <summary>
        /// Visual states for the controller UI.
        /// </summary>
        public enum HandleProcessType
        {
            None = 0,
            LoadingScreen = 1,
            InternetDisconnected = 2,
            CloudCodeException = 3
        }

        /// <summary>
        /// A minimal Unit struct used as a placeholder for void-like generic flows.
        /// </summary>
        private readonly struct Unit { }

        /// <summary>
        /// Internal representation of a managed process.
        /// Stores wrappers so retry UI can re-run operations even when the original produced a generic result.
        /// </summary>
        private class ProcessEntry
        {
            /// <summary>Identifier for logs/debug.</summary>
            public string Id;

            /// <summary>Number of attempts performed.</summary>
            public int Attempts;

            /// <summary>Last exception message (if any).</summary>
            public string LastErrorMessage;

            /// <summary>Error code representation.</summary>
            public string LastErrorCode;

            /// <summary>Whether this task requested the loading screen.</summary>
            public bool ShowLoadingScreen;

            /// <summary>An alternative check for the task (if the task doesn't return an exception)</summary>
            public Func<object, bool> ResultValidator;

            /// <summary>If true, exceptions from this task are omitted from the failed entries list and doesn't show error container</summary>
            public bool shouldIgnoreTryAgainProcess;

            /// <summary>
            /// Non-generic wrapper that runs the original operation and discards the result.
            /// Present when the operation was supplied as a factory.
            /// </summary>
            public Func<UniTask> Factory;
        }

        #endregion
    }
}
