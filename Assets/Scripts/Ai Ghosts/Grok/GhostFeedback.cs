using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class GhostFeedback : MonoBehaviour
{
    public static GhostFeedback Instance { get; private set; }

    public int totalCatches;
    public int totalDeaths;               // times this ghost was eaten
    public float totalPlayTime;
    public float totalTimeInChase;
    public float totalTimeInFlee;

    public List<EventRecord> eventHistory = new List<EventRecord>();

    [System.Serializable]
    public class EventRecord
    {
        public string type;               // "Catch" or "Death"
        public string ghostName;
        public Vector2Int pacmanCell;
        public float timeAlive;
        public string modeUsed;           // only meaningful on Catch
        public float timeInChase;
        public float timeInFlee;
        public float realtimeSinceStartup;
    }

    private float sessionStart;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStart = Time.realtimeSinceStartup;
    }

    public void RegisterGhost(InterceptorGhostAI ghost)
    {
        Debug.Log($"[GhostFeedback] Registered {ghost.name}");
    }

    public void RecordCatch(InterceptorGhostAI ghost, Vector2Int pacmanCell,
                            float timeAlive, string mode,
                            float timeInChase, float timeInFlee)
    {
        totalCatches++;
        totalTimeInChase += timeInChase;
        totalTimeInFlee += timeInFlee;

        eventHistory.Add(new EventRecord
        {
            type = "Catch",
            ghostName = ghost.name,
            pacmanCell = pacmanCell,
            timeAlive = timeAlive,
            modeUsed = mode,
            timeInChase = timeInChase,
            timeInFlee = timeInFlee,
            realtimeSinceStartup = Time.realtimeSinceStartup
        });
    }

    public void RecordDeath(InterceptorGhostAI ghost, Vector2Int pacmanCell,
                            float timeAlive, float timeInChase, float timeInFlee)
    {
        totalDeaths++;
        totalTimeInChase += timeInChase;
        totalTimeInFlee += timeInFlee;

        eventHistory.Add(new EventRecord
        {
            type = "Death",
            ghostName = ghost.name,
            pacmanCell = pacmanCell,
            timeAlive = timeAlive,
            modeUsed = "Flee",
            timeInChase = timeInChase,
            timeInFlee = timeInFlee,
            realtimeSinceStartup = Time.realtimeSinceStartup
        });
    }

    private void OnApplicationQuit()
    {
        totalPlayTime = Time.realtimeSinceStartup - sessionStart;
        WriteFeedbackFile();
    }

    public void ForceWrite()
    {
        totalPlayTime = Time.realtimeSinceStartup - sessionStart;
        WriteFeedbackFile();
    }

    private void WriteFeedbackFile()
    {
        string path = Application.dataPath + "/GhostAI Feedback Data/Grok_FeedbackData.txt";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Ghost AI Feedback Log (Interceptor) ===");
        sb.AppendLine($"Generated: {System.DateTime.Now}");
        sb.AppendLine($"Total catches: {totalCatches}");
        sb.AppendLine($"Total deaths (eaten): {totalDeaths}");
        sb.AppendLine($"Total play time (s): {totalPlayTime:F1}");
        sb.AppendLine($"Accumulated time in Chase: {totalTimeInChase:F1}s");
        sb.AppendLine($"Accumulated time in Flee:  {totalTimeInFlee:F1}s");
        sb.AppendLine();
        sb.AppendLine("--- Event History ---");

        for (int i = 0; i < eventHistory.Count; i++)
        {
            var r = eventHistory[i];
            sb.AppendLine($"{i + 1}. [{r.type}] {r.ghostName} | Pac cell: {r.pacmanCell} | " +
                          $"Alive: {r.timeAlive:F1}s | Mode: {r.modeUsed} | " +
                          $"Chase: {r.timeInChase:F1}s | Flee: {r.timeInFlee:F1}s | " +
                          $"Realtime: {r.realtimeSinceStartup:F1}");
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[GhostFeedback] Written → {path}");
    }
}