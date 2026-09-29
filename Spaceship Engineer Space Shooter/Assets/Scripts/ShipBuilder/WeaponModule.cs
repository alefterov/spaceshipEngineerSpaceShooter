using System.Collections.Generic;
using UnityEngine;

/// <summary>Fixed = welded to the ship, fires straight along the muzzle and never rotates.
/// Turret = the turretPivot child swings to track its target, limited by turretRotationSpeed.</summary>
public enum WeaponAiming { Fixed, Turret }

/// <summary>Automatic = fires by itself whenever a valid target is in range and the cooldown is up.
/// Manual = on a PLAYER ship, never fires on its own — the player triggers it from a button (see
/// ManualWeaponController). On an ENEMY ship this distinction doesn't apply: there's no one to press a
/// button, so every weapon fires automatically regardless of this setting (see WeaponModule.Update) —
/// Manual only matters for planning the player's own loadout.</summary>
public enum WeaponFireControl { Automatic, Manual }

/// <summary>
/// A weapon slot on the ship. Independent axes — how it aims, who pulls the trigger, and which kinds of
/// target it may engage (allowedTargets) — so any combination is just prefab configuration (e.g. a manual
/// fixed cannon, a fast-spinning automatic point-defence turret that alone is allowed to shoot missiles).
///
/// Automatic weapons pick the nearest allowed target in range, aim AHEAD of it (leading a moving target
/// by its velocity and the shot's speed) and fire once the barrel points at that intercept point. A
/// turret can only swing its barrel as fast as turretRotationSpeed allows — a heavy tower gun with a low
/// speed visibly lags behind a nimble point-defence turret with a high one, and only fires once it has
/// actually caught up.
///
/// Only fires while combat is active (ShipIdentity.SetCombatActive), so weapons stay silent in the
/// main menu preview and in the ship builder. Stops instantly once ShipModule.TakeDamage() reduces
/// HP to zero — this is what makes shooting off a specific part actually matter.
///
/// Every shot costs energy from the ship's ShipEnergySystem (see EnergyCostPerShot) — a weapon simply
/// holds fire, cooldown and all, whenever the ship can't afford the next shot, the same "can't afford
/// it, do nothing" rule ShipMovement and ShieldModule already follow. This applies identically to
/// player and enemy ships, so a heavy loadout genuinely has to be power-budgeted on both sides.
/// </summary>
public class WeaponModule : ShipModule
{
    [Header("Weapon — behaviour")]
    public WeaponAiming aiming = WeaponAiming.Fixed;
    public WeaponFireControl fireControl = WeaponFireControl.Automatic;

    [Header("Weapon — targeting")]
    [Tooltip("Kinds of target this weapon may engage. Meteor is open to everything today; e.g. give " +
             "only point-defence turrets Missile, and cannons/launchers/turrets alike Torpedo. Enemy-side " +
             "kinds only count when they belong to the other faction; Meteors are hostile to everyone.")]
    public List<TargetKind> allowedTargets = new() { TargetKind.Ship, TargetKind.Meteor, TargetKind.Torpedo };
    [Tooltip("Aim where the target WILL be (using its velocity and this weapon's projectile speed) " +
             "instead of where it is now. Turn off for a weapon that should just shoot at what it sees.")]
    public bool leadTarget = true;

    [Header("Weapon — firing")]
    [Tooltip("The projectile spawned per shot — a prefab with a Projectile component (Rigidbody2D + trigger Collider2D).")]
    public GameObject projectilePrefab;
    [Tooltip("Where projectiles spawn: place this at the very tip of the barrel. Shots fly in the direction " +
             "from the turret pivot (or Top Root) toward this point, so its own rotation doesn't matter. For " +
             "a turret it must be a child of turretPivot so it swings with the barrel.")]
    public Transform muzzle;
    [Tooltip("Seconds between shots — the fire rate. 0.25 = 4 shots per second. Also what the manual-fire " +
             "button's radial cooldown ring is scaled against.")]
    public float cooldownSeconds = 0.35f;
    public float projectileDamage = 5f;
    public float projectileSpeed = 12f;
    // Energy spent PER SHOT — reuses Energy Delta (that field's own tooltip already documents it as
    // "per second"): at a shot every Cooldown Seconds, spending |Energy Delta| x Cooldown Seconds each
    // time averages out to exactly that per-second rate. Firing is skipped (held, not wasted) whenever
    // the ship's ShipEnergySystem can't afford it — the same pool engines and shields draw from, so a
    // ship's weapons, engines and shields all compete over one real energy budget. See FireIfReady.
    public float EnergyCostPerShot => Mathf.Abs(energyDelta) * EffectiveCooldownSeconds;
    [Tooltip("FIRING RANGE, in world units (1 = one grid cell). A target is engaged only within this " +
             "distance, and the shot itself disappears once it has travelled this far.")]
    public float range = 12f;
    [Tooltip("Maximum angle (total spread, degrees) between where the muzzle points and the direction to " +
             "the aim point at which this weapon will still shoot. For a Fixed weapon this is its mount's " +
             "arc. For a Turret it doubles as the aiming tolerance — while the barrel is still traversing " +
             "toward a fast target, the aim point sits outside the arc and the turret holds fire until it " +
             "has swung around. Smaller = more precise but fires later.")]
    public float fireArcDegrees = 30f;

