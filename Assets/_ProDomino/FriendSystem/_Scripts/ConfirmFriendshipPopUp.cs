using System;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.FriendSystem.FriendManager;

namespace ProDomino.FriendSystem
{
    /// <summary>
    /// Displays a pop-up UI for confirming the sending of a friend request to another player.
    /// </summary>
    public class ConfirmFriendshipPopUp : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button confirmSendRequestButton;
        [SerializeField] private Button cancelSendRequestButton;
        
        private FriendManager friendManager;
        private PromptFadeController promptFadeController;
        
        internal string PlayerNameToRequestAddAsFriend { get; private set; }
        internal string PlayerIDToRequestAddAsFriend { get; private set; }
        
        private void Awake()
        {
            friendManager = ServiceLocator.Instance.GetService<FriendManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            if (!promptFadeController || !friendManager)
                Debug.LogWarning("Prompt Fade Controller or friend manager services references are null");

            if (confirmSendRequestButton)
                confirmSendRequestButton.onClick.AddListener(ConfirmSendRequest);
            else
                Debug.LogWarning("The confirm send request button is null");

            if (cancelSendRequestButton)
                cancelSendRequestButton.onClick.AddListener(CancelSendRequest);
            else
                Debug.LogWarning("The confirm send request button is null");
        }

        /// <summary>
        /// Opens the confirm friendship pop-up for the specified player if the pop-up is not already visible and valid
        /// player information is provided.
        /// </summary>
        /// <param name="playerName">The name of the player to request as a friend.</param>
        /// <param name="playerId">The ID of the player to request as a friend.</param>
        internal void OpenPopUp(string playerName, string playerId)
        {
            if (canvasGroup.alpha is 1)
                return;

            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("Couldn't open confirm friendship popUp because the player name or id is null or empty");
                return;
            }

            PlayerNameToRequestAddAsFriend = playerName;
            PlayerIDToRequestAddAsFriend = playerId;

            if (headerLabel)
                headerLabel.text = headerLabel.text.Replace("{{playerName}}", PlayerNameToRequestAddAsFriend);

            SetVisibility(true);
        }

        /// <summary>
        /// Sets the visibility of the controller by activating or deactivating the CanvasGroup and refreshing its
        /// layout.
        /// </summary>
        /// <param name="isVisible">True to make the controller visible; false to hide it.</param>
        private void SetVisibility(bool isVisible)
        {
            if (!canvasGroup)
            {
                Debug.LogWarning("CanvasGroup is not assigned in PostMatchResultController.");
                return;
            }

            // Set the visibility of the controller
            canvasGroup.SetActive(isVisible);
            canvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
        }

        /// <summary>
        /// Confirms the send request to add a player as a friend
        /// </summary>
        private async void ConfirmSendRequest()
        {
            if (string.IsNullOrEmpty(PlayerIDToRequestAddAsFriend))
            {
                Debug.LogWarning("Couldn't send friend request because the player id is null or empty");
                return;
            }

            // Set the canvas group to inactive to prevent multiple clicks
            canvasGroup?.SetActive(false, isSettingAlpha: false);
            try
            {
                // Attempt to send the friend request
                await friendManager.MakeFriendAction(FriendAction.SendRequest, PlayerIDToRequestAddAsFriend);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while sending friend request: {e.Message}");
            }
            finally
            {
                // Once the request is sent, we can set the canvas group back to active
                canvasGroup?.SetActive(true, isSettingAlpha: false);
            }

            // Turn off the pop-up and show a prompt message
            SetVisibility(false);
            promptFadeController?.Fade
                ($"Invitation sent to {PlayerNameToRequestAddAsFriend}!",
                secondsShown: 2f, 
                fadeInDuration: 0.5f, 
                fadeOutDuration: 1f);
        }

        /// <summary>
        /// Clears friend request data and hides the send request UI.
        /// </summary>
        private void CancelSendRequest()
        {
            PlayerNameToRequestAddAsFriend = null;
            PlayerIDToRequestAddAsFriend = null;

            SetVisibility(false);
        }
    }
}
