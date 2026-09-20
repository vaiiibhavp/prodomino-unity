using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using ProDomino.Shared;

namespace ProDomino.Insights
{
    [CreateAssetMenu(fileName = nameof(ExposedGameControllerDictionaryDatabase), menuName = "Timba/" + nameof(ExposedGameControllerDictionaryDatabase))]
    public class ExposedGameControllerDictionaryDatabase : ScriptableObject
    {
        [field: SerializeField] public DictionaryExposedGameController[] ExposedGameControllerDictionaries { get; private set; }

        
        public IExposedGameController GetExposedGameController(string dictionaryName, GameMode gameMode)
        {
            foreach (var dictionary in ExposedGameControllerDictionaries)
            {
                if (dictionary.DictionaryName == dictionaryName)
                {
                    if (dictionary.TryGetValue(gameMode, out var interfaceReference))
                    {
                        return interfaceReference.Value;
                    } else
                    {
                        Debug.LogWarning($"Text '{gameMode}' not found in dictionary '{dictionaryName}'.");
                        return default;
                    }
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return default;
        }

        [Serializable]
        public class DictionaryExposedGameController : Dictionary<GameMode, InterfaceReference<IExposedGameController>>, ISerializationCallbackReceiver
        {
            [field: SerializeField] public string DictionaryName { get; private set; }

            [SerializeField]
            private List<GameMode> keys = new();

            [SerializeField]
            private List<InterfaceReference<IExposedGameController>> values = new();

            // save the dictionary to lists
            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();
                foreach (KeyValuePair<GameMode, InterfaceReference<IExposedGameController>> pair in this)
                {
                    keys.Add(pair.Key);
                    values.Add(pair.Value);
                }
            }

            // load dictionary from lists
            public void OnAfterDeserialize()
            {
                this.Clear();

                if (keys.Count != values.Count)
                {
                    var diff = values.Count - keys.Count;

                    for (int i = 0; i < diff; i++)
                    {
                        keys.Add(default);
                    }
                }

                for (int i = 0; i < keys.Count; i++)
                {
                    var key = keys.ElementAtOrDefault(i);
                    if (ContainsKey(key))
                    {
                        if (typeof(GameMode) == typeof(string))
                        {
                            key = (GameMode)(object)string.Empty;
                        } else
                        {
                            key = Activator.CreateInstance<GameMode>();
                        }
                    }

                    this.Add(key, values.ElementAtOrDefault(i));
                }
            }
        }
    }
}
