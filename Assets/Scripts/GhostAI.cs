using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GhostAI : MovableMob
{
    [SerializeField] protected GhostStates ghostState = GhostStates.chase;

    [SerializeField] private float firstUpdateDelay = 0;
    [SerializeField, Tooltip("how many times per second should the ai update")] private float updateRate = 1;
    private float updateCountDown = 0;

    [SerializeField] protected PacmanController pacman; //a reference to the pacman itself, to find out what grid cell the pacman is currently situated at
    [SerializeField] protected Vector2Int goalOffset; //the offset that will be applied to the pacman's grid cell (based on where pacman is facing), so that if this ghost wants to persuade a different cell than the pacman
    protected Vector2Int goal; //the final grid cell that the ghost will persuade

    protected List<GridCellKVP> destinationsList = new List<GridCellKVP>();
    protected int currentDestinationIndex = 0;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer eyesSR;
    [SerializeField] private Sprite eyesRight, eyesUp, eyesLeft, eyesDown;
    [SerializeField] private SpriteRenderer ghostSR;
    [SerializeField] private Sprite ghostDefaultSprite;
    private Sprite initGhostSprite;
    private Color initGhostColor;

    private void Start()
    {
        if (updateRate == 0) updateRate = 1;

        initGhostSprite = ghostSR.sprite;
        initGhostColor = ghostSR.color;

        InitializeAgent();

        GameManager.instance.onGameStateChanged += ChangeState;
    }

    protected virtual void InitializeAgent()
    {
        ghostState = GhostStates.none;
        InitializeMob(); //put the agent on it's starting grid cell
        GridManager.instance.SetIsCellOccupied(currentCell, true);
        updateCountDown = firstUpdateDelay;
    }

    private void Update()
    {
        Tick();

        HandleMovement();
    }

    protected virtual void Tick()
    {
        if (ghostState == GhostStates.none) return;

        updateCountDown -= Time.deltaTime;

        if (updateCountDown <= 0)
        {
            UpdateAgentPath();
            updateCountDown = 1 / updateRate;
        }
    }

    protected virtual void HandleMovement()
    {
        if (ghostState == GhostStates.none) return;

        if (destinationsList.Count > currentDestinationIndex)
        {
            if (Vector2.Distance(transform.position, destinationsList[currentDestinationIndex].gridPosition) > minDistance)
            {
                Vector2 direction = destinationsList[currentDestinationIndex].gridPosition - (Vector2)transform.position;
                transform.position = transform.position + (Vector3)direction.normalized * moveSpeed * Time.deltaTime;
            }
            else
            {
                GridManager.instance.SetIsCellOccupied(currentCell, false);
                currentCell = destinationsList[currentDestinationIndex].gridIndex;
                GridManager.instance.SetIsCellOccupied(currentCell, true);
                currentDestinationIndex++;

                UpdateEyesVisuals();
            }
        }
    }

    protected virtual void UpdateAgentPath()
    {
        if (ghostState == GhostStates.chase)
        {
            goal = pacman.GetCellBasedOnOffset(goalOffset); //if the goal cell doesn't exist on the grid, the pathfinding algorithm returns the cell that pacman is situated at
        }
        else if (ghostState == GhostStates.runAway)
        {
            Vector2Int closestCornerToPacman = pacman.GetClosestCorner();
            List<Vector2Int> validCorners = GridManager.instance.gridCorners.ToList();

            if (validCorners.Contains(closestCornerToPacman))
                validCorners.Remove(closestCornerToPacman);

            goal = validCorners[Random.Range(0, validCorners.Count)];
        }
        else
        {
            destinationsList = new List<GridCellKVP>() { new GridCellKVP() { gridIndex = currentCell, gridPosition = GridManager.instance.GetGridPosition(currentCell) } };
            return;
        }

        destinationsList = AStarPathFinding.instance.FindPath(currentCell, goal); //grid cells with walls or obstacles and also cells that have been occupied by other ghosts are non walkable

        if (destinationsList.Count > 1)
        {
            if (Vector2.Distance(transform.position, destinationsList[1].gridPosition) >= Vector2.Distance(destinationsList[0].gridPosition, destinationsList[1].gridPosition))
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

    public virtual void ChangeState(GameState gameState)
    {
        if (gameState == GameState.ghostsChasingPacman)
        {
            ghostState = GhostStates.chase;

            ghostSR.sprite = initGhostSprite;
            ghostSR.color = initGhostColor;
        }
        else if (gameState == GameState.pacmanChasingGhosts)
        {
            ghostState = GhostStates.runAway;

            ghostSR.sprite = ghostDefaultSprite;
            ghostSR.color = Color.white;
        }
        if (gameState == GameState.paused)
        {
            ghostState = GhostStates.none;

            ghostSR.sprite = initGhostSprite;
            ghostSR.color = initGhostColor;
        }
        
        UpdateAgentPath();
    }

    protected void UpdateEyesVisuals()
    {
        if (destinationsList.Count <= currentDestinationIndex) return;

        Vector2 direction = destinationsList[currentDestinationIndex].gridPosition - (Vector2)transform.position;
        direction.Normalize();

        float upVectorDotProduct = Vector2.Dot(transform.up, direction);
        float rightVectorDotProduct = Vector2.Dot(transform.right, direction);

        if (upVectorDotProduct > 0.7f)
        {
            eyesSR.sprite = eyesUp;
        }
        else if (upVectorDotProduct < -0.7f)
        {
            eyesSR.sprite = eyesDown;
        }

        if (rightVectorDotProduct > 0.7f)
        {
            eyesSR.sprite = eyesRight;
        }
        else if (rightVectorDotProduct < -0.7f)
        {
            eyesSR.sprite = eyesLeft;
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (ghostState == GhostStates.chase)
                GameManager.instance.RestartGame();
            else
                InitializeAgent();
        }
    }
}

public enum GhostStates
{
    none, chase, runAway
}