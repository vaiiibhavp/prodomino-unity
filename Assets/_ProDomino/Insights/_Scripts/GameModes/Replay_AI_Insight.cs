using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DominoTemplate.Controllers;
using DominoTemplate.DragAndDrop;
using ProDomino.ReplaySystem;
using ProDomino.Shared;
using UnityEngine;

namespace ProDomino.Insights
{
    public class Replay_AI_Insight : MonoBehaviour
    {
        [SerializeField]
        private ReplayTimelineController replayTimelineController;

        public GameMode GameMode => replayTimelineController.CurrentReplayGameMode.ExtendedGameController.GameMode;
        //public ExposedHandController HandController => replayTimelineController.CurrentReplayGameMode.ExtendedGameController.HandController as ExposedHandController;
        public HandController HandController => replayTimelineController.CurrentReplayGameMode.ExtendedGameController.HandController;
        public DeckController DeckController => replayTimelineController.CurrentReplayGameMode.ExtendedGameController.DeckController;
        public GameTurnController GameTurnController => replayTimelineController.CurrentReplayGameMode.ExtendedGameController.GameTurnController;

        public List<DragHandler> BoardTiles => DeckController.GetList(-1);
        public List<DragHandler> BoneyardTiles => DeckController.GetList(0);
        public List<DragHandler> PlayerTiles => DeckController.GetList(1);
        public List<DragHandler> AITiles => DeckController.GetList(3);

        void Start()
        {
            replayTimelineController.Calcule_IA_Insight = Calcule_IA_Insight;
        }

        private (double, double, double) Calcule_IA_Insight()
        {
            double winProbability = CalculateWinningProbability(true, VisibleInfo.All);
            double blockingProbability = CalculateBlockingProbability(VisibleInfo.All);
            double tieProbability = CalculateTieProbability(VisibleInfo.All);

            return (winProbability, blockingProbability, tieProbability);

            //Debug.Log("/*-/*- Se Ejecuto");

            //return (0, 0, 0);
        }

        public double CalculateWinningProbability(bool isPlayer, VisibleInfo visibleInfo)
        {
            if(DeckController == null)
            {
                Debug.Log("/*-/*- DeckController is null");
            }

            if(HandController is null)
            {
                Debug.Log("/*-/*- HandController is null");
            }

            if (DeckController == null || HandController is null)
                throw new Exception($"GameController or DeckController or HandController is not assigned in {gameObject.name}");

            var playerHandTiles = GetHandTiles(TargetType.Player);
            var aiHandTiles = GetHandTiles(TargetType.AI);
            if (playerHandTiles is null or { Count: 0 } || aiHandTiles is null or { Count: 0 })
            {
                Debug.LogWarning($"Player or AI hand tiles are not assigned in {gameObject.name}");
                return 0;
            }

            var user1Hand = isPlayer ? playerHandTiles : aiHandTiles;
            var user2Hand = isPlayer ? aiHandTiles : playerHandTiles;

            var myHand = user1Hand.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var opponentHand = user2Hand.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();

            var openBranches = GetOpenBranchesFrench(out var doublesRequired);

            // Si no hay ramas abiertas, solo se pueden jugar dobles requeridos
            if (openBranches.Length == 0)
                openBranches = doublesRequired.ToArray(); // solo se puede jugar si tienes ese doble

            var (probability, explantation) = CalculateVictoryProbabilityWithExplanation();

            Debug.Log($"<color={(isPlayer ? "blue>Player" : "green>AI")}</color> Winning Probability: {probability:P2}");
            Debug.Log($"Winning Explanation: \n\n{explantation}");

            return probability;

            (double probability, string explanation) CalculateVictoryProbabilityWithExplanation()
            {
                bool knowMyHand = true; //visibleInfo.HasFlag(VisibleInfo.MyHand);
                bool knowOpponent = true; //visibleInfo.HasFlag(VisibleInfo.OpponentHand);
                bool knowBoard = true; //visibleInfo.HasFlag(VisibleInfo.Board);

                int myMoves = knowMyHand && knowBoard ? CountPlayableMovesFrench(myHand, openBranches) : 0;
                int myPipSum = knowMyHand ? myHand.Sum(d => d.left + d.right) : 0;

                int opponentMoves = -1;
                int opponentPipSum = -1;

                if (knowOpponent && knowBoard)
                {
                    opponentMoves = CountPlayableMovesFrench(opponentHand, openBranches);
                    opponentPipSum = opponentHand.Sum(d => d.left + d.right);
                }

                if (knowMyHand && knowOpponent && knowBoard)
                {
                    if (IsVictoryGuaranteedFrench(myHand, opponentHand, openBranches))
                    {
                        return (1.0, "Victory is guaranteed: you have a playable tile, opponent has no valid moves, and you only have one tile left.");
                    }
                }

                int positionalControl = (knowOpponent && knowBoard) ? myMoves - opponentMoves : 0;

                double score =
                    0.35 * myMoves +
                    (knowOpponent ? -0.35 * opponentMoves : 0) +
                    -0.15 * myPipSum +
                    (knowOpponent ? 0.10 * opponentPipSum : 0) +
                    (knowOpponent ? 0.25 * positionalControl : 0);

                double probability = Sigmoid(score);
                string explanation = GenerateExplanation();
                return (probability, explanation);

                string GenerateExplanation()
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Your playable moves: {myMoves} (weighted: {0.35 * myMoves:+0.00;-0.00})");
                    sb.AppendLine($"Your pip sum: {myPipSum} (weighted: {-0.15 * myPipSum:+0.00;-0.00})");

                    if (knowOpponent)
                    {
                        sb.AppendLine($"Opponent playable moves: {opponentMoves} (weighted: {-0.35 * opponentMoves:+0.00;-0.00})");
                        sb.AppendLine($"Opponent pip sum: {opponentPipSum} (weighted: {0.10 * opponentPipSum:+0.00;-0.00})");
                        sb.AppendLine($"Positional control (you - opponent): {positionalControl} (bonus: {0.25 * positionalControl:+0.00;-0.00})");
                    }

                    sb.AppendLine($"\nFinal score: {score:+0.00;-0.00} �� Win probability: {probability:P2}");
                    return sb.ToString();
                }
            }

