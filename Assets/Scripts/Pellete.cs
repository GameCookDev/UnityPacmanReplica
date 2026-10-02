using UnityEngine;

public class Pellete : MonoBehaviour
{
    [SerializeField] protected Sound eatSound;

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            OnCollisionLogic();
            AudioManager.instance?.PlaySoundAtPoint(eatSound, transform, false);
        }
    }

    protected virtual void OnCollisionLogic()
    {
        GameManager.instance.PelleteEaten();
        Destroy(gameObject);
    }
}
