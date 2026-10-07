using System.Collections.Generic;
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
        private bool? lastAuthenticated;

        private readonly List<RectTransform> shiftedSiblings = new();
        private float collapsedOffset;
        private bool isCollapsed;

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

        private void OnDisable()
        {
            StopListening();
            lastAuthenticated = null;
        }

        private void LateUpdate()
        {
            // onSignedIn can fire before IsAuthenticated turns true (sessionActive/init flags set later),
            // so the event alone leaves the pills hidden; this cached check catches the real transition.
            var isAuthenticated = gameManager is { IsAuthenticated: true };
            if (lastAuthenticated == isAuthenticated)
                return;

            if (isAuthenticated)
                StartListening();
            else
                StopListening();

            UpdateUI();
        }

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
            lastAuthenticated = isAuthenticated;

            SetPillsVisible(isAuthenticated);

            SetValue(gamesPlayedTodayLabel, data?.gamesPlayedToday);
            SetValue(usersPlayingNowLabel, data?.usersPlayingNow);
            SetValue(activePlayersLabel, isAuthenticated && gameManager.ActivePlayers >= 0 ? gameManager.ActivePlayers : null);
        }

        // Pills are children of this row. Toggle them instead of the row so this component keeps
        // receiving sign in/out callbacks and OnEnable while the stats are hidden.
        private void SetPillsVisible(bool visible)
        {
            foreach (Transform pill in transform)
            {
                if (pill.gameObject.activeSelf != visible)
                    pill.gameObject.SetActive(visible);
            }

            SetRowCollapsed(!visible);
        }

        // Dashboard content uses fixed top-anchored positions (no layout group), so the gap left by the
        // hidden pills is closed by hand: siblings below the row move up and the content shrinks by the
        // same amount. Original layout is restored when the row expands again.
        private void SetRowCollapsed(bool collapse)
        {
            if (collapse == isCollapsed || transform is not RectTransform row || row.parent is not RectTransform content)
                return;

            if (collapse)
            {
                var rowTop = row.anchoredPosition.y;
                float? nextTop = null;
                shiftedSiblings.Clear();

                foreach (RectTransform sibling in content)
                {
                    if (sibling == row || sibling.anchoredPosition.y >= rowTop)
                        continue;

                    shiftedSiblings.Add(sibling);
                    if (!nextTop.HasValue || sibling.anchoredPosition.y > nextTop.Value)
                        nextTop = sibling.anchoredPosition.y;
                }

                // Space from the row top to the next section top (row height + spacing)
                collapsedOffset = nextTop.HasValue ? rowTop - nextTop.Value : row.rect.height;
            }

            var offset = collapse ? collapsedOffset : -collapsedOffset;
            foreach (var sibling in shiftedSiblings)
            {
                if (sibling)
                    sibling.anchoredPosition += new Vector2(0f, offset);
            }

            content.sizeDelta -= new Vector2(0f, offset);
            isCollapsed = collapse;
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
