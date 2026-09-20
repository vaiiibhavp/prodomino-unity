using DominoTemplate.Controllers;
using DominoTemplate.DragAndDrop;
using ProDomino.Shared;
using System.Collections.Generic;
using UnityEngine;

namespace ProDomino.Insights
{
    public interface IExposedGameController
    {
        GameObject gameObject { get; }
        public GameMode GameMode { get; }
        public GameTurnController GameTurnController { get; }
        public List<DragHandler> BoardTiles { get; }
        public List<DragHandler> BoneyardTiles { get; }
        public List<DragHandler> PlayerTiles { get; }
        public List<DragHandler> AITiles { get; }

        void RestartGame(int difficulty, GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID);
        RectTransform GetRect();

        double CalculateWinningProbability(bool isPlayer, VisibleInfo visibleInfo);
        double CalculateTileDrawProbability(TargetType targetType, (byte value1, byte value2) tileToSearch);
        double CalculateBlockingProbability(VisibleInfo visibleInfo);
        double CalculateTieProbability(VisibleInfo visibleInfo);
    }
}
