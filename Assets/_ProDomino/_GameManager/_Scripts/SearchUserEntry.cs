using Cysharp.Threading.Tasks;
using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static ProDomino.GameSystem.GameManager;

namespace ProDomino.GameSystem
{
    public class SearchUserEntry : MonoBehaviour
    {
        [SerializeField] private Image playerImage;
        [SerializeField] private TMP_Text usernameLabel;
        [SerializeField] private TMP_Text userIdLabel;
        [SerializeField] private Button invitePlayerButton;

        [Header("Request Sent State (optional)")]
        [SerializeField] private TMP_Text invitePlayerButtonLabel;
        [SerializeField] private Image invitePlayerButtonImage;
        [SerializeField] private Sprite requestSentSprite;
        [SerializeField] private Color requestSentLabelColor = Color.black;
        [SerializeField] private string requestSentText = "Request Sent";

        private string defaultButtonText;
        private Sprite defaultButtonSprite;
        private Color defaultLabelColor;

        private GameManager gameManager;
        private AsyncActionHandler<RtdbUserData> searchUserAction;

        public RtdbUserData SearchedUserData { get; private set; }

        private bool isInitialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        // Entries are created under a hidden section and configured before their Awake runs,
        // so every entry point initializes the references and default visuals first.
        private void EnsureInitialized()
        {
            if (isInitialized)
                return;

            isInitialized = true;
            gameManager = ServiceLocator.Instance.GetService<GameManager>();

            if (invitePlayerButtonLabel)
            {
                defaultButtonText = invitePlayerButtonLabel.text;
                defaultLabelColor = invitePlayerButtonLabel.color;
            }

            if (invitePlayerButtonImage)
                defaultButtonSprite = invitePlayerButtonImage.sprite;

            if (playerImage)
                defaultPlayerSprite = playerImage.sprite;
        }

        private Sprite defaultPlayerSprite;

        /// <summary>
        /// Switches the invite button between its default look and the "request sent" look.
        /// </summary>
        private void SetRequestSent(bool isSent)
        {
            if (invitePlayerButton)
                invitePlayerButton.interactable = !isSent;

            if (invitePlayerButtonLabel)
            {
                invitePlayerButtonLabel.text = isSent ? requestSentText : defaultButtonText;
                invitePlayerButtonLabel.color = isSent ? requestSentLabelColor : defaultLabelColor;
            }

            if (invitePlayerButtonImage && requestSentSprite)
                invitePlayerButtonImage.sprite = isSent ? requestSentSprite : defaultButtonSprite;
        }

        /// <summary>
        /// Initializes the search user entry with the provided action for inviting users.
        /// </summary>
        public void Initialize(AsyncActionHandler<RtdbUserData> searchUserAction)
        {
            this.searchUserAction = searchUserAction;

            // Set up button listener
            if (invitePlayerButton)
                invitePlayerButton.onClick.AddListener(OnPressInvitePlayerButton);
            else
                Debug.LogWarning("Invite Player Button is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Configures the search user entry with the provided data.
        /// </summary>
        public async UniTask Configure(RtdbUserData searchedUserData)
        {
            EnsureInitialized();
            SearchedUserData = searchedUserData;
            SetRequestSent(false);

            // Fill in username
            if (usernameLabel && !string.IsNullOrEmpty(SearchedUserData.displayName))
                usernameLabel.text = SearchedUserData.displayName;
            else
                Debug.LogWarning("Profile Icon ID is null or empty. Cannot configure player image.");

            // Fill in user ID
            if (userIdLabel && !string.IsNullOrEmpty(SearchedUserData.userId))
                userIdLabel.text = SearchedUserData.userId;
            else
                Debug.LogWarning("User ID is null or empty. Cannot configure user ID label.");

            // Fill in player image
            if (playerImage)
            {
                // A reused entry must not keep the previous player's icon
                if (defaultPlayerSprite)
                    playerImage.sprite = defaultPlayerSprite;

                // Recently played players carry no icon, keep the default avatar for them
                if (gameManager && !string.IsNullOrEmpty(SearchedUserData.profileIconID))
                {
                    var sprite = await gameManager.GetSpriteAsync(SearchedUserData.profileIconID, Consts.CollectionKeys.Icons);
                    if (sprite != null)
                        playerImage.sprite = sprite;
                    else
                        Debug.LogWarning($"Sprite not found for Profile Icon ID: {SearchedUserData.profileIconID}. Check if the icon exists in the collection.");
                }
            } 
            else
                Debug.LogError("Player Image is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Loads the player icon resolved after the entry was configured (recently played players carry no icon).
        /// </summary>
        public async UniTask SetPlayerIcon(string profileIconID)
        {
            EnsureInitialized();

            if (!playerImage || !gameManager || string.IsNullOrEmpty(profileIconID) || SearchedUserData is null)
                return;

            var userId = SearchedUserData.userId;
            SearchedUserData.profileIconID = profileIconID;

            var sprite = await gameManager.GetSpriteAsync(profileIconID, Consts.CollectionKeys.Icons);

            // Skip if the entry was reused for another player meanwhile
            if (sprite != null && SearchedUserData?.userId == userId)
                playerImage.sprite = sprite;
        }

        /// <summary>
        /// Handles the invite player button press event.
        /// </summary>
        private async void OnPressInvitePlayerButton()
        {
            if (SearchedUserData is null || searchUserAction is null)
            { 
                Debug.LogWarning("SearchedUserData is null or searchUserAction is not assigned. Cannot invite player.");
                return;
            }

            var isSent = false;
            invitePlayerButton.interactable = false;
            try
            {
                await searchUserAction.Invoke(SearchedUserData);
                isSent = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception while inviting player: " + ex.Message);
            }
            finally
            {
                SetRequestSent(isSent);
            }
        }
    }
}
