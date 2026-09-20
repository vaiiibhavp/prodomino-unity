using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ProDomino.ReplaySystem
{
    public class ReplaySavedGameContainer : MonoBehaviour
    {
        public TMP_Text nameLabel;
        public TMP_Text gameTypeLabel;
        public TMP_Text dateLabel;
        public TMP_Text gameModeLabel;
        public Image gameModeIcon;
        public CanvasGroup buttonWithDataCanvasGroup;
        public CanvasGroup noSavedDataCanvasGroup;
        public Button button;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
