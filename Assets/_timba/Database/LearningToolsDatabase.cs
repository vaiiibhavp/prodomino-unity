using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Timba.Database
{
    /// <summary>
    /// Database for learning tools information.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(LearningToolsDatabase), menuName = "Timba/" + nameof(LearningToolsDatabase))]
    public class LearningToolsDatabase : ScriptableObject
    {
        [SerializeField] private List<GameModeDataLearningInfo> items = new List<GameModeDataLearningInfo>();

        private Dictionary<string, GameModeDataLearningInfo> itemDictionary;

        void OnEnable()
        {
            // Build the dictionary for quick lookup
            BuildDictionary();
        }

        /// <summary>
        /// Builds a dictionary for quick lookup of items by their ID.
        /// </summary>
        private void BuildDictionary()
        {
            itemDictionary = new Dictionary<string, GameModeDataLearningInfo>();
            foreach (var item in items)
            {
                if (!itemDictionary.ContainsKey(item.id))
                {
                    itemDictionary.Add(item.id, item);
                }
                else
                {
                    Debug.LogWarning($"Duplicated key '{item.id}' in database '{name}'");
                }
            }
        }

        /// <summary>
        /// Retrieves an item by its ID.
        /// </summary>
        /// <param name="id">The ID of the item to retrieve.</param>
        /// <returns>The item with the specified ID, or null if not found.</returns>
        public GameModeDataLearningInfo GetItemById(string id)
        {
            if (itemDictionary == null)
                BuildDictionary();

            if (itemDictionary.TryGetValue(id, out var data))
                return data;

            Debug.LogWarning($"Item with ID '{id}' not found in database '{name}'");
            return null;
        }

        /// <summary>
        /// Retrieves all items in the database.
        /// </summary>
        /// <returns>A list of all items.</returns>
        public List<GameModeDataLearningInfo> GetAllItems() => items;
    }

    /// <summary>
    /// Data structure for learning tool game mode information.
    /// </summary>
    [Serializable]
    public class GameModeDataLearningInfo
    {
        public string id;
        public Sprite spriteImg;
        public linkVideo[] linkVideos;

        [TextArea(3, 10)]
        public string descriptionText;

        [TextArea(3, 100)]
        public string rulesText;
    }

    /// <summary>
    /// Data structure for video link information.
    /// </summary>
    [Serializable]
    public class linkVideo
    {
        public string titleText;
        public string subTitleText;
        public string link;
    }

    
}