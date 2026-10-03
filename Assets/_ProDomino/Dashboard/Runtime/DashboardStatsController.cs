using System.Globalization;
using ProDomino.GameSystem;
using Timba.Patterns;
using TMPro;
using UnityEngine;

namespace ProDomino.Dashboard
{
    /// <summary>
    /// Fills the dashboard stat pills (Games Played Today, User Playing Now, Active Player) from the
    /// GameManager global analytics. GameManager keeps the data live (RTDB listener + active players polling)
    /// and raises its update event, this component only renders it.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashboardStatsController : MonoBehaviour
    {
        private const string EmptyValue = "-";

        [SerializeField] private TMP_Text gamesPlayedTodayLabel;
        [SerializeField] private TMP_Text usersPlayingNowLabel;
        [SerializeField] private TMP_Text activePlayersLabel;

        private GameManager gameManager;
        private bool isListening;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();

            gameManager?.HandleOnSignIn(OnSignIn);
            gameManager?.HandleOnSignOut(OnSignOut);
        }

        private void OnEnable()
        {
            // The dashboard panel can be enabled after sign in already happened
            if (gameManager is { IsAlreadyInitialized: true, IsAuthenticated: true })
                StartListening();

            UpdateUI();
        }

        private void OnDisable() => StopListening();

        private void OnSignIn()
        {
            if (isActiveAndEnabled)
                StartListening();

            UpdateUI();
        }

        private void OnSignOut()
        {
            StopListening();
            UpdateUI();
        }

        private void StartListening()
        {
            if (isListening || gameManager is null)
                return;

            gameManager.AddListenerWhenUpdateGlobalAnalytics(UpdateUI);
            isListening = true;
        }

        private void StopListening()
        {
            if (!isListening || gameManager is null)
                return;

            gameManager.RemoveListenerWhenUpdateGlobalAnalytics(UpdateUI);
            isListening = false;
        }

        private void UpdateUI()
        {
            var isAuthenticated = gameManager is { IsAuthenticated: true };
            var data = isAuthenticated ? gameManager.GlobalAnalyticsData : null;

            SetValue(gamesPlayedTodayLabel, data?.gamesPlayedToday);
            SetValue(usersPlayingNowLabel, data?.usersPlayingNow);
            SetValue(activePlayersLabel, isAuthenticated && gameManager.ActivePlayers >= 0 ? gameManager.ActivePlayers : null);
        }

        private static void SetValue(TMP_Text label, int? value)
        {
            if (!label)
                return;

            label.text = value.HasValue ? FormatCount(Mathf.Max(0, value.Value)) : EmptyValue;
        }

        // Pill value box is narrow, so large counts are shortened (1.5k, 2.3M)
        private static string FormatCount(int value)
        {
            var culture = CultureInfo.InvariantCulture;
            if (value >= 1_000_000)
                return (value / 1_000_000f).ToString(value >= 10_000_000 ? "0" : "0.#", culture) + "M";
            if (value >= 1_000)
                return (value / 1_000f).ToString(value >= 10_000 ? "0" : "0.#", culture) + "k";
            return value.ToString();
        }
    }
}
