using UnityEngine;

/// <summary>Fixed = welded to the hull, fires straight along the muzzle and never rotates.
/// Turret = the turretPivot child swings to track its target.</summary>
public enum WeaponAiming { Fixed, Turret }

/// <summary>Automatic = fires by itself whenever a valid target is in range and the cooldown is up.
/// Manual = never fires on its own; the player triggers it from a button (see ManualWeaponController).</summary>
public enum WeaponFireControl { Automatic, Manual }

/// <summary>Offensive = shoots enemy ships. Defensive = point-defence, engages incoming threats
/// (missiles, meteors, ships) ONLY, and can never intercept enemy projectiles.</summary>
public enum WeaponRole { Offensive, Defensive }

/// <summary>
/// A weapon slot on the ship. Three independent axes — how it aims, who pulls the trigger, and what
/// it is allowed to shoot at — so any combination is just prefab configuration (e.g. a manual
/// fixed cannon, an automatic defensive turret).
///
/// Only fires while combat is active (ShipIdentity.SetCombatActive), so weapons stay silent in the
/// main menu preview and in the ship builder. Stops instantly once ShipModule.TakeDamage() reduces
/// HP to zero — this is what makes shooting off a specific part actually matter.
/// </summary>
public class WeaponModule : ShipModule
{
    [Header("Weapon — behaviour")]
    public WeaponAiming aiming = WeaponAiming.Fixed;
    public WeaponFireControl fireControl = WeaponFireControl.Automatic;
    public WeaponRole role = WeaponRole.Offensive;

    [Header("Weapon — firing")]
    public GameObject projectilePrefab;
    [Tooltip("Where projectiles spawn from, pointing along its own +Y. For a turret this must be a " +
             "child of turretPivot so it swings with the barrel.")]
    public Transform muzzle;
    [Tooltip("Seconds before this weapon can fire again. Also what the manual-fire button's radial " +
             "cooldown ring is scaled against.")]
    public float cooldownSeconds = 0.35f;
    public float projectileDamage = 5f;
    public float projectileSpeed = 12f;
    [Tooltip("How far away a target can be and still be engaged, in world units.")]
    public float range = 12f;
    [Tooltip("Maximum angle (total spread, degrees) off the muzzle's forward direction at which this " +
             "weapon will still shoot. For a Fixed weapon this is its mount's arc. For a Turret it " +
             "doubles as the aiming tolerance — while the barrel is still traversing, the target sits " +
             "outside the arc and the turret holds fire until it has swung around.")]
    public float fireArcDegrees = 30f;

    [Header("Turret (only used when aiming == Turret)")]
    [Tooltip("The child transform that rotates to aim — put the barrel under it, and muzzle under " +
             "that. Must NOT be visualRoot — ShipGrid owns that one for the block's grid orientation, " +
             "and the two would fight each other. Normally this IS (or lives under) ShipModule.topRoot, " +
             "since the barrel is exactly the kind of part that stays visible in every view and draws " +
             "above the roof.")]
    public Transform turretPivot;
    [Tooltip("Degrees per second the barrel can traverse.")]
    public float turretRotationSpeed = 180f;

    private float cooldownRemaining;
    private bool poweredOn = true;
    private bool combatActive;
    private ShipIdentity identity;

    /// <summary>Sustained damage output — what the stats panel's Firepower reads (see ShipGrid.ComputeFirepower).</summary>
    public float DamagePerSecond => projectileDamage / Mathf.Max(0.01f, cooldownSeconds);

    /// <summary>1 right after firing, easing to 0 as the weapon becomes ready — drives the manual-fire
    /// button's radial cooldown ring directly.</summary>
    public float CooldownRemaining01 => Mathf.Clamp01(cooldownRemaining / Mathf.Max(0.01f, cooldownSeconds));

    public bool IsReadyToFire => !IsDestroyed && poweredOn && cooldownRemaining <= 0f;

    private Faction OwnFaction => identity != null ? identity.faction : Faction.Player;

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Weapon;
        energyDelta = -Mathf.Abs(energyDelta); // weapons consume energy
        identity = GetComponentInParent<ShipIdentity>();

