using UnityEngine;

public class MovableMob : MonoBehaviour
{
    public Vector2Int startingCell;
    [HideInInspector] public Vector2Int currentCell;

    public float minDistance;

    public float moveSpeed;

    protected void InitializeMob()
    {
        currentCell = startingCell;
        transform.position = GridManager.instance.GetGridPosition(startingCell);
    }
}
