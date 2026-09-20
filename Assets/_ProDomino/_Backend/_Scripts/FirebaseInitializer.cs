using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;
using Unity.Services.CloudCode.GeneratedBindings;

namespace ProDomino.Backend
{
    public class FirebaseInitializer : MonoBehaviour
    {
        public static bool IsFirebaseInitialized { get; private set; } = false;

        private async void Awake()
        {
            // Initialize Firebase with the JSON config
            if (!isValidPlatform())
                return;

            //// Wait for Unity services to be initialized and the user to be signed in
            //await UniTask.WaitUntil
            //    (() => AuthenticationService.Instance.IsSignedIn, 
            //    cancellationToken: this.GetCancellationTokenOnDestroy());

            // Get the Firebase config JSON from the backend (encrypted)
            //var jsonConfig = await GetFirebaseConfigJson();
            //if (string.IsNullOrEmpty(jsonConfig))
            //{
            //    Debug.LogError("Failed to get Firebase config JSON.");
            //    return;
            //}

            
            //FirebaseInitialization.InitFirebase(jsonConfig, gameObject.name, nameof(OnInitSuccessfully), nameof(OnFailedToInit));

            async Task<string> GetFirebaseConfigJson()
            {
                // Call the function within the module and provide the parameters we defined in there
                var module = new BackendBindings(CloudCodeService.Instance);
                var getFirebaseConfigResponse = await module.GetFirebaseConfig();
                var securityData = JsonConvert.DeserializeObject<SecurityData>(getFirebaseConfigResponse);

                if (securityData is null 
                    || string.IsNullOrEmpty(securityData.encryptedData) 
                    || string.IsNullOrEmpty(securityData.ivBase64))
                {
                    Debug.LogError("Failed to get Firebase config data.");
                    return null;
                }

                string playerId = AuthenticationService.Instance.PlayerId;
                string accessToken = AuthenticationService.Instance.AccessToken;

                var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
                return SecurityHelper.DecryptData(securityData, derivedKey);
            }
        }
        private bool isValidPlatform() => Application.platform is RuntimePlatform.WebGLPlayer;

        private void OnInitSuccessfully(string info)
        {
            Debug.Log($"Firebase initialized successfully: {info}");
            IsFirebaseInitialized = true;
        }

        private void OnFailedToInit(string info)
        {
            Debug.LogError($"Failed to initialize Firebase: {info}");
        }
    }
}
