using UnityEngine;

/// <summary>
/// One bolt of lightning, drawn procedurally: a jagged line from a start to an end, with a wider, fainter line
/// under it for the glow. The jags are re-rolled every Flicker Seconds so it crackles, and it fades out over its
/// Lifetime. Both ends follow their transforms (the ball, the block) for as long as those exist, and stay where they
/// were if one disappears. Created by PlasmaBall (Strike); removes itself when done.
/// </summary>
public class PlasmaLightning : MonoBehaviour
{
    public struct Style
    {
        public Color color;
        public float width;
        public float haloWidthMultiplier;
        public float haloAlpha;
        public float segmentLength;   // roughly how long each straight piece is
        public float jaggedness;      // the most a point is pushed sideways, in world units
        public float lifetime;
        public float flickerSeconds;
        public Material material;
        public int sortingOrder;
    }

    private Style style;
    private Transform from;
    private Transform to;
    private Vector3 fromLast;
    private Vector3 toLast;

    private LineRenderer core;
    private LineRenderer halo;
    private float age;
    private float nextFlicker;

    private static Material fallbackMaterial;
    private static Material FallbackMaterial
    {
        get
        {
            if (fallbackMaterial == null) fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
            return fallbackMaterial;
        }
    }

    /// <summary>Creates a bolt between two transforms.</summary>
    public static void Spawn(Style style, Transform from, Transform to)
    {
        var go = new GameObject("PlasmaLightning");
        var bolt = go.AddComponent<PlasmaLightning>();
        bolt.Begin(style, from, to);
    }

    private void Begin(Style lightningStyle, Transform start, Transform end)
    {
        style = lightningStyle;
        from = start;
        to = end;
        fromLast = start != null ? start.position : Vector3.zero;
        toLast = end != null ? end.position : Vector3.zero;

        halo = CreateLine("Halo", style.sortingOrder);
        core = CreateLine("Core", style.sortingOrder + 1);

        Redraw();
    }

    private LineRenderer CreateLine(string lineName, int sortingOrder)
    {
        var go = new GameObject(lineName, typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.sortingOrder = sortingOrder;
        line.material = style.material != null ? style.material : FallbackMaterial;
        return line;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= style.lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (from != null) fromLast = from.position;
        if (to != null) toLast = to.position;

        if (Time.time >= nextFlicker) Redraw();
        else FollowEnds();

        SetAlpha(1f - age / Mathf.Max(0.01f, style.lifetime));
    }

    /// <summary>Re-rolls the jagged shape (the crackle).</summary>
    private void Redraw()
    {
        nextFlicker = Time.time + Mathf.Max(0.01f, style.flickerSeconds);

        Vector3 a = fromLast;
        Vector3 b = toLast;
        float distance = Vector3.Distance(a, b);
        int pieces = Mathf.Max(2, Mathf.CeilToInt(distance / Mathf.Max(0.05f, style.segmentLength)));

        Vector3 direction = distance > 0.0001f ? (b - a) / distance : Vector3.up;
        Vector3 side = new(-direction.y, direction.x, 0f);

        core.positionCount = halo.positionCount = pieces + 1;
        core.startWidth = core.endWidth = style.width;
        halo.startWidth = halo.endWidth = style.width * style.haloWidthMultiplier;

        for (int i = 0; i <= pieces; i++)
        {
            float t = i / (float)pieces;
            Vector3 point = Vector3.Lerp(a, b, t);

            // The ends stay exactly on the ball and the block; the push is biggest in the middle.
            if (i > 0 && i < pieces)
                point += side * (Random.Range(-1f, 1f) * style.jaggedness * Mathf.Sin(t * Mathf.PI));

            core.SetPosition(i, point);
            halo.SetPosition(i, point);
        }
    }

    /// <summary>Between re-rolls, keeps just the two ends glued to what they are attached to.</summary>
    private void FollowEnds()
    {
        int last = core.positionCount - 1;
        if (last < 1) return;

        core.SetPosition(0, fromLast);
        halo.SetPosition(0, fromLast);
        core.SetPosition(last, toLast);
        halo.SetPosition(last, toLast);
    }

    private void SetAlpha(float alpha)
    {
        Color coreColor = Color.Lerp(style.color, Color.white, 0.6f); // a hot white-ish centre
        coreColor.a = alpha;
        core.startColor = core.endColor = coreColor;

        Color haloColor = style.color;
        haloColor.a = style.haloAlpha * alpha;
        halo.startColor = halo.endColor = haloColor;
    }
}
