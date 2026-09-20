using DominoTemplate.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Draw mode AI logic. Seeks flexibility and gradual advantage.
    /// </summary>
    public partial class DominoAI
    {
        /// <summary>
        /// Determines the best move for the AI in Block mode.
        /// Prioritizes tiles that reduce the number of available values in play.
        /// </summary>
        private int[] GetDrawOpenBranchArray(out List<int> doubleTilesEnabled)
        {
            int rightBranch = 0;
            var leftBranch = 0;
            var topNum = 0;
            var downNum = 0;

            var doubleTiles = new List<int>();
            SlotHelper.TellBranchNums(ref rightBranch, ref leftBranch, ref topNum, ref downNum, ref doubleTiles);
            doubleTilesEnabled = doubleTiles;

            // Return only the branches that are available (if its value is 99, is blocked)
            return new[] { rightBranch, leftBranch, topNum, downNum }
                ?.Where(x => x is not 99)
                ?.Distinct()
                ?.ToArray();
        }

        /// <summary>
        /// Determine if a domino is playable based on current open branches.<br></br>
        /// This got you only the dominos that can be played right considering specific game mode confitions.
        /// </summary>
        public bool IsPlayable_SpecificRules_Draw(Domino domino)
        {
            var openValues = GetDrawOpenBranchArray(out var doubles);
            return openValues.Any(x => domino.HasValue(x));
        }

        public Domino[] GetFirstTurnAvailableTilesDraw()
        {
            var currentTurnControl = GameTurnController.GetCurrentTurnControl();

            // Get the dominos required to filter for the possible first ones
            var aiHand = GetHandByTurn(currentTurnControl);
            return GetFirstTurnAvailableTilesDraw_CustomHand(aiHand);
        }
        
        public Domino[] GetFirstTurnAvailableTilesDraw_CustomHand(List<Domino> hand)
        {
            var allBoard = GetBoardDominos();
            var playable = GetPlayableDominos(hand);

            // Ensure we have a valid AI hand to evaluate
            if (hand == null || hand.Count == 0)
            {
                Debug.LogWarning("[AI Decision] No AI hand available for scoring.");
                return null;
            }

            // If is the first turn, filter the playable domino to get the first expeceted move of the round
            DetermineDrawFirstAvailableTiles(ref playable, hand, allBoard);

            // Return the filtered first moves that the current opponent could do
            return playable?.ToArray();
        }

        /// <summary>
        /// Determines the best possible move for the Top AI player in Draw mode (Pro difficulty),
        /// using a knowledge-constrained profile and an adaptive scoring system.
        /// </summary>
        public Domino GetBestMoveDraw()
        {
            var currentTurnControl = GameTurnController.GetCurrentTurnControl();
            var aiHand = GetHandByTurn(currentTurnControl);

            return GetBestMoveDraw_CustomHand(aiHand);
        }

        internal Domino GetBestMoveDraw_CustomHand(List<Domino> hand)
        {
            var isFirstTurn = GameTurnController.TurnCount is 0;

            // Ensure we have a valid AI hand to evaluate
            if (hand == null || hand.Count == 0)
            {
                Debug.LogWarning("[AI Decision] No AI hand available for scoring.");
                return null;
            }

            var playerHand = GetPlayerDominos();
            var allBoard = GetBoardDominos();
            var boneyard = GetBoneyardDominos();

            // Get the tiles that could be played without considering other rules
            var playable = GetPlayableDominos(hand);

            // If is the first turn, filter the playable domino to get the first expeceted move of the round
            if (isFirstTurn)
                DetermineDrawFirstAvailableTiles(ref playable, hand, allBoard);

            // Get the branches that could be used
            var openValues = GetDrawOpenBranchArray(out var requiredDoublesToOpenBranch);

            // If there are open branches, filter to use only the open available ones
            if (openValues is not null and { Length: > 0 })
                playable = playable.Where(x => openValues.Any(y => y is -1 || x.HasValue(y)))?.ToList();

            // If there aren't open branches, filter to use only the doubles that are required in each branch
            else
                playable = playable.Where(x => x.IsDouble() && requiredDoublesToOpenBranch.Any(y => x.HasValue(y)))?.ToList();

            // Construct knowledge base based on visibility
            var allKnown = hand.Concat(allBoard);
            if (KnowledgeProfile.CanSeePlayerHand)
                allKnown = allKnown.Concat(playerHand);
            if (KnowledgeProfile.CanSeeBoneyard)
                allKnown = allKnown.Concat(boneyard);
            var knownTiles = allKnown.ToList();

            Domino bestTile = null;
            float bestScore = float.NegativeInfinity;

            foreach (var tile in playable)
            {
                float score = ScoreDrawTile(tile, hand, knownTiles, openValues);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = tile;
                }
            }

            if (bestTile != null)
                Debug.Log($"[AI Decision] Best tile chosen: {bestTile.TopIndex}|{bestTile.BottomIndex} with score {bestScore}");

            return bestTile;
        }

        /// <summary>
        /// Calculates and optionally returns the Draw AI scoring probabilities for the player.
        /// </summary>
        /// <param name="onGetProbabilities">Optional callback to receive the calculated probabilities.</param>
        public void DebugDrawAIScoring_Player(Action<Dictionary<Domino, float>> onGetProbabilities = null)
        {
            DetermineProbabilities_Draw(1, onGetProbabilities);
        }

        /// <summary>
        /// Logs normalized quality scores for each playable domino in Draw mode (Pro),
        /// based on the same logic used by the AI.
        /// </summary>
        public void DebugDrawAIScoring_AI(Action<Dictionary<Domino, float>> onGetProbabilities = null)
        {
            var turn = GameTurnController.GetPlayerTurn()
                ? GameTurnController.GetNextTurn()
                : GameTurnController.GetCurrentTurnControl();

            DetermineProbabilities_Draw(turn, onGetProbabilities);
        }

        /// <summary>
        /// Logs normalized quality scores for each playable domino in Draw mode (Pro),
        /// based on the same logic used by the AI.
        /// </summary>
        public void DetermineProbabilities_Draw(int turn, Action<Dictionary<Domino, float>> onGetProbabilities = null)
        {
            var nextTurnDominos = GetHandByTurn(turn);
            if (nextTurnDominos == null || nextTurnDominos.Count == 0)
                return;

            var isFirstTurn = GameTurnController.TurnCount == 0;
            var playable = GetPlayableDominos(nextTurnDominos);

            if (isFirstTurn)
                DetermineDrawFirstAvailableTiles(ref playable, nextTurnDominos, GetBoardDominos());

            var openValues = GetDrawOpenBranchArray(out var requiredDoublesToOpenBranch);

            // Filter only by playable branches
            if (openValues is { Length: > 0 })
                playable = playable.Where(x => openValues.Any(v => x.HasValue(v))).ToList();
            
            // No branches opened: only allow required doubles
            else
                playable = playable
                    .Where(x => x.IsDouble() && requiredDoublesToOpenBranch.Any(v => x.HasValue(v)))
                    .ToList();

            var knownTiles = BuildKnowledge(nextTurnDominos);

            var scored = GetScoredDominos(playable, tile =>
                ScoreBlockTile(tile, nextTurnDominos, knownTiles, openValues));

            if (scored is { Count: > 0 })
            {
                Debug.Log("AI - Move Evaluation (Draw Pro):");
                foreach (var kvp in scored.OrderBy(k => k.Value))
                {
                    var d = kvp.Key;
                    var pct = Mathf.RoundToInt(kvp.Value * 100f);
                    Debug.Log($"Domino {d.TopIndex}|{d.BottomIndex} �� {pct}% move quality");
                }
            }

            onGetProbabilities?.Invoke(scored);
        }


        /// <summary>
        /// Core scoring logic for evaluating a single domino in Draw mode (Pro).
        /// This function is reused by both the decision and debugging logic.
        /// </summary>
        private float ScoreDrawTile(Domino tile, List<Domino> aiHand, List<Domino> knownTiles, int[] openValues)
        {
            int v1 = tile.TopIndex;
            int v2 = tile.BottomIndex;
            float score = 0f;

            bool isLateGame = aiHand.Count <= 3;

            // Prioritize discarding high-point tiles, especially near the end of the game
            float pointWeight = isLateGame ? 2.5f : 1.0f;
            score += (v1 + v2) * pointWeight;

            // Favor values that are frequently seen (more tactical control)
            score += knownTiles.Count(t => t.HasValue(v1));
            score += knownTiles.Count(t => t.HasValue(v2));

            // Favor values that match others in hand (flexibility to follow up)
            score += aiHand.Count(t => t.HasValue(v1)) * 2;
            score += aiHand.Count(t => t.HasValue(v2)) * 2;

            // Reward doubles for their branching power
            if (v1 == v2)
                score += 3;

            // Encourage playing values that may be exhausted
            if (knownTiles.Count(t => t.HasValue(v1)) >= 7)
                score += 2;
            if (knownTiles.Count(t => t.HasValue(v2)) >= 7)
                score += 2;

            // Advanced: evaluate whether playing this tile opens a new branch (if it's a double)
            if (tile.IsDouble())
            {
                bool opensNewBranch = !openValues.Contains(tile.TopIndex);
                if (opensNewBranch)
                {
                    int support = aiHand.Count(t => t.HasValue(tile.TopIndex));
                    float supportFactor = Mathf.Clamp(support - 1, 0, 4);

                    // Reward opening a branch if we have tiles to support it
                    score += supportFactor * 1.5f;

                    // Penalize opening branches blindly (without backup)
                    if (supportFactor == 0)
                        score -= 4f;
                }
            }

            // Penalty if the tile continues a saturated value
            if (knownTiles.Count(t => t.HasValue(v1)) >= 6)
                score -= 1f;
            if (knownTiles.Count(t => t.HasValue(v2)) >= 6)
                score -= 1f;

            // Bonus for blocking the player if their hand is visible
            if (KnowledgeProfile.CanSeePlayerHand)
            {
                var playerHand = GetPlayerDominos();
                bool playerCanPlay = playerHand.Any(p => openValues.Any(x => p.HasValue(x)));
                if (!playerCanPlay)
                    score += 5;
            }

            Debug.Log($"[AI Debug] {tile.TopIndex}|{tile.BottomIndex} �� score: {score}");
            return score;
        }


        private void DetermineDrawFirstAvailableTiles(ref List<Domino> playableDominos, List<Domino> aiHand, List<Domino> allBoard)
        {
            // Check if there aren't any possible play yet
            if (aiHand is null or { Count: 0 }  // The Ai hand has values
                || playableDominos is null or { Count: > 0 } // There aren't any possible play yet
                || allBoard is null or { Count: > 0 }) // The board is still empty
                return;

            playableDominos = aiHand;

            /*
            // First move: If the current turn is the first one; try to get the highest double beetween every opponent
            var doubles = hand?.Where(x => x.IsDouble());

            // TODO: comented due rules preference, uncomment if needed
            // Try to get first the 0|0 double
            // var highestDouble = doubles.FirstOrDefault(x => x.id is 0);

            // If the 0|0 double is not available, try to get the highest double
            // highestDouble ??= doubles
            //    ?.OrderByDescending(x => x.TopIndex + x.BottomIndex)
            //    ?.FirstOrDefault();
            
            // If the 0|0 double is not available, try to get the highest double
            var highestDouble = doubles
                ?.OrderByDescending(x => x.TopIndex + x.BottomIndex)
                ?.FirstOrDefault();

            // Only if the player has a double it could be registered to play
            if (highestDouble is not null)
                playableDominos = new() { highestDouble };
            */
        }
    }
}
