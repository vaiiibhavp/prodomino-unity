using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProDomino.LearningTool
{
    internal abstract class LearningToolModule : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected Transform chapterParent;
        [SerializeField] protected CustomButtonToggleGroupUI chaptersButtonToggleGroupUI;
        [SerializeField] protected CustomButtonUI chapterButtonUIPrefab;

        protected DictionaryService dictionaryService;
        protected List<CustomButtonUI> chapterButtons;
        protected string[] enumChaptersParsedToList;

        private Func<GameMode> getCurrentGameMode;
        private Action<bool> setBackButtonVisibility;

        internal abstract ModuleType ModuleType { get; }
        internal GameMode GameMode => getCurrentGameMode?.Invoke() ?? default;

        protected virtual void Awake()
        {
            if (chapterParent)
                chapterButtons = chapterParent.GetComponentsInChildren<CustomButtonUI>(true)?.ToList() ?? new();
        }

        internal virtual void Initialize(DictionaryService dictionaryService, Func<GameMode> getCurrentGameMode, Action<bool> setBackButtonVisibility)
        {
            this.dictionaryService = dictionaryService;
            this.getCurrentGameMode = getCurrentGameMode;
            this.setBackButtonVisibility = setBackButtonVisibility;
        }

        internal virtual void SetActive(bool isActive)
        {
            canvasGroup.SetActive(isActive);

            if (isActive)
            { 
                setBackButtonVisibility?.Invoke(true);
                LoadChapters();
            }
        }

        protected virtual void LoadChapters()
        {
            if (chapterButtonUIPrefab == null || chaptersButtonToggleGroupUI == null)
            { 
                Debug.LogWarning($"Chapter button prefab or toggle group UI is not assigned in {gameObject.name}");
                return;
            }

            if (chapterButtons is null or { Count: 0 })
            { 
                foreach (var id in enumChaptersParsedToList)
                {
                    var chapterButton = Instantiate(chapterButtonUIPrefab, chapterParent);
                    chapterButton.SetCustomButtonID(id);
                    chapterButton.SetMainText(id);
                    chapterButtons.Add(chapterButton);

                }

                // Once all buttons are created, configure the toggle group
                chaptersButtonToggleGroupUI.Configure();
            }
        }
    }
}
