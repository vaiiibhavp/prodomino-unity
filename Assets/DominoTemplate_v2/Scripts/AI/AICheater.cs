using System.Collections.Generic;
using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using UnityEngine;

namespace DominoTemplate.AI
{
    public class AICheater : MonoBehaviour
    {
        private DeckController _deckScript;
        private SlotHelper _slotPosScript;
        private AIController _aiController;

        private int _difficulty;

        public void SetData(DeckController newDeckScript,
            SlotHelper newSlotPosScript, AIController newAIController, int newDifficulty)
        {
            _deckScript = newDeckScript;
            _slotPosScript = newSlotPosScript;
            _aiController = newAIController;
            _difficulty = newDifficulty;
        }

        private void SeparateTiles(List<DragHandler> hayTiles, List<DragHandler> nextTiles,
            ref List<DragHandler> goodTiles, ref List<DragHandler> badTiles)
        {
            int i;
            int leftNum = -1;
            int rightNum = -1;

            int topNum = -1;
            int downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);

            goodTiles = new List<DragHandler>();
            badTiles = new List<DragHandler>();

            i = 0;
            while (i < hayTiles.Count)
            {
                DominoView dominoView = hayTiles[i].GetDominoView();
                Domino dominoInfo = dominoView.GetDomino();

                if (dominoInfo.TopIndex == leftNum || dominoInfo.TopIndex == rightNum || dominoInfo.TopIndex == topNum || dominoInfo.TopIndex == downNum)
                {
                    goodTiles.Add(hayTiles[i]);

                    /*int check = 0;
                    while (check < nextTiles.Count)
                    {
                        DominoView checkDominoView = nextTiles[check].GetDominoView();
                        Domino checkDominoInfo = checkDominoView.GetDomino();

                        if (checkDominoInfo.TopIndex == dominoInfo.BottomIndex
                            || checkDominoInfo.BottomIndex == dominoInfo.BottomIndex)
                        {
                            goodTiles.Add(hayTiles[i]);
                            break;
                        }

                        check++;
                    }

                    if (check >= nextTiles.Count)
                        badTiles.Add(hayTiles[i]);*/
                }
                else if (dominoInfo.BottomIndex == leftNum || dominoInfo.BottomIndex == rightNum || dominoInfo.BottomIndex == topNum || dominoInfo.BottomIndex == downNum)
                {
                    goodTiles.Add(hayTiles[i]);

                    /*int check = 0;
                    while (check < nextTiles.Count)
                    {
                        DominoView checkDominoView = nextTiles[check].GetDominoView();
                        Domino checkDominoInfo = checkDominoView.GetDomino();

                        if (checkDominoInfo.TopIndex == dominoInfo.TopIndex
                            || checkDominoInfo.BottomIndex == dominoInfo.TopIndex)
                        {
                            goodTiles.Add(hayTiles[i]);
                            break;
                        }

                        check++;
                    }

                    if (check >= nextTiles.Count)
                        badTiles.Add(hayTiles[i]);*/
                }

                foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                {
                    if (dominoInfo.TopIndex == aux && dominoInfo.BottomIndex == aux)
                    {
                        goodTiles.Add(hayTiles[i]);
                        
                        /*Debug.Log("---> " + "a 01: " + dominoInfo.TopIndex);
                        int check = 0;
                        while (check < nextTiles.Count)
                        {
                            DominoView checkDominoView = nextTiles[check].GetDominoView();
                            Domino checkDominoInfo = checkDominoView.GetDomino();

                            if (checkDominoInfo.TopIndex == dominoInfo.TopIndex
                                || checkDominoInfo.BottomIndex == dominoInfo.TopIndex)
                            {
                                Debug.Log("---> " + "a 02: " + dominoInfo.TopIndex);
                                goodTiles.Add(hayTiles[i]);
                                break;
                            }

                            check++;
                        }

                        if (check >= nextTiles.Count)
                        {
                            badTiles.Add(hayTiles[i]);
                            Debug.Log("---> " + "a 03: " + dominoInfo.TopIndex);
                        }*/

                        break;
                    }
                }

                i++;
            }
        }


