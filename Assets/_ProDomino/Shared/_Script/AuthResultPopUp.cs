using UnityEngine;

namespace ProDomino.Shared
{
    /// <summary>
    /// The "Account created" / "Something went wrong" pop-ups shown after an attempt to create an
    /// account. It is driven by the auth UI's sign-up event (wired in the Inspector), so the
    /// authentication logic itself is untouched. It lives outside the auth pop-up because that one
    /// closes as soon as a sign-up succeeds.
    /// </summary>
    [DisallowMultipleComponent]
    public class AuthResultPopUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup root;
        [SerializeField] private CanvasGroup successCard;
        [SerializeField] private CanvasGroup errorCard;

        private void Awake() => Hide();

        /// <summary>Shows the result of a sign-up attempt. Hooked to AuthUI's onCredentialsSignUp.</summary>
        public void Show(bool succeeded)
        {
            SetVisible(successCard, succeeded);
            SetVisible(errorCard, !succeeded);
            SetVisible(root, true);
        }

        /// <summary>Closes the pop-up (close button, "Go to Dashboard", "Try Again").</summary>
        public void Hide() => SetVisible(root, false);

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
