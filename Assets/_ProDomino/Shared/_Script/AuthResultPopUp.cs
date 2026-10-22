using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Shared
{
    /// <summary>
    /// The "Account created" / "Something went wrong" pop-ups shown after an attempt to create an
    /// account. It is driven by the auth UI's sign-up event (wired in the Inspector), so the
    /// authentication logic itself is untouched. It lives outside the auth pop-up because that one
    /// closes as soon as a sign-up succeeds.<br/>
    /// The success card is also reused for other auth confirmations (e.g. password reset link sent)
    /// through <see cref="ShowSuccess"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class AuthResultPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup root;
        [SerializeField] private CanvasGroup successCard;
        [SerializeField] private CanvasGroup errorCard;

        [Header("Success card texts")]
        [SerializeField] private TMP_Text successTitleText;
        [SerializeField] private TMP_Text successDescriptionText;
        [SerializeField] private TMP_Text successButtonLabel;
        [SerializeField] private Button successButton;

        private string defaultSuccessTitle;
        private string defaultSuccessDescription;
        private string defaultSuccessButtonLabel;
        private UnityAction pendingSuccessAction;

        private void Awake()
        {
            // Keep the sign-up texts so the card can be restored after a custom message
            defaultSuccessTitle = successTitleText ? successTitleText.text : null;
            defaultSuccessDescription = successDescriptionText ? successDescriptionText.text : null;
            defaultSuccessButtonLabel = successButtonLabel ? successButtonLabel.text : null;

            if (successButton)
                successButton.onClick.AddListener(OnSuccessButtonClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (successButton)
                successButton.onClick.RemoveListener(OnSuccessButtonClicked);
        }

        /// <summary>Shows the result of a sign-up attempt. Hooked to AuthUI's onCredentialsSignUp.</summary>
        public void Show(bool succeeded)
        {
            SetSuccessTexts(defaultSuccessTitle, defaultSuccessDescription, defaultSuccessButtonLabel);
            pendingSuccessAction = null;

            SetVisible(successCard, succeeded);
            SetVisible(errorCard, !succeeded);
            SetVisible(root, true);
        }

        /// <summary>
        /// Shows the success card with a custom message. <paramref name="onButton"/> runs once when the main button is pressed.
        /// </summary>
        public void ShowSuccess(string title, string description, string buttonLabel, UnityAction onButton = null)
        {
            SetSuccessTexts(title, description, buttonLabel);
            pendingSuccessAction = onButton;

            SetVisible(successCard, true);
            SetVisible(errorCard, false);
            SetVisible(root, true);
        }

        /// <summary>Closes the pop-up (close button, "Go to Dashboard", "Try Again").</summary>
        public void Hide() => SetVisible(root, false);

        private void OnSuccessButtonClicked()
        {
            var action = pendingSuccessAction;
            pendingSuccessAction = null;
            action?.Invoke();
        }

        private void SetSuccessTexts(string title, string description, string buttonLabel)
        {
            if (successTitleText && title != null)
                successTitleText.text = title;

            if (successDescriptionText && description != null)
                successDescriptionText.text = description;

            if (successButtonLabel && buttonLabel != null)
                successButtonLabel.text = buttonLabel;
        }

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
