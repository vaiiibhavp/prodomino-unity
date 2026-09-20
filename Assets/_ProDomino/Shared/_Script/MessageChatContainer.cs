using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    public class MessageChatContainer : MonoBehaviour
    {
        [SerializeField] private Image profileImage;
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private TMP_Text timeStampText;
        [SerializeField] private TMP_Text MessageText;

        private Sprite profileIcon;
        private string fromPlayerName;
        private string timeStamp;
        private string message;

        private Action<int> onSendBanToPlayer = null;

        public int FromClientID { get; private set; } = -1;
        public int ToClientID { get; private set; } = -1;
        public string FromPlayerID { get; private set; }

        public void SetMessageData
            (int fromClientID, 
            int toClientID,
            string fromPlayerID,
            string fromPlayerName,
            string timeStamp, 
            string message, 
            Sprite profileIcon,
            Action<int> SendBanToPlayerAction)
        {
            FromClientID = fromClientID;
            ToClientID = toClientID;
            FromPlayerID = fromPlayerID;

            this.profileIcon = profileIcon;
            this.fromPlayerName = fromPlayerName;
            this.timeStamp = timeStamp;
            this.message = message;

            if (fromClientID != toClientID)
                onSendBanToPlayer = SendBanToPlayerAction;

            if (profileImage)
                profileImage.sprite = this.profileIcon;
            else
                Debug.LogWarning("Profile image is not assigned in MessageChatContainer.");

            if (userNameText)
                userNameText.text = this.fromPlayerName;
            else
                Debug.LogWarning("User name text is not assigned in MessageChatContainer.");

            if (timeStampText)
                timeStampText.text = this.timeStamp;
            else
                Debug.LogWarning("Time stamp text is not assigned in MessageChatContainer.");

            if (MessageText)
                MessageText.text = this.message;
            else
                Debug.LogWarning("Message text is not assigned in MessageChatContainer.");
        }

        public void SendBanToPlayerInChat()
        {
            onSendBanToPlayer?.Invoke(FromClientID);
        }
    }
}
