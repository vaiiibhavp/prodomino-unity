using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using ProDomino.Shared;

namespace ProDomino.ReplaySystem
{
    [Serializable]
    public class DominoPieceData {
        public int tileId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector2 sizeDelta;
        public string sideInfo; // "right", "left", "top", "down"
    }

    [Serializable]
    public class PlayerHandData {
        public int playerIndexId;
        public List<int> handPieceIds; 
    }

    [Serializable]
    public class TurnData
    {
        public int turnNumber;
        public int roundNumber;
        public int playerIndexId;
        public List<PlayerHandData> playersHands;
        public List<DominoPieceData> boardPieces = new List<DominoPieceData>();
        public DominoPieceData playedPiece;
        public List<int> tilesTakenFromBoneyard = new List<int>(); // index 0 = score playerIndexId -> 0, index 1 = score playerIndexId -> 1, etc
        public List<int> roundPlayerScores; // index 0 = score playerIndexId -> 0, index 1 = score playerIndexId -> 1, etc
        public List<int> cumulatePlayerScores; // index 0 = score playerIndexId -> 0, index 1 = score playerIndexId -> 1, etc
        public List<int> boneyardPieceIds;
        public TurnActionReplay turnAction = TurnActionReplay.none; //"play", 0120"takeBoneyard0",. "pass"
        //public List<TurnActionReplay> turnActions; //= TurnActionReplay.none; //"play", 0120"takeBoneyard0",. "pass"
        public TurnResultReplay turnResult = TurnResultReplay.none; //"gameOver", "gameblocked"
        public int roundWinnerPlayerIndexId = -1; // only if gameover
        public bool isTie = false;
        public bool isEndGame = false; // only if gameover
        public bool dealHands = false; // Indica si en este turno se repartieron las manos (turno 1 de cada ronda)
        public float turnTimer = 0;
        public float timeOnTimeline = 0;
        public TurnSlotHelper turnSlotHelper;
    }

    [Serializable]
    public class PlayerInfoReplay {
        public int playerIndexId;
        public string playerName;
    }

    [Serializable]
    public class MatchReplay
    {
        public string matchId;
        public string date;
        public GameMode gameMode; //"french", "block", "draw", etc
        public GameType gameType; //"singlePlayerIA", "casual", "competitive"
        public NumberPlayers numberPlayers; //"oneVsOne", "oneVsThree", "twoVsTwo", "solo"
        public PlayerInfoReplay player_0;
        public PlayerInfoReplay player_1;
        public PlayerInfoReplay player_2;
        public PlayerInfoReplay player_3;
        public int localPlayerIndexId = -1;
        public int winnerPlayerIndexId = -1;
        public List<TurnData> turns = new List<TurnData>();
        public int saveSlotIndex = 0; // Indica el índice del slot donde se guardó la partida (1-10), 0 es el valor default antes de ser cargado
    }

    [Serializable]
    public enum TurnActionReplay
    {
        none,
        play,
        takeBoneyard,
        pass
    }

    [Serializable]
    public enum TurnResultReplay
    {
        none,
        gameOver,
        gameblocked
    }

    [Serializable]
    public class TurnSlotHelper
    {
        public int sideLimitLeftRight = 12;
        public int sideLimitTopDown = 4;
        public float tileOffset = 60;
        public float lyingOffset = 30;
        public float doubleOffset = 0;
        public float tileDist_hor = 2;

        public float _rightTiles_Hor = 0;
        public float _rightTiles_Ver = 0;
        public int _rightPhase = 0;
        public int _rightNum = 0;

        public float _leftTiles_Hor = 0;
        public float _leftTiles_Ver = 0;
        public int _leftPhase = 0;
        public int _leftNum = 0;

        public float _topTiles_Hor = 0;
        public float _topTiles_Ver = 0;
        public int _topPhase = 0;
        public int _topNum = 0;

        public float _downTiles_Hor = 0;
        public float _downTiles_Ver = 0;
        public int _downPhase = 0;
        public int _downNum = 0;
        public bool started = false;
        public bool auxStarted = false;
        public int firstPlacedTileId = -1;
    }
}
