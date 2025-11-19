using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using GNW2.Events;
using GNW2.GameManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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

        // -----------------------------
        // PoisonedGlass (merged fields)
        // -----------------------------
        [Header("Roulette HP Settings")]
        public int playerHP = 5;
        public int opponentHP = 5;

        [Header("Roulette Batch Settings")]
        public int totalDrinksInBatch = 6;
        public int poisonedDrinksInBatch = 2;

        private int drinksLeft;
        private int poisonedLeft;

        [Header("Roulette UI References")]
        public TMP_Text playerHPText;
        public TMP_Text opponentHPText;
        public TMP_Text resultText;
        public TMP_Text batchInfoText;

        [Header("Roulette Buttons")]
        public Button drinkButton;
        public Button passButton;
        public Button restartButton;

        [Header("AI Settings")]
        [Tooltip("How many seconds the AI waits before acting")]
        public float aiResponseDelay = 1f;

        private bool isPlayerTurn = true;
        private bool opponentIsHuman = false;

        // ----------------------------

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

            // Set up UserData folder (use persistentDataPath for runtime writable storage)
            UserData = Path.Combine(Application.persistentDataPath, "UserData");
            if (!Directory.Exists(UserData))
                Directory.CreateDirectory(UserData);

            HideAllPanels();
            loginPanel.SetActive(true);

            // If a current user exists in PlayerPrefs (from previous login), show name immediately
            string savedUser = PlayerPrefs.GetString("CurrentUser", "");
            if (!string.IsNullOrEmpty(savedUser))
            {
                DisplayOpponentName(savedUser);
                if (opponentNamePanel != null)
                    opponentNamePanel.SetActive(true);
            }
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

            // Wire up PoisonedGlass buttons (if assigned)
            if (drinkButton != null)
                drinkButton.onClick.AddListener(OnDrinkButtonClicked);
            if (passButton != null)
                passButton.onClick.AddListener(OnPassButtonClicked);
            if (restartButton != null)
                restartButton.onClick.AddListener(RestartGame);

            // initial refresh of local accounts
            RefreshLocalAccountsList();
        }

        private void OnDestroy()
        {
            // Remove listeners we've added
            if (registerButton != null)
                registerButton.onClick.RemoveListener(OnClick_RegisterButton);

            Button confirmButton = GameObject.Find("ConfirmRegisterButton")?.GetComponent<Button>();
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(RegisterAccount);

            Button loginButton = GameObject.Find("LoginButton")?.GetComponent<Button>();
            if (loginButton != null)
                loginButton.onClick.RemoveListener(LoginAccount);

            if (deleteAccountButton != null)
                deleteAccountButton.onClick.RemoveAllListeners();

            if (refreshAccountsButton != null)
                refreshAccountsButton.onClick.RemoveListener(RefreshLocalAccountsList);

            if (drinkButton != null)
                drinkButton.onClick.RemoveListener(OnDrinkButtonClicked);
            if (passButton != null)
                passButton.onClick.RemoveListener(OnPassButtonClicked);
            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartGame);

            // Note: EventBus unsubscribe API unknown in this workspace; if available you should unsubscribe here.
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

            // Call backend via PlayerManager (async). On success, also create/update a local JSON as fallback for UI list.
            PlayerManager.instance.Register(user, pass, email, (success, message) =>
            {
                if (success)
                {
                    // create/update local JSON for UI convenience
                    try
                    {
                        string filePath = Path.Combine(UserData, $"{user}.json");
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
                        PlayerPrefs.SetFloat($"WinRate_{user}", 0f);
                        PlayerPrefs.Save();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RegisterAccount] Failed to write local fallback file: {ex}");
                    }

                    if (feedbackText != null) feedbackText.text = "Account registered!";
                    registerPanel.SetActive(false);
                    loginPanel.SetActive(true);
                    RefreshLocalAccountsList();
                }
                else
                {
                    if (feedbackText != null) feedbackText.text = $"Registration failed: {message}";
                }
            });
        }

        public void LoginAccount()
        {
            string user = loginUsernameInput.text.Trim();
            string pass = loginPasswordInput.text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                if (feedbackText != null) feedbackText.text = "Please enter username and password.";
                return;
            }

            PlayerManager.instance.Login(user, pass, (success, message) =>
            {
                if (success)
                {
                    // Update or create local JSON fallback so the local UI list can work
                    try
                    {
                        string filePath = Path.Combine(UserData, $"{user}.json");
                        PlayerData loaded = null;
                        if (File.Exists(filePath))
                        {
                            string j = File.ReadAllText(filePath);
                            try { loaded = JsonUtility.FromJson<PlayerData>(j); } catch { loaded = null; }
                        }

                        if (loaded == null)
                        {
                            loaded = new PlayerData
                            {
                                username = user,
                                password = pass,
                                email = "",
                                score = 0,
                                wins = 0,
                                losses = 0,
                                creationDate = DateTime.UtcNow.ToString("o")
                            };
                        }

                        loaded.lastLoggedIn = DateTime.UtcNow.ToString("o");
                        string updatedJson = JsonUtility.ToJson(loaded, true);
                        File.WriteAllText(filePath, updatedJson);

                        float winRate = ComputeWinRate(loaded);
                        PlayerPrefs.SetFloat($"WinRate_{user}", winRate);
                        PlayerPrefs.SetString("CurrentUser", user);
                        PlayerPrefs.Save();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[LoginAccount] Failed to write local fallback file: {ex}");
                    }

                    if (feedbackText != null)
                        feedbackText.text = "Login successful!";

                    HideAllPanels();
                    opponentNamePanel.SetActive(true);
                    DisplayOpponentName(user);

                    if (gameHandler == null)
                        gameHandler = FindFirstObjectByType<GameHandler>();

                    if (gameHandler != null)
                        gameHandler.SendUsernameToServer(user);
                    else
                        StartCoroutine(WaitForGameHandlerAndSend(user));

                    RefreshLocalAccountsList();
                }
                else
                {
                    if (feedbackText != null)
                        feedbackText.text = $"Login failed: {message}";
                }
            });
        }

        // Retry helper: wait a short time for the network-spawned GameHandler to appear then send username
        private IEnumerator WaitForGameHandlerAndSend(string username)
        {
            const float timeout = 5f;
            const float pollInterval = 0.2f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                gameHandler = FindFirstObjectByType<GameHandler>();
                if (gameHandler != null)
                {
                    gameHandler.SendUsernameToServer(username);
                    Debug.Log($"[LOGIN] Found GameHandler after {elapsed:0.00}s — sent username: {username}");
                    yield break;
                }

                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;
            }

            Debug.LogWarning("[LOGIN] GameHandler still not found after waiting — username not sent to server.");
        }

        public void ShowGamePanel()
        {
            HideAllPanels();

            if (gamePanel == null)
            {
                Debug.LogError("[UI] ShowGamePanel called but gamePanel is null on GameUIManager.Instance!");
                return;
            }

            // Activate the panel
            gamePanel.SetActive(true);

            // Ensure there is an enabled Canvas on the panel or its parents
            var canvas = gamePanel.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogWarning("[UI] No Canvas found for gamePanel. UI may not render.");
            }
            else
            {
                if (!canvas.enabled)
                {
                    canvas.enabled = true;
                    Debug.Log("[UI] Enabled Canvas for gamePanel.");
                }
                Debug.Log($"[UI] Canvas found: name='{canvas.name}' renderMode={canvas.renderMode} sortingOrder={canvas.sortingOrder}");
            }

            // Ensure CanvasGroup (if present) is visible & interactable
            var cg = gamePanel.GetComponentInParent<CanvasGroup>(true);
            if (cg != null)
            {
                if (cg.alpha < 0.01f) cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                Debug.Log("[UI] Adjusted CanvasGroup on gamePanel parent (alpha/interactable/blocksRaycasts).");
            }

            Debug.Log($"[UI] Game panel SetActive(true) — activeSelf: {gamePanel.activeSelf}, activeInHierarchy: {gamePanel.activeInHierarchy}");
            Debug.Log("[UI] Game panel activated — both players ready!");

            // Initialize merged PoisonedGlass game UI/logic
            InitializePoisonedGlass();
        }

        private void InitializePoisonedGlass()
        {
            // Reset gameplay state and UI when the panel is shown
            StartNewBatch();
            UpdatePoisonedGlassUI();

            // Do NOT assume it's the local player's turn yet — authority will tell clients which player starts.
            UpdatePoisonedGlassButtonVisibility();

            // Ensure panel visibility respects whose turn it is (will be set once server tells starting player)
            UpdateGamePanelVisibility();

            if (restartButton != null)
                restartButton.gameObject.SetActive(false);
        }

        // New: called by GameHandler client-side when game start broadcast arrives
        public void SetStartingPlayer(int startingPlayerId)
        {
            // ensure we have a GameHandler reference to access Runner for local player id
            if (gameHandler == null)
                gameHandler = FindFirstObjectByType<GameHandler>();

            int localPlayerId = -1;
            try
            {
                if (gameHandler != null && gameHandler.Runner != null)
                {
                    localPlayerId = gameHandler.Runner.LocalPlayer.PlayerId;
                }
            }
            catch (Exception)
            {
                // ignore - can't determine local id
            }

            isPlayerTurn = (localPlayerId == startingPlayerId);

            // Update UI visibility + buttons based on who starts
            UpdateGamePanelVisibility();
            UpdatePoisonedGlassButtonVisibility();

            if (resultText != null)
                resultText.text = isPlayerTurn ? "Your turn! Choose Drink or Pass." : "Waiting for opponent...";
        }

        // New: authoritative turn update from server
        public void SetTurnPlayer(int playerId)
        {
            if (gameHandler == null)
                gameHandler = FindFirstObjectByType<GameHandler>();

            int localPlayerId = -1;
            if (gameHandler != null && gameHandler.Runner != null)
                localPlayerId = gameHandler.Runner.LocalPlayer.PlayerId;

            isPlayerTurn = (localPlayerId == playerId);
            UpdatePoisonedGlassButtonVisibility();

            if (resultText != null)
                resultText.text = isPlayerTurn ? "Your turn! Choose Drink or Pass." : "Opponent's turn...";
        }

        private void OnDrinkButtonClicked()
        {
            // tell authoritative handler if present
            if (gameHandler != null)
            {
                gameHandler.SendPlayerSelection(0);
            }

            // In PvP, wait for server resolution, do not simulate locally
            if (opponentIsHuman)
            {
                if (resultText != null) resultText.text = "Waiting for server...";
                return;
            }

            OnDrink();
        }

        private void OnPassButtonClicked()
        {
            if (gameHandler != null)
            {
                gameHandler.SendPlayerSelection(1);
            }

            // In PvP, wait for server resolution, do not simulate locally
            if (opponentIsHuman)
            {
                if (resultText != null) resultText.text = "Waiting for server...";
                return;
            }

            OnPassDrink();
        }

        // Player chooses to drink
        private void OnDrink()
        {
            if (!isPlayerTurn || IsGameOver()) return;

            bool poisoned = DrawDrink();
            if (poisoned)
            {
                playerHP--;
                if (resultText != null) resultText.text = "You drank poison! -1 HP";
                EndTurn();
            }
            else
            {
                if (resultText != null) resultText.text = "You drank safely! You get to choose again.";
                UpdatePoisonedGlassUI();
            }
        }

        // Player chooses to pass
        private void OnPassDrink()
        {
            if (!isPlayerTurn || IsGameOver()) return;

            bool poisoned = DrawDrink();
            if (poisoned)
            {
                opponentHP--;
                if (resultText != null) resultText.text = "You passed poison! Opponent -1 HP";
            }
            else
            {
                if (resultText != null) resultText.text = "You passed a safe drink.";
            }

            UpdatePoisonedGlassUI();
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
                if (UnityEngine.Random.value < chance)
                {
                    poisoned = true;
                    poisonedLeft--;
                }
            }

            UpdatePoisonedGlassUI();
            return poisoned;
        }

        private void EndTurn()
        {
            UpdatePoisonedGlassUI();
            if (IsGameOver()) return;

            // In PvP, do not toggle locally — wait for server RPC to call SetTurnPlayer(...)
            if (opponentIsHuman)
            {
                UpdatePoisonedGlassButtonVisibility();
                UpdateGamePanelVisibility();
                if (resultText != null && !isPlayerTurn) resultText.text = "Waiting for opponent...";
                return;
            }

            // Versus AI: local toggle
            isPlayerTurn = !isPlayerTurn;
            UpdatePoisonedGlassButtonVisibility();
            UpdateGamePanelVisibility();

            if (!isPlayerTurn)
                StartCoroutine(OpponentTurnWithDelay(aiResponseDelay));
            else if (resultText != null)
                resultText.text += "\nYour turn!";
        }

        private IEnumerator OpponentTurnWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            OpponentTurn();
        }

        private void OpponentTurn()
        {
            if (IsGameOver()) return;

            // If there is a human opponent connected, do not simulate AI here.
            if (opponentIsHuman)
            {
                if (resultText != null) resultText.text = "Waiting for opponent...";
                return;
            }

            bool aiChoiceDrink = UnityEngine.Random.value > 0.5f;

            if (aiChoiceDrink)
            {
                ProcessOpponentChoice(0, true);
            }
            else
            {
                ProcessOpponentChoice(1, true);
            }
        }

        /// <summary>
        /// Apply the opponent's selection (0 = Drink, 1 = Pass).
        /// If madeByAI is false, this was triggered by a remote human player event.
        /// </summary>
        private void ProcessOpponentChoice(int selection, bool madeByAI)
        {
            if (IsGameOver()) return;

            if (selection == 0) // Drink
            {
                bool poisoned = DrawDrink();
                if (poisoned)
                {
                    opponentHP--;
                    if (resultText != null) resultText.text = madeByAI ? "Opponent chose DRINK and got poison! -1 HP" : "Opponent drank and got poison! -1 HP";
                }
                else
                {
                    if (resultText != null) resultText.text = madeByAI ? "Opponent chose DRINK and was safe." : "Opponent drank safely.";
                }
            }
            else // Pass
            {
                bool poisoned = DrawDrink();
                if (poisoned)
                {
                    playerHP--;
                    if (resultText != null) resultText.text = madeByAI ? "Opponent chose PASS and gave you poison! -1 HP" : "Opponent passed and gave you poison! -1 HP";
                }
                else
                {
                    if (resultText != null) resultText.text = madeByAI ? "Opponent chose PASS but it was safe." : "Opponent passed but it was safe.";
                }
            }

            UpdatePoisonedGlassUI();
            EndTurn();
        }

        private void StartNewBatch()
        {
            drinksLeft = totalDrinksInBatch;
            poisonedLeft = poisonedDrinksInBatch;
        }

        private void UpdatePoisonedGlassUI()
        {
            if (playerHPText != null) playerHPText.text = "Player HP: " + playerHP;
            if (opponentHPText != null) opponentHPText.text = "Opponent HP: " + opponentHP;
            if (batchInfoText != null) batchInfoText.text = $"Batch: {drinksLeft} drinks left, {poisonedLeft} poisoned";
        }

        private void UpdatePoisonedGlassButtonVisibility()
        {
            bool show = isPlayerTurn && !IsGameOver();

            if (drinkButton != null)
                drinkButton.interactable = show;
            if (passButton != null)
                passButton.interactable = show;

            if (drinkButton != null)
                drinkButton.gameObject.SetActive(true);
            if (passButton != null)
                passButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the entire gamePanel when it's not the player's turn and show it when it is.
        /// If the game is over, keep the gamePanel visible so end panels can be shown.
        /// </summary>
        private void UpdateGamePanelVisibility()
        {
            if (gamePanel == null) return;

            // Keep panel always visible; just disable interaction when not your turn.
            if (!gamePanel.activeSelf)
                gamePanel.SetActive(true);
        }

        private bool IsGameOver()
        {
            if (playerHP <= 0)
            {
                if (resultText != null) resultText.text = "You lost! Opponent wins.";
                UpdatePoisonedGlassButtonVisibility();
                if (restartButton != null) restartButton.gameObject.SetActive(true);
                // Ensure panel visible on game over so the player can see results
                if (gamePanel != null) gamePanel.SetActive(true);
                return true;
            }
            else if (opponentHP <= 0)
            {
                if (resultText != null) resultText.text = "You win! Opponent is out.";
                UpdatePoisonedGlassButtonVisibility();
                if (restartButton != null) restartButton.gameObject.SetActive(true);
                if (gamePanel != null) gamePanel.SetActive(true);
                return true;
            }
            return false;
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ============================
        // GAME UI EVENTS
        // ============================

        private void OnPlayerMadeSelection(PlayerMadeSelectionEvent evt)
        {
            selectionPanel.SetActive(false);

            // If opponent is a human and the selection came from the other player, apply it locally.
            // Use gameHandler.Runner.LocalPlayer to determine which PlayerRef is local.
            if (opponentIsHuman && gameHandler != null)
            {
                try
                {
                    var localPlayerRef = gameHandler.Runner.LocalPlayer;
                    // Only apply the event if it came from the remote player (not the local one)
                    if (!evt.Player.Equals(localPlayerRef))
                    {
                        ProcessOpponentChoice(evt.Selection, false);
                    }
                }
                catch (Exception)
                {
                    // If we cannot access Runner/LocalPlayer for some reason, do not apply and just wait.
                    Debug.LogWarning("[OnPlayerMadeSelection] Failed to determine local player; selection ignored here.");
                }
            }
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

            // Determine if there is another connected human player
            opponentIsHuman = (usernames.Count > 1);

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
        // SERVER SYNC (added)
        // ============================

        // Receive a snapshot from server at start and sync local state
        public void SyncServerState(int startingPlayerId,
            int p1Id, int p1Hp, int p2Id, int p2Hp, int drinksLeft, int poisonedLeft)
        {
            if (gameHandler == null)
                gameHandler = FindFirstObjectByType<GameHandler>();

            int me = -1;
            try
            {
                if (gameHandler != null && gameHandler.Runner != null)
                    me = gameHandler.Runner.LocalPlayer.PlayerId;
            }
            catch { }

            if (me == p1Id)
            {
                playerHP = p1Hp;
                opponentHP = p2Hp;
            }
            else if (me == p2Id)
            {
                playerHP = p2Hp;
                opponentHP = p1Hp;
            }

            this.drinksLeft = drinksLeft;
            this.poisonedLeft = poisonedLeft;

            SetStartingPlayer(startingPlayerId);
            UpdatePoisonedGlassUI();
        }

        // Apply authoritative resolution for each action from server
        public void ApplyServerResolution(int actorId, int selection, bool poisoned, int nextTurnId,
            int p1Id, int p1Hp, int p2Id, int p2Hp, int drinksLeft, int poisonedLeft)
        {
            if (gameHandler == null)
                gameHandler = FindFirstObjectByType<GameHandler>();

            int me = -1;
            try
            {
                if (gameHandler != null && gameHandler.Runner != null)
                    me = gameHandler.Runner.LocalPlayer.PlayerId;
            }
            catch { }

            if (me == p1Id)
            {
                playerHP = p1Hp;
                opponentHP = p2Hp;
            }
            else if (me == p2Id)
            {
                playerHP = p2Hp;
                opponentHP = p1Hp;
            }

            this.drinksLeft = drinksLeft;
            this.poisonedLeft = poisonedLeft;

            bool iActed = (actorId == me);
            string who = iActed ? "You" : "Opponent";
            if (resultText != null)
            {
                if (selection == 0)
                    resultText.text = poisoned ? $"{who} drank poison! -1 HP" : $"{who} drank safely.";
                else
                    resultText.text = poisoned ? $"{who} passed poison! -1 HP to {(iActed ? "opponent" : "you")}" : $"{who} passed a safe drink.";
            }

            UpdatePoisonedGlassUI();
            SetTurnPlayer(nextTurnId);
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
            if (opponentNameText != null) opponentNameText.text = $"Player: {opponent}";
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