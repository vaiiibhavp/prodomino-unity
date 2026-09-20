using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProDomino.LearningTool.ChapterType;

namespace ProDomino.LearningTool
{
    internal class LearningToolModule_HowToPlay : LearningToolModule
    {
        [Space(15), Header(nameof(LearningToolModule_HowToPlay) + "_Attributes")]
        [SerializeField] private Image gameModeDescriptionImage;
        [SerializeField] private TMP_Text gameModeDescriptionText;

        internal override ModuleType ModuleType => ModuleType.HowToPlay;
        internal HowToPlayChapters CurrentHowToPlayChapters { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            enumChaptersParsedToList = Enum.GetNames(typeof(HowToPlayChapters));
        }
    }
}
