using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InterceptorGhostAI : GhostAI
{
    [Header("Interceptor – Chase Tuning")]
    [SerializeField] private float closeDistanceThreshold = 5f;
    [SerializeField] private float mediumDistanceThreshold = 11f;
    [SerializeField] private int maxLookAhead = 5;
    [SerializeField] private float modeSwitchCooldown = 0.45f;

    [Header("Interceptor – Flee Tuning")]
    [SerializeField] private bool preferFarthestCorner = true;
    [SerializeField] private float fleeBiasStrength = 2.5f;   // how strongly we push away from Pac-Man facing

    private float modeSwitchTimer;
    private enum HuntMode { Direct, Ahead, CutOff }
    private HuntMode currentMode = HuntMode.Direct;

    // Runtime stats for feedback
    private Vector2Int lastKnownPacmanCell;
    private float sessionStartTime;
    private float timeInChase;
    private float timeInFlee;
    private float lastStateChangeTime;

    protected override void InitializeAgent()
    {
        base.InitializeAgent();
        sessionStartTime = Time.time;
        lastStateChangeTime = Time.time;
        modeSwitchTimer = 0f;
        timeInChase = 0f;
        timeInFlee = 0f;

        if (GhostFeedback.Instance != null)
            GhostFeedback.Instance.RegisterGhost(this);
    }

    protected override void Tick()
    {
        // Let the base class respect ghostState == none and the updateRate
        base.Tick();

        // Accumulate time in current state (only while the game is actually running)
        if (ghostState == GhostStates.chase)
            timeInChase += Time.deltaTime;
        else if (ghostState == GhostStates.runAway)
            timeInFlee += Time.deltaTime;

        if (ghostState != GhostStates.chase) return;

        modeSwitchTimer -= Time.deltaTime;
        if (modeSwitchTimer <= 0f)
        {
            DecideHuntMode();
            modeSwitchTimer = modeSwitchCooldown;
        }
    }

    protected override void UpdateAgentPath()
    {
        if (pacman == null) return;

        lastKnownPacmanCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);

        if (ghostState == GhostStates.chase)
        {
            Vector2Int targetOffset = Vector2Int.zero;
            float dist = Vector2Int.Distance(currentCell, lastKnownPacmanCell);

            switch (currentMode)
            {
                case HuntMode.Direct:
                    targetOffset = Vector2Int.zero;
                    break;

                case HuntMode.Ahead:
                    int lookAhead = Mathf.Clamp(Mathf.RoundToInt(dist * 0.5f), 1, maxLookAhead);
                    targetOffset = GetFacingOffset(pacman) * lookAhead;
                    break;

                case HuntMode.CutOff:
                    Vector2Int facing = GetFacingOffset(pacman);
                    // Perpendicular + a little bit forward so we cut the lane
                    targetOffset = new Vector2Int(-facing.y, facing.x) * 3 + facing * 2;
                    if (Random.value > 0.55f)
                        targetOffset = new Vector2Int(facing.y, -facing.x) * 3 + facing * 2;
                    break;
            }

            goal = pacman.GetCellBasedOnOffset(targetOffset);
        }
        else if (ghostState == GhostStates.runAway)
        {
            // Smarter flee: farthest corner that is not the one closest to Pac-Man,
            // with a bias away from Pac-Man’s current facing.
            goal = ChooseBestFleeCorner();
        }
        else
        {
            // none / paused – base already handles this, but we keep the list safe
            destinationsList = new List<GridCellKVP>
            {
                new GridCellKVP
                {
                    gridIndex = currentCell,
                    gridPosition = GridManager.instance.GetGridPosition(currentCell)
                }
            };
            return;
        }

        destinationsList = AStarPathFinding.instance.FindPath(currentCell, goal);

        // Same destination-index logic the base class uses
        if (destinationsList.Count > 1)
        {
            if (Vector2.Distance(transform.position, destinationsList[1].gridPosition) >=
                Vector2.Distance(destinationsList[0].gridPosition, destinationsList[1].gridPosition))
                currentDestinationIndex = 0;
            else
                currentDestinationIndex = 1;
        }
        else
        {
            currentDestinationIndex = 0;
        }
    }

    private Vector2Int ChooseBestFleeCorner()
    {
        Vector2Int closestCorner = pacman.GetClosestCorner();
        List<Vector2Int> candidates = GridManager.instance.gridCorners.ToList();

        if (candidates.Contains(closestCorner))
            candidates.Remove(closestCorner);

        if (candidates.Count == 0)
            return closestCorner; // safety

        if (!preferFarthestCorner)
            return candidates[Random.Range(0, candidates.Count)];

        // Score = distance from Pac-Man + bias away from his facing
        Vector2Int pacFacing = GetFacingOffset(pacman);
        Vector2Int best = candidates[0];
        float bestScore = float.MinValue;

        foreach (var corner in candidates)
        {
            float distScore = Vector2Int.Distance(corner, lastKnownPacmanCell);
            // Prefer corners that lie opposite to Pac-Man’s movement direction
            float bias = -Vector2.Dot((Vector2)(corner - lastKnownPacmanCell),
                                      (Vector2)pacFacing) * fleeBiasStrength;
            float score = distScore + bias;

            if (score > bestScore)
            {
                bestScore = score;
                best = corner;
            }
        }

        return best;
    }

    private void DecideHuntMode()
    {
        if (pacman == null) return;

        float dist = Vector2Int.Distance(currentCell, lastKnownPacmanCell);

        if (dist <= closeDistanceThreshold)
            currentMode = HuntMode.Direct;
        else if (dist <= mediumDistanceThreshold)
            currentMode = HuntMode.Ahead;
        else
            currentMode = HuntMode.CutOff;
    }

    // Tries the most common field / property names students use for facing
    private Vector2Int GetFacingOffset(PacmanController p)
    {
        if (p == null) return goalOffset;

        var t = p.GetType();
        var field = t.GetField("facingDirection")
                 ?? t.GetField("currentDirection")
                 ?? t.GetField("moveDirection")
                 ?? t.GetField("direction");

        if (field != null)
        {
            object val = field.GetValue(p);
            if (val is Vector2 v2) return new Vector2Int(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
            if (val is Vector2Int v2i) return v2i;
        }

        // Property fallback
        var prop = t.GetProperty("Facing") ?? t.GetProperty("Direction") ?? t.GetProperty("CurrentDirection");
        if (prop != null)
        {
            object val = prop.GetValue(p);
            if (val is Vector2 v2) return new Vector2Int(Mathf.RoundToInt(v2.x), Mathf.RoundToInt(v2.y));
            if (val is Vector2Int v2i) return v2i;
        }

        return goalOffset; // last resort
    }

    public override void ChangeState(GameState gameState)
    {
        // Let the base class do the official state + visual switch
        base.ChangeState(gameState);

        // Reset mode timer so we re-evaluate quickly after a power-pellet ends
        modeSwitchTimer = 0f;
        lastStateChangeTime = Time.time;
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (ghostState == GhostStates.chase)
        {
            // We caught Pac-Man
            if (GhostFeedback.Instance != null)
            {
                GhostFeedback.Instance.RecordCatch(
                    this,
                    lastKnownPacmanCell,
                    Time.time - sessionStartTime,
                    currentMode.ToString(),
                    timeInChase,
                    timeInFlee
                );
            }
            base.OnTriggerEnter2D(collision); // RestartGame
        }
        else if (ghostState == GhostStates.runAway)
        {
            // We got eaten
            if (GhostFeedback.Instance != null)
            {
                GhostFeedback.Instance.RecordDeath(
                    this,
                    lastKnownPacmanCell,
                    Time.time - sessionStartTime,
                    timeInChase,
                    timeInFlee
                );
            }
            base.OnTriggerEnter2D(collision); // InitializeAgent (respawn)
        }
    }

    // Public helpers for the feedback system
    public string GetCurrentMode() => currentMode.ToString();
    public Vector2Int GetLastPacmanCell() => lastKnownPacmanCell;
    public float GetSessionTime() => Time.time - sessionStartTime;
    public float GetTimeInChase() => timeInChase;
    public float GetTimeInFlee() => timeInFlee;
}