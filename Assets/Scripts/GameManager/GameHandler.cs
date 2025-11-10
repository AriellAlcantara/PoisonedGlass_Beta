using System.Linq;
using System.Collections;
using Fusion;
using GNW2.Events;
using GNW2.GameManager;
using System.Collections.Generic;
using UnityEngine;

namespace GNW2.UI
{
    public class GameHandler : NetworkBehaviour
    {
        public static GameHandler Instance;

        private GameStateMachine _stateMachine;
        // track players locally using events (avoids relying on Runner.ActivePlayers race)
        private List<PlayerRef> players = new();
        private PlayerRef currentPlayer;
        private PlayerRef otherPlayer;
        private System.Random random = new System.Random();
        private Dictionary<PlayerRef, string> playerUsernames = new();

        // Guard so authoritative initialization runs exactly once
        private bool _authorityInitialized = false;

        public override void Spawned()
        {
            base.Spawned();
            Debug.Log($"[GameHandler] Spawned. Object.HasStateAuthority={Object.HasStateAuthority}, RunnerPresent={(Runner!=null)}");

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Runner.Despawn(Object);
                return;
            }

            _stateMachine = GetComponent<GameStateMachine>();
            if (_stateMachine == null)
                _stateMachine = gameObject.AddComponent<GameStateMachine>();

            // Subscribe to player-joined/left events so we react as soon as players arrive/leave
            EventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);

