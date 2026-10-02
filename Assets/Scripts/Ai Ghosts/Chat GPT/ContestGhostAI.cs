using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A contest-oriented GhostAI that tries to intercept Pacman instead of
/// simply targeting his current cell.
///
/// It evaluates several cells in front of Pacman, estimates how long Pacman
/// will take to reach each one, estimates how long this ghost will take to
/// reach it, and picks the most plausible interception point.
/// </summary>
public class ContestGhostAI : GhostAI
{
    [Header("Contest Planning")]
    [SerializeField, Min(1f)] private float planningRate = 8f;
    [SerializeField, Min(0f)] private float firstPlanningDelay = 0.05f;

    [Tooltip("Fallback time for Pacman to move one grid cell before enough data has been collected.")]
    [SerializeField, Min(0.01f)] private float fallbackPacmanCellTime = 0.18f;

    [Tooltip("Fallback world-units-per-second for this ghost before its real movement speed can be measured.")]
    [SerializeField, Min(0.01f)] private float fallbackGhostSpeed = 3f;

    [Tooltip("We try to arrive roughly this many seconds before Pacman reaches the predicted target.")]
    [SerializeField, Min(0f)] private float desiredInterceptLead = 0.12f;

    [Tooltip("A little lateness is acceptable. Larger lateness gets strongly penalized.")]
    [SerializeField, Min(0f)] private float lateArrivalTolerance = 0.35f;

    [Tooltip("After Pacman turns, use shorter predictions because long predictions become unreliable.")]
    [SerializeField, Min(0f)] private float recentTurnWindow = 0.55f;

    [Tooltip("Maximum world-space distance at which the ghost switches to very short predictions.")]
    [SerializeField, Min(0f)] private float closeDistance = 3.5f;

    [Tooltip("How long to wait before considering a path to a farther target again if it is invalid.")]
    [SerializeField] private int[] normalLookAhead = { 0, 2, 4, 6, 8 };

    [SerializeField] private int[] recentTurnLookAhead = { -2, 0, 1, 2, 3 };
    [SerializeField] private int[] closeRangeLookAhead = { -1, 0, 1, 2 };

    [Header("Feedback")]
    [SerializeField] private string contestGhostId = "AdaptiveInterceptor";

    private float planningTimer;

    private bool hasPacmanSample;
    private Vector2Int lastPacmanCell;
    private Vector2Int lastPacmanDirection;
    private float lastPacmanCellChangeTime;
    private float averagePacmanCellTime;
    private float recentTurnTimer;
    private int pacmanTurnCount;
    private int pacmanReversalCount;

    private Vector3 lastGhostPosition;
    private float ghostSpeedEstimate;

    private int lastChosenLookAhead;
    private float lastCatchRecordTime = -10f;

    protected override void InitializeAgent()
    {
        base.InitializeAgent();

        planningTimer = firstPlanningDelay;
        averagePacmanCellTime = fallbackPacmanCellTime;
        ghostSpeedEstimate = fallbackGhostSpeed;
        lastGhostPosition = transform.position;
        lastChosenLookAhead = 0;

        if (PacmanGhostContestFeedback.Instance != null)
            PacmanGhostContestFeedback.Instance.BeginRun(contestGhostId);
    }

    protected override void Tick()
    {
        float deltaTime = Time.unscaledDeltaTime;

        UpdateMovementEstimates(deltaTime);
        UpdatePacmanPredictionState(deltaTime);

        planningTimer -= deltaTime;

        if (planningTimer <= 0f || destinationsList.Count <= currentDestinationIndex)
        {
            UpdateAgentPath();
            planningTimer = 1f / Mathf.Max(1f, planningRate);
        }
    }

    /// <summary>
    /// Measures the actual movement of both actors so the interceptor can
    /// make timing decisions instead of using a hard-coded prediction only.
    /// </summary>
    private void UpdateMovementEstimates(float deltaTime)
    {
        if (deltaTime <= Mathf.Epsilon)
            return;

        float ghostDistanceMoved = Vector2.Distance(transform.position, lastGhostPosition);
        if (ghostDistanceMoved > 0.0001f)
        {
            float measuredSpeed = ghostDistanceMoved / deltaTime;
            ghostSpeedEstimate = Mathf.Lerp(ghostSpeedEstimate, measuredSpeed, 0.15f);
        }

        lastGhostPosition = transform.position;
    }

