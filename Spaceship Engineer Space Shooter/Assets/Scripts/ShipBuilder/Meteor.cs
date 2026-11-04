using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Targetable))]
public class Meteor : MonoBehaviour, IShieldBlockable
{
    [Tooltip("Damage dealt to whatever block it hits. Separate from Targetable.maxHP, which is how " +
             "tough the meteor itself is against being shot down.")]
    public float damage = 20f;
    public float speed = 3f;
    [Tooltip("Credits earned when this meteor is shot down by a weapon or stopped by a shield — NOT when " +
             "it hits the ship instead, and not when it just flies past and misses (see OnDestroyed below).")]
    public int creditReward = 10;
    [Tooltip("Experience earned under the same conditions as Credit Reward.")]
    public int experienceReward = 5;
    [Tooltip("World-space distance beyond the camera's edge before this meteor despawns for having missed entirely.")]
    public float offscreenMargin = 3f;

    [Header("Spin")]
    [Tooltip("Each meteor picks a random spin speed between Min and Max (degrees per second) and keeps " +
             "rotating at that speed, always the same way. Set both to 0 for no spin.")]
    public float minSpinSpeed = 20f;
    public float maxSpinSpeed = 90f;
    [Tooltip("Off: every meteor spins counter-clockwise. On: each meteor randomly picks clockwise or " +
             "counter-clockwise (still a single direction for its whole flight).")]
    public bool randomSpinDirection;

    [Header("Destruction — explosion, then it breaks apart")]
    [Tooltip("Dust explosion effect spawned where the meteor is destroyed (shot down, stopped by a shield, or " +
             "crashing into the ship) — not when it just flies off-screen. A particle/animation prefab; " +
             "leave empty for none.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Seconds until the spawned explosion object is removed. Make it at least as long as the effect. " +
             "0 = never removed by this script (e.g. the prefab destroys itself).")]
    public float explosionLifetime = 2f;
    [Tooltip("Your own chunk sprites, one flying fragment per entry. Leave EMPTY to have the meteor's own " +
             "sprite cut into a grid of pieces automatically (see Slice Columns/Rows).")]
    public Sprite[] fragmentSprites;
    [Min(1)] public int sliceColumns = 3;
    [Min(1)] public int sliceRows = 3;
    [Tooltip("Speed each fragment flies outward at, random between Min and Max (world units per second).")]
    public float fragmentSpeedMin = 1f;
    public float fragmentSpeedMax = 3f;
    [Tooltip("How much of the meteor's own velocity fragments keep (0 = they fly apart from a standstill).")]
    [Range(0f, 1f)] public float fragmentInheritVelocity = 0.4f;
    [Tooltip("Max spin of a fragment, degrees per second (random up to this, either direction).")]
    public float fragmentSpinMax = 240f;
    [Tooltip("Seconds fragments hold still first, so the explosion cloud covers the break-up.")]
    public float fragmentDelay = 0.12f;
    [Tooltip("Seconds fragments spend drifting and fading out after that delay.")]
    public float fragmentFadeSeconds = 1.2f;

    [Tooltip("The player's ship — only used to know hits count (it's no longer aimed at). Assigned at spawn time " +
             "(see EnemySpawner); falls back to searching for it if left unset.")]
    public ShipGrid targetShip;

    [Header("Sound")]
    [Tooltip("Played when the meteor strikes a block of the ship.")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float impactVolume = 1f;
    [Tooltip("Played when the meteor is destroyed (shot down, or stopped by a shield).")]
    public AudioClip destroySound;
    [Range(0f, 1f)] public float destroyVolume = 1f;

    private Rigidbody2D rb;
    private bool consumed;

    public float ImpactDamage => damage;
    public bool IsConsumed => consumed;
    public bool IsHostileTo(Faction shieldFaction) => shieldFaction == Faction.Player;
    public void AbsorbedByShield()
    {
        consumed = true;
        Break();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (explosionEffectPrefab != null && explosionEffectPrefab.transform.IsChildOf(transform))
            explosionEffectPrefab.SetActive(false);

        var targetable = GetComponent<Targetable>();
        targetable.kind = TargetKind.Meteor;
        targetable.OnDestroyed += _ =>
        {
            BattleRewards.AddKill(creditReward, experienceReward);
            Break();
        };
    }

    private bool broken;

    private void Break()
    {
        if (broken) return;
        broken = true;
        AudioManager.PlaySfx(destroySound, destroyVolume);

        if (explosionEffectPrefab != null)
        {
            var fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            fx.SetActive(true);
            if (explosionLifetime > 0f) Destroy(fx, explosionLifetime);
        }

        SpawnFragments();
        Destroy(gameObject);
    }

    private void SpawnFragments()
    {
        var bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;

        SpriteBreakup.Spawn(bodyRenderer, fragmentSprites, sliceColumns, sliceRows, velocity, new SpriteBreakup.Motion
        {
            speedMin = fragmentSpeedMin,
            speedMax = fragmentSpeedMax,
            inheritVelocity = fragmentInheritVelocity,
            spinMax = fragmentSpinMax,
            delay = fragmentDelay,
            fadeSeconds = fragmentFadeSeconds,
        });
    }

    private void Update()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfHeight = cam.orthographicSize + offscreenMargin;
        float halfWidth = halfHeight * cam.aspect;
        Vector2 relative = (Vector2)transform.position - (Vector2)cam.transform.position;

        if (Mathf.Abs(relative.x) > halfWidth || Mathf.Abs(relative.y) > halfHeight)
            Destroy(gameObject);
    }

    private Vector2 travelDirection = Vector2.down;

    public void Launch(Vector2 direction)
    {
        travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        if (rb != null) rb.linearVelocity = travelDirection * speed;
    }

    private void Start()
    {
        if (targetShip == null)
        {
            var playerObj = GameObject.FindWithTag("PlayerShip");
            if (playerObj != null) targetShip = playerObj.GetComponentInParent<ShipGrid>();
        }

        rb.linearVelocity = travelDirection * speed;

        rb.angularDamping = 0f;
        float spin = UnityEngine.Random.Range(Mathf.Min(minSpinSpeed, maxSpinSpeed), Mathf.Max(minSpinSpeed, maxSpinSpeed));
        if (randomSpinDirection && UnityEngine.Random.value < 0.5f) spin = -spin;
        rb.angularVelocity = spin;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || targetShip == null || !other.CompareTag("PlayerShip")) return;

        var hitModule = other.GetComponent<ShipModule>();
        if (hitModule == null) return;

        hitModule.TakeDamage(damage);
        AudioManager.PlaySfx(impactSound, impactVolume);
        consumed = true;
        Break();
    }
}
