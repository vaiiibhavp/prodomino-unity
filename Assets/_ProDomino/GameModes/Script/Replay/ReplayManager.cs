using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Timba.Patterns;
using UnityEngine;

namespace ProDomino.ReplaySystem
{
    /// <summary>
    /// Manages the creation, storage, loading, and manipulation of match replay data, including turn-by-turn actions,
    /// scores, and player information, with support for saving and retrieving replays from persistent storage slots.
    /// 
    /// NOTE: In online game modes is necessary to ensure that the replay data is synchronized between players. To guarantee this, the replay data should be generated and updated on the server side, and then sent to each client after every turn. This way, all players will have the same replay data for the match, allowing them to save and review the replay accurately. Additionally, when a player saves a replay, it should be saved locally on their device, but the replay data can also be sent to the server for backup or sharing purposes if desired.
    /// </summary>
    public class ReplayManager : MonoBehaviour
    {
        public static ReplayManager Instance { get; private set; }

        private bool isSave = false;
        private int turnCounter = 0;
        private List<(int slotIndex, MatchReplay replay, string date)> loadedReplays = new();
        private readonly List<MatchReplay> currentReplayList = new();
        private PromptFadeController promptFadeController;

        private const int max_slots = 10;

        public MatchReplay CurrentReplay { get; private set; }
        public IReadOnlyList<MatchReplay> CurrentReplayList => currentReplayList;

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("More than one GameManager instance!");
            }
            Instance = this;
        }

        private void Start()
        {
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();
        }

        /// <summary>
        /// Loads all replays from slots, updates the current replay list with the loaded replays, and logs details
        /// about each replay and the total count.
        /// </summary>
        public void RunLoadSlots()
        {
            // Load all replays from slots and log the results, including the number of replays loaded and details for each replay
            loadedReplays = LoadAllSlotsOrdered();
            if (loadedReplays is null)
            {
                Debug.LogError("Failed to load replays from slots.");
                return;
            }

            // Clear the current replay list before adding the newly loaded replays
            currentReplayList.Clear();

            // Add the loaded replays to the current replay list and log the updated list, including details for each replay such as ID, date, game mode, game type, and number of players
            foreach (var (slotIndex, replay, date) in loadedReplays)
                currentReplayList.Add(replay);

            Debug.Log($"Current replay list updated with loaded replays. Total replays in current list: {currentReplayList.Count}\n\n" +
                $"{string.Join("\n", currentReplayList.Select(r => $"Replay ID: {r.matchId}, Date: {r.date}, Game Mode: {r.gameMode}, Game Type: {r.gameType}, Players: {r.numberPlayers}"))}");

            /// Carga todos los slots existentes, los convierte a MatchReplay
            /// y los devuelve ordenados por fecha (más reciente → más antiguo)
            List<(int slotIndex, MatchReplay replay, string date)> LoadAllSlotsOrdered()
            {
                var loadedSlots = new List<(int slotIndex, MatchReplay replay, string date)>();

                for (int i = 1; i <= max_slots; i++)
                {
                    string dataKey = $"slot{i}_data";
                    string dateKey = $"slot{i}_date";

                    if (PlayerPrefs.HasKey(dataKey))
                    {
                        // Get the JSON string for the replay data from PlayerPrefs and check if it's not empty before attempting to deserialize it, logging an error if the data is empty and skipping that slot
                        var json = PlayerPrefs.GetString(dataKey);
                        if (string.IsNullOrEmpty(json))
                        {
                            Debug.LogError($"[ReplayManager_LoadAllSlotsOrdered] Slot {i} has empty data. Skipping.");
                            continue;
                        }

                        var dateStr = PlayerPrefs.HasKey(dateKey)
                            ? PlayerPrefs.GetString(dateKey)
                            : "01/01/2000";

                        try
                        {
                            var replay = JsonUtility.FromJson<MatchReplay>(json);

                            // Defensive check in case JSON is valid but maps to null
                            if (replay == null)
                                throw new JsonSerializationException("Deserialized replay is null");

                            replay.saveSlotIndex = i;
                            loadedSlots.Add((i, replay, dateStr));
                        }
                        catch (JsonException ex) // Base class for JsonSerializationException + JsonReaderException
                        {
                            Debug.LogError(
                                $"[ReplayManager_LoadAllSlotsOrdered] Failed to deserialize replay data for slot {i}. " +
                                $"Error: {ex.Message}"
                            );
                        }
                        catch (Exception ex) // Optional safety net
                        {
                            Debug.LogError(
                                $"[ReplayManager_LoadAllSlotsOrdered] Unexpected error while loading slot {i}. " +
                                $"Error: {ex}"
                            );
                        }
                    }
                }

                // Order the loaded slots by date, parsing the date string and handling any potential parsing errors by assigning a default minimum date value, ensuring that slots with invalid or missing dates are treated as the oldest
                loadedSlots = loadedSlots
                    .OrderByDescending(slot =>
                    {
                        if (DateTime.TryParse(slot.date, out DateTime parsedDate))
                            return parsedDate;
                        return DateTime.MinValue; // por si alguna fecha está mal formateada
                    })
                    .ToList();

                return loadedSlots;
            }
        }

        /// <summary>
        /// Saves the current replay data to the next available slot and displays a confirmation message. If no replay
        /// data is available, shows a warning.
        /// </summary>
        public void SaveReplayData()
        {
            // Check if there is replay data to save
            if (CurrentReplay is null)
            { 
                Debug.LogWarning("No current replay data to save.");
                promptFadeController?.Fade("Couldn't save replay data: No current replay.");

                return;
            }

            // Save the replay data to the next available slot if it hasn't been saved yet
            if (!isSave)
            {
                isSave = true;
                SaveMatchToNextAvailableSlot(CurrentReplay);

                promptFadeController?.Fade($"Replay with id <b>{CurrentReplay.matchId}</b> was saved successfully!");
            }

            /// Saves the provided match replay to the next available slot, or replaces the oldest slot if all are occupied.
            void SaveMatchToNextAvailableSlot(MatchReplay replay)
            {
                if (replay is null)
                {
                    Debug.LogError("Cannot save null replay data.");
                    return;
                }

                var json = string.Empty;
                try
                {
                    // Serialize the replay data to JSON format for storage in PlayerPrefs
                    json = JsonUtility.ToJson(replay);
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"Failed to serialize replay data: {ex.Message}");
                    return;
                }

                // 1️. Search for a free slot (without replay data) to save the new replay
                for (int i = 1; i <= max_slots; i++)
                {
                    string keyData = $"slot{i}_data";
                    if (!PlayerPrefs.HasKey(keyData))
                    {
                        SaveToSlot(i, json, replay.date);

                        Debug.Log($"[ReplayManager_SaveMatchToNextAvailableSlot] Saved to slot{i} (free slot found).");
                        return;
                    }
                }

                // 2️. If no free slots are found, find the oldest slot
                var oldestSlot = FindOldestSlot();
                SaveToSlot(oldestSlot, json, replay.date);

                Debug.Log($"[ReplayManager_SaveMatchToNextAvailableSlot] Replaced slot{oldestSlot} (oldest slot).");

                /// Save the replay data and date to the specified slot index in PlayerPrefs
                void SaveToSlot(int slotIndex, string json, string date)
                {
                    PlayerPrefs.SetString($"slot{slotIndex}_data", json);
                    PlayerPrefs.SetString($"slot{slotIndex}_date", date);
                    PlayerPrefs.Save();
                }

                /// Finds the slot with the oldest saved date among all available slots.
                int FindOldestSlot()
                {
                    var oldestSlot = 1;
                    var oldestDate = DateTime.MaxValue;

                    for (int i = 1; i <= max_slots; i++)
                    {
                        var dateKey = $"slot{i}_date";
                        if (PlayerPrefs.HasKey(dateKey))
                        {
                            var dateStr = PlayerPrefs.GetString(dateKey);
                            if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                            {
                                if (parsedDate < oldestDate)
                                {
                                    oldestDate = parsedDate;
                                    oldestSlot = i;
                                }
                            }
                        }
                    }
                    return oldestSlot;
                }
            }
        }

        #region Replay in Match Game Flow
        /// <summary>
        /// Initializes replay data for a match, setting up match details and player information based on the provided
        /// parameters.
        /// </summary>
        /// <param name="auxGameMode">The game mode for the match replay.</param>
        /// <param name="auxGameType">The game type for the match replay.</param>
        /// <param name="auxNumberPlayers">The number of players participating in the match.</param>
        /// <param name="auxLocalPlayerIndexId">The index of the local player in the match.</param>
        /// <param name="playersInfo">A list containing replay information for each player.</param>
        public void InitializeReplayData(GameMode auxGameMode, GameType auxGameType, NumberPlayers auxNumberPlayers, int auxLocalPlayerIndexId, List<PlayerInfoReplay> playersInfo)
        {
            isSave = false;
            turnCounter = 0;

            // Initialize the CurrentReplay with the provided parameters and an empty list of turns
            CurrentReplay = new MatchReplay
            {
                matchId = Guid.NewGuid().ToString(),
                date = DateTime.Now.ToString("MM/dd/yyyy"),
                gameMode = auxGameMode,
                gameType = auxGameType,
                numberPlayers = auxNumberPlayers,
                localPlayerIndexId = auxLocalPlayerIndexId,

                turns = new List<TurnData>()
            };

            // Set player information based on the number of players in the match
            if (auxNumberPlayers == NumberPlayers.oneVsOne)
            {
                CurrentReplay.player_0 = playersInfo[0];
                CurrentReplay.player_1 = playersInfo[1];
            }
            else
            {
                CurrentReplay.player_0 = playersInfo[0];
                CurrentReplay.player_1 = playersInfo[1];
                CurrentReplay.player_2 = playersInfo[2];
                CurrentReplay.player_3 = playersInfo[3];
            }

            Debug.Log($"[ReplayManager_InitializeReplayData] Replay initialized with ID: {CurrentReplay.matchId}");
        }

        /// <summary>
        /// Configures and adds the first turn of the game to the replay, initializing turn and round numbers, player
        /// hands, scores, and boneyard piece IDs.
        /// </summary>
        /// <param name="auxRoundNumber">The round number to assign to the first turn.</param>
        /// <param name="auxPlayersHands">The list of player hand data for the first turn.</param>
        /// <param name="auxBoneyardPieceIds">The list of boneyard piece IDs for the first turn.</param>
        public void ConfigureFirstTurnOfGame(int auxRoundNumber, List<PlayerHandData> auxPlayersHands, List<int> auxBoneyardPieceIds)
        {
            // Validate CurrentReplay and its turns before attempting to configure the first turn of the game
            if (CurrentReplay is null or { turns: null })
            {
                Debug.LogError($"[ReplayManager_ConfigureFirstTurnOfGame] CurrentReplay is null or has no turns. Cannot configure first turn for round {auxRoundNumber}.");
                return;
            }

            var auxNewTurn = new TurnData
            {
                turnNumber = turnCounter,
                roundNumber = auxRoundNumber,
                playerIndexId = -1,
                playersHands = auxPlayersHands,
                roundPlayerScores = new List<int> { 0, 0, 0, 0 },
                cumulatePlayerScores = new List<int> { 0, 0, 0, 0 },
                boneyardPieceIds = auxBoneyardPieceIds,
                dealHands = true
            };

            turnCounter++;
            CurrentReplay.turns.Add(auxNewTurn);

            Debug.Log($"[ReplayManager_ConfigureFirstTurnOfGame] First turn configured: " +
                $"Round {auxRoundNumber}, " +
                $"Player Hands: {string.Join(", ", auxPlayersHands.Select(h => $"Player {h.playerIndexId}: [{string.Join(", ", h.handPieceIds)}]"))}, " +
                $"Boneyard: [{string.Join(", ", auxBoneyardPieceIds)}]");
        }

        /// <summary>
        /// Adds a new turn to the replay with the specified round number, player hands, boneyard piece IDs, turn slot
        /// helper, and board pieces.
        /// </summary>
        /// <param name="auxRoundNumber">The round number for the new turn.</param>
        /// <param name="auxPlayersHands">The list of player hand data for the new turn.</param>
        /// <param name="auxBoneyardPieceIds">The list of boneyard piece IDs for the new turn.</param>
        /// <param name="auxTurnSlotHelper">The turn slot helper for the new turn.</param>
        /// <param name="auxPieces">The list of domino pieces on the board for the new turn.</param>
        public void CreateNewTurn(int auxRoundNumber, List<PlayerHandData> auxPlayersHands, List<int> auxBoneyardPieceIds, TurnSlotHelper auxTurnSlotHelper, List<DominoPieceData> auxPieces)
        {
            // Validate CurrentReplay and its turns before attempting to create a new turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_CreateNewTurn] CurrentReplay is null or has no turns. Cannot create new turn for round {auxRoundNumber}.");
                return;
            }

            // Validate turnCounter value before accessing the previous turn's scores
            if (turnCounter is <= 0)
            {
                Debug.LogError($"[ReplayManager_CreateNewTurn] Invalid turnCounter value: {turnCounter}. Cannot create new turn for round '{auxRoundNumber}' if it's the first turn of invalid value.");
                return;
            }

            var auxNewTurn = new TurnData
            {
                turnNumber = turnCounter,
                roundNumber = auxRoundNumber, // cada 10 turnos cambia de ronda (ajústalo a tu lógica)
                playersHands = auxPlayersHands,
                boardPieces = auxPieces,
                roundPlayerScores = CurrentReplay.turns[turnCounter - 1].roundPlayerScores,
                cumulatePlayerScores = CurrentReplay.turns[turnCounter - 1].cumulatePlayerScores,
                boneyardPieceIds = auxBoneyardPieceIds,
                turnSlotHelper = auxTurnSlotHelper
            };

            CurrentReplay.turns.Add(auxNewTurn);
            Debug.Log($"[ReplayManager_CreateNewTurn] New turn created: " +
                $"Round {auxRoundNumber}, " +
                $"Player Hands: {string.Join(", ", auxPlayersHands.Select(h => $"Player {h.playerIndexId}: [{string.Join(", ", h.handPieceIds)}]"))}, " +
                $"Boneyard: [{string.Join(", ", auxBoneyardPieceIds)}], " +
                $"Board Pieces: [{string.Join(", ", auxPieces.Select(p => $"Tile {p.tileId} at {p.position} with rotation {p.rotation}"))}]");
        }

        /// <summary>
        /// Removes the last turn from the current replay and updates the turn counter.
        /// </summary>
        public void RemoveTurn()
        {
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogWarning("No turns to remove.");
                return;
            }

            // Remove the last turn from the current replay's list of turns and decrement the turn counter, ensuring that there are turns to remove before attempting to do so
            CurrentReplay.turns.RemoveAt(CurrentReplay.turns.Count - 1);
            turnCounter -= 1;

            Debug.Log($"[ReplayManager_RemoveTurn] Turn removed. Current turn count: {turnCounter}");
        }

        /// <summary>
        /// Updates the current replay data for the next round, including round number, player hands, boneyard piece
        /// IDs, and player scores.
        /// </summary>
        /// <param name="auxRoundNumber">The round number to set for the next round.</param>
        /// <param name="auxPlayersHands">The list of player hand data for the next round.</param>
        /// <param name="auxBoneyardPieceIds">The list of boneyard piece IDs for the next round.</param>
        public void SetDataNextRound(int auxRoundNumber, List<PlayerHandData> auxPlayersHands, List<int> auxBoneyardPieceIds)
        {
            // Validate CurrentReplay and its turns before attempting to set data for the next round
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetDataNextRound] CurrentReplay is null or has no turns. Cannot set data for next round {auxRoundNumber}.");
                return;
            }

            // Validate turnCounter value before accessing the current turn or previous turn's scores
            if (turnCounter is <= 0)
            {
                Debug.LogError($"[ReplayManager_SetDataNextRound] Invalid turnCounter value: {turnCounter}. Cannot set data for next round {auxRoundNumber} if it's the first turn of invalid value.");
                return;
            }

            // Ensure there is a turn at the current turnCounter index before attempting to set data for the next round
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetDataNextRound] No turn found at index {turnCounter} for the next round. Cannot set data for round {auxRoundNumber}.");
                return;
            }

            var previousTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter - 1);
            if (previousTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetDataNextRound] No previous turn found at index {turnCounter - 1}. Cannot set data for next round {auxRoundNumber}.");
                return;
            }

            replayTurn.roundNumber = auxRoundNumber;
            replayTurn.playersHands = auxPlayersHands;
            replayTurn.boneyardPieceIds = auxBoneyardPieceIds;
            replayTurn.roundPlayerScores = previousTurn.roundPlayerScores;
            replayTurn.cumulatePlayerScores = previousTurn.cumulatePlayerScores;
            replayTurn.dealHands = true;

            turnCounter++;

            Debug.Log($"[ReplayManager_SetDataNextRound] Data set for next round: " +
                $"Round {auxRoundNumber}, " +
                $"Player Hands: {string.Join(", ", auxPlayersHands.Select(h => $"Player {h.playerIndexId}: [{string.Join(", ", h.handPieceIds)}]"))}, " +
                $"Boneyard: [{string.Join(", ", auxBoneyardPieceIds)}], " +
                $"Round Scores: [{string.Join(", ", replayTurn.roundPlayerScores)}], " +
                $"Cumulative Scores: [{string.Join(", ", replayTurn.cumulatePlayerScores)}]");
        }

        /// <summary>
        /// Sets the play action for the current turn in the replay, assigning the played domino piece and its
        /// properties to the specified player.
        /// </summary>
        /// <param name="auxPlayerIndexId">Index of the player performing the play action.</param>
        /// <param name="auxTile">Identifier of the domino tile being played.</param>
        /// <param name="auxSideInfo">Side information indicating where the tile is played.</param>
        /// <param name="posInfo">Position of the played tile.</param>
        /// <param name="rotInfo">Rotation of the played tile.</param>
        /// <param name="sizeInfo">Size information of the played tile.</param>
        public void SetTurnAction_Play(int auxPlayerIndexId, int auxTile, string auxSideInfo, Vector3 posInfo, Quaternion rotInfo, Vector2 sizeInfo)
        {
            // Validate CurrentReplay and its turns before attempting to set the play action for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_Play] CurrentReplay is null or has no turns. Cannot set play action for player {auxPlayerIndexId}.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_Play] No turn found at index {turnCounter}. Cannot set play action for player {auxPlayerIndexId}.");
                return;
            }

            replayTurn.playerIndexId = auxPlayerIndexId;
            replayTurn.playedPiece = new DominoPieceData
            {
                tileId = auxTile,
                position = posInfo,
                rotation = rotInfo,
                sizeDelta = sizeInfo,
                sideInfo = auxSideInfo
            };

            // Only set the turn action to play if it hasn't been set yet (e.g., if the player took from the boneyard before playing)
            if (replayTurn.turnAction is TurnActionReplay.none)
                replayTurn.turnAction = TurnActionReplay.play;

            Debug.Log($"[ReplayManager_SetTurnAction_Play] Play action set: " +
                $"Player {auxPlayerIndexId} played tile {auxTile} on {auxSideInfo} side at position {posInfo} with rotation {rotInfo} and size {sizeInfo}.");
        }

        /// <summary>
        /// Sets the current turn action to 'pass' for the specified player in the replay, if not already set.
        /// </summary>
        /// <param name="auxPlayerIndexId">The index ID of the player passing their turn.</param>
        public void SetTurnAction_Pass(int auxPlayerIndexId)
        {
            // Validate CurrentReplay and its turns before attempting to set the pass action for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_Pass] CurrentReplay is null or has no turns. Cannot set pass action for player {auxPlayerIndexId}.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_Pass] No turn found at index {turnCounter}. Cannot set pass action for player {auxPlayerIndexId}.");
                return;
            }

            // Set the player index for the turn, even if the action is pass, to ensure the replay data accurately reflects which player took the action
            replayTurn.playerIndexId = auxPlayerIndexId;

            // Only set the turn action to pass if it hasn't been set yet (e.g., if the player took from the boneyard before passing)
            if (replayTurn.turnAction is TurnActionReplay.none)
                replayTurn.turnAction = TurnActionReplay.pass;

            Debug.Log($"[ReplayManager_SetTurnAction_Pass] Pass action set: Player {auxPlayerIndexId} passed their turn.");
        }

        /// <summary>
        /// Records a player's action of taking a tile from the boneyard in the current replay turn.
        /// </summary>
        /// <param name="auxPlayerIndexId">The index ID of the player taking the tile.</param>
        /// <param name="tileId">The ID of the tile taken from the boneyard.</param>
        public void SetTurnAction_TakeBoneyard(int auxPlayerIndexId, int tileId)
        {
            // Validate CurrentReplay and its turns before attempting to set the take boneyard action for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_TakeBoneyard] CurrentReplay is null or has no turns. Cannot set take boneyard action for player {auxPlayerIndexId}.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnAction_TakeBoneyard] No turn found at index {turnCounter}. Cannot set take boneyard action for player {auxPlayerIndexId}.");
                return;
            }

            // Set the player index for the turn, even if the action is taking from the boneyard, to ensure the replay data accurately reflects which player took the action
            replayTurn.playerIndexId = auxPlayerIndexId;
            replayTurn.tilesTakenFromBoneyard.Add(tileId);

            // Only set the turn action to takeBoneyard if it hasn't been set yet (e.g., if the player took from the boneyard before playing or passing)
            replayTurn.turnAction = TurnActionReplay.takeBoneyard;

            Debug.Log($"[ReplayManager_SetTurnAction_TakeBoneyard] Take boneyard action set: Player {auxPlayerIndexId} took tile {tileId} from the boneyard.");
        }
        
        /// <summary>
        /// Updates the current turn in the replay with player scores, end game status, and the round winner index.
        /// </summary>
        /// <param name="auxRoundPlayerScores">List of player scores for the current round.</param>
        /// <param name="auxCumulatePlayerScores">List of cumulative player scores up to the current turn.</param>
        /// <param name="isEndGame">Indicates whether the current turn is the end of the game.</param>
        /// <param name="auxRoundWinnerPlayerIndexId">Index of the player who won the current round.</param>
        public void SetTurnScores(List<int> auxRoundPlayerScores, List<int> auxCumulatePlayerScores, bool isEndGame, int auxRoundWinnerPlayerIndexId)
        {
            // Validate CurrentReplay and its turns before attempting to set scores for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnScores] CurrentReplay is null or has no turns. Cannot set turn scores.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnScores] No turn found at index {turnCounter}. Cannot set turn scores.");
                return;
            }

            // Set the round and cumulative player scores for the current turn, as well as end game status and round winner index
            replayTurn.roundPlayerScores = auxRoundPlayerScores;
            replayTurn.cumulatePlayerScores = auxCumulatePlayerScores;
            replayTurn.isEndGame = isEndGame;
            replayTurn.roundWinnerPlayerIndexId = auxRoundWinnerPlayerIndexId;

            Debug.Log($"[ReplayManager_SetTurnScores] Turn scores set: Round scores = {string.Join(", ", auxRoundPlayerScores)}, Cumulative scores = {string.Join(", ", auxCumulatePlayerScores)}, End game = {isEndGame}, Round winner = {auxRoundWinnerPlayerIndexId}");
        }

        /// <summary>
        /// Sets the tie status for the current turn in the replay.
        /// </summary>
        /// <param name="isTie">Indicates whether the current turn ended in a tie.</param>
        public void SetTurnIsTie(bool isTie)
        {
            // Validate CurrentReplay and its turns before attempting to set the tie status for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnIsTie] CurrentReplay is null or has no turns. Cannot set turn tie status.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnIsTie] No turn found at index {turnCounter}. Cannot set turn tie status.");
                return;
            }

            // Set the isTie property for the current turn to indicate whether the turn ended in a tie
            replayTurn.isTie = isTie;

            Debug.Log($"[ReplayManager_SetTurnIsTie] Turn tie status set: Is tie = {isTie}");
        }

        /// <summary>
        /// Sets the result of the current turn in the replay and advances the turn counter.
        /// </summary>
        /// <param name="turnResult">The result data to assign to the current turn.</param>
        public void SetTurnResult(TurnResultReplay turnResult)
        {
            // Validate CurrentReplay and its turns before attempting to set the turn result for the current turn
            if (CurrentReplay is null or { turns: null or { Count: 0 } })
            {
                Debug.LogError($"[ReplayManager_SetTurnResult] CurrentReplay is null or has no turns. Cannot set turn result.");
                return;
            }

            // Validate turnCounter value before accessing the current turn
            var replayTurn = CurrentReplay.turns.ElementAtOrDefault(turnCounter);
            if (replayTurn is null)
            {
                Debug.LogError($"[ReplayManager_SetTurnResult] No turn found at index {turnCounter}. Cannot set turn result.");
                return;
            }

            // Set the turn result for the current turn, indicating whether the game is over, blocked, or ongoing
            replayTurn.turnResult = turnResult;
            turnCounter++;

            Debug.Log($"[ReplayManager_SetTurnResult] Turn result set: Turn result = {turnResult}");
        }
        #endregion

        #region Manager PlayerPrefs Slots               
        /// <summary>
        /// Updates the name of a replay stored at the specified slot index without altering other replay data.
        /// </summary>
        /// <param name="slotIndex">The index of the replay slot to update.</param>
        /// <param name="newName">The new name to assign to the replay.</param>
        public void ChangeReplayNameByIndex(int slotIndex, string newName)
        {
            var dataKey = $"slot{slotIndex}_data";
            if (PlayerPrefs.HasKey(dataKey))
            {
                var json = PlayerPrefs.GetString(dataKey);
                if (string.IsNullOrEmpty(json))
                { 
                    Debug.LogError($"[ReplayManager_ChangeReplayNameByIndex] Slot {slotIndex} has empty data. Cannot change name.");
                    return;
                }

                try
                {
                    var replay = JsonUtility.FromJson<MatchReplay>(json);

                    // Modify the matchId (name) of the replay and serialize it back to JSON format, ensuring that only the name is changed while the rest of the replay data remains intact
                    replay.matchId = newName;
                    var updatedJson = JsonUtility.ToJson(replay);

                    // Override the existing replay data with the updated JSON string containing the new name, ensuring that the rest of the replay data remains unchanged
                    PlayerPrefs.SetString(dataKey, updatedJson);
                    PlayerPrefs.Save();

                    Debug.Log($"[ReplayManager_ChangeReplayNameByIndex] Slot {slotIndex} name changed to '{newName}'.");
                }
                catch (JsonException ex)
                {
                    Debug.LogError(
                        $"[ReplayManager_ChangeReplayNameByIndex] Failed to deserialize replay data for slot {slotIndex}. " +
                        $"Error: {ex.Message}"
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[ReplayManager_ChangeReplayNameByIndex] Unexpected error while changing name for slot {slotIndex}. " +
                        $"Error: {ex}"
                    );
                }
            } 
        }

        /// <summary>
        /// Deletes the replay data and associated date for the specified slot index from PlayerPrefs.
        /// </summary>
        /// <param name="slotIndex">The index of the replay slot to delete.</param>
        public void DeleteReplayByIndex(int slotIndex)
        {
            string dataKey = $"slot{slotIndex}_data";
            string dateKey = $"slot{slotIndex}_date";

            if (PlayerPrefs.HasKey(dataKey))
            {
                PlayerPrefs.DeleteKey(dataKey);
                PlayerPrefs.DeleteKey(dateKey);
                PlayerPrefs.Save();

                Debug.Log($"[ReplayManager_DeleteReplayByIndex] Slot {slotIndex} deleted");
            }
        }

        /// <summary>
        /// Deletes all PlayerPrefs data and saves the changes.
        /// </summary>
        private void ResetAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[ReplayManager_ResetAllPlayerPrefs] All PlayerPrefs have been reset.");
        }
        #endregion
    }
}
 