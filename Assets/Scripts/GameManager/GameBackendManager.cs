using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameBackendManager : MonoBehaviour
{
    private string apiBaseUrl = "http://localhost:5000/api";

    // JWT or other bearer token returned by backend after login (optional; current backend doesn't issue tokens)
    private string authToken;

    // Store plaintext session password in memory ONLY (required by current backend API). Do NOT persist.
    private string sessionPassword;

    // Admin secret for admin-only endpoints (/api/players)
    private string adminSecret;

    // Singleton instance
    public static GameBackendManager instance { get; private set; }

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

    public void SetServerUrl(string url)
    {
        apiBaseUrl = url;
    }

    public void SetAuthToken(string token)
    {
        authToken = token;
    }

    public void SetSessionPassword(string password)
    {
        sessionPassword = password;
    }

    public void SetAdminSecret(string secret)
    {
        adminSecret = secret;
    }

    private void MaybeAttachAuth(UnityWebRequest req)
    {
        if (!string.IsNullOrEmpty(authToken))
        {
            req.SetRequestHeader("Authorization", $"Bearer {authToken}");
        }
    }

    #region Authentication Routes

    /// <summary>
    /// Register a new player
    /// </summary>
    public void Register(string username, string password, string email, System.Action<RegisterResponse> onComplete)
    {
        StartCoroutine(RegisterCoroutine(username, password, email, onComplete));
    }

    private IEnumerator RegisterCoroutine(string username, string password, string email, System.Action<RegisterResponse> onComplete)
    {
        var registerData = new RegisterRequest
        {
            username = username,
            password = password,
            email = email
        };

        string jsonData = JsonUtility.ToJson(registerData);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{apiBaseUrl}/player/register", ""))
        {
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<RegisterResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new RegisterResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Register Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Login a player
    /// </summary>
    public void Login(string username, string password, System.Action<LoginResponse> onComplete)
    {
        StartCoroutine(LoginCoroutine(username, password, onComplete));
    }

    private IEnumerator LoginCoroutine(string username, string password, System.Action<LoginResponse> onComplete)
    {
        var loginData = new LoginRequest
        {
            username = username,
            password = password
        };

        string jsonData = JsonUtility.ToJson(loginData);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{apiBaseUrl}/player/login", ""))
        {
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<LoginResponse>(www.downloadHandler.text);

                // If the backend returns a token, cache it for subsequent authenticated calls
                if (response != null && !string.IsNullOrEmpty(response.token))
                {
                    authToken = response.token;
                }

                // Cache plaintext password for subsequent authenticated calls (server requires password).
                sessionPassword = password;

                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new LoginResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Login Error: " + www.error);
            }
        }
    }

    #endregion

    #region Player Data Routes

    /// <summary>
    /// Get player data (current backend requires password each request). Uses POST /api/player/get
    /// </summary>
    public void GetPlayerData(string playerId, System.Action<GetPlayerResponse> onComplete)
    {
        StartCoroutine(GetPlayerDataCoroutine(playerId, onComplete));
    }

    private IEnumerator GetPlayerDataCoroutine(string playerId, System.Action<GetPlayerResponse> onComplete)
    {
        var payload = new AuthGetPlayerRequest
        {
            id = playerId,
            password = sessionPassword
        };
        string jsonData = JsonUtility.ToJson(payload);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{apiBaseUrl}/player/get", ""))
        {
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            MaybeAttachAuth(www);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<GetPlayerResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new GetPlayerResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Get Player Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Update player core data (requires currentPassword)
    /// </summary>
    public void UpdatePlayerData(string playerId, int level, int experience, string email, System.Action<UpdatePlayerResponse> onComplete)
    {
        StartCoroutine(UpdatePlayerDataCoroutine(playerId, level, experience, email, onComplete));
    }

    private IEnumerator UpdatePlayerDataCoroutine(string playerId, int level, int experience, string email, System.Action<UpdatePlayerResponse> onComplete)
    {
        var updateData = new UpdatePlayerRequest
        {
            id = playerId,
            level = level,
            experience = experience,
            email = email,
            currentPassword = sessionPassword
        };

        string jsonData = JsonUtility.ToJson(updateData);

        using (UnityWebRequest www = UnityWebRequest.Put($"{apiBaseUrl}/player", System.Text.Encoding.UTF8.GetBytes(jsonData)))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            MaybeAttachAuth(www);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<UpdatePlayerResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new UpdatePlayerResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Update Player Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Delete player (current backend requires password in body). Uses DELETE /api/delete with JSON body.
    /// </summary>
    public void DeletePlayer(string playerId, System.Action<DeletePlayerResponse> onComplete)
    {
        StartCoroutine(DeletePlayerCoroutine(playerId, onComplete));
    }

    private IEnumerator DeletePlayerCoroutine(string playerId, System.Action<DeletePlayerResponse> onComplete)
    {
        var payload = new FlexibleDeleteRequest { id = playerId, password = sessionPassword };
        string jsonData = JsonUtility.ToJson(payload);

        using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/delete", UnityWebRequest.kHttpVerbDELETE))
        {
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            MaybeAttachAuth(www);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DeletePlayerResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new DeletePlayerResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Delete Player Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Get all users (admin-only). Sends admin secret via x-admin-password header.
    /// </summary>
    public void GetUsers(System.Action<GetUsersResponse> onComplete)
    {
        StartCoroutine(GetUsersCoroutine(onComplete));
    }

    private IEnumerator GetUsersCoroutine(System.Action<GetUsersResponse> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/players"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(adminSecret))
            {
                www.SetRequestHeader("x-admin-password", adminSecret);
            }
            MaybeAttachAuth(www);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<GetUsersResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new GetUsersResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Get Users Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Get a specific user by id using same auth flow as GetPlayerData (password required).
    /// </summary>
    public void GetUserById(string playerId, System.Action<GetPlayerResponse> onComplete)
    {
        GetPlayerData(playerId, onComplete);
    }

    /// <summary>
    /// Update player's score (requires currentPassword). Uses PUT /api/player.
    /// </summary>
    public void UpdatePlayerScore(string playerId, int score, System.Action<UpdateScoreResponse> onComplete)
    {
        StartCoroutine(UpdatePlayerScoreCoroutine(playerId, score, onComplete));
    }

    private IEnumerator UpdatePlayerScoreCoroutine(string playerId, int score, System.Action<UpdateScoreResponse> onComplete)
    {
        var body = new UpdateScorePutRequest
        {
            id = playerId,
            currentPassword = sessionPassword,
            score = score
        };
        string jsonData = JsonUtility.ToJson(body);

        using (UnityWebRequest www = UnityWebRequest.Put($"{apiBaseUrl}/player", System.Text.Encoding.UTF8.GetBytes(jsonData)))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            MaybeAttachAuth(www);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // server returns UpdatePlayerResponse shape; adapt to UpdateScoreResponse for client
                var raw = JsonUtility.FromJson<UpdatePlayerResponse>(www.downloadHandler.text);
                var mapped = new UpdateScoreResponse
                {
                    success = raw != null && raw.success,
                    message = raw != null ? raw.message : www.error,
                    data = raw != null ? raw.data : null
                };
                onComplete?.Invoke(mapped);
            }
            else
            {
                var errorResponse = new UpdateScoreResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Update Score Error: " + www.error);
            }
        }
    }

    /// <summary>
    /// Get leaderboard (public endpoint on backend)
    /// </summary>
    public void GetLeaderboard(int limit, System.Action<GetLeaderboardResponse> onComplete)
    {
        StartCoroutine(GetLeaderboardCoroutine(limit, onComplete));
    }

    private IEnumerator GetLeaderboardCoroutine(int limit, System.Action<GetLeaderboardResponse> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/leaderboard?limit={limit}"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            // No auth needed per backend

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<GetLeaderboardResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response);
            }
            else
            {
                var errorResponse = new GetLeaderboardResponse
                {
                    success = false,
                    message = www.error
                };
                onComplete?.Invoke(errorResponse);
                Debug.LogError("Get Leaderboard Error: " + www.error);
            }
        }
    }

    #endregion
}

#region Response Classes

[System.Serializable]
public class RegisterRequest
{
    public string username;
    public string password;
    public string email;
}

[System.Serializable]
public class RegisterResponse
{
    public bool success;
    public string message;
    public PlayerData data;
}

[System.Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[System.Serializable]
public class LoginResponse
{
    public bool success;
    public string message;
    public string token; // JWT or similar
    public PlayerData data;
}

[System.Serializable]
public class PlayerData
{
    public string id;
    public string username;
    public string email;
    public int level;
    public int experience;
    public int score; // optional if backend supports
    public int wins;
    public int losses;
}

[System.Serializable]
public class GetPlayerResponse
{
    public bool success;
    public string message;
    public PlayerData data;
}

[System.Serializable]
public class AuthGetPlayerRequest
{
    public string id;
    public string username;
    public string password;
}

[System.Serializable]
public class UpdatePlayerRequest
{
    public string id;
    public int level;
    public int experience;
    public string email;
    public string currentPassword;
}

[System.Serializable]
public class UpdatePlayerResponse
{
    public bool success;
    public string message;
    public PlayerData data;
}

[System.Serializable]
public class DeletePlayerResponse
{
    public bool success;
    public string message;
}

[System.Serializable]
public class GetUsersResponse
{
    public bool success;
    public string message;
    public List<PlayerData> data;
}

[System.Serializable]
public class UpdateScorePutRequest
{
    public string id;
    public string currentPassword;
    public int score;
}

[System.Serializable]
public class UpdateScoreResponse
{
    public bool success;
    public string message;
    public PlayerData data;
}

[System.Serializable]
public class LeaderboardEntry
{
    public string username;
    public int score;
}

[System.Serializable]
public class GetLeaderboardResponse
{
    public bool success;
    public string message;
    public List<LeaderboardEntry> data;
}

[System.Serializable]
public class FlexibleDeleteRequest
{
    public string id;
    public string username;
    public string password;
}

#endregion
