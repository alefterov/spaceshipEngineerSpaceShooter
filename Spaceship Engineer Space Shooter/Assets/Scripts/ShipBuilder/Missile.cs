using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A homing missile, fired by a MissileLauncher. It flies STRAIGHT along its launch direction for Straight
/// Seconds, then starts turning toward its target block — at most Turn Rate degrees per second, so it
/// curves rather than snapping around. The target is the nearest block of the first category in its owner's
/// MissileTargetPriority that has a living block (looked up again if the block is destroyed on the way).
///
/// On contact with a block of the OTHER side it explodes: the block it hit takes the full damage and every
/// block touching that block takes Neighbour Damage Fraction (10%) of it. Enemy missiles are the exact mirror —
/// they go for the player's blocks by the enemy ship's own priority.
///
/// A missile is also a target: it carries a Targetable (kind Missile, so only weapons set up to shoot
/// missiles — point defence — engage it) with Hit Points, and exploding without damage when shot down. A
/// shield stops it (it explodes at the shield's edge, the shield pays the missile's damage), and a dodged
/// shot (the Navigator's evasion perk) passes through the ship harmlessly.
///
/// PREFAB: Rigidbody2D (any body type; gravity is switched off here), a trigger Collider2D, the sprite with
/// its nose along +Y, and this component. Optional: a TrailRenderer of your own (otherwise the Trail settings below
/// make one) and an Explosion Effect Prefab. Speed, damage and launch direction come from the launcher.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Missile : MonoBehaviour, IShieldBlockable
{
    [Header("Flight")]
    [Tooltip("World units per second — normally set by the launcher (its Projectile Speed). Keep it below an ordinary shot's speed.")]
    public float speed = 5f;
    [Tooltip("Seconds it flies straight ahead after launch before it starts turning toward its target.")]
    public float straightSeconds = 0.6f;
    [Tooltip("How fast it can turn toward its target, in degrees per second. Lower = wider, lazier curves.")]
    public float turnRateDegreesPerSecond = 120f;
    [Tooltip("Seconds until it self-destructs without a hit (fuel runs out).")]
    public float lifeTime = 10f;

    [Header("Sound")]
    [Tooltip("Played where the missile blows up (a hit, a shield, or being shot down).")]
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    [Header("Explosion")]
    [Tooltip("The block it hits takes the full damage; every block touching that block takes this share of it.")]
    [Range(0f, 1f)] public float neighbourDamageFraction = 0.1f;
    [Tooltip("Explosion effect spawned where it blows up (hit, shield or shot down). A particle/animation prefab.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Seconds until the spawned explosion is removed. Make it at least as long as the effect. 0 = never removed here.")]
    public float explosionEffectLifetime = 2f;

    [Header("Trail")]
    [Tooltip("Leaves a short fading trail behind the missile while it flies; when the missile ends, the trail fades away where " +
             "it was. If the prefab already has its own TrailRenderer (a child, or the one assigned in ProjectileGlow) that " +
             "one is used and its own settings are kept — only the fade-out after the end is added. Otherwise one is " +
             "created from the values below.")]
    public bool trailEnabled = true;
    [Tooltip("How long a point of the trail lasts, in seconds — the trail's length.")]
    [Min(0f)] public float trailTime = 0.6f;
    [Tooltip("Trail width right behind the missile, in world units, and at its far tip.")]
    public float trailStartWidth = 0.15f;
    public float trailEndWidth = 0f;
    [Tooltip("Trail color; it fades to transparent along its length. Taken from the prefab's ProjectileGlow color if it has one.")]
    public Color trailColor = new(1f, 0.85f, 0.6f, 0.8f);
    [Tooltip("Optional material (e.g. additive). Empty = the plain sprite material.")]
    public Material trailMaterial;
    public int trailSortingOrder = 0;

    [Header("As a target")]
    [Tooltip("Damage it takes before it is shot down. Point-defence shots deal their own damage per hit.")]
    public float hitPoints = 5f;

    // Set by the launcher
    private Faction owner = Faction.Player;
    private float damage = 10f;
    private WeaponClass weaponClass = WeaponClass.Missile;
    private static readonly TargetKind[] DefaultAllowed = { TargetKind.Ship, TargetKind.Meteor, TargetKind.Torpedo };
    private IReadOnlyList<ModuleType> priority = MissileTargetPriority.DefaultCategories;

    private Rigidbody2D rb;
    private Targetable targetable;
    private FadingTrail trail;
    private ShipModule targetBlock;          // a ship block chosen by the priority list ...
    private Targetable targetObject;         // ... or, when there is no ship block to hit, a meteor / torpedo
    private IReadOnlyList<TargetKind> allowed = DefaultAllowed;
    private float age;
    private float retargetTimer;
    private bool finished;
    private bool evasionRolled;
    private bool harmless; // dodged — flies on through, doing nothing

    /// <summary>Called right after the launcher instantiates it.</summary>
    public void Launch(Faction ownerFaction, float damageAmount, float flightSpeed, IReadOnlyList<ModuleType> targetPriority, WeaponClass launcherClass, IReadOnlyList<TargetKind> allowedTargets)
    {
        owner = ownerFaction;
        damage = damageAmount;
        speed = flightSpeed;
        weaponClass = launcherClass;
        if (targetPriority != null) priority = targetPriority;
        if (allowedTargets != null) allowed = allowedTargets;

        targetable.Configure(TargetKind.Missile, owner, hitPoints);
        Acquire();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        targetable = GetComponent<Targetable>();
        if (targetable == null) targetable = gameObject.AddComponent<Targetable>();
        targetable.OnDestroyed += _ => ShotDown();

        trail = GetComponent<FadingTrail>();
        if (trail == null) trail = gameObject.AddComponent<FadingTrail>();
        if (trailEnabled && trailTime > 0f) trail.Setup(trailTime, trailStartWidth, trailEndWidth, trailColor, trailMaterial, trailSortingOrder);

        // The explosion may be a child object of this prefab rather than a separate asset; it is only a template.
        if (explosionEffectPrefab != null && explosionEffectPrefab.transform.IsChildOf(transform))
            explosionEffectPrefab.SetActive(false);
    }

    private void Start() => Invoke(nameof(Despawn), lifeTime);

    private void Update()
    {
        if (finished) return;

        age += Time.deltaTime;

        if (age >= straightSeconds) Steer();
        rb.linearVelocity = transform.up * speed;
    }

    // ---------- Flight ----------

    private void Steer()
    {
        // Re-pick only when the chosen target is gone (destroyed, or hit by something else).
        if (!HasLiveTarget)
        {
            retargetTimer -= Time.deltaTime;
            if (retargetTimer <= 0f)
            {
                retargetTimer = 0.25f;
                Acquire();
            }
        }

        if (!HasLiveTarget) return; // nothing left to chase — flies on straight

        Vector2 toTarget = TargetPosition - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        float wanted = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f; // the nose is +Y
        float current = transform.eulerAngles.z;
        float turned = Mathf.MoveTowardsAngle(current, wanted, turnRateDegreesPerSecond * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, turned);
    }

    private bool HasLiveTarget
        => (targetBlock != null && !targetBlock.IsDestroyed) || (targetObject != null && !targetObject.IsDestroyed);

    private Vector2 TargetPosition
        => targetBlock != null && !targetBlock.IsDestroyed ? (Vector2)targetBlock.transform.position : (Vector2)targetObject.transform.position;

    /// <summary>Chooses what to chase: a ship block by the priority list (if ships are allowed targets and the
    /// other side has any block left); otherwise the nearest hostile meteor / torpedo among the allowed kinds.</summary>
    private void Acquire()
    {
        MissileTargeting.PickTarget(owner, priority, allowed, transform.position, out targetBlock, out targetObject);
    }

    // ---------- Hits ----------

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (finished || harmless) return;

        // A meteor (or torpedo...) in the way: the missile blows up on it, dealing its damage to that object.
        var hitObject = other.GetComponent<Targetable>();
        // A meteor can never be ignored, whatever the priority or allowed targets say.
        if (hitObject != null && (MissileTargeting.IsValidObjectTarget(hitObject, owner, allowed)
                                  || (hitObject.kind == TargetKind.Meteor && !hitObject.IsDestroyed)))
        {
            ExplodeOnObject(hitObject);
            return;
        }

        var block = other.GetComponent<ShipModule>();
        if (block == null || block.IsDestroyed) return;

        bool hostile = owner == Faction.Enemy ? other.CompareTag("PlayerShip") : other.CompareTag("EnemyShip");
        if (!hostile) return;

        // Only a block of the top-priority kind is worth exploding on; anything else in the way is flown past.
        if (!ShouldDetonateOn(block)) return;

        // The Navigator's evasion perk: an enemy missile may be dodged — rolled once, then it just passes through.
        if (owner == Faction.Enemy && TryEvade())
        {
            harmless = true;
            CancelInvoke(nameof(Despawn));
            Invoke(nameof(Despawn), 0.5f);
            return;
        }

        Explode(block);
    }

    /// <summary>Whether touching this enemy block should set the missile off. It does only if the block is of the
    /// same kind as the block it is chasing — the highest-priority kind that still exists. Any other block it
    /// brushes against is ignored and the missile flies on toward the real target. While chasing a meteor every
    /// ship block is ignored. (Meteors themselves can never be ignored — see OnTriggerEnter2D.)</summary>
    private bool ShouldDetonateOn(ShipModule block)
    {
        if (!HasLiveTarget) Acquire(); // the target just went; look again before deciding

        if (targetBlock != null && !targetBlock.IsDestroyed) return block.type == targetBlock.type;
        if (targetObject != null && !targetObject.IsDestroyed) return false;
        return true; // nothing was chosen at all — hit whatever enemy block this is
    }

    private bool TryEvade()
    {
        if (!evasionRolled)
        {
            evasionRolled = true;
            var data = GameDataManager.Instance;
            return data != null && data.RollCrewChance(CrewPerkType.EvadeBallisticWeapons);
        }
        return false;
    }

    private void ExplodeOnObject(Targetable hit)
    {
        if (finished) return;
        finished = true;

        hit.TakeDamage(damage);
        SpawnExplosion();
        Despawn();
    }

    private void Explode(ShipModule hit)
    {
        if (finished) return;
        finished = true;

        // Who is next to the block is worked out BEFORE the damage — a destroyed block frees its cells.
        var neighbours = MissileTargeting.GetNeighbours(hit);

        hit.TakeDamage(damage);
        foreach (var neighbour in neighbours)
            if (neighbour != null) neighbour.TakeDamage(damage * neighbourDamageFraction);

        SpawnExplosion();
        Despawn();
    }

    // ---------- IShieldBlockable — a shield stops it and takes its damage ----------

    public float ImpactDamage => damage;
    public bool IsConsumed => finished;
    public bool IsHostileTo(Faction shieldFaction) => owner != shieldFaction;

    public void AbsorbedByShield()
    {
        if (finished) return;
        finished = true;
        SpawnExplosion();
        Despawn();
    }

    private void ShotDown()
    {
        if (finished) return;
        finished = true;
        SpawnExplosion(); // blows up with no damage
        Despawn();
    }

    // ---------- Ending ----------

    private void SpawnExplosion()
    {
        AudioManager.PlaySfx(explosionSound, explosionVolume);

        if (explosionEffectPrefab == null) return;

        var fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity); // unparented: it outlives the missile
        fx.SetActive(true);
        if (explosionEffectLifetime > 0f) Destroy(fx, explosionEffectLifetime);
    }

    private void Despawn()
    {
        finished = true;
        if (trail != null) trail.Detach(); // the trail fades away where the missile ended
        Destroy(gameObject);
    }
}
