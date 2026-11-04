using UnityEngine;

public class ShipFragment : MonoBehaviour
{
    private ShipBody body;
    private Vector2 direction = Vector2.down;
    private float driftSpeed;
    private float spinDegreesPerSecond;
    private float despawnMargin = 2f;

    public void Begin(float speed, float maxSpin, float angleRange, float despawnMargin = 2f)
    {
        body = ShipBody.GetOrAdd(this);
        driftSpeed = speed;
        this.despawnMargin = despawnMargin;
        spinDegreesPerSecond = Random.Range(-maxSpin, maxSpin);
        direction = RandomDownDirection(angleRange);
    }

    public static Vector2 RandomDownDirection(float angleRange)
        => (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-angleRange, angleRange)) * Vector3.down);

    private void Update()
    {
        if (body == null) body = ShipBody.GetOrAdd(this);

        transform.position += body.FilterMove((Vector3)(direction * driftSpeed * Time.deltaTime));
        var ship = GetComponent<ShipIdentity>();
        transform.RotateAround(CenterOf(ship), Vector3.forward, spinDegreesPerSecond * Time.deltaTime);

        if (HasLeftScreen(ship, despawnMargin)) Destroy(gameObject);
    }

    public static Vector3 CenterOf(ShipIdentity ship)
    {
        if (ship == null) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var block in ship.LiveBlocks)
        {
            if (block == null || block.IsDestroyed) continue;
            sum += block.visualRoot != null ? block.visualRoot.position : block.transform.position;
            count++;
        }

        return count > 0 ? sum / count : ship.transform.position;
    }

    public static bool HasLeftScreen(ShipIdentity ship, float margin = 2f)
    {
        var cam = Camera.main;
        if (ship == null || cam == null || !cam.orthographic) return false;

        bool any = false;
        Bounds bounds = default;
        foreach (var block in ship.LiveBlocks)
        {
            if (block == null || block.IsDestroyed || !block.TryGetComponent<Collider2D>(out var c)) continue;
            if (!any) { bounds = c.bounds; any = true; }
            else bounds.Encapsulate(c.bounds);
        }

        if (!any) return false;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 center = cam.transform.position;

        return bounds.max.y < center.y - halfHeight - margin
            || bounds.max.x < center.x - halfWidth - margin
            || bounds.min.x > center.x + halfWidth + margin;
    }
}
