using System;
using System.Collections.Generic;
using System.Linq;
using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.View;
using UnityEngine;

namespace DominoTemplate.DragAndDrop
{
    public class SlotHelper : MonoBehaviour
    {
        public int sideLimitLeftRight = 13;
        public int SideLimitLeftRight
        {
            get => sideLimitLeftRight;
            set => sideLimitLeftRight = value;
        }
        public int sideLimitTopDown = 4;
        public float tileOffset = 60;
        public float lyingOffset = 30;
        public float doubleOffset = 30;
        public float tileDist_hor;

        // Tiles;
        public float _rightTiles_Hor;
        public float RightTiles_Hor
        {
            get => _rightTiles_Hor;
            set => _rightTiles_Hor = value;
        }
        public float _rightTiles_Ver;
        public float RightTiles_Ver => _rightTiles_Ver;
        public int _rightPhase;
        public int RightPhase => _rightPhase;
        public int _rightNum;
        public int RightNum => _rightNum;

        public float _leftTiles_Hor;
        public float LeftTiles_Hor
        {
            get => _leftTiles_Hor;
            set => _leftTiles_Hor = value;
        }
        public float _leftTiles_Ver;
        public float LeftTiles_Ver => _leftTiles_Ver;
        public int _leftPhase;
        public int LeftPhase => _leftPhase;
        public int _leftNum;
        public int LeftNum => _leftNum;

        public float _topTiles_Hor;
        public float TopTiles_Hor
        {
            get => _topTiles_Hor;
            set => _topTiles_Hor = value;
        }
        public float _topTiles_Ver;
        public float TopTiles_Ver
        {
            get => _topTiles_Ver;
            set => _topTiles_Ver = value;
        }
        public int _topPhase;
        public int _topNum;
        //public int TopNum => _topNum;
        public int TopNum
        {
            get => _topNum;
            set => _topNum = value;
        }

        public float _downTiles_Hor;
        public float DownTiles_Hor
        {
            get => _downTiles_Hor;
            set => _downTiles_Hor = value;
        }
        public float _downTiles_Ver;
        public float DownTiles_Ver
        {
            get => _downTiles_Ver;
            set => _downTiles_Ver = value;
        }
        public int _downPhase;
        public int _downNum;
        //public int DownNum => _downNum;
        public int DownNum
        {
            get => _downNum;
            set => _downNum = value;
        }

        public int LeftTileId { get; private set; }
        public int RightTileId { get; private set; }
        public int TopTileId { get; private set; }
        public int DownTileId { get; private set; }

        public bool started;
        public bool auxStarted;
        public int firstPlacedTileId = -1;

        public delegate void SlotHelperValidateNumbersAvailableInBranches(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled, int firstPlacedTileId, Domino tileInfo = null);
        [SerializeField] protected SlotHelperValidateNumbersAvailableInBranches slotHelper_validateNumbersAvailableInBranches = null;
        public SlotHelperValidateNumbersAvailableInBranches SlotHelper_validateNumbersAvailableInBranches
        {
            get => slotHelper_validateNumbersAvailableInBranches;
            set => slotHelper_validateNumbersAvailableInBranches = value;
        }
        public int FirstPlaceTileId => firstPlacedTileId;

        private List<SlotRecord> tilesPutOnBoardRecords = new();

        // Stores the currently active slots per branch
        // Key = branch (0 right, 1 left, 2 top, 3 down)
        private Dictionary<int, EmptySlot> activeSlots = new();

        private EmptySlot bestSlotReference;

        private void OnDrawGizmos()
        {
            if (tilesPutOnBoardRecords is null or { Count: 0 })
                return;

            var coreRecord = tilesPutOnBoardRecords[0];

            if (coreRecord.DominoView == null)
                return;

            Transform coreTransform = coreRecord.DominoView.transform;

            for (int i = 0; i < tilesPutOnBoardRecords.Count; i++)
            {
                var record = tilesPutOnBoardRecords[i];

                if (record.DominoView == null)
                    continue;

                // Convert stored local offset to world space
                Vector3 worldPosition = coreTransform.TransformPoint(record.OffsetAccordingCore);

                // Draw expected position
                Gizmos.color = i == 0 ? Color.green : Color.red;
                Gizmos.DrawSphere(worldPosition, 0.1f);

                // Optional: draw line from core to tile
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(coreTransform.position, worldPosition);
            }
        }

        public void RestartSlotHelper()
        {
            _rightTiles_Hor = 0;
            _rightTiles_Ver = 0;
            _rightPhase = 0;

            _leftTiles_Hor = 0;
            _leftTiles_Ver = 0;
            _leftPhase = 0;

            _topTiles_Hor = 0;
            _topTiles_Ver = 0;
            _topPhase = 0;

            _downTiles_Hor = 0;
            _downTiles_Ver = 0;
            _downPhase = 0;

            _rightNum = -1;
            _leftNum = -1;
            _topNum = -1;
            _downNum = -1;

            RightTileId = -1;
            LeftTileId = -1;
            TopTileId = -1;
            DownTileId = -1;

            firstPlacedTileId = -1;

            started = false;
            auxStarted = false;
            tilesPutOnBoardRecords.Clear();
        }

