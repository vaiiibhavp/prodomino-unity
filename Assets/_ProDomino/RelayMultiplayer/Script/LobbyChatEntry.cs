using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.RelayMultiplayer
{
    public class LobbyChatEntry : MonoBehaviour
    {
        [SerializeField] private Image profileImage;
        [SerializeField] private TMP_Text uuidLabel;
        [SerializeField] private TMP_Text displayNameLabel;
        [SerializeField] private Image[] badgesImages;
        [SerializeField] private Image tierImage;
        [SerializeField] private TMP_Text scoreLabel;
        public string PlayerID { get; private set; }

        public void Configure
            (Sprite profileSprite,
            string uuid,
            string displayName,
            Sprite[] badgesSprites,
            Sprite tierSprite,
            Sprite defaultTierSprite,
            int score)
        {
            PlayerID = uuid;

            if (profileImage)
                profileImage.sprite = profileSprite;
            else
                Debug.LogWarning("Profile image is not assigned in LobbyChatEntry.");

            if (uuidLabel)
                uuidLabel.text = uuid;
            else
                Debug.LogWarning("UUID label is not assigned in LobbyChatEntry.");

            if (displayNameLabel)
                displayNameLabel.text = displayName;
            else
                Debug.LogWarning("Display name label is not assigned in LobbyChatEntry.");

            if (badgesImages is not null and { Length: > 0 })
                for (int i = 0; i < badgesImages.Length; i++)
                {
                    var badgeImage = badgesImages.ElementAtOrDefault(i);
                    if (!badgeImage)
                    {
                        Debug.LogWarning($"Badge image at index {i} is not assigned in LobbyChatEntry.");
                        continue;
                    }

                    var hasSprite = i < badgesSprites?.Length;
                    if (hasSprite)
                        badgeImage.sprite = badgesSprites[i];

                    badgeImage.gameObject.SetActive(hasSprite);
                }
            else
                Debug.LogWarning("Badges images array is not assigned or empty in LobbyChatEntry.");

            if (tierImage)
                tierImage.sprite = tierSprite != null ? tierSprite : defaultTierSprite;
            else
                Debug.LogWarning("Rank image is not assigned in LobbyChatEntry.");

            if (scoreLabel)
                scoreLabel.text = score.ToString();
            else
                Debug.LogWarning("Score label is not assigned in LobbyChatEntry.");
        }

        public void SetActive(bool isActive)
        {
            if (gameObject)
                gameObject.SetActive(isActive);
            else
                Debug.LogWarning("GameObject is not assigned in LobbyChatEntry.");
        }
    }
}
