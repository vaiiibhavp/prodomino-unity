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
        public static Task<string> Get(string key, string table = DefaultTable)
        {
            var localized = new LocalizedString(table, key);
            var handle = localized.GetLocalizedStringAsync();

            // AsyncOperationHandle.Task waits on a worker thread, which never completes on WebGL (no threads),
            // so the result is taken from the Completed callback instead
            var completion = new TaskCompletionSource<string>();
            handle.Completed += x => completion.TrySetResult(x.Result);
            return completion.Task;
        }
    }
}
