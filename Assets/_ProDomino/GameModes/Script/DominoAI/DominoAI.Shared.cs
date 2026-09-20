using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProDomino.GameModes
{
    public partial class DominoAI
    {
        public AbstractGameMode CurrentGameMode { get; private set; }
        public GameTurnController GameTurnController { get; private set; }
        public SlotHelper SlotHelper { get; private set; }
        public DeckController DeckController { get; private set; }
        public IAKnowledgeProfile KnowledgeProfile { get; private set; }

        public DominoAI(AbstractGameMode gameMode, GameTurnController gameTurnController, SlotHelper slotHelper, DeckController deckController)
        {
            CurrentGameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
            GameTurnController = gameTurnController ?? throw new ArgumentNullException(nameof(gameTurnController));
            SlotHelper = slotHelper ?? throw new ArgumentNullException(nameof(slotHelper));
            DeckController = deckController ?? throw new ArgumentNullException(nameof(deckController));
            KnowledgeProfile = IAKnowledgeProfile.Realistic; // Default knowledge profile
        }

        protected List<Domino> GetDominosFrom(List<DragHandler> handlers, bool isCheckingIfIsInOnHand = false)
        {
            return handlers
                .Where(h => h != null
                    && h.GetDominoView() != null
                    && (!isCheckingIfIsInOnHand || !h.GetDominoView().IsOnTable()))
                .Select(h => h.GetDominoView().GetDomino())
                .Where(d => d != null)
                .ToList();
        }

        public List<Domino> GetBoardDominos() => GetDominosFrom(DeckController.GetList(-1));
        public List<Domino> GetBoneyardDominos() => GetDominosFrom(DeckController.GetList(0));
        public List<Domino> GetPlayerDominos() => GetDominosFrom(DeckController.PlayerTiles);
        public List<Domino> GetTopAIDominos() => GetDominosFrom(DeckController.TopAITiles);
        public List<Domino> GetLeftAIDominos() => GetDominosFrom(DeckController.LeftAITiles);
        public List<Domino> GetRightAIDominos() => GetDominosFrom(DeckController.RightAITiles);

        public int[] GetOpenBranchesArray()
        {
            return SlotHelper.TellBranchNums();
        }

        /// <summary>
        /// Determine if a domino is playable based on current open branches.<br></br>
        /// This got you only the dominos that can be played right now without any special conditions.
        /// </summary>
        public bool IsPlayable(Domino domino)
        {
            // Return only the branches that are available (if its value is 99, is blocked)
            var openValues = GetOpenBranchesArray();

            // If the game mode is draw or block, only the first two branches are playable
            if (CurrentGameMode?.GameModeID is GameMode.draw or GameMode.block)
            { 
                Debug.Log($"<color={Consts.Colors.Process}><b>[AI]</b> Checking PLAYABILITY of Domino ID {domino.id} in {CurrentGameMode.GameModeID} mode with open values [{string.Join(", ", openValues)}]</color>");
                openValues = openValues.Take(2).ToArray(); // In draw and block modes, only the first two branches are playable
            }

            if (openValues.Any(x => domino.HasValue(x)))
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[AI]</b> Domino ID {domino.id} is PLAYABLE on open values [{string.Join(", ", openValues)}]</color>");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine if a domino is playable based on current open branches.<br></br>
        /// This got you only the dominos that can be played right considering specific game mode confitions.
        /// </summary>
        public bool IsPlayable_SpecificRules(Domino domino)
        {
            return CurrentGameMode.GameModeID switch
            {
                GameMode.block => IsPlayable_SpecificRules_Block(domino),
                GameMode.french => IsPlayable_SpecificRules_French(domino),
                GameMode.draw => IsPlayable_SpecificRules_Draw(domino),
                GameMode.five => IsPlayable_SpecificRules_Five(domino),
                _ => IsPlayable_SpecificRules_Draw(domino)
            };
        }

        public List<Domino> GetHandByTurn(int turn)
        {
            return turn switch
            {
                1 => GetPlayerDominos(),
                2 => GetLeftAIDominos(),
                3 => GetTopAIDominos(),
                4 => GetRightAIDominos(),
                _ => null
            };
        }

        /// <summary>
        /// This function gets all playable dominos from a given hand.<br></br>
        /// This got you only the dominos that can be played right now without any special conditions.
        /// </summary>
        /// <param name="hand"></param>
        /// <returns></returns>
        public List<Domino> GetPlayableDominos(List<Domino> hand)
        {
            return hand.Where(IsPlayable).ToList();
        }
        
        /// <summary>
        /// This function gets all playable dominos from a given hand.<br></br>
        /// This got you only the dominos that can be played right now without any special conditions.
        /// </summary>
        /// <param name="hand"></param>
        /// <returns></returns>
        public List<Domino> GetPlayableDominos_SpecificRules(List<Domino> hand)
        {
            return hand.Where(IsPlayable_SpecificRules).ToList();
        }

        /// <summary>
        /// Returns a dictionary of playable dominos with normalized scores (0 to 1), using a custom scoring function.
        /// This can be used to visualize "how good" each move is.
        /// </summary>
        public Dictionary<Domino, float> GetScoredDominos(List<Domino> availableHand, Func<Domino, float> scoreFunc)
        {
            var scored = availableHand.ToDictionary(d => d, d => scoreFunc(d));

            // Check if there are no scored dominos
            if (scored is null or { Count: 0 })
                return null;

            // If there's only one tile, return it with a score of 1 (100% quality)
            if (availableHand.Count is 1)
                return scored.ToDictionary(kvp => kvp.Key, kvp => 1f); // Single tile is always the best

            float max = scored.Values.Max();
            float min = scored.Values.Min();
            float range = Math.Max(1e-5f, max - min); // Avoid division by zero

            return scored.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value - min) / range
            );
        }

        /// <summary>
        /// Constructs a list of all dominos currently visible or known by the AI.
        /// Visibility depends on the configured KnowledgeProfile.
        /// </summary>
        public List<Domino> BuildKnowledge(List<Domino> aiHand)
        {
            var known = aiHand.Concat(GetBoardDominos());

            if (KnowledgeProfile.CanSeePlayerHand)
                known = known.Concat(GetPlayerDominos());
            if (KnowledgeProfile.CanSeeBoneyard)
                known = known.Concat(GetBoneyardDominos());

            return known.ToList();
        }


        public void SetKnowledgeProfile(IAKnowledgeProfile iAKnowledgeProfile)
        {
            KnowledgeProfile = iAKnowledgeProfile ?? throw new ArgumentNullException(nameof(iAKnowledgeProfile));
        }

        /// <summary>
        /// Evaluates a potential move, returning:
        /// AEV (Average Expected Value),
        /// MEV (Maximum Expected Value of all alternatives in hand),
        /// EMC (Expected Mistake Calculation).
        /// </summary>
        public MoveEvaluationResult EvaluateMove(
            int tileIDToPlay,
            GameMode gameMode,
            Dictionary<int, List<Domino>> clientTilesIDsCollection,
            List<Domino> boardTiles,
            List<Domino> boneyardTiles,
            int kFactor)
        {
            // Build evaluation context
            var ctx = BuildEvalContext(tileIDToPlay, gameMode, clientTilesIDsCollection, boardTiles, boneyardTiles);
            if (ctx is null)
                return new MoveEvaluationResult { AEV = null, MEV = null, EMC = 0 };

            // --- Check if the move is valid ---
            // Build list of playable moves from this hand
            var playable = GetPlayableDominos(ctx.Hand);

            // If the hand has 0 or 1 tiles, or if there are no open values, return neutral evaluation
            if (playable.Count <= 1 || (ctx.OpenValues.Length <= 1 && ctx.OpenValues.Contains(-1)))
                return new MoveEvaluationResult { AEV = 0, MEV = 0, EMC = 0 };

            // --- Normalized scoring for stability ---
            // Use GetScoredDominos to ensure values are in [0,1]
            var scored = GetScoredDominos(ctx.Hand, tile =>
                ctx.GetScoreAction(tile, ctx.Hand, ctx.KnownTiles, ctx.OpenValues));

            if (scored == null || !scored.ContainsKey(ctx.DominoPlayed))
                return new MoveEvaluationResult { AEV = null, MEV = null, EMC = 0 };

            var aev = scored[ctx.DominoPlayed];          // Normalized score of the chosen move
            var mev = scored.Values.Max();               // Best possible normalized score
            var emc = -kFactor * (mev - aev);            // EMC always <= 0

            Debug.Log($"<color={Consts.Colors.Process}><b>[AI]</b> Evaluated Move" +
                      $"\n\nTileID: {tileIDToPlay}" +
                      $"\nAEV(norm): {aev:F2}" +
                      $"\nMEV(norm): {mev:F2}" +
                      $"\nEMC: {emc:F2}</color>");

            return new MoveEvaluationResult
            {
                AEV = aev,
                MEV = mev,
                EMC = emc
            };
        }


        /// <summary>
        /// Builds the evaluation context required for computing AEV, MEV and EMC.
        /// The context contains: 
        /// - The selected tile
        /// - The current hand
        /// - All known tiles (hand, board, boneyard, visible opponents)
        /// - The active scoring function
        /// - The current open values
        /// </summary>
        /// <param name="tileIDToPlay">The ID of the tile to evaluate.</param>
        /// <param name="gameMode">The current game mode (french, block, draw, five).</param>
        /// <param name="clientTilesIDsCollection">Dictionary of tiles for all clients.</param>
        /// <param name="boardTiles">Tiles currently placed on the board.</param>
        /// <param name="boneyardTiles">Tiles remaining in the boneyard.</param>
        /// <returns>An EvalContext object with all relevant evaluation data.</returns>
        private EvalContext BuildEvalContext(
            int tileIDToPlay,
            GameMode gameMode,
            Dictionary<int, List<Domino>> clientTilesIDsCollection,
            List<Domino> boardTiles,
            List<Domino> boneyardTiles)
        {
            // Try to find which client's hand contains the tile to play
            var tilesCollection_Client0 = clientTilesIDsCollection.ElementAtOrDefault(0).Value;
            var tilesCollection_Client1 = clientTilesIDsCollection.ElementAtOrDefault(1).Value;
            var tilesCollection_Client2 = clientTilesIDsCollection.ElementAtOrDefault(2).Value;
            var tilesCollection_Client3 = clientTilesIDsCollection.ElementAtOrDefault(3).Value;

            var hand = default(List<Domino>);
            var dominoPlayed = default(Domino);

            if (tilesCollection_Client0 != null && SearchInList(tilesCollection_Client0, out dominoPlayed))
                hand = tilesCollection_Client0;
            else if (tilesCollection_Client1 != null && SearchInList(tilesCollection_Client1, out dominoPlayed))
                hand = tilesCollection_Client1;
            else if (tilesCollection_Client2 != null && SearchInList(tilesCollection_Client2, out dominoPlayed))
                hand = tilesCollection_Client2;
            else if (tilesCollection_Client3 != null && SearchInList(tilesCollection_Client3, out dominoPlayed))
                hand = tilesCollection_Client3;

            if (hand is null)
                return null;

            // Build knowledge base: visible tiles (all clients, boneyard, board)
            var knownTiles = new List<Domino>();

            // Register all visible client tiles
            if (tilesCollection_Client0 is not null and { Count: > 0 })
                knownTiles.AddRange(tilesCollection_Client0);

            if (tilesCollection_Client1 is not null and { Count: > 0 })
                knownTiles.AddRange(tilesCollection_Client1);

            if (tilesCollection_Client2 is not null and { Count: > 0 })
                knownTiles.AddRange(tilesCollection_Client2);

            if (tilesCollection_Client3 is not null and { Count: > 0 })
                knownTiles.AddRange(tilesCollection_Client3);

            // Register boneyard and board tiles if the knowledge profile allows it
            if (boneyardTiles is not null and { Count: > 0})
                knownTiles.AddRange(boneyardTiles);

            // Board tiles are always known
            if (boardTiles is not null and { Count: > 0 })
                knownTiles.AddRange(boardTiles);

            // Clean up known tiles: remove nulls and duplicates
            knownTiles = knownTiles
                ?.Where(x => x != null)
                ?.Distinct()
                ?.ToList();

            // Select the scoring function depending on the game mode
            var getScoreAction = (Func<Domino, List<Domino>, List<Domino>, int[], float>)(gameMode switch
            {
                GameMode.french => ScoreFrenchTile,
                GameMode.block => ScoreBlockTile,
                GameMode.draw => ScoreDrawTile,
                GameMode.five => ScoreFiveTile,
                _ => throw new NotImplementedException()
            });

            // Select open-values function depending on the game mode
            var openValuesAction = (Func<List<int>, int[]>)(gameMode switch
            {
                GameMode.french => _doubles => GetFrenchOpenBranchArray(out _doubles),
                GameMode.block => _doubles => GetBlockOpenBranchArray(out _doubles),
                GameMode.draw => _doubles => GetDrawOpenBranchArray(out _doubles),
                GameMode.five => _doubles => GetFiveOpenBranchArray(out _doubles),
                _ => throw new NotImplementedException()
            });

            // Compute current open values
            var doubles = new List<int>();
            var openValues = openValuesAction.Invoke(doubles);

            Debug.Log($"<color={Consts.Colors.Process}><b>[AI]<b> Building EvalContext - TileID: {tileIDToPlay}, Hand: {hand.Count} tiles, KnownTiles: {knownTiles.Count} tiles, OpenValues: [{string.Join(", ", openValues)}]" +
                $"\n\nHand:\n{JsonConvert.SerializeObject(hand, Formatting.Indented)}" +
                $"\n\nKnownTiles:\n{JsonConvert.SerializeObject(knownTiles, Formatting.Indented)}" +
                $"\n\nOpenValues:\n{JsonConvert.SerializeObject(openValues, Formatting.Indented)}" +
                $"</color>");

            return new EvalContext
            (
                dominoPlayed: dominoPlayed,
                hand: hand,
                knownTiles: knownTiles,
                openValues: openValues,
                getScoreAction: getScoreAction
            );

            // Local function: tries to locate the tile in a given list
            bool SearchInList(List<Domino> list, out Domino dominoPlayed)
            {
                dominoPlayed = list.FirstOrDefault(d => d.id == tileIDToPlay);
                return dominoPlayed != null;
            }
        }



        /// <summary>
        /// Controls what parts of the game state the AI can access based on difficulty or debug mode.
        /// </summary>
        public class IAKnowledgeProfile
        {
            public bool CanSeePlayerHand { get; set; } = false;
            public bool CanSeeBoneyard { get; set; } = true;
            public bool CanSeeAllDominoes => CanSeePlayerHand && CanSeeBoneyard;

            public static readonly IAKnowledgeProfile Realistic = new()
            {
                CanSeePlayerHand = false,
                CanSeeBoneyard = false
            };

            public static readonly IAKnowledgeProfile Undertaker = new()
            {
                CanSeePlayerHand = false,
                CanSeeBoneyard = true
            };

            public static readonly IAKnowledgeProfile Omniscient = new()
            {
                CanSeePlayerHand = true,
                CanSeeBoneyard = true
            };
        }

        // Contexto con toda la info que tanto GetAEV como EvaluateMove necesitan
        public class EvalContext
        {
            public Domino DominoPlayed { get; private set; }
            public List<Domino> Hand { get; private set; }
            public List<Domino> KnownTiles { get; private set; }
            public int[] OpenValues { get; private set; }
            public Func<Domino, List<Domino>, List<Domino>, int[], float> GetScoreAction { get; private set; }

            public EvalContext(Domino dominoPlayed, List<Domino> hand, List<Domino> knownTiles, int[] openValues, Func<Domino, List<Domino>, List<Domino>, int[], float> getScoreAction)
            {
                DominoPlayed = dominoPlayed;
                Hand = hand;
                KnownTiles = knownTiles;
                OpenValues = openValues;
                GetScoreAction = getScoreAction;
            }
        }

        public class MoveEvaluationResult
        {
            public float? AEV; // Jugada elegida
            public float? MEV; // Mejor jugada posible
            public float EMC;  // Diferencia escalada
        }
    }
}
