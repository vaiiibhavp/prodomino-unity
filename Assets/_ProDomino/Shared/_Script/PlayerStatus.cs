using UnityEngine;

namespace ProDomino.Shared
{
    [System.Serializable]
    public class PlayerStatus
    {
        public bool isTurn;
        public bool isEliminated;
        public int score;
    }
}
