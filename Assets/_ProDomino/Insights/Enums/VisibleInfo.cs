using System;

namespace ProDomino.Insights
{
    [Flags, Serializable]
    public enum VisibleInfo
    {
        None = 0,
        MyHand = 1 << 0,
        OpponentHand = 1 << 1,
        Boneyard = 1 << 2,
        Board = 1 << 3,

        Basic = MyHand | Board,
        All = MyHand | OpponentHand | Boneyard | Board
    }
}
