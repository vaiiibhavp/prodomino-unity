using Cysharp.Threading.Tasks;
using FirebaseWebGL.Scripts.FirebaseBridge;
using HelperSharedLibrary;
using Newtonsoft.Json;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProDomino.GameSystem
{
    /// <summary>
    /// This partial class was create to content all info related to PurchasesSystem into the GameManager
    /// </summary>
    public partial class GameManager
    {
        public PlayerPurchasesData PlayerPurchase { get; private set; }

        public Dictionary<string, SearchDataModel> SearchedPurchasessCollection { get; private set; }

        #region Refresh Realtime Database Player Models
        /// <summary>
        /// Refreshes the Realtime Database player purchases data if the player belongs to a purchases.
        /// </summary>
        public async UniTask RefreshRealtimeDatabasePurchasesData()
        {
            // Check if already waiting for a previous request
            if (waitingDictionary.TryGetValue(Consts.CollectionKeys.GetPurchasesData, out var isWaitingGetPurchasesData) && isWaitingGetPurchasesData)
            {
                Debug.LogWarning("Already waiting for a previous request to get purchases data");
                return;
            }

            // Mark as waiting
            waitingDictionary[Consts.CollectionKeys.GetPurchasesData] = true;

            // Reset purchases value
            PlayerPurchasesData = default;

            // Check if the platform is valid for using JSlib (WebGL)
            if (IsValidPlatformToUseJSlib())
            {
                Debug.Log($"Getting document from collection: {purchasesCollectionsIdKey}, document ID: {authManager.UUID}, callback: {nameof(OnGetPurchasesData)}, error callback: {nameof(OnFailToGetPurchasesData)}");

                // Generate a wrapper to await the callback
                var getDocumentWrapper = new Func<UniTask>(async () =>
                {
                    // Get the purchases data from Realtime Database
                    FirebaseDatabase.GetJSON
                        ($"{purchasesCollectionsIdKey}/{authManager.UUID}",
                        gameObject.name,
                        nameof(OnGetPurchasesData),
                        nameof(OnFailToGetPurchasesData));

                    // Wait until the callback is called
                    await UniTask
                        .WaitWhile(() => waitingDictionary[Consts.CollectionKeys.GetPurchasesData])
                        .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);
                });

                // Get the purchases data from Realtime Database
                await HandleProcess_GameManagerProxy
                     (uniTask: getDocumentWrapper,
                     taskId: nameof(getDocumentWrapper),
                     showLoading: true);

                // Force a reset to avoid waiting loops
                waitingDictionary[Consts.CollectionKeys.GetPurchasesData] = false;
            } 
            else
            {
                Debug.LogWarning("Realtime Database client is only available on WebGL platform - Simulating purchases data fetch in editor");

                var purchasesEncryptedData = await HandleProcess_GameManagerProxy
                    (uniTask: () => module.TryToGetPurchasesData().AsUniTask(),
                    taskId: nameof(module.TryToGetPurchasesData),
                    showLoading: true);

                var purchasesDataResponse = default(PurchaseDataResponse);

                try
                {
                    purchasesDataResponse = authManager.DeserializeAndDecryptData<PurchaseDataResponse>(purchasesEncryptedData);

                    if (purchasesDataResponse is null)
                    {
                        OnFailToGetPurchasesData("Failed to deserialize Realtime Database purchases data response.");
                        return;
                    }

                    PlayerPurchasesData = purchasesDataResponse.purchaseData;
                    if (PlayerPurchasesData is not null)
                        Debug.Log($"Successfully retrieved purchases data for player with id: {authManager.UUID}");
                    else
                        Debug.LogWarning("Failed to deserialize purchases data from Realtime Database.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception while deserializing Realtime Database purchases data response: {ex.Message}");
                }
                finally
                {
                    // Reset the waiting flag when the callback is called
                    waitingDictionary[Consts.CollectionKeys.GetPurchasesData] = false;
                }
            }
        }

        /// <summary>
        /// Callback method for handling the retrieved purchases data from Realtime Database.
        /// </summary>
        private void OnGetPurchasesData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetPurchasesData] = false;

            // Reset the waiting flag
            if (string.IsNullOrEmpty(output) || output == "null")
            {
                Debug.LogWarning("Received empty purchases data from Realtime Database.");
                return;
            }

            // Trt to deserialize the output to PurchasesData
            try
            {
                Debug.Log($"Purchases output: \n\n{output}");

                var settings = new JsonSerializerSettings
                {
                    Converters = { new FirestoreTimestampConverter() }
                };

                var dict = JsonConvert.DeserializeObject<Dictionary<string, PlayerPurchasesData.PlayerPurchaseReceiptData>>(output);
                if (dict is not null and { Count: > 0 })
                {
                    PlayerPurchasesData = new PlayerPurchasesData {
                        receipts = dict.Values.ToList()
                    };

                    Debug.Log($"Successfully retrieved purchases data for player with id: {authManager.UUID}");
                }
                else
                    Debug.LogWarning("Failed to deserialize purchases data from Realtime Database.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while deserializing purchases data from Realtime Database: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback method for handling failures in retrieving purchases data from Realtime Database.
        /// </summary>
        /// <param name="output"></param>
        private void OnFailToGetPurchasesData(string output)
        {
            // Reset the waiting flag
            waitingDictionary[Consts.CollectionKeys.GetPurchasesData] = false;
            PlayerPurchasesData = null;

            Debug.LogWarning($"Failed to get purchases data from Realtime Database: {output}");
        }
        #endregion
    }
}
