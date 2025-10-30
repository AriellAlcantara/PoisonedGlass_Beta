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
        public int losses;
        public string creationDate;   // ISO 8601 UTC
        public string lastLoggedIn;   // ISO 8601 UTC
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

        [Header("Game Panel")]
        public GameObject gamePanel;

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
        public Button deleteAccountButton;
        public Button refreshAccountsButton;

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

            // Delete account now requires both correct username AND password typed.
            if (deleteAccountButton != null)
            {
                deleteAccountButton.onClick.RemoveAllListeners();
                deleteAccountButton.onClick.AddListener(() =>
                {
                    string userToDelete = loginUsernameInput != null ? loginUsernameInput.text.Trim() : "";
                    string passEntered = loginPasswordInput != null ? loginPasswordInput.text : "";

                    if (string.IsNullOrEmpty(userToDelete) || string.IsNullOrEmpty(passEntered))
                    {
                        if (feedbackText != null) feedbackText.text = "Enter username and password to delete account.";
                        return;
                    }

                    if (ValidateCredentials(userToDelete, passEntered, out _))
                    {
                        // optional extra safety: you could show a confirm dialog here
                        DeleteAccount(userToDelete);
                    }
                    else
                    {
                        if (feedbackText != null) feedbackText.text = "Username or password incorrect. Account not deleted.";
                    }
                });
            }

            if (refreshAccountsButton != null)
                refreshAccountsButton.onClick.AddListener(RefreshLocalAccountsList);

            // initial refresh of local accounts
            RefreshLocalAccountsList();
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
            if (gamePanel != null)
                gamePanel.SetActive(false);
        }

        // ============================
        // LOGIN & REGISTRATION LOGIC
        // ============================

        public void OnClick_RegisterButton()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
            if (feedbackText != null) feedbackText.text = "";
        }

        public void OnClick_BackToLogin()
        {
            registerPanel.SetActive(false);
            loginPanel.SetActive(true);
            if (feedbackText != null) feedbackText.text = "";
        }

        public void RegisterAccount()
        {
            string user = regUsernameInput.text.Trim();
            string pass = regPasswordInput.text.Trim();
            string repass = regRepeatPasswordInput.text.Trim();
            string email = regEmailInput.text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(email))
            {
                if (feedbackText != null) feedbackText.text = "Please fill in all fields.";
                return;
            }

            if (pass != repass)
            {
                if (feedbackText != null) feedbackText.text = "Passwords do not match!";
                return;
            }

            string filePath = Path.Combine(UserData, $"{user}.json");
            if (File.Exists(filePath))
            {
                if (feedbackText != null) feedbackText.text = "Username already exists!";
                return;
            }

            string nowIso = DateTime.UtcNow.ToString("o");
            PlayerData newData = new PlayerData
            {
                username = user,
                password = pass,
                email = email,
                score = 0,
                wins = 0,
                losses = 0,
                creationDate = nowIso,
                lastLoggedIn = nowIso
            };

            string json = JsonUtility.ToJson(newData, true);
            File.WriteAllText(filePath, json);

            // store initial win rate separately in PlayerPrefs (NOT in JSON)
            PlayerPrefs.SetFloat($"WinRate_{user}", 0f);
            PlayerPrefs.Save();

            // Keep players list updated
            RefreshLocalAccountsList();

            if (feedbackText != null) feedbackText.text = "Account registered!";
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
                if (feedbackText != null) feedbackText.text = "No account found!";
                return;
            }

            string json = File.ReadAllText(filePath);
            PlayerData loaded = JsonUtility.FromJson<PlayerData>(json);

            if (loaded.password != pass)
            {
                if (feedbackText != null) feedbackText.text = "Incorrect password!";
                return;
            }

            currentPlayer = loaded;

            // Compute time since last online based on loaded.lastLoggedIn
            DateTime prevLoginUtc;
            string humanized = "Unknown";
            bool parsed = DateTime.TryParse(loaded.lastLoggedIn, null, System.Globalization.DateTimeStyles.RoundtripKind, out prevLoginUtc);
            if (parsed)
            {
                TimeSpan diff = DateTime.UtcNow - prevLoginUtc;
                humanized = FormatTimeAgo(diff);

                // Save human readable to PlayerPrefs
                PlayerPrefs.SetString($"LastOnline_{user}", humanized);
                // Save raw ISO timestamp as well
                PlayerPrefs.SetString($"LastOnlineRaw_{user}", loaded.lastLoggedIn);
            }
            else
            {
                // if not parsable, show account creation as fallback
                humanized = "Never logged in before";
                PlayerPrefs.SetString($"LastOnline_{user}", humanized);
            }

            // Compute win rate on the fly and store separately (PlayerPrefs), NOT in JSON
            float winRate = ComputeWinRate(loaded);
            PlayerPrefs.SetFloat($"WinRate_{user}", winRate);

            PlayerPrefs.SetString("CurrentUser", user);
            PlayerPrefs.Save();

            // Use the feedback textmesh to show status including last seen and win rate
            if (feedbackText != null)
                feedbackText.text = $"Login successful!\nLast seen: {humanized}\nWin Rate: {winRate:0.##}%";

            Debug.Log($"[LOGIN] User '{user}' was last seen: {humanized} (WR: {winRate:0.##}%)");

            // Update lastLoggedIn to now and persist to JSON
            loaded.lastLoggedIn = DateTime.UtcNow.ToString("o");
            string updatedJson = JsonUtility.ToJson(loaded, true);
            File.WriteAllText(filePath, updatedJson);

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

            // Refresh local accounts list to reflect updated lastLoggedIn for UI
            RefreshLocalAccountsList();
        }
        public void ShowGamePanel()
        {
            HideAllPanels();
            if (gamePanel != null)
                gamePanel.SetActive(true);

            Debug.Log("[UI] Game panel activated — both players ready!");
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
                currentPlayer.losses += 1;
                currentPlayer.score = Mathf.Max(0, currentPlayer.score - 1);
            }

            string filePath = Path.Combine(UserData, $"{currentPlayer.username}.json");
            string json = JsonUtility.ToJson(currentPlayer, true);
            File.WriteAllText(filePath, json);

            // keep PlayerPrefs in sync with last known player (optional)
            PlayerPrefs.SetString($"LastOnline_{currentPlayer.username}", "Just played");

            // update win rate saved in PlayerPrefs (computed, NOT stored in JSON)
            float wr = ComputeWinRate(currentPlayer);
            PlayerPrefs.SetFloat($"WinRate_{currentPlayer.username}", wr);

            PlayerPrefs.Save();
        }

        public void DisplayOpponentName(string opponent)
        {
            opponentNameText.text = $"Player: {opponent}";
        }

        // ============================
        // ACCOUNT MANAGEMENT HELPERS
        // ============================

        // Delete account and associated PlayerPrefs keys
        public void DeleteAccount(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                if (feedbackText != null) feedbackText.text = "No username specified to delete.";
                return;
            }

            string filePath = Path.Combine(UserData, $"{username}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DeleteAccount] Failed to delete file: {ex}");
                    if (feedbackText != null) feedbackText.text = "Failed to delete account file.";
                    return;
                }
            }
            else
            {
                if (feedbackText != null) feedbackText.text = "Account file not found.";
            }

            // Remove PlayerPrefs entries for that account
            PlayerPrefs.DeleteKey($"LastOnline_{username}");
            PlayerPrefs.DeleteKey($"LastOnlineRaw_{username}");
            PlayerPrefs.DeleteKey($"WinRate_{username}");
            if (PlayerPrefs.HasKey("CurrentUser") && PlayerPrefs.GetString("CurrentUser") == username)
                PlayerPrefs.DeleteKey("CurrentUser");
            PlayerPrefs.Save();

            // If currently logged in user was deleted, reset state
            if (currentPlayer != null && currentPlayer.username == username)
            {
                currentPlayer = null;
                HideAllPanels();
                loginPanel.SetActive(true);
            }

            if (feedbackText != null) feedbackText.text = $"Account '{username}' deleted.";
            RefreshLocalAccountsList();
        }

        // Validate credentials against stored JSON, returns true if user exists and password matches.
        private bool ValidateCredentials(string username, string password, out PlayerData loadedData)
        {
            loadedData = null;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            string filePath = Path.Combine(UserData, $"{username}.json");
            if (!File.Exists(filePath))
                return false;

            try
            {
                string json = File.ReadAllText(filePath);
                PlayerData pd = JsonUtility.FromJson<PlayerData>(json);
                if (pd != null && pd.password == password)
                {
                    loadedData = pd;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ValidateCredentials] Error reading/parsing user file: {ex}");
            }

            return false;
        }

        // Enumerate local JSON accounts and display basic info including computed win rate
        public void RefreshLocalAccountsList()
        {
            if (allPlayersText == null) return;

            string[] files = Directory.GetFiles(UserData, "*.json");
            if (files.Length == 0)
            {
                allPlayersText.text = "No local accounts.";
                return;
            }

            List<string> lines = new List<string>();
            foreach (var f in files)
            {
                try
                {
                    string j = File.ReadAllText(f);
                    PlayerData pd = JsonUtility.FromJson<PlayerData>(j);
                    float winRate = ComputeWinRate(pd);

                    // persist a copy of win rate separately (PlayerPrefs) so it's accessible without parsing JSON
                    PlayerPrefs.SetFloat($"WinRate_{pd.username}", winRate);

                    string lastOnlineHuman = "Unknown";
                    DateTime prev;
                    if (!string.IsNullOrEmpty(pd.lastLoggedIn) && DateTime.TryParse(pd.lastLoggedIn, null, System.Globalization.DateTimeStyles.RoundtripKind, out prev))
                    {
                        lastOnlineHuman = FormatTimeAgo(DateTime.UtcNow - prev);
                    }
                    lines.Add($"{pd.username} • Wins: {pd.wins} • Losses: {pd.losses} • WR: {winRate:0.##}% • Last: {lastOnlineHuman}");
                }
                catch { /* ignore malformed files */ }
            }

            PlayerPrefs.Save();
            allPlayersText.text = string.Join("\n", lines);
        }

        // Compute win rate on the fly (do NOT store in JSON)
        public float ComputeWinRate(PlayerData pd)
        {
            int total = pd.wins + pd.losses;
            if (total == 0) return 0f;
            return 100f * ((float)pd.wins / total);
        }

        // Format TimeSpan into human readable string similar to examples:
        // "17h 10 minutes ago.", "2 days ago."
        private string FormatTimeAgo(TimeSpan diff)
        {
            if (diff.TotalSeconds < 60)
                return "Just now.";
            if (diff.TotalMinutes < 60)
            {
                int m = (int)diff.TotalMinutes;
                return $"{m} minute{(m == 1 ? "" : "s")} ago.";
            }
            if (diff.TotalHours < 24)
            {
                int h = (int)diff.TotalHours;
                int m = diff.Minutes;
                if (m > 0)
                    return $"{h}h {m} minute{(m == 1 ? "" : "s")} ago.";
                return $"{h}h ago.";
            }
            if (diff.TotalDays < 7)
            {
                int d = (int)diff.TotalDays;
                return $"{d} day{(d == 1 ? "" : "s")} ago.";
            }
            // fallback: weeks / months
            int days = (int)diff.TotalDays;
            return $"{days} days ago.";
        }
    }
}