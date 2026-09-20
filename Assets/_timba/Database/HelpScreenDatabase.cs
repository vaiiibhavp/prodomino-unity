using System;
using System.Collections.Generic;
using UnityEngine;

namespace Timba.Database
{
    /// <summary>
    /// Database for help screen information.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(HelpScreenDatabase), menuName = "Timba/" + nameof(HelpScreenDatabase))]
    public class HelpScreenDatabase : ScriptableObject
    {
        [SerializeField] private List<HelpScreenData> items = new List<HelpScreenData>();
        public List<HelpScreenData> Items => items;
    }

    /// <summary>
    /// Data structure for help screen entries.
    /// </summary>
    [Serializable]
    public class HelpScreenData
    {
        public string title_en;

        [TextArea(3, 100)]
        public string description_en;

        public string title_es;

        [TextArea(3, 100)]
        public string description_es;
    } 
}