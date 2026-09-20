using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Leaderboard
{
    public class TempUpdatePlayerLeaderboardScoreController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup cheaterCanvasGroup;
        [SerializeField] private TMP_Dropdown gameModeDropDown;
        [SerializeField] private TMP_Dropdown numberPlayerDropDown;
        [SerializeField] private TMP_InputField resultPositionInputfield;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button updateScoreButton;
        private int resultPosition;
        private LeaderboardManager leaderboardManager;
        private GameMode gameMode;
        private NumberPlayers numberPlayers;

        private void Start()
        {
            leaderboardManager = ServiceLocator.Instance.GetService<LeaderboardManager>();
            
            if (updateScoreButton)
                updateScoreButton.onClick.AddListener(UpdatePlayerLeaderboardScore);

            if (cancelButton)
                cancelButton.onClick.AddListener(HideCheaterCanvasGroup);

            if (gameModeDropDown)
            {
                gameModeDropDown.ClearOptions();
                gameModeDropDown.AddOptions(Enum.GetNames(typeof(GameMode)).ToList());
                gameModeDropDown.onValueChanged.AddListener(delegate
                {
                    gameMode = (GameMode)gameModeDropDown.value;
                });
            } else
                Debug.LogWarning("GameMode dropdown is not assigned. Please assign it in the inspector.");

            if (numberPlayerDropDown)
            {
                numberPlayerDropDown.ClearOptions();
                numberPlayerDropDown.AddOptions(Enum.GetNames(typeof(NumberPlayers)).ToList());
                numberPlayerDropDown.onValueChanged.AddListener(delegate
                {
                    numberPlayers = (NumberPlayers)numberPlayerDropDown.value;
                });
            } else
                Debug.LogWarning("NumberPlayers dropdown is not assigned. Please assign it in the inspector.");

            if (resultPositionInputfield)
                resultPositionInputfield.onValueChanged.AddListener(delegate
                {
                    var isParsed = int.TryParse(resultPositionInputfield.text, out resultPosition);
                    if (!isParsed)
                    {
                        Debug.LogWarning("Invalid input for result position. Please enter a valid integer.");
                        resultPosition = 0; // Reset to default value if parsing fails
                    }
                });
            else
                Debug.LogWarning("Player winner input field is not assigned. Please assign it in the inspector.");
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!leaderboardManager.IsAlreadyInitialized)
                return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                if (Input.GetKeyDown(KeyCode.L))
                    SetActiveCheaterCanvasGroup(cheaterCanvasGroup.alpha is not 1);
#endif
        }

        private void SetActiveCheaterCanvasGroup(bool isActive)
        {
            if (cheaterCanvasGroup != null && cheaterCanvasGroup.gameObject.activeInHierarchy)
                cheaterCanvasGroup.SetActive(isActive);
        }

        private async void UpdatePlayerLeaderboardScore()
        {
            if (!leaderboardManager.IsAlreadyInitialized)
            {
                Debug.LogWarning("LeaderboardManager is not initialized yet.");
                return;
            }

            if (gameMode is GameMode.none || numberPlayers is NumberPlayers.none or NumberPlayers.twoVsTwo)
            {
                Debug.LogWarning("Game mode or number of players is not selected. Please select valid options.");
                return;
            }

            if (cheaterCanvasGroup.alpha is not 1)
            {
                Debug.LogWarning("Cheater Canvas Group is not active. Please activate it to update the leaderboard.");
                return;
            }

            // DUMMY TEST:: only test if the player score is updated correctly
            await leaderboardManager.UpdateLeadeboardResult(gameMode, numberPlayers, resultPosition: resultPosition, matchEMC: 0);
        }

        /// <summary>
        /// Use in button to hide the cheater canvas group.
        /// </summary>
        private void HideCheaterCanvasGroup()
        {
            if (cheaterCanvasGroup)
                cheaterCanvasGroup.SetActive(false);
        }
    }
}
