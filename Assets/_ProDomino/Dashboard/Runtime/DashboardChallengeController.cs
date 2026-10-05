using System.Collections.Generic;
using System.Linq;
using System.Text;
using HelperSharedLibrary;
using ProDomino.GameSystem;
using ProDomino.MissionSystem;
using Timba.Patterns;
using UnityEngine;

namespace ProDomino.Dashboard
{
    /// <summary>
    /// Feeds the dashboard challenge banner with the signed in player's weekly and daily missions
    /// from <see cref="MissionManager"/>. Each period shows its first unclaimed mission with a
    /// progress bar, count and reward. Signed out (or before missions load) the banner falls back
    /// to the slides authored in the prefab, without the progress row.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DashboardBannerCarousel))]
    public class DashboardChallengeController : MonoBehaviour
    {
        [SerializeField] private DashboardBannerCarousel carousel;

        [Header("Backgrounds")]
        [SerializeField] private Sprite weeklyBackground;
        [SerializeField] private Sprite dailyBackground;

        [Header("Copy")]
        [SerializeField] private string weeklyTitle = "Complete Your\nWeekly Challenge!!";
        [SerializeField] private string dailyTitle = "Complete Your\nDaily Challenge!!";
        [SerializeField] private string subtitleFormat = "{0} and earn big rewards!";

        [Tooltip("Seconds between checks of the mission data. Mission progress changes in place, without an event.")]
        [SerializeField] private float refreshInterval = 0.5f;

        private readonly List<DashboardBannerCarousel.Slide> slides = new();
        private GameManager gameManager;
        private MissionManager missionManager;
        private string lastSignature;
        private float refreshTimer;

        private void Awake()
        {
            if (!carousel)
                carousel = GetComponent<DashboardBannerCarousel>();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
        }

        private void OnEnable()
        {
            refreshTimer = 0f;
            Refresh();
        }

        private void Update()
        {
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < refreshInterval)
                return;

            refreshTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (!carousel)
                return;

            BuildSlides();

            var signature = BuildSignature();
            if (signature == lastSignature)
                return;

            var wasShowingChallenges = !string.IsNullOrEmpty(lastSignature);
            lastSignature = signature;

            if (slides.Count is 0)
                carousel.ResetSlides();
            else if (wasShowingChallenges)
                carousel.UpdateSlides(slides);
            else
                carousel.SetSlides(slides);
        }

        private void BuildSlides()
        {
            slides.Clear();

            if (gameManager is not { IsAuthenticated: true })
                return;

            if (!missionManager)
                missionManager = ServiceLocator.Instance.GetService<MissionManager>();

            if (!missionManager)
                return;

            TryAddSlide(missionManager.WeeklyMissionsDataCollection, weeklyBackground, weeklyTitle);
            TryAddSlide(missionManager.DailyMissionsDataCollection, dailyBackground, dailyTitle);
        }

        private void TryAddSlide(Dictionary<PlayerMissionData, GameMissionData> missions, Sprite background, string title)
        {
            var regularMissions = missions?.Where(x => x.Key is { isBonus: false } && x.Value is not null).ToList();
            if (regularMissions is null or { Count: 0 })
                return;

            // First mission still to claim. Once every mission is claimed the last one stays, full
            var (player, game) = regularMissions.FirstOrDefault(x => !x.Key.claimed) is { Key: not null } pending
                ? pending
                : regularMissions[regularMissions.Count - 1];

            var goal = Mathf.Max(0, game.goalAmount);
            var progress = player.claimed ? goal : (int)Mathf.Min(player.progress, goal);

            slides.Add(new DashboardBannerCarousel.Slide
            {
                background = background,
                title = title,
                subtitle = string.Format(subtitleFormat, (game.name ?? string.Empty).TrimEnd('.', '!', ' ')),
                showProgress = true,
                progress = goal > 0 ? (float)progress / goal : 1f,
                progressText = $"{progress}/{goal}",
                rewardText = game.rewardAmount.ToString(),
            });
        }

        private string BuildSignature()
        {
            if (slides.Count is 0)
                return null;

            var builder = new StringBuilder();
            foreach (var slide in slides)
                builder.Append(slide.title).Append('|').Append(slide.subtitle).Append('|')
                       .Append(slide.progressText).Append('|').Append(slide.rewardText).Append(';');

            return builder.ToString();
        }
    }
}
