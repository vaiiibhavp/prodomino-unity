using System;

namespace ProDomino.Insights
{
    [Flags]
    public enum TargetType
    {
        None = 0,
        Player = 1 << 0,
        AI = 1 << 1,
    }
}
