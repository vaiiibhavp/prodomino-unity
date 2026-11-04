using ProDomino.Shared.Accessibility;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProDomino.Authentication
{
    /// <summary>
    /// Enter (or the soft keyboard's Done) inside the Login / Registration / Recovery forms: moves to the next field,
    /// and from the last field presses the form's main button when it is enabled (otherwise focus moves on like Tab,
    /// e.g. to the Terms toggles that still need to be checked). Tab order itself is handled globally by
    /// <see cref="KeyboardNavigationController"/>.
    /// </summary>
    internal partial class AuthUI
    {
        private void OnAwake_KeyboardNavigation()
        {
            // Sign In
            Bind(signInCredentialsInputField, OnSubmit_SignInCredentials);
            Bind(signInPasswordInputField, OnSubmit_SignInPassword);

            // Sign Up
            Bind(signUpUserNameInputField, OnSubmit_SignUpUserName);
            Bind(signUpEmailInputField, OnSubmit_SignUpEmail);
            Bind(signUpPasswordInputField, OnSubmit_SignUpPassword);
            Bind(signUpConfirmPasswordInputField, OnSubmit_SignUpConfirmPassword);

            // Recovery
            Bind(recoveryEmailInputField, OnSubmit_RecoveryEmail);
        }

        private void OnDestroy_KeyboardNavigation()
        {
            Unbind(signInCredentialsInputField, OnSubmit_SignInCredentials);
            Unbind(signInPasswordInputField, OnSubmit_SignInPassword);

            Unbind(signUpUserNameInputField, OnSubmit_SignUpUserName);
            Unbind(signUpEmailInputField, OnSubmit_SignUpEmail);
            Unbind(signUpPasswordInputField, OnSubmit_SignUpPassword);
            Unbind(signUpConfirmPasswordInputField, OnSubmit_SignUpConfirmPassword);

            Unbind(recoveryEmailInputField, OnSubmit_RecoveryEmail);
        }

        private void OnSubmit_SignInCredentials(string _) => FocusNext(signInPasswordInputField);
        private void OnSubmit_SignInPassword(string _) => SubmitOrAdvance(signInButton);

        private void OnSubmit_SignUpUserName(string _) => FocusNext(signUpEmailInputField);
        private void OnSubmit_SignUpEmail(string _) => FocusNext(signUpPasswordInputField);
        private void OnSubmit_SignUpPassword(string _) => FocusNext(signUpConfirmPasswordInputField);
        private void OnSubmit_SignUpConfirmPassword(string _) => SubmitOrAdvance(signUpButton);

        private void OnSubmit_RecoveryEmail(string _) => SubmitOrAdvance(recoveryButton);

        private static void FocusNext(Selectable next)
        {
            if (next != null)
                KeyboardNavigationController.FocusNextFrame(next);
        }

        private static void SubmitOrAdvance(Button button)
        {
            if (button != null)
                KeyboardNavigationController.SubmitOrAdvanceNextFrame(button);
        }

        // Remove first: the Awake hooks can be re-run by the HandleProcesses auto-recovery.
        private static void Bind(TMP_InputField field, UnityAction<string> listener)
        {
            if (field == null)
                return;
            field.onSubmit.RemoveListener(listener);
            field.onSubmit.AddListener(listener);
        }

        private static void Unbind(TMP_InputField field, UnityAction<string> listener)
        {
            if (field != null)
                field.onSubmit.RemoveListener(listener);
        }
    }
}