            // If we already have enough players in Runner (e.g. host case), attempt initialization immediately
            if (Object.HasStateAuthority)
            {
                // If GameManager has already tracked players we will use that; otherwise attempt to seed players list from Runner
                if (players.Count == 0)
                {
                    // seed players list from Runner.ActivePlayers if available
                    if (Runner != null)
                    {
                        foreach (var p in Runner.ActivePlayers)
                        {
                            if (!players.Contains(p))
                                players.Add(p);
                        }
                    }
                }

                TryInitializeAuthority();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe to avoid dangling handlers if object is destroyed
            EventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        // Handler invoked when a player joins (published by GameManager)
        private void OnPlayerJoined(PlayerJoinedEvent evt)
        {
            // Only the authoritative instance should manage turn order/initialization
            if (!Object.HasStateAuthority)
                return;

            // Maintain our own players list based on events to avoid Runner.ActivePlayers race
            if (!players.Contains(evt.Player))
            {
                players.Add(evt.Player);
                Debug.Log($"[GameHandler] OnPlayerJoined event received and player added: {evt.Player}. players.Count={players.Count}");
            }
            else
            {
                Debug.Log($"[GameHandler] OnPlayerJoined event received but player already tracked: {evt.Player}");
            }

            // Rebuild turn order & try init
            InitializeTurnOrderFromTrackedPlayers();
            TryInitializeAuthority();
        }

        // Handler invoked when a player leaves (published by GameManager)
        private void OnPlayerLeft(PlayerLeftEvent evt)
        {
            if (!Object.HasStateAuthority)
                return;

            if (players.Contains(evt.Player))
            {
                players.Remove(evt.Player);
                Debug.Log($"[GameHandler] OnPlayerLeft event received and player removed: {evt.Player}. players.Count={players.Count}");
            }
            else
            {
                Debug.Log($"[GameHandler] OnPlayerLeft event received for unknown player: {evt.Player}");
            }

            // If players drop below required threshold, allow re-initialization later
            if (players.Count < 2)
            {
                if (_authorityInitialized)
                {
                    Debug.Log("[GameHandler] Less than 2 players — resetting authority-initialized flag so future joins re-init.");
                    _authorityInitialized = false;
                }
            }

            InitializeTurnOrderFromTrackedPlayers();
        }

        // Attempt authoritative initialization (runs only once while at least 2 players present)
        private void TryInitializeAuthority()
        {
            if (!Object.HasStateAuthority || _authorityInitialized)
                return;

            // Use tracked players list for decision
            if (players.Count >= 2)
            {
                _stateMachine.Initialize();
                _authorityInitialized = true;
                Debug.Log("[GameHandler] StateAuthority initialized state machine and turn order (via subscribe/tracked players).");

                // Broadcast game start now even if usernames haven't arrived.
                // Build names array: prefer submitted usernames, otherwise use placeholders "Player<id>".
                string[] namesArray;
                if (playerUsernames.Count >= 2)
                {
                    namesArray = new List<string>(playerUsernames.Values).ToArray();
                }
                else
                {
                    namesArray = players.Select(p => $"Player{p.PlayerId}").ToArray();
                }

                Debug.Log($"[GameHandler] Broadcasting game start to clients (names count={namesArray.Length}).");
                RPC_BroadcastGameStart(namesArray);
            }
            else
            {
                Debug.Log($"[GameHandler] TryInitializeAuthority deferred - not enough tracked players (count={players.Count}).");
            }
        }

        // Initialize turn order using the tracked players list (preferred) or fall back to Runner.ActivePlayers
        private void InitializeTurnOrderFromTrackedPlayers()
        {
            Debug.Log("[GameHandler] Initializing turn order from tracked players...");
            // Prefer tracked players (event-driven)
            var sourceList = players;

            // If tracked list is empty, fall back to Runner.ActivePlayers snapshot
            if (sourceList.Count == 0 && Runner != null)
            {
                Debug.Log("[GameHandler] Tracked players empty, falling back to Runner.ActivePlayers.");
                foreach (var p in Runner.ActivePlayers)
                {
                    if (!players.Contains(p))
                        players.Add(p);
                }
                sourceList = players;
            }

            // Assign turn order from sourceList
            if (sourceList.Count >= 2)
            {
                currentPlayer = sourceList[0];
                otherPlayer = sourceList[1];
                Debug.Log($"[GameHandler] Turn order set. currentPlayer={currentPlayer}, otherPlayer={otherPlayer}");
            }
            else
            {
                Debug.Log($"[GameHandler] Not enough players for turn order (count={sourceList.Count}).");
            }
        }

        // Deprecated compatibility method kept for logs/diagnostics (not used for decision making)
        private void InitializeTurnOrder()
        {
            Debug.Log("[GameHandler] (compat) Initializing turn order from Runner...");
            players.Clear();

            if (Runner == null)
            {
                Debug.LogWarning("[GameHandler] Runner is null when initializing turn order.");
            }
            else
            {
                Debug.Log($"[GameHandler] Runner.ActivePlayers count: {Runner.ActivePlayers.Count()}");
                foreach (var p in Runner.ActivePlayers)
                {
                    Debug.Log($"[GameHandler] ActivePlayer found: {p}");
                    players.Add(p);
                }
            }

            if (players.Count >= 2)
            {
                currentPlayer = players[0];
                otherPlayer = players[1];
                Debug.Log($"[GameHandler] Turn order set. currentPlayer={currentPlayer}, otherPlayer={otherPlayer}");
            }
            else
            {
                Debug.Log($"[GameHandler] Not enough players for turn order (count={players.Count}).");
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SendUsernameToServer(string username, PlayerRef player)
        {
            Debug.Log($"[GameHandler] RPC_SendUsernameToServer called for player={player} username='{username}' on StateAuthority. Known usernames before: {playerUsernames.Count}");

            if (!playerUsernames.ContainsKey(player))
            {
                playerUsernames.Add(player, username);
                Debug.Log($"[GameHandler] Added username for player {player}: '{username}'");
            }
            else
            {
                playerUsernames[player] = username;
                Debug.Log($"[GameHandler] Updated username for player {player}: '{username}'");
            }

            Debug.Log($"[GameHandler] playerUsernames count now: {playerUsernames.Count}");
            UpdateAllPlayerNamesUI();

            if (playerUsernames.Count >= 2)
            {
                Debug.Log("[GameHandler] Two players detected - broadcasting start to all clients!");
                var namesArray = new List<string>(playerUsernames.Values).ToArray();
                RPC_BroadcastGameStart(namesArray);
            }
        }

        //  Send the start event to everyone once both players are in
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastGameStart(string[] usernames)
        {
            Debug.Log("[GameHandler] Game start broadcast received - scheduling Game Panel show on client.");
            // Use coroutine to wait for GameUIManager.Instance to exist (defensive against race)
            StartCoroutine(WaitAndShowGamePanel(usernames));
        }

        // Coroutine runs on clients (and also on server if it receives the RPC)
        private IEnumerator WaitAndShowGamePanel(string[] usernames)
        {
            const float timeout = 5f;
            const float poll = 0.1f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (GameUIManager.Instance != null)
                {
                    try
                    {
                        Debug.Log("[GameHandler] GameUIManager.Instance found - showing game panel now.");
                        GameUIManager.Instance.ShowGamePanel();
                        GameUIManager.Instance.UpdateAllPlayerNames(new List<string>(usernames));
                        Debug.Log("[GameHandler] ShowGamePanel and UpdateAllPlayerNames called.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[GameHandler] Exception while showing game panel: {ex}");
                    }
                    yield break;
                }

                if (elapsed == 0f)
                    Debug.Log("[GameHandler] Waiting for GameUIManager.Instance to be available on client...");

                yield return new WaitForSeconds(poll);
                elapsed += poll;
            }

            Debug.LogWarning("[GameHandler] Timed out waiting for GameUIManager.Instance — UI not shown.");
        }

        private void UpdateAllPlayerNamesUI()
        {
            List<string> names = new List<string>(playerUsernames.Values);
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.UpdateAllPlayerNames(names);
                Debug.Log($"[GameHandler] Updated UI player list with {names.Count} names.");
            }
            else
            {
                Debug.LogWarning("[GameHandler] GameUIManager.Instance null when trying to update player names UI.");
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SendTurn(int type, PlayerRef player)
        {
            if (player != currentPlayer) return;
            HandleTurn(player, type);
        }

        public void SendPlayerSelection(int selection)
        {
            if (Runner != null && Runner.LocalPlayer.IsRealPlayer)
                RPC_SendTurn(selection, Runner.LocalPlayer);
        }

        private void HandleTurn(PlayerRef player, int choice)
        {
            bool poisoned = random.NextDouble() < 0.25;
            Debug.Log($"[GameHandler] Player {player} chose {(choice == 0 ? "Drink" : "Make Other Drink")} | Poisoned: {poisoned}");

            if (choice == 0)
            {
                if (poisoned) PlayerLose(player);
            }
            else if (choice == 1)
            {
                if (poisoned) PlayerLose(otherPlayer);
                else SwapTurns();
            }
        }

        private void SwapTurns()
        {
            var temp = currentPlayer;
            currentPlayer = otherPlayer;
            otherPlayer = temp;
        }

        private void PlayerLose(PlayerRef loser)
        {
            var winner = (loser == currentPlayer) ? otherPlayer : currentPlayer;
            _stateMachine.RPC_ShowLoseUI(loser);
            _stateMachine.RPC_ShowWinUI(winner);
            RPC_BroadcastRoundEnded(winner, false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastRoundEnded(PlayerRef winner, NetworkBool isDraw)
        {
            EventBus.Publish(new RoundEndedEvent
            {
                Winner = winner,
                IsDraw = isDraw
            });
        }

        public void SendUsernameToServer(string username)
        {
            if (Runner != null)
                RPC_SendUsernameToServer(username, Runner.LocalPlayer);
            Debug.Log($"[SERVER] Received username: {username}");
        }
    }
}
