using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Networking;
using TMPro; // Use TextMeshPro
using UnityEngine.UI; // For Button slots

/// <summary>
/// Toggleable leaderboard panel using direct HTTP calls to /leaderboard.
/// Uses TextMeshPro for rendering. Optional button slots provided.
/// </summary>
public class ToggleLeaderboardUI : MonoBehaviour
{
    [Header("Backend Settings")]
    [Tooltip("Base API URL, e.g. http://localhost:5000/api")] public string serverBaseUrl = "http://localhost:5000/api";
    [Range(1,50)] public int maxEntries = 10;

    [Header("UI References")]
    public GameObject leaderboardPanel;
    public TMP_Text listText; // TMP version

    [Header("Buttons (Optional)")]
    [Tooltip("Assign to have this script wire the click automatically.")] public Button toggleButton;
    [Tooltip("Assign to have this script wire refresh click automatically.")] public Button refreshButton;

    [Header("Behavior")]
    public bool refreshOnShow = true;
    public string rowFormat = "#{0} {1} - {2}"; // rank username - score

    private bool isVisible;
    private bool requestInFlight;
    private bool _listenersWired;

    private void Awake()
    {
        // Initialize visibility state from panel active state
        if (leaderboardPanel != null)
            isVisible = leaderboardPanel.activeSelf;
        // Wire listeners in Awake so they persist even if the panel gets disabled
        WireListeners();
    }

    private void OnEnable()
    {
        // In case buttons were assigned after Awake, ensure listeners are wired
        WireListeners();
    }

    private void OnDisable()
    {
        // Do not remove toggle listeners when panel is disabled, otherwise the toggle cannot re-open the panel
        // Keep refresh removal (optional): but generally keep listeners wired until destroy
    }

    private void OnDestroy()
    {
        // Clean up listeners on destroy
        if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleLeaderboard);
        if (refreshButton != null) refreshButton.onClick.RemoveListener(RefreshLeaderboard);
        _listenersWired = false;
    }

    private void WireListeners()
    {
        if (_listenersWired) return;
        if (toggleButton != null) toggleButton.onClick.AddListener(ToggleLeaderboard);
        if (refreshButton != null) refreshButton.onClick.AddListener(RefreshLeaderboard);
        _listenersWired = true;
    }

    public void ToggleLeaderboard()
    {
        if (leaderboardPanel == null)
        {
            Debug.LogWarning("[Leaderboard] No panel assigned.");
            return;
        }
        isVisible = !isVisible;
        leaderboardPanel.SetActive(isVisible);
        if (isVisible && refreshOnShow)
            RefreshLeaderboard();
    }

    public void RefreshLeaderboard()
    {
        if (!isVisible && leaderboardPanel != null && !leaderboardPanel.activeSelf)
        {
            leaderboardPanel.SetActive(true);
            isVisible = true;
        }
        if (requestInFlight) return;
        StartCoroutine(FetchLeaderboard());
    }

    private IEnumerator FetchLeaderboard()
    {
        requestInFlight = true;
        int limit = Mathf.Clamp(maxEntries, 1, 50);
        string url = serverBaseUrl.TrimEnd('/') + $"/leaderboard?limit={limit}";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var root = JsonUtility.FromJson<LeaderboardRoot>(www.downloadHandler.text);
                if (root != null && root.success && root.data != null)
                {
                    Render(root.data);
                }
                else
                {
                    SetText("Leaderboard unavailable.");
                }
            }
            else
            {
                SetText("Error: " + www.error);
            }
        }
        requestInFlight = false;
    }

    private void Render(List<LBItem> entries)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Top Players");
        int rank = 1;
        foreach (var e in entries)
        {
            sb.AppendLine(string.Format(rowFormat, rank++, e.username, e.score));
        }
        SetText(sb.ToString());
    }

    private void SetText(string value)
    {
        if (listText != null) listText.text = value; else Debug.Log("[Leaderboard] " + value);
    }

    [System.Serializable]
    private class LeaderboardRoot { public bool success; public List<LBItem> data; }

    [System.Serializable]
    private class LBItem { public string username; public int score; }
}
