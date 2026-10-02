using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance { get; private set; }

    [SerializeField] private GameState gameState = GameState.paused;

    public delegate void OnGameStateChanged(GameState gameState);
    public OnGameStateChanged onGameStateChanged;

    [SerializeField] Sound gameStartSound, chasingGhostsSound;

    private int pelletesEaten = 0;
    [SerializeField] TextMeshProUGUI pelletesEatenTxt;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        Application.targetFrameRate = 60;

        AudioManager.instance.PlaySoundAtPoint(gameStartSound, transform, false);

        StartCoroutine(ResetGameState(GameState.ghostsChasingPacman, gameStartSound.clip.length + 0.25f));

        InputManager.instance.onBackPressed += GoBackToMainMenu;
    }

    private void OnDestroy()
    {
        InputManager.instance.onBackPressed -= GoBackToMainMenu;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ChangeGameState(GameState newGameState, float duration = 0)
    {
        if (duration > 0)
            StartCoroutine(ResetGameState(gameState, duration));

        gameState = newGameState;

        onGameStateChanged?.Invoke(gameState);
    }

    IEnumerator ResetGameState(GameState stateToResetTo, float waitTime)
    {
        yield return new WaitForSeconds(waitTime);

        ChangeGameState(stateToResetTo);
    }

    public void PelleteEaten()
    {
        pelletesEaten++;
        pelletesEatenTxt.text = pelletesEaten.ToString();
    }

    void GoBackToMainMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
}

public enum GameState
{
    paused, ghostsChasingPacman, pacmanChasingGhosts 
}