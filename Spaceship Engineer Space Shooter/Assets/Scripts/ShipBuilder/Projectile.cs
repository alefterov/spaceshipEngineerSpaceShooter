using UnityEngine;

public enum ProjectileOwner { Player, Enemy }

/// <summary>
/// Simple projectile that damages exactly the module collider it hits —
/// this is the core piece that makes "точечный урон по модулям" work.
/// Requires each module's Collider2D to be on its own GameObject (not a shared
/// composite collider) so OnTriggerEnter2D resolves to the specific part hit.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour, IShieldBlockable
{
    public float damage = 5f;
    public float speed = 12f;
    public ProjectileOwner owner = ProjectileOwner.Player;
    [Tooltip("Seconds before it disappears. A weapon that fires it overrides this so the shot stops at " +
             "the weapon's Range (see WeaponModule.Spawn).")]
    public float lifeTime = 4f;

    private Rigidbody2D rb;
    private bool consumed;

    // ---------- IShieldBlockable — a shield stops shots fired by the OTHER side; its own side's pass through ----------
    public float ImpactDamage => damage;
    public bool IsConsumed => consumed;
    public bool IsHostileTo(Faction shieldFaction)
        => (owner == ProjectileOwner.Player ? Faction.Player : Faction.Enemy) != shieldFaction;
    public void AbsorbedByShield()
    {
        consumed = true;
        Destroy(gameObject);
    }

    private void Awake() => rb = GetComponent<Rigidbody2D>();

    private void Start()
    {
        rb.linearVelocity = transform.up * speed;

        // Scheduled here rather than in Awake: Awake runs inside Instantiate, before the weapon that
        // fired this shot has had a chance to set lifeTime from its range.
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (TryHitModule(other) || TryHitTargetable(other))
            Destroy(gameObject);
    }

    /// <summary>Ships: damage resolves to the SPECIFIC module collider hit, not to some abstract
    /// whole-ship HP bar — this is what makes shooting off individual parts work.</summary>
    private bool TryHitModule(Collider2D other)
    {
        var hitModule = other.GetComponent<ShipModule>();
        if (hitModule == null) return false;

        bool isEnemyModule = other.CompareTag("EnemyShip");
        bool isPlayerModule = other.CompareTag("PlayerShip");

        if (owner == ProjectileOwner.Player && !isEnemyModule) return false;
        if (owner == ProjectileOwner.Enemy && !isPlayerModule) return false;

        hitModule.TakeDamage(damage);
        return true;
    }

    /// <summary>Missiles and meteors — single-HP objects rather than module assemblies, which is what
    /// defensive turrets are built to knock down. TargetKind.Projectile is excluded on purpose: shots
    /// can never shoot down other shots (same rule WeaponModule.CanEngage enforces when aiming).</summary>
    private bool TryHitTargetable(Collider2D other)
    {
        var target = other.GetComponent<Targetable>();
        if (target == null || target.IsDestroyed) return false;
        if (target.kind is TargetKind.Ship or TargetKind.Projectile) return false;

        var ownerFaction = owner == ProjectileOwner.Enemy ? Faction.Enemy : Faction.Player;
        bool hostile = target.kind == TargetKind.Meteor || target.faction != ownerFaction;
        if (!hostile) return false;

        target.TakeDamage(damage);
        return true;
    }
}
