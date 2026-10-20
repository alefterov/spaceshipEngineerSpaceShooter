using UnityEngine;

/// <summary>
/// The physical side of a ship colliding with another ship (see RamCollision): where it is touching, a short
/// stop, and the bounce. Ships here move by having their position set directly, so they know nothing about
/// each other — this is what keeps them from sliding through one another:
///  - Whatever moves the ship (player's ShipMovement, an EnemyShip, a fragment) passes its step through
///    FilterMove. On impact the ship is stopped dead for Halt Seconds; afterwards it simply can't push further INTO
///    what it is touching (it can still slide along it or back away).
///  - On the first touch both ships get a knock away from each other, heavier ships moving less; it dies away
///    over a moment.
///
/// Added to ships automatically by RamCollision (player ship included, when an enemy touches it).
/// </summary>
public class ShipBody : MonoBehaviour
{
    [Tooltip("Mass of the ship is the sum of its blocks' mass — heavier ships get pushed less in a collision.")]
    public bool massFromBlocks = true;

    private Vector2 contactNormal;   // points AWAY from whatever this ship is touching
    private float contactUntil;
    private float haltUntil;
    private Vector2 knock;           // knockback velocity, world units per second
    private float knockDecay = 6f;   // units per second the knock slows by

    /// <summary>Whether the ship is touching another ship right now (or just was).</summary>
    public bool InContact => Time.time < contactUntil;

    /// <summary>Total mass of the living blocks (a fixed 1 if the ship has no identity).</summary>
    public float Mass
    {
        get
        {
            var ship = GetComponent<ShipIdentity>();
            if (ship == null || !massFromBlocks) return 1f;

            float total = 0f;
            foreach (var block in ship.LiveBlocks)
                if (block != null && !block.IsDestroyed) total += block.mass;
            return Mathf.Max(0.1f, total);
        }
    }

    /// <summary>Called by RamCollision every frame two ships overlap. `normal` points away from the other ship.
    /// `impact` = the first frame of a fresh touch: the ship stops dead for `haltSeconds` and is knocked.</summary>
    public void RegisterContact(Vector2 normal, float memorySeconds, bool impact, float haltSeconds, Vector2 knockVelocity, float decay)
    {
        contactNormal = normal;
        contactUntil = Time.time + memorySeconds;

        if (!impact) return;

        haltUntil = Time.time + haltSeconds;
        knock = knockVelocity;
        knockDecay = Mathf.Max(0.01f, decay);
    }

    /// <summary>Pass every self-propelled step through this. Returns the step the ship may actually take: nothing at
    /// all during the stop after an impact, otherwise the step with any part that pushes into the other ship removed.</summary>
    public Vector3 FilterMove(Vector3 delta)
    {
        if (Time.time < haltUntil) return Vector3.zero;

        if (InContact)
        {
            Vector2 step = delta;
            float into = Vector2.Dot(step, contactNormal);
            if (into < 0f) step -= contactNormal * into; // remove the part that goes against the normal (into the other ship)
            return new Vector3(step.x, step.y, delta.z);
        }

        return delta;
    }

    private void LateUpdate()
    {
        if (knock.sqrMagnitude < 0.0001f) return;

        transform.position += (Vector3)(knock * Time.deltaTime);
        knock = Vector2.MoveTowards(knock, Vector2.zero, knockDecay * Time.deltaTime);
    }

    /// <summary>The ShipBody of this object's ship, added if it has none.</summary>
    public static ShipBody GetOrAdd(Component ship)
    {
        var body = ship.GetComponent<ShipBody>();
        return body != null ? body : ship.gameObject.AddComponent<ShipBody>();
    }
}
