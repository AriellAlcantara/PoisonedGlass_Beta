using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameBackendManager : MonoBehaviour
{
    private string apiBaseUrl = "http://localhost:5000/api";
    
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
    /// Get player data
    /// </summary>
    public void GetPlayerData(string playerId, System.Action<GetPlayerResponse> onComplete)
    {
        StartCoroutine(GetPlayerDataCoroutine(playerId, onComplete));
    }

    private IEnumerator GetPlayerDataCoroutine(string playerId, System.Action<GetPlayerResponse> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/player?id={playerId}"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

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
    /// Update player data
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
            email = email
        };

        string jsonData = JsonUtility.ToJson(updateData);

        using (UnityWebRequest www = UnityWebRequest.Put($"{apiBaseUrl}/player", System.Text.Encoding.UTF8.GetBytes(jsonData)))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

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
    /// Delete player
    /// </summary>
    public void DeletePlayer(string playerId, System.Action<DeletePlayerResponse> onComplete)
    {
        StartCoroutine(DeletePlayerCoroutine(playerId, onComplete));
    }

    private IEnumerator DeletePlayerCoroutine(string playerId, System.Action<DeletePlayerResponse> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Delete($"{apiBaseUrl}/delete/{playerId}"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

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
}

[System.Serializable]
public class GetPlayerResponse
{
    public bool success;
    public string message;
    public PlayerData data;
}

[System.Serializable]
public class UpdatePlayerRequest
{
    public string id;
    public int level;
    public int experience;
    public string email;
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

#endregion
