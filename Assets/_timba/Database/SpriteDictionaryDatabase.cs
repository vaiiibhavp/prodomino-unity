using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Timba.Database
{ 
    [CreateAssetMenu(fileName = nameof(SpriteDictionaryDatabase), menuName = "Timba/" + nameof(SpriteDictionaryDatabase))]
    public class SpriteDictionaryDatabase : ScriptableObject
    {
        [field: SerializeField] public DictionarySprite[] SpriteDictionaries { get; private set; }

        public Sprite GetSprite(string dictionaryName, string spriteName)
        {
            var entry = SpriteDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                if (entry.TryGetValue(spriteName, out var sprite))
                    return sprite;
                else
                {
                    Debug.LogWarning($"Sprite '{spriteName}' not found in dictionary '{dictionaryName}'.");
                    return null;
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }
        
        public string GetName(string dictionaryName, Sprite sprite)
        {
            var entry = SpriteDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                var searched = entry.SerializableDictionary.FirstOrDefault(x => x.Value == sprite).Key;
                if (!string.IsNullOrEmpty(searched))
                    return searched;
                else
                {
                    Debug.LogWarning($"Sprite '{sprite.name}' not found as a value in dictionary '{dictionaryName}'.");
                    return null;
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }

        public Sprite GetSpriteNoCollection(string spriteName)
        {
            foreach (var dictionary in SpriteDictionaries)
                if (dictionary.TryGetValue(spriteName, out var sprite))
                    return sprite;

            Debug.LogWarning($"Sprite '{spriteName}' not found in any dictionary.");
            return null;
        }

        public Sprite[] GetSpriteCollection(string dictionaryName)
        {
            var entry = SpriteDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                return entry.Values.ToArray();
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }
        
        
        public Dictionary<string, Sprite> GetSpriteDataCollection(string dictionaryName)
        {
            var entry = SpriteDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                return entry.SerializableDictionary.ToDictionary(x => x.Key, x => x.Value);
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }

        [Serializable] public class DictionarySprite : KeyDictionary<string, Sprite> { }
    }
}
