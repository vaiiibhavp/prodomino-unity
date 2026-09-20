using ProDomino.Shared;
using System.Collections;
using Timba.Database;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.LearningTool.LearningToolManager;

namespace ProDomino.LearningTool
{
    /// <summary>
    /// UI panel for displaying learning tool information.
    /// </summary>
    internal class LearningToolUI : MonoBehaviour, INavigationPanel
    {
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Learn;
        public bool RequiresAuthentication => false;

        /// <summary>
        /// URL for learning resources.
        /// </summary>
        [SerializeField] private string learnUrl;

        [SerializeField] private LearningToolsDatabase learningToolsGameModeData;
        [SerializeField] private TMP_Dropdown gameModeDropdown;
        [SerializeField] private Image gameModeImage;
        [SerializeField] private TMP_Text gameModeDescriptionText;
        [SerializeField] private TMP_Text gameModeRulesText;
        [SerializeField] private RectTransform containerLinkBtns;
        [SerializeField] private GameObject videoLinkBtnPrefab;
        [SerializeField] private ScrollRect scrollRectVideoLinks;
        [SerializeField] private ScrollRect scrollRectRules;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        [Header("Configuración")]
        [SerializeField] private float scrollStep = 0.25f;

        void Start()
        {
            gameModeDropdown.onValueChanged.AddListener(SelectedMode);
            SelectedMode(0);
            
            leftButton.onClick.AddListener(ScrollLeft);
            rightButton.onClick.AddListener(ScrollRight);
        }

        /// <summary>
        /// Activates or deactivates the navigation panel and optionally opens a learn URL if specified.
        /// </summary>
        /// <param name="isActive">True to activate the navigation panel; false to deactivate it.</param>
        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            if (!string.IsNullOrEmpty(learnUrl))
            {
                // Only open the URL if we're activating the panel, not deactivating it
                if (isActive)
                    Application.OpenURL(learnUrl);
            }

            else
            { 
                RootCanvasGroup?.SetActive(isActive);
                RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
            }

        }

        #region Learning tools functionalities
        /// <summary>
        /// Handles the selection of a game mode from the dropdown.
        /// </summary>
        /// <param name="index"></param>
        private void SelectedMode(int index)
        {
            switch(index)
            {
                case 0:
                    SetData(learningToolsGameModeData.GetItemById("french"));
                break;
                case 1:
                    SetData(learningToolsGameModeData.GetItemById("draw"));
                break;
                case 2:
                    SetData(learningToolsGameModeData.GetItemById("five"));
                break;
                case 3:
                    SetData(learningToolsGameModeData.GetItemById("block"));
                break;
                case 4:
                    SetData(learningToolsGameModeData.GetItemById("concentrate"));
                break;
                default:
                    Debug.Log("Invalid Option");
                break;
            }
        }

        /// <summary>
        /// Sets the UI data based on the selected game mode.
        /// </summary>
        /// <param name="gameModeDataLearningInfo">The game mode data to display.</param>
        private void SetData(GameModeDataLearningInfo gameModeDataLearningInfo)
        {
            gameModeImage.sprite = gameModeDataLearningInfo.spriteImg;
            gameModeDescriptionText.text = gameModeDataLearningInfo.descriptionText;
            gameModeRulesText.text = gameModeDataLearningInfo.rulesText;

            scrollRectVideoLinks.horizontal = true;
            scrollRectRules.verticalNormalizedPosition = 1f;
            scrollRectVideoLinks.horizontalNormalizedPosition = 0;

            foreach (Transform child in containerLinkBtns)
                Destroy(child.gameObject);

            for (int i = 0; i < gameModeDataLearningInfo.linkVideos.Length; i++)
            {
                GameObject newBtn = Instantiate(videoLinkBtnPrefab, containerLinkBtns);

                newBtn.transform.localScale = Vector3.one;

                LinkBtnData auxBtnData = newBtn.GetComponentInChildren<LinkBtnData>();
                
                auxBtnData.titleText.text = gameModeDataLearningInfo.linkVideos[i].titleText;
                auxBtnData.subTitleText.text = "(" + gameModeDataLearningInfo.linkVideos[i].subTitleText + ")";

                Button btn = auxBtnData.btn;
                string auxLink = gameModeDataLearningInfo.linkVideos[i].link;
                Debug.Log("LINK: " + auxLink);
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        OpenVideoLink(auxLink);
                    });
                }
            }

            if(gameModeDataLearningInfo.linkVideos.Length <= 4)
            {
                //containerLinkBtns.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                containerLinkBtns.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                Vector2 offsetMax = containerLinkBtns.offsetMax;
                offsetMax.x = 0;
                containerLinkBtns.offsetMax = offsetMax;

                scrollRectVideoLinks.horizontal = false;

                leftButton.gameObject.SetActive(false);
                rightButton.gameObject.SetActive(false);
            }
            else
            {
                containerLinkBtns.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                scrollRectVideoLinks.horizontal = true;

                leftButton.gameObject.SetActive(true);
                rightButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Opens a video link in the default web browser.
        /// </summary>
        /// <param name="link"></param>
        private void OpenVideoLink(string link)
        {
            if(link != "")
            {
                Application.OpenURL(link);   
            }
        }

        private void ScrollLeft()
        {
            StartCoroutine(SmoothScrollTo(scrollRectVideoLinks.horizontalNormalizedPosition - scrollStep));
        }

        private void ScrollRight()
        {
            StartCoroutine(SmoothScrollTo(scrollRectVideoLinks.horizontalNormalizedPosition + scrollStep));
        }

        private IEnumerator SmoothScrollTo(float target)
        {
            target = Mathf.Clamp01(target);
            float start = scrollRectVideoLinks.horizontalNormalizedPosition;
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                scrollRectVideoLinks.horizontalNormalizedPosition = Mathf.Lerp(start, target, t);
                yield return null;
            }

            scrollRectVideoLinks.horizontalNormalizedPosition = target;
        }
        #endregion
    }
}
