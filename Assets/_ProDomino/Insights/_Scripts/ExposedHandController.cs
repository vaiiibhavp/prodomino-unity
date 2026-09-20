using DominoTemplate.Controllers;
using System.Collections.Generic;
using System.Linq;

namespace ProDomino.Insights
{
    public class ExposedHandController : HandController
    {
        public (int rightNum, int leftNum, int topNum, int downNum, List<int>) GetBranchNumbers()
        {
            var rightNum = -1;
            var leftNum = -1;

            var topNum = -1;
            var downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            return (rightNum, leftNum, topNum, downNum, doubleTilesEnabled);
        }

        public int HandAvailableTilesCount(byte index)
        {
            var rightNum = -1;
            var leftNum = -1;

            var topNum = -1;
            var downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            var i = 0;

            var tileList = _deckScript.GetList(index).Where(x => !x.GetDominoView().IsOnTable())?.ToList();
            int unlocked = 0;

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            while (i < tileList.Count)
            {
                var dominoView = tileList[i].GetDominoView();
                var dominoInfo = dominoView.GetDomino();

                if (dominoInfo.TopIndex == rightNum || dominoInfo.BottomIndex == rightNum
                    || dominoInfo.TopIndex == leftNum || dominoInfo.BottomIndex == leftNum
                    || dominoInfo.TopIndex == topNum || dominoInfo.BottomIndex == topNum
                    || dominoInfo.TopIndex == downNum || dominoInfo.BottomIndex == downNum)
                {
                    dominoView.UnLockTile(true);
                    unlocked++;
                } else if (rightNum == -1 || leftNum == -1 || topNum == -1 || downNum == -1)
                {
                    dominoView.UnLockTile(true);
                    unlocked++;
                }

                foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                {
                    if (dominoInfo.TopIndex == aux && dominoInfo.BottomIndex == aux)
                    {
                        dominoView.UnLockTile(true);
                        unlocked++;
                        break;
                    }
                }

                i++;
            }

            return unlocked;
        }
    }
}
