using Cysharp.Threading.Tasks;
using ProDomino.Authentication;
using Timba.Patterns;
using TMPro;
using UnityEngine;

namespace ProDomino.GameSystem
{
    public class GlobalAnalyticsController : MonoBehaviour
    {
        [SerializeField] private TMP_Text gamesPlayedTodayLabel;
        [SerializeField] private TMP_Text usersPlayingNowLabel;

        [Header("Container References")]
        [SerializeField] private GameObject gamesPlayedTodayContainer;
        [SerializeField] private GameObject usersPlayingNowContainer;

        [Header("ExternalReferences")]
        [SerializeField] private TMP_Text blockPlayersLabel;
        [SerializeField] private TMP_Text frenchPlayersLabel;
        [SerializeField] private TMP_Text drawPlayersLabel;
        [SerializeField] private TMP_Text fivePlayersLabel;
        [SerializeField] private TMP_Text concentratePlayersLabel;


        private GameManager gameManager;
        private AuthManager authManager;

        private GlobalAnalyticsData GlobalAnalyticsData => gameManager?.GlobalAnalyticsData;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();

            // Add listener for sign in event
            gameManager?.HandleOnSignIn(OnSignIn);

            // Add listener for sign out event
            gameManager?.HandleOnSignOut(OnSignOut);
        }

        private void Start()
        {
            UpdateUI();
        }

        private void UpdateUI() => UpdateUI(null);
        private void UpdateUI(bool? valueForced = null)
        {
            var hasValue = valueForced ?? (gameManager?.IsAuthenticated ?? false) && GlobalAnalyticsData is not null;

            if (gamesPlayedTodayLabel)
                gamesPlayedTodayLabel.text = (hasValue ? GlobalAnalyticsData?.gamesPlayedToday ?? 0 : 0).ToString();
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(gamesPlayedTodayLabel)}");

            if (usersPlayingNowLabel)
                usersPlayingNowLabel.text = (hasValue ? GlobalAnalyticsData?.usersPlayingNow ?? 0 : 0).ToString();
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(usersPlayingNowLabel)}");


            if (gamesPlayedTodayContainer)
                gamesPlayedTodayContainer.SetActive(hasValue);
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(gamesPlayedTodayContainer)}");
            
            if (usersPlayingNowContainer)
                usersPlayingNowContainer.SetActive(hasValue);
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(usersPlayingNowContainer)}");


            if (blockPlayersLabel)
            { 
                blockPlayersLabel.text = $"{(hasValue ? GlobalAnalyticsData?.block ?? 0 : 0)} online now";
                blockPlayersLabel.gameObject.SetActive(hasValue);
            }
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(blockPlayersLabel)}");

            if (frenchPlayersLabel)
            { 
                frenchPlayersLabel.text = $"{(hasValue ? GlobalAnalyticsData?.french ?? 0 : 0)} online now";
                frenchPlayersLabel.gameObject.SetActive(hasValue);
            } 
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(frenchPlayersLabel)}");

            if (drawPlayersLabel)
            { 
                drawPlayersLabel.text = $"{(hasValue ? GlobalAnalyticsData?.draw ?? 0 : 0)} online now";
                drawPlayersLabel.gameObject.SetActive(hasValue);
            }
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(drawPlayersLabel)}");

            if (fivePlayersLabel)
            { 
                fivePlayersLabel.text = $"{(hasValue ? GlobalAnalyticsData?.five ?? 0 : 0)} online now";
                fivePlayersLabel.gameObject.SetActive(hasValue);
            }
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(fivePlayersLabel)}");

            if (concentratePlayersLabel)
            { 
                concentratePlayersLabel.text = $"{(hasValue ? GlobalAnalyticsData?.concentrate ?? 0 : 0)} online now";
                concentratePlayersLabel.gameObject.SetActive(hasValue);
            }
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] Missing reference: {nameof(concentratePlayersLabel)}");
        }

        private void OnSignIn()
        {
            if (gameManager is not null and { IsAlreadyInitialized: true, IsAuthenticated: true })
            { 
                Debug.Log($"[{nameof(GlobalAnalyticsController)}] Registering Global Analytics UI update listener after sign in");
                gameManager.AddListenerWhenUpdateGlobalAnalytics(UpdateUI);
                UpdateUI();
            }
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] GameManager is not properly initialized or user is not authenticated on sign in.");
        }

        private void OnSignOut()
        {
            if (gameManager)
            {
                Debug.Log($"[{nameof(GlobalAnalyticsController)}] Removing Global Analytics UI update listener after sign out");
                gameManager.RemoveListenerWhenUpdateGlobalAnalytics(UpdateUI);
                UpdateUI(false);
            } 
            else
                Debug.LogWarning($"[{nameof(GlobalAnalyticsController)}] GameManager reference is missing on sign out.");
        }
    }
}
