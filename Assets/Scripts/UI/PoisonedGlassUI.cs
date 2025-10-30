using System.Collections.Generic;
using Fusion;
using GNW2.GameManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GNW2.UI
{
    public class PoisonedGlassUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button drinkButton;
        [SerializeField] private Button makeOtherDrinkButton;

        [Header("Player Display")]
        [SerializeField] private TMP_Text allPlayersText;

        private GameHandler gameHandler;

        private void Awake()
        {
            // Use new Unity API for finding components
            gameHandler = FindFirstObjectByType<GameHandler>();

            if (drinkButton != null)
                drinkButton.onClick.AddListener(() => OnButtonClicked(0));
            if (makeOtherDrinkButton != null)
                makeOtherDrinkButton.onClick.AddListener(() => OnButtonClicked(1));
        }

        private void OnDestroy()
        {
            if (drinkButton != null)
                drinkButton.onClick.RemoveAllListeners();
            if (makeOtherDrinkButton != null)
                makeOtherDrinkButton.onClick.RemoveAllListeners();
        }

        private void OnButtonClicked(int choice)
        {
            if (gameHandler == null)
            {
                Debug.LogWarning("[PoisonedGlassUI] GameHandler not found!");
                return;
            }

            // Send player action to GameHandler
            gameHandler.SendPlayerSelection(choice);
        }

        /// <summary>
        /// Called by GameHandler to update the list of all player names.
        /// </summary>
        public void UpdateAllPlayerNames(List<string> usernames)
        {
            if (allPlayersText == null)
            {
                Debug.LogWarning("[PoisonedGlassUI] allPlayersText not assigned!");
                return;
            }

            if (usernames == null || usernames.Count == 0)
            {
                allPlayersText.text = "No players connected";
                return;
            }

            allPlayersText.text = "Connected Players:\n";
            foreach (var name in usernames)
            {
                allPlayersText.text += "• " + name + "\n";
            }
        }
    }
}