        public void TellBranchNums(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled)
        {
            slotHelper_validateNumbersAvailableInBranches?.Invoke
                (ref rightBranch, 
                ref leftBranch, 
                ref topNum, 
                ref downNum, 
                ref doubleTilesEnabled, 
                firstPlacedTileId);
        }
        
        public int[] TellBranchNums()
        {
            return new int[]
            {
                _rightNum,
                _leftNum,
                _topNum,
                _downNum
            };
        }

        private void ConfirmMovingLeftOrRight(ref float tile_Hor, ref float tile_Ver, ref int phase, RectTransform slot,
            int scenario)
        {
            float vertShift;
            int sideLimit = sideLimitLeftRight;
            int shift1;
            int shift2;

            if (scenario == 0)
            {
                // right branch
                shift1 = -1;
                shift2 = -2;
                vertShift = -1.5f;
            }
            else
            {
                // left branch
                shift1 = 1;
                shift2 = 2;
                vertShift = 1.5f;
            }


            // this is first turn
            if (phase == 0)
            {
                // If Standing
                if (slot.rotation.z == 0)
                    tile_Hor -= shift1;
                else
                    tile_Hor -= shift2;

                if (scenario == 0 && tile_Hor > sideLimit && tile_Ver == 0)
                    phase++;
                else if (scenario == 1 && tile_Hor < -sideLimit && tile_Ver == 0)
                    phase++;
            }
            else if (phase == 1)
            {
                tile_Ver += vertShift;
                tile_Hor -= shift1;
                phase++;
            }
            else if (phase == 2)
            {
                tile_Ver += vertShift;
                tile_Hor += shift1;
                phase++;
            }
            else if (phase == 3)
            {
                tile_Hor += shift2;
                phase++;
            }
            else
            {
                // Phase 4
                // If Standing 
                if (slot.rotation.z == 0)
                    tile_Hor += shift1;
                else
                    tile_Hor += shift2;
            }
        }
        
        private void ConfirmMovingTopOrDown(ref float tile_Hor, ref float tile_Ver, ref int phase, RectTransform slot,
            int scenario)
        {
            float horShift;
            int sideLimit = sideLimitTopDown; //13;
            int shift1;
            int shift2;

            if (scenario == 2)
            {
                // right branch
                shift1 = -2;
                shift2 = -1;
                horShift = -1.5f;
            }
            else
            {
                // left branch
                shift1 = 2;
                shift2 = 1;
                horShift = 1.5f;
            }


            // this is first turn
            if (phase == 0)
            {
                Debug.Log("/// Angle " + Mathf.Abs(slot.eulerAngles.z));
                // If Standing
                if (slot.rotation.z == 0 || Mathf.Abs(slot.eulerAngles.z) == 180)
                {
                    tile_Ver -= shift1;
                    Debug.Log("///1");
                }
                else
                {
                    tile_Ver -= shift2;//tile_Ver -= shift2;
                    Debug.Log("///2");
                }

                if (scenario == 2 && tile_Ver > sideLimit /*&& tile_Hor == 0*/)
                    phase++;
                else if (scenario == 3 && tile_Ver < -sideLimit /*&& tile_Hor == 0*/)
                    phase++;

                /*if (scenario == 2 && tile_Ver > sideLimit && tile_Ver == 0)
                    phase++;
                else if (scenario == 3 && tile_Ver < -sideLimit && tile_Ver == 0)
                    phase++;*/
            }
            else if (phase == 1)
            {
                tile_Ver -= shift2; //tile_Ver += vertShift; //3.5 + (-1.5)
                tile_Hor -= horShift; //tile_Hor -= shift1; //0 - (-2)
                phase++;
            }
            else if (phase == 2)
            {
                //tile_Ver += horShift;
                //tile_Hor += shift1;
                tile_Hor -= shift1;
                phase++;
            }
            else //if (phase == 3)
            {
                //tile_Hor += shift2;//tile_Hor += shift2;
                if (slot.rotation.z == 0 || Mathf.Abs(slot.eulerAngles.z) == 180)
                    tile_Hor -= shift2;
                else
                    tile_Hor -= shift1;
                phase++;
            }
            /*else
            {
                // Phase 4
                // If Standing 
                if (slot.rotation.z == 0)
                    tile_Hor += shift1;
                else
                    tile_Hor += shift2;
            }*/
        }

