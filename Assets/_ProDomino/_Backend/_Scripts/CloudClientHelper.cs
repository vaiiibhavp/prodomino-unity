using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.Services.CloudSave;
using UnityEngine;

namespace ProDomino.Backend
{
    /// <summary>
    /// Helper class for Cloud Client
    /// </summary>
    /// <remarks>
    /// This class is used to help with the Cloud Client functionality.
    /// </remarks>
    public class CloudClientHelper
    {
        public static async UniTask SaveDataAsync(Dictionary<string, object> data)
        {
            if (data is null or { Count: 0 })
            {
                Debug.LogError("Data is null or empty");
                return;
            }

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (CloudSaveException ex)
            {
                Debug.LogError($"Error saving data to Cloud Save: {ex.Message}");
                throw;
            }
        }

        public static async UniTask<T> LoadDataAsync<T>(string key)
        {
            try
            {
                var loadedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>() { key });

                if (loadedData.TryGetValue(key, out var value))
                    return JsonConvert.DeserializeObject<T>(value.ToString());
            }
            catch (CloudSaveException ex)
            {
                Debug.LogError($"Error loading data from Cloud Save: {ex.Message}");
            }

            return default;
        }
        
        public static async UniTask<Dictionary<string, object>> LoadDataAsync(params string[] keys)
        {
            var keySet = new HashSet<string>(keys); // Convertir a HashSet para la consulta
            var result = new Dictionary<string, object>();

            try
            {
                var loadedData = await CloudSaveService.Instance.Data.Player.LoadAsync(keySet);

                foreach (var key in keySet)
                    result[key] = loadedData.TryGetValue(key, out var value) 
                        ? value 
                        : default;
            }
            catch (CloudSaveException ex)
            {
                Debug.LogError($"Error loading data from Cloud Save: {ex.Message}");
            }

            return result;
        }
    }
}
