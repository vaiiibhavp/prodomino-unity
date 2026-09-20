using HelperSharedLibrary;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Timba.Patterns;
using Timba.Utils;
using TMPro;
using UnityEngine;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.FirestoreClubData;
using static ProDomino.ClubSystem.ClubIconDataSelectorController;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Manages the UI and logic for editing club data settings, including club icon, name, and slogan, and handles
    /// permissions and navigation between related screens.
    /// </summary>
    public class ClubDataSettingsController : MonoBehaviour
    {
        [SerializeField] private Preview preview;
        [SerializeField] private ClubIconDataSelectorController clubSelectorController;
        [SerializeField] private TMP_InputField clubNameInputField;
        [SerializeField] private TMP_InputField clubSloganInputField;
        [SerializeField] private CustomButtonUI editIconButton;
        [SerializeField] private CustomButtonUI backButton;
        [SerializeField] private CustomButtonUI saveChangesButton;
        [SerializeField] private TMP_Text confirmChangesLabel;

        [Header("UI GameObjects")]
        [SerializeField] private GameObject clubIconGameObject;
        [SerializeField] private GameObject clubNameGameObject;
        [SerializeField] private GameObject clubSloganGameObject;

        private string defaultClubNameLabel;
        private string defaultClubSloganLabel;

        private GameManager gameManager;
        private DictionaryService dictionaryService;
        private Action goToIconCreation;
        private Action tryToGoToHomeScreen;
        private Action goToDataSettingsScreen;
        private Func<bool> checkIfIsUserInClub;
        private Func<ConfigData> getConfigData;
        private Func<MemberData> getCurrentPlayerMemberData;
        private Func<IconData> getCurrentIconData;
        private AsyncActionHandler<IconData, string, string, bool> tryToUpdateClubData;

        internal bool IsUserInClub => checkIfIsUserInClub?.Invoke() ?? false;
        internal ClubIconDataSelectorController ClubIconDataSelectorController => clubSelectorController;


        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            if (editIconButton)
                editIconButton.onClick.AddListener(OnGoToIconCreation);
            else
                Debug.LogError("Edit Icon Button is not assigned in the inspector.", this);

            if (backButton)
                backButton.onClick.AddListener(OnTryToGoToHomeScreen);
            else
                Debug.LogError("Back Button is not assigned in the inspector.", this);

            if (saveChangesButton)
                saveChangesButton.onClick.AddListener(SaveClubDataChanges);
            else
                Debug.LogError("Save Changes Button is not assigned in the inspector.", this);
        }

        private void Start()
        {
            // By default, hide the club name input field if the user is not in a club
            DetermineSetClubNameVisibility();

            if (clubNameInputField)
            {
                defaultClubNameLabel = clubNameInputField.text;
                clubNameInputField.onValueChanged.AddListener(_ => saveChangesButton.SetButtonInteractable(CouldSaveChanges()));
            }

            if (clubSloganInputField)
            {
                defaultClubSloganLabel = clubSloganInputField.text;
                clubSloganInputField.onValueChanged.AddListener(_ => saveChangesButton.SetButtonInteractable(CouldSaveChanges()));
            }
        }

        /// <summary>
        /// Initialize the ClubUI with the given services and actions
        /// </summary>
        /// <param name="checkIfIsUserInClub">Function to check if the user is in a club.</param>
        /// <param name="goToIconCreation">Action to navigate to the icon creation screen.</param>
        /// <param name="goToDataSettingsScreen">Action to navigate to the data settings screen.</param>
        /// <param name="tryToGoToHomeScreen">Action to attempt to navigate to the home screen.</param>
        /// <param name="getConfigData">Function to get the configuration data.</param>
        /// <param name="getCurrentIconData">Function to get the current icon data.</param>
        /// <param name="getCurrentPlayerMemberData">Function to get the current player's member data.</param>
        /// <param name="tryToUpdateClubData">Async action handler to attempt updating club data.</param>
        internal void Initialize
            (Func<bool> checkIfIsUserInClub,
            Action goToIconCreation,
            Action goToDataSettingsScreen,
            Action tryToGoToHomeScreen,
            Func<ConfigData> getConfigData,
            Func<IconData> getCurrentIconData,
            Func<MemberData> getCurrentPlayerMemberData,
            AsyncActionHandler<IconData, string, string, bool> tryToUpdateClubData)
        {
            this.checkIfIsUserInClub = checkIfIsUserInClub;
            this.goToIconCreation = goToIconCreation;
            this.goToDataSettingsScreen = goToDataSettingsScreen;
            this.tryToGoToHomeScreen = tryToGoToHomeScreen;

            this.getConfigData = getConfigData;
            this.getCurrentIconData = getCurrentIconData;
            this.getCurrentPlayerMemberData = getCurrentPlayerMemberData;

            this.tryToUpdateClubData = tryToUpdateClubData;

            // Initialize the previews modifyng statics fields
            Preview.Initialize(dictionaryService);

            // Initialize the club icon selector controller
            clubSelectorController.Initialize
                (ConfirmClubDataSelection, 
                OnGoToDataSettingsScreen);
        }

        /// <summary>
        /// Called when the user clicks the confirm icon button to register the club icon selector changes
        /// </summary>
        private void ConfirmClubDataSelection()
        {
            if (goToDataSettingsScreen is null)
            {
                Debug.LogError("onConfirmClubDataSelection is null, cannot confirm club data selection", this);
                return;
            }

            if (ClubIconDataSelectorController?.TemporalIconData is null)
            {
                Debug.LogError("TemporalIconData is null, cannot confirm club data selection", this);
                return;
            }

            goToDataSettingsScreen();
            ResfreshPreview();
        }

        /// <summary>
        /// Shows or hides the club icon Name selector UI
        /// </summary>
        private void DetermineSetClubNameVisibility()
        {
            if (!clubNameGameObject)
            {
                Debug.LogError("Club Name Canvas Group is not assigned in the inspector.", this);
                return;
            }

            bool isUserInClub = IsUserInClub;
            clubNameGameObject.SetActive(!isUserInClub);
        }

        /// <summary>
        /// Updates the permissions for the current user to edit club data based on their rank.
        /// </summary>
        private void DetermineSetClubPermissions()
        {
            // Check if the user is in a club
            if (!IsUserInClub)
            {
                // If the user is in a club, show both the club icon and slogan edit options by default
                SetObjectActive(clubIconGameObject, true);
                SetObjectActive(clubSloganGameObject, true);
                return;
            }

            // By default, hide both the club icon and slogan edit options
            SetObjectActive(clubIconGameObject, false);
            SetObjectActive(clubSloganGameObject, false);

            // Check the event action that provides current player member data is assigned
            if (getCurrentPlayerMemberData is null)
            {
                Debug.LogError("getCurrentPlayerMemberData function is not assigned.", this);
                return;
            }

            // Check if the event that provides config data is assigned
            if (getConfigData is null)
            {
                Debug.LogError("getConfigData function is not assigned.", this);
                return;
            }

            // Get the current player's member data
            var currentMemberData = getCurrentPlayerMemberData();
            if (currentMemberData is null)
            {
                Debug.LogError("Current player member data is null. Cannot open club data settings screen.", this);
                return;
            }

            // Get the configuration data
            var configData = getConfigData();
            if (configData is null or { clubsConfig: null or { clubPermissionDatas: null or { Length: 0 } } })
            {
                Debug.LogError("Configuration data or club permissions data is missing. Cannot determine if the user has permission to change club data.", this);
                return;
            }

            // Get the permissions data for the current member's rank. If not found, log an error and return.
            var permissionsData = configData.clubsConfig.clubPermissionDatas.FirstOrDefault(x => x.clubRanksType.HasFlag(currentMemberData.rank));
            if (permissionsData is null)
            {
                Debug.LogError($"No permissions data found for the rank: {currentMemberData.rank}. Cannot determine if the user has permission to change club data.", this);
                return;
            }

            // Set permissions based on the retrieved data
            SetObjectActive(clubIconGameObject, permissionsData.clubPermissionsType.HasFlag(ClubPermissionsTypes.ChangeLogo));
            SetObjectActive(clubSloganGameObject, permissionsData.clubPermissionsType.HasFlag(ClubPermissionsTypes.ChangeSlogan));

            void SetObjectActive(GameObject obj, bool isActive)
            {
                if (obj)
                    obj.SetActive(isActive);
                else
                    Debug.LogError("A GameObject is not assigned in the inspector.", this);
            }
        }

        /// <summary>
        /// Set the preview with the current club icon data or default values if not set
        /// </summary>
        internal void ResfreshPreview()
        {
            // Check if GameManager and PlayerClubData are not null
            if (gameManager is null)
            {
                Debug.LogError("You are trying to get club data but GameManager or PlayerClubData is null", this);
                return;
            }

            if (preview is null)
            {
                Debug.LogError("Preview is not assigned in the inspector.", this);
                return;
            }

            /* 
             1. try to get the temporal icon data from the club selector controller
             2. Try to get the player's current club icon data
            */
            var iconData = ClubIconDataSelectorController?.TemporalIconData ?? gameManager.PlayerClubData?.iconData;

            // Override the preview in the club selector controller to keep it in sync
            ClubIconDataSelectorController.OverridePreview(iconData);

            // Set the preview with the current club icon data or default values if not set
            preview.SetPreviewData(ClubDataSelectableType.BaseShield, iconData?.shieldId, iconData?.shieldColorId, Preview.byDefaultShieldSprite, Preview.byDefaultShieldColor);
            preview.SetPreviewData(ClubDataSelectableType.Texture, iconData?.textureId, iconData?.textureColorId, Preview.byDefaultTextureSprite, Preview.byDefaultTextureColor);
            preview.SetPreviewData(ClubDataSelectableType.CentralImage, iconData?.centralImageId, iconData?.centralImageColorId, Preview.byDefaultCentralImageSprite, Preview.byDefaultCentralImageColor);
            preview.SetPreviewData(ClubDataSelectableType.Background, default, iconData?.backgroundColorId, default, Preview.byDefaultbackgroundColor);

            // After refreshing the preview, check if the changes can be saved
            saveChangesButton.SetButtonInteractable(CouldSaveChanges());
        }

        /// <summary>
        /// Check if the current club data changes can be saved
        /// </summary>
        private bool CouldSaveChanges()
        {
            var currentIconData = getCurrentIconData?.Invoke();
            if (currentIconData is null && IsUserInClub)
            {
                Debug.LogError("Current icon data is null. Cannot determine if changes can be saved.", this);
                return false;
            }

            var hasValidName = // Using System.Text.RegularExpressions;
                clubNameInputField is not null and
                { text: not null and { Length: >= 5 and <= 15 } } &&
                Regex.IsMatch(
                    clubNameInputField.text,
                    @"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ0-9 ]+$"
                );

            // Using System.Text.RegularExpressions;
            var hasValidSlogan =
                clubSloganInputField is not null and
                { text: not null and { Length: >= 10 and <= 100 } } &&
                Regex.IsMatch(
                    clubSloganInputField.text,
                    @"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ0-9 ]+$"
                );

            var hasValidIcon = false;

            var isTemporalIconNull = new Func<bool>(() => ClubIconDataSelectorController.TemporalIconData is null);

            // If the user is already in a club, only the slogan and icon need to be valid
            if (IsUserInClub)
            {
                // Check if the icon data is valid (not null and not the same as the current icon data)
                hasValidIcon = currentIconData != ClubIconDataSelectorController.TemporalIconData;
                if (!hasValidIcon)
                { 
                    ClubIconDataSelectorController.FillEmptyIconData();
                    hasValidIcon = !isTemporalIconNull();
                }

                return hasValidSlogan || hasValidIcon;
            }

            // But if the user is creating a club, all three fields need to be valid
            else
            {
                // Check if the icon data is valid (not null)
                ClubIconDataSelectorController.FillEmptyIconData();
                hasValidIcon = !isTemporalIconNull();

                return hasValidName && hasValidSlogan && hasValidIcon;
            }
        }

        /// <summary>
        /// Save the changes made to the club data (icon, name, slogan)
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the onTryToUpdateClubData action is null.</exception>
        private async void SaveClubDataChanges()
        {
            if (!clubSelectorController || !clubNameInputField || !clubSloganInputField)
            {
                Debug.LogError("One or more required components are not assigned in the inspector.", this);
                return;
            }

            if (tryToUpdateClubData is null)
                throw new ArgumentException("onTryToUpdateClubData is null, cannot update club data");

            if (!CouldSaveChanges())
            {
                Debug.LogWarning("Cannot save changes because the current club data is not valid.", this);
                return;
            }

            var isPlayerCreatingClub = !IsUserInClub && !string.IsNullOrWhiteSpace(clubNameInputField.text);
            await tryToUpdateClubData
                (clubSelectorController.TemporalIconData,
                clubNameInputField.text,
                clubSloganInputField.text,
                isPlayerCreatingClub);

            // Once the club is created, show the club name input field
            DetermineSetClubNameVisibility();
            DetermineSetClubPermissions();

            preview.Clear();
        }

        /// <summary>
        /// Set  data values as default
        /// </summary>
        public void CleanData()
        {
            ClubIconDataSelectorController.ClearSelection();
            ResfreshPreview();

            if (clubNameInputField)
                clubNameInputField.text = defaultClubNameLabel;

            if (clubSloganInputField)
                clubSloganInputField.text = defaultClubSloganLabel;
        }

        /// <summary>
        /// Called when the controller is opened
        /// </summary>
        public void OnOpenController()
        {
            if (ClubIconDataSelectorController.TemporalIconData is null)
                ClubIconDataSelectorController.OverridePreview(gameManager.PlayerClubData?.iconData);

            DetermineSetClubNameVisibility();
            DetermineSetClubPermissions();
            ResfreshPreview();

            if (confirmChangesLabel)
            {
                if (IsUserInClub)
                    confirmChangesLabel.text = "Save Changes";
                else
                    confirmChangesLabel.text = "Create Club";
            } else
                Debug.LogWarning("Couldn't update confirmChangesLabel text because its reference is null");
        }

        /// <summary>
        /// Called when the user clicks the edit icon button to open the club icon selector
        /// </summary>
        private void OnGoToIconCreation()
        {
            if (goToIconCreation is null)
            {
                Debug.LogError("onOpenClubIconSelector is null, cannot open club icon selector", this);
                return;
            }

            clubSelectorController.OnOpen();
            goToIconCreation.Invoke();
        }

        /// <summary>
        /// Called when the user clicks the back button, when the current screen is the icon selector, to close the club icon selector and return to the data settings screen
        /// </summary>
        private void OnGoToDataSettingsScreen()
        {
            if (goToDataSettingsScreen is null)
            {
                Debug.LogError("onBackToDataSettingsScreen is null, cannot go back to data settings screen", this);
                return;
            }

            goToDataSettingsScreen();
        }

        /// <summary>
        /// Called when the user clicks the back button, when the current screen is the data selection, to return to the home screen
        /// </summary>
        private void OnTryToGoToHomeScreen()
        {
            if (tryToGoToHomeScreen is null)
            {
                Debug.LogError("onBackToHomeScreen is null, cannot go back to home screen", this);
                return;
            }

            tryToGoToHomeScreen();
            ClubIconDataSelectorController.ClearSelection();
        }
    }
}