        /// <summary>
        /// Confirm the occupation of a slot<br></br>
        /// calls when the tiles is dropped on the slot or the target slot was clicked
        /// </summary>
        public void ConfirmOccupation(RectTransform slot, int scenario, DominoView dominoView)
        {
            var dominoInfo = dominoView.GetDomino();
            if (dominoInfo.TopIndex < 0 || dominoInfo.BottomIndex < 0)
                Debug.Log("Weird Domino info!");

            if (started)
            {
                started = false;

                // By default, adjust positive in right and negative in left (first move right)
                _rightTiles_Hor -= 0.5f;
                _leftTiles_Hor += 0.5f;
            }

            //The first tile is placed
            if (auxStarted)
            {
                auxStarted = false;
                _topTiles_Ver -= 0.5f;
                _downTiles_Ver += 0.5f;

                firstPlacedTileId = dominoInfo.id;
                Debug.Log("++---- auxStarted: " + auxStarted);
            }

            // Set the branch numbers
            if (_rightNum == -1 && _leftNum == -1 && _topNum == -1 && _downNum == -1)
            {
                _rightNum = dominoInfo.TopIndex;
                _leftNum = dominoInfo.BottomIndex;

                // The first tile should set both top and down numbers if is a double tile
                if (dominoInfo.TopIndex == dominoInfo.BottomIndex)
                {
                    _topNum = dominoInfo.TopIndex;
                    _downNum = dominoInfo.BottomIndex;
                }

                Debug.Log("++---- 00000 _rightNum: " + _rightNum + ", _leftNum: " + _leftNum + ", _topNum: " + _topNum + ", _downNum: " + _downNum);
            }

            // Right side
            if (scenario == 0)
            { 
                ConfirmMovingLeftOrRight(ref _rightTiles_Hor, ref _rightTiles_Ver, ref _rightPhase, slot, scenario);
                _rightNum = _rightNum == dominoInfo.TopIndex ? dominoInfo.BottomIndex : dominoInfo.TopIndex;
                RightTileId = dominoInfo.id;
            }

            // Left side
            if (scenario == 1)
            { 
                ConfirmMovingLeftOrRight(ref _leftTiles_Hor, ref _leftTiles_Ver, ref _leftPhase, slot, scenario);
                _leftNum = _leftNum == dominoInfo.TopIndex ? dominoInfo.BottomIndex : dominoInfo.TopIndex;
                LeftTileId = dominoInfo.id;
            }

            // Top side
            if (scenario == 2)
            { 
                ConfirmMovingTopOrDown(ref _topTiles_Hor, ref _topTiles_Ver, ref _topPhase, slot, scenario);
                _topNum = _topNum == dominoInfo.TopIndex ? dominoInfo.BottomIndex : dominoInfo.TopIndex;
                TopTileId = dominoInfo.id;
            }

            // Down side
            if (scenario == 3)
            { 
                ConfirmMovingTopOrDown(ref _downTiles_Hor, ref _downTiles_Ver, ref _downPhase, slot, scenario);
                _downNum = _downNum == dominoInfo.TopIndex ? dominoInfo.BottomIndex : dominoInfo.TopIndex;
                DownTileId = dominoInfo.id;
            }

            // Save the tile position and rotation on the board once the occupation is confirmed,
            // this will be used to make sure each tile is correctly placed on the board
            var coreReference = tilesPutOnBoardRecords.FirstOrDefault()?.DominoView;
            tilesPutOnBoardRecords.Add(new SlotRecord
                (coreReference: coreReference,
                tileId: dominoInfo.id, 
                dominoView: dominoView, 
                rotation: slot.rotation));

            Debug.Log("Right branch shift Hor " + _rightTiles_Hor + " Ver " + _rightTiles_Ver + " Phase " + _rightPhase);
            Debug.Log("Left branch shift Hor " + _leftTiles_Hor + " Ver " + _leftTiles_Ver + " Phase " + _leftPhase);
            Debug.Log("Right num -> " + _rightNum + " | " + _leftNum + " <- Left num");
        }


        private void SetSlotPosition(ref RectTransform slot, bool standing, int scenario, Domino dominoInfo)
        {
            int phase;
            int branchNum;
            float tile_Ver;
            float tile_Hor;
            float horOffset;
            float doubOffset;
            float angle;
            float resAngle = 0;
            float horShift;

            if (scenario == 0)
            {
                // Right
                tile_Hor = _rightTiles_Hor;
                tile_Ver = _rightTiles_Ver;
                phase = _rightPhase;
                branchNum = _rightNum;
                horOffset = lyingOffset;
                doubOffset = doubleOffset;
                angle = (FirstPlaceTileId == dominoInfo.id || FirstPlaceTileId == -1) && dominoInfo.TopIndex > dominoInfo.BottomIndex ? -90 : 90;
                horShift = -tileDist_hor;
            }
            else
            {
                // Left
                tile_Hor = _leftTiles_Hor;
                tile_Ver = _leftTiles_Ver;
                phase = _leftPhase;
                branchNum = _leftNum;
                horOffset = -lyingOffset;
                doubOffset = -doubleOffset;
                angle = (FirstPlaceTileId == dominoInfo.id || FirstPlaceTileId == -1) && dominoInfo.TopIndex < dominoInfo.BottomIndex ? 90 : -90;
                horShift = tileDist_hor;
            }

            Debug.Log("**** standing: " + standing);

            // This must correctly place domino tile
            if (standing)
            {
                if (phase < 2)
                {
                    slot.anchoredPosition = new Vector2((tileOffset * tile_Hor), tileOffset * tile_Ver);
                    Debug.Log("rrr 1");
                }
                else if (phase == 2)
                {
                    slot.anchoredPosition = new Vector2((tileOffset * tile_Hor) + horShift, tileOffset * tile_Ver);
                    Debug.Log("rrr 2");
                }
                else
                {
                    slot.anchoredPosition = new Vector2((tileOffset * tile_Hor + horOffset * 2), tileOffset * tile_Ver);
                    Debug.Log("rrr 3");
                }

                if (dominoInfo.IsDouble())
                {
                    var tempPosition = slot.anchoredPosition;

                    if (phase is < 2 or 3)
                        tempPosition.x += doubOffset;
                    else if (phase is 2)
                        tempPosition.y += doubOffset;

                    slot.anchoredPosition = tempPosition;
                }
            }
            else
            {
                resAngle += angle;
                slot.anchoredPosition = new Vector2((tileOffset * tile_Hor + horOffset), tileOffset * tile_Ver);
                Debug.Log("rrr 4");

                if (dominoInfo.IsDouble())
                {
                    var tempPosition = slot.anchoredPosition;
                    tempPosition.x += doubOffset;
                    slot.anchoredPosition = tempPosition;
                }
            }

            //slot.anchoredPosition =  new Vector2 (slot.anchoredPosition.x * sizeOfTable, slot.anchoredPosition.y * sizeOfTable);
            // This must correctly rotate domino tile
            if (branchNum != -1)
            {
                if (dominoInfo.TopIndex != branchNum && phase < 2)
                    resAngle += 180;

                if (scenario == 0)
                {
                    if (dominoInfo.TopIndex != branchNum && phase == 2)
                        resAngle += 180;
                    if (dominoInfo.BottomIndex != branchNum && phase > 2)
                        resAngle += 180;
                }
                else
                {
                    if (dominoInfo.BottomIndex != branchNum && phase >= 2)
                        resAngle += 180;
                }
            }

            slot.localRotation = Quaternion.Euler(0, 0, resAngle);
            slot.localScale = Vector3.one;
        }

