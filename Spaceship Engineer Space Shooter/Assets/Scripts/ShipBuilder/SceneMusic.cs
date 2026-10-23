using UnityEngine;

/// <summary>
/// The music of a scene: put one in the main menu scene and one in the battle scene, each with its own track. When the
/// scene starts, the music cross-fades to this track (if it is already the one playing, it just carries on). The
/// loudness is the player's Music Volume from the settings, times Volume here.
/// </summary>
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
