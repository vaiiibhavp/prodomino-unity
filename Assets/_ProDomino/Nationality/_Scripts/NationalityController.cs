using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;
using static TMPro.TMP_Dropdown;

namespace ProDomino.NationalitySystem
{
    public class NationalityController : MonoBehaviour
    {
        [SerializeField] private GameObject nationalityGameobject;
        [SerializeField] private TMP_Dropdown nationalitiesDropdown;

        private GameManager gameManager;
        private DictionaryService dictionaryService;
        private PromptFadeController promptFadeController;
        private UnityEvent<NationalityType> onNationalitySelected = new();

        internal NationalityType SelectedNationalityType { get; private set; }

        private void Awake()
        {
            onNationalitySelected ??= new();

            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            // Log errors if the required services are not available in the ServiceLocator
            if (!gameManager)
                Debug.LogError("[NationalityController] GameManager service is not available in the ServiceLocator.");

            if (!dictionaryService)
                Debug.LogError("[NationalityController] DictionaryService is not available in the ServiceLocator.");

            gameManager?.HandleOnSignIn(OnSignIn);
            gameManager?.HandleOnSignOut(OnSignOut);
        }

        private void Start()
        {
            if (!nationalitiesDropdown)
            {
                Debug.LogError("[NationalityController] Nationality dropdown is not assigned.");
                return;
            }

            if (!dictionaryService)
            {
                Debug.LogError("[NationalityController] DictionaryService is not available in the ServiceLocator.");
                return;
            }

            var nationalitiesSpriteDataCollection = dictionaryService.GetSpriteDataCollection(Consts.CollectionKeys.Nationality);

            // Simply func that retrieves a sprite from the collection based on the key
            var getSprite = new Func<string, Sprite>(key =>
            {
                var sprite = nationalitiesSpriteDataCollection?.FirstOrDefault(x => x.Key == key).Value;
                if (!sprite)
                    Debug.LogWarning($"[NationalityController] Sprite not found for key: {key}");
                return sprite;
            });

            DetermineNationalityVisibility(false);
            nationalitiesDropdown.SetValueWithoutNotify(0);

            // Reset the selected nationality
            SelectedNationalityType = default;


            nationalitiesDropdown.ClearOptions();

            // Simply get all the nationality
            var nationalities = Enum.GetNames(typeof(NationalityType))?.ToList();

            // Generate a collection of nationality and its sprite
            var nationalitiesCollection = nationalities.ToDictionary(
                x => x.SplitByUpperCase(),
                x => getSprite(x.ToString()));

            // Make a list with the options to set up in the dropdown
            var nationalitiesOptionsData = nationalitiesCollection
                ?.Select(x => new OptionData(x.Key, x.Value, Color.white))
                ?.ToList();

            // Add an empty option at the beginning of the list to allow deselecting the filter (showing all nationalities)
            nationalitiesDropdown.AddOptions(nationalitiesOptionsData);

            // Add listener to handle
            nationalitiesDropdown.onValueChanged.AddListener(OnNationalitySelected);
        }

        /// <summary>
        /// Registers a listener to be invoked when a nationality is selected.
        /// </summary>
        /// <param name="action">The callback to execute when the nationality selection event occurs.</param>
        public void HandleOnNationalitySelected(UnityAction<NationalityType> action)
        { 
            if (onNationalitySelected is not null)
                onNationalitySelected.AddListener(action);
            else
                Debug.LogWarning("Couldn't register the nationality selected listener because the event is null.");
        }

        /// <summary>
        /// Removes a listener from the nationality selected event.
        /// </summary>
        /// <param name="action">The UnityAction to remove from the event listeners.</param>
        public void UnHandleOnNationalitySelected(UnityAction<NationalityType> action)
        {
            if (onNationalitySelected is not null)
                onNationalitySelected.RemoveListener(action);
            else
                Debug.LogWarning("Couldn't unregister the nationality selected listener because the event is null.");
        }