            double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));
        }

        public double CalculateBlockingProbability(VisibleInfo visibleInfo)
        {
            if (DeckController == null || HandController == null)
                throw new Exception("Required references not assigned");

            var playerHandTiles = GetHandTiles(TargetType.Player);
            var aiHandTiles = GetHandTiles(TargetType.AI);
            var boneyardTiles = BoneyardTiles;
            var boardTiles = BoardTiles;

            if (playerHandTiles == null || aiHandTiles == null || boardTiles == null)
                return 0;

            var myHand = playerHandTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var opponentHand = aiHandTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var boneyard = boneyardTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();

            var openBranches = GetOpenBranchesFrench(out List<int> requiredDoubles);

            var consideredDominoes = new List<(int left, int right)>();
            /*if (visibleInfo.HasFlag(VisibleInfo.MyHand))*/ consideredDominoes.AddRange(myHand);
            /*if (visibleInfo.HasFlag(VisibleInfo.OpponentHand))*/ consideredDominoes.AddRange(opponentHand);
            /*if (visibleInfo.HasFlag(VisibleInfo.Boneyard))*/ consideredDominoes.AddRange(boneyard);

            /*if (!visibleInfo.HasFlag(VisibleInfo.Board) || consideredDominoes.Count == 0 || boardTiles.Count == 0)
                return 0.0;*/

            // Check if both players are blocked
            if (visibleInfo.HasFlag(VisibleInfo.MyHand) && visibleInfo.HasFlag(VisibleInfo.OpponentHand))
            {
                bool bothBlocked = CountPlayableMovesFrench(myHand, openBranches, requiredDoubles) == 0 &&
                                   CountPlayableMovesFrench(opponentHand, openBranches, requiredDoubles) == 0;

                if (bothBlocked)
                    return 1.0;
            }

            int total = consideredDominoes.Count;
            int playable = consideredDominoes.Count(d =>
                openBranches.Any(b => d.left == b || d.right == b) ||
                (openBranches.Length == 0 && requiredDoubles.Any(r => d.left == r && d.left == d.right))
            );

            int unplayable = total - playable;
            return Math.Round((double)unplayable / total, 2);
        }

        public double CalculateTieProbability(VisibleInfo visibleInfo)
        {
            if (DeckController == null || HandController == null)
                throw new Exception("Required references not assigned");

            var playerHandTiles = GetHandTiles(TargetType.Player);
            var aiHandTiles = GetHandTiles(TargetType.AI);
            var boneyardTiles = BoneyardTiles;
            var boardTiles = BoardTiles;

            if (playerHandTiles == null || aiHandTiles == null || boardTiles == null)
                return 0;

            var myHand = playerHandTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var opponentHand = aiHandTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var boneyard = boneyardTiles.Select(x => (x.GetDominoView().GetDomino().TopIndex, x.GetDominoView().GetDomino().BottomIndex)).ToList();

            var openBranches = GetOpenBranchesFrench(out List<int> requiredDoubles);

            /*if (!visibleInfo.HasFlag(VisibleInfo.Board) || boardTiles.Count == 0)
                return 0;*/

            int myPlayable = /*visibleInfo.HasFlag(VisibleInfo.MyHand)*/ true ? CountPlayableMovesFrench(myHand, openBranches, requiredDoubles) : -1;
            int opponentPlayable = /*visibleInfo.HasFlag(VisibleInfo.OpponentHand)*/ true ? CountPlayableMovesFrench(opponentHand, openBranches, requiredDoubles) : -1;

            bool bothBlocked = myPlayable == 0 && opponentPlayable == 0;

            if (bothBlocked)
            {
                int mySum = myHand.Sum(d => d.TopIndex + d.BottomIndex);
                int oppSum = opponentHand.Sum(d => d.TopIndex + d.BottomIndex);

                if (mySum == oppSum)
                    return 1.0;

                if (!visibleInfo.HasFlag(VisibleInfo.Boneyard))
                    return 0.5;
            }

            double chanceBlocked = (myPlayable == 0 ? 1.0 : 0.5) * (opponentPlayable == 0 ? 1.0 : 0.5);
            return Math.Round(chanceBlocked, 2);
        }


        private int[] GetOpenBranchesFrench(out List<int> requiredDoubles)
        {
            var (left, right, top, down, doublesEnabled) = GetBranchNumbers(); //HandController.GetBranchNumbers();
            requiredDoubles = doublesEnabled;
            return new[] { left, right, top, down }
                .Where(x => x != 99)
                .Distinct()
                .ToArray();
        }

        public (int rightNum, int leftNum, int topNum, int downNum, List<int>) GetBranchNumbers()
        {
            var rightNum = -1;
            var leftNum = -1;

            var topNum = -1;
            var downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            //_slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            HandController.SlotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            return (rightNum, leftNum, topNum, downNum, doubleTilesEnabled);
        }

        private int CountPlayableMovesFrench(List<(int left, int right)> hand, int[] openBranches)
        {
            return hand.Count(d => openBranches.Any(branch => d.left == branch || d.right == branch));
        }


        private int CountPlayableMovesFrench(List<(int left, int right)> hand, int[] openBranches, List<int> requiredDoubles = null)
        {
            if (openBranches is { Length: > 0 })
            {
                return hand.Count(d => openBranches.Any(b => d.left == b || d.right == b));
            } else if (requiredDoubles != null && requiredDoubles.Count > 0)
            {
                return hand.Count(d => d.left == d.right && requiredDoubles.Contains(d.left));
            }

            return 0;
        }


        private bool IsVictoryGuaranteedFrench(List<(int left, int right)> myHand, List<(int left, int right)> opponentHand, int[] openBranches)
        {
            int myPlayable = CountPlayableMovesFrench(myHand, openBranches);
            int opponentPlayable = CountPlayableMovesFrench(opponentHand, openBranches);
            return myHand.Count == 1 && myPlayable == 1 && opponentPlayable == 0;
        }

        ///*** EXPOSED GAME CONTROLLER FUNCTIONS:
        private List<DragHandler> GetHandTiles(TargetType targetType)
        {
            // Register the tiles played by the target
            var targetTilesPlayed = new List<DragHandler>();
            if (targetType.HasFlag(TargetType.Player))
                targetTilesPlayed = PlayerTiles?.Where(x => !x.GetDominoView().IsOnTable()).ToList();

            if (targetType.HasFlag(TargetType.AI))
                targetTilesPlayed = AITiles?.Where(x => !x.GetDominoView().IsOnTable()).ToList();
            return targetTilesPlayed;
        }
    }
}
