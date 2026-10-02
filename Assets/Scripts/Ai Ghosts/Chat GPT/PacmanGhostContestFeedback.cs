using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Session telemetry for the ghost contest.
///
/// The data is kept in memory while Unity is playing and written to a text
/// file when play mode/application stops. The file is deliberately human-
/// readable so it can be sent back for analysis later.
/// </summary>
public class PacmanGhostContestFeedback : MonoBehaviour
{
    public static PacmanGhostContestFeedback Instance { get; private set; }

    [Serializable]
    private class CatchRecord
    {
        public int runNumber;
        public float runTime;
        public Vector2Int pacmanCell;
        public Vector2Int ghostCell;
        public Vector3 ghostWorldPosition;
        public int lookAhead;
        public bool recentTurn;
        public Vector2Int pacmanDirection;
        public int turnCount;
        public int reversalCount;
    }

    private readonly Dictionary<int, int> decisionCountByOffset = new Dictionary<int, int>();
    private readonly Dictionary<int, float> totalDistanceByOffset = new Dictionary<int, float>();
    private readonly Dictionary<int, float> timeUsedByOffset = new Dictionary<int, float>();
    private readonly Dictionary<int, int> catchesByOffset = new Dictionary<int, int>();
    private readonly List<CatchRecord> catches = new List<CatchRecord>();

    private string ghostId = "Unknown";
    private float sessionStartTime;
    private float currentRunStartTime;
    private float lastDecisionTime;
    private bool runActive;
    private bool hasActiveDecision;
    private int activeOffset;
    private int runNumber;

    private int totalReplans;
    private int totalPacmanTurns;
    private int totalPacmanReversals;
    private int totalCatches;
    private int totalGhostDeaths;

    private float totalRunTime;
    private float minCatchTime = float.PositiveInfinity;
    private float maxCatchTime;
    private float minimumObservedDistance = float.PositiveInfinity;

    private bool fileWritten;

    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    //private static void ResetStatics()
    //{
    //    Instance = null;
    //}

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.realtimeSinceStartup;
    }

    public void BeginRun(string newGhostId)
    {
        ghostId = newGhostId;

        runNumber++;
        currentRunStartTime = Time.realtimeSinceStartup;
        lastDecisionTime = currentRunStartTime;
        runActive = true;
        hasActiveDecision = false;
        activeOffset = 0;
    }

    public void RecordDecision(string newGhostId, int lookAhead, float distanceToPacman)
    {
        ghostId = newGhostId;

        float now = Time.realtimeSinceStartup;

        if (hasActiveDecision)
        {
            float elapsed = Mathf.Max(0f, now - lastDecisionTime);
            AddToDictionary(timeUsedByOffset, activeOffset, elapsed);
        }

        hasActiveDecision = true;
        activeOffset = lookAhead;
        lastDecisionTime = now;

        totalReplans++;
        AddToDictionary(decisionCountByOffset, lookAhead, 1);
        AddToDictionary(totalDistanceByOffset, lookAhead, distanceToPacman);

        minimumObservedDistance = Mathf.Min(minimumObservedDistance, distanceToPacman);
    }

    public void RegisterPacmanTurn(string newGhostId, bool reversal)
    {
        ghostId = newGhostId;
        totalPacmanTurns++;

        if (reversal)
            totalPacmanReversals++;
    }

    public void RegisterGhostCatch(
        string newGhostId,
        Vector2Int pacmanCell,
        Vector2Int ghostCell,
        Vector3 ghostWorldPosition,
        int lookAhead,
        bool recentTurn,
        Vector2Int pacmanDirection,
        int turnCount,
        int reversalCount)
    {
        ghostId = newGhostId;

        CloseActiveDecisionSegment();

        float runTime = runActive
            ? Mathf.Max(0f, Time.realtimeSinceStartup - currentRunStartTime)
            : 0f;

        totalCatches++;
        totalRunTime += runTime;
        minCatchTime = Mathf.Min(minCatchTime, runTime);
        maxCatchTime = Mathf.Max(maxCatchTime, runTime);

        AddToDictionary(catchesByOffset, lookAhead, 1);

        catches.Add(new CatchRecord
        {
            runNumber = runNumber,
            runTime = runTime,
            pacmanCell = pacmanCell,
            ghostCell = ghostCell,
            ghostWorldPosition = ghostWorldPosition,
            lookAhead = lookAhead,
            recentTurn = recentTurn,
            pacmanDirection = pacmanDirection,
            turnCount = turnCount,
            reversalCount = reversalCount
        });

        runActive = false;
        hasActiveDecision = false;
    }

    /// <summary>
    /// The base GhostAI has no ghost-death callback, so your death code can call this.
    /// </summary>
    public void RegisterGhostDeath(string newGhostId)
    {
        ghostId = newGhostId;
        totalGhostDeaths++;
    }

    private void CloseActiveDecisionSegment()
    {
        if (!hasActiveDecision)
            return;

        float now = Time.realtimeSinceStartup;
        float elapsed = Mathf.Max(0f, now - lastDecisionTime);
        AddToDictionary(timeUsedByOffset, activeOffset, elapsed);

        hasActiveDecision = false;
    }

    private void OnApplicationQuit()
    {
        WriteFeedbackFile();
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        // This catches editor Play Mode being stopped as well as normal application shutdown.
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            WriteFeedbackFile();
    }
