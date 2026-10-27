using UnityEngine;

/// <summary>
/// The battle zone: the area the battle camera sees. A block of an enemy ship can be hurt (and is a target for the player's weapons
/// and missiles) only while it is in the zone. A block is IN the zone when it lies completely between the left, right and bottom
/// edges, and either lies completely under the top edge or is just passing through it — so a ship flying in from above can be
/// shot at with the part that has already come in, while the rest of it is still beyond the edge. With no camera to measure
/// against, everything counts as inside.
/// </summary>
public static class BattleZone
{
    private const float Tolerance = 0.02f; // a block resting exactly against an edge still counts as inside

    private static int viewFrame = -1;
    private static bool hasView;
    private static Vector2 viewMin, viewMax;

    private static bool TryGetView(out Vector2 min, out Vector2 max)
    {
        if (viewFrame != Time.frameCount)
        {
            viewFrame = Time.frameCount;
            var cam = Camera.main;
            hasView = cam != null && cam.orthographic;
            if (hasView)
            {
                float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
                Vector3 c = cam.transform.position;
                viewMin = new Vector2(c.x - halfWidth, c.y - halfHeight);
                viewMax = new Vector2(c.x + halfWidth, c.y + halfHeight);
            }
        }

        min = viewMin;
        max = viewMax;
        return hasView;
    }

    /// <summary>Whether a block with these world bounds is in the battle zone (see the class comment).</summary>
    public static bool BlockInZone(Bounds bounds)
    {
        if (!TryGetView(out var min, out var max)) return true;

        return bounds.min.x >= min.x - Tolerance && bounds.max.x <= max.x + Tolerance   // inside left and right
            && bounds.min.y >= min.y - Tolerance                                          // not sunk below the bottom edge
            && bounds.min.y <= max.y;                                                     // at least its lower edge has come under the top edge
    }

    /// <summary>Whether a point (the middle of a meteor, say) is inside the camera's view.</summary>
    public static bool PointInZone(Vector2 point)
    {
        if (!TryGetView(out var min, out var max)) return true;
        return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
    }
}
