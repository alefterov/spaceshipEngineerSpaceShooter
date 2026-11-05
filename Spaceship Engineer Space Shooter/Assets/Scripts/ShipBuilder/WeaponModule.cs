using System.Collections.Generic;
using UnityEngine;

public enum WeaponAiming { Fixed, Turret }

public enum WeaponFireControl { Automatic, Manual }

public class WeaponModule : ShipModule
{
    [Header("Weapon — behaviour")]
    public WeaponAiming aiming = WeaponAiming.Fixed;
    public WeaponFireControl fireControl = WeaponFireControl.Automatic;
    [Tooltip("Ballistic / Laser / Missile / Plasma — picks the crew damage perk that boosts this weapon and the " +
             "evasion perk that can dodge its shots. Set automatically from the BlockDefinition when the block is " +
             "placed, so it only needs hand-setting on a prefab used outside the ship builder.")]
    public WeaponClass weaponClass = WeaponClass.Ballistic;

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
    [Tooltip("The tips of the barrels — one Transform per barrel (1 element for an ordinary gun, 2 for a " +
             "double-barrel, 3 for a triple). Place each at the very tip of its barrel. All barrels shoot parallel, in " +
             "the direction from the turret pivot (or Top Root) toward the MIDDLE of all the tips, so place them " +
             "symmetrically around the barrel axis; a turret's tips must be children of turretPivot so they swing " +
             "with it. Every barrel has its own reload, and each shot costs the full energy of a shot. The ORDER of " +
             "the list is the priority: when the ship can't pay for every shot, the first barrels in the list fire and " +
             "the last ones wait.")]
    public List<Transform> muzzles = new();
    [SerializeField, HideInInspector] private Transform muzzle;
    [Tooltip("Seconds between shots — the fire rate. 0.25 = 4 shots per second. Also what the manual-fire " +
             "button's radial cooldown ring is scaled against.")]
    public float cooldownSeconds = 0.35f;
    public float projectileDamage = 5f;
    public float projectileSpeed = 12f;
    public virtual float EnergyCostPerShot => energyPerShot;
    [Tooltip("FIRING RANGE, in world units (1 = one grid cell). A target is engaged only within this " +
             "distance, and the shot itself disappears once it has travelled this far.")]
    public float range = 12f;
    [Tooltip("Maximum angle (total spread, degrees) between where the muzzle points and the direction to " +
             "the aim point at which this weapon will still shoot. For a Fixed weapon this is its mount's " +
             "arc. For a Turret it doubles as the aiming tolerance — while the barrel is still traversing " +
             "toward a fast target, the aim point sits outside the arc and the turret holds fire until it " +
             "has swung around. Smaller = more precise but fires later.")]
    public float fireArcDegrees = 30f;

    [Header("Weapon — sound")]
    [Tooltip("Sound of one shot (for a laser: the beam switching on; for a missile launcher: a launch; for plasma: the balls leaving). Every kind of weapon has its own.")]
    public AudioClip fireSound;
    [Range(0f, 1f)] public float fireVolume = 1f;
    [Range(0f, 1f), Tooltip("Enemy ships fire this weapon at this share of the volume (many enemies at once would drown everything out).")]
    public float enemyVolumeScale = 0.7f;

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
    [Tooltip("A turret fires only once its barrel points at the aim point within this many degrees (angle off the " +
             "target) — while it is still swinging round to the target it holds fire. Smaller = it waits for a more exact lock.")]
    [Min(0.1f)] public float turretAimToleranceDegrees = 3f;

    private readonly List<(SpriteRenderer renderer, int authoredOrder)> topRenderers = new();

    protected readonly List<Transform> barrels = new();
    protected float[] barrelCooldowns = new float[1];
    private float energyPerShot;
    protected float EnergyPerShot => energyPerShot;
    protected ShipEnergySystem Energy => energy;
    private bool poweredOn = true;
    private bool combatActive;
    protected bool CombatActive => combatActive;
    protected bool IsPoweredOn => poweredOn;
    private bool warnedMisconfigured;
    private ShipIdentity identity;
    private ShipEnergySystem energy;

