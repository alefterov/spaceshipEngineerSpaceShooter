using UnityEngine;

/// <summary>
/// A lifeless drifting chunk: a part of an enemy ship cut loose by destroyed blocks (see ShipSplitter). It does NOT home
/// in on the player. It picks one direction when it breaks off — roughly down the screen, within Angle Range degrees
/// of straight down — and keeps flying that way at Drift Speed with its own spin (each piece turns differently), so
/// pieces can get in the way of the player's ship and collide with it (RamCollision). Its blocks are still enemy
/// blocks: they can be shot and hit by missiles. It never fires. Once it has left the screen (bottom or sides) it is removed.
/// </summary>
public class ShipFragment : MonoBehaviour
{
    private ShipBody body;
    private Vector2 direction = Vector2.down;
    private float driftSpeed;
    private float spinDegreesPerSecond;
    private float despawnMargin = 2f;

    /// <summary>Starts the fragment drifting. `angleRange` = the most its direction may differ from straight down, in
    /// degrees, either way; `maxSpin` = the most it turns per second, either way. Both are random within their range.</summary>
    public void Begin(float speed, float maxSpin, float angleRange, float despawnMargin = 2f)
    {
        body = ShipBody.GetOrAdd(this);
        driftSpeed = speed;
        this.despawnMargin = despawnMargin;
        spinDegreesPerSecond = Random.Range(-maxSpin, maxSpin);
        direction = RandomDownDirection(angleRange);
    }

    /// <summary>A direction within `angleRange` degrees of straight down.</summary>
    public static Vector2 RandomDownDirection(float angleRange)
        => (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-angleRange, angleRange)) * Vector3.down);

    private void Update()
    {
        if (body == null) body = ShipBody.GetOrAdd(this);

        transform.position += body.FilterMove((Vector3)(direction * driftSpeed * Time.deltaTime));
        // It turns about its OWN middle, so the middle keeps going in a straight line while the pieces spin around it.
        var ship = GetComponent<ShipIdentity>();
        transform.RotateAround(CenterOf(ship), Vector3.forward, spinDegreesPerSecond * Time.deltaTime);

        if (HasLeftScreen(ship, despawnMargin)) Destroy(gameObject);
    }

    /// <summary>The middle of the ship's living blocks (the root itself if there are none).</summary>
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

    /// <summary>True once every block of the ship is more than `margin` world units outside the camera view — past the bottom or a side. Above the
    /// top edge does NOT count (a piece may be born up there and still be on its way in).</summary>
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
