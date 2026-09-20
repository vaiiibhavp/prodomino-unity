using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;

namespace HelperSharedLibrary
{
    public class ExceptionHelper
    {
        public class UGSException : HttpRequestException
        {
            public UGSRequestErrorResponse? errorResponse;
            public string? content;

            public UGSException(string message, Exception? inner = null, string? content = null) : base(message, inner)
            {
                this.content = content;
                if (content is not null and not "")
                    errorResponse = JsonConvert.DeserializeObject<UGSRequestErrorResponse>(content);
            }

            public bool HasErrorCode(params string[] errorCodes)
            {
                return errorResponse?.details?.Any(x => errorCodes?.Contains(x.code) ?? false) ?? false;
            }

            public UGSException UGSAuthException()
            {
                if (this is not null and { errorResponse: not null })
                {
                    // Check if the error response is RESOURCE_NOT_FOUND
                    if (errorResponse.title == "RESOURCE_NOT_FOUND")
                        throw new UGSException("Invalid credentials. Please check your credentials", this, content);

                    foreach (var error in this.errorResponse.details)
                        throw error.code switch
                        {
                            "INVALID_CREDENTIALS" or "Invalid Credentials" or "RESOURCE_NOT_FOUND" => new UGSException("Invalid credentials. Please check your credentials", this, content),
                            "EMAIL_NOT_FOUND" or "Email Not Found" => new UGSException("User not found. Please check your email.", this, content),
                            "ACCOUNT_DISABLED" or "Account Disabled" => new UGSException("User account is disabled. Contact support.", this, content),
                            "USERNAME_EXISTS" or "Username Exists" => new UGSException("Username already exists. Try a different name.", this, content),
                            "INVALID_REQUEST" or "Invalid Request" => new UGSException("Invalid request format. Check your parameters.", this, content),
                            "NOT_FOUND" or "Not Found" => new UGSException("User not exists. Check your credentials.", this, content),
                            _ => new UGSException($"Unexpected Sign-In failure: {error.code}. Details: {error.message}", this, content),
                        };
                }

                throw new UGSException($"Unexpected Sign-In failure. {JsonConvert.SerializeObject(this, Formatting.Indented)}", this, this?.content);
            }
        }
        
        public class FirebaseException : HttpRequestException
        {
            public FirebaseRequestErrorResponse? errorResponse;
            public string? content;

            public FirebaseException(string message, Exception? inner = null, string? content = null) : base(message, inner)
            {
                this.content = content;
                if (content is not null and not "")
                {
                    if (LooksLikeJson(content))
                    {
                        try
                        {
                            errorResponse = JsonConvert.DeserializeObject<FirebaseRequestErrorResponse>(content);
                        }
                        catch
                        {
                            errorResponse = null;
                        }
                    }
                }
            }

            public bool HasErrorCode(params string[] errorCodes)
            {
                Exception? current = this;

                while (current != null)
                {
                    if (current is FirebaseException fex &&
                        !string.IsNullOrEmpty(fex.content))
                    {
                        foreach (var code in errorCodes)
                        {
                            if (fex.content.Contains(code, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }

                    current = current.InnerException;
                }

                return false;
            }


            public FirebaseException FirebaseAuthException()
            {
                if (!string.IsNullOrEmpty(content))
                {
                    string firebaseErrorMessage = content;

                    if (firebaseErrorMessage.Contains("INVALID_PASSWORD") || firebaseErrorMessage.Contains("INVALID_LOGIN_CREDENTIALS"))
                        throw new FirebaseException("Invalid credentials. Please check your credentials", this, content);

                    else if (firebaseErrorMessage.Contains("EMAIL_NOT_FOUND"))
                        throw new FirebaseException("User not found. Please check your email.", this, content);

                    else if (firebaseErrorMessage.Contains("USER_DISABLED"))
                        throw new FirebaseException("This account has been disabled. Contact support for assistance.", this, content);

                    else if (firebaseErrorMessage.Contains("EMAIL_EXISTS"))
                        throw new FirebaseException("An account with this email already exists. Try signing in or using a different email.", this, content);

                    else if (firebaseErrorMessage.Contains("INVALID_ID_TOKEN"))
                        throw new FirebaseException("The provided ID token is invalid or has expired. Please log in again.", this, content);

                    else if (firebaseErrorMessage.Contains("TOKEN_EXPIRED"))
                        throw new FirebaseException("Authentication token has expired. Please refresh or reauthenticate.", this, content);

                    else if (firebaseErrorMessage.Contains("CREDENTIAL_TOO_OLD_LOGIN_AGAIN"))
                        throw new FirebaseException("Your login session is outdated. Please log in again.", this, content);

                    else if (firebaseErrorMessage.Contains("OPERATION_NOT_ALLOWED"))
                        throw new FirebaseException("This authentication method is not enabled in Firebase settings.", this, content);

                    else if (firebaseErrorMessage.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
                        throw new FirebaseException("Too many unsuccessful login attempts. Please wait before trying again.", this, content);
                    
                    else if (firebaseErrorMessage.Contains("NOT_FOUND"))
                        throw new FirebaseException("User couldn't be found. Check your credentials.", this, content);
                }

                throw new FirebaseException($"Unexpected Sign-In failure.", this, this?.content);
            }

            private static string? FindDeepestJson(Exception ex)
            {
                while (ex != null)
                {
                    if (ex is FirebaseException fex &&
                        !string.IsNullOrEmpty(fex.content) &&
                        LooksLikeJson(fex.content))
                    {
                        return fex.content;
                    }

                    ex = ex.InnerException;
                }

                return null;
            }

            private static bool LooksLikeJson(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return false;

                value = value.TrimStart();
                return value.StartsWith("{") || value.StartsWith("[");
            }
        }
    }
}
