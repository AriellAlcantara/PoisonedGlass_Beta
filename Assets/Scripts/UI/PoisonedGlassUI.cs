using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement; // for restarting the scene

namespace GNW2.UI
{
    public class PoisonedGlassUI : MonoBehaviour
    {
        [Header("HP Settings")]
        public int playerHP = 5;
        public int opponentHP = 5;

        [Header("Batch Settings")]
        public int totalDrinksInBatch = 6;
        public int poisonedDrinksInBatch = 2;

        private int drinksLeft;
        private int poisonedLeft;

        [Header("UI References")]
        public TMP_Text playerHPText;
        public TMP_Text opponentHPText;
        public TMP_Text resultText;
        public TMP_Text batchInfoText;

        [Header("Buttons")]
        public GameObject drinkButton;
        public GameObject passButton;
        public GameObject restartButton;

        [Header("AI Settings")]
        [Tooltip("How many seconds the AI waits before acting")]
        public float aiResponseDelay = 1f;

        private bool isPlayerTurn = true;

        private GameUIManager hostUIManager; // new: reference to the UI manager

        void Start()
        {
            StartNewBatch();
            UpdateUI();
            resultText.text = "Your turn! Choose Drink or Pass.";
            UpdateButtonVisibility();

            if (restartButton != null) restartButton.SetActive(false);
        }

        // Player chooses to drink
        public void OnDrink()
        {
            if (!isPlayerTurn || IsGameOver()) return;

            bool poisoned = DrawDrink();
            if (poisoned)
            {
                playerHP--;
                resultText.text = "You drank poison! -1 HP";
                EndTurn();
            }
            else
            {
                resultText.text = "You drank safely! You get to choose again.";
                UpdateUI();
            }
        }

        // Player chooses to pass
        public void OnPassDrink()
        {
            if (!isPlayerTurn || IsGameOver()) return;

            bool poisoned = DrawDrink();
            if (poisoned)
            {
                opponentHP--;
                resultText.text = "You passed poison! Opponent -1 HP";
            }
            else
            {
                resultText.text = "You passed a safe drink.";
            }

            UpdateUI();
            EndTurn();
        }

        private bool DrawDrink()
        {
            if (drinksLeft <= 0)
            {
                StartNewBatch();
            }

            drinksLeft--;

            bool poisoned = false;
            if (poisonedLeft > 0)
            {
                float chance = (float)poisonedLeft / (drinksLeft + 1);
                if (Random.value < chance)
                {
                    poisoned = true;
                    poisonedLeft--;
                }
            }

            UpdateUI();
            return poisoned;
        }

        private void EndTurn()
        {
            UpdateUI();
            if (IsGameOver()) return;

            isPlayerTurn = !isPlayerTurn;
            UpdateButtonVisibility();

            if (!isPlayerTurn)
            {
                StartCoroutine(OpponentTurnWithDelay(aiResponseDelay));
            }
            else
            {
                resultText.text += "\nYour turn!";
            }
        }

        private IEnumerator OpponentTurnWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            OpponentTurn();
        }

        private void OpponentTurn()
        {
            if (IsGameOver()) return;

            bool aiChoiceDrink = Random.value > 0.5f;

            if (aiChoiceDrink)
            {
                bool poisoned = DrawDrink();
                if (poisoned)
                {
                    opponentHP--;
                    resultText.text = "Opponent chose DRINK and got poison! -1 HP";
                }
                else
                {
                    resultText.text = "Opponent chose DRINK and was safe.";
                }
            }
            else
            {
                bool poisoned = DrawDrink();
                if (poisoned)
                {
                    playerHP--;
                    resultText.text = "Opponent chose PASS and gave you poison! -1 HP";
                }
                else
                {
                    resultText.text = "Opponent chose PASS but it was safe.";
                }
            }

            UpdateUI();
            EndTurn();
        }

        private void StartNewBatch()
        {
            drinksLeft = totalDrinksInBatch;
            poisonedLeft = poisonedDrinksInBatch;
        }

        private void UpdateUI()
        {
            playerHPText.text = "Player HP: " + playerHP;
            opponentHPText.text = "Opponent HP: " + opponentHP;
            batchInfoText.text = $"Batch: {drinksLeft} drinks left, {poisonedLeft} poisoned";
        }

        private void UpdateButtonVisibility()
        {
            drinkButton.SetActive(isPlayerTurn && !IsGameOver());
            passButton.SetActive(isPlayerTurn && !IsGameOver());
        }

        private bool IsGameOver()
        {
            if (playerHP <= 0)
            {
                resultText.text = "You lost! Opponent wins.";
                UpdateButtonVisibility();
                if (restartButton != null) restartButton.SetActive(true);
                return true;
            }
            else if (opponentHP <= 0)
            {
                resultText.text = "You win! Opponent is out.";
                UpdateButtonVisibility();
                if (restartButton != null) restartButton.SetActive(true);
                return true;
            }
            return false;
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Called by GameUIManager when the Game Panel is shown so the PoisonedGlassUI can
        /// initialize state and keep a reference to the manager if needed.
        /// </summary>
        public void OnGamePanelShown(GameUIManager uiManager)
        {
            hostUIManager = uiManager;

            // Ensure restart button is hidden when panel shows
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);

            // Reset or ensure gameplay UI is in a known state
            StartNewBatch();
            UpdateUI();
            UpdateButtonVisibility();

            if (resultText != null)
                resultText.text = "Your turn! Choose Drink or Pass.";
        }
    }
}
