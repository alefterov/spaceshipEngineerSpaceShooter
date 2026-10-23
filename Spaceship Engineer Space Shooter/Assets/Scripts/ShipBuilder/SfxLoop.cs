using UnityEngine;

/// <summary>
/// A sound that loops for as long as its object lives (for example a plasma ball humming as it flies), at the player's
/// Sfx Volume — it follows the settings slider even while playing. Created in code by whatever needs one: SfxLoop.Attach.
/// </summary>
public class SfxLoop : MonoBehaviour
{
    private AudioSource source;
    private float volume;

    /// <summary>Starts the loop on `host` (nothing happens for a null clip). It stops when `host` is destroyed.</summary>
    public static void Attach(GameObject host, AudioClip clip, float volume)
    {
        if (host == null || clip == null) return;

        var loop = host.AddComponent<SfxLoop>();
        loop.volume = Mathf.Clamp01(volume);
        loop.source = host.AddComponent<AudioSource>();
        loop.source.clip = clip;
        loop.source.loop = true;
        loop.source.spatialBlend = 0f;
        loop.source.playOnAwake = false;
        loop.ApplyVolume();
        loop.source.Play();
    }

    private void OnEnable() => AudioManager.OnVolumesChanged += ApplyVolume;
    private void OnDisable() => AudioManager.OnVolumesChanged -= ApplyVolume;

    private void ApplyVolume()
    {
        var manager = AudioManager.Instance;
        if (source != null && manager != null) source.volume = volume * manager.SfxVolume;
    }
}