    [Header("Weapon — moving parts")]
    [Tooltip("Separate child holding the parts that move relative to the block's body — e.g. a turret's " +
             "tower and barrel. Always drawn ABOVE every block body (ShipGrid's top sorting order), " +
             "with sorting orders authored in the prefab kept as offsets. The body itself lives under " +
             "Roof Root as for any other block. Leave unassigned for a weapon with no moving parts.")]
    public Transform topRoot;

    [Header("Turret (only used when aiming == Turret)")]
    [Tooltip("The child transform that rotates to aim — put the barrel under it, and muzzle under " +
             "that. Must NOT be visualRoot — ShipGrid owns that one for the block's grid orientation, " +
             "and the two would fight each other. Normally this IS (or lives under) Top Root.")]
    public Transform turretPivot;
    [Tooltip("Maximum degrees per second the barrel can traverse. Low (e.g. 60) for heavy tower guns, " +
             "high (e.g. 360) for point-defence turrets. If the target moves faster than this can track, " +
             "the weapon simply can't hit it.")]
    public float turretRotationSpeed = 180f;

    private readonly List<(SpriteRenderer renderer, int authoredOrder)> topRenderers = new();

    private float cooldownRemaining;
    private bool poweredOn = true;
    private bool combatActive;
    private bool warnedMisconfigured;
    private ShipIdentity identity;
    private ShipEnergySystem energy;

    /// <summary>Sustained damage output — what the stats panel's Firepower reads (see ShipGrid.ComputeFirepower).</summary>
    public float DamagePerSecond => projectileDamage / Mathf.Max(0.01f, EffectiveCooldownSeconds);

    /// <summary>1 right after firing, easing to 0 as the weapon becomes ready — drives the manual-fire
    /// button's radial cooldown ring directly.</summary>
    public float CooldownRemaining01 => Mathf.Clamp01(cooldownRemaining / Mathf.Max(0.01f, EffectiveCooldownSeconds));

    public bool IsReadyToFire => !IsDestroyed && poweredOn && cooldownRemaining <= 0f;

    private Faction OwnFaction => identity != null ? identity.faction : Faction.Player;

    /// <summary>Crew Gunner bonus — player ships only. Higher fire rate means a SHORTER cooldown, so this
    /// divides rather than multiplies.</summary>
    private float EffectiveCooldownSeconds
        => OwnFaction == Faction.Player && GameDataManager.Instance != null
            ? cooldownSeconds / Mathf.Max(0.01f, GameDataManager.Instance.GetGunnerFireRateMultiplier())
            : cooldownSeconds;

    /// <summary>Crew Gunner bonus — player ships only.</summary>
    private float EffectiveTurretRotationSpeed
        => OwnFaction == Faction.Player && GameDataManager.Instance != null
            ? turretRotationSpeed * GameDataManager.Instance.GetGunnerAimSpeedMultiplier()
            : turretRotationSpeed;

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Weapon;
        energyDelta = -Mathf.Abs(energyDelta); // weapons consume energy
        CacheRenderers(topRoot, topRenderers);
        identity = GetComponentInParent<ShipIdentity>();
        energy = GetComponentInParent<ShipEnergySystem>(); // absent = energy is simply never checked (see FireIfReady)