        public DragHandler PickHardTile(List<DragHandler> thisTiles,
            List<DragHandler> nextTiles, bool nextPlayer)
        {
            List<DragHandler> toThrowTiles;
            List<DragHandler> goodTiles = null;
            List<DragHandler> badTiles = null;

            toThrowTiles = _aiController.GetMatchingTiles(thisTiles);
            SeparateTiles(toThrowTiles, nextTiles, ref goodTiles, ref badTiles);

            // For Easy difficult
            if (_difficulty == 0)
            {
                return GoodBot(toThrowTiles, badTiles, nextPlayer);
            }

            // For Hard difficult
            if (goodTiles.Count > 0)
                if (_difficulty == 2 && !nextPlayer)
                {
                    Debug.Log("Dropped Good Tile");
                    return goodTiles[UnityEngine.Random.Range(0, goodTiles.Count)];
                }

            if (badTiles.Count > 0)
                if (_difficulty == 2 && nextPlayer)
                {
                    Debug.Log("Dropped Bad Tile");
                    return badTiles[UnityEngine.Random.Range(0, badTiles.Count)];
                }


            Debug.Log("Could not find Proper Dominos (means: Random Tile or Pass)");
            if (toThrowTiles.Count == 0)
                return null;
            int rand = UnityEngine.Random.Range(0, toThrowTiles.Count);
            return toThrowTiles[rand];
        }


        private DragHandler LogicForGoodTile(List<DragHandler> goodTiles)
        {
            int i;
            DragHandler toDrop = null;

            i = 0;
            while (i < goodTiles.Count)
            {
                DominoView checkDominoView = goodTiles[i].GetDominoView();
                Domino checkDominoInfo = checkDominoView.GetDomino();

                // Check if double
                //if (checkDominoInfo.TopIndex != checkDominoInfo.BottomIndex) //HERE
                //{
                    toDrop = goodTiles[i];
                    break;
                //}

                i++;
            }

            return toDrop;
        }


        private bool CausePass(List<DragHandler> tempTiles, DragHandler checkTile)
        {
            int leftNum = -1;
            int rightNum = -1;

            int topNum = -1;
            int downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            int i;

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            if (checkTile == null)
            {
                // Means pass;

                if (leftNum == -1 || rightNum == -1 || topNum == -1 || downNum == -1)
                    return false;

                i = 0;
                while (i < tempTiles.Count)
                {
                    DominoView tempDominoView = tempTiles[i].GetDominoView();
                    Domino tempDominoInfo = tempDominoView.GetDomino();

                    if (tempDominoInfo.TopIndex == leftNum || tempDominoInfo.TopIndex == rightNum
                                                           || tempDominoInfo.BottomIndex == leftNum ||
                                                           tempDominoInfo.BottomIndex == rightNum
                                                           || tempDominoInfo.TopIndex == downNum || tempDominoInfo.TopIndex == topNum
                                                           || tempDominoInfo.BottomIndex == downNum ||
                                                           tempDominoInfo.BottomIndex == topNum)
                    {
                        return false;
                    }

                    foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                    {
                        if (tempDominoInfo.TopIndex == aux && tempDominoInfo.BottomIndex == aux)
                        {
                            return false;
                        }
                    }

                    i++;
                }

                return true;
            }

            DominoView checkDominoView = checkTile.GetDominoView();
            Domino checkDominoInfo = checkDominoView.GetDomino();

            i = 0;
            while (i < tempTiles.Count)
            {
                DominoView tempDominoView = tempTiles[i].GetDominoView();
                Domino tempDominoInfo = tempDominoView.GetDomino();

                if (leftNum == -1 || rightNum == -1)
                {
                    if (tempDominoInfo.TopIndex == checkDominoInfo.BottomIndex
                        || tempDominoInfo.BottomIndex == checkDominoInfo.BottomIndex
                        || tempDominoInfo.TopIndex == checkDominoInfo.TopIndex
                        || tempDominoInfo.BottomIndex == checkDominoInfo.TopIndex)
                        return false;
                }

                if (tempDominoInfo.TopIndex == leftNum
                    || tempDominoInfo.BottomIndex == leftNum)
                {
                    if (checkDominoInfo.TopIndex == rightNum)
                        if (tempDominoInfo.TopIndex == checkDominoInfo.BottomIndex
                            || tempDominoInfo.BottomIndex == checkDominoInfo.BottomIndex)
                            return false;
                    if (checkDominoInfo.BottomIndex == rightNum)
                        if (tempDominoInfo.TopIndex == checkDominoInfo.TopIndex
                            || tempDominoInfo.BottomIndex == checkDominoInfo.TopIndex)
                            return false;
                }
                else if (tempDominoInfo.TopIndex == rightNum
                         || tempDominoInfo.BottomIndex == rightNum)
                {
                    if (checkDominoInfo.TopIndex == leftNum)
                        if (tempDominoInfo.TopIndex == checkDominoInfo.BottomIndex
                            || tempDominoInfo.BottomIndex == checkDominoInfo.BottomIndex)
                            return false;
                    if (checkDominoInfo.BottomIndex == leftNum)
                        if (tempDominoInfo.TopIndex == checkDominoInfo.TopIndex
                            || tempDominoInfo.BottomIndex == checkDominoInfo.TopIndex)
                            return false;
                }

                i++;
            }

            return true;
        }


