using UnityEngine;
using System.IO;
using System.Text;

public class GhostTelemetry : MonoBehaviour
{
    public static GhostTelemetry Instance { get; private set; }

    private int catchCount = 0;
    private int deathCount = 0;
    private float sessionStartTime;
    private StringBuilder eventLog = new StringBuilder();

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

    public void RecordCatch(Vector2Int pacmanLocation, Vector2Int ghostLocation)
    {
        catchCount++;
        float timestamp = Time.time - sessionStartTime;
        eventLog.AppendLine($"[Time: {timestamp:F1}s] CATCH: Pacman caught at grid {pacmanLocation} (Ghost was at {ghostLocation})");
    }

    public void RecordDeath(Vector2Int pacmanLocation, Vector2Int ghostLocation)
    {
        deathCount++;
        float timestamp = Time.time - sessionStartTime;
        eventLog.AppendLine($"[Time: {timestamp:F1}s] DEATH: Ghost eaten at grid {ghostLocation} (Pacman was at {pacmanLocation})");
    }

    private void OnApplicationQuit()
    {
        SaveTelemetryReport();
    }

    private void SaveTelemetryReport()
    {
        float totalTime = Time.time - sessionStartTime;
        string filePath = Application.dataPath + "/GhostAI Feedback Data/Gemini_FeedbackData.txt";

        StringBuilder report = new StringBuilder();

        if (File.Exists(filePath))
        {
            report.AppendLine("\n--------------------------------------------------\n");
        }

        report.AppendLine("=== INTERCEPTOR GHOST AI PERFORMANCE REPORT ===");
        report.AppendLine($"Total Session Time: {totalTime:F1} seconds");
        report.AppendLine($"Total Pacman Catches: {catchCount}");
        report.AppendLine($"Total Ghost Deaths: {deathCount}");

        float ratio = deathCount == 0 ? catchCount : (float)catchCount / deathCount;
        report.AppendLine($"Catch-to-Death Ratio: {ratio:F2}");

        report.AppendLine("\n=== EVENT TIMELINE ===");
        if (eventLog.Length > 0)
        {
            report.Append(eventLog.ToString());
        }
        else
        {
            report.AppendLine("No catches or deaths recorded in this session.");
        }

        File.AppendAllText(filePath, report.ToString());
        Debug.Log($"[Telemetry] Match report appended to: {filePath}");
    }
}