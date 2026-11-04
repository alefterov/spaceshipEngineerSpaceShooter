using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Seconds the previous track takes to fade out while this one fades in.")]
    [Min(0f)] public float fadeSeconds = 1f;

    private void Start()
    {
        var manager = AudioManager.Instance;
        if (manager != null) manager.PlayMusic(music, volume, fadeSeconds);
    }
}
