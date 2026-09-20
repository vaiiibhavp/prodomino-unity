using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace Timba.Database
{ 
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        // load dictionary from lists
        public void OnAfterDeserialize()
        {
            this.Clear();

            int minCount = Math.Min(keys.Count, values.Count);
            Dictionary<TKey, int> keyOccurrences = new Dictionary<TKey, int>();

            for (int i = 0; i < minCount; i++)
            {
                TKey originalKey = keys[i];
                TValue value = values[i];

                if (originalKey == null)
                {
                    Debug.LogWarning($"[SerializableDictionary] Clave nula en índice {i}. se omite.");
                    continue;
                }

                TKey keyToAdd = originalKey;

                // Detectar claves duplicadas y modificarlas
                if (this.ContainsKey(keyToAdd))
                {
                    int count = keyOccurrences.TryGetValue(originalKey, out var c) ? c + 1 : 1;
                    keyOccurrences[originalKey] = count;

                    // Para claves string e int, podemos generar versiones únicas
                    if (typeof(TKey) == typeof(string))
                    {
                        keyToAdd = (TKey)(object)($"{originalKey}_{count}");
                    } else if (typeof(TKey) == typeof(int))
                    {
                        int baseKey = Convert.ToInt32(originalKey);
                        keyToAdd = (TKey)(object)(baseKey + count);
                    } else
                    {
                        Debug.LogWarning($"[SerializableDictionary] Clave duplicada en índice {i}: '{originalKey}. no se puede generar nueva clave automáticamente para tipo {typeof(TKey)}.");
                        continue;
                    }

                    Debug.LogWarning($"[SerializableDictionary] Clave duplicada detectada en índice {i}: '{originalKey}'. reemplazada por '{keyToAdd}'.");
                }

                this[keyToAdd] = value;
                keyOccurrences[originalKey] = keyOccurrences.TryGetValue(originalKey, out var v) ? v : 0;
            }
        }

    }

    [Serializable]
    public class KeyDictionary<TKey, TValue> 
    {
        [field: SerializeField] public string DictionaryName { get; private set; }
        [field: SerializeField] public SerializableDictionary<TKey, TValue> SerializableDictionary { get; private set; }

        public bool TryGetValue(TKey key, out TValue value)
        {
            // Check if the key is null
            if (key is null)
            {
                value = default;

                Debug.LogWarning($"[KeyDictionary] Null key provided for dictionary '{DictionaryName}'.");
                return false;
            }

            // Try to get the value from the dictionary
            if (key is string keyString && typeof(TKey) == typeof(string))
            {
                var tempDict = SerializableDictionary
                    ?.Select(x => (key: x.Key as string, value: x.Value))
                    ?.ToArray();

                value = tempDict.FirstOrDefault(x => x.key.Compare(keyString)).value;
                return value != null;
            }

            // Return the value if the key exists in the dictionary
            return SerializableDictionary.TryGetValue(key, out value);
        }
        public ICollection<TValue> Values => SerializableDictionary?.Values;
    }
}
