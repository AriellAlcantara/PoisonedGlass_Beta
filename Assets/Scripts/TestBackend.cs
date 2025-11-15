using UnityEngine;
using System.Collections;

// Simple test runner: registers then logs in, printing results to Console.
public class TestBackend : MonoBehaviour
{
    public string testUsername = "testuser";
    public string testPassword = "testpass";
    public string testEmail = "test@example.com";

    private IEnumerator Start()
    {
        // Wait a frame for singletons to initialize
        yield return null;

        if (PlayerManager.instance == null)
        {
            Debug.LogError("PlayerManager.instance is null - make sure PlayerManager is in the scene and active.");
            yield break;
        }

        if (GameBackendManager.instance == null)
        {
            Debug.LogError("GameBackendManager.instance is null - make sure GameBackendManager is in the scene and active.");
            yield break;
        }

        Debug.Log($"[TestBackend] Starting test for user: {testUsername}");

        bool finished = false;

        // Register
        PlayerManager.instance.Register(testUsername, testPassword, testEmail, (success, message) =>
        {
            Debug.Log($"[TestBackend] Register callback - success={success}, message={message}");
            finished = true;
        });

        while (!finished) yield return null;

        // small delay
        yield return new WaitForSeconds(0.2f);

        finished = false;

        // Login
        PlayerManager.instance.Login(testUsername, testPassword, (success, message) =>
        {
            Debug.Log($"[TestBackend] Login callback - success={success}, message={message}");
            finished = true;
        });

        while (!finished) yield return null;

        // small delay
        yield return new WaitForSeconds(0.2f);

        // Read current player
        var player = PlayerManager.instance.GetCurrentPlayer();
        if (player != null)
        {
            Debug.Log($"[TestBackend] Current player loaded: {player.username} (level:{player.level}, xp:{player.experience})");
        }
        else
        {
            Debug.LogWarning("[TestBackend] Current player is null after login");
        }

        // Show PlayerPrefs keys
        Debug.Log($"[TestBackend] PlayerPrefs CurrentUser: {PlayerPrefs.GetString("CurrentUser", "(none)")}");
        Debug.Log($"[TestBackend] PlayerPrefs player_id: {PlayerPrefs.GetString("player_id", "(none)")}");

        // Done
        Debug.Log("[TestBackend] Test finished.");
    }
}
