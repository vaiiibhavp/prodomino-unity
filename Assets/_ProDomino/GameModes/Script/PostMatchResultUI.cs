using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Manages the user interface for displaying post-match results, including result elements, buttons, and related UI
    /// components.
    /// 
    /// This class was made because the Post Match Result UI is quite complex to scalate dinamically, and it is better to have a dedicated class to manage it, 
    /// instead of having it in the PostMatchResultContrller, which is already quite complex.
    /// </summary>
    internal class PostMatchResultUI : MonoBehaviour
    {
        [Header("References")]
        [field: SerializeField] public PostMatchResultElement PostMatchResultElementPrefab { get; private set; }
        [field: SerializeField] public Transform PostMatchResultParent { get; private set; }
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        [field: SerializeField] public Image GameModeIcon { get; private set; }
        [field: SerializeField] public GameObject ButtonContainerBottom { get; private set; }

        [Header("Buttons")]
        [field: SerializeField] public Button NextRoundButton { get; private set; }
        [field: SerializeField] public Button ToLobbyButton { get; private set; }
        [field: SerializeField] public Button RematchButton { get; private set; }
        [field: SerializeField] public Button SaveReviewButton { get; private set; }

        [Header("Texts")]
        [field: SerializeField] public TMP_Text NextRoundButtonText { get; private set; }
        [field: SerializeField] public TMP_Text RematchButtonText { get; private set; }
        [field: SerializeField] public TMP_Text GameModeText { get; private set; }
        [field: SerializeField] public TMP_Text HeaderText { get; private set; }
        [field: SerializeField] public TMP_Text PlayerResultStateText { get; private set; }
    }
}
