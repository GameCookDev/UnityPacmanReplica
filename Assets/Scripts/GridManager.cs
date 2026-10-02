using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager instance { get; private set; }

    [SerializeField] private Vector2Int gridSize;
    [SerializeField] private Vector2 cellSize;

    private List<GridCell> grid = new List<GridCell>();

    [SerializeField] private List<WalkableCellsSparse> walkableCells;

    [SerializeField] private List<OverrideCellsPellete> overridePelleteCells;

    [SerializeField] private GameObject smallPellete, bigPellete;

    private readonly Dictionary<Directions, Vector2Int> movementDirectionPairs = new Dictionary<Directions, Vector2Int>
    {
        { Directions.right, new Vector2Int(1, 0) },
        { Directions.left, new Vector2Int(-1, 0) },
        { Directions.up, new Vector2Int(0, 1) },
        { Directions.down, new Vector2Int(0, -1) }
    };

    [SerializeField] private bool ghostsAreObstacles = false;

    [SerializeField] private MovableMob pacman;

    public Vector2Int[] gridCorners;

    [Header("Gizmos")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private bool customGridGizmos = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeGrid();
    }

    [ContextMenu("Initialize Grid")]
    private void InitializeGrid()
    {
        Vector2Int currentGrid = Vector2Int.zero;
        Vector2 cellPosition = transform.position;

        for (int i = 0; i < gridSize.y; i++)
        {
            currentGrid.y = i;
            currentGrid.x = 0;
            cellPosition.x = transform.position.x;

            for (int j = 0; j < gridSize.x; j++)
            {
                bool isCurrentGridWalkable = false;
                PelleteType pelleteType = PelleteType.small;

                WalkableCellsSparse walkableCellsToCheck = walkableCells.FirstOrDefault(x => x.row == currentGrid.y);
                if (walkableCellsToCheck != null && IsGridInColumnRange(currentGrid, walkableCellsToCheck.columnRangeList))
                    isCurrentGridWalkable = true;

                OverrideCellsPellete cellsPelletToCheck = overridePelleteCells.FirstOrDefault(x => x.row == currentGrid.y);
                if (cellsPelletToCheck != null && IsGridInColumnRange(currentGrid, cellsPelletToCheck.columnRangeList))
                    pelleteType = cellsPelletToCheck.pelleteType;

                grid.Add(new GridCell
                {
                    gridIndex = currentGrid,
                    gridPosition = cellPosition,
                    isWalkable = isCurrentGridWalkable,
                    pelleteType = pelleteType
                });

                if (isCurrentGridWalkable)
                {
                    switch (pelleteType)
                    {
                        case PelleteType.small:
                            Instantiate(smallPellete, cellPosition, Quaternion.identity);
                            break;

                        case PelleteType.big:
                            Instantiate(bigPellete, cellPosition, Quaternion.identity);
                            break;
                    }
                }

                cellPosition.x += cellSize.x;
                currentGrid.x++;
            }

            cellPosition.y += cellSize.y;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGrid) return;

        if (customGridGizmos)
        {
            Vector2Int currentGrid = Vector2Int.zero;
            Vector2 gridRowStartPosition = transform.position;

            for (int i = 0; i < gridSize.y; i++)
            {
                Vector2 cellPosition = gridRowStartPosition;
                currentGrid.x = 0;

                for (int j = 0; j < gridSize.x; j++)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireCube(cellPosition, cellSize);

                    WalkableCellsSparse walkableCellsToCheck = walkableCells.FirstOrDefault(x => x.row == currentGrid.y);
                    if (walkableCellsToCheck != null && IsGridInColumnRange(currentGrid, walkableCellsToCheck.columnRangeList))
                        Gizmos.color = Color.white;
                    else
                        Gizmos.color = Color.red;

                    Gizmos.DrawSphere(cellPosition, 0.1f);

                    cellPosition.x += cellSize.x;
                    currentGrid.x++;
                }

                gridRowStartPosition.y += cellSize.y;
                currentGrid.y++;
            }
        }
        else
        {
            for (int i = 0; i < grid.Count; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawWireCube(grid[i].gridPosition, cellSize);

                if (grid[i].isWalkable)
                    Gizmos.color = Color.white;
                else
                    Gizmos.color = Color.red;

                Gizmos.DrawSphere(grid[i].gridPosition, 0.1f);
            }
        }
    }

    //helpers & utils
    public bool IsGridInColumnRange(Vector2Int grid, List<WalkableCellsColumnRange> columnRanges)
    {
        for (int i = 0; i < columnRanges.Count; i++)
        {
            if (grid.x >= columnRanges[i].columnStart && grid.x <= columnRanges[i].columnFinish)
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetGridPosition(Vector2Int gridIndex)
    {
        int cellIndex = (gridIndex.y * gridSize.x) + gridIndex.x;

        if (grid[cellIndex] != null)
        {
            if (grid[cellIndex].isWalkable)
                return grid[cellIndex].gridPosition;
        }

        return Vector2.zero;
    }

    public Vector2Int GetGridCellOrClosest(Vector2Int cellToFind)
    {
        Vector2Int clampedCell = Vector2Int.zero;
        clampedCell.x = Mathf.Clamp(cellToFind.x, 0, gridSize.x - 1);
        clampedCell.y = Mathf.Clamp(cellToFind.y, 0, gridSize.y - 1);

        if (IsGridWalkable(clampedCell))
            return clampedCell;

        return pacman.currentCell;
    }

    public List<Vector2Int> GetNeighbors(Vector2Int cell)
    {
        var neighbors = new List<Vector2Int>();

        foreach (var direction in movementDirectionPairs)
        {
            Vector2Int neighbor = cell + direction.Value;

            if (IsGridWalkable(neighbor))
                neighbors.Add(neighbor);
        }

        return neighbors;
    }

    public bool IsGridWalkable(Vector2Int cell)
    {
        int cellIndex = (cell.y * gridSize.x) + cell.x;

        if (grid[cellIndex] != null)
        {
            if (grid[cellIndex].isWalkable)
            {
                if (ghostsAreObstacles)
                {
                    if (!grid[cellIndex].isOccupied)
                        return true;
                }
                else
                {
                    return true;
                }
            }

        }

        return false;
    }

    public void SetIsCellOccupied(Vector2Int cell, bool isOccupied)
    {
        int cellIndex = (cell.y * gridSize.x) + cell.x;
        grid[cellIndex].isOccupied = isOccupied;
    }
}

public class GridCell
{
    public Vector2Int gridIndex;
    public Vector2 gridPosition;
    public bool isWalkable;
    public bool isOccupied;
    public PelleteType pelleteType = PelleteType.small;
}

[System.Serializable]
public class GridCellKVP
{
    public Vector2Int gridIndex;
    public Vector2 gridPosition;
}

[System.Serializable]
public class OverrideCellsPellete
{
    public int row = 0;
    public List<WalkableCellsColumnRange> columnRangeList = new List<WalkableCellsColumnRange>();
    public PelleteType pelleteType = PelleteType.none;
}

[System.Serializable]
public class WalkableCellsSparse
{
    public int row = 0;
    public List<WalkableCellsColumnRange> columnRangeList = new List<WalkableCellsColumnRange>();
}

[System.Serializable]
public class WalkableCellsColumnRange
{
    public int columnStart;
    public int columnFinish;
}

public enum PelleteType
{
    none, small, big
}
