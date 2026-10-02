using UnityEngine;

public class BigPellete : Pellete
{
    [SerializeField] private float pacmanChasingGhostsDuration = 7.5f;

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
    }

    protected override void OnCollisionLogic()
    {
        GameManager.instance.ChangeGameState(GameState.pacmanChasingGhosts, pacmanChasingGhostsDuration);
        base.OnCollisionLogic();
    }
}
