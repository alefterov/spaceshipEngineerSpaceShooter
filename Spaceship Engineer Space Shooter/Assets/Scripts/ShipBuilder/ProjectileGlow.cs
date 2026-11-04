using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ProjectileGlow : MonoBehaviour
{
    public Color color = new(1f, 0.75f, 0.3f, 1f);

    [Header("Light (URP 2D)")]
    public Light2D glowLight;
    public float lightIntensity = 1.2f;
    [Tooltip("Outer radius of the light, in world units.")]
    public float lightRadius = 1.5f;

    [Header("Halo sprite")]
    public SpriteRenderer halo;
    [Range(0f, 1f)] public float haloAlpha = 0.6f;

    [Header("Trail")]
    public TrailRenderer trail;

    [Header("Pulse")]
    [Tooltip("How much the glow swells and dims, as a fraction (0.15 = ±15%). 0 = steady.")]
    [Range(0f, 0.9f)] public float pulseAmount = 0.15f;
    public float pulseSpeed = 12f;

    private float phase;

    private void Awake()
    {
        phase = Random.value * Mathf.PI * 2f;

        if (trail != null)
        {
            trail.startColor = color;
            var end = color;
            end.a = 0f;
            trail.endColor = end;
        }

        if (glowLight != null)
        {
            glowLight.color = color;
            glowLight.pointLightOuterRadius = lightRadius;
        }

        Apply(1f);
    }

    private void Update()
    {
        float k = 1f + Mathf.Sin(Time.time * pulseSpeed + phase) * pulseAmount;
        Apply(k);
    }

    private void Apply(float k)
    {
        if (glowLight != null) glowLight.intensity = lightIntensity * k;

        if (halo != null)
        {
            var c = color;
            c.a = Mathf.Clamp01(haloAlpha * k);
            halo.color = c;
        }
    }
}
