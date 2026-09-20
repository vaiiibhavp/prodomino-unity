using TMPro;
using UnityEngine;

public class PlayerUI : MonoBehaviour {


    [SerializeField] private GameObject crossArrowGameObject;
    [SerializeField] private GameObject circleArrowGameObject;
    [SerializeField] private GameObject crossYouTextGameObject;
    [SerializeField] private GameObject circleYouTextGameObject;
    [SerializeField] private TextMeshProUGUI playerCrossScoreTextMesh;
    [SerializeField] private TextMeshProUGUI playerCircleScoreTextMesh;



    private void Awake() {
        crossArrowGameObject.SetActive(false);
        circleArrowGameObject.SetActive(false);
        crossYouTextGameObject.SetActive(false);
        circleYouTextGameObject.SetActive(false);

        playerCrossScoreTextMesh.text = "";
        playerCircleScoreTextMesh.text = "";
    }

    private void Start() {
        GameManagerDemo.Instance.OnGameStarted += GameManager_OnGameStarted;
        GameManagerDemo.Instance.OnCurrentPlayablePlayerTypeChanged += GameManager_OnCurrentPlayablePlayerTypeChanged;
    }

    private void GameManager_OnScoreChanged(object sender, System.EventArgs e) {
        //playerCrossScoreTextMesh.text = playerCrossScore.ToString();
        //playerCircleScoreTextMesh.text = playerCircleScore.ToString();
    }

    private void GameManager_OnCurrentPlayablePlayerTypeChanged(object sender, System.EventArgs e) {
        UpdateCurrentArrow();
    }

    private void GameManager_OnGameStarted(object sender, System.EventArgs e) {
        if (GameManagerDemo.Instance.GetLocalPlayerType() == GameManagerDemo.PlayerType.Cross) {
            crossYouTextGameObject.SetActive(true);
        } else {
            circleYouTextGameObject.SetActive(true);
        }
        playerCrossScoreTextMesh.text = "0";
        playerCircleScoreTextMesh.text = "0";

        UpdateCurrentArrow();
    }

    private void UpdateCurrentArrow() {
        if (GameManagerDemo.Instance.GetCurrentPlayablePlayerType() == GameManagerDemo.PlayerType.Cross) {
            crossArrowGameObject.SetActive(true);
            circleArrowGameObject.SetActive(false);
        } else {
            crossArrowGameObject.SetActive(false);
            circleArrowGameObject.SetActive(true);
        }
    }

}