using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.LearningTool.ChapterType;

namespace ProDomino.LearningTool
{
    internal class LearningToolModule_LearnBasicStrategies : LearningToolModule
    {
        [Space(15), Header(nameof(LearningToolModule_LearnBasicStrategies) + "_Attributes")]
        [SerializeField] private Image gameModeDescriptionImage;
        [SerializeField] private TMP_Text gameModeDescriptionText;

        internal override ModuleType ModuleType => ModuleType.LearnBasicStrategies;
        internal LearnBasicStrategyChapters CurrentLearnBasicStrategyChapters { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            enumChaptersParsedToList = Enum.GetNames(typeof(HowToPlayChapters));
        }
    }
}