        private void SetSlotPositionTopOrDown(ref RectTransform slot, bool standing, int scenario, Domino dominoInfo)
        {
            int phase;
            int branchNum;
            float tile_Ver;
            float tile_Hor;
            float horOffset;
            float doubOffset;
            float angle;
            float resAngle = 0;
            float horShift;

            if (scenario == 2)
            {
                // Right
                tile_Hor = _topTiles_Hor;
                tile_Ver = _topTiles_Ver;
                phase = _topPhase;
                branchNum = _topNum;
                horOffset = lyingOffset;
                doubOffset = doubleOffset;
                angle = 90;
                horShift = -tileDist_hor;
            }
            else // scenario == 3
            {
                // Left
                tile_Hor = _downTiles_Hor;
                tile_Ver = _downTiles_Ver;
                phase = _downPhase;
                branchNum = _downNum;
                horOffset = -lyingOffset;
                doubOffset = -doubleOffset;
                angle = -90;
                horShift = tileDist_hor;
            }

            Debug.Log("****New standing: " + standing);

            // This must correctly place domino tile
            if (standing)
            {
                if (phase < 2)
                {
                    slot.anchoredPosition = new Vector2(tileOffset * tile_Hor, (tileOffset * tile_Ver) + horOffset);
                    //slot.anchoredPosition = new Vector2((tileOffset * tile_Hor), tileOffset * tile_Ver);
                    Debug.Log("rrrNew 1");
                }
                else if (phase == 2)
                {
                    slot.anchoredPosition = new Vector2((tileOffset * tile_Hor) + horShift, tileOffset * tile_Ver);
                    Debug.Log("rrrNew 2");
                }
                else
                {
                    slot.anchoredPosition = new Vector2((tileOffset * tile_Hor - horOffset), tileOffset * tile_Ver);
                    //slot.anchoredPosition = new Vector2((tileOffset * tile_Hor), tileOffset * tile_Ver);
                    //slot.anchoredPosition = new Vector2((tileOffset * tile_Hor + horOffset * 2), tileOffset * tile_Ver);
                    Debug.Log("rrrNew 3");
                }

                if (dominoInfo.IsDouble())
                {
                    var tempPosition = slot.anchoredPosition;
                    tempPosition.x += doubOffset;
                    slot.anchoredPosition = tempPosition;
                }
            }
            else
            {
                resAngle += angle;
                slot.anchoredPosition = new Vector2((tileOffset * tile_Hor) , (tileOffset * tile_Ver));
                //slot.anchoredPosition = new Vector2((tileOffset * tile_Hor + horOffset), tileOffset * tile_Ver);
                Debug.Log("rrrNew 4");

                if (dominoInfo.IsDouble())
                {
                    var tempPosition = slot.anchoredPosition;
                    tempPosition.y += doubOffset;
                    slot.anchoredPosition = tempPosition;
                }
            }

            //slot.anchoredPosition =  new Vector2 (slot.anchoredPosition.x * sizeOfTable, slot.anchoredPosition.y * sizeOfTable);
            // This must correctly rotate domino tile
            if (branchNum != -1)
            {
                
                /*if (dominoInfo.TopIndex != branchNum && phase < 2)
                    resAngle += 180;*/

                if (scenario == 2)
                {
                    if (dominoInfo.TopIndex == branchNum && phase < 2)
                    resAngle += 180;

                    if (dominoInfo.TopIndex != branchNum && phase == 2)
                        resAngle += 180;
                    if (dominoInfo.BottomIndex == branchNum && phase > 2)
                        resAngle += 180;
                }
                else
                {
                    if (dominoInfo.BottomIndex == branchNum /*&& phase < 2*/)
                        resAngle += 180;
                }
            }

            slot.localRotation = Quaternion.Euler(0, 0, resAngle);
            slot.localScale = Vector3.one;
        }