    /// <summary>
    /// Infers Pacman's facing direction from GetCellBasedOnOffset((1, 0)).
    /// We also learn how long Pacman normally takes to cross one cell.
    /// </summary>
    private void UpdatePacmanPredictionState(float deltaTime)
    {
        if (pacman == null)
            return;

        Vector2Int pacmanCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
        Vector2Int forwardCell = pacman.GetCellBasedOnOffset(Vector2Int.right);
        Vector2Int facing = GetUnitDirection(forwardCell - pacmanCell);

        float now = Time.realtimeSinceStartup;

        if (!hasPacmanSample)
        {
            hasPacmanSample = true;
            lastPacmanCell = pacmanCell;
            lastPacmanDirection = facing;
            lastPacmanCellChangeTime = now;
            return;
        }

        if (pacmanCell != lastPacmanCell)
        {
            float cellTravelTime = now - lastPacmanCellChangeTime;

            if (cellTravelTime > 0.03f && cellTravelTime < 2f)
            {
                averagePacmanCellTime = Mathf.Lerp(
                    averagePacmanCellTime,
                    cellTravelTime,
                    0.20f);
            }

            lastPacmanCell = pacmanCell;
            lastPacmanCellChangeTime = now;
        }

        if (facing != Vector2Int.zero)
        {
            if (lastPacmanDirection != Vector2Int.zero && facing != lastPacmanDirection)
            {
                pacmanTurnCount++;
                recentTurnTimer = recentTurnWindow;

                int dot = lastPacmanDirection.x * facing.x + lastPacmanDirection.y * facing.y;
                if (dot < 0)
                    pacmanReversalCount++;

                if (PacmanGhostContestFeedback.Instance != null)
                    PacmanGhostContestFeedback.Instance.RegisterPacmanTurn(contestGhostId, dot < 0);
            }

            // Also initialize/update the facing direction when the first valid
            // direction sample arrives.
            lastPacmanDirection = facing;
        }

        if (recentTurnTimer > 0f)
            recentTurnTimer -= deltaTime;
    }

    protected override void UpdateAgentPath()
    {
        if (pacman == null || AStarPathFinding.instance == null)
            return;

        Vector2Int pacmanCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
        float distanceToPacman = Vector2.Distance(transform.position, pacman.transform.position);

        int[] candidates = ChooseCandidateOffsets(distanceToPacman);

        List<GridCellKVP> bestPath = null;
        Vector2Int bestGoal = pacmanCell;
        int bestLookAhead = 0;
        float bestScore = float.PositiveInfinity;

        HashSet<Vector2Int> testedGoals = new HashSet<Vector2Int>();

        for (int i = 0; i < candidates.Length; i++)
        {
            int lookAhead = candidates[i];
            Vector2Int offset = new Vector2Int(lookAhead, 0);
            Vector2Int candidateGoal = pacman.GetCellBasedOnOffset(offset);

            // If a positive/negative prediction collapses back onto Pacman's
            // current cell, it usually means we hit the edge of the valid grid.
            if (lookAhead != 0 && candidateGoal == pacmanCell)
                continue;

            if (!testedGoals.Add(candidateGoal))
                continue;

            List<GridCellKVP> candidatePath =
                AStarPathFinding.instance.FindPath(currentCell, candidateGoal);

            if (candidatePath == null || candidatePath.Count == 0)
                continue;

            float ghostTravelDistance = GetPathWorldDistance(candidatePath);
            float ghostTravelTime = ghostTravelDistance / Mathf.Max(0.01f, ghostSpeedEstimate);

            // GetCellBasedOnOffset uses the offset relative to Pacman's facing,
            // so the magnitude is a useful first-order estimate of how many
            // cells Pacman must travel to reach this prediction point.
            float pacmanTravelTime = Mathf.Abs(lookAhead) * Mathf.Max(0.01f, averagePacmanCellTime);
            float idealGhostArrival = Mathf.Max(0.03f, pacmanTravelTime - desiredInterceptLead);

            float score = Mathf.Abs(ghostTravelTime - idealGhostArrival);

            // If we arrive noticeably after Pacman, this is a weak interception
            // target because Pacman has already passed it.
            float lateness = ghostTravelTime - pacmanTravelTime;
            if (lateness > lateArrivalTolerance)
            {
                score += 4f + lateness * 8f;
            }

            // At close range, direct pursuit is safer than over-predicting.
            if (lookAhead == 0 && distanceToPacman > closeDistance)
                score += 0.15f;

            // Very large predictions are useful at range, but slightly penalize
            // them so an unnecessarily far target doesn't win on timing alone.
            score += Mathf.Abs(lookAhead) * 0.008f;

            if (score < bestScore)
            {
                bestScore = score;
                bestPath = candidatePath;
                bestGoal = candidateGoal;
                bestLookAhead = lookAhead;
            }
        }

        // Fallback: always have a valid pursuit target.
        if (bestPath == null)
        {
            bestLookAhead = 0;
            goalOffset = Vector2Int.zero;
            bestGoal = pacmanCell;
            bestPath = AStarPathFinding.instance.FindPath(currentCell, bestGoal);
        }

        goalOffset = new Vector2Int(bestLookAhead, 0);
        goal = bestGoal;
        destinationsList = bestPath ?? new List<GridCellKVP>();
        lastChosenLookAhead = bestLookAhead;

        UpdateCurrentDestinationIndex();

        if (PacmanGhostContestFeedback.Instance != null)
        {
            PacmanGhostContestFeedback.Instance.RecordDecision(
                contestGhostId,
                bestLookAhead,
                distanceToPacman);
        }
    }

