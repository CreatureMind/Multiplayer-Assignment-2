using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utils;

namespace GameControllers {

  public class ClientGameController : MonoBehaviour, INetworkRunnerCallbacks {

    [SerializeField] private NetworkRunner _runnerPrefab;

    public string SessionName { get => _sessionName; set => _sessionName = value; }
      public string LobbyName { get => _lobbyName; set => _lobbyName = value; }

    private string _sessionName;
    private string _lobbyName;
    private NetworkRunner _instanceRunner;

    

    private enum State {
      SelectMode,
      StartClient,
      JoinLobby,
      LobbyJoined,
      Started,
    }

    private State _state;
    private List<SessionInfo> _currentSessionList;

    [Header("UI Elements")]
    public Canvas MainCanvas;

    public GameObject SelectModeUIGameObject;
    public GameObject StartClientUIGameObject;
    public GameObject JoinLobbyUIGameObject;
    public GameObject LobbyJoinedUIGamedObject;
    public GameObject StartedUIGameObject;

    public Button ShutdownRunnerButton;

    public TMP_Dropdown LobbyDropdown;
    public Button JoinSessionFromLobbyButton;

    public bool ShowGUI;

    void Awake() {
      Application.targetFrameRate = 60;

      SelectModeUIGameObject.SetActive(true);
      StartClientUIGameObject.SetActive(false);
      JoinLobbyUIGameObject.SetActive(false);
      LobbyJoinedUIGamedObject.SetActive(false);
      StartedUIGameObject.SetActive(false);

      LobbyDropdown.ClearOptions();
      LobbyDropdown.interactable = false;
      JoinSessionFromLobbyButton.interactable = false;
      ShutdownRunnerButton.gameObject.SetActive(false);
    }

    void SetState(State newState) {

      _state = newState;

      SelectModeUIGameObject.SetActive(_state == State.SelectMode);
      StartClientUIGameObject.SetActive(_state == State.StartClient);
      JoinLobbyUIGameObject.SetActive(_state == State.JoinLobby);
      LobbyJoinedUIGamedObject.SetActive(_state == State.LobbyJoined);
      StartedUIGameObject.SetActive(_state == State.Started);

      ShutdownRunnerButton.gameObject.SetActive(_state != State.SelectMode);
    }

    void Update() {
      bool isRunnerCloudReady = _instanceRunner != null && _instanceRunner.IsCloudReady;
      ShutdownRunnerButton.interactable = isRunnerCloudReady;
    }

    void OnGUI() {
      if (!ShowGUI)
        return;
      
      Rect area = new Rect(10, 90, Screen.width - 20, Screen.height - 100);

      GUILayout.BeginArea(area);

      switch (_state) {
        case State.SelectMode: State_SelectMode(); break;
        case State.StartClient: State_StartClient(); break;
        case State.JoinLobby: State_JoinLobby(); break;
        case State.LobbyJoined: State_LobbyJoined(); break;
        case State.Started: State_Started(); break;
      }

      if (_instanceRunner != null && _instanceRunner.IsCloudReady) {
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        {
          GUILayout.FlexibleSpace();

          if (GUILayout.Button("Shutdown", GUILayout.ExpandWidth(false), GUILayout.MinHeight(50), GUILayout.MinWidth(200))) {

            _instanceRunner.Shutdown();
          }
        }
        GUILayout.EndHorizontal();
      }

      GUILayout.EndArea();
    }

    public void ShutdownRunner() {
      if (_instanceRunner != null && _instanceRunner.IsCloudReady) {
        _instanceRunner.Shutdown();
      }

      MainCanvas.enabled = !ShowGUI;
    }

    void State_SelectMode() {
      GUILayout.BeginHorizontal();
      GUILayout.Label("Session Name:", GUILayout.ExpandWidth(false));
      _sessionName = GUILayout.TextField(_sessionName)?.Trim();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.Label("Custom Lobby:", GUILayout.ExpandWidth(false));
      _lobbyName = GUILayout.TextField(_lobbyName)?.Trim();
      GUILayout.EndHorizontal();

      if (ExpandButton("Client")) {
        //_currentState = State.StartClient;
      }

      if (ExpandButton("Join Lobby")) {
        //_currentState = State.JoinLobby;
      }
    }