        public bool SetGamePositions(ref RectTransform slot1, ref RectTransform slot2, ref RectTransform slot3, ref RectTransform slot4,
            DominoView dominoTile, EmptySlot slotPrefab, bool playerTurn, GameControler gameControler)
        {
            bool standing = false;
            Domino tileInfo = dominoTile.GetDomino();

            EmptySlot slotScript1 = default;
            EmptySlot slotScript2 = default;
            EmptySlot slotScript3 = default;
            EmptySlot slotScript4 = default;

            int rightBranch = 99; //_rightNum;
            int leftBranch = 99; //_leftNum;
            int topNum = 99;  //_topNum;
            int downNum = 99;  //_downNum;

            var auxNull = new List<int>(); //Not used

            slotHelper_validateNumbersAvailableInBranches?.Invoke(ref rightBranch, ref leftBranch, ref topNum, ref downNum, ref auxNull, firstPlacedTileId, tileInfo);
            activeSlots.Clear();
            DestroyBestMoveSlot();

            // Check if slot could exist
            // The result is this
            if (tileInfo.HasValue(rightBranch) // Simply check if the current tile could be placed on the right side
                || (rightBranch == -1/* && tileInfo.TopIndex > tileInfo.BottomIndex*/)) // Or if both branches are -1, paired tiles or tiles with predominant bottom value (rotated -90°)
            {
                if (slot1 != null)
                    slotScript1 = slot1.GetComponent<EmptySlot>();
                else
                    slotScript1 = Instantiate(slotPrefab);

                if (slotScript1 != null)
                    activeSlots[0] = slotScript1;

                slotScript1.sideInfo = "right";
                slotScript1.gameScript = gameControler;
                slot1 = slotScript1.GetOwnRectTransform();

                // If the player is not the one who is playing, the slot should be invisible
                if (!playerTurn)
                    slotScript1.BecomeInvisible();
            }
            else
            {
                if (slot1 != null)
                {
                    slotScript1 = slot1.GetComponent<EmptySlot>();
                    if (slotScript1 != null)
                        gameControler.RemoveSpecificSlot(ref slot1);

                    // Also remove from active slots to avoid any possible issue with the dictionary
                    if (activeSlots.ContainsKey(0))
                        activeSlots.Remove(0);
                }
            }

            if (tileInfo.HasValue(leftBranch) // Simply check if the current tile could be placed on the left side
                || (leftBranch == -1/* && tileInfo.TopIndex < tileInfo.BottomIndex*/)) // Or if both branches are -1, paired tiles or tiles with predominant top value (rotated 90°)
            {
                if (slot2 != null)
                    slotScript2 = slot2.GetComponent<EmptySlot>();
                else
                    slotScript2 = Instantiate(slotPrefab);

                if (slotScript2 != null)
                    activeSlots[1] = slotScript2;

                slotScript2.sideInfo = "left";
                slotScript2.gameScript = gameControler;
                slot2 = slotScript2.GetOwnRectTransform();

                // If the player is not the one who is playing, the slot should be invisible
                if (!playerTurn)
                    slotScript2.BecomeInvisible();
            }
            else
            {
                if (slot2 != null)
                {
                    slotScript2 = slot2.GetComponent<EmptySlot>();
                    if (slotScript2 != null)
                        gameControler.RemoveSpecificSlot(ref slot2);

                    // Also remove from active slots to avoid any possible issue with the dictionary
                    if (activeSlots.ContainsKey(1))
                        activeSlots.Remove(1);
                }
            }

            if (tileInfo.HasValue(topNum) || topNum == -1)
            {
                if (slot3 != null)
                    slotScript3 = slot3.GetComponent<EmptySlot>();
                else
                    slotScript3 = Instantiate(slotPrefab);

                if (slotScript3 != null)
                    activeSlots[2] = slotScript3;

                slotScript3.sideInfo = "top";
                slotScript3.gameScript = gameControler;
                slot3 = slotScript3.GetOwnRectTransform();

                // If the player is not the one who is playing, the slot should be invisible
                if (!playerTurn)
                    slotScript3.BecomeInvisible();
            }
            else
            {
                if (slot3 != null)
                {
                    slotScript3 = slot3.GetComponent<EmptySlot>();
                    if (slotScript3 != null)
                        gameControler.RemoveSpecificSlot(ref slot3);

                    // Also remove from active slots to avoid any possible issue with the dictionary
                    if (activeSlots.ContainsKey(2))
                        activeSlots.Remove(2);
                }
            }

            if (tileInfo.HasValue(downNum) || downNum == -1)
            {
                if (slot4 != null)
                    slotScript4 = slot4.GetComponent<EmptySlot>();
                else
                    slotScript4 = Instantiate(slotPrefab);

                if (slotScript4 != null)
                    activeSlots[3] = slotScript4;

                slotScript4.sideInfo = "down";
                slotScript4.gameScript = gameControler;
                slot4 = slotScript4.GetOwnRectTransform();

                // If the player is not the one who is playing, the slot should be invisible
                if (!playerTurn)
                    slotScript4.BecomeInvisible();
            }
            else
            {
                if (slot4 != null)
                {
                    slotScript4 = slot4.GetComponent<EmptySlot>();
                    if (slotScript4 != null)
                        gameControler.RemoveSpecificSlot(ref slot4);

                    // Also remove from active slots to avoid any possible issue with the dictionary
                    if (activeSlots.ContainsKey(3))
                        activeSlots.Remove(3);
                }
            }

            // Check if there is any slot to be placed
            if (slot1 == null && slot2 == null && slot3 == null && slot4 == null)
                return false;

            // Right scenario
            if (slot1 != null)
            {
                standing = CheckStanding(tileInfo, _rightPhase);
                SetSlotPosition(ref slot1, standing, 0, tileInfo);
            }

            // Left scenario
            if (slot2 != null)
            {
                standing = CheckStanding(tileInfo, _leftPhase);
                SetSlotPosition(ref slot2, standing, 1, tileInfo);
            }

            // Top scenario
            if (slot3 != null)
            {
                standing = CheckStandingTopAndDown(tileInfo, _topPhase);
                SetSlotPositionTopOrDown(ref slot3, standing, 2, tileInfo);

                Debug.Log("++---- Top scenario");
            }

            // Down scenario
            if (slot4 != null)
            {
                standing = CheckStandingTopAndDown(tileInfo, _downPhase);
                SetSlotPositionTopOrDown(ref slot4, standing, 3, tileInfo);

                Debug.Log("++---- Down scenario");
            }

            // Case if Center (only happens on start)
            if (_rightTiles_Hor == 0 && _rightTiles_Ver == 0 && _leftTiles_Hor == 0 && _leftTiles_Ver == 0 &&
                _topTiles_Hor == 0 && _topTiles_Ver == 0 && _downTiles_Hor == 0 && _downTiles_Ver == 0)
            {
                Debug.Log("++---- Case if Center");
                auxStarted = true;
                var oneSlotCentered = false;
                if (slot1 != null)
                { 
                    slot1.anchoredPosition = new Vector2(0, 0);
                    oneSlotCentered = true;
                }
                if (slot2 != null)
                { 
                    slot2.anchoredPosition = new Vector2(0, 0);

                    if (oneSlotCentered)
                        slotScript2.BecomeInvisible();
                    else
                        oneSlotCentered = true;
                }
                if (slot3 != null)
                { 
                    slot3.anchoredPosition = new Vector2(0, 0);

                    if (oneSlotCentered)
                        slotScript3.BecomeInvisible();
                    else
                        oneSlotCentered = true;
                }
                if (slot4 != null)
                { 
                    slot4.anchoredPosition = new Vector2(0, 0);

                    if (oneSlotCentered)
                        slotScript4.BecomeInvisible();
                }
                if (standing)
                    started = false;
                else
                    started = true;
            }

            return true;
        }


