using DominoTemplate.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Concentrate mode AI logic. Focuses on maximizing impact through high-value and symmetric tiles.
    /// </summary>
    public partial class DominoAI
    {
        /// <summary>
        /// Determines best move in Concentrate mode based on high-value and doubles.
        /// </summary>
        public Domino GetBestMoveConcentrate()
        {
            var currentTurn = GameTurnController.GetCurrentTurnControl();
            var aiHand = GetHandByTurn(currentTurn);

            return GetBestMoveConcentrate_CustomHand(aiHand);
        }

        internal Domino GetBestMoveConcentrate_CustomHand(List<Domino> hand)
        {
            if (hand == null || hand.Count == 0) return null;

            var playable = GetPlayableDominos(hand);
            if (playable.Count == 0) return null;

            Domino best = null;
            float bestScore = float.NegativeInfinity;

            foreach (var tile in playable)
            {
                float score = 0;

                // Prioritize high-value tiles
                if (tile.TopIndex + tile.BottomIndex >= 10)
                    score += 2f;

                // Bonus for doubles (symmetric tiles may have strategic value)
                if (tile.TopIndex == tile.BottomIndex)
                    score += 3f;

                // Slight flexibility bonus
                score += hand.Count(t => t.HasValue(tile.TopIndex));
                score += hand.Count(t => t.HasValue(tile.BottomIndex));

                if (score > bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }

            Debug.Log($"[Concentrate AI] Best move selected: {best.TopIndex}|{best.BottomIndex} with score {bestScore}");
            return best;
        }

        /// <summary>
        /// Debug scoring function for Concentrate mode.
        /// </summary>
        public void DebugConcentrateAIScoring_TopAI(Action<Dictionary<Domino, float>> callback = null)
        {
            var aiHand = GetHandByTurn(GameTurnController.GetCurrentTurnControl());
            if (aiHand == null || aiHand.Count == 0) return;

            var playable = GetPlayableDominos(aiHand);

            var scored = GetScoredDominos(playable, tile =>
            {
                float s = 0;
                if (tile.TopIndex + tile.BottomIndex >= 10) s += 2f;
                if (tile.TopIndex == tile.BottomIndex) s += 3f;
                s += aiHand.Count(t => t.HasValue(tile.TopIndex));
                s += aiHand.Count(t => t.HasValue(tile.BottomIndex));
                return s;
            });

            foreach (var kv in scored.OrderBy(x => x.Value))
                Debug.Log($"[Concentrate Debug] {kv.Key.TopIndex}|{kv.Key.BottomIndex} Å® {Mathf.RoundToInt(kv.Value * 100)}%");

            callback?.Invoke(scored);
        }
    }
}
