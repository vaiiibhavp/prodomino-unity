using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using ProDomino.Shared;
using static UniqueBoolEvent;
using UnityEngine.Events;
using Unity.Services.Multiplayer;

[Obsolete("This class is deprecated and will be removed in future versions. This is a proxy that only starts the relay session calling to MatchManager. A singleton class.")]
public class NetworkManagerUIDemo : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI playerCounterTextMesh;
    [SerializeField] private TextMeshProUGUI yourIdTextMesh;
    [SerializeField] private TextMeshProUGUI joinCodeTextMesh;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject containerButtons;
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button placeTileButton;

    [SerializeField] private GameMode gameModeSelectedID = GameMode.none;
    [SerializeField] private GameType gameTypeSelectedID = GameType.none;
    [SerializeField] private NumberPlayers vsPlayerSelectedID = NumberPlayers.none;
    [SerializeField] private ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none;

    [Header("Matchmaking Events")]
    [SerializeField] private UnityEvent<string> onPlayerJoined;
    [SerializeField] private UnityEvent<string> onPlayerLeaving;
    [SerializeField] private UnityEvent onSetMatchmakingPreviousSettingsEvent;
    [SerializeField] private UnityEvent onStartMatchmakingEvent;
    [SerializeField] private UnityEvent onCancelMatchmakingEvent;

    private void Awake()
    {
        playerCounterTextMesh?.gameObject.SetActive(false);
        yourIdTextMesh?.gameObject.SetActive(false);
        joinCodeTextMesh?.gameObject.SetActive(false);
        joinCodeInputField?.gameObject.SetActive(false);
        timerText?.gameObject.SetActive(false);
        startHostButton?.gameObject.SetActive(false);
        startClientButton?.gameObject.SetActive(false);
        placeTileButton?.gameObject.SetActive(false);

        //startHostButton.onClick.AddListener(() =>
        //{
        //    MatchManager.Instance.OnCreateRelayLobbyID(CreateLobbyWithJoinCode, vsPlayerSelectedID);
        //    Hide();
        //});
        //startClientButton.onClick.AddListener(() =>
        //{
        //    JoinToLobby();
        //    //NetworkManager.Singleton.StartClient();
        //    //Hide();
        //});

        /*startHostButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            Hide();
        });
        startClientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            Hide();
        });*/
    }

    private void Start()
    {
        MatchManager.Instance.OnCliendConnected.AddListener(MatchManager_OnCliendConnected);
        MatchManager.Instance.OnGameStarted.AddListener(MatchManager_OnGameStarted);
        MatchManager.Instance.OnSetMatchmakingPreviousSettingsEvent.AddListener(MatchManager_OnSetMatchmakingPreviousSettingsEvent);
        MatchManager.Instance.OnStartMatchmakingEvent.AddListener(MatchManager_OnStartMatchmaking);
        MatchManager.Instance.OnCancelMatchmakingEvent.AddListener(MatchManager_OnCancelMatchmaking);
        MatchManager.Instance.OnCurrentPlayablePlayerChanged.AddListener(MatchManager_OnCurrentPlayablePlayerChanged);

        placeTileButton.onClick.AddListener(() =>
        {
            MatchManager.Instance.PlaceTileRpc("6/6", MatchManager.Instance.GetLocalPlayerMatchID());
            placeTileButton.gameObject.SetActive(false);
        });
    }

    private void OnDestroy()
    {
        MatchManager.Instance.OnCliendConnected.RemoveListener(MatchManager_OnCliendConnected);
        MatchManager.Instance.OnGameStarted.RemoveListener(MatchManager_OnGameStarted);
        MatchManager.Instance.OnStartMatchmakingEvent.RemoveListener(MatchManager_OnStartMatchmaking);
        MatchManager.Instance.OnCancelMatchmakingEvent.RemoveListener(MatchManager_OnCancelMatchmaking);
        MatchManager.Instance.OnCurrentPlayablePlayerChanged.RemoveListener(MatchManager_OnCurrentPlayablePlayerChanged);
    }


    //private void CreateLobbyWithJoinCode(String joinCode)
    //{
    //    /*Debug.Log("Join code: " + joinCode);
    //    //NetworkManager.Singleton.StartHost();
    //    //Hide();
    //    joinCodeTextMesh.text = "Host Join Code: " + joinCode;
    //    joinCodeTextMesh.gameObject.SetActive(true);*/
    //}

    //private void JoinToLobby()
    //{
    //    string joinCode = joinCodeInputField.text;
    //    if (Regex.IsMatch(joinCode, @"^[A-Z0-9]{6}$"))
    //    {
    //        Debug.Log("Join code: " + joinCode);
    //        //MatchManager.Instance.JoinToLobby(JoinComplete, joinCode);
    //        //Hide();
    //    }
    //    else
    //    {
    //        Debug.LogError("Invalid Join Code format. Please enter a 6-character alphanumeric code.");
    //    }
    //}

    //private void JoinComplete()
    //{
    //    Hide();
    //    //NetworkManager.Singleton.StartClient();
    //    //joinCodeTextMesh.text = "Join Code: " + joinCodeInputField.text;
    //    //joinCodeTextMesh.gameObject.SetActive(true);
    //}

    #region GameModeConfig Proxies
    /// <summary>
    /// Checks if the player is currently in a match session.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    /// <returns></returns>
    public void CheckIfIsInMatch(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = MatchManager.Instance.IsInMatch;
    }

    /// <summary>
    /// Checks if the player is currently matchmaking.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfIsMatchmaking(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = MatchManager.Instance.IsMatchmakingRef;
    }

    /// <summary>
    /// Checks if the player is currently the hosting.<br></br>
    /// Due some classes could not have a reference to MatchManager, this method is used to check if the player is in a match session.<br></br>
    /// </summary>
    public void CheckIfLocalPlayerIsHost(BoolResult result)
    {
        // Set the value on the provided result container
        result.value = MatchManager.Instance.IsHost;
    }

    /// <summary>
    /// Opens the UI and sets the game mode data based on the provided GameModeData object.<br></br>
    /// Called from the GameModeDataEvent to create or join a match session.<br></br>
    /// </summary>
    /// <param name="data"></param>
    public async void CreateOrJoinMatchSessionProxy(GameModeData data)
    {
        gameModeSelectedID = data.gameMode;
        gameTypeSelectedID = data.gameType;
        vsPlayerSelectedID = data.NumberPlayers;
        concentrateNumberOfTiles = data.concentrateNumberOfTiles;

        try
        {
            // Try to create or join a match session with the selected game mode, type, and number of players
            await MatchManager.Instance.CreateOrJoinMatchSession(new(gameModeSelectedID, gameTypeSelectedID, vsPlayerSelectedID, concentrateNumberOfTiles));
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating or joining match session: \n\n{e.Message}");
        }
    }

    /// <summary>
    /// Try to quit the current match session if the player is already in one.<br></br>
    /// Called from the GameModeDataEvent to leave a match session.<br></br>
    /// </summary>
    /// <param name="data"></param>
    public async void LeaveMatchSessionProxy()
    {
        try
        {
            // Try to quit the current match session if the player is already in one
            await MatchManager.Instance.LeaveMatch(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating or joining match session: \n\n{e.Message}");
        }
    }
    
    /// <summary>
    /// Cancel the matchmaking.<br></br>
    /// Called from the GameModeDataEvent to leave a match session.<br></br>
    /// </summary>
    /// <param name="data"></param>
    public void CancelMatchmakingProxy()
    {
        MatchManager.Instance.CancelMatchmaking();
    }
    #endregion

    #region Matchmaking Events
    /// <summary>
    /// Invokes the event before the matchmaking starts
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MatchManager_OnSetMatchmakingPreviousSettingsEvent()
    {
        onSetMatchmakingPreviousSettingsEvent?.Invoke();
    }
    
    /// <summary>
    /// Invokes the event when matchmaking starts
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MatchManager_OnStartMatchmaking()
    {
        onStartMatchmakingEvent?.Invoke();
    }

    /// <summary>
    /// Invokes the event when matchmaking is cancelled
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MatchManager_OnCancelMatchmaking()
    {
        onCancelMatchmakingEvent?.Invoke();
    }
    #endregion

    /*public void OpenAndSetGameModeData(GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID)
    {
        GameModeSelectedID = gameModeID;
        GameTypeSelectedID = gameTypeID;
        VSPlayerSelectedID = vsPlayerID;

        this.gameObject.SetActive(true);

        string auxGameID = $"{gameModeID}_{gameTypeID}_{vsPlayerID}";

        //Debug.Log("Game: " + auxGame);

        //New setup
        //MatchManager.Instance.ToLobby(CreateLobbyWithJoinCode, VSPlayerSelectedID);
        MatchManager.Instance.ToLobby(JoinComplete, auxGameID, VSPlayerSelectedID);
        //MatchManager.Instance.OnCreateRelayLobbyID(CreateLobbyWithJoinCode, VSPlayerSelectedID);
    }*/

    //public void OpenAndSetSingleVsIAData(GameModeData data)
    //{
    //    gameModeSelectedID = data.gameMode;
    //    gameTypeSelectedID = data.gameType;
    //    vsPlayerSelectedID = data.NumberPlayers;
    //    concentrateNumberOfTiles = data.concentrateNumberOfTiles;

    //    this.gameObject.SetActive(true);

    //    string auxGameID = $"{gameModeSelectedID}_{gameTypeSelectedID}_{vsPlayerSelectedID}_{concentrateNumberOfTiles}";

    //    Debug.Log("GameModeData: " + auxGameID);

    //    MatchManager.Instance.ToLobby(JoinComplete, auxGameID, vsPlayerSelectedID);
    //}

    public void CloseMenu()
    {
        gameModeSelectedID = GameMode.none;
        gameTypeSelectedID = GameType.none;
        vsPlayerSelectedID = NumberPlayers.none;
        this.gameObject.SetActive(false);
    }

    private void Hide()
    {
        containerButtons.SetActive(false);
        joinCodeInputField.gameObject.SetActive(false);
        //gameObject.SetActive(false);
    }

    private void MatchManager_OnCliendConnected()
    {
        UpdatePlayerCounterText();
    }
    
    private void MatchManager_OnPlayerConnected(string playerId)
    {
        onPlayerJoined?.Invoke(playerId);
    }
    
    private void MatchManager_OnPlayerLeaving(string playerId)
    {
        onPlayerLeaving?.Invoke(playerId);
    }

    private void MatchManager_OnGameStarted()
    {
        //playerCounterTextMesh.gameObject.SetActive(false);
        containerButtons.SetActive(false);
        playerCounterTextMesh.text = "Current turn for the player ID: " + MatchManager.Instance.GetCurrentPlayablePlayer().ToString();

        yourIdTextMesh.text = "Your Player ID Is " + MatchManager.Instance.GetLocalPlayerMatchID().ToString();
        //yourIdTextMesh.gameObject.SetActive(true);

        ValidateMyTurn();
    }

    private void ValidateMyTurn()
    {
        if (MatchManager.Instance.GetLocalPlayerMatchID() == MatchManager.Instance.GetCurrentPlayablePlayer())
        {
            //placeTileButton.gameObject.SetActive(true);
        }
        else
        {
            placeTileButton.gameObject.SetActive(false);
        }
    }

    private void UpdatePlayerCounterText()
    {
        playerCounterTextMesh.text = "Match player counter: " +
        NetworkManager.Singleton.ConnectedClientsList.Count + " / " + MatchManager.Instance.GetNumberOfPlayers().ToString();

        //if (playerCounterTextMesh.gameObject.activeSelf == false)
        //{
        //    playerCounterTextMesh.gameObject.SetActive(true);
        //}
    }

    private void MatchManager_OnCurrentPlayablePlayerChanged()
    {
        playerCounterTextMesh.text = "Current turn for the player ID: " + MatchManager.Instance.GetCurrentPlayablePlayer().ToString();
        ValidateMyTurn();
        //UpdateCurrentArrow();
        //playerCounterTextMesh.text = "Current turn for the player: " + MatchManager.Instance.GetCurrentPlayablePlayer().ToString();
    }

    #region Turn Timer
    void Update()
    {
        //Debug.Log("+++--- MatchManager.Instance.IsTurnActive: " + MatchManager.Instance.Get_IsTurnActive());
        //Debug.Log("+++--- NetworkManager.Singleton.IsConnectedClient: " + NetworkManager.Singleton.IsConnectedClient);

        //if (MatchManager.Instance == null || !MatchManager.Instance.Get_IsTurnActive() || !NetworkManager.Singleton.IsConnectedClient)
        //    return;

        ////Debug.Log("+++---Updating turn timer UI...");

        //float time = MatchManager.Instance.GetRemainingTime();
        //int seconds = Mathf.CeilToInt(time);
        
        //if (!timerText.gameObject.activeSelf)
        //{
        //    timerText.gameObject.SetActive(true);
        //}

        //timerText.text = $"Remaining time for turn: {seconds}s";
    }
    #endregion
}