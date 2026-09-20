using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.LeaderboardSystem;

public class UpdateLeaderboardScoreByMatchResultModule(ILogger<UpdateLeaderboardScoreByMatchResultModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<UpdateLeaderboardScoreByMatchResultModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(UpdateLeaderboardScoreByMatchResult))]
    public async Task<string> UpdateLeaderboardScoreByMatchResult(IExecutionContext executionContext, string parametersEncryptedJson)
    {
        // Validate the execution context
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Get the player ID from the execution context and validate it
        var playerId = executionContext.PlayerId;
        if (string.IsNullOrEmpty(playerId))
            throw new Exception("Player ID is invalid or null.");

        // Decrypt and parse parameters
        var data = BackendHelper.ValidateEncriptedParameters(
            parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate required parameters
        if (!data.TryGetValue("leaderboardId", out var leaderboardIdToken) || leaderboardIdToken is not string leaderboardId)
            throw new ArgumentException("Missing or invalid 'leaderboardId'");

        var resultPosition = 0;
        if (!data.TryGetValue("resultPosition", out var resultPositionObj) || !int.TryParse(resultPositionObj.ToString(), out resultPosition) || resultPosition is < 1 or > 4)
            throw new ArgumentException("Missing or invalid 'resultPosition'");

        float matchEMC = 0f;
        if (!data.TryGetValue("matchEMC", out var matchEMCObj) || !float.TryParse(matchEMCObj.ToString(), out matchEMC))
            _logger.LogWarning("matchEMC missing or invalid. Defaulting to 0.");

        // Load Game Data
        var gameDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Config.ToString());

        // Validate and parse Game Data configuration
        var configData = gameDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())?.value?.ToObject<ConfigData>();
        if (configData is null or { leaderboardConfig: null or { maxPlayers: 0 } })
            throw new UGSException("Game Data configuration invalid.");

        // Define keys for player data retrieval
        var matchIDKey = CloudSaveProperties.Match.ToString();
        var analyticsIDKey = CloudSaveProperties.Analytics.ToString();
        var clubIDKey = CloudSaveProperties.Club.ToString();
        var nationalityKey = CloudSaveProperties.Nationality.ToString();
        var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
        var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
        var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

        // Load current player data
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            matchIDKey, analyticsIDKey, clubIDKey, nationalityKey,
            firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey);

        // Parse player data with safe defaults
        var playerMatchData = playerDataResponse?.FirstOrDefault(x => x?.key == matchIDKey)?.value?.ToObject<PlayerMatchData>() ?? new();
        var playerAnalyticsData = playerDataResponse?.FirstOrDefault(x => x?.key == analyticsIDKey)?.value?.ToObject<AnalyticsData>() ?? new AnalyticsData();
        var playerNationalityData = playerDataResponse?.FirstOrDefault(x => x?.key == nationalityKey)?.value?.ToObject<NationalityData>() ?? new NationalityData();

        // Derived key/iv for encrypting response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Get current player score from leaderboard
        var leaderboardData = await UGSApiHelper.GetPlayerScore(executionContext, leaderboardId);
        var tier = leaderboardData is not null && Enum.TryParse<LeaderboardTier>(leaderboardData.tier, out var _tier) ? _tier : LeaderboardTier.ClassC;

        // Update ELO
        var kFactor = configData.leaderboardConfig.kFactorByTier
            ?.FirstOrDefault(k => k.leaderboardTier == tier)
            ?.kFactor
            ?? 10;

        // If the result prosition is 1st it measn they win the match (also consider 2v2)
        var isVictory = resultPosition is 1;

        UpdateELO(ref playerMatchData, matchEMC, isVictory, configData.leaderboardConfig.maxEMCImpact, kFactor);

        // Update club score if is victory and player is in a club
        await UpdateClubScore(executionContext, _gameApiClient,
            isVictory, playerMatchData.elo, configData, playerDataResponse, leaderboardId, clubIDKey, firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey);

        // Update leaderboard score
        var (leaderboardResult, newLeaderboardData) = await ModifyPlayerScore(executionContext, playerMatchData, leaderboardId, isVictory, configData, leaderboardData);
        
        // Refresh the leaderboardData with the new one (score updated)
        leaderboardData = newLeaderboardData ?? leaderboardData ?? new(); 

        // Update analytics
        UpdateAnalytics(ref playerMatchData, ref playerAnalyticsData, leaderboardId, leaderboardData, resultPosition);

        // Update nationality leaderboard score based on the match result
        await UpdateNationalityLeaderboardScore(executionContext, _gameApiClient, 
            playerNationalityData.nationalityType, leaderboardId, leaderboardData?.score ?? 0, 
            _logger);

        // Save updated player match data
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
            new() 
            { 
                [matchIDKey] = playerMatchData,
                [analyticsIDKey] = playerAnalyticsData
            });

        // Build response
        var responseObj = new UpdateLeaderboardScoreByMatchResultResponse(leaderboardId, leaderboardResult);
        var responseJson = JsonConvert.SerializeObject(responseObj);
        var encryptedResponse = SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv);
        return JsonConvert.SerializeObject(encryptedResponse);
    }

    /// <summary>
    /// Updates the club score if the player is in a club and the match was a victory.<br></br>
    /// </summary>
    private async Task UpdateClubScore(IExecutionContext executionContext, IGameApiClient gameApiClient,
        bool isVictory, int newEloRating,
        ConfigData configData, ResponseData?[]? playerDataResponse,
        string leaderboardId, string clubIDKey,
        string firebaseIDKey, string firebaseIDTokenKey, string firebaseRefreshTokenKey)
    {
        // Try to update club score if is victory
        if (!isVictory)
        {
            _logger.LogInformation("Match lost. Club score will not be updated.");
            return;
        }

        // Check if the player is in a club according to the Cloud loaded data
        var playerClubData = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<CloudPlayerClubData>();
        if (playerClubData is not null and { clubName: not null or "" })
        {
            // Validate entry data
            var firebaseIDObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var firebaseIDTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var firebaseRefreshTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            // Check if the player has valid Firebase tokens
            if (firebaseIDObj is string firebaseId && !string.IsNullOrEmpty(firebaseId)
                && firebaseIDTokenObj is string firebaseIdToken && !string.IsNullOrEmpty(firebaseIdToken)
                && firebaseRefreshTokenObj is string firebaseRefreshToken && !string.IsNullOrEmpty(firebaseRefreshToken))
            {
                // Load club data from Firestore
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (firebaseIdToken,
                    firebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: playerClubData.clubName);

                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? JsonConvert.DeserializeObject<FirestoreClubData>(clubDataJson)
                    : null;

                // Try to update club score if the club data is valid and the player is a member of the club
                if (clubData is not null
                    && clubData.members.Any(x => x.firebaseMemberId == firebaseId) // Check if the club has the player as a member
                    && TryToUpdateClubScore(ref clubData, newEloRating, playerClubData, configData.clubsConfig, firebaseId, _logger))
                {
                    // Get club rank and update club data with it
                    var clubRank = await FirebaseApiHelper.GetClubRankAsync(
                        firebaseIdToken,
                        firebaseRefreshToken,
                        collection: FirebaseApiHelper.clubsCollectionIdKey,
                        targetClubId: playerClubData.clubName,
                        _logger);

                    if (clubRank.HasValue)
                    {
                        _logger.LogInformation($"Club '{playerClubData.clubName}' rank: {clubRank.Value}");
                        clubData.clubRank = clubRank.Value;
                    } else
                        _logger.LogWarning($"Club '{playerClubData.clubName}' not found in ranking.");

                    var updatedFields = new
                    {
                        score = new { integerValue = clubData.score },
                        clubRank = new { integerValue = clubData.clubRank },
                        members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                        updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                    };

                    // Update club data in Firestore
                    var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                        executionContext,
                        gameApiClient,
                        collection: FirebaseApiHelper.clubsCollectionIdKey,
                        documentId: playerClubData.clubName,
                        documentFields: updatedFields,
                        updateFieldPaths: ["score", "clubRank", "members", "updatedAt"]
                    );

                    // Log the result of the update operation
                    if (!string.IsNullOrEmpty(updateResponse))
                        _logger.LogInformation($"Club {playerClubData.clubName} score updated successfully.");
                    else
                        _logger.LogWarning($"Failed to update club {playerClubData.clubName} score.");
                }
            }
        }
    }

    /// <summary>
    /// Updates the ELO rating based on the match result.<br></br>
    /// </summary>
    /// <param name="playerMatchData"></param>
    /// <param name="matchEMC"></param>
    /// <param name="isVictory"></param>
    /// <param name="maxImpact"></param>
    /// <param name="KFactor"></param>
    private void UpdateELO(ref PlayerMatchData playerMatchData, float matchEMC, bool isVictory, int maxImpact, int KFactor)
    {
        var currentElo = playerMatchData.elo;

        // Scale EMC by K
        var scaledEMC = matchEMC * (KFactor / 10f);

        // Apply Max Impact
        if (Math.Abs(scaledEMC) > maxImpact)
            scaledEMC = Math.Sign(scaledEMC) * maxImpact;

        var baf = isVictory ? 10 : -5; // Best Answer Factor based on win/loss

        var newElo = Math.Max(0, currentElo + baf + (int)Math.Round(scaledEMC));

        _logger.LogInformation($"Updating ELO: OldELO={currentElo}, BAF={baf}, EMC={scaledEMC:F2}, NewELO={newElo}");
        playerMatchData.elo = newElo;
    }

    /// <summary>
    /// Updates the analytics data based on the match result.<br></br>
    /// </summary>
    /// <param name="matchData"></param>
    /// <param name="analyticsData"></param>
    /// <param name="isVictory"></param>
    private void UpdateAnalytics(ref PlayerMatchData matchData, ref AnalyticsData analyticsData, string leaderboardId, LeaderboardData leaderboardData, int resultPosition)
    {
        ref int activeStreak = ref matchData.winStreak;
        ref int resetStreak = ref matchData.loseStreak;
        var isVictory = resultPosition is 1;

        if (!isVictory)
        {
            activeStreak = ref matchData.loseStreak;
            resetStreak = ref matchData.winStreak;
        }

        // Update win/lose streaks
        activeStreak++;
        resetStreak = 0;

        // Check if the player won the match
        if (isVictory)
        {
            // Increase the victory count
            analyticsData.competitiveVictoriesCount += 1;
            
            // Update the longest streaks in analytics
            analyticsData.competitiveVictoryLongestStreak = Math.Max(analyticsData.competitiveVictoryLongestStreak, matchData.winStreak);
        }
        else
        { 
            // Increase the defeat count
            analyticsData.competitiveDefeatsCount += 1;

            // Also, register the exact position of the player in the match
            switch (resultPosition)
            {
                case 2: analyticsData.competitive2ndPositionCount += 1; break;
                case 3: analyticsData.competitive3rdPositionCount += 1; break;
                case 4: analyticsData.competitive4thPositionCount += 1; break;
            }

            // Update the longest streaks in analytics
            analyticsData.competitiveDefeatLongestStreak = Math.Max(analyticsData.competitiveDefeatLongestStreak, matchData.loseStreak);
        }

        // Try to update the mas record of the player
        if (leaderboardData is not null && !string.IsNullOrEmpty(leaderboardId))
        {
            var leaderboardRecord = analyticsData.leaderboardRecords.FirstOrDefault(x => x.leaderboardId == leaderboardId);
            if (leaderboardRecord == null)
                leaderboardRecord = new();

            if (leaderboardData.score > leaderboardRecord.higherScore)
                leaderboardRecord.higherScore = leaderboardData.score;

            if (Enum.TryParse<LeaderboardTier>(leaderboardData.tier, out var leaderboardTier) && leaderboardTier > leaderboardRecord.higherTier)
                leaderboardRecord.higherTier = leaderboardTier;
        }
    }

    /// <summary>
    /// Modifies the player's score in the leaderboard based on the match result.<br></br>
    /// </summary>
    /// <param name="contextData"></param>
    /// <param name="playerMatchData"></param>
    /// <param name="leaderboardId"></param>
    /// <param name="isVictory"></param>
    /// <param name="configData"></param>
    /// <param name="leaderboardData"></param>
    /// <returns></returns>
    private async Task<(UpdateLeaderboardScoreByMatchResultResponseEntry updateLeaderboardScoreByMatchResultResponseEntry, LeaderboardData? leaderboardData)> ModifyPlayerScore(IExecutionContext contextData, PlayerMatchData playerMatchData, string leaderboardId, bool isVictory, ConfigData configData, LeaderboardData? leaderboardData)
    {
        ref int activeStreak = ref playerMatchData.winStreak;
        if (!isVictory)
            activeStreak = ref playerMatchData.loseStreak;

        // Get the streaks configuration based on victory or defeat
        var streaks = isVictory ? configData.leaderboardConfig.victoryStreaks : configData.leaderboardConfig.defeatStreaks;
        var referenceStreak = activeStreak;

        // Find the score delta based on the current streak
        int scoreDelta = streaks?
            .Where(s => s.startStreak <= referenceStreak && (s.targetStreak == null || referenceStreak < s.targetStreak))
            .OrderByDescending(s => s.startStreak)
            .FirstOrDefault()?.streakScore ?? 0;

        try
        {
            // If the leaderboard data is not null, add the registered score to the score delta
            if (leaderboardData is not null and { score: double registeredScore })
                scoreDelta += (int)registeredScore;

            // Ensure score does not go below 0
            scoreDelta = Math.Max(0, scoreDelta);
            
            // Add the score to the player's leaderboard score
            leaderboardData = await UGSApiHelper.AddPlayerScore(contextData, scoreDelta, leaderboardId);

            _logger.LogInformation($"Player {contextData.PlayerId} score updated. ScoreDelta={scoreDelta}, Victory={isVictory}");
            return (new UpdateLeaderboardScoreByMatchResultResponseEntry(isVictory, scoreDelta, playerMatchData.winStreak, playerMatchData.loseStreak), leaderboardData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update score for player {PlayerId}", contextData.PlayerId);
            throw;
        }
    }

    /// <summary>
    /// Try to update club score according to its members contributions and the win factor defined in the clubs config.<br></br>
    /// </summary>
    /// <returns>Returns true if the club score was updated successfully, otherwise false.</returns>
    private bool TryToUpdateClubScore
        (ref FirestoreClubData firestoreClubData,
        int newEloRating,
        CloudPlayerClubData cloudPlayerClubData,
        ClubsConfig clubsConfig,
        string firebasePlayerID,
        ILogger _logger)
    {
        if (firestoreClubData is null or { members: null or { Count: 0 } } || cloudPlayerClubData is null || clubsConfig is null)
        {
            _logger.LogWarning("Firestore or Cloud Club data, or its config data, is null or empty");
            return false;
        }

        // Check if the club has the player as member
        var currentMember = firestoreClubData.members.FirstOrDefault(x => x.firebaseMemberId == firebasePlayerID);
        if (currentMember == null)
        {
            _logger.LogWarning("The club {ClubName} has no member with id {PlayerId}", cloudPlayerClubData.clubName, firebasePlayerID);
            return false;
        }

        // Increment victories for the current member
        currentMember.victories++;

        // Update club score according to its members contributions and the win factor defined in the clubs config
        var totalVictories = firestoreClubData.members.Sum(x => x.victories);
        var clubScore = totalVictories / clubsConfig.winFactor;
        firestoreClubData.score = clubScore;

        return true;
    }

    /// <summary>
    /// Updates the leaderboard score for a player based on their nationality.
    /// </summary>
    /// <param name="executionContext">The execution context containing player information.</param>
    /// <param name="gameApiClient">The game API client used to interact with the leaderboard service.</param>
    /// <param name="playerNationality">The nationality of the player.</param>
    /// <param name="leaderboardId">The identifier of the leaderboard to update.</param>
    /// <param name="score">The score to be recorded for the player.</param>
    /// <param name="_logger">Optional logger for logging information and warnings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task UpdateNationalityLeaderboardScore(IExecutionContext executionContext, IGameApiClient gameApiClient,
        NationalityType playerNationality, string leaderboardId, double score, 
        ILogger? _logger = null)
    {
        try
        {
            _logger?.LogInformation("Trying to update nationality leaderboard score for player {PlayerId}", 
                executionContext.PlayerId);

            // Get the player's username to be used in the leaderboard entry
            var playerUsername = await UGSApiHelper.GetUsername(executionContext, _logger: _logger).ConfigureAwait(false);

            // Call firebase real-time database to update the player's score in the nationality leaderboard
            await FirebaseApiHelper.UpsertLeaderboardScoreAsync
                (executionContext, gameApiClient,
                playerNationality, leaderboardId, executionContext!.PlayerId!, playerUsername, score,
                _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Failed to update nationality leaderboard score for player {executionContext.PlayerId}");
            throw;
        }
    }
}