    private int[] ChooseCandidateOffsets(float distanceToPacman)
    {
        if (distanceToPacman <= closeDistance)
            return closeRangeLookAhead;

        if (recentTurnTimer > 0f)
            return recentTurnLookAhead;

        return normalLookAhead;
    }

    /// <summary>
    /// Copies the destination-index behavior from GhostAI so the inherited
    /// movement code continues to work exactly as intended.
    /// </summary>
    private void UpdateCurrentDestinationIndex()
    {
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

        UpdateEyesVisuals();
    }

    private float GetPathWorldDistance(List<GridCellKVP> path)
    {
        if (path == null || path.Count < 2)
            return 0f;

        float totalDistance = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            totalDistance += Vector2.Distance(path[i - 1].gridPosition, path[i].gridPosition);
        }

        return totalDistance;
    }

    private Vector2Int GetUnitDirection(Vector2Int vector)
    {
        if (vector.x != 0)
            return new Vector2Int(vector.x > 0 ? 1 : -1, 0);

        if (vector.y != 0)
            return new Vector2Int(0, vector.y > 0 ? 1 : -1);

        return Vector2Int.zero;
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && Time.unscaledTime - lastCatchRecordTime > 0.20f)
        {
            lastCatchRecordTime = Time.unscaledTime;

            Vector2Int pacmanCell = pacman != null
                ? pacman.GetCellBasedOnOffset(Vector2Int.zero)
                : Vector2Int.zero;

            if (PacmanGhostContestFeedback.Instance != null)
            {
                PacmanGhostContestFeedback.Instance.RegisterGhostCatch(
                    contestGhostId,
                    pacmanCell,
                    currentCell,
                    transform.position,
                    lastChosenLookAhead,
                    recentTurnTimer > 0f,
                    lastPacmanDirection,
                    pacmanTurnCount,
                    pacmanReversalCount);
            }
        }

        base.OnTriggerEnter2D(collision);
    }

    /// <summary>
    /// Call this from whatever code in your project handles this ghost being killed.
    /// It is intentionally separate because GhostAI.cs contains no ghost-death event.
    /// </summary>
    public void ReportGhostDeath()
    {
        if (PacmanGhostContestFeedback.Instance != null)
            PacmanGhostContestFeedback.Instance.RegisterGhostDeath(contestGhostId);
    }
}
