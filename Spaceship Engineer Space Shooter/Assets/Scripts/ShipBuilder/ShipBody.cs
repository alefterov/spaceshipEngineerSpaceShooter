using UnityEngine;

public class ShipBody : MonoBehaviour
{
    [Tooltip("Mass of the ship is the sum of its blocks' mass — heavier ships get pushed less in a collision.")]
    public bool massFromBlocks = true;

    private Vector2 contactNormal;
    private float contactUntil;
    private float haltUntil;
    private Vector2 knock;
    private float knockDecay = 6f;

    public bool InContact => Time.time < contactUntil;

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

    public void RegisterContact(Vector2 normal, float memorySeconds, bool impact, float haltSeconds, Vector2 knockVelocity, float decay)
    {
        contactNormal = normal;
        contactUntil = Time.time + memorySeconds;

        if (!impact) return;

        haltUntil = Time.time + haltSeconds;
        knock = knockVelocity;
        knockDecay = Mathf.Max(0.01f, decay);
    }

    public Vector3 FilterMove(Vector3 delta)
    {
        if (Time.time < haltUntil) return Vector3.zero;

        if (InContact)
        {
            Vector2 step = delta;
            float into = Vector2.Dot(step, contactNormal);
            if (into < 0f) step -= contactNormal * into;
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

    public static ShipBody GetOrAdd(Component ship)
    {
        var body = ship.GetComponent<ShipBody>();
        return body != null ? body : ship.gameObject.AddComponent<ShipBody>();
    }
}
