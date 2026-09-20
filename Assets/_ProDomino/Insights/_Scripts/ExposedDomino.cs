using DominoTemplate.Core;

namespace ProDomino.Insights
{
    public class ExposedDomino : Domino
    {
        public void Configure(int id, int topIndex, int bottomIndex, bool available, bool portraitOrientation)
        {
            this.id = id;

            TopIndex = topIndex;
            BottomIndex = bottomIndex;
            Available = available;
            PortraitOrientation = portraitOrientation;
        }
    }
}
