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
        private List<PlayerRef> players = new();
        private PlayerRef currentPlayer;
        private PlayerRef otherPlayer;
        private System.Random random = new System.Random();
        private Dictionary<PlayerRef, string> playerUsernames = new();

        // NEW: authoritative HP and deck state
        private readonly Dictionary<int, int> _hpById = new(); // PlayerId -> HP
        private int _drinksLeft;
        private int _poisonedLeft;
        private const int TotalDrinksInBatch = 6;
        private const int PoisonedDrinksInBatch = 2;

        private bool _authorityInitialized = false;

        public override void Spawned()
        {
            base.Spawned();
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

            _stateMachine = GetComponent<GameStateMachine>() ?? gameObject.AddComponent<GameStateMachine>();

            EventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);

            if (Object.HasStateAuthority)
            {
                if (players.Count == 0 && Runner != null)
                {
                    foreach (var p in Runner.ActivePlayers)
                    {
                        if (!players.Contains(p))
                            players.Add(p);
                    }
                }

                TryInitializeAuthority();
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        private void OnPlayerJoined(PlayerJoinedEvent evt)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!players.Contains(evt.Player))
            {
                players.Add(evt.Player);
                Debug.Log($"[GameHandler] OnPlayerJoined added: {evt.Player}. players.Count={players.Count}");
            }

            InitializeTurnOrderFromTrackedPlayers();
            TryInitializeAuthority();
        }

        private void OnPlayerLeft(PlayerLeftEvent evt)
        {
            if (!Object.HasStateAuthority)
                return;

            if (players.Contains(evt.Player))
            {
                players.Remove(evt.Player);
                Debug.Log($"[GameHandler] OnPlayerLeft removed: {evt.Player}. players.Count={players.Count}");
            }

            if (players.Count < 2 && _authorityInitialized)
            {
                _authorityInitialized = false;
            }

            InitializeTurnOrderFromTrackedPlayers();
        }

        // Initialize and randomize, plus seed HP/deck
        private void TryInitializeAuthority()
        {
            if (!Object.HasStateAuthority || _authorityInitialized) return;

            if (players.Count >= 2)
            {
                // Randomize who starts
                int idx = UnityEngine.Random.Range(0, 2);
                currentPlayer = players[idx];
                otherPlayer = players[1 - idx];

                // Seed HP and deck on authority
                _hpById[currentPlayer.PlayerId] = 5;
                _hpById[otherPlayer.PlayerId] = 5;
                ResetBatch();

                _stateMachine.Initialize();
                _authorityInitialized = true;

                // keep your existing game-start UI flow
                string[] namesArray = playerUsernames.Count >= 2
                    ? new List<string>(playerUsernames.Values).ToArray()
                    : players.Select(p => $"Player{p.PlayerId}").ToArray();

                // Broadcast full snapshot with starter and state
                RPC_BroadcastGameStartWithStarter(
                    namesArray,
                    currentPlayer.PlayerId,
                    currentPlayer.PlayerId, _hpById[currentPlayer.PlayerId],
                    otherPlayer.PlayerId, _hpById[otherPlayer.PlayerId],
                    _drinksLeft, _poisonedLeft
                );

                // tell clients who has the turn (no UI deletion needed)
                RPC_BroadcastTurn(currentPlayer.PlayerId);
            }
            else
            {
                Debug.Log($"[GameHandler] TryInitializeAuthority deferred - not enough tracked players (count={players.Count}).");
            }
        }

        private void InitializeTurnOrderFromTrackedPlayers()
        {
            if (players.Count >= 2)
            {
                currentPlayer = players[0];
                otherPlayer = players[1];
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SendUsernameToServer(string username, PlayerRef player)
        {
            if (!playerUsernames.ContainsKey(player))
                playerUsernames.Add(player, username);
            else
                playerUsernames[player] = username;

            UpdateAllPlayerNamesUI();

            if (playerUsernames.Count >= 2)
            {
                if (!_authorityInitialized)
                {
                    TryInitializeAuthority();
                }
                else
                {
                    // If a client connected late to names, resend current snapshot so both sides are in sync
                    RPC_BroadcastGameStartWithStarter(BuildUsernamesArray(), currentPlayer.PlayerId,
                        currentPlayer.PlayerId, _hpById.GetValueOrDefault(currentPlayer.PlayerId, 5),
                        otherPlayer.PlayerId, _hpById.GetValueOrDefault(otherPlayer.PlayerId, 5),
                        _drinksLeft, _poisonedLeft);
                    RPC_BroadcastTurn(currentPlayer.PlayerId);
                }
            }
        }

        private void UpdateAllPlayerNamesUI()
        {
            List<string> names = new List<string>(playerUsernames.Values);
            if (GameUIManager.Instance != null)
                GameUIManager.Instance.UpdateAllPlayerNames(names);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastGameStartWithStarter(string[] usernames, int startingPlayerId,
            int p1Id, int p1Hp, int p2Id, int p2Hp, int drinksLeft, int poisonedLeft)
        {
            StartCoroutine(WaitAndShowGamePanelWithStarter(usernames, startingPlayerId,
                p1Id, p1Hp, p2Id, p2Hp, drinksLeft, poisonedLeft));
        }

        private IEnumerator WaitAndShowGamePanelWithStarter(string[] usernames, int startingPlayerId,
            int p1Id, int p1Hp, int p2Id, int p2Hp, int drinksLeft, int poisonedLeft)
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
                        GameUIManager.Instance.ShowGamePanel();
                        GameUIManager.Instance.UpdateAllPlayerNames(new List<string>(usernames));
                        GameUIManager.Instance.SyncServerState(startingPlayerId,
                            p1Id, p1Hp, p2Id, p2Hp, drinksLeft, poisonedLeft);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[GameHandler] Exception while showing game panel: {ex}");
                    }
                    yield break;
                }

                yield return new WaitForSeconds(poll);
                elapsed += poll;
            }

            Debug.LogWarning("[GameHandler] Timed out waiting for GameUIManager.Instance — UI not shown.");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastTurn(int currentPlayerId)
        {
            if (GameUIManager.Instance != null)
                GameUIManager.Instance.SetTurnPlayer(currentPlayerId);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastResolution(int actorId, int selection, bool poisoned, int nextTurnId,
            int p1Id, int p1Hp, int p2Id, int p2Hp, int drinksLeft, int poisonedLeft)
        {
            if (GameUIManager.Instance != null)
                GameUIManager.Instance.ApplyServerResolution(actorId, selection, poisoned, nextTurnId,
                    p1Id, p1Hp, p2Id, p2Hp, drinksLeft, poisonedLeft);
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
            // Authority draws and applies HP
            bool poisoned = ServerDrawDrink();
            var target = (choice == 0) ? player : otherPlayer;
            if (poisoned)
            {
                int tId = target.PlayerId;
                _hpById[tId] = Mathf.Max(0, _hpById.GetValueOrDefault(tId, 5) - 1);
            }

            // Check lose conditions
            if (_hpById.GetValueOrDefault(player.PlayerId, 5) <= 0)
            {
                PlayerLose(player);
                return;
            }
            if (_hpById.GetValueOrDefault(otherPlayer.PlayerId, 5) <= 0)
            {
                PlayerLose(otherPlayer);
                return;
            }

            // Next turn: alternate
            SwapTurns();

            // Broadcast authoritative resolution snapshot
            RPC_BroadcastResolution(
                player.PlayerId, choice, poisoned, currentPlayer.PlayerId,
                currentPlayer.PlayerId, _hpById.GetValueOrDefault(currentPlayer.PlayerId, 5),
                otherPlayer.PlayerId, _hpById.GetValueOrDefault(otherPlayer.PlayerId, 5),
                _drinksLeft, _poisonedLeft
            );

            RPC_BroadcastTurn(currentPlayer.PlayerId);
        }

        private void SwapTurns()
        {
            var temp = currentPlayer;
            currentPlayer = otherPlayer;
            otherPlayer = temp;
        }

        private void ResetBatch()
        {
            _drinksLeft = TotalDrinksInBatch;
            _poisonedLeft = PoisonedDrinksInBatch;
        }

        // Same logic as UI DrawDrink but authoritative and deterministic for all clients
        private bool ServerDrawDrink()
        {
            if (_drinksLeft <= 0)
                ResetBatch();

            _drinksLeft--;

            bool poisoned = false;
            if (_poisonedLeft > 0)
            {
                float chance = (float)_poisonedLeft / (_drinksLeft + 1);
                if (UnityEngine.Random.value < chance)
                {
                    poisoned = true;
                    _poisonedLeft--;
                }
            }

            return poisoned;
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

        private string[] BuildUsernamesArray()
        {
            return playerUsernames.Count >= 2
                ? new List<string>(playerUsernames.Values).ToArray()
                : players.Select(p => $"Player{p.PlayerId}").ToArray();
        }
    }
}
