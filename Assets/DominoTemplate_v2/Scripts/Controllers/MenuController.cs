using ProDomino.Shared;
using UnityEngine;

namespace DominoTemplate.Controllers
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas = null;
        [SerializeField] private GameControler _allGameStuffPrefab = null;
        [SerializeField] private RectTransform _entireMainMenuStuff = null;
        [SerializeField] private RectTransform _mainMenu = null;
        [SerializeField] private RectTransform _chooseDifficulty = null;
        [SerializeField] private RectTransform _inGameMenuStuff = null;
        [SerializeField] private RectTransform _inGameMenu = null;

        [SerializeField] private GameMode gameModeSelectedID = GameMode.none;
        [SerializeField] private GameType gameTypeSelectedID = GameType.none;
        [SerializeField] private NumberPlayers vsPlayerSelectedID = NumberPlayers.none;

        private RectTransform _currentGameHolder;
        private GameControler _currentGameScript;

        private int _difficulty;
        private bool _isOpenMenu;

        private void CloseAllMainMenus()
        {
            _mainMenu.gameObject.SetActive(false);
            _chooseDifficulty.gameObject.SetActive(false);
        }

        public void OpenMainMenu()
        {
            CloseAllMainMenus();
            _mainMenu.gameObject.SetActive(true);

            gameModeSelectedID = GameMode.none;
            gameTypeSelectedID = GameType.none;
            vsPlayerSelectedID = NumberPlayers.none;
        }

        public void OpenDifficultyMenu()
        {
            CloseAllMainMenus();
            _chooseDifficulty.gameObject.SetActive(true);
        }

        public void InGameMenuActivate()
        {
            _isOpenMenu = !_isOpenMenu;
            _inGameMenu.gameObject.SetActive(_isOpenMenu);
        }

        public void SetDifficulty(int selectedDifficulty)
        {
            _difficulty = selectedDifficulty;
            StartDomino();
        }

        private void EndGame()
        {
            CloseAllMainMenus();

            if (_currentGameHolder != null)
                Destroy(_currentGameHolder.gameObject);
        }

        public void RestartDomino()
        {
            EndGame();
            // This will restart inGameMenu;
            _isOpenMenu = false;
            _inGameMenu.gameObject.SetActive(_isOpenMenu);

            StartDomino();
        }

        public void ToLobby()
        {
            EndGame();
            // This will close inGameMenu;
            _isOpenMenu = false;
            _inGameMenu.gameObject.SetActive(_isOpenMenu);
            _inGameMenuStuff.gameObject.SetActive(false);
            _entireMainMenuStuff.gameObject.SetActive(true);
            OpenMainMenu();
        }


        private void StartDomino()
        {
            // this turns off main menu stuff
            _entireMainMenuStuff.gameObject.SetActive(false);
            _inGameMenuStuff.gameObject.SetActive(true);

            _currentGameScript = Instantiate(_allGameStuffPrefab);
            _currentGameHolder = _currentGameScript.GetRect();

            _currentGameHolder.SetParent(_canvas, false);
            _currentGameHolder.localScale = Vector3.one;
            _currentGameHolder.offsetMin = new Vector2(0, 0);
            _currentGameHolder.offsetMin = new Vector2(0, 0);

            _currentGameScript.RestartGame(_difficulty, gameModeSelectedID, gameTypeSelectedID, vsPlayerSelectedID);
        }

        public void SetGameModeData(GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID)
        {
            gameModeSelectedID = gameModeID;
            gameTypeSelectedID = gameTypeID;
            vsPlayerSelectedID = vsPlayerID;
        }
    }
}