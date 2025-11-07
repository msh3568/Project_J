using UnityEngine;

[System.Serializable]
public class SoundEffect
{
    public AudioClip clip;
    [Range(0, 1f)]
    public float volume = 1f;
}
