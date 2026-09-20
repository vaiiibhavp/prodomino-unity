using Timba.Database;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ProDomino.Shared
{
    /// <summary>
    /// UI panel for displaying help screen information.
    /// </summary>
    public class HelpScreenUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }

        public NavigationPanelType NavigationPanelType => NavigationPanelType.HelpScreen;

        public bool RequiresAuthentication => false;

        [SerializeField] private HelpScreenDatabase helpScreenDatabase;

        [SerializeField] private QuestionInHelpScreen questionInHelpScreenPrefab;

        [SerializeField] private Transform questionParant;

        void Start()
        {
            // Initialize the help screen UI by loading the database entries
            LoadDatabase();
        }

        /// <summary>
        /// Loads help screen entries from the database and populates the UI.
        /// </summary>
        private void LoadDatabase()
        {
            for(int i = 0; i < helpScreenDatabase.Items.Count; i++)
            {
                QuestionInHelpScreen instance = Instantiate(questionInHelpScreenPrefab, questionParant);

                string lang = LocalizationSettings.SelectedLocale.Identifier.Code;

                if (lang.StartsWith("es"))
                {
                    instance.SetInfo(helpScreenDatabase.Items[i].title_en, helpScreenDatabase.Items[i].description_en);
                }
                else
                {
                    instance.SetInfo(helpScreenDatabase.Items[i].title_es, helpScreenDatabase.Items[i].description_es);
                }
            }
        }

        /// <summary>
        ///  Sets the active state of the navigation panel.
        /// </summary>
        /// <param name="isActive"></param>
        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            RootCanvasGroup?.SetActive(isActive);
            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

            /*if (isActive)
            {
                
            }
            else
            {
                
            }*/
        }
    }
}
