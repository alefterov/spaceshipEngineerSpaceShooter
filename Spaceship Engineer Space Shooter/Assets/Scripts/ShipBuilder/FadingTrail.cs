using UnityEngine;

/// <summary>
/// A short fading trail behind a moving object that, when the object ends, is cut loose and fades away where it
/// was instead of vanishing with it. Not set up by hand: Missile adds it and feeds it the Trail settings from
/// its own Inspector (Setup). Uses the object's own TrailRenderer if it has one (child or same object);
/// otherwise creates one from those settings (color taken from a ProjectileGlow on the same object, if any).
/// Call Detach() when the object is destroyed.
/// </summary>
public class FadingTrail : MonoBehaviour
{
    private TrailRenderer trail;

    /// <summary>Creates the trail from the given settings, unless the object already has a TrailRenderer of its
    /// own — that one is used as it is.</summary>
    public void Setup(float time, float startWidth, float endWidth, Color color, Material material, int sortingOrder)
    {
        trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null) return;

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