        private bool CheckStanding(Domino tileInfo, int phase)
        {
            bool standing;

            standing = false;

            if (tileInfo.id == 0 || tileInfo.id == 7 || tileInfo.id == 13 || tileInfo.id == 18
                || tileInfo.id == 22 || tileInfo.id == 25 || tileInfo.id == 27)
                standing = true;
            if (phase == 1) // First Corner
                standing = false;
            else if (phase == 2) // First up/down turn
                standing = true;
            else if (phase == 3) // End of first corner	
                standing = false;

            return standing;
        }

        private bool CheckStandingTopAndDown(Domino tileInfo, int phase)
        {
            bool standing;

            standing = true;

            if (_rightTiles_Hor == 0 && _rightTiles_Ver == 0 && _leftTiles_Hor == 0 && _leftTiles_Ver == 0 &&
                _topTiles_Hor == 0 && _topTiles_Ver == 0 && _downTiles_Hor == 0 && _downTiles_Ver == 0)
            {
                return standing;
            }

            if (tileInfo.id == 0 || tileInfo.id == 7 || tileInfo.id == 13 || tileInfo.id == 18 || tileInfo.id == 22 || tileInfo.id == 25 || tileInfo.id == 27)
                    standing = false;

            if (phase == 1) // First Corner
            {
                standing = true;
            }
            else if (phase == 2) // First up/down turn
            {
                standing = false;
            } 
            else if (phase >= 3) // End of first corner
            {
                //standing = false;

                if (tileInfo.id == 0 || tileInfo.id == 7 || tileInfo.id == 13 || tileInfo.id == 18 || tileInfo.id == 22 || tileInfo.id == 25 || tileInfo.id == 27)
                {
                    standing = true;
                }
                else
                {
                    standing = false;
                }
            } 
            return standing;
        }

