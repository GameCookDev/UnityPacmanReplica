using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InterceptorGhost : GhostAI
{
    [Header("Chase Settings")]
    [SerializeField, Tooltip("How many grid cells ahead of Pacman to target during chase")]
    private int leadDistance = 3;

    [SerializeField, Tooltip("Distance at which the ghost switches from ambush to direct pursuit")]
    private float directPursuitRadius = 4f;

    private Vector2Int pacmanLastCell;

    protected override void InitializeAgent()
    {
        base.InitializeAgent();
        if (pacman != null)
        {
            pacmanLastCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
        }
    }

    protected override void UpdateAgentPath()
    {
        if (ghostState == GhostStates.chase)
        {
            Vector2Int pacmanCurrentCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);

            // Calculate direction Pacman is moving
            Vector2Int pacmanDir = pacmanCurrentCell - pacmanLastCell;
            pacmanDir.Clamp(new Vector2Int(-1, -1), new Vector2Int(1, 1));
            pacmanLastCell = pacmanCurrentCell;

            float distanceToPacman = Vector2.Distance(transform.position, pacman.transform.position);

            if (distanceToPacman <= directPursuitRadius)
            {
                // Close range: Direct chase
                goal = pacmanCurrentCell;
            }
            else
            {
                // Mid/Long range: Ambush ahead of Pacman's path
                goal = pacmanCurrentCell + (pacmanDir * leadDistance);
            }
        }
        else if (ghostState == GhostStates.runAway)
        {
            Vector2Int pacmanCurrentCell = pacman.GetCellBasedOnOffset(Vector2Int.zero);
            Vector2Int closestCornerToPacman = pacman.GetClosestCorner();
            List<Vector2Int> validCorners = GridManager.instance.gridCorners.ToList();

            if (validCorners.Contains(closestCornerToPacman))
                validCorners.Remove(closestCornerToPacman);

            // Smart Fleeing: Pick the valid corner farthest from Pacman
            if (validCorners.Count > 0)
            {
                goal = validCorners
                    .OrderByDescending(corner => Vector2Int.Distance(corner, pacmanCurrentCell))
                    .First();
            }
            else
            {
                goal = currentCell;
            }
        }
        else
        {
            destinationsList = new List<GridCellKVP>()
            {
                new GridCellKVP() { gridIndex = currentCell, gridPosition = GridManager.instance.GetGridPosition(currentCell) }
            };
            return;
        }

        // Calculate path to selected goal
        destinationsList = AStarPathFinding.instance.FindPath(currentCell, goal);

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

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Vector2Int pacmanLocation = pacman.GetCellBasedOnOffset(Vector2Int.zero);

            if (ghostState == GhostStates.chase)
            {
                GhostTelemetry.Instance?.RecordCatch(pacmanLocation, currentCell);
            }
            else if (ghostState == GhostStates.runAway)
            {
                GhostTelemetry.Instance?.RecordDeath(pacmanLocation, currentCell);
            }
        }

        base.OnTriggerEnter2D(collision);
    }
}