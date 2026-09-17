using UnityEngine;

/// <summary>
/// Primitive hazard: aimed at the player's ship once at spawn (not homing) and flies straight at it,
/// detonating on the first block it touches — see ShipGrid.ApplyCollisionDamage for how that damage is
/// resolved. Also a Targetable (kind=Meteor), so point-defense weapons can shoot it down first — see
/// WeaponModule.CanEngage / Projectile.TryHitTargetable.
///
/// Needs a Collider2D (Is Trigger = on) in addition to the required components below.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Targetable))]
public class Meteor : MonoBehaviour
{
    [Tooltip("Damage dealt to whatever block it hits. Separate from Targetable.maxHP, which is how " +
             "tough the meteor itself is against being shot down.")]
    public float damage = 20f;
    public float speed = 3f;

    [Tooltip("Assign at spawn time (see EnemySpawner). Falls back to searching for the player's ship if left unset.")]
    public ShipGrid targetShip;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        var targetable = GetComponent<Targetable>();
        targetable.kind = TargetKind.Meteor;
        // Targetable.TakeDamage only flips IsDestroyed and fires an event — nothing removes the
        // GameObject on its own, so a defensive weapon shooting this down would otherwise leave it
        // sitting in the scene forever, still flying, just inert.
        targetable.OnDestroyed += _ => Destroy(gameObject);
    }

    private void Start()
    {
        if (targetShip == null)
        {
            var playerObj = GameObject.FindWithTag("PlayerShip");
            if (playerObj != null) targetShip = playerObj.GetComponentInParent<ShipGrid>();
        }

        Vector2 direction = targetShip != null
            ? ((Vector2)targetShip.transform.position - (Vector2)transform.position).normalized
            : Vector2.down;

        rb.linearVelocity = direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (targetShip == null || !other.CompareTag("PlayerShip")) return;
        if (other.GetComponent<ShipModule>() == null) return; // only an actual block counts as a hit

        Vector2Int cell = targetShip.WorldToGrid(transform.position);
        targetShip.ApplyCollisionDamage(cell, damage);
        Destroy(gameObject); // consumed on impact either way, lethal or not
    }
}
