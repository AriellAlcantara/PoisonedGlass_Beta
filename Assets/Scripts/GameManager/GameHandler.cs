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

            _stateMachine = GetComponent<GameStateMachine>();
            if (_stateMachine == null)
                _stateMachine = gameObject.AddComponent<GameStateMachine>();

            if (Object.HasStateAuthority)
            {
                _stateMachine.Initialize();
                InitializeTurnOrder();
            }
        }

        private void InitializeTurnOrder()
        {
            players.Clear();
            foreach (var p in Runner.ActivePlayers)
            {
                players.Add(p);
            }

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
                Debug.Log("[GameHandler] Two players detected — broadcasting start to all clients!");
                // convert to array because Fusion RPCs require serializable types (arrays are supported)
                var namesArray = new List<string>(playerUsernames.Values).ToArray();
                RPC_BroadcastGameStart(namesArray);
            }
        }

        //  Send the start event to everyone once both players are in
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BroadcastGameStart(string[] usernames)
        {
            Debug.Log("[GameHandler] Game start broadcast received — showing Game Panel.");
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowGamePanel();
                GameUIManager.Instance.UpdateAllPlayerNames(new List<string>(usernames));
            }
        }

        private void UpdateAllPlayerNamesUI()
        {
            List<string> names = new List<string>(playerUsernames.Values);
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.UpdateAllPlayerNames(names);
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