        // Picks up the ship's existing state, so a weapon built/loaded onto an already-fighting ship
        // isn't left inert — the reverse direction is covered by ShipIdentity.SetCombatActive.
        combatActive = identity != null && identity.CombatActive;
    }

    /// <summary>Body sprites at the roof layer (base behaviour), moving parts on top of every body.</summary>
    public override void ApplySortingOrders(int roofBase, int topBase)
    {
        base.ApplySortingOrders(roofBase, topBase);

        foreach (var (renderer, authoredOrder) in topRenderers)
            if (renderer != null) renderer.sortingOrder = topBase + authoredOrder;
    }

    private void Update()
    {
        // Ticks even out of combat, so every weapon starts a battle ready to fire.
        if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;

        if (!combatActive || IsDestroyed || !poweredOn) return;
        if (!ValidateSetup()) return;

        var target = FindTarget();
        if (target == null) return;

        Vector2 aimPoint = PredictAimPoint(target);

        if (aiming == WeaponAiming.Turret) AimTurretAt(aimPoint);

        // Manual only means anything for the player, who has a button to pull the trigger with — an
        // enemy ship has no one to press it, so every one of its weapons fires on its own (see the
        // WeaponFireControl enum doc comment).
        if (fireControl == WeaponFireControl.Automatic || OwnFaction == Faction.Enemy) TryFireAt(aimPoint);
    }

    /// <summary>Warns ONCE about a weapon that can never fire because of missing prefab wiring — silence
    /// here is exactly what makes "the guns don't shoot" so hard to track down otherwise.</summary>
    private bool ValidateSetup()
    {
        bool ok = projectilePrefab != null && muzzle != null && (aiming != WeaponAiming.Turret || turretPivot != null);
        if (ok || warnedMisconfigured) return ok;

        warnedMisconfigured = true;
        Debug.LogWarning($"Weapon '{moduleId}' can't fire: " +
                         (projectilePrefab == null ? "Projectile Prefab is not assigned. " : "") +
                         (muzzle == null ? "Muzzle is not assigned. " : "") +
                         (aiming == WeaponAiming.Turret && turretPivot == null ? "Turret Pivot is not assigned (aiming = Turret). " : "") +
                         "Fix it on the block's prefab.", this);
        return false;
    }

    /// <summary>Called by ShipIdentity.SetCombatActive — weapons hold fire entirely outside battle.</summary>
    public void SetCombatActive(bool active)
    {
        combatActive = active;
        RefreshIdleAnimation();
    }

    /// <summary>A turret's idle animation rotates its pivot; the Animator writes that rotation after every
    /// Update, overwriting the aiming code and freezing the barrel on whatever the animation says. So a
    /// turret plays its idle animation only outside combat (menu preview) and lets the code aim in battle.</summary>
    protected override bool IdleAnimationAllowed => !(combatActive && aiming == WeaponAiming.Turret);

    /// <summary>Called externally by the ship's PowerSystem when total energy runs out.</summary>
    public void SetPowered(bool powered) => poweredOn = powered;

    /// <summary>
    /// Fires if the cooldown is up and (when a target is given) the muzzle is pointing close enough to
    /// where that target is heading. Public so ManualWeaponController can pull the trigger for Manual
    /// weapons; automatic ones call it themselves. Pass no target to fire straight ahead regardless of
    /// what's there — which is what a manually triggered weapon does when the player just wants shots
    /// downrange. Returns whether a shot actually went out.
    /// </summary>
    public bool TryFire(Targetable target = null)
    {
        if (target == null) return FireIfReady(null);
        return TryFireAt(PredictAimPoint(target));
    }

    private bool TryFireAt(Vector2 aimPoint) => FireIfReady(aimPoint);

    private bool FireIfReady(Vector2? aimPoint)
    {
        if (!IsReadyToFire || projectilePrefab == null || muzzle == null) return false;
        if (aimPoint.HasValue && !InFiringArc(aimPoint.Value)) return false;
        if (energy != null && !energy.TrySpend(EnergyCostPerShot, ShipEnergySystem.EnergyPriorityGroup.Weapons)) return false; // not enough power right now — holds fire, tries again next frame

        Spawn();
        cooldownRemaining = EffectiveCooldownSeconds;
        return true;
    }

    /// <summary>The point the barrel swings around (turret pivot) or points away from (fixed mount) — the
    /// shot direction is measured from here to the muzzle. Falls back to the block's own root.</summary>
    private Vector2 BarrelOrigin
        => turretPivot != null ? (Vector2)turretPivot.position
         : topRoot != null ? (Vector2)topRoot.position
         : (Vector2)transform.position;

    /// <summary>The direction the barrel points, as a world direction: from the barrel's origin toward
    /// the Muzzle. Deliberately geometric rather than muzzle.up — so shots always leave through the tip
    /// of the barrel however the art happens to be rotated or flipped in the prefab (a 180° pivot, a
    /// mirrored sprite, a Muzzle turned around the wrong axis...). Only if the Muzzle sits exactly on
    /// the origin does it fall back to its own +Y.</summary>
    private Vector2 BarrelDirection
    {
        get
        {
            Vector2 offset = (Vector2)muzzle.position - BarrelOrigin;
            return offset.sqrMagnitude > 0.0025f ? offset.normalized : (Vector2)muzzle.up;
        }
    }

    /// <summary>The direction this weapon actually shoots in — just BarrelDirection. Enemy ships are
    /// given a real, physical facing at spawn (EnemyShip rotates the whole ship root — see Face Down),
    /// so no per-weapon correction is needed here; the muzzle's geometry already points the right way
    /// for both factions.</summary>
    private Vector2 MuzzleForward => BarrelDirection;

    private void Spawn()
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);

        var proj = Instantiate(projectilePrefab, muzzle.position, rotation);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            p.damage = projectileDamage;
            p.speed = projectileSpeed;
            p.lifeTime = (range + 1f) / Mathf.Max(0.01f, projectileSpeed); // the shot ends where the weapon's range ends (+1 unit of slack)
            p.owner = OwnFaction == Faction.Enemy ? ProjectileOwner.Enemy : ProjectileOwner.Player;
        }
    }

    private bool InFiringArc(Vector2 aimPoint)
    {
        Vector2 toAim = aimPoint - (Vector2)muzzle.position;
        return Vector2.Angle(MuzzleForward, toAim) <= fireArcDegrees * 0.5f;
    }

    /// <summary>
    /// Where to point so a shot fired now meets the target: solves for the time t at which the target
    /// (moving at constant velocity) is exactly as far from the muzzle as a shot of projectileSpeed
    /// has travelled, i.e. |d + v·t| = s·t. Falls back to the target's current position when leading is
    /// off or no interception is possible (target faster than the shot and moving away).
    /// </summary>
    private Vector2 PredictAimPoint(Targetable target)
    {
        Vector2 position = target.transform.position;
        if (!leadTarget || projectileSpeed <= 0.01f) return position;

        Vector2 origin = aiming == WeaponAiming.Turret && turretPivot != null
            ? (Vector2)turretPivot.position
            : muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;

        Vector2 d = position - origin;
        Vector2 v = target.Velocity;
        float s = projectileSpeed;

        float a = v.sqrMagnitude - s * s;
        float b = 2f * Vector2.Dot(d, v);
        float c = d.sqrMagnitude;

        float time;
        if (Mathf.Abs(a) < 0.0001f)
        {
            // Target exactly as fast as the shot — the equation degrades to linear.
            if (Mathf.Abs(b) < 0.0001f) return position;
            time = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return position;

            float root = Mathf.Sqrt(discriminant);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);

            // The earliest interception that's actually in the future.
            time = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
        }

        return time > 0f ? position + v * time : position;
    }

    /// <summary>Swings the barrel toward the aim point, but never faster than turretRotationSpeed.</summary>
    private void AimTurretAt(Vector2 aimPoint)
    {
        if (turretPivot == null) return;

        Vector2 toAim = aimPoint - (Vector2)turretPivot.position;
        if (toAim.sqrMagnitude < 0.0001f) return;

        // Rotate the pivot so the barrel's direction (pivot -> muzzle, see BarrelDirection) — not the
        // pivot's own +Y — ends up on the aim point. Measured relative to the pivot, this works for any
        // layout of the prefab, so the barrel always visibly swings toward the target.
        Vector3 shootDirectionInPivot = Quaternion.Inverse(turretPivot.rotation) * (Vector3)BarrelDirection;
        Quaternion desired = Quaternion.FromToRotation(shootDirectionInPivot, toAim);
        turretPivot.rotation = Quaternion.RotateTowards(
            turretPivot.rotation, desired, EffectiveTurretRotationSpeed * Time.deltaTime);
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
        if (!allowedTargets.Contains(candidate.kind)) return false;

        // Meteors threaten everyone; everything else is only a target if it belongs to the other side.
        return candidate.kind == TargetKind.Meteor || candidate.faction != OwnFaction;
    }

    protected override void OnModuleDestroyed()
    {
        // Weapon simply stops firing — Update() already checks IsDestroyed,
        // but this hook is here for VFX (sparks, smoke) or sound triggers.
    }
}
