using UnityEngine;
using Fusion;
using GNW2.Events;
using TMPro;

namespace GNW2.UI
{
    /// <summary>
    /// UI component that displays player scores by listening to ScoreUpdatedEvent.
    /// Example of how the event bus makes it trivial to add new UI without touching game logic.
    /// </summary>
    public class ScoreDisplayUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI winRateText;

        [Header("Display Settings")]
        [SerializeField] private bool showWinRate = true;
        [SerializeField] private Color winColor = Color.green;
        [SerializeField] private Color lossColor = Color.red;
        [SerializeField] private Color drawColor = Color.yellow;

        private int _wins = 0;
        private int _losses = 0;
        private int _draws = 0;

        private void OnEnable()
        {
            // Subscribe to score updates
            EventBus.Subscribe<ScoreUpdatedEvent>(OnScoreUpdated);
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            EventBus.Unsubscribe<ScoreUpdatedEvent>(OnScoreUpdated);
        }

        /// <summary>
        /// Update display when score changes
        /// Only shows score for the local player
        /// </summary>
        private void OnScoreUpdated(ScoreUpdatedEvent evt)
        {
            // Get local player reference
            var localPlayer = NetworkRunner.GetRunnerForGameObject(gameObject)?.LocalPlayer;
            if (localPlayer == null || evt.Player != localPlayer)
                return;

            // Update stored values
            _wins = evt.Wins;
            _losses = evt.Losses;
            _draws = evt.Draws;

            // Update UI
            UpdateDisplay();
        }

        /// <summary>
        /// Refresh the UI display with current scores
        /// </summary>
        private void UpdateDisplay()
        {
            if (scoreText != null)
            {
                // Format: "W: 5  L: 2  D: 1"
                scoreText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(winColor)}>W: {_wins}</color>  " +
                                $"<color=#{ColorUtility.ToHtmlStringRGB(lossColor)}>L: {_losses}</color>  " +
                                $"<color=#{ColorUtility.ToHtmlStringRGB(drawColor)}>D: {_draws}</color>";
            }

            if (showWinRate && winRateText != null)
            {
                int totalGames = _wins + _losses + _draws;
                float winRate = totalGames > 0 ? ((float)_wins / totalGames) * 100f : 0f;
                winRateText.text = $"Win Rate: {winRate:F1}%";
            }
        }

        /// <summary>
        /// Optional: Manually refresh display (useful for initial setup)
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateDisplay();
        }
    }
}
