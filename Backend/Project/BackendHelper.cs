using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using static HelperSharedLibrary.ExceptionHelper;

namespace HelperSharedLibrary
{
    public class BackendHelper
    {
        /// <summary>
        /// Validates the execution context to ensure required authentication and identification fields are present.
        /// </summary>
        /// <param name="contextData">Execution context containing authentication and identification information.</param>
        /// <exception cref="Exception">Thrown if the execution context or any required fields are missing or invalid.</exception>
        internal static void ContextValidation(IExecutionContext contextData)
        {
            // Check if the execution context is valid. This is important to ensure that the function is being called from a valid context.
            if (contextData is null 
                || string.IsNullOrEmpty(contextData.ServiceToken) 
                || string.IsNullOrEmpty(contextData.PlayerId)
                || string.IsNullOrEmpty(contextData.AccessToken))
                throw new Exception("Access not Granted. Only backend is valid to process this endpoint");
        }

       /// <summary>
       /// Validates, decrypts, and parses encrypted parameters from a JSON string using the provided player ID and
       /// access token.
       /// </summary>
       /// <param name="parametersEncryptedJson">A JSON string containing the encrypted parameters.</param>
       /// <param name="playerIDObj">The player ID used for key derivation, or null.</param>
       /// <param name="accessTokenObj">The access token used for key derivation, or null.</param>
       /// <returns>A dictionary containing the decrypted and parsed parameters.</returns>
       /// <exception cref="UGSException">Thrown when validation, decryption, or parsing of parameters fails.</exception>
        internal static Dictionary<string, object> ValidateEncriptedParameters(string parametersEncryptedJson, string? playerIDObj, string? accessTokenObj)
        {
            // Check if the data dictionary contains the required keys and values
            if (string.IsNullOrEmpty(parametersEncryptedJson))
                throw new UGSException($"Invalid input data. Provide parameters.");

            var decrypedDataJson = default(string);
            try
            {
                // Parse the JSON string into a dictionary
                var encryptedSecurityData = JsonConvert.DeserializeObject<SecurityData>(parametersEncryptedJson);
                if (encryptedSecurityData is null)
                    throw new ArgumentException($"Failed to parse input data. Check the input format.");

                // Decrypt the security data
                var playerID = playerIDObj ?? string.Empty;
                var accessToken = accessTokenObj ?? string.Empty;
                var derivedKey = SecurityHelper.DeriveKey(playerID, accessToken);

                // Decrypt the data using the derived key
                decrypedDataJson = SecurityHelper.DecryptData(encryptedSecurityData, derivedKey);
                if (string.IsNullOrEmpty(decrypedDataJson))
                    throw new UGSException($"Failed to decrypt input data. Check the input format.");
            }
            catch (Exception ex)
            {
                throw new UGSException($"An error occurred while validating and decrypting parameters: {ex.Message}", ex);
            }

            try
            {
                // Parse the decrypted JSON string into a dictionary
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(decrypedDataJson);
                if (data is null || data.Count == 0)
                    throw new ArgumentException($"Failed to parse input data. Check the input format.");

                // Return the decrypted and parsed data
                return data;
            }
            catch (Exception ex)
            {
                throw new UGSException($"An error occurred while parsing decrypted parameters: {ex.Message}", ex);
            }
        }
    }
}