        /// <summary>
        /// Handles the selection of a nationality from the dropdown, updates the selected nationality, and attempts to
        /// set it in the GameManager.
        /// </summary>
        /// <param name="nationalitySelectedIndex">The index of the selected nationality in the dropdown options.</param>
        private async void OnNationalitySelected(int nationalitySelectedIndex)
        {
            var nationalityName = nationalitiesDropdown.options.ElementAtOrDefault(nationalitySelectedIndex)?.text?.CompactByUpperCase();
            var nationality = Enum.TryParse(nationalityName, out NationalityType nationalityValue) ? nationalityValue : default(NationalityType?);

            // If the selected option is not "All" and the parsed nationality value is null, it means the selection is invalid
            if (!nationality.HasValue)
            {
                // Check fi the entry arg is empty. If so, that means the toggle is probably deselecting
                if (nationalityName is not "")
                    Debug.LogWarning($"[NationalityController] Invalid nationality selected: {nationalityName}");
                return;
            }

            SelectedNationalityType = nationality.Value;

            // Try to set the selected nationality in the GameManager.
            var updateNationalityResponse = await gameManager.TryToSetNationality(SelectedNationalityType);

            // Check if the response contains a new nationality.
            if (updateNationalityResponse.newNationality is not null)
            {
                // Overwrite the selected nationality with the new value from the response
                SelectedNationalityType = updateNationalityResponse.newNationality.nationalityType;

                // Invoke the onNationalitySelected event to notify any listeners about the change in nationality
                onNationalitySelected?.Invoke(SelectedNationalityType);
            }
            else
            { 
                Debug.LogError($"[NationalityController] Failed to set nationality: {SelectedNationalityType}");
                
                var currentNationalityDropdownOptionIndex = nationalitiesDropdown.options
                    .Select((option, index) => new { option, index })
                    .FirstOrDefault(x => x.option.text.CompactByUpperCase().ToString() == SelectedNationalityType.ToString())
                    ?.index ?? 0;

                // Revert the dropdown selection to the current
                nationalitiesDropdown.SetValueWithoutNotify(currentNationalityDropdownOptionIndex);

                // Show an error prompt to the user
                promptFadeController?.Fade(updateNationalityResponse.message, 3);
            }
        }

        /// <summary>
        /// Handles user sign-in by updating the selected nationality and displaying the nationality dropdown if the
        /// user is authenticated and verified.
        /// </summary>
        private void OnSignIn()
        {
            var shouldShowDropdownGraphic = gameManager.IsAuthenticatedAndVerified;

            // When the user signs in, selecte the user registered nationality
            SelectedNationalityType = gameManager.NationalityData?.nationalityType ?? default;

            // Show the dropdown graphic if the user is authenticated and verified
            if (nationalitiesDropdown)
            {
                DetermineNationalityVisibility(shouldShowDropdownGraphic);

                if (shouldShowDropdownGraphic)
                {
                    // Try to find the select dropdow nof the user's registered nationality
                    var nationalityDropdownToSelect = nationalitiesDropdown.options
                        .Select((option, index) => new { option, index })
                        .FirstOrDefault(x => x.option.text.CompactByUpperCase().ToString() == SelectedNationalityType.ToString());

                    // If found, select the dropdown option without triggering the onValueChanged event
                    if (nationalityDropdownToSelect != null)
                        nationalitiesDropdown.SetValueWithoutNotify(nationalityDropdownToSelect.index);
                }

            }
        }

        /// <summary>
        /// Resets the nationalities dropdown and selected nationality when signing out.
        /// </summary>
        private void OnSignOut()
        {
            if (nationalitiesDropdown)
            {
                DetermineNationalityVisibility(false);
                nationalitiesDropdown.SetValueWithoutNotify(0);

                // Reset the selected nationality
                SelectedNationalityType = default;
            }
        }

        /// <summary>
        /// Sets the visibility of the nationalityGameobject based on the specified value.
        /// </summary>
        /// <param name="isVisible">True to show the nationalityGameobject; false to hide it.</param>
        private void DetermineNationalityVisibility(bool isVisible)
        {
            if (nationalityGameobject)
                nationalityGameobject.SetActive(isVisible);
            else
                Debug.LogError("[NationalityController] Nationality GameObject is not assigned.");
        }
    }
}