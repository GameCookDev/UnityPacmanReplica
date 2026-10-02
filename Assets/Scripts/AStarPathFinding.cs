using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class AStarPathFinding : MonoBehaviour
{
    public static AStarPathFinding instance { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public List<GridCellKVP> FindPath(Vector2Int start, Vector2Int agentGoal)
    {
        Vector2Int goal = GridManager.instance.GetGridCellOrClosest(agentGoal);

        if (start == goal)
            return new List<GridCellKVP>() { new GridCellKVP() { gridIndex = start, gridPosition = GridManager.instance.GetGridPosition(start) } };

        HashSet<Vector2Int> openSet = new HashSet<Vector2Int> { start };
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float> { { start, 0f } };
        Dictionary<Vector2Int, float> hScore = new Dictionary<Vector2Int, float> { { start, CalculateHScore(start, goal) } };
        Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float> { { start, hScore[start] } };

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestFScoreCell(openSet, fScore);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            List<Vector2Int> neighbors = GridManager.instance.GetNeighbors(current);

            foreach (Vector2Int neighbor in neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;

                float tempGScore = gScore[current] + 1f;

                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tempGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tempGScore;
                hScore[neighbor] = CalculateHScore(neighbor, goal);
                fScore[neighbor] = gScore[neighbor] + hScore[neighbor];
            }
        }

        return new List<GridCellKVP>() { new GridCellKVP() { gridIndex = start, gridPosition = GridManager.instance.GetGridPosition(start) } };
    }

    //helpers & utils
    private float CalculateHScore(Vector2Int from, Vector2Int to)
    {
        int xDistance = Mathf.Abs(to.x - from.x);
        int yDistance = Mathf.Abs(to.y - from.y);
        return xDistance + yDistance;
    }

    private Vector2Int GetLowestFScoreCell(HashSet<Vector2Int> openSet, Dictionary<Vector2Int, float> fScores)
    {
        return openSet.OrderBy(pos => fScores[pos]).First();
    }

    private List<GridCellKVP> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<GridCellKVP> path = new List<GridCellKVP>() { new GridCellKVP() { gridIndex = current, gridPosition = GridManager.instance.GetGridPosition(current) } };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, new GridCellKVP() { gridIndex = current, gridPosition = GridManager.instance.GetGridPosition(current) } );
        }

        return path;
    }
}