        /// <summary>
        /// Updates the positions of domino tiles on the board based on recorded placement data.
        /// </summary>
        public void UpdateDominoPositionsAccordingRecords()
        {
            // Check if the records of board tiles has more than one registry
            if (tilesPutOnBoardRecords is null or { Count: < 1 })
                return;

            // Get the reference of the first tile put on the board
            var coreTile = tilesPutOnBoardRecords[0];

            // According the core tile, load and set the transform configuration in each tile
            for (int i = 1; i < tilesPutOnBoardRecords.Count; i++)
            {
                var tileOnBoard = tilesPutOnBoardRecords[i];
                tileOnBoard.LoadRecord(coreTile.DominoView);
            }
        }

        /// <summary>
        /// Removes the slot record associated with the specified tile ID from the collection.
        /// </summary>
        /// <param name="tileId">The ID of the tile whose slot record should be removed.</param>
        public void RemoveSlotRecordByTileId(int tileId)
        {
            var recordToRemove = tilesPutOnBoardRecords.FirstOrDefault(r => r.TileId == tileId);
            if (recordToRemove != null)
            {
                recordToRemove.ClearRecord();
                tilesPutOnBoardRecords.Remove(recordToRemove);
            }
        }

        /// <summary>
        /// Creates ONLY ONE slot in the best possible branch for the given domino.
        /// </summary>
        public RectTransform CreateBestSlot(
            GameControler gameControler,
            Domino dominoInfo,
            EmptySlot slotPrefab,
            Transform parent)
        {
            // -----------------------------
            // STEP 1: Get current branch numbers
            // -----------------------------
            int rightBranch = _rightNum;
            int leftBranch = _leftNum;
            int topBranch = _topNum;
            int downBranch = _downNum;

            List<int> dummy = new List<int>();

            // Allow external override logic (IMPORTANT)
            slotHelper_validateNumbersAvailableInBranches?.Invoke(
                ref rightBranch,
                ref leftBranch,
                ref topBranch,
                ref downBranch,
                ref dummy,
                firstPlacedTileId,
                dominoInfo
            );

            // -----------------------------
            // STEP 2: Check valid branches
            // -----------------------------
            List<int> validBranches = new List<int>();

            if (dominoInfo.HasValue(rightBranch) || rightBranch == -1)
                validBranches.Add(0); // RIGHT

            if (dominoInfo.HasValue(leftBranch) || leftBranch == -1)
                validBranches.Add(1); // LEFT

            if (dominoInfo.HasValue(topBranch) || topBranch == -1)
                validBranches.Add(2); // TOP

            if (dominoInfo.HasValue(downBranch) || downBranch == -1)
                validBranches.Add(3); // DOWN

            // No valid moves
            if (validBranches.Count == 0)
                return null;

            // -----------------------------
            // STEP 3: Choose best branch
            // -----------------------------
            var bestBranch = ChooseBestBranch(dominoInfo, validBranches,
                rightBranch, leftBranch, topBranch, downBranch);

            // -----------------------------
            // STEP 4: Instantiate slot
            // -----------------------------
            if (!bestSlotReference)
            {
                // If there is no slot reference, we can simply instantiate a new one
                if (gameControler)
                    gameControler.RemoveAllSlots();

                // If there is no reference, we can simply instantiate a new one
                bestSlotReference = Instantiate(slotPrefab);
            }

            // Make sure the slot is parented to the board (important for correct positioning)
            if (bestSlotReference.transform.parent != parent)
                bestSlotReference.transform.SetParent(parent, false);

            bestSlotReference.BlockPointerEvents();

            var slot = bestSlotReference.GetOwnRectTransform();

            // Assign side string
            switch (bestBranch)
            {
                case 0: bestSlotReference.sideInfo = "right"; break;
                case 1: bestSlotReference.sideInfo = "left"; break;
                case 2: bestSlotReference.sideInfo = "top"; break;
                case 3: bestSlotReference.sideInfo = "down"; break;
            }

            // -----------------------------
            // STEP 5: Position slot
            // -----------------------------
            bool standing = false;

            if (bestBranch == 0)
            {
                standing = CheckStanding(dominoInfo, _rightPhase);
                SetSlotPosition(ref slot, standing, 0, dominoInfo);
            } 
            else if (bestBranch == 1)
            {
                standing = CheckStanding(dominoInfo, _leftPhase);
                SetSlotPosition(ref slot, standing, 1, dominoInfo);
            } 
            else if (bestBranch == 2)
            {
                standing = CheckStandingTopAndDown(dominoInfo, _topPhase);
                SetSlotPositionTopOrDown(ref slot, standing, 2, dominoInfo);
            } 
            else if (bestBranch == 3)
            {
                standing = CheckStandingTopAndDown(dominoInfo, _downPhase);
                SetSlotPositionTopOrDown(ref slot, standing, 3, dominoInfo);
            }

            return slot;
        }

