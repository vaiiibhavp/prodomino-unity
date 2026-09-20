using DominoTemplate.Controllers;
using DominoTemplate.DragAndDrop;
using ProDomino.GameModes;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProDomino.Insights
{
    public abstract class ExposedGameController<ExposedControllerInheritedType> : MonoBehaviour
        where ExposedControllerInheritedType : AbstractGameMode
    {

        [field: SerializeField, Tooltip("Use it instead of the base one")]
        public ExposedControllerInheritedType GamerController { get; private set; }

        public GameMode GameMode => GamerController.GameModeID;
        public ExposedHandController HandController => GamerController.ExtendedGameController.HandController as ExposedHandController;
        public DeckController DeckController => GamerController.ExtendedGameController.DeckController;
        public GameTurnController GameTurnController => GamerController.ExtendedGameController.GameTurnController;

        public List<DragHandler> BoardTiles => DeckController.GetList(-1);
        public List<DragHandler> BoneyardTiles => DeckController.GetList(0);
        public List<DragHandler> PlayerTiles => DeckController.GetList(1);
        public List<DragHandler> AITiles => DeckController.GetList(3);

        public List<DragHandler> GetHandTiles(TargetType targetType)
        {
            // Register the tiles played by the target
            var targetTilesPlayed = new List<DragHandler>();
            if (targetType.HasFlag(TargetType.Player))
                targetTilesPlayed = PlayerTiles?.Where(x => !x.GetDominoView().IsOnTable()).ToList();

            if (targetType.HasFlag(TargetType.AI))
                targetTilesPlayed = AITiles?.Where(x => !x.GetDominoView().IsOnTable()).ToList();
            return targetTilesPlayed;
        }
        public List<DragHandler> KnowTiles(TargetType targetType)
        {
            // Register the tiles played on the board
            var boardTiles = BoardTiles;

            // Register the tiles played by the target
            var targetTilesPlayed = GetHandTiles(targetType);

            return boardTiles?.Concat(targetTilesPlayed)?.ToList();
        }

        public RectTransform GetRect()
        {
            if (GamerController is null or { ExtendedGameController: null })
                throw new Exception($"GameController is not assigned in {gameObject.name}");
            return GamerController.ExtendedGameController.GetRect();
        }

        public void RestartGame(int difficulty, GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID)
        {
            if (GamerController is null or { ExtendedGameController: null })
                throw new Exception($"GameController is not assigned in {gameObject.name}");
            GamerController.ExtendedGameController.RestartGame(difficulty, gameModeID, gameTypeID, vsPlayerID);
        }


        /// <summary>
        /// Calculates the estimated probability of victory based on available game state.
        /// </summary>
        public virtual double CalculateWinningProbability(bool isPlayer, VisibleInfo visibleInfo)
        {
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
            var (rightEnd, leftEnd, topEnd, downEnd, doublesEnables) = HandController.GetBranchNumbers();

            var (probability, explantation) = CalculateVictoryProbabilityWithExplanation();

            Debug.Log($"<color={(isPlayer ? "blue>Player" : "green>AI")}</color> Winning Probability: {probability:P2}");
            Debug.Log($"Winning Explanation: \n\n{explantation}");

            return probability;

            (double probability, string explanation) CalculateVictoryProbabilityWithExplanation()
            {
                bool knowMyHand = visibleInfo.HasFlag(VisibleInfo.MyHand);
                bool knowOpponent = visibleInfo.HasFlag(VisibleInfo.OpponentHand);
                bool knowBoard = visibleInfo.HasFlag(VisibleInfo.Board);

                int myMoves = knowMyHand && knowBoard ? CountPlayableMoves(myHand, leftEnd, rightEnd) : 0;
                int myPipSum = knowMyHand ? myHand.Sum(d => d.left + d.right) : 0;

                int opponentMoves = -1;
                int opponentPipSum = -1;

                if (knowOpponent && knowBoard)
                {
                    opponentMoves = CountPlayableMoves(opponentHand, leftEnd, rightEnd);
                    opponentPipSum = opponentHand.Sum(d => d.left + d.right);
                }

                if (knowMyHand && knowOpponent && knowBoard)
                {
                    if (IsVictoryGuaranteed(myHand, opponentHand, leftEnd, rightEnd))
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

                    sb.AppendLine($"\nFinal score: {score:+0.00;-0.00} → Win probability: {probability:P2}");
                    return sb.ToString();
                }
            }

            double Sigmoid(double x)
            {
                return 1.0 / (1.0 + Math.Exp(-x));
            }
        }

        public virtual double CalculateTileDrawProbability(TargetType targetType, (byte value1, byte value2) tileToSearch)
        {
            if (DeckController == null)
                throw new Exception("DeckController is not assigned");

            var (value1, value2) = tileToSearch;

            int totalTiles = DeckController.dominoCount; // normalmente 28

            // Obtener todas las fichas visibles: en tu mano + jugadas en el tablero
            var knownTiles = KnowTiles(targetType);
            int knownCount = knownTiles?.Count ?? 0;

            // Verificar si ya conoces la ficha (jugada o en tu mano)
            bool isTileKnown = knownTiles?.Any(x => x.GetDominoView().GetDomino().IsValue(value1, value2)) ?? false;
            if (isTileKnown)
                return 0.0;

            // Fichas en el monte = total - (tu mano + mano oponente + fichas jugadas)
            var tilesInMonte = BoneyardTiles.Count;
            if (tilesInMonte <= 0)
                return 0.0;

            // Probabilidad de que al arrastrar una ficha del monte, justo obtengas esa
            var probability = 1.0 / tilesInMonte;

            return Math.Round(probability * 100, 2); // como porcentaje
        }


        // Calcula la probabilidad de que el juego se bloquee en el próximo movimiento.
        public virtual double CalculateBlockingProbability(VisibleInfo visibleInfo)
        {
            if (DeckController == null || HandController is null)
                throw new Exception($"GameController or DeckController or HandController is not assigned in {gameObject.name}");

            var playerHandTiles = GetHandTiles(TargetType.Player);
            var aiHandTiles = GetHandTiles(TargetType.AI);
            if (playerHandTiles is null or { Count: 0 } || aiHandTiles is null or { Count: 0 })
            {
                Debug.LogWarning($"Player or AI hand tiles are not assigned in {gameObject.name}");
                return 0;
            }

            var myHand = playerHandTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var opponentHand = aiHandTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var boneyard = BoneyardTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var (rightEnd, leftEnd, topEnd, downEnd, doublesEnables) = HandController.GetBranchNumbers();

            var (probability, explantation) = EstimateBlockProbability();

            Debug.Log($"Block Probability: {probability:P2}");
            Debug.Log($"Block Explanation: \n\n{explantation}");

            return probability;

            (double probability, string explanation) EstimateBlockProbability()
            {
                bool knowMyHand = visibleInfo.HasFlag(VisibleInfo.MyHand);
                bool knowOpponentHand = visibleInfo.HasFlag(VisibleInfo.OpponentHand);
                bool knowBoneyard = visibleInfo.HasFlag(VisibleInfo.Boneyard);
                bool knowBoard = visibleInfo.HasFlag(VisibleInfo.Board);

                var consideredDominoes = new List<(int left, int right)>();

                if (knowMyHand) consideredDominoes.AddRange(myHand);
                if (knowOpponentHand) consideredDominoes.AddRange(opponentHand);
                if (knowBoneyard) consideredDominoes.AddRange(boneyard);


                if (!knowBoard || consideredDominoes.Count == 0 || BoardTiles is null or { Count: 0 })
                {
                    return (0.0, "Insufficient information to estimate block probability.");
                }

                if (knowMyHand && knowOpponentHand && knowBoard)
                {
                    if (IsBlocked(myHand, opponentHand, leftEnd, rightEnd))
                    {
                        return (1.0, "Block is guaranteed: you and your opponent has no valid moves.");
                    }
                }

                int totalDominoes = consideredDominoes.Count;
                int playableCount = consideredDominoes.Count(d =>
                    d.left == leftEnd || d.right == leftEnd ||
                    d.left == rightEnd || d.right == rightEnd
                );

                int unplayableCount = totalDominoes - playableCount;
                double blockProbability = (double)unplayableCount / totalDominoes;

                string explanation = GenerateExplanation();
                return (blockProbability, explanation);

                string GenerateExplanation()
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Total considered dominoes: {totalDominoes}");
                    sb.AppendLine($"Playable dominoes (match ends): {playableCount}");
                    sb.AppendLine($"Unplayable dominoes: {unplayableCount}");
                    sb.AppendLine($"Estimated block probability: {blockProbability:P2}");
                    return sb.ToString();
                }
            }
        }

        public virtual double CalculateTieProbability(VisibleInfo visibleInfo)
        {
            if (DeckController == null || HandController is null)
                throw new Exception($"GameController or DeckController or HandController is not assigned in {gameObject.name}");

            var playerHandTiles = GetHandTiles(TargetType.Player);
            var aiHandTiles = GetHandTiles(TargetType.AI);
            if (playerHandTiles is null or { Count: 0 } || aiHandTiles is null or { Count: 0 })
            {
                Debug.LogWarning($"Player or AI hand tiles are not assigned in {gameObject.name}");
                return 0;
            }

            var myHand = playerHandTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var opponentHand = aiHandTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var boneyard = BoneyardTiles.Select(x => (left: x.GetDominoView().GetDomino().TopIndex, right: x.GetDominoView().GetDomino().BottomIndex)).ToList();
            var (rightEnd, leftEnd, topEnd, downEnd, doublesEnables) = HandController.GetBranchNumbers();

            var (probability, explantation) = EstimateDrawProbability();

            Debug.Log($"Tie Probability: {probability:P2}");
            Debug.Log($"Tie Explanation: \n\n{explantation}");

            return probability;

            (double probability, string explanation) EstimateDrawProbability()
            {
                bool knowMyHand = visibleInfo.HasFlag(VisibleInfo.MyHand);
                bool knowOpponentHand = visibleInfo.HasFlag(VisibleInfo.OpponentHand);
                bool knowBoneyard = visibleInfo.HasFlag(VisibleInfo.Boneyard);
                bool knowBoard = visibleInfo.HasFlag(VisibleInfo.Board);

                if (!knowBoard || (!knowMyHand && !knowOpponentHand) || BoardTiles is null or { Count: 0 })
                {
                    return (0.0, "Insufficient information to estimate draw probability.");
                }

                if (knowMyHand && knowOpponentHand && knowBoard)
                {
                    if (IsDraw(myHand, opponentHand, leftEnd, rightEnd))
                    {
                        return (1.0, "Draw is guaranteed: you and your opponent has no valid moves and same score");
                    }
                }

                int myPlayable = knowMyHand ? CountPlayable(myHand, leftEnd, rightEnd) : -1;
                int opponentPlayable = knowOpponentHand ? CountPlayable(opponentHand, leftEnd, rightEnd) : -1;

                bool bothBlocked = (myPlayable == 0) && (opponentPlayable == 0);

                // Determinista si se conoce todo
                if (bothBlocked && (knowBoneyard && boneyard.All(d => !MatchesEnds(d, leftEnd, rightEnd))))
                {
                    return (1.0, "Both players are blocked and the boneyard has no playable tiles. Draw is certain.");
                }

                // Probabilístico si el boneyard no es conocido
                double chanceOpponentBlocked = opponentPlayable == 0 ? 1.0 :
                                                opponentPlayable < 0 ? 0.5 : 0.0;

                double chanceMyBlocked = myPlayable == 0 ? 1.0 :
                                            myPlayable < 0 ? 0.5 : 0.0;

                double estimatedDrawProbability = chanceOpponentBlocked * chanceMyBlocked;

                var explanation = new StringBuilder();
                explanation.AppendLine($"Playable tiles - You: {(myPlayable >= 0 ? myPlayable.ToString() : "Unknown")}, Opponent: {(opponentPlayable >= 0 ? opponentPlayable.ToString() : "Unknown")}");
                explanation.AppendLine($"Known boneyard size: {(knowBoneyard ? boneyard.Count.ToString() : "Unknown")}");

                if (bothBlocked && !knowBoneyard)
                    explanation.AppendLine("Both players appear blocked. Draw depends on unknown boneyard contents.");

                explanation.AppendLine($"Estimated draw probability: {estimatedDrawProbability:P2}");

                return (estimatedDrawProbability, explanation.ToString());
            }

            int CountPlayable(List<(int left, int right)> hand, int leftEnd, int rightEnd)
            {
                return hand.Count(d => MatchesEnds(d, leftEnd, rightEnd));
            }

            bool MatchesEnds((int left, int right) domino, int leftEnd, int rightEnd)
            {
                return domino.left == leftEnd || domino.right == leftEnd || domino.left == rightEnd || domino.right == rightEnd;
            }

        }

        int CountPlayableMoves(List<(int left, int right)> hand, int leftEnd, int rightEnd)
        {
            return hand.Count(d =>
                d.left == leftEnd || d.right == leftEnd ||
                d.left == rightEnd || d.right == rightEnd);
        }

        bool IsVictoryGuaranteed(List<(int left, int right)> myHand, List<(int left, int right)> opponentHand, int leftEnd, int rightEnd)
        {
            int myPlayable = CountPlayableMoves(myHand, leftEnd, rightEnd);
            int opponentPlayable = CountPlayableMoves(opponentHand, leftEnd, rightEnd);
            return myHand.Count == 1 && myPlayable == 1 && opponentPlayable == 0;
        }

        bool IsBlocked(List<(int left, int right)> myHand, List<(int left, int right)> opponentHand, int leftEnd, int rightEnd)
        {
            return CountPlayableMoves(myHand, leftEnd, rightEnd) == 0 &&
                    CountPlayableMoves(opponentHand, leftEnd, rightEnd) == 0;
        }
        private bool IsDraw(List<(int left, int right)> myHand, List<(int left, int right)> opponentHand, int leftEnd, int rightEnd)
        {
            if (!IsBlocked(myHand, opponentHand, leftEnd, rightEnd))
                return false;

            int myPipSum = myHand.Sum(d => d.left + d.right);
            int opponentPipSum = opponentHand.Sum(d => d.left + d.right);
            return myPipSum == opponentPipSum;
        }
    }
}