        // Picks up the ship's existing state, so a weapon built/loaded onto an already-fighting ship
        // isn't left inert — the reverse direction is covered by ShipIdentity.SetCombatActive.
        combatActive = identity != null && identity.CombatActive;
    }

    private void Update()
    {
        // Ticks even out of combat, so every weapon starts a battle ready to fire.
        if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;

        if (!combatActive || IsDestroyed || !poweredOn) return;

        var target = FindTarget();

        if (aiming == WeaponAiming.Turret) AimTurretAt(target);

        if (fireControl == WeaponFireControl.Automatic && target != null) TryFire(target);
    }

    /// <summary>Called by ShipIdentity.SetCombatActive — weapons hold fire entirely outside battle.</summary>
    public void SetCombatActive(bool active) => combatActive = active;

    /// <summary>Called externally by the ship's PowerSystem when total energy runs out.</summary>
    public void SetPowered(bool powered) => poweredOn = powered;

    /// <summary>
    /// Fires if the cooldown is up and the target sits inside the firing arc. Public so
    /// ManualWeaponController can pull the trigger for Manual weapons; automatic ones call it
    /// themselves. Pass no target to fire straight ahead regardless of what's there — which is what a
    /// manually triggered weapon does when the player just wants shots downrange.
    /// Returns whether a shot actually went out.
    /// </summary>
    public bool TryFire(Targetable target = null)
    {
        if (!IsReadyToFire || projectilePrefab == null || muzzle == null) return false;
        if (target != null && !InFiringArc(target.transform.position)) return false;

        Spawn();
        cooldownRemaining = cooldownSeconds;
        return true;
    }

    /// <summary>The direction this weapon actually shoots in. Enemy ships are spawned unrotated
    /// (EnemyShipSpawner), so their fixed mounts are flipped here instead — keeping aiming checks and
    /// the spawned projectile consistent with one another rather than correcting only one of the two.</summary>
    private Vector2 MuzzleForward
    {
        get
        {
            bool flip = aiming == WeaponAiming.Fixed && OwnFaction == Faction.Enemy;
            return flip ? -(Vector2)muzzle.up : (Vector2)muzzle.up;
        }
    }

    private void Spawn()
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);

        var proj = Instantiate(projectilePrefab, muzzle.position, rotation);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            p.damage = projectileDamage;
            p.speed = projectileSpeed;
            p.owner = OwnFaction == Faction.Enemy ? ProjectileOwner.Enemy : ProjectileOwner.Player;
        }
    }

    private bool InFiringArc(Vector3 targetPosition)
    {
        Vector2 toTarget = (Vector2)targetPosition - (Vector2)muzzle.position;
        return Vector2.Angle(MuzzleForward, toTarget) <= fireArcDegrees * 0.5f;
    }

    private void AimTurretAt(Targetable target)
    {
        if (turretPivot == null || target == null) return;

        Vector2 toTarget = (Vector2)target.transform.position - (Vector2)turretPivot.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.FromToRotation(Vector3.up, toTarget);
        turretPivot.rotation = Quaternion.RotateTowards(
            turretPivot.rotation, desired, turretRotationSpeed * Time.deltaTime);
    }

    /// <summary>Nearest engageable target within range, or null.</summary>
    private Targetable FindTarget()
    {
        Targetable best = null;
        float bestSqr = range * range;
        Vector2 origin = transform.position;

        foreach (var candidate in Targetable.Active)
        {
            if (candidate == null || candidate.IsDestroyed || !CanEngage(candidate)) continue;

            float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }

    private bool CanEngage(Targetable candidate)
    {
        // Meteors threaten everyone; everything else is only a target if it belongs to the other side.
        bool hostile = candidate.kind == TargetKind.Meteor || candidate.faction != OwnFaction;
        if (!hostile) return false;

        // Point defence engages incoming threats only. Projectile is deliberately absent from this
        // list: defensive turrets must never be able to shoot down enemy shots.
        if (role == WeaponRole.Defensive)
            return candidate.kind is TargetKind.Missile or TargetKind.Meteor or TargetKind.Ship;

        return candidate.kind == TargetKind.Ship;
    }

    protected override void OnModuleDestroyed()
    {
        // Weapon simply stops firing — Update() already checks IsDestroyed,
        // but this hook is here for VFX (sparks, smoke) or sound triggers.
    }
}
