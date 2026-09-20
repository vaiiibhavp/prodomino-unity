using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ProDomino.Shared
{
    public class LanguageBoot : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Espera a que la localización esté lista
            yield return LocalizationSettings.InitializationOperation;

            // Fuerza inglés como idioma al iniciar
            var english = LocalizationSettings.AvailableLocales.GetLocale("en");
            LocalizationSettings.SelectedLocale = english;
        }
    }
}
