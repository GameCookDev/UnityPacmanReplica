using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contest ghost AI: "The Interceptor".
///
/// Strategy in one sentence: chase Pac-Man directly when close, but predict
/// and cut him off from a distance, switching between the two with
/// hysteresis so the ghost doesn't flip-flop mid-path.
///
/// WHY this strategy:
/// - A pure "always chase pacman's current cell" ghost (classic Blinky) is
///   easy for a human player to read and juke around corners.
/// - A pure "always aim X cells ahead of pacman" ghost (classic Pinky) is
///   strong at range but overshoots / whiffs once it gets close, because the
///   lead cell can end up behind or past the player.
/// - Combining both and switching based on distance gets the strengths of
///   each: long-range interception to cut off escape routes, short-range
///   precision to actually close the catch.
///
/// IMPLEMENTATION NOTE on GhostAI's private fields:
/// firstUpdateDelay, updateRate and updateCountDown are declared `private`
/// in GhostAI (not `protected`), so this subclass cannot read or modify
/// them in code. That's fine — they're still [SerializeField], so Unity's
/// Inspector still shows and serializes them per-ghost-instance; just tune
/// "Update Rate" directly on this ghost's prefab/inspector instead of
/// touching it from code. Because of this, Tick() is intentionally left
/// un-overridden here — only UpdateAgentPath() (which IS protected virtual)
/// is overridden, since that's the actual decision-making hook.
/// </summary>
public class ClaudeInterceptorGhostAI : GhostAI
{
    private enum PursuitMode
    {
        Chase,   // Head straight for Pac-Man's actual cell.
        Ambush   // Head for Pac-Man's cell + goalOffset (predicted lead cell).
    }

    [Header("Interceptor Settings (Chase state)")]
    [SerializeField, Tooltip("Grid (Manhattan) distance to Pac-Man at or below which the ghost switches to direct Chase mode.")]
    private int chaseDistanceThreshold = 4;

    [SerializeField, Tooltip("Grid (Manhattan) distance to Pac-Man at or above which the ghost switches to predictive Ambush mode. Must be greater than chaseDistanceThreshold to create a hysteresis band.")]
    private int ambushDistanceThreshold = 8;

    [Header("Feedback Logging")]
    [SerializeField, Tooltip("Optional explicit reference. If left empty, this ghost will use GhostFeedbackLogger.Instance at runtime.")]
    private GhostFeedbackLogger feedbackLogger;

    [SerializeField, Tooltip("Name used to identify this ghost inside the feedback log file.")]
    private string ghostDisplayName = "Interceptor";

    private PursuitMode currentMode = PursuitMode.Ambush;
    private int distanceToPacman;

    protected override void InitializeAgent()
    {
        base.InitializeAgent();

        // Fall back to the session-wide singleton logger if no explicit
        // reference was dragged into the Inspector. Safe to read here since
        // Unity guarantees all Awake() calls (including
        // GhostFeedbackLogger's) run before any Start() call.
        if (feedbackLogger == null)
        {
            feedbackLogger = GhostFeedbackLogger.Instance;
        }

        // Reset pursuit mode on every (re)spawn, including after being eaten
        // during RunAway — start each life fresh in Ambush.
        currentMode = PursuitMode.Ambush;
    }

    protected override void UpdateAgentPath()
    {
        if (ghostState == GhostStates.chase)
        {
            RunChaseLogic();
        }
        else if (ghostState == GhostStates.runAway)
        {
            Vector2Int pacmanCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
            goal = ChooseFleeCorner(pacmanCell);
        }
        else
        {
            // GhostStates.none — mirrors the base class exactly: don't path,
            // just sit on the current cell. Reached when paused or on the
            // brief window between spawning and the first ChangeState call.
            destinationsList = new List<GridCellKVP>
            {
                new GridCellKVP { gridIndex = currentCell, gridPosition = GridManager.instance.GetGridPosition(currentCell) }
            };
            return;
        }

        destinationsList = AStarPathFinding.instance.FindPath(currentCell, goal);

        // Same "which node to resume from" smoothing as the base class —
        // kept identical on purpose so movement doesn't visibly stutter
        // every time a fresh path is calculated.
        if (destinationsList.Count > 1)
        {
            if (Vector2.Distance(transform.position, destinationsList[1].gridPosition) >=
                Vector2.Distance(destinationsList[0].gridPosition, destinationsList[1].gridPosition))
            {
                currentDestinationIndex = 0;
            }
            else
            {
                currentDestinationIndex = 1;
            }
        }
        else
        {
            currentDestinationIndex = 0;
        }
    }

    /// <summary>
    /// Distance-based hysteresis chase strategy (unchanged from the pre-power-pellet version).
    /// </summary>
    protected virtual void RunChaseLogic()
    {
        Vector2Int pacmanCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
        distanceToPacman = Mathf.Abs(pacmanCell.x - currentCell.x) + Mathf.Abs(pacmanCell.y - currentCell.y);

        if (currentMode == PursuitMode.Ambush && distanceToPacman <= chaseDistanceThreshold)
        {
            currentMode = PursuitMode.Chase;
        }
        else if (currentMode == PursuitMode.Chase && distanceToPacman >= ambushDistanceThreshold)
        {
            currentMode = PursuitMode.Ambush;
        }

        Vector2Int offsetToUse = currentMode == PursuitMode.Chase ? Vector2Int.zero : goalOffset;
        goal = pacman.GetCellBasedOnOffset(offsetToUse);
    }

    /// <summary>
    /// Flee strategy: always target whichever grid corner is farthest
    /// (Manhattan distance) from Pac-Man's current cell, tie-broken
    /// randomly. This is a stricter/safer version of the base class's
    /// "pick a random corner that isn't the single closest one" — a corner
    /// that's merely not-the-closest can still be near enough that the
    /// flee path crosses Pac-Man's position. Maximizing distance minimizes
    /// that risk, which is exactly what "dies the least" rewards.
    /// </summary>
    protected virtual Vector2Int ChooseFleeCorner(Vector2Int pacmanCell)
    {
        Vector2Int[] corners = GridManager.instance.gridCorners;

        if (corners == null || corners.Length == 0)
        {
            return currentCell; // no corners configured — safest fallback is to stay put
        }

        int bestDistance = -1;
        List<Vector2Int> farthestCorners = new List<Vector2Int>();

        foreach (Vector2Int corner in corners)
        {
            int dist = Mathf.Abs(corner.x - pacmanCell.x) + Mathf.Abs(corner.y - pacmanCell.y);

            if (dist > bestDistance)
            {
                bestDistance = dist;
                farthestCorners.Clear();
                farthestCorners.Add(corner);
            }
            else if (dist == bestDistance)
            {
                farthestCorners.Add(corner);
            }
        }

        return farthestCorners[Random.Range(0, farthestCorners.Count)];
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && feedbackLogger != null)
        {
            // Mirrors the base class's own branch exactly: chase == Pac-Man
            // catches nothing, this ghost catches him; anything else
            // (runAway, or the rare none-state edge case) means this ghost
            // is the one that gets eaten.
            if (ghostState == GhostStates.chase)
            {
                Vector2Int pacmanCellAtCatch = pacman.GetCellBasedOnOffset(Vector2Int.zero);
                feedbackLogger.RecordCatch(pacmanCellAtCatch, ghostDisplayName, currentMode.ToString());
            }
            else
            {
                feedbackLogger.RecordGhostDeath(currentCell, ghostDisplayName);
            }
        }

        // Preserve the actual game behaviour (RestartGame() or respawn).
        base.OnTriggerEnter2D(collision);
    }

}
