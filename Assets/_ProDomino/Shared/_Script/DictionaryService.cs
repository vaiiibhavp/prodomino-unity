using System;
using System.Collections.Generic;
using Timba.Database;
using Timba.Patterns;
using UnityEngine;

namespace ProDomino.Shared
{
    public class DictionaryService : SingleInstanceMonoBehaviour<DictionaryService>, IService
    {
        [SerializeField] private SpriteDictionaryDatabase _spriteDictionaryDatabase;
        [SerializeField] private TextDictionaryDatabase _textDictionaryDatabase;
        [SerializeField] private ColorDictionaryDatabase _colorDictionaryDatabase;

        public bool IsAlreadyInitialized => true;

        public Sprite GetSprite(string dictionaryName, string spriteName)
        {
            if (!_spriteDictionaryDatabase)
            {
                Debug.LogError("SpriteDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _spriteDictionaryDatabase.GetSprite(dictionaryName, spriteName);
        }
        
        public string GetSpriteID(string dictionaryName, Sprite sprite)
        {
            if (!_spriteDictionaryDatabase)
            {
                Debug.LogError("SpriteDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _spriteDictionaryDatabase.GetName(dictionaryName, sprite);
        }
        public Sprite GetSpriteNoCollection(string spriteName)
        {
            if (!_spriteDictionaryDatabase)
            {
                Debug.LogError("SpriteDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _spriteDictionaryDatabase.GetSpriteNoCollection(spriteName);
        }

        public Sprite[] GetSpriteCollection(string dictionaryName)
        {
            if (!_spriteDictionaryDatabase)
            {
                Debug.LogError("SpriteDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _spriteDictionaryDatabase.GetSpriteCollection(dictionaryName);
        }
        
        public Dictionary<string, Sprite> GetSpriteDataCollection(string dictionaryName)
        {
            if (!_spriteDictionaryDatabase)
            {
                Debug.LogError("SpriteDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _spriteDictionaryDatabase.GetSpriteDataCollection(dictionaryName);
        }

        public string GetText(string dictionaryName, string textName)
        {
            if (!_textDictionaryDatabase)
            {
                Debug.LogError("TextDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _textDictionaryDatabase.GetText(dictionaryName, textName);
        }
        
        public Color? GetColor(string dictionaryName, string colorName)
        {
            if (!_colorDictionaryDatabase)
            {
                Debug.LogError("ColorDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _colorDictionaryDatabase.GetColor(dictionaryName, colorName);
        }

        public Color[] GetColorCollection(string dictionaryName)
        {
            if (!_colorDictionaryDatabase)
            {
                Debug.LogError("ColorDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _colorDictionaryDatabase.GetColorCollection(dictionaryName);
        }
        
        public Dictionary<string, Color?> GetColorDataCollection(string dictionaryName)
        {
            if (!_colorDictionaryDatabase)
            { 
                Debug.LogError("ColorDictionaryDatabase is not assigned in the inspector.", this);
                return null;
            }
            return _colorDictionaryDatabase.GetColorDataCollection(dictionaryName);
        }
    }
}
