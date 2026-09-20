using ProDomino.GameModes;
using UnityEngine;
using TMPro;

namespace ProDomino.ReplaySystem
{
    public class ReplayTurnByTurn : MonoBehaviour
    {
        /*[SerializeField]
        private ScoreUI scoreUI_playerPlayer;
        [SerializeField]
        private ScoreUI scoreUI_playerLeft;
        [SerializeField]
        private ScoreUI scoreUI_playerTop;
        [SerializeField]
        private ScoreUI scoreUI_playerRight;*/

        [SerializeField]
        private CanvasGroup canvasGroup;

        public TMP_Text titleNameLabel;

        private ReplayTimelineController replayTimelineController;

        void Awake()
        {
            replayTimelineController = GetComponent<ReplayTimelineController>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        public void InitializeTurnByTurnReplay(MatchReplay matchReplay)
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            StartCoroutine(replayTimelineController.SetupTimeLineData(matchReplay));
            //replayTimelineController.Play();
        }
        
        public void CloseTurnByTurnReplay()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0;

            replayTimelineController.ClearReplay();
            replayTimelineController.ActiveControllerBtns(isActive: true);
            
            //currentReplay = null;
        }

        // Update is called once per frame
        /*void Update()
        {
        
        }*/
    }
}
