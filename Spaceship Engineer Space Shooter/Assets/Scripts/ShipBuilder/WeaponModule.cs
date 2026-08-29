using UnityEngine;

/// <summary>
/// A weapon slot on the ship. Fires automatically while alive and powered.
/// Stops instantly once its ShipModule.TakeDamage() reduces HP to zero —
/// this is what makes "отстрелить конкретную деталь" actually matter in combat.
/// </summary>
public class WeaponModule : ShipModule
{
    [Header("Weapon")]
    public GameObject projectilePrefab;
    public Transform muzzle;              // where projectiles spawn from
    public float fireRate = 3f;           // shots per second
    public float projectileDamage = 5f;
    public float projectileSpeed = 12f;

    [Tooltip("Firing arc in degrees relative to the ship's forward direction. " +
             "A weapon mounted on the side of the hull should have a narrower/offset arc.")]
    public float fireArcDegrees = 30f;

    private float fireCooldown;
    private bool poweredOn = true;
    private ShipIdentity identity;

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Weapon;
        energyDelta = -Mathf.Abs(energyDelta); // weapons consume energy
        identity = GetComponentInParent<ShipIdentity>();
    }

    private void Update()
    {
        if (IsDestroyed || !poweredOn) return;

        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            Fire();
            fireCooldown = 1f / Mathf.Max(0.01f, fireRate);
        }
    }

    /// <summary>Called externally by the ship's PowerSystem when total energy runs out.</summary>
    public void SetPowered(bool powered) => poweredOn = powered;

    private void Fire()
    {
        if (projectilePrefab == null || muzzle == null) return;

        bool isEnemy = identity != null && identity.faction == Faction.Enemy;
        Quaternion rotation = isEnemy ? Quaternion.Euler(0, 0, 180f) * muzzle.rotation : muzzle.rotation;

        var proj = Instantiate(projectilePrefab, muzzle.position, rotation);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            p.damage = projectileDamage;
            p.speed = projectileSpeed;
            p.owner = isEnemy ? ProjectileOwner.Enemy : ProjectileOwner.Player;
        }
    }

    protected override void OnModuleDestroyed()
    {
        // Weapon simply stops firing — Update() already checks IsDestroyed,
        // but this hook is here for VFX (sparks, smoke) or sound triggers.
    }
}
