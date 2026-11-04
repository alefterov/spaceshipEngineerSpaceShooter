using UnityEngine;

public class SfxLoop : MonoBehaviour
{
    private AudioSource source;
    private float volume;

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
