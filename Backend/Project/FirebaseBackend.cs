using FirebaseSharedLibrary;
using HelperSharedLibrary;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace Backend;

/// <summary>
/// This class contains a Cloud Code function to retrieve Firebase configuration data.<br></br>
/// Its main purpose is to provide the necessary configuration for Firebase services and hide a little bit sensitive data.
/// </summary>
public class FirebaseBackend
{
    internal static FirebaseConfigData _firebaseConfigData;

    internal static readonly RestClient _authClient = new("https://identitytoolkit.googleapis.com/");
    internal static readonly RestClient _firestoreClient = new("https://firestore.googleapis.com/");
    internal static readonly RestClient _realtimeDatabaseClient;
    internal static readonly RestClient _functionsClient;

    static FirebaseBackend()
    {
        try
        {
            _firebaseConfigData = new
            (
                apiKey: "AIzaSyA9WjUEn5IxbnxuDqbArYmh9tfEWW9qsAU",
                authDomain: "playprodomino.firebaseapp.com",
                databaseURL: "https://playprodomino-default-rtdb.firebaseio.com",
                projectId: "playprodomino",
                storageBucket: "playprodomino.firebasestorage.app",
                messagingSenderId: "794812349025",
                appId: "1:794812349025:web:3b3dd58dbcf7f41c0a0820",
                functionsRegion: "us-central1"
            );

        _realtimeDatabaseClient = new($"https://{_firebaseConfigData.projectId}-default-rtdb.firebaseio.com/");
            _functionsClient = new($"https://{_firebaseConfigData.functionsRegion}-{_firebaseConfigData.projectId}.cloudfunctions.net/");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize FirebaseBackend: {ex.Message}");
        }
    }

    /// <summary>
    /// Cloud Code function to retrieve Firebase configuration data.<br></br>
    /// This was made to hide sensitive data from Client and provide the necessary configuration for Firebase services.<br></br><br></br>
    /// TODO: check if is necesary to use RemoteConfig or any Key Management Systems (AWS KMS, Azure Key Vault).
    /// </summary>
    /// <param name="executionContext"></param>
    /// <returns>json of the security data</returns>
    [CloudCodeFunction(nameof(GetFirebaseConfig))]
    public Task<string> GetFirebaseConfig(IExecutionContext executionContext)
    {
        // Check if the execution context is null
        if (executionContext == null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        var firebaseConfigDataJson = JsonConvert.SerializeObject(_firebaseConfigData);
        var derivedKey = SecurityHelper.DeriveKey
            (executionContext?.PlayerId ?? string.Empty,
            executionContext?.AccessToken ?? string.Empty);

        var securityDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(firebaseConfigDataJson, derivedKey));
        return Task.FromResult(securityDataJson);
    }
}
