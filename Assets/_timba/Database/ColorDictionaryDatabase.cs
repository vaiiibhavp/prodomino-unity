using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Timba.Database
{ 
    [CreateAssetMenu(fileName = nameof(ColorDictionaryDatabase), menuName = "Timba/" + nameof(ColorDictionaryDatabase))]
    public class ColorDictionaryDatabase : ScriptableObject
    {
        [field: SerializeField] public DictionaryColor[] ColorDictionaries { get; private set; }

        public Color? GetColor(string dictionaryName, string colorName)
        {
            foreach (var dictionary in ColorDictionaries)
            {
                if (dictionary.DictionaryName == dictionaryName)
                {
                    if (dictionary.TryGetValue(colorName, out var color))
                    {
                        return color == Color.clear ? null : color;
                    } else
                    {
                        Debug.LogWarning($"Color '{colorName}' not found in dictionary '{dictionaryName}'.");
                        return null;
                    }
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }
        public string GetName(string dictionaryName, Color color)
        {
            var entry = ColorDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                var searched = entry.SerializableDictionary.FirstOrDefault(x => x.Value == color).Key;
                if (!string.IsNullOrEmpty(searched))
                    return searched;
                else
                {
                    Debug.LogWarning($"Color '{color.ToString()}' not found as a value in dictionary '{dictionaryName}'.");
                    return null;
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }

        public Color? GetColorNoCollection(string colorName)
        {
            foreach (var dictionary in ColorDictionaries)
                if (dictionary.TryGetValue(colorName, out var color))
                    return color == Color.clear ? null : color;

            Debug.LogWarning($"Color '{colorName}' not found in any dictionary.");
            return null;
        }

        public Color[] GetColorCollection(string dictionaryName)
        {
            var entry = ColorDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                return entry.Values.ToArray();
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }
        
        public Dictionary<string, Color?> GetColorDataCollection(string dictionaryName)
        {
            var entry = ColorDictionaries?.FirstOrDefault(x => x.DictionaryName == dictionaryName);
            if (entry != null)
            {
                return entry.SerializableDictionary.ToDictionary(x => x.Key, x => x.Value == Color.clear ? default(Color?) : x.Value);
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }
        [Serializable] public class DictionaryColor : KeyDictionary<string, Color> { }
    }
}
