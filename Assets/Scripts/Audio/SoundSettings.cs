using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "new Sound Settings", menuName = "Sound/Sound Settings")]
public class SoundSettings : ScriptableObject
{
    public AudioMixerGroup audioMixerGroup;

    public bool loop;

    [Range(0, 1)] public float volume = 1;
    [Range(-3, 3)] public float pitch = 1;

    [Range(0, 1)] public float spatialBlend = 0;
    [Range(0, 1.25f)] public float reverbZoneWet = 1;

    public float maxDistance = 25f;

    public bool informsEnemies = false;
}