        /// <summary>
        /// Advanced heuristic using AI-like scoring per branch.
        /// Evaluates EACH possible placement as a different move.
        /// </summary>
        private int ChooseBestBranch(
            Domino domino,
            List<int> validBranches,
            int right, int left, int top, int down)
        {
            int bestBranch = validBranches[0];
            float bestScore = float.NegativeInfinity;

            foreach (var branch in validBranches)
            {
                int branchValue = GetBranchValue(branch, right, left, top, down);

                // ----------------------------------------
                // STEP 1: Simulate resulting value
                // ----------------------------------------
                int resultingValue = GetResultingValue(domino, branchValue);

                // ----------------------------------------
                // STEP 2: Score like your AI
                // ----------------------------------------
                float score = 0f;

                int v1 = domino.TopIndex;
                int v2 = domino.BottomIndex;

                // -------------------------
                // Base: tile value
                // -------------------------
                score += (v1 + v2);

                // -------------------------
                // Favor high resulting value (important)
                // -------------------------
                score += resultingValue * 2;

                // -------------------------
                // Double bonus (reuse your logic)
                // -------------------------
                if (domino.IsDouble())
                {
                    score += 3;

                    // Bonus if opening branch with support
                    if (branchValue == -1)
                    {
                        int support = CountValueInHand(domino, resultingValue);
                        score += Mathf.Clamp(support - 1, 0, 4) * 1.5f;

                        if (support == 0)
                            score -= 4f;
                    }
                }

                // -------------------------
                // Penalize bad openings
                // -------------------------
                if (branchValue == -1)
                {
                    score -= 2; // softer than before
                }

                // -------------------------
                // Flexibility (VERY important)
                // -------------------------
                int connections = CountConnections(domino, right, left, top, down);
                score += connections * 2;

                // -------------------------
                // Prefer branches already active
                // -------------------------
                if (branchValue != -1)
                    score += 1.5f;

                // ----------------------------------------
                // DEBUG
                // ----------------------------------------
                // Debug.Log($"Branch {branch} score: {score}");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBranch = branch;
                }
            }

            return bestBranch;
        }

        /// <summary>
        /// Returns the value of a branch based on index
        /// </summary>
        private int GetBranchValue(int branch, int right, int left, int top, int down)
        {
            switch (branch)
            {
                case 0: return right;
                case 1: return left;
                case 2: return top;
                case 3: return down;
            }
            return -1;
        }

        /// <summary>
        /// Counts how many branches this domino can connect to
        /// </summary>
        private int CountConnections(Domino d, int right, int left, int top, int down)
        {
            int count = 0;

            if (d.HasValue(right)) count++;
            if (d.HasValue(left)) count++;
            if (d.HasValue(top)) count++;
            if (d.HasValue(down)) count++;

            return count;
        }

        /// <summary>
        /// Returns the resulting open value after placing the domino
        /// </summary>
        private int GetResultingValue(Domino d, int branchValue)
        {
            if (branchValue == -1)
                return Math.Max(d.TopIndex, d.BottomIndex);

            if (d.TopIndex == branchValue)
                return d.BottomIndex;

            return d.TopIndex;
        }

        /// <summary>
        /// Counts how many tiles in hand support a value
        /// </summary>
        private int CountValueInHand(Domino domino, int value)
        {
            // Minimal version (you can inject AI hand later)
            int count = 0;

            if (domino.TopIndex == value) count++;
            if (domino.BottomIndex == value) count++;

            return count;
        }

        /// <summary>
        /// Hides the best move slot by making it invisible.
        /// </summary>
        public void DestroyBestMoveSlot()
        {
            if (bestSlotReference)
            {
                Destroy(bestSlotReference.gameObject);
                bestSlotReference = null;
            }
            else
                Debug.LogWarning("No best move slot to hide!");
        }
        public class SlotRecord
        {
            public SlotRecord(DominoView coreReference, int tileId, DominoView dominoView, Quaternion rotation)
            {
                TileId = tileId;
                DominoView = dominoView;

                // Store position in local space of the core reference
                OffsetAccordingCore = coreReference != null
                    ? coreReference.transform.InverseTransformPoint(dominoView.transform.position)
                    : Vector2.zero;

                // Store local rotation relative to core
                Rotation = coreReference != null
                    ? Quaternion.Inverse(coreReference.transform.rotation) * dominoView.transform.rotation
                    : rotation;
            }

            public int TileId { get; private set; }
            public DominoView DominoView { get; private set; }
            public Vector3 OffsetAccordingCore { get; private set; }
            public Quaternion Rotation { get; private set; } = Quaternion.identity;

            public void LoadRecord(DominoView coreDomino)
            {
                if (!DominoView)
                {
                    Debug.LogError("No domino to load!");
                    return;
                }

                // Convert local offset back to world space
                var worldPosition = coreDomino.transform.TransformPoint(OffsetAccordingCore);

                // Reconstruct rotation relative to core
                var worldRotation = coreDomino.transform.rotation * Rotation;

                DominoView.transform.SetPositionAndRotation(worldPosition, worldRotation);

                var localPosition = DominoView.transform.localPosition;
                localPosition.z = 0; // Ensure the tile stays on the same plane

                DominoView.transform.localPosition = localPosition;
            }

            public void ClearRecord()
            {
                TileId = -1;
                DominoView = null;
                OffsetAccordingCore = Vector3.zero;
                Rotation = Quaternion.identity;
            }
        }
    }
}