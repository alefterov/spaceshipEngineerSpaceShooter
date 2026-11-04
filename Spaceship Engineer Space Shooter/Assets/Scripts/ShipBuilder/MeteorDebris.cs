using UnityEngine;

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
    private Gradient colorOverLife;

    public void BeginCooling(SpriteRenderer renderer, Vector2 velocity, float spinDegreesPerSecond, float delay, float fadeSeconds, Gradient gradient)
    {
        Begin(renderer, velocity, spinDegreesPerSecond, delay, fadeSeconds);
        colorOverLife = gradient;
    }

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

        if (colorOverLife != null)
        {
            sr.color = colorOverLife.Evaluate(t);
            if (t >= 1f) Destroy(gameObject);
            return;
        }

        var c = startColor;
        c.a = startColor.a * (1f - t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
