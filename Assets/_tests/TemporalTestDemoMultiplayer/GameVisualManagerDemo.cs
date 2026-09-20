using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameVisualManager : NetworkBehaviour {


    private const float GRID_SIZE = 3.1f;


    [SerializeField] private Transform crossPrefab;
    [SerializeField] private Transform circlePrefab;
    [SerializeField] private Transform lineCompletePrefab;


    private List<GameObject> visualGameObjectList;


    private void Awake() {
        visualGameObjectList = new List<GameObject>();
    }

    private void Start() {
        GameManagerDemo.Instance.OnClickedOnGridPosition += GameManager_OnClickedOnGridPosition;
    }

    private void GameManager_OnClickedOnGridPosition(object sender, GameManagerDemo.OnClickedOnGridPositionEventArgs e) {
        Debug.Log("GameManager_OnClickedOnGridPosition");
        SpawnObjectRpc(e.x, e.y, e.playerType);
    }

    [Rpc(SendTo.Server)]
    private void SpawnObjectRpc(int x, int y, GameManagerDemo.PlayerType playerType) {
        Debug.Log("SpawnObject");
        Transform prefab;
        switch (playerType) {
            default:
            case GameManagerDemo.PlayerType.Cross:
                prefab = crossPrefab;
                break;
            case GameManagerDemo.PlayerType.Circle:
                prefab = circlePrefab;
                break;
        }
        Transform spawnedCrossTransform = Instantiate(prefab, GetGridWorldPosition(x, y), Quaternion.identity);
        spawnedCrossTransform.GetComponent<NetworkObject>().Spawn(true);

        visualGameObjectList.Add(spawnedCrossTransform.gameObject);
    }

    private Vector2 GetGridWorldPosition(int x, int y) {
        return new Vector2(-GRID_SIZE + x * GRID_SIZE, -GRID_SIZE + y * GRID_SIZE);
    }

}