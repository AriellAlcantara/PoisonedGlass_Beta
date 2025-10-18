using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using GNW2.Input;
using GNW2.Events;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GNW2.GameManager
{
    /// <summary>
    /// Main game manager responsible for:
    /// - Managing Photon Fusion networking
    /// - Handling player connections/disconnections
    /// - Processing network input
    /// - Spawning/despawning players
    /// </summary>
    public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static GameManager Instance;

        private NetworkRunner _runner;

        [Header("Game Settings")]
        [SerializeField] private GameMode _currentGameMode;
        [SerializeField] private NetworkPrefabRef _playerPrefab;
        [SerializeField] private bool _autoStartServer = false;

        [Header("UI References")]
        [SerializeField] private Button _button;
        [SerializeField] private TMP_InputField _input;

        // Dictionary tracking all active players in the session
        private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

        // Input tracking
        private bool _isMouseButton0Pressed;

        /// <summary>
        /// Public accessor for active players
        /// </summary>
        public Dictionary<PlayerRef, NetworkObject> activePlayers => _spawnedPlayers;

        /// <summary>
        /// Initialize singleton instance and setup UI
        /// </summary>
        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            // Auto start server if enabled
            if (_autoStartServer && _currentGameMode == GameMode.Server)
            {
                StartGame(_currentGameMode);
                // Hide UI when auto-starting
                if (_button != null && _button.transform.parent != null)
                {
                    _button.transform.parent.gameObject.SetActive(false);
                }
            }
            else
            {
                // Setup start game button for manual start
                if (_button != null)
                {
                    _button.onClick.AddListener(() =>
                    {
                        StartGame(_currentGameMode);
                        _button.transform.parent.gameObject.SetActive(false);
                    });
                }
            }
        }

        #region NetworkRunner Callbacks

        // ===== Area of Interest Callbacks (not used in this project) =====
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        /// <summary>
        /// Called when a new player joins the session
        /// Spawns a player object and assigns input authority
        /// Publishes PlayerJoinedEvent to notify other systems
        /// </summary>
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                // Spawn player at offset position based on player count
                Vector3 spawnPosition = new Vector3(runner.SessionInfo.PlayerCount, 0, 0);
                NetworkObject playerNetworkObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity);

                // Give this player control over their object
                playerNetworkObject.AssignInputAuthority(player);

                // Track spawned player
                _spawnedPlayers.Add(player, playerNetworkObject);

                // Publish event for other systems to react
                EventBus.Publish(new PlayerJoinedEvent
                {
                    Player = player,
                    PlayerObject = playerNetworkObject
                });

                Debug.Log($"[GameManager] Player {player.PlayerId} joined. Total players: {_spawnedPlayers.Count}");
            }
        }

        /// <summary>
        /// Called when a player leaves the session
        /// Despawns their player object and publishes PlayerLeftEvent
        /// </summary>
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerNetworkObject))
            {
                // Despawn and remove from tracking
                runner.Despawn(playerNetworkObject);
                _spawnedPlayers.Remove(player);

                // Publish event
                EventBus.Publish(new PlayerLeftEvent
                {
                    Player = player
                });

                Debug.Log($"[GameManager] Player {player.PlayerId} left. Total players: {_spawnedPlayers.Count}");
            }
        }

        /// <summary>
        /// Called by Fusion every tick to gather input from this client
        /// Converts Unity input into networked input data
        /// </summary>
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            // WASD movement input
            if (UnityEngine.Input.GetKey(KeyCode.W))
            {
                data.Direction += Vector3.forward;
            }
            if (UnityEngine.Input.GetKey(KeyCode.A))
            {
                data.Direction += Vector3.left;
            }
            if (UnityEngine.Input.GetKey(KeyCode.S))
            {
                data.Direction += Vector3.back;
            }
            if (UnityEngine.Input.GetKey(KeyCode.D))
            {
                data.Direction += Vector3.right;
            }

            // Mouse button for shooting
            data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _isMouseButton0Pressed);

            // Send input to Fusion
            input.Set(data);
        }

        // ===== Unused Network Callbacks (kept for INetworkRunnerCallbacks interface) =====
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[GameManager] Connected to server");
            EventBus.Publish(new NetworkConnectedEvent { IsHost = false });
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[GameManager] Disconnected from server: {reason}");
            EventBus.Publish(new NetworkDisconnectedEvent());
        }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }

        #endregion

        /// <summary>
        /// Update loop to track mouse input
        /// Mouse input is polled here and used in OnInput callback
        /// </summary>
        private void Update()
        {
            _isMouseButton0Pressed = UnityEngine.Input.GetMouseButton(0);
        }

        /// <summary>
        /// Initializes and starts a Fusion networking session
        /// Sets up the NetworkRunner with the specified game mode
        /// </summary>
        /// <param name="mode">Host, Client, or other Fusion GameMode</param>
        async void StartGame(GameMode mode)
        {
            // Create and configure NetworkRunner
            _runner = this.gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true; // This client will provide input

            // Setup scene management
            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (scene.IsValid)
            {
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
            }

            // Start the Fusion session
            await _runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = "TestRoom",
                Scene = scene,
                PlayerCount = 2,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            Debug.Log($"[GameManager] Started game in {mode} mode");

            // Publish connection event
            bool isHost = mode == GameMode.Host || mode == GameMode.Server;
            EventBus.Publish(new NetworkConnectedEvent { IsHost = isHost });
        }

        /// <summary>
        /// Legacy debug GUI - kept for reference but commented out
        /// </summary>
        private void OnGUI()
        {
            // Original host/client buttons - now handled by UI button
            /*
            if (_runner == null)
            {
                if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
                {
                    StartGame(GameMode.Host);
                }
                if (GUI.Button(new Rect(0, 40, 200, 40), "Client"))
                {
                    StartGame(GameMode.Client);
                }
            }
            */
        }
    }
}
