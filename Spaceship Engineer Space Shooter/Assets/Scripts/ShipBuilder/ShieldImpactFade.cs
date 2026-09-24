using UnityEngine;

/// <summary>Fades an impact arc's sprites from their starting alpha to nothing, then destroys it. Added
/// by ShieldModule to each arc it spawns; never attach by hand.</summary>
public class ShieldImpactFade : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private float[] startAlpha;
    private float duration;
    private float elapsed;

    public void Begin(float fadeSeconds)
    {
        duration = Mathf.Max(0.01f, fadeSeconds);
        renderers = GetComponentsInChildren<SpriteRenderer>();
        startAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) startAlpha[i] = renderers[i].color.a;
    }

    private void Update()
    {
        if (renderers == null) return;

        elapsed += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(elapsed / duration);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            var c = renderers[i].color;
            c.a = startAlpha[i] * remaining;
            renderers[i].color = c;
        }

        if (elapsed >= duration) Destroy(gameObject);
    }
}
