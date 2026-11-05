using UnityEngine;

public enum ProjectileOwner { Player, Enemy }

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour, IShieldBlockable
{
    public float damage = 5f;
    public float speed = 12f;
    public ProjectileOwner owner = ProjectileOwner.Player;
    [Tooltip("Set by the firing weapon. Laser and Plasma shots can be dodged with the crew evasion-vs-energy " +
             "perk, Ballistic and Missile shots with the evasion-vs-ballistic perk.")]
    public WeaponClass weaponClass = WeaponClass.Ballistic;
    [Tooltip("Seconds before it disappears. A weapon that fires it overrides this so the shot stops at " +
             "the weapon's Range (see WeaponModule.Spawn).")]
    public float lifeTime = 4f;

    [Header("Sound")]
    [Tooltip("Played when the shot hits something (not when a shield stops it — the shield has its own sound — and not when it just runs out of range).")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float impactVolume = 1f;

    [Header("Impact")]
    [Tooltip("Quick particle effect spawned where the shot hits something or is stopped by a shield — " +
             "not when it simply runs out of range. A particle prefab, or a child object of this " +
             "prefab used as a template (it's switched off until the hit). Leave empty for none.")]
    public GameObject impactEffectPrefab;
    [Tooltip("Seconds until the spawned impact effect is removed. Make it at least as long as the effect. " +
             "0 = never removed by this script.")]
    public float impactEffectLifetime = 1f;

    [Header("Trail")]
    [Tooltip("Leaves a short fading trail behind the shot while it flies. If the prefab already has its own " +
             "TrailRenderer (a child, or the one assigned in ProjectileGlow) that one is used and its own settings " +
             "are kept — only the fade-out after the shot ends is added. Otherwise one is created from the " +
             "values below.")]
    public bool trailEnabled = true;
    [Tooltip("How long a point of the trail lasts, in seconds — the trail's length.")]
    [Min(0f)] public float trailTime = 0.25f;
    [Tooltip("Trail width right behind the shot, in world units, and at its far tip.")]
    public float trailStartWidth = 0.12f;
    public float trailEndWidth = 0f;
    [Tooltip("Trail color; it fades to transparent along its length. Taken from the prefab's ProjectileGlow color if it has one.")]
    public Color trailColor = new(1f, 0.8f, 0.4f, 0.9f);
    [Tooltip("Optional material (e.g. additive). Empty = the plain sprite material.")]
    public Material trailMaterial;
    public int trailSortingOrder = 0;

    private TrailRenderer trail;
    private Rigidbody2D rb;
    private bool consumed;
    private bool broken;
    private bool evasionRolled;
    private bool evaded;

    public float ImpactDamage => damage;
    public bool IsConsumed => consumed;
    public bool IsHostileTo(Faction shieldFaction)
        => (owner == ProjectileOwner.Player ? Faction.Player : Faction.Enemy) != shieldFaction;
    public void AbsorbedByShield()
    {
        consumed = true;
        Break(playSound: false);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        SetUpTrail();

        if (impactEffectPrefab != null && impactEffectPrefab.transform.IsChildOf(transform))
            impactEffectPrefab.SetActive(false);
    }

    private void Break(bool playSound = true)
    {
        if (broken) return;
        broken = true;
        consumed = true;

        if (playSound) AudioManager.PlaySfx(impactSound, impactVolume);

        if (impactEffectPrefab != null)
        {
            var fx = EffectSorting.Apply(Instantiate(impactEffectPrefab, transform.position, Quaternion.identity));
            fx.SetActive(true);
            if (impactEffectLifetime > 0f) Destroy(fx, impactEffectLifetime);
        }

        Despawn();
    }

    private void SetUpTrail()
    {
        if (!trailEnabled || trailTime <= 0f) return;

        trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null) return;

        var glow = GetComponent<ProjectileGlow>();
        Color color = glow != null ? glow.color : trailColor;

        var go = new GameObject("Trail", typeof(TrailRenderer));
        go.transform.SetParent(transform, false);

        trail = go.GetComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.sortingOrder = trailSortingOrder;
        trail.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));

        var end = color;
        end.a = 0f;
        trail.startColor = color;
        trail.endColor = end;
    }

    private void Despawn()
    {
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            trail.autodestruct = true;
            trail = null;
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        rb.linearVelocity = transform.up * speed;

        Invoke(nameof(Despawn), lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (TryHitModule(other) || TryHitTargetable(other))
            Break();
    }

    private bool TryHitModule(Collider2D other)
    {
        var hitModule = other.GetComponent<ShipModule>();
        if (hitModule == null) return false;

        bool isEnemyModule = other.CompareTag("EnemyShip");
        bool isPlayerModule = other.CompareTag("PlayerShip");

        if (owner == ProjectileOwner.Player && !isEnemyModule) return false;
        if (owner == ProjectileOwner.Enemy && !isPlayerModule) return false;

        if (isPlayerModule && owner == ProjectileOwner.Enemy && TryEvade()) return false;

        float dealt = CriticalHit.Roll(owner == ProjectileOwner.Player ? Faction.Player : Faction.Enemy, damage, out bool crit);
        hitModule.TakeDamage(dealt, crit);
        return true;
    }

    private bool TryEvade()
    {
        if (!evasionRolled)
        {
            evasionRolled = true;
            var data = GameDataManager.Instance;
            if (data != null)
            {
                bool energyWeapon = weaponClass is WeaponClass.Laser or WeaponClass.Plasma;
                evaded = data.RollCrewChance(energyWeapon ? CrewPerkType.EvadeEnergyWeapons : CrewPerkType.EvadeBallisticWeapons);
            }
        }
        return evaded;
    }

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