#endif

    private void WriteFeedbackFile()
    {
        if (fileWritten)
            return;

        fileWritten = true;
        CloseActiveDecisionSegment();

        float sessionDuration = Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartTime);
        float averageCatchTime = totalCatches > 0 ? totalRunTime / totalCatches : 0f;
        float activeRunTime = runActive
            ? Mathf.Max(0f, Time.realtimeSinceStartup - currentRunStartTime)
            : 0f;

        string path = GetOutputPath();
        string directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        StringBuilder output = new StringBuilder(16 * 1024);

        output.AppendLine("PACMAN GHOST CONTEST FEEDBACK");
        output.AppendLine("================================");
        output.AppendLine("Ghost: " + ghostId);
        output.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        output.AppendLine("Session duration (s): " + F(sessionDuration));
        output.AppendLine();

        output.AppendLine("SESSION TOTALS");
        output.AppendLine("-------------");
        output.AppendLine("Runs started: " + runNumber);
        output.AppendLine("Pacman catches: " + totalCatches);
        output.AppendLine("Ghost deaths reported: " + totalGhostDeaths);
        output.AppendLine("Path replans: " + totalReplans);
        output.AppendLine("Pacman turns: " + totalPacmanTurns);
        output.AppendLine("Pacman reversals: " + totalPacmanReversals);
        output.AppendLine("Minimum observed distance: " + F(SafeMinimum(minimumObservedDistance)));
        output.AppendLine("Average catch time (s): " + F(averageCatchTime));
        output.AppendLine("Fastest catch (s): " + (totalCatches > 0 ? F(minCatchTime) : "N/A"));
        output.AppendLine("Slowest catch (s): " + (totalCatches > 0 ? F(maxCatchTime) : "N/A"));
        output.AppendLine("Active run time at shutdown (s): " + F(activeRunTime));
        output.AppendLine();

        output.AppendLine("LOOK-AHEAD PERFORMANCE");
        output.AppendLine("----------------------");
        output.AppendLine("Offset | Decisions | Avg Distance | Time Used (s) | Catches");

        List<int> offsets = new List<int>(decisionCountByOffset.Keys);
        offsets.Sort();

        for (int i = 0; i < offsets.Count; i++)
        {
            int offset = offsets[i];
            int decisions = GetDictionaryValue(decisionCountByOffset, offset);
            float distanceSum = GetDictionaryValue(totalDistanceByOffset, offset);
            float timeUsed = GetDictionaryValue(timeUsedByOffset, offset);
            int catchesForOffset = GetDictionaryValue(catchesByOffset, offset);

            float averageDistance = decisions > 0 ? distanceSum / decisions : 0f;

            output.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,6} | {1,9} | {2,12:0.000} | {3,14:0.000} | {4,7}",
                    offset,
                    decisions,
                    averageDistance,
                    timeUsed,
                    catchesForOffset));
        }

        output.AppendLine();
        output.AppendLine("CATCH EVENTS");
        output.AppendLine("------------");

        if (catches.Count == 0)
        {
            output.AppendLine("No Pacman catches were recorded.");
        }
        else
        {
            for (int i = 0; i < catches.Count; i++)
            {
                CatchRecord record = catches[i];

                output.AppendLine("Catch #" + (i + 1));
                output.AppendLine("  Run: " + record.runNumber);
                output.AppendLine("  Time to catch (s): " + F(record.runTime));
                output.AppendLine("  Pacman cell: " + record.pacmanCell);
                output.AppendLine("  Ghost cell: " + record.ghostCell);
                output.AppendLine("  Ghost world position: " + record.ghostWorldPosition);
                output.AppendLine("  Selected look-ahead: " + record.lookAhead);
                output.AppendLine("  Pacman was recently turning: " + record.recentTurn);
                output.AppendLine("  Pacman direction: " + record.pacmanDirection);
                output.AppendLine("  Pacman turns in run: " + record.turnCount);
                output.AppendLine("  Pacman reversals in run: " + record.reversalCount);
                output.AppendLine();
            }
        }

        output.AppendLine("OUTPUT PATH");
        output.AppendLine("-----------");
        output.AppendLine(path);

        try
        {
            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            Debug.Log("Pacman ghost contest feedback written to: " + path);
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to write Pacman ghost contest feedback: " + exception);
        }
    }

    private string GetOutputPath()
    {
#if UNITY_EDITOR
        return Application.dataPath + "/GhostAI Feedback Data/ChatGPT_FeedbackData.txt";
#else
        return Path.Combine(Application.persistentDataPath, "GhostContestFeedback.txt");
#endif
    }

    private static void AddToDictionary(Dictionary<int, int> dictionary, int key, int amount)
    {
        if (dictionary.TryGetValue(key, out int current))
            dictionary[key] = current + amount;
        else
            dictionary.Add(key, amount);
    }

    private static void AddToDictionary(Dictionary<int, float> dictionary, int key, float amount)
    {
        if (dictionary.TryGetValue(key, out float current))
            dictionary[key] = current + amount;
        else
            dictionary.Add(key, amount);
    }

    private static int GetDictionaryValue(Dictionary<int, int> dictionary, int key)
    {
        return dictionary.TryGetValue(key, out int value) ? value : 0;
    }

    private static float GetDictionaryValue(Dictionary<int, float> dictionary, int key)
    {
        return dictionary.TryGetValue(key, out float value) ? value : 0f;
    }

    private static float SafeMinimum(float value)
    {
        return float.IsPositiveInfinity(value) ? 0f : value;
    }

    private static string F(float value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
