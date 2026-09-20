using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.CustomizationSystem
{
    /// <summary>
    /// Represents a UI element that displays a link button with an icon and tooltip, allowing dynamic configuration of
    /// its appearance and click behavior.
    /// </summary>
    public class RedirectElement : MonoBehaviour
    {
        [SerializeField] private CustomButtonUI linkButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text tooltipText;

        /// <summary>
        /// Dinamically configures the link button's onClick event.
        /// </summary>
        /// <param name="redirectMessage">The message to display in the tooltip.</param>
        /// <param name="icon">The icon to display on the link button.</param>
        /// <param name="onLinkButtonPress">The action to perform when the link button is pressed.</param>
        public void Configure(string redirectMessage, Sprite icon, UnityAction onLinkButtonPress)
        {
            // Configure the entry text
            if (tooltipText)
                tooltipText.text = redirectMessage;
            else
                Debug.LogWarning("Tooltip text component is not assigned.");

            // Configure the entry icon
            if (iconImage)
                iconImage.sprite = icon;
            else
                Debug.LogWarning("Icon image component is not assigned.");

            // Remove all existing listeners to prevent multiple registrations
            if (linkButton)
                linkButton.onClick.RemoveAllListeners();

            // Configure the link button's onClick event
            if (onLinkButtonPress is not null)
                linkButton.onClick.AddListener(onLinkButtonPress);
            else
                Debug.LogWarning("onLinkButtonPress is null. Link button will not be configured.");

            // Refresh layout and content size
            transform.RefreshLayoutGroupsImmediateAndRecursive();
            transform.RefreshContentSizeFitterImmediateAndRecursive(this);
        }

        /// <summary>
        /// Removes all click event listeners from the link button.
        /// </summary>
        public void ResetValues()
        {
            if (linkButton)
                linkButton.onClick.RemoveAllListeners();
        }
    }
}
