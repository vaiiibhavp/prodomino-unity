using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.CustomizationSystem
{
    /// <summary>
    /// Represents a tooltip variant that provides redirect links to navigation panels based on cosmetic purchase
    /// methods and their purchase status.
    /// </summary>
    public class TooltipLink : Tooltip
    {
        [Header("Link Variant Fields")]
        [SerializeField] private RedirectElement redirectElementPrefab;
        [SerializeField] private Transform redirectElementsParent;

        private List<RedirectElement> redirectInstances;
        private DictionaryService dictionaryService;
        private Action<NavigationPanelType> redirectAction;

        private void Awake()
        {
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            redirectInstances = redirectElementsParent?.GetComponentsInChildren<RedirectElement>(true)?.ToList() ?? new();

            if (dictionaryService is null)
                Debug.LogError("TooltipLink: DictionaryService is not initialized. Please ensure it is registered in the ServiceLocator.");
        }

        /// <summary>
        /// Assigns the redirect action for navigation panel operations.
        /// </summary>
        /// <param name="redirectAction">The action to execute when redirecting based on navigation panel type.</param>
        /// <exception cref="ArgumentNullException">Thrown when redirectAction is null.</exception>
        public void Initialize(Action<NavigationPanelType> redirectAction)
        { 
            this.redirectAction = redirectAction ?? throw new ArgumentNullException(nameof(redirectAction));
        }

        /// <summary>
        /// Configures redirect elements based on possible cosmetic purchase methods and their purchase status.
        /// </summary>
        /// <param name="getPossiblePurchaseMethods">A function that returns available cosmetic purchase methods and whether a method was already purchased.</param>
        public void Configure(Func<(CosmeticPurchaseMethod[] cosmeticPurchaseMethods, bool wasAlreadyPurchased)> getPossiblePurchaseMethods)
        {
            var (cosmeticPurchaseMethods, wasAlreadyPurchased) = getPossiblePurchaseMethods?.Invoke() ?? default;

            // If there is an already purchased cosmetic purchase method, redirect to that method
            if (wasAlreadyPurchased)
            {
                var navigationPanelType = CastPurchaseMethod(cosmeticPurchaseMethods.FirstOrDefault());
                ControlRedirectElementInstances(navigationPanelType);

                // Configure the first redirect element instance with the purchased cosmetic purchase method
                ConfigureRedirectElements(redirectInstances.FirstOrDefault(), navigationPanelType);
            }

            // If no cosmetic purchase method is already purchased, check for possible purchase methods
            else
            {
                var navigationPanelTypes = cosmeticPurchaseMethods
                    ?.Select(x => CastPurchaseMethod(x))
                    ?.Where(x => x is not NavigationPanelType.None)
                    ?.ToArray();

                // Control the number of redirect element instances based on the possible purchase methods
                ControlRedirectElementInstances(navigationPanelTypes);

                // Register the redirect element instances that are active
                var turnedOnInstances = redirectInstances
                    ?.Where(x => x.gameObject.activeSelf)
                    ?.ToArray();

                // Configure each redirect element instance with the possible purchase methods
                for (var i = 0; i < navigationPanelTypes.Length; i++)
                {
                    var navigationType = navigationPanelTypes.ElementAtOrDefault(i);
                    if (navigationType is NavigationPanelType.None)
                        continue;

                    // Get the redirect element instance that is active and configure it
                    var redirectElement = turnedOnInstances?.ElementAtOrDefault(i);
                    if (redirectElement != null)
                        ConfigureRedirectElements(redirectElement, navigationType);
                }
            }

            // Cast the cosmetic purchase method to a navigation panel type
            NavigationPanelType CastPurchaseMethod(CosmeticPurchaseMethod cosmeticPurchaseMethod)
            {
                return cosmeticPurchaseMethod switch
                {
                    CosmeticPurchaseMethod.Shop => NavigationPanelType.Shop,
                    CosmeticPurchaseMethod.Achievement => NavigationPanelType.Achievements,
                    _ => NavigationPanelType.None
                };
            }

            // Controls the number of redirect element instances based on the provided navigation panel types
            void ControlRedirectElementInstances(params NavigationPanelType[] navigationPanelTypes)
            {
                // Make sure we have at least one redirect element instance
                if (redirectInstances is null or { Count: < 1 })
                {
                    var instance = Instantiate(redirectElementPrefab, transform);
                    redirectInstances.Add(instance);
                }

                if (navigationPanelTypes is not null and { Length: > 0 })
                {
                    var instancesDifference = navigationPanelTypes.Length - redirectInstances.Count;

                    // If there are more navigation panel types than instances, create new instances
                    if (instancesDifference > 0)
                        for (var i = 0; i < instancesDifference; i++)
                        {
                            var newInstance = Instantiate(redirectElementPrefab, redirectElementsParent);
                            redirectInstances.Add(newInstance);
                        }

                    var turnedOnInstances = redirectInstances.Count(x => x.gameObject.activeSelf);

                    // If there are more turned on instances than navigation panel types, deactivate the excess instances
                    if (turnedOnInstances > navigationPanelTypes.Length)
                        for (var i = redirectInstances.Count - 1; i >= navigationPanelTypes.Length; i--)
                        {
                            var instance = redirectInstances[i];
                            if (instance != null)
                                instance.gameObject.SetActive(false);
                        }

                    // If there are fewer turned on instances than navigation panel types, configure the existing instances
                    else
                        for (var i = 0; i < navigationPanelTypes.Length; i++)
                        {
                            var instance = redirectInstances[i];
                            if (instance != null)
                                instance.gameObject.SetActive(true);
                        }
                }
            }

            /// Configure the redirect element with the appropriate message and icon
            void ConfigureRedirectElements(
                RedirectElement redirectElement,
                NavigationPanelType navigationPanelType)
            {
                // Check if the redirect element has value
                if (redirectElement == null)
                {
                    Debug.LogWarning("Redirect element is null");
                    return;
                }

                // If the navigation panel that aims the redirect element is no defined, hide the redirect element
                if (navigationPanelType is NavigationPanelType.None)
                { 
                    redirectElement.gameObject.SetActive(false);
                    redirectElement.ResetValues();
                    return;
                }    
                redirectElement.gameObject.SetActive(true);

                var redirectMessage = navigationPanelType.ToString();
                var spriteIcon = dictionaryService.GetSprite(Consts.CollectionKeys.NavigationPanelIcons, navigationPanelType.ToString());

                // Configure the redirect element with the message, icon, and action
                redirectElement.Configure
                    (redirectMessage,
                    spriteIcon,
                    () => redirectAction?.Invoke(navigationPanelType));
            }
        }
    }
}