        private DragHandler LogicForSimpleTile(List<DragHandler> toThrow,
            List<DragHandler> badTiles, bool nextPlayer)
        {
            int i;
            bool botPass;
            bool botThrow = false;
            DragHandler toDrop = null;
            List<DragHandler> playerTiles = _deckScript.GetList(1);

            botPass = CausePass(playerTiles, null);

            i = 0;
            while (i < toThrow.Count)
            {
                if (!CausePass(playerTiles, toThrow[i]))
                {
                    toDrop = toThrow[i];
                    botThrow = false;
                    break;
                }

                botThrow = true;
                i++;
            }

            if (botThrow)
            {
                if (nextPlayer)
                {
                    if (!botPass)
                        return null;
                    return toThrow[UnityEngine.Random.Range(0, toThrow.Count)];
                }

                if (badTiles != null)
                    if (badTiles.Count > 0)
                        return badTiles[UnityEngine.Random.Range(0, badTiles.Count)];
                return toThrow[UnityEngine.Random.Range(0, toThrow.Count)];
            }

            return toDrop;
        }


        private DragHandler GoodBot(List<DragHandler> toThrow,
            List<DragHandler> badTilesForNext, bool nextPlayer)
        {
            if (toThrow.Count == 0)
                return null;

            DragHandler toDrop;
            List<DragHandler> goodTiles = null;
            List<DragHandler> badTiles = null;
            SeparateTiles(toThrow, _deckScript.GetList(1),
                ref goodTiles, ref badTiles);

            foreach (DragHandler aux in goodTiles)
            {
                Debug.Log("DDDragHandler GoodTiles Top : " + aux.GetDominoView().GetDomino().TopIndex);
                Debug.Log("DDDragHandler GoodTiles Bottom: " + aux.GetDominoView().GetDomino().BottomIndex);
            }
            
            foreach (DragHandler aux in badTiles)
            {
                Debug.Log("DDDragHandler BadTiles Top: " + aux.GetDominoView().GetDomino().TopIndex);
                Debug.Log("DDDragHandler BadTiles Bottom: " + aux.GetDominoView().GetDomino().BottomIndex);
            }

            if (goodTiles.Count > 0)
            {
                Debug.Log("---> a 05");
                // Logic to throw good tile
                // Try to throw good for player tiles
                // If double
                // Throw something else or pass
                toDrop = LogicForGoodTile(goodTiles);
            }
            else
            {
                Debug.Log("---> a 06");
                // Check all throwable tiles if they cause player to pass
                // if not
                // throw that that will not cause player pass
                // else, means that all throwable will do player pass
                // if bot pass will result player to pass
                // throw random throwable
                // else
                // pass
                toDrop = LogicForSimpleTile(toThrow, badTilesForNext, nextPlayer);
            }


            return toDrop;
        }
    }
}