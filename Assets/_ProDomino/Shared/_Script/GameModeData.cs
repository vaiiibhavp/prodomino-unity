using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Events;

namespace ProDomino.Shared
{
    [System.Serializable]
    public class GameModeData
    {
        public GameMode gameMode;
        public GameType gameType;
        public NumberPlayers NumberPlayers;
        public ConcentrateNumberOfTiles concentrateNumberOfTiles;

        public GameModeData()
        {
        }

        public GameModeData(GameMode gameMode, GameType gameType, NumberPlayers numberPlayers, ConcentrateNumberOfTiles concentrateNumberOfTiles)
        {
            this.gameMode = gameMode;
            this.gameType = gameType;
            NumberPlayers = numberPlayers;
            this.concentrateNumberOfTiles = concentrateNumberOfTiles;
        }
    }

    [System.Serializable]
    public class GameModeDataEvent : UnityEvent<GameModeData> { }
}
