using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Threading.Tasks;

namespace ProDomino.Shared
{
    public static class LocalizationHelper
    {
        private const string DefaultTable = "MainTable"; // Cambia al nombre de tu tabla principal

        /// <summary>
        /// Obtiene el texto localizado de forma asíncrona.
        /// </summary>
        public static async Task<string> Get(string key, string table = DefaultTable)
        {
            var localized = new LocalizedString(table, key);
            var handle = localized.GetLocalizedStringAsync();
            await handle.Task;
            return handle.Result;
        }
    }
}
