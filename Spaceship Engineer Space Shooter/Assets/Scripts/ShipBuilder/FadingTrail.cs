using UnityEngine;

/// <summary>
/// A short fading trail behind a moving object that, when the object ends, is cut loose and fades away where it
/// was instead of vanishing with it. Uses the object's own TrailRenderer if it has one (child or same object);
/// otherwise creates one from the settings below (color taken from a ProjectileGlow on the same object, if any).
/// Call Detach() when the object is destroyed. Added automatically by Missile.
/// </summary>
public class FadingTrail : MonoBehaviour
{
    [Tooltip("How long a point of the trail lasts, in seconds — the trail's length.")]
    [Min(0.01f)] public float time = 0.6f;
    [Tooltip("Trail width right behind the object, in world units, and at its far tip.")]
    public float startWidth = 0.15f;
    public float endWidth = 0f;
    [Tooltip("Trail color; it fades to transparent along its length. A ProjectileGlow color wins if present.")]
    public Color color = new(1f, 0.85f, 0.6f, 0.8f);
    [Tooltip("Optional material (e.g. additive). Empty = the plain sprite material.")]
    public Material material;
    public int sortingOrder = 0;

    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null) return; // the object's own trail keeps its own settings

        var glow = GetComponent<ProjectileGlow>();
        Color c = glow != null ? glow.color : color;

        var go = new GameObject("Trail", typeof(TrailRenderer));
        go.transform.SetParent(transform, false);

        trail = go.GetComponent<TrailRenderer>();
        trail.time = time;
        trail.startWidth = startWidth;
        trail.endWidth = endWidth;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.sortingOrder = sortingOrder;
        trail.material = material != null ? material : new Material(Shader.Find("Sprites/Default"));

        var end = c;
        end.a = 0f;
        trail.startColor = c;
        trail.endColor = end;
    }

    /// <summary>Cuts the trail loose; it removes itself once it has faded out.</summary>
    public void Detach()
    {
        if (trail == null) return;

        trail.transform.SetParent(null, true);
        trail.emitting = false;
        trail.autodestruct = true;
        trail = null;
    }
}
