using System.Collections.Generic;
using UnityEngine;

public class PacmanController : MovableMob
{
    private readonly Dictionary<Directions, Vector2Int> movementDirectionPairs = new Dictionary<Directions, Vector2Int>
    {
        { Directions.right, new Vector2Int(1, 0) },
        { Directions.left, new Vector2Int(-1, 0) },
        { Directions.up, new Vector2Int(0, 1) },
        { Directions.down, new Vector2Int(0, -1) }
    };

    private Directions movementDirection = Directions.right;
    private Directions queuedMovementDirection = Directions.right;

    protected Vector2 destination = Vector2.zero;

    [SerializeField] private Transform pacmanVisuals;

    private bool canMove = false;

    void Start()
    {
        InitializeMob();

        GetNextDestination();

        InputManager.instance.onPlayerMovementInput += ReadPlayerMovementInput;
        GameManager.instance.onGameStateChanged += OnGameStateChanged;
    }

    private void OnDestroy()
    {
        InputManager.instance.onPlayerMovementInput -= ReadPlayerMovementInput;
    }

    void Update()
    {
        if (canMove == false) return;

        if (Vector2.Distance(transform.position, destination) > minDistance)
        {
            Vector2 direction = destination - (Vector2)transform.position;
            transform.position = transform.position + (Vector3)direction.normalized * moveSpeed * Time.deltaTime;
        }
        else
        {
            GetNextDestination();

            switch (movementDirection)
            {
                case Directions.up:
                    pacmanVisuals.localEulerAngles = new Vector3(0, 0, 90);
                    break;

                case Directions.right:
                    pacmanVisuals.localEulerAngles = new Vector3(0, 0, 0);
                    break;

                case Directions.left:
                    pacmanVisuals.localEulerAngles = new Vector3(0, 0, -180);
                    break;

                case Directions.down:
                    pacmanVisuals.localEulerAngles = new Vector3(0, 0, -90);
                    break;
            }
        }
    }

    private void ReadPlayerMovementInput(Vector2 movementInput)
    {
        movementInput.Normalize();

        foreach (var kvp in movementDirectionPairs)
        {
            if (kvp.Value == movementInput)
            {
                queuedMovementDirection = kvp.Key;
            }
        }
    }

    public void GetNextDestination()
    {
        Vector2Int tempNextGrid = currentCell;

        foreach (var kvp in movementDirectionPairs)
        {
            if (kvp.Key == queuedMovementDirection)
            {
                tempNextGrid = currentCell + kvp.Value;
            }
        }

        Vector2 nextGridPos = GridManager.instance.GetGridPosition(tempNextGrid);

        if (nextGridPos != Vector2.zero)
        {
            currentCell = tempNextGrid;
            destination = nextGridPos;

            movementDirection = queuedMovementDirection;
        }
        else
        {
            foreach (var kvp in movementDirectionPairs)
            {
                if (kvp.Key == movementDirection)
                {
                    tempNextGrid = currentCell + kvp.Value;
                }
            }

            nextGridPos = GridManager.instance.GetGridPosition(tempNextGrid);

            if (nextGridPos != Vector2.zero)
            {
                currentCell = tempNextGrid;
                destination = nextGridPos;
            }
        }
    }

    public Vector2Int GetCellBasedOnOffset(Vector2Int offset)
    {
        Vector2Int offsetCell = currentCell;

        switch (movementDirection)
        {
            case Directions.right:
                offsetCell += offset;
                break;

            case Directions.left:
                offsetCell += new Vector2Int(-offset.x, -offset.y);
                break;

            case Directions.up:
                offsetCell += new Vector2Int(-offset.y, offset.x);
                break;

            case Directions.down:
                offsetCell += new Vector2Int(offset.y, -offset.x);
                break;
        }

        return offsetCell;
    }

    public Vector2Int GetClosestCorner()
    {
        Vector2Int closestCorner = GridManager.instance.gridCorners[Random.Range(0, GridManager.instance.gridCorners.Length)];
        float closestDistance = float.MaxValue;

        foreach (Vector2Int corner in GridManager.instance.gridCorners)
        {
            float distanceToCorner = Vector2Int.Distance(corner, currentCell);

            if (distanceToCorner < closestDistance)
            {
                closestCorner = corner;
                closestDistance = distanceToCorner;
            }
        }

        return closestCorner;
    }

    private void OnGameStateChanged(GameState gameState)
    {
        if (gameState == GameState.paused)
            canMove = false;
        else
            canMove = true;
    }
}

public enum Directions
{
    right, left, up, down
}