    protected virtual float ShotCycleSeconds => EffectiveCooldownSeconds;

    public override float EnergyPerSecondEstimate => -EnergyCostPerShot * BarrelCount / Mathf.Max(0.01f, ShotCycleSeconds);

    public virtual float NominalDamagePerShot => projectileDamage;

    public virtual float NominalDamagePerSecond => projectileDamage * NominalBarrels / Mathf.Max(0.01f, cooldownSeconds);

    protected int NominalBarrels
    {
        get
        {
            int count = 0;
            foreach (var tip in muzzles) if (tip != null) count++;
            if (count == 0 && muzzle != null) count = 1;
            return Mathf.Max(1, count);
        }
    }

    public virtual float DamagePerSecond => projectileDamage * EffectiveDamageMultiplier * BarrelCount / Mathf.Max(0.01f, EffectiveCooldownSeconds);

    public int BarrelCount => barrels.Count > 0 ? barrels.Count : 1;

    public float CooldownRemaining01 => Mathf.Clamp01(ShortestCooldown / Mathf.Max(0.01f, EffectiveCooldownSeconds));

    public bool IsReadyToFire => !IsDestroyed && poweredOn && ShortestCooldown <= 0f;

    private float ShortestCooldown
    {
        get
        {
            float shortest = float.MaxValue;
            foreach (float c in barrelCooldowns) shortest = Mathf.Min(shortest, c);
            return shortest == float.MaxValue ? 0f : Mathf.Max(0f, shortest);
        }
    }

    protected Faction OwnFaction => identity != null ? identity.faction : Faction.Player;

    protected float EffectiveCooldownSeconds => cooldownSeconds;

    protected float EffectiveDamageMultiplier
        => OwnFaction == Faction.Player && GameDataManager.Instance != null
            ? GameDataManager.Instance.GetCrewPerkMultiplier(DamagePerkFor(weaponClass))
            : 1f;

    private static CrewPerkType DamagePerkFor(WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.Missile => CrewPerkType.MissileDamage,
        WeaponClass.Laser => CrewPerkType.LaserDamage,
        WeaponClass.Plasma => CrewPerkType.PlasmaDamage,
        _ => CrewPerkType.BallisticDamage,
    };

    private float EffectiveTurretRotationSpeed => turretRotationSpeed;

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Weapon;
        energyDelta = -Mathf.Abs(energyDelta);
        energyPerShot = Mathf.Abs(energyDelta);

        BuildBarrels();
        CacheRenderers(topRoot, topRenderers);
        identity = GetComponentInParent<ShipIdentity>();
        energy = GetComponentInParent<ShipEnergySystem>();

