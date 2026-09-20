using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelperSharedLibrary
{
    /// <summary>
    /// This class is used to validate credentials for Unity Gaming Services (UGS) using Remote Config via Unity Service or Rest API
    /// </summary>
    public abstract class CredentialsValidator
    {
        protected static HashSet<string> _tempEmailDomains = new();
        public static readonly char[] UsernameAllowedSymbols = new[] { '.', '-', '@', '_' };

        public virtual async Task Initialize()
        {
            _tempEmailDomains = await FetchBlockedDomainsAsync();
        }

        public abstract Task<HashSet<string>> FetchBlockedDomainsAsync();

        private static void BasicValidation(string toValidate, ref CredentialsDenyState refCredentialsDenyState, int minCharLenght, int maxCharLength = 20)
        {
            refCredentialsDenyState = CredentialsDenyState.IsValid;

            // Username is empty
            if (string.IsNullOrEmpty(toValidate))
                refCredentialsDenyState |= CredentialsDenyState.IsEmpty;

            // Username is too short
            if (toValidate.Length < minCharLenght)
                refCredentialsDenyState |= CredentialsDenyState.IsTooShort;

            // Username is too long
            if (toValidate.Length > maxCharLength)
                refCredentialsDenyState |= CredentialsDenyState.IsTooLong;
        }

        /// <summary>
        /// Validates the username based on the following rules:<br></br>
        /// 1. Must be between 3 and 20 characters long.<br></br>
        /// 2. Must contain only letters, numbers, and the following symbols: . - @ _<br></br>
        /// </summary>
        /// <param name="username"></param>
        /// <param name="refCredentialsDenyState"></param>
        /// <returns></returns>
        public static bool IsValidUsername(string username, ref CredentialsDenyState refCredentialsDenyState)
        {
            refCredentialsDenyState = CredentialsDenyState.IsValid;
            BasicValidation(username, ref refCredentialsDenyState, 3, 20);

            // If the usernmae is empty at starts, omit other checks and return false
            if (refCredentialsDenyState.HasFlag(CredentialsDenyState.IsEmpty))
                return false;

            // Username contains invalid characters.
            if (username.Any(x => !char.IsLetter(x) && !char.IsNumber(x) && !UsernameAllowedSymbols.Contains(x)))
                refCredentialsDenyState |= CredentialsDenyState.IsContainingInvalidChars;

            return refCredentialsDenyState == CredentialsDenyState.IsValid;
        }
        public static bool IsValidUsername(string username)
        {
            var tempCredentialsDenyState = CredentialsDenyState.IsValid;
            return IsValidUsername(username, ref tempCredentialsDenyState);
        }

        /// <summary>
        /// Validates the email based on the following rules:<br></br>
        /// 1. Must be between 3 and 64 characters long.<br></br>
        /// 2. Must contain a valid local part and domain.<br></br>
        /// 3. Must not be a temporary email domain.<br></br>
        /// </summary>
        /// <returns></returns>
        public static bool IsValidEmail(string email, ref CredentialsDenyState refCredentialsDenyState)
        {
            refCredentialsDenyState = CredentialsDenyState.IsValid;

            var localPart = email.Contains('@') ? email.Split('@')[0] : email;
            var domain = email.Contains('@') ? email.Split('@')[1] : string.Empty;

            BasicValidation(localPart, ref refCredentialsDenyState, 3, 64);

            // If the email is empty at starts, omit other checks and return false
            if (refCredentialsDenyState.HasFlag(CredentialsDenyState.IsEmpty))
                return false;

            // Check if the email doesn't has '@' to split between localPart and domain
            if (!email.Contains('@'))
                refCredentialsDenyState |= CredentialsDenyState.IsLeftingRequiredChars;

            // 📌 **Step 1: Regex Validation**
            var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            Match match = Regex.Match(email, pattern);
            if (!match.Success) 
                refCredentialsDenyState |= CredentialsDenyState.IsNotUsingEmailStructure;

            // 📌 **Step 2: VTemp Emails validation**
            if(_tempEmailDomains.Contains(domain)) // ✅ Block if the email is in the list
                refCredentialsDenyState |= CredentialsDenyState.IsNotValidDomain;

            return refCredentialsDenyState is CredentialsDenyState.IsValid;
        }
        public static bool IsValidEmail(string email)
        {
            var tempCredentialsDenyState = CredentialsDenyState.IsValid;
            return IsValidEmail(email, ref tempCredentialsDenyState);
        }


        /// <summary>
        /// Validates the password based on the following rules:<br></br>
        /// 1. Must be between 8 and 30 characters long.<br></br>
        /// 2. Must contain at least one uppercase letter, one lowercase letter, and one special character.<br></br>
        /// </summary>
        /// <param name="password"></param>
        /// <param name="refCredentialsDenyState"></param>
        /// <returns></returns>
        public static bool IsValidPassword(string password, ref CredentialsDenyState refCredentialsDenyState)
        {
            refCredentialsDenyState = CredentialsDenyState.IsValid;
            BasicValidation(password, ref refCredentialsDenyState, 8, 30);

            bool hasSymbol = password.Any(x => char.IsSymbol(x));  // Solo cuenta símbolos válidos
            bool hasPunctuation = password.Any(x => char.IsPunctuation(x));
            bool hasLower = password.Any(x => char.IsLower(x));
            bool hasUpper = password.Any(x => char.IsUpper(x));

            // Check if the password is lefting required characters
            if (!hasLower || !hasUpper || !(hasSymbol || hasPunctuation))
                refCredentialsDenyState |= CredentialsDenyState.IsLeftingRequiredChars;

            return refCredentialsDenyState == CredentialsDenyState.IsValid;
        }
        public static bool IsValidPassword(string password)
        {
            var tempCredentialsDenyState = CredentialsDenyState.IsValid;
            return IsValidPassword(password, ref tempCredentialsDenyState);
        }

        public static bool IsValidConfirmPassword(string confirmPassword, string password, ref CredentialsDenyState refCredentialsDenyState)
        {
            refCredentialsDenyState = CredentialsDenyState.IsValid;

            // Check fi both passwords are not the same
            if (!string.Equals(password, confirmPassword))
                refCredentialsDenyState |= CredentialsDenyState.IsNotMatching;

            return refCredentialsDenyState == CredentialsDenyState.IsValid;
        }


        [Flags]
        public enum CredentialsDenyState
        {
            IsValid = 0,

            IsEmpty = 1 << 0,
            IsTooShort = 1 << 1,
            IsTooLong = 1 << 2,
            IsContainingInvalidChars = 1 << 3,
            IsLeftingRequiredChars = 1 << 4,
            IsNotUsingEmailStructure = 1 << 5,
            IsNotValidDomain = 1 << 6,
            IsNotMatching = 1 << 7,
        }
    }
}