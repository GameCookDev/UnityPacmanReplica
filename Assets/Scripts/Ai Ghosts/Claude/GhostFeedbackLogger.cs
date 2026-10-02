using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Session-wide feedback recorder for the contest.
///
/// Design notes:
/// - This is a persistent singleton (DontDestroyOnLoad). That matters
///   because a "catch" calls GameManager.instance.RestartGame(), which very
///   likely reloads or resets the scene — a plain scene object would lose
///   all its counters on the very first catch. The singleton survives that.
/// - Only holds plain data (counts + lists of formatted event strings) and
///   methods to add to that data, per your spec — no game logic lives here.
/// - Writes the report in OnApplicationQuit(), which Unity also fires when
///   you press Stop/Play again to exit Play Mode in the Editor.
/// </summary>
public class GhostFeedbackLogger : MonoBehaviour
{
    public static GhostFeedbackLogger Instance { get; private set; }

    [Header("Output")]
    [SerializeField, Tooltip("Base file name. A timestamp is appended automatically so sessions don't overwrite each other.")]
    private string fileNamePrefix = "ghost_feedback";

    // --- Recorded data ---
    private int totalCatches = 0;
    private int totalGhostDeaths = 0;
    private float sessionStartTime;
    private readonly List<string> catchEvents = new List<string>();
    private readonly List<string> deathEvents = new List<string>();
    private readonly Dictionary<string, int> catchesByMode = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.time;
    }

    /// <summary>Call this whenever a ghost catches Pac-Man.</summary>
    public void RecordCatch(Vector2Int pacmanCell, string ghostName, string modeAtCatch = "")
    {
        totalCatches++;
        float elapsed = Time.time - sessionStartTime;

        string modeInfo = string.IsNullOrEmpty(modeAtCatch) ? "" : $" | Mode: {modeAtCatch}";
        catchEvents.Add($"Catch #{totalCatches} | Ghost: {ghostName} | Pac-Man Cell: {pacmanCell}{modeInfo} | Session Time: {elapsed:F2}s");

        if (!string.IsNullOrEmpty(modeAtCatch))
        {
            catchesByMode.TryGetValue(modeAtCatch, out int count);
            catchesByMode[modeAtCatch] = count + 1;
        }
    }

    /// <summary>Call this whenever a ghost is eaten (e.g. caught by Pac-Man while in RunAway state).</summary>
    public void RecordGhostDeath(Vector2Int ghostCell, string ghostName)
    {
        totalGhostDeaths++;
        float elapsed = Time.time - sessionStartTime;

        deathEvents.Add($"Death #{totalGhostDeaths} | Ghost: {ghostName} | Ghost Cell: {ghostCell} | Session Time: {elapsed:F2}s");
    }

    private void OnApplicationQuit()
    {
        WriteLogToFile();
    }

    private void WriteLogToFile()
    {
        float totalSessionTime = Time.time - sessionStartTime;
        float avgTimeBetweenCatches = totalCatches > 0 ? totalSessionTime / totalCatches : 0f;

        var sb = new StringBuilder();
        sb.AppendLine("=== Ghost AI Feedback Report ===");
        sb.AppendLine($"Date: {DateTime.Now}");
        sb.AppendLine($"Total Session Time: {totalSessionTime:F2}s");
        sb.AppendLine($"Total Catches: {totalCatches}");
        sb.AppendLine($"Total Ghost Deaths: {totalGhostDeaths}");
        sb.AppendLine($"Average Time Between Catches: {avgTimeBetweenCatches:F2}s");

        if (catchesByMode.Count > 0)
        {
            sb.Append("Catches By Mode: ");
            var parts = new List<string>();
            foreach (var kvp in catchesByMode) parts.Add($"{kvp.Key}={kvp.Value}");
            sb.AppendLine(string.Join(", ", parts));
        }
        sb.AppendLine();

        sb.AppendLine("--- Catch Events (chronological) ---");
        if (catchEvents.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (string entry in catchEvents) sb.AppendLine(entry);
        }
        sb.AppendLine();

        sb.AppendLine("--- Ghost Death Events (chronological) ---");
        if (deathEvents.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (string entry in deathEvents) sb.AppendLine(entry);
        }

        string fileName = $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = Application.dataPath + "/GhostAI Feedback Data/Claude_FeedbackData.txt";

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[GhostFeedbackLogger] Report written to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GhostFeedbackLogger] Failed to write report: {e.Message}");
        }
    }
}
