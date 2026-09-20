using HelperSharedLibrary;
using System;
using TMPro;
using UnityEngine;
using static ProDomino.ClubSystem.ClubIconDataSelectorController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a UI entry for displaying club information and handling joining requests in a club search interface.
    /// </summary>
    internal class ClubSearchEntry : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup rootCanvas;
        [SerializeField] protected Preview iconPreview;
        [SerializeField] protected TMP_Text clubNameLabel;
        [SerializeField] protected TMP_Text clubSloganLabel;
        [SerializeField] protected TMP_Text clubMembersLabel;
        [SerializeField] protected CustomButtonUI sendJoiningRequestButton;

        protected Action<FirestoreClubData> tryToOpenSendJoiningRequestPopUp;
        protected Func<ConfigData> getClubConfig;

        public FirestoreClubData ClubData { get; protected set; }
        public Preview IconPreview => iconPreview;

        protected virtual void Awake()
        {
            if (sendJoiningRequestButton)
                sendJoiningRequestButton.onClick.AddListener(OnTryToSendJoiningRequest);
        }

        /// <summary>
        /// Initializes the ClubSearchEntry with a function to retrieve the current ClubsConfig.
        /// </summary>
        /// <param name="tryToOpenSendJoiningRequestPopUp">Action to open the send joining request pop-up.</param>
        /// <param name="getConfigData">Function to retrieve the current configuration data.</param>
        internal void Initialize
            (Action<FirestoreClubData> tryToOpenSendJoiningRequestPopUp,
            Func<ConfigData> getConfigData)
        {
            this.tryToOpenSendJoiningRequestPopUp = tryToOpenSendJoiningRequestPopUp ?? throw new ArgumentNullException(nameof(tryToOpenSendJoiningRequestPopUp), "tryToSendJoiningRequest action cannot be null.");
            this.getClubConfig = getConfigData ?? throw new ArgumentNullException(nameof(getConfigData), "getConfigData function cannot be null.");
        }

        /// <summary>
        /// Configures the club search entry with the provided club data.
        /// </summary>
        /// <param name="clubData">The club data to configure the entry with.</param>
        internal virtual void Configure(FirestoreClubData clubData)
        { 
            ClubData = clubData;
            if (ClubData is null)
            { 
                Debug.LogWarning("ClubData is null. Cannot configure ClubSearchEntry.", this);
                return;
            }

            var configData = getClubConfig?.Invoke();
            if (configData is null or { clubsConfig: null })
            {
                Debug.LogWarning("ConfigData or ClubsConfig is null. Cannot configure ClubSearchEntry.", this);
                return;
            }

            // Configure the preview with the current club icon data or default values if not set
            if (IconPreview != null)
            {
                IconPreview.SetPreviewData(ClubDataSelectableType.BaseShield, ClubData.iconData?.shieldId, ClubData.iconData?.shieldColorId, Preview.byDefaultShieldSprite, Preview.byDefaultShieldColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.Texture, ClubData.iconData?.textureId, ClubData.iconData?.textureColorId, Preview.byDefaultTextureSprite, Preview.byDefaultTextureColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.CentralImage, ClubData.iconData?.centralImageId, ClubData.iconData?.centralImageColorId, Preview.byDefaultCentralImageSprite, Preview.byDefaultCentralImageColor);
                IconPreview.SetPreviewData(ClubDataSelectableType.Background, default, ClubData.iconData?.backgroundColorId, default, Preview.byDefaultbackgroundColor);
            } 
            else
                Debug.LogWarning("Icon Preview is not assigned in the inspector.", this);

            // Set the club name label
            if (clubNameLabel)
                clubNameLabel.text = ClubData.clubName;
            else
                Debug.LogWarning("Club Name Label is not assigned in the inspector.", this);

            // Set the club slogan label
            if (clubSloganLabel)
                clubSloganLabel.text = ClubData.slogan;
            else
                Debug.LogWarning("Club Slogan Label is not assigned in the inspector.", this);

            // Set the club members label
            if (clubMembersLabel)
                clubMembersLabel.text = $"{ClubData.members?.Count ?? 0}/{configData.clubsConfig.membersLimit} Members";
            else
                Debug.LogWarning("Club Members Label is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Handles the click event to send a joining request to the club.
        /// </summary>
        protected virtual void OnTryToSendJoiningRequest()
        {
            if (ClubData is null || string.IsNullOrEmpty(ClubData.normalizedName))
            {
                Debug.LogWarning("ClubData is null. Cannot send join request.", this);
                return;
            }

            if (tryToOpenSendJoiningRequestPopUp is null)
            {
                Debug.LogWarning("tryToSendRequest is not set. Cannot send join request.", this);
                return;
            }

            rootCanvas?.SetActive(false, isSettingAlpha: false);
            try
            {
                tryToOpenSendJoiningRequestPopUp(ClubData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while trying to send join request: {ex.Message}", this);
            }
            finally
            {
                rootCanvas?.SetActive(true);
            }
        }
    }
}
