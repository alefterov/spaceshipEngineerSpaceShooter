using UnityEngine;

public static class BattleZone
{
    private const float Tolerance = 0.02f;

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

    public static bool BlockInZone(Bounds bounds)
    {
        if (!TryGetView(out var min, out var max)) return true;

        return bounds.min.x >= min.x - Tolerance && bounds.max.x <= max.x + Tolerance
            && bounds.min.y >= min.y - Tolerance
            && bounds.min.y <= max.y;
    }

    public static bool PointInZone(Vector2 point)
    {
        if (!TryGetView(out var min, out var max)) return true;
        return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
    }
}
