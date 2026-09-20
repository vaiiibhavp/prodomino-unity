using System;
using UnityEngine;

namespace Timba.Database
{ 
    [CreateAssetMenu(fileName = nameof(TextDictionaryDatabase), menuName = "Timba/" + nameof(TextDictionaryDatabase))]
    public class TextDictionaryDatabase : ScriptableObject
    {
        [field: SerializeField] public DictionaryText[] TextDictionaries { get; private set; }

        public string GetText(string dictionaryName, string spriteName)
        {
            foreach (var dictionary in TextDictionaries)
            {
                if (dictionary.DictionaryName == dictionaryName)
                {
                    if (dictionary.TryGetValue(spriteName, out var text))
                    {
                        return text;
                    } else
                    {
                        Debug.LogWarning($"Text '{spriteName}' not found in dictionary '{dictionaryName}'.");
                        return null;
                    }
                }
            }
            Debug.LogWarning($"Dictionary '{dictionaryName}' not found.");
            return null;
        }

        [Serializable] public class DictionaryText : KeyDictionary<string, string> { }
    }
}
