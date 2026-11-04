using UnityEngine;

public class FadingTrail : MonoBehaviour
{
    private TrailRenderer trail;

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

    public void Detach()
    {
        if (trail == null) return;

        trail.transform.SetParent(null, true);
        trail.emitting = false;
        trail.autodestruct = true;
        trail = null;
    }
}
