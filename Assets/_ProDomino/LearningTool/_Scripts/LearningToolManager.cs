using ProDomino.Authentication;
using ProDomino.Shared;
using Timba.Patterns;
using UnityEngine;

namespace ProDomino.LearningTool
{
    public class LearningToolManager : SingleInstanceMonoBehaviour<LearningToolManager>, IService
    {
        public bool IsAlreadyInitialized { get; private set; }
        public GameMode SelectedGameMode { get; private set; }
        public GameModeDelegate OnGameModeSelected { get; private set; }

        private LearningToolUI _learningToolUI;
        internal LearningToolUI LearningToolUI
        {
            get
            {
                if (_learningToolUI == null)
                {
                    _learningToolUI = FindFirstObjectByType<LearningToolUI>();
                    if (_learningToolUI == null)
                        Debug.LogWarning($"{nameof(LearningTool.LearningToolUI)} not found in the scene");
                }

                return _learningToolUI;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            OnGameModeSelected = new GameModeDelegate(DetermineGameMode);
        }

        /// <summary>
        /// Determines the game mode based on the provided argument.
        /// </summary>
        /// <param name="gameMode"></param>
        /// <returns></returns>
        private GameMode DetermineGameMode(GameMode? gameMode)
        {
            // If the argument is not null, set the selected game mode to the new value
            if (gameMode is not null) 
                SelectedGameMode = gameMode.Value;

            return SelectedGameMode;
        }

        public delegate GameMode GameModeDelegate(GameMode? newGameMode = null);
    }
}
