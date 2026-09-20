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

        private GameManager gameManager;
        private AsyncActionHandler<RtdbUserData> searchUserAction;

        public RtdbUserData SearchedUserData { get; private set; }

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
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
            SearchedUserData = searchedUserData;

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
                if (gameManager)
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
        /// Handles the invite player button press event.
        /// </summary>
        private async void OnPressInvitePlayerButton()
        {
            if (SearchedUserData is null || searchUserAction is null)
            { 
                Debug.LogWarning("SearchedUserData is null or searchUserAction is not assigned. Cannot invite player.");
                return;
            }

            invitePlayerButton.interactable = false;
            try
            {
                await searchUserAction.Invoke(SearchedUserData);
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception while inviting player: " + ex.Message);
            }
            finally
            {
                invitePlayerButton.interactable = true;
            }
        }
    }
}
