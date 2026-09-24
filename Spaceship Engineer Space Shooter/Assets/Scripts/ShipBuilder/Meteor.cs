using UnityEngine;

/// <summary>
/// Primitive hazard: aimed at the player's ship once at spawn (not homing) and flies straight at it,
/// detonating on the first block it touches — that block (and only that block) takes the damage from its
/// own HP pool. Also a Targetable (kind=Meteor), so point-defense weapons can shoot it down first — see
/// WeaponModule.CanEngage / Projectile.TryHitTargetable.
///
/// Needs a Collider2D (Is Trigger = on) in addition to the required components below.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Targetable))]
public class Meteor : MonoBehaviour, IShieldBlockable
{
    [Tooltip("Damage dealt to whatever block it hits. Separate from Targetable.maxHP, which is how " +
             "tough the meteor itself is against being shot down.")]
    public float damage = 20f;
    public float speed = 3f;
    [Tooltip("Added to BattleScore when this meteor is shot down by a weapon — NOT when it hits the " +
             "ship instead, and not when it just flies past and misses (see OnDestroyed below and Update).")]
    public int scoreValue = 10;
    [Tooltip("World-space distance beyond the camera's edge before this meteor despawns for having missed entirely.")]
    public float offscreenMargin = 3f;

    [Tooltip("Assign at spawn time (see EnemySpawner). Falls back to searching for the player's ship if left unset.")]
    public ShipGrid targetShip;

    private Rigidbody2D rb;
    private bool consumed;

    // ---------- IShieldBlockable — a shield's protected area stops it before it reaches a block ----------
    public float ImpactDamage => damage;
    public bool IsConsumed => consumed;
    public bool IsHostileTo(Faction shieldFaction) => shieldFaction == Faction.Player; // only ever aimed at the player's ship
    public void AbsorbedByShield()
    {
        consumed = true;
        Destroy(gameObject); // no score — only being shot down awards points (see Awake)
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        var targetable = GetComponent<Targetable>();
        targetable.kind = TargetKind.Meteor;
        // Targetable.TakeDamage only flips IsDestroyed and fires an event — nothing removes the
        // GameObject on its own, so a defensive weapon shooting this down would otherwise leave it
        // sitting in the scene forever, still flying, just inert. This path is ALSO the only one that
        // awards score — hitting the ship or flying off-screen both call Destroy(gameObject) directly
        // further down, bypassing Targetable.TakeDamage entirely, so neither one reaches this handler.
        targetable.OnDestroyed += _ =>
        {
            BattleScore.Add(scoreValue);
            Destroy(gameObject);
        };
    }

    private void Update()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfHeight = cam.orthographicSize + offscreenMargin;
        float halfWidth = halfHeight * cam.aspect;
        Vector2 relative = (Vector2)transform.position - (Vector2)cam.transform.position;

        if (Mathf.Abs(relative.x) > halfWidth || Mathf.Abs(relative.y) > halfHeight)
            Destroy(gameObject); // missed the ship and flew off-screen — no score, doesn't count as a kill
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
        if (consumed || targetShip == null || !other.CompareTag("PlayerShip")) return;

        var hitModule = other.GetComponent<ShipModule>();
        if (hitModule == null) return; // only an actual block counts as a hit

        // The collider that fired the trigger belongs to exactly one block — no grid lookup needed
        // (a WorldToGrid on the meteor's own center would resolve to the wrong cell anyway, since its
        // collider is large enough to fire while its center is still outside the block it touched).
        hitModule.TakeDamage(damage);
        Destroy(gameObject); // consumed on impact either way, lethal or not
    }
}