        combatActive = identity != null && identity.CombatActive;
    }

    protected override System.Collections.Generic.IEnumerable<Transform> VisualParts
    {
        get
        {
            foreach (var part in base.VisualParts) yield return part;
            if (topRoot != null) yield return topRoot;
            if (turretPivot != null) yield return turretPivot;
        }
    }

    private int topBaseCached = 40;
    private int layerRank;
    private int layerStep = 10;

    public override void ApplySortingOrders(int roofBase, int topBase)
    {
        base.ApplySortingOrders(roofBase, topBase);

        topBaseCached = topBase;
        ApplyTopOrders();
    }

    public void SetLayerRank(int rank, int step)
    {
        if (rank == layerRank && step == layerStep) return;

        layerRank = rank;
        layerStep = Mathf.Max(1, step);
        ApplyTopOrders();
    }

    private void ApplyTopOrders()
    {
        foreach (var (renderer, authoredOrder) in topRenderers)
            if (renderer != null) renderer.sortingOrder = topBaseCached + layerRank * layerStep + authoredOrder;
    }

    protected void TickCooldowns()
    {
        float step = Time.deltaTime * FireRateMultiplier;
        for (int i = 0; i < barrelCooldowns.Length; i++)
            if (barrelCooldowns[i] > 0f) barrelCooldowns[i] -= step;
    }

    public bool SupportsAbility(WeaponAbility ability) => ability switch
    {
        WeaponAbility.RapidFire => weaponClass == WeaponClass.Ballistic,
        WeaponAbility.MissileDoubleSalvo => weaponClass == WeaponClass.Missile,
        WeaponAbility.PlasmaDoubleSalvo => weaponClass == WeaponClass.Plasma,
        WeaponAbility.LongLaser => weaponClass == WeaponClass.Laser,
        _ => false,
    };

    protected float FireRateMultiplier
        => OwnFaction == Faction.Player && SupportsAbility(WeaponAbility.RapidFire) ? WeaponAbilities.Strength(WeaponAbility.RapidFire) : 1f;

    private bool TryGetDoubleSalvo(out WeaponAbility ability)
    {
        ability = weaponClass == WeaponClass.Missile ? WeaponAbility.MissileDoubleSalvo : WeaponAbility.PlasmaDoubleSalvo;
        return OwnFaction == Faction.Player && SupportsAbility(ability) && WeaponAbilities.IsActive(ability);
    }

    private readonly List<Transform> firedBarrels = new();

    private System.Collections.IEnumerator RepeatShots(List<Transform> tips, float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (var tip in tips)
        {
            if (IsDestroyed || tip == null) continue;
            Spawn(tip);
            AudioManager.PlaySfx(fireSound, fireVolume);
        }
    }

    protected virtual void Update()
    {
        TickCooldowns();

        if (!combatActive || IsDestroyed || !poweredOn) return;
        if (!ValidateSetup()) return;

        Targetable target = null;
        ShipModule focus = null;
        if (!(IsTowerGun && OwnFaction == Faction.Player && TryPickByPriority(out target, out focus))) target = FindTarget();
        if (target == null) return;

        Vector2 aimPoint = PredictAimPoint(target, focus);

        if (aiming == WeaponAiming.Turret) AimTurretAt(aimPoint);

        if (fireControl == WeaponFireControl.Automatic || OwnFaction == Faction.Enemy) TryFireAt(aimPoint);
    }

    protected bool ValidateSetup()
    {
        bool ok = (!RequiresProjectilePrefab || projectilePrefab != null) && barrels.Count > 0 && (aiming != WeaponAiming.Turret || turretPivot != null);
        if (ok || warnedMisconfigured) return ok;

        warnedMisconfigured = true;
        Debug.LogWarning($"Weapon '{moduleId}' can't fire: " +
                         (RequiresProjectilePrefab && projectilePrefab == null ? "Projectile Prefab is not assigned. " : "") +
                         (barrels.Count == 0 ? "Muzzles list is empty (assign at least one barrel tip). " : "") +
                         (aiming == WeaponAiming.Turret && turretPivot == null ? "Turret Pivot is not assigned (aiming = Turret). " : "") +
                         "Fix it on the block's prefab.", this);
        return false;
    }

    public void SetCombatActive(bool active)
    {
        combatActive = active;
        RefreshIdleAnimation();
    }

    public void SetPowered(bool powered) => poweredOn = powered;

    public bool TryFire(Targetable target = null)
    {
        if (target == null) return FireIfReady(null);
        return TryFireAt(PredictAimPoint(target));
    }

    private bool TryFireAt(Vector2 aimPoint) => FireIfReady(aimPoint);

    private void BuildBarrels()
    {
        barrels.Clear();
        foreach (var tip in muzzles)
            if (tip != null && !barrels.Contains(tip)) barrels.Add(tip);

        if (barrels.Count == 0 && muzzle != null) barrels.Add(muzzle);

        barrelCooldowns = new float[Mathf.Max(1, barrels.Count)];
    }

    protected virtual float SalvoDelay => 0f;

    protected virtual bool RequiresProjectilePrefab => true;

    protected virtual float ReloadAfterShot => EffectiveCooldownSeconds;
    private float nextShotTime;

    protected bool FireIfReady(Vector2? aimPoint)
    {
        if (IsDestroyed || !poweredOn || (RequiresProjectilePrefab && projectilePrefab == null) || barrels.Count == 0) return false;

        if (OwnFaction == Faction.Enemy && identity != null && !identity.IsFullyInView()) return false;

        if (SalvoDelay > 0f && Time.time < nextShotTime) return false;
        if (aimPoint.HasValue && !InFiringArc(aimPoint.Value)) return false;

        bool fired = false;
        firedBarrels.Clear();
        for (int i = 0; i < barrels.Count; i++)
        {
            if (barrels[i] == null || barrelCooldowns[i] > 0f) continue;

            if (energy != null && !energy.TrySpend(EnergyCostPerShot, ShipEnergySystem.EnergyPriorityGroup.Weapons)) break;

            Spawn(barrels[i]);
            AudioManager.PlaySfx(fireSound, fireVolume * (OwnFaction == Faction.Enemy ? enemyVolumeScale : 1f));
            barrelCooldowns[i] = ReloadAfterShot;
            fired = true;
            firedBarrels.Add(barrels[i]);

            if (SalvoDelay > 0f)
            {
                nextShotTime = Time.time + SalvoDelay;
                break;
            }
        }

        if (fired && TryGetDoubleSalvo(out var salvo))
            StartCoroutine(RepeatShots(new List<Transform>(firedBarrels), WeaponAbilities.Delay(salvo)));

        return fired;
    }

    private Vector2 BarrelOrigin
        => turretPivot != null ? (Vector2)turretPivot.position
         : topRoot != null ? (Vector2)topRoot.position
         : (Vector2)transform.position;

    protected Vector2 MuzzleCenter
    {
        get
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var barrel in barrels)
            {
                if (barrel == null) continue;
                sum += (Vector2)barrel.position;
                count++;
            }
            return count > 0 ? sum / count : BarrelOrigin;
        }
    }

    private Vector2 BarrelDirection
    {
        get
        {
            Vector2 offset = MuzzleCenter - BarrelOrigin;
            if (offset.sqrMagnitude > 0.0025f) return offset.normalized;
            return barrels.Count > 0 && barrels[0] != null ? (Vector2)barrels[0].up : Vector2.up;
        }
    }

    protected Vector2 MuzzleForward
        => aiming == WeaponAiming.Fixed ? (Vector2)(visualRoot != null ? visualRoot.up : transform.up) : BarrelDirection;

    protected virtual void Spawn(Transform tip)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);

        var proj = Instantiate(projectilePrefab, tip.position, rotation);
        if (proj.TryGetComponent<Projectile>(out var p))
        {
            p.damage = projectileDamage * EffectiveDamageMultiplier;
            p.speed = projectileSpeed;
            p.lifeTime = (range + 1f) / Mathf.Max(0.01f, projectileSpeed);
            p.owner = OwnFaction == Faction.Enemy ? ProjectileOwner.Enemy : ProjectileOwner.Player;
            p.weaponClass = weaponClass;
        }
    }

    private bool InFiringArc(Vector2 aimPoint)
    {
        Vector2 toAim = aimPoint - MuzzleCenter;
        float allowed = aiming == WeaponAiming.Turret ? turretAimToleranceDegrees : fireArcDegrees * 0.5f;
        return Vector2.Angle(MuzzleForward, toAim) <= allowed;
    }

    private Vector2 PredictAimPoint(Targetable target, ShipModule focus = null)
    {
        Vector2 origin = aiming == WeaponAiming.Turret && turretPivot != null
            ? (Vector2)turretPivot.position
            : barrels.Count > 0 ? MuzzleCenter : (Vector2)transform.position;

        Vector2 position = focus != null ? (Vector2)focus.transform.position : target.AimPointFrom(origin);
        if (!leadTarget || projectileSpeed <= 0.01f) return position;
        Vector2 d = position - origin;
        Vector2 v = target.Velocity;
        float s = projectileSpeed;

        float a = v.sqrMagnitude - s * s;
        float b = 2f * Vector2.Dot(d, v);
        float c = d.sqrMagnitude;

        float time;
        if (Mathf.Abs(a) < 0.0001f)
        {
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

            time = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
        }

        return time > 0f ? position + v * time : position;
    }

    private void AimTurretAt(Vector2 aimPoint)
    {
        if (turretPivot == null) return;

        if (idleAnimator != null && idleAnimator.enabled) idleAnimator.enabled = false;

        Vector2 toAim = aimPoint - (Vector2)turretPivot.position;
        if (toAim.sqrMagnitude < 0.0001f) return;

        Vector2 barrel = BarrelDirection;
        float current = Mathf.Atan2(barrel.y, barrel.x) * Mathf.Rad2Deg;
        float wanted = Mathf.Atan2(toAim.y, toAim.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(current, wanted);

        float maxStep = EffectiveTurretRotationSpeed * Mathf.Min(Time.deltaTime, 0.1f);
        float step = Mathf.Clamp(delta, -maxStep, maxStep);

        turretPivot.Rotate(0f, 0f, step, Space.World);
    }

    private Targetable FindTarget()
    {
        Targetable nearest = null;
        Targetable nearestInArc = null;
        float nearestSqr = range * range;
        float nearestInArcSqr = range * range;
        Vector2 origin = transform.position;

        foreach (var candidate in Targetable.Active)
        {
            if (candidate == null || candidate.IsDestroyed || !candidate.HasLiveBlocks || !CanEngage(candidate)) continue;

            float sqr = (candidate.AimPointFrom(origin) - origin).sqrMagnitude;
            if (sqr > range * range) continue;

            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }

            if (aiming == WeaponAiming.Fixed && sqr < nearestInArcSqr && InFiringArc(PredictAimPoint(candidate)))
            {
                nearestInArcSqr = sqr;
                nearestInArc = candidate;
            }
        }

        return nearestInArc != null ? nearestInArc : nearest;
    }

    public bool IsTowerGun
        => weaponClass == WeaponClass.Ballistic && aiming == WeaponAiming.Turret && !allowedTargets.Contains(TargetKind.Missile);

    private TowerTargetPriority towerPriority;
    private static readonly float[] bestSqr = new float[9];
    private static readonly ShipModule[] bestBlock = new ShipModule[9];
    private static readonly Targetable[] bestTarget = new Targetable[9];

    private bool TryPickByPriority(out Targetable target, out ShipModule block)
    {
        target = null;
        block = null;

        if (towerPriority == null) towerPriority = TowerTargetPriority.GetOrAdd(this);

        for (int i = 0; i < bestSqr.Length; i++) { bestSqr[i] = float.MaxValue; bestBlock[i] = null; bestTarget[i] = null; }

        Vector2 origin = transform.position;
        float rangeSqr = range * range;

        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == OwnFaction) continue;
            if (!ship.TryGetComponent<Targetable>(out var shipTarget) || !CanEngage(shipTarget)) continue;

            foreach (var candidate in ship.LiveBlocks)
            {
                if (candidate == null || candidate.IsDestroyed || !BattleZone.BlockInZone(candidate.WorldBounds)) continue;

                int kind = (int)candidate.type;
                if (kind <= 0 || kind >= bestSqr.Length) continue;

                float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
                if (sqr > rangeSqr || sqr >= bestSqr[kind]) continue;

                bestSqr[kind] = sqr;
                bestBlock[kind] = candidate;
                bestTarget[kind] = shipTarget;
            }
        }

        int meteor = (int)ModuleType.Meteor;
        foreach (var candidate in Targetable.Active)
        {
            if (candidate == null || candidate.IsDestroyed || candidate.kind != TargetKind.Meteor || !CanEngage(candidate)) continue;

            float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqr > rangeSqr || sqr >= bestSqr[meteor]) continue;

            bestSqr[meteor] = sqr;
            bestTarget[meteor] = candidate;
        }

        foreach (var category in towerPriority.Order)
        {
            int k = (int)category;
            if (k <= 0 || k >= bestSqr.Length || bestTarget[k] == null) continue;

            target = bestTarget[k];
            block = bestBlock[k];
            return true;
        }

        return false;
    }

    private bool CanEngage(Targetable candidate)
    {
        if (!allowedTargets.Contains(candidate.kind)) return false;

        if (candidate.kind == TargetKind.Ship && candidate.faction == Faction.Enemy && !candidate.HasBlockInZone) return false;

        return candidate.kind == TargetKind.Meteor || candidate.faction != OwnFaction;
    }

    protected override void OnModuleDestroyed()
    {
    }
}
