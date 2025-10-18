using UnityEngine;
using Fusion;
using GNW2.Events;
using System.Collections.Generic;

namespace GNW2.GameManager
{
    /// <summary>
    /// Tracks player scores across rounds using the event bus pattern.
    /// Demonstrates how to add features without modifying existing systems.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        /// <summary>
        /// Data structure to track individual player statistics
        /// </summary>
        [System.Serializable]
        public class PlayerScore
        {
            public int Wins;
            public int Losses;
            public int Draws;

            public int TotalGames => Wins + Losses + Draws;
            public float WinRate => TotalGames > 0 ? (float)Wins / TotalGames : 0f;
        }

        // Track scores for all players
        private Dictionary<PlayerRef, PlayerScore> _playerScores = new Dictionary<PlayerRef, PlayerScore>();

        private void OnEnable()
        {
            // Subscribe to relevant events
            EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
            EventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        private void OnDisable()
        {
            // Always unsubscribe to prevent memory leaks
            EventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
            EventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
            EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
        }

        /// <summary>
        /// Initialize score tracking for new players
        /// </summary>
        private void OnPlayerJoined(PlayerJoinedEvent evt)
        {
            if (!_playerScores.ContainsKey(evt.Player))
            {
                _playerScores[evt.Player] = new PlayerScore();
                Debug.Log($"[ScoreManager] Tracking started for Player {evt.Player.PlayerId}");
            }
        }

        /// <summary>
        /// Clean up score tracking for disconnected players
        /// </summary>
        private void OnPlayerLeft(PlayerLeftEvent evt)
        {
            if (_playerScores.ContainsKey(evt.Player))
            {
                var score = _playerScores[evt.Player];
                Debug.Log($"[ScoreManager] Player {evt.Player.PlayerId} final stats - " +
                         $"W:{score.Wins} L:{score.Losses} D:{score.Draws}");
                _playerScores.Remove(evt.Player);
            }
        }

        /// <summary>
        /// Update scores when a round ends
        /// Publishes ScoreUpdatedEvent for UI to display
        /// </summary>
        private void OnRoundEnded(RoundEndedEvent evt)
        {
            if (evt.IsDraw)
            {
                // Both players get a draw
                foreach (var kvp in _playerScores)
                {
                    kvp.Value.Draws++;
                    PublishScoreUpdate(kvp.Key, kvp.Value);
                }
                Debug.Log("[ScoreManager] Round ended in draw");
            }
            else
            {
                // Winner gets a win
                if (_playerScores.ContainsKey(evt.Winner))
                {
                    _playerScores[evt.Winner].Wins++;
                    PublishScoreUpdate(evt.Winner, _playerScores[evt.Winner]);
                    Debug.Log($"[ScoreManager] Player {evt.Winner.PlayerId} wins!");
                }

                // Everyone else gets a loss
                foreach (var kvp in _playerScores)
                {
                    if (kvp.Key != evt.Winner)
                    {
                        kvp.Value.Losses++;
                        PublishScoreUpdate(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Publish score update event for UI systems to consume
        /// </summary>
        private void PublishScoreUpdate(PlayerRef player, PlayerScore score)
        {
            EventBus.Publish(new ScoreUpdatedEvent
            {
                Player = player,
                Wins = score.Wins,
                Losses = score.Losses,
                Draws = score.Draws
            });
        }

        /// <summary>
        /// Public API to get a player's current score
        /// </summary>
        public PlayerScore GetPlayerScore(PlayerRef player)
        {
            return _playerScores.ContainsKey(player) ? _playerScores[player] : null;
        }

        /// <summary>
        /// Public API to get all scores (for leaderboard, etc.)
        /// </summary>
        public Dictionary<PlayerRef, PlayerScore> GetAllScores()
        {
            return new Dictionary<PlayerRef, PlayerScore>(_playerScores);
        }

        /// <summary>
        /// Reset all scores (for new tournament, etc.)
        /// </summary>
        public void ResetAllScores()
        {
            foreach (var kvp in _playerScores)
            {
                kvp.Value.Wins = 0;
                kvp.Value.Losses = 0;
                kvp.Value.Draws = 0;
                PublishScoreUpdate(kvp.Key, kvp.Value);
            }
            Debug.Log("[ScoreManager] All scores reset");
        }
    }
}
