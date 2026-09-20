using ProDomino.GameSystem;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Shop
{
    /// <summary>
    /// Header coin chip: shows the player's token balance and opens the Shop when pressed.
    /// </summary>
    public class HeaderTokenChip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text amountLabel;
        [SerializeField] private Button button;
        [Tooltip("toggleID of the navigation button to open when the chip is pressed.")]
        [SerializeField] private string navigationButtonId = "Shop";
        [SerializeField] private float refreshInterval = 0.5f;

        private GameManager gameManager;
        private uint? shownAmount;
        private float nextRefreshTime;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            if (!gameManager)
            {
                Debug.LogError("[HeaderTokenChip] GameManager service is not available.");
                return;
            }

            button?.onClick.AddListener(OpenShop);
            gameManager.HandleOnSignIn(OnSignIn);
            gameManager.HandleOnSignOut(OnSignOut);

            if (gameManager.IsAlreadyInitialized && gameManager.IsAuthenticated)
                OnSignIn();
            else
                OnSignOut();
        }

        private void OnDestroy()
        {
            gameManager?.UnHandleOnSignIn(OnSignIn);
            gameManager?.UnHandleOnSignOut(OnSignOut);
        }

        // The balance is refreshed by several systems (shop purchases, match rewards, ads), none of
        // which raise a shared event, so the chip simply re-reads the cached collection.
        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshAmount();
        }

        private void OnSignIn()
        {
            canvasGroup?.SetActive(gameManager.IsAuthenticated);
            RefreshAmount();
        }

        private void OnSignOut()
        {
            canvasGroup?.SetActive(false);
            shownAmount = null;
        }

        private void RefreshAmount()
        {
            if (!amountLabel || gameManager is not { IsAuthenticated: true })
                return;

            uint amount = 0;
            if (gameManager.PlayerCurrencyCollection is { } currencies && currencies.TryGetValue(Currency.Token, out var tokens))
                amount = tokens;

            if (shownAmount == amount)
                return;

            shownAmount = amount;
            amountLabel.text = amount.ToString("N0");
        }

        private void OpenShop()
        {
            var navigationButton = FindObjectsByType<CustomButtonUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.CustomButtonID == navigationButtonId);

            if (navigationButton)
                navigationButton.HandleClick();
            else
                Debug.LogWarning($"[HeaderTokenChip] Navigation button '{navigationButtonId}' not found.");
        }
    }
}
