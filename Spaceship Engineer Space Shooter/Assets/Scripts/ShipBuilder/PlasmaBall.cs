using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlasmaBall : MonoBehaviour, IShieldBlockable
{
    [Header("Field")]
    [Tooltip("Radius of the ball's field, in world units: enemy blocks inside it are struck by lightning.")]
    [Min(0.1f)] public float strikeRadius = 2.5f;
    [Tooltip("Seconds between strikes on the same block while it stays inside the field. The first strike comes the " +
             "moment it enters.")]
    [Min(0.05f)] public float strikeIntervalSeconds = 1f;
    [Tooltip("Hitting a shield deals the ball's damage times this to the shield's reserve, and the ball explodes.")]
    public float shieldDamageMultiplier = 10f;

    [Header("Explosion")]
    [Tooltip("Spawned where the ball blows up on a shield. Optional.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Seconds until the spawned explosion is removed. Make it at least as long as the effect. 0 = never removed here.")]
    public float explosionEffectLifetime = 2f;

    [Header("Sound")]
    [Tooltip("Loops for as long as the ball is flying.")]
    public AudioClip flightSound;
    [Range(0f, 1f)] public float flightVolume = 0.6f;
    [Tooltip("Played each time the ball strikes a block with lightning.")]
    public AudioClip lightningSound;
    [Range(0f, 1f)] public float lightningVolume = 1f;
    [Tooltip("Played when the ball blows up on a shield.")]
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    [Header("Lightning")]
    [Tooltip("Colour of the lightning.")]
    public Color lightningColor = new(0.55f, 0.85f, 1f, 1f);
    [Tooltip("Thickness of the bright core of a bolt, in world units.")]
    public float lightningWidth = 0.08f;
    [Tooltip("The glow around the core is this many times wider.")]
    public float lightningHaloWidthMultiplier = 3f;
    [Range(0f, 1f)] public float lightningHaloAlpha = 0.35f;
    [Tooltip("Roughly how long each straight piece of a bolt is. Smaller = more detailed and more jagged.")]
    public float lightningSegmentLength = 0.35f;
    [Tooltip("The furthest a point of the bolt is pushed sideways, in world units.")]
    public float lightningJaggedness = 0.25f;
    [Tooltip("How long a bolt stays on screen, fading out.")]
    public float lightningLifetime = 0.3f;
    [Tooltip("The bolt's shape is re-rolled this often, so it crackles.")]
    public float lightningFlickerSeconds = 0.04f;
    [Tooltip("Optional material (e.g. additive) for a stronger glow. Empty = the plain sprite material.")]
    public Material lightningMaterial;
    public int lightningSortingOrder = 25;

    private Faction owner = Faction.Player;
    private float damage = 10f;
    private float speed = 5f;

    private Rigidbody2D rb;
    private bool finished;

    private readonly Dictionary<ShipModule, float> nextStrike = new();
    private readonly List<ShipModule> seen = new();
    private readonly List<ShipModule> forget = new();
    private readonly List<ShipModule> toStrike = new();

    public void Launch(Faction ownerFaction, float strikeDamage, float flightSpeed, float lifetimeSeconds)
    {
        owner = ownerFaction;
        damage = strikeDamage;
        speed = flightSpeed;

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifetimeSeconds);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        SfxLoop.Attach(gameObject, flightSound, flightVolume);

        if (GetComponent<Collider2D>() == null)
        {
            var circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.15f;
        }

        if (explosionEffectPrefab != null && explosionEffectPrefab.transform.IsChildOf(transform))
            explosionEffectPrefab.SetActive(false);
    }

    private void Update()
    {
        if (finished) return;

        rb.linearVelocity = transform.up * speed;
        Scan();
    }

    private void Scan()
    {
        Vector2 center = transform.position;
        float radiusSqr = strikeRadius * strikeRadius;

        seen.Clear();
        toStrike.Clear();

        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == owner) continue;

            foreach (var block in ship.LiveBlocks)
            {
                if (block == null || block.IsDestroyed) continue;
                if (((Vector2)BlockCenter(block) - center).sqrMagnitude > radiusSqr) continue;

                seen.Add(block);

                if (nextStrike.TryGetValue(block, out float due) && Time.time < due) continue;

                nextStrike[block] = Time.time + strikeIntervalSeconds;
                toStrike.Add(block);
            }
        }

        foreach (var block in toStrike)
            if (block != null && !block.IsDestroyed) Strike(block);

        forget.Clear();
        foreach (var pair in nextStrike)
            if (pair.Key == null || !seen.Contains(pair.Key)) forget.Add(pair.Key);
        foreach (var block in forget) nextStrike.Remove(block);
    }

    private static Vector3 BlockCenter(ShipModule block)
        => block.visualRoot != null ? block.visualRoot.position : block.transform.position;

    private void Strike(ShipModule block)
    {
        AudioManager.PlaySfx(lightningSound, lightningVolume);

        PlasmaLightning.Spawn(new PlasmaLightning.Style
        {
            color = lightningColor,
            width = lightningWidth,
            haloWidthMultiplier = lightningHaloWidthMultiplier,
            haloAlpha = lightningHaloAlpha,
            segmentLength = lightningSegmentLength,
            jaggedness = lightningJaggedness,
            lifetime = lightningLifetime,
            flickerSeconds = lightningFlickerSeconds,
            material = lightningMaterial,
            sortingOrder = lightningSortingOrder,
        }, transform, block.visualRoot != null ? block.visualRoot : block.transform);

        if (owner == Faction.Enemy)
        {
            var data = GameDataManager.Instance;
            if (data != null && data.RollCrewChance(CrewPerkType.EvadeEnergyWeapons)) return;
        }

        float dealt = CriticalHit.Roll(owner, damage, out bool crit);
        block.TakeDamage(dealt, crit);
    }

    public float ImpactDamage => damage * shieldDamageMultiplier;
    public bool IsConsumed => finished;
    public bool IsHostileTo(Faction shieldFaction) => owner != shieldFaction;

    public void AbsorbedByShield()
    {
        if (finished) return;

        AudioManager.PlaySfx(explosionSound, explosionVolume);

        if (explosionEffectPrefab != null)
        {
            var fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            fx.SetActive(true);
            if (explosionEffectLifetime > 0f) Destroy(fx, explosionEffectLifetime);
        }

        Despawn();
    }

    private void Despawn()
    {
        finished = true;
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, strikeRadius);
    }
}
