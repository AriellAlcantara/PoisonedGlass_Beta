using UnityEngine;
using System;

/// <summary>
/// Manages player data locally using PlayerPrefs
/// Handles login/logout sessions and caching
/// </summary>
public class PlayerManager : MonoBehaviour
{
    // PlayerPrefs keys
    private const string PLAYER_ID_KEY = "player_id";
    private const string PLAYER_USERNAME_KEY = "player_username";
    private const string PLAYER_EMAIL_KEY = "player_email";
    private const string PLAYER_LEVEL_KEY = "player_level";
    private const string PLAYER_EXPERIENCE_KEY = "player_experience";
    private const string PLAYER_LOGGED_IN_KEY = "player_logged_in";

    public static PlayerManager instance { get; private set; }

    // Local player data
    private LocalPlayerData currentPlayer;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        // Load saved player data if exists
        if (IsPlayerLoggedIn())
        {
            LoadPlayerFromPrefs();
        }
    }

    #region Authentication

    /// <summary>
    /// Register a new player (calls backend). onComplete(success, message)
    /// </summary>
    public void Register(string username, string password, string email, Action<bool, string> onComplete = null)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Username and password cannot be empty");
            onComplete?.Invoke(false, "Username and password cannot be empty");
            return;
        }

        GameBackendManager.instance.Register(username, password, email, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                Debug.Log($"Registration successful! Player ID: {response.data.id}");
                SavePlayerToPrefs(response.data);
                onComplete?.Invoke(true, response.message);
            }
            else
            {
                string msg = response?.message ?? "Registration failed";
                Debug.LogError($"Registration failed: {msg}");
                onComplete?.Invoke(false, msg);
            }
        });
    }

    /// <summary>
    /// Login a player (calls backend). onComplete(success, message)
    /// </summary>
    public void Login(string username, string password, Action<bool, string> onComplete = null)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Username and password cannot be empty");
            onComplete?.Invoke(false, "Username and password cannot be empty");
            return;
        }

        GameBackendManager.instance.Login(username, password, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                Debug.Log($"Login successful! Welcome, {response.data.username}");
                SavePlayerToPrefs(response.data);
                onComplete?.Invoke(true, response.message);
            }
            else
            {
                string msg = response?.message ?? "Login failed";
                Debug.LogError($"Login failed: {msg}");
                onComplete?.Invoke(false, msg);
            }
        });
    }

    /// <summary>
    /// Logout player (clears local data)
    /// </summary>
    public void Logout()
    {
        ClearPlayerPrefs();
        currentPlayer = null;
        Debug.Log("Player logged out");
    }

    #endregion

    #region Player Data Management

    /// <summary>
    /// Get current player data
    /// </summary>
    public LocalPlayerData GetCurrentPlayer()
    {
        return currentPlayer;
    }

    /// <summary>
    /// Refresh player data from backend
    /// </summary>
    public void RefreshPlayerData()
    {
        if (currentPlayer == null || string.IsNullOrEmpty(currentPlayer.id))
        {
            Debug.LogWarning("No player logged in");
            return;
        }

        GameBackendManager.instance.GetPlayerData(currentPlayer.id, OnGetPlayerDataComplete);
    }

    private void OnGetPlayerDataComplete(GetPlayerResponse response)
    {
        if (response.success && response.data != null)
        {
            Debug.Log($"Player data refreshed: {response.data.username}");
            SavePlayerToPrefs(response.data);
        }
        else
        {
            Debug.LogError($"Failed to refresh player data: {response.message}");
        }
    }

    /// <summary>
    /// Update player data (level, experience, email)
    /// </summary>
    public void UpdatePlayerData(int level, int experience, string email = "")
    {
        if (currentPlayer == null || string.IsNullOrEmpty(currentPlayer.id))
        {
            Debug.LogWarning("No player logged in");
            return;
        }

        currentPlayer.level = level;
        currentPlayer.experience = experience;
        if (!string.IsNullOrEmpty(email))
        {
            currentPlayer.email = email;
        }

        // Save locally first
        SavePlayerToPrefs(currentPlayer);

        // Update on backend
        GameBackendManager.instance.UpdatePlayerData(currentPlayer.id, level, experience, currentPlayer.email, OnUpdatePlayerDataComplete);
    }

    private void OnUpdatePlayerDataComplete(UpdatePlayerResponse response)
    {
        if (response.success && response.data != null)
        {
            Debug.Log("Player data updated successfully on backend");
            SavePlayerToPrefs(response.data);
        }
        else
        {
            Debug.LogError($"Failed to update player data: {response.message}");
        }
    }

    /// <summary>
    /// Delete player account
    /// </summary>
    public void DeletePlayerAccount()
    {
        if (currentPlayer == null || string.IsNullOrEmpty(currentPlayer.id))
        {
            Debug.LogWarning("No player logged in");
            return;
        }

        string playerId = currentPlayer.id;
        GameBackendManager.instance.DeletePlayer(playerId, OnDeletePlayerComplete);
    }

    private void OnDeletePlayerComplete(DeletePlayerResponse response)
    {
        if (response.success)
        {
            Debug.Log("Player account deleted successfully");
            Logout();
        }
        else
        {
            Debug.LogError($"Failed to delete player account: {response.message}");
        }
    }

    #endregion

    #region PlayerPrefs Management

    /// <summary>
    /// <summary>
    /// Save player data to PlayerPrefs (from backend PlayerData)
    /// </summary>
    private void SavePlayerToPrefs(PlayerData playerData)
    {
        currentPlayer = new LocalPlayerData
        {
            id = playerData.id,
            username = playerData.username,
            email = playerData.email,
            level = playerData.level,
            experience = playerData.experience
        };

        PlayerPrefs.SetString(PLAYER_ID_KEY, playerData.id);
        PlayerPrefs.SetString(PLAYER_USERNAME_KEY, playerData.username);
        PlayerPrefs.SetString(PLAYER_EMAIL_KEY, playerData.email);
        // Track current user for quick access
        PlayerPrefs.SetString("CurrentUser", playerData.username);
        PlayerPrefs.SetInt(PLAYER_LEVEL_KEY, playerData.level);
        PlayerPrefs.SetInt(PLAYER_EXPERIENCE_KEY, playerData.experience);
        PlayerPrefs.SetInt(PLAYER_LOGGED_IN_KEY, 1);
        PlayerPrefs.Save();

        Debug.Log($"Player data saved to PlayerPrefs: {playerData.username}");
    }

    /// <summary>
    /// Save player data to PlayerPrefs (from local data)
    /// </summary>
    private void SavePlayerToPrefs(LocalPlayerData local)
    {
        currentPlayer = local;

        PlayerPrefs.SetString(PLAYER_ID_KEY, local.id);
        PlayerPrefs.SetString(PLAYER_USERNAME_KEY, local.username);
        PlayerPrefs.SetString(PLAYER_EMAIL_KEY, local.email);
        PlayerPrefs.SetString("CurrentUser", local.username);
        PlayerPrefs.SetInt(PLAYER_LEVEL_KEY, local.level);
        PlayerPrefs.SetInt(PLAYER_EXPERIENCE_KEY, local.experience);
        PlayerPrefs.SetInt(PLAYER_LOGGED_IN_KEY, 1);
        PlayerPrefs.Save();

        Debug.Log($"Player data saved to PlayerPrefs: {local.username}");
    }

    /// <summary>
    /// Load player data from PlayerPrefs
    /// </summary>
    private void LoadPlayerFromPrefs()
    {
        currentPlayer = new LocalPlayerData
        {
            id = PlayerPrefs.GetString(PLAYER_ID_KEY, ""),
            username = PlayerPrefs.GetString(PLAYER_USERNAME_KEY, ""),
            email = PlayerPrefs.GetString(PLAYER_EMAIL_KEY, ""),
            level = PlayerPrefs.GetInt(PLAYER_LEVEL_KEY, 1),
            experience = PlayerPrefs.GetInt(PLAYER_EXPERIENCE_KEY, 0)
        };

        Debug.Log($"Player data loaded from PlayerPrefs: {currentPlayer.username}");
    }

    /// <summary>
    /// Clear all player data from PlayerPrefs
    /// </summary>
    private void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteKey(PLAYER_ID_KEY);
        PlayerPrefs.DeleteKey(PLAYER_USERNAME_KEY);
        PlayerPrefs.DeleteKey(PLAYER_EMAIL_KEY);
        PlayerPrefs.DeleteKey(PLAYER_LEVEL_KEY);
        PlayerPrefs.DeleteKey(PLAYER_EXPERIENCE_KEY);
        PlayerPrefs.DeleteKey(PLAYER_LOGGED_IN_KEY);
        PlayerPrefs.Save();

        Debug.Log("Player data cleared from PlayerPrefs");
    }

    /// <summary>
    /// Check if player is logged in
    /// </summary>
    public bool IsPlayerLoggedIn()
    {
        return PlayerPrefs.GetInt(PLAYER_LOGGED_IN_KEY, 0) == 1;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Add experience to current player
    /// </summary>
    public void AddExperience(int amount)
    {
        if (currentPlayer == null)
            return;

        currentPlayer.experience += amount;
        UpdatePlayerData(currentPlayer.level, currentPlayer.experience);
    }

    /// <summary>
    /// Set player level
    /// </summary>
    public void SetLevel(int newLevel)
    {
        if (currentPlayer == null)
            return;

        currentPlayer.level = newLevel;
        UpdatePlayerData(newLevel, currentPlayer.experience);
    }

    /// <summary>
    /// Print current player info to console
    /// </summary>
    public void PrintPlayerInfo()
    {
        if (currentPlayer == null)
        {
            Debug.Log("No player logged in");
            return;
        }

        Debug.Log($"=== Player Info ===\nID: {currentPlayer.id}\nUsername: {currentPlayer.username}\nEmail: {currentPlayer.email}\nLevel: {currentPlayer.level}\nExperience: {currentPlayer.experience}");
    }

    #endregion
}

#region Local Player Data Class

[System.Serializable]
public class LocalPlayerData
{
    public string id;
    public string username;
    public string email;
    public int level;
    public int experience;
}

#endregion