    public void OnClientClicked() {
      SetState(State.StartClient);
      State_StartClient();
    }

    public void OnJoinLobbyClicked() {
      SetState(State.JoinLobby);
      State_JoinLobby();
    }

    async void State_StartClient() {
      _instanceRunner = GetRunner("Client");

      SetState(State.Started);

      var result = await StartSimulation(_instanceRunner, GameMode.Client, _sessionName);

      if (result.Ok == false) {
        Debug.LogWarning(result.ShutdownReason);

        SetState(State.SelectMode);
      } else {
        Debug.Log("Done");
      }
    }

    async void State_JoinLobby() {
      _instanceRunner = GetRunner("Client");

      SetState(State.LobbyJoined);

      var result = await JoinLobby(_instanceRunner);

      if (result.Ok == false) {
        Debug.LogWarning(result.ShutdownReason);

        SetState(State.SelectMode);
      } else {
        Debug.Log("Done");
      }
    }

    void State_Started() { }

    void State_LobbyJoined() {

      if (_currentSessionList != null && _currentSessionList.Count > 0) {
        GUILayout.BeginVertical();

        foreach (var session in _currentSessionList.ToArray()) {

          GUILayout.BeginHorizontal();

          var props = "";
          foreach (var item in session.Properties) {
            props += $"{item.Key}={item.Value.PropertyValue}, ";
          }

          GUILayout.Label($"Session: {session.Name} ({props})");

          if (GUILayout.Button("Join", GUILayout.ExpandWidth(false), GUILayout.MinWidth(200))) {

            StartSimulation(_instanceRunner, GameMode.Client, session.Name);

            SetState(State.Started);
          }

          GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
      }
    }

    public void JoinSession() {

      var options = LobbyDropdown.options;
      int value = LobbyDropdown.value;
      if (value < 0) {
        LobbyDropdown.value = 0;
        value = 0;
      }

      StartSimulation(_instanceRunner, GameMode.Client, options[value].text);

      SetState(State.Started);
    }


    private NetworkRunner GetRunner(string name) {

      var runner = Instantiate(_runnerPrefab);
      runner.name = name;
      runner.ProvideInput = true;
      runner.AddCallbacks(this);

      return runner;
    }

    public Task<StartGameResult> StartSimulation(
        NetworkRunner runner,
        GameMode gameMode,
        string sessionName
      ) {

      return runner.StartGame(new StartGameArgs() {
        SessionName = sessionName,
        GameMode = gameMode,
        SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
        Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
        EnableClientSessionCreation = false,
      });
    }

    public Task<StartGameResult> JoinLobby(NetworkRunner runner) {
      return runner.JoinSessionLobby(string.IsNullOrEmpty(_lobbyName) ? SessionLobby.ClientServer : SessionLobby.Custom, _lobbyName);
    }

    bool ExpandButton(string text) {
      return GUILayout.Button(text, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }

    // ------------ RUNNER CALLBACKS ------------------------------------------------------------------------------------

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {

      _currentSessionList = null;
      SetState(State.SelectMode);

      // Reload scene after shutdown

      if (Application.isPlaying) {
        SceneManager.LoadScene((byte)SceneDefs.MENU);
      }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {
      runner.Shutdown();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {
      runner.Shutdown();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {

      Log.Debug($"Received: {sessionList.Count}");

      _currentSessionList = sessionList;

      List<string> sessions = new List<string>();


      for (int i = 0; i < _currentSessionList.Count; i++) {
        sessions.Add(_currentSessionList[i].Name);
      }

      LobbyDropdown.interactable = sessions.Count > 0;
      JoinSessionFromLobbyButton.interactable = LobbyDropdown.interactable;

      LobbyDropdown.AddOptions(sessions);
    }

    #region Other callbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {
    }
    #endregion
  }
}