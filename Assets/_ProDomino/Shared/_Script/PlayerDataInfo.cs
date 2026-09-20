using Newtonsoft.Json;
using System;
using Unity.Netcode;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Shared
{
    public struct PlayerDataInfo : INetworkSerializable
    {
        public int clientId;
        public string userId;
        public string username;
        public string profileIconID;
        public string tileSkinID;
        public string boardSkinID;
        public string boardFundSkinID;
        public string badgesIDs;
        public string leaderboardTier;
        public double leaderboardScore;
        public bool isBot;

        /// <summary>
        /// This property indicates whether the player data has been configured.<br></br>
        /// Trick used to inform the client that the player data has been configured and its data is not default<br></br>
        /// TODO: to replace this, the field or properties should be capable of being null
        /// </summary>
        private bool isConfigured;
        public readonly bool IsConfigured => isConfigured;

        public PlayerDataInfo
            (int clientId,
            string userId,
            string username,
            string profileIconID,
            string tileSkinID,
            string boardSkinID,
            string boardFundSkinID,
            string badgesIDs,
            string leaderboardTier,
            double leaderboardScore,
            bool isBot)
        {
            this.clientId = clientId;
            this.userId = userId ?? string.Empty;
            this.username = username ?? string.Empty;
            this.profileIconID = profileIconID ?? string.Empty;
            this.tileSkinID = tileSkinID ?? string.Empty;
            this.boardSkinID = boardSkinID ?? string.Empty;
            this.boardFundSkinID = boardFundSkinID ?? string.Empty;
            this.badgesIDs = badgesIDs ?? string.Empty;
            this.leaderboardTier = leaderboardTier ?? string.Empty;
            this.leaderboardScore = leaderboardScore;
            this.isBot = isBot;

            isConfigured = true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // ---------------------------------------------------------------------
            // Validate string-based fields to avoid null references and trimming size
            // ---------------------------------------------------------------------

            // userId should never be null Å® assign empty string
            if (string.IsNullOrEmpty(userId))
                userId = string.Empty;

            // username should not be null Å® assign placeholder if needed
            if (string.IsNullOrEmpty(username))
                username = "Unknown";

            // profileIconID, tileSkinID, boardSkinID, boardFundSkinID could also be null
            if (string.IsNullOrEmpty(profileIconID))
                profileIconID = $"{CosmeticType.Icons}_{Consts.CollectionKeys.Default}_Male";

            if (string.IsNullOrEmpty(tileSkinID))
                tileSkinID = $"{CosmeticType.Tiles}_{CosmeticType.Tiles}_{Consts.CollectionKeys.Default}";

            if (string.IsNullOrEmpty(boardSkinID))
                boardSkinID = $"{CosmeticType.Boards}_{Consts.CollectionKeys.Default}";

            if (string.IsNullOrEmpty(boardFundSkinID))
                boardFundSkinID = $"{CosmeticType.Fund}_{Consts.CollectionKeys.Default}";

            // badgesIDs should not be null; if it is, initialize as empty array
            if (badgesIDs is null)
                badgesIDs = string.Empty;

            // leaderboardTier should have a valid string; assign "None" if empty
            if (string.IsNullOrEmpty(leaderboardTier))
                leaderboardTier = LeaderboardTier.None.ToString();

            // ---------------------------------------------------------------------
            // Validate numeric values (avoid negatives when not allowed)
            // ---------------------------------------------------------------------

            // clientId should not be negative (use 0 as default)
            if (clientId < 0)
                clientId = 0;

            // leaderboardScore cannot be negative (depending on design, clamp to 0)
            if (leaderboardScore < 0)
                leaderboardScore = 0;

            // ---------------------------------------------------------------------
            // Serialize values (order matters, must match across sender/receiver)
            // ---------------------------------------------------------------------

            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref userId);
            serializer.SerializeValue(ref username);
            serializer.SerializeValue(ref profileIconID);
            serializer.SerializeValue(ref tileSkinID);
            serializer.SerializeValue(ref boardSkinID);
            serializer.SerializeValue(ref boardFundSkinID);
            serializer.SerializeValue(ref badgesIDs);
            serializer.SerializeValue(ref leaderboardTier);
            serializer.SerializeValue(ref leaderboardScore);
            serializer.SerializeValue(ref isBot);
            serializer.SerializeValue(ref isConfigured);
        }

    }
}