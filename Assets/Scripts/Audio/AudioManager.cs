using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    [SerializeField] AudioSource audioPrefab;
    [SerializeField] int audioPoolSize = 20;

    private Queue<AudioSource> audioPool = new Queue<AudioSource>();

    [SerializeField] private Sound pacmanChasingGhostsMusic;
    private AudioSource pacmanChasingGhostsSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < audioPoolSize; i++)
        {
            var src = Instantiate(audioPrefab, transform);
            DontDestroyOnLoad(src);
            src.gameObject.SetActive(false);
            audioPool.Enqueue(src);
        }

        pacmanChasingGhostsSource = Instantiate(audioPrefab, transform);
        ApplySoundSettings(pacmanChasingGhostsSource, pacmanChasingGhostsMusic);
    }

    private void Start()
    {
        GameManager.instance.onGameStateChanged += OnGameStateChanged;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnNewLevelLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnNewLevelLoaded;
    }

    void OnNewLevelLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (AudioSource a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            a.Stop();
            a.gameObject.SetActive(false);
        }
    }

    public void PlaySoundAtPoint(Sound sound, Transform soundPos, bool parent)
    {
        AudioSource src = GetSource();

        ApplySoundSettings(src, sound);

        src.gameObject.SetActive(true);

        src.transform.position = soundPos.position;

        if (parent)
            src.transform.parent = soundPos;
        else
            src.transform.parent = null;

        src.Play();

        StartCoroutine(ReturnAfterPlay(src, sound.clip.length * 2));
    }

    AudioSource GetSource()
    {
        if (audioPool.Count > 0)
            return audioPool.Dequeue();

        return Instantiate(audioPrefab, transform);
    }

    IEnumerator ReturnAfterPlay(AudioSource src, float time)
    {
        yield return null;

        yield return new WaitForSecondsRealtime(time);

        ReturnSource(src);
    }

    void ReturnSource(AudioSource src)
    {
        src.gameObject.SetActive(false);
        audioPool.Enqueue(src);
    }

    private void ApplySoundSettings(AudioSource src, Sound sound)
    {
        src.clip = sound.clip;
        src.outputAudioMixerGroup = sound.settings.audioMixerGroup;
        src.loop = sound.settings.loop;
        src.volume = sound.settings.volume;
        src.pitch = sound.settings.pitch;
        src.reverbZoneMix = sound.settings.reverbZoneWet;
        src.spatialBlend = sound.settings.spatialBlend;
        src.maxDistance = sound.settings.maxDistance;

        if (sound.overrideVolume)
            src.volume = sound.volume;

        if (sound.overrideMaxDistance)
            src.maxDistance = sound.maxDistance;

        if (sound.overrideLoop)
            src.loop = sound.loop;
    }

    private void OnGameStateChanged(GameState gameState)
    {
        if (gameState == GameState.pacmanChasingGhosts)
            PlayPacmanChasingGhostsMusic();
        else
            StopPacmanChasingGhostsMusic();
    }

    private void PlayPacmanChasingGhostsMusic()
    {
        pacmanChasingGhostsSource.Play();
    }

    private void StopPacmanChasingGhostsMusic()
    {
        pacmanChasingGhostsSource.Stop();
    }
}
