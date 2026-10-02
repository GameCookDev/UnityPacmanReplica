using UnityEngine;

[System.Serializable]
public class Sound
{
    public AudioClip clip;
    public SoundSettings settings;

    public bool overrideVolume = false;
    [Range(0, 1)] public float volume = 1f;

    public bool overrideMaxDistance = false;
    public float maxDistance = 30f;

    public bool overrideLoop = false;
    public bool loop = false;
}
