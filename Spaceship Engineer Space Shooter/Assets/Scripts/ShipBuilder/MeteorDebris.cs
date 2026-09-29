using UnityEngine;

/// <summary>One flying chunk of a destroyed meteor: holds still for a moment (so the dust explosion covers
/// the break-up), then drifts outward while spinning, shrinking slightly and fading to nothing, then
/// removes itself. Added by Meteor at spawn time; never attach by hand. Pure transform motion — no
/// physics, no collider, so it can't hit anything.</summary>
public class MeteorDebris : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private float spin;
    private float startDelay;
    private float lifetime;
    private float elapsed;
    private Color startColor;
    private Vector3 startScale;

    public void Begin(SpriteRenderer renderer, Vector2 velocity, float spinDegreesPerSecond, float delay, float fadeSeconds)
    {
        sr = renderer;
        this.velocity = velocity;
        spin = spinDegreesPerSecond;
        startDelay = Mathf.Max(0f, delay);
        lifetime = Mathf.Max(0.05f, fadeSeconds);
        startColor = sr.color;
        startScale = transform.localScale;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < startDelay) return;

        float t = Mathf.Clamp01((elapsed - startDelay) / lifetime);

        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        transform.localScale = startScale * Mathf.Lerp(1f, 0.6f, t);

        var c = startColor;
        c.a = startColor.a * (1f - t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
