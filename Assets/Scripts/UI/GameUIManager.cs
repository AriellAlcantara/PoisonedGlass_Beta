using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using GNW2.Events;
using GNW2.GameManager;
using System;
using System.Collections.Generic;

namespace GNW2.UI
{
    [Serializable]
    public class PlayerData
    {
        public string username;
        public string password;
        public string email;
        public int score;
        public int wins;
    }

    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance;

        [Header("Panels")]
        public GameObject loginPanel;
        public GameObject registerPanel;
        public GameObject selectionPanel;
        public GameObject winPanel;
        public GameObject losePanel;
        public GameObject drawPanel;
        public GameObject opponentNamePanel;

        [Header("Login Fields")]
        public TMP_InputField loginUsernameInput;
        public TMP_InputField loginPasswordInput;

        [Header("Register Fields")]
        public TMP_InputField regUsernameInput;
        public TMP_InputField regPasswordInput;
        public TMP_InputField regRepeatPasswordInput;
        public TMP_InputField regEmailInput;

        [Header("Buttons")]
        public Button registerButton;

        [Header("Player List UI")]
        public TMP_Text allPlayersText;

        [Header("Text")]
        public TMP_Text feedbackText;
        public TMP_Text opponentNameText;

        private GameHandler gameHandler;
        private string UserData;
        private PlayerData currentPlayer;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            // Set up UserData folder
            UserData = Path.Combine(Application.dataPath, "UserData");
            if (!Directory.Exists(UserData))
                Directory.CreateDirectory(UserData);

            HideAllPanels();
            loginPanel.SetActive(true);
        }

        private void Start()
        {
            gameHandler = FindFirstObjectByType<GameHandler>();
            EventBus.Subscribe<PlayerMadeSelectionEvent>(OnPlayerMadeSelection);
            EventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);

            if (registerButton != null)
                registerButton.onClick.AddListener(OnClick_RegisterButton);

            // Automatically wire up confirm register button
            Button confirmButton = GameObject.Find("ConfirmRegisterButton")?.GetComponent<Button>();
            if (confirmButton != null)
                confirmButton.onClick.AddListener(RegisterAccount);

            Button loginButton = GameObject.Find("LoginButton")?.GetComponent<Button>();
            if (loginButton != null)
                loginButton.onClick.AddListener(LoginAccount);
            else
                Debug.LogWarning("[GameUIManager] LoginButton not found in scene!");
        }

        private void HideAllPanels()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(false);
            selectionPanel.SetActive(false);
            winPanel.SetActive(false);
            losePanel.SetActive(false);
            drawPanel.SetActive(false);
            opponentNamePanel.SetActive(false);
        }

        // ============================
        // LOGIN & REGISTRATION LOGIC
        // ============================

        public void OnClick_RegisterButton()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
            feedbackText.text = "";
        }

        public void OnClick_BackToLogin()
        {
            registerPanel.SetActive(false);
            loginPanel.SetActive(true);
            feedbackText.text = "";
        }

        public void RegisterAccount()
        {
            string user = regUsernameInput.text.Trim();
            string pass = regPasswordInput.text.Trim();
            string repass = regRepeatPasswordInput.text.Trim();
            string email = regEmailInput.text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(email))
            {
                feedbackText.text = "Please fill in all fields.";
                return;
            }

            if (pass != repass)
            {
                feedbackText.text = "Passwords do not match!";
                return;
            }

            string filePath = Path.Combine(UserData, $"{user}.json");
            if (File.Exists(filePath))
            {
                feedbackText.text = "Username already exists!";
                return;
            }

            PlayerData newData = new PlayerData
            {
                username = user,
                password = pass,
                email = email,
                score = 0,
                wins = 0
            };

            string json = JsonUtility.ToJson(newData, true);
            File.WriteAllText(filePath, json);

            feedbackText.text = "Account registered!";
            registerPanel.SetActive(false);
            loginPanel.SetActive(true);
        }

        public void LoginAccount()
        {
            string user = loginUsernameInput.text.Trim();
            string pass = loginPasswordInput.text.Trim();

            string filePath = Path.Combine(UserData, $"{user}.json");
            if (!File.Exists(filePath))
            {
                feedbackText.text = "No account found!";
                return;
            }

            string json = File.ReadAllText(filePath);
            PlayerData loaded = JsonUtility.FromJson<PlayerData>(json);

            if (loaded.password != pass)
            {
                feedbackText.text = "Incorrect password!";
                return;
            }

            feedbackText.text = "Login successful!";
            currentPlayer = loaded;

            HideAllPanels();
            opponentNamePanel.SetActive(true);

            // Display the logged-in username immediately
            DisplayOpponentName(user);

            // Send username to GameHandler if it exists
            if (gameHandler != null)
            {
                gameHandler.SendUsernameToServer(user);
                Debug.Log($"[LOGIN] Sending username to server: {user}");
            }
            else
            {
                Debug.LogWarning("[LOGIN] GameHandler not found!");
            }
        }

        // ============================
        // GAME UI EVENTS
        // ============================

        private void OnPlayerMadeSelection(PlayerMadeSelectionEvent evt)
        {
            selectionPanel.SetActive(false);
        }

        private void OnRoundEnded(RoundEndedEvent evt)
        {
            HideAllPanels();

            if (evt.IsDraw)
            {
                drawPanel.SetActive(true);
            }
            else if (evt.Winner == gameHandler.Runner.LocalPlayer)
            {
                winPanel.SetActive(true);
                UpdatePlayerStats(true);
            }
            else
            {
                losePanel.SetActive(true);
                UpdatePlayerStats(false);
            }

            Invoke(nameof(ShowSelectionAgain), 3f);
        }

        private void ShowSelectionAgain()
        {
            HideAllPanels();
            selectionPanel.SetActive(true);
        }

        public void UpdateAllPlayerNames(List<string> usernames)
        {
            if (allPlayersText == null) return;

            if (usernames.Count == 0)
            {
                allPlayersText.text = "No players connected.";
                return;
            }

            allPlayersText.text = "Players:\n";
            foreach (var name in usernames)
            {
                allPlayersText.text += name + "\n";
            }
        }

        // ============================
        // SAVE/LOAD PLAYER DATA
        // ============================

        public void UpdatePlayerStats(bool won)
        {
            if (currentPlayer == null) return;

            if (won)
            {
                currentPlayer.score += 1;
                currentPlayer.wins += 1;
            }
            else
            {
                currentPlayer.score = Mathf.Max(0, currentPlayer.score - 1);
            }

            string filePath = Path.Combine(UserData, $"{currentPlayer.username}.json");
            string json = JsonUtility.ToJson(currentPlayer, true);
            File.WriteAllText(filePath, json);
        }

        public void DisplayOpponentName(string opponent)
        {
            opponentNameText.text = $"Player: {opponent}";
        }
    }
}
