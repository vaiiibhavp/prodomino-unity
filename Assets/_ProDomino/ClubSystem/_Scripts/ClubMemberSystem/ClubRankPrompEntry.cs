using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.ClubSystem
{
    /// <summary>
    /// Represents a UI entry for selecting a club rank, displaying rank information and handling selection
    /// interactions.
    /// </summary>
    public class ClubRankPrompEntry : MonoBehaviour
    {
        [SerializeField] private Image rankImage;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private CustomButtonUI selectRankToggle;

        internal ClubRanksTypes? RankType { get; private set; }

        private void Awake()
        {
            if (selectRankToggle)
                selectRankToggle.Deselect(false);
            else
                Debug.LogError("Select Rank Toggle is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Initializes the rank entry with the provided data and selection callback.
        /// </summary>
        /// <param name="rankSprite">The sprite representing the rank.</param>
        /// <param name="rankName">The name of the rank.</param>
        /// <param name="clubRanksTypes">The type of the club rank.</param>
        /// <param name="selectRank">The callback to invoke when the rank is selected.</param>
        internal void Initialize(Sprite rankSprite, string rankName, ClubRanksTypes clubRanksTypes, Action<ClubRankPrompEntry> selectRank)
        {
            RankType = clubRanksTypes;

            if (rankImage)
                rankImage.sprite = rankSprite;
            else
                Debug.LogError("Rank Image is not assigned in the inspector.", this);

            if (rankLabel)
                rankLabel.text = rankName;
            else
                Debug.LogError("Rank Label is not assigned in the inspector.", this);

            if (selectRankToggle)
                selectRankToggle.onClick.AddListener(() => selectRank?.Invoke(this));
            else
                Debug.LogError("Select Rank Toggle is not assigned in the inspector.", this);
        }

        /// <summary>
        /// Forces the selection of this rank entry.
        /// </summary>
        /// <param name="isCallingCallback">Indicates whether to call the selection callback.</param>
        internal void ForceSelection(bool isCallingCallback = false)
        { 
            if (selectRankToggle)
                selectRankToggle.Select(isCallingCallback);
            else
                Debug.LogError("Select Rank Toggle is not assigned in the inspector.", this);
        }
    }
}
