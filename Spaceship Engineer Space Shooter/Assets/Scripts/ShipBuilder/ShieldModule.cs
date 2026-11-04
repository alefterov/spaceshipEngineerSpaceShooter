using UnityEngine;

public class ShieldModule : ShipModule
{
    [Header("Shield field")]
    [Tooltip("Radius of the protected area, in world units (1 = one grid cell). One value drives everything: " +
             "the trigger collider, the circle sprite's size and the impact crescent's size. Updates " +
             "live in the editor (select the prefab to see the radius drawn as a gizmo) and at runtime.")]
    [Min(0.1f)] public float radius = 2f;
    [Tooltip("The shield's own reserve, in energy units: how much damage it can absorb before collapsing. " +
             "Each absorbed hit drains it by exactly the damage it would have dealt.")]
    [Min(1f)] public float capacity = 50f;
    [Tooltip("Reserve refilled per second. Every point refilled is paid for 1:1 from the ship's energy pool " +
             "— if the ship has less energy than that, the shield only refills as much as the ship can give.")]
    public float rechargePerSecond = 5f;
    [Range(0f, 1f)]
    [Tooltip("After a collapse the shield stays down until its reserve has refilled to this fraction of Capacity.")]
    public float reactivateFraction = 0.25f;
    [Tooltip("After the shield is broken through (collapses), seconds before its reserve starts refilling at all.")]
    [Min(0f)] public float collapseCooldown = 5f;

    [Header("Sound")]
    [Tooltip("Played when the shield stops a hit.")]
    public AudioClip absorbSound;
    [Range(0f, 1f)] public float absorbVolume = 1f;

    [Header("Visuals (all optional)")]
    [Tooltip("The faint circle marking the protected area. Put a SpriteRenderer anywhere under this " +
             "prefab's Visual Root, centered on it. Its scale is set from Radius automatically — draw " +
             "the sprite as a circle filling its whole canvas.")]
    public SpriteRenderer zoneRenderer;
    [Range(0f, 1f)]
    [Tooltip("Alpha of the circle while the shield is up. 0 = invisible until it takes a hit.")]
    public float zoneAlpha = 0.08f;
    [Tooltip("How fast the circle fades in/out when the shield goes down or comes back, alpha per second.")]
    public float zoneFadeSpeed = 2f;
    [Tooltip("Crescent shown on each absorbed hit — a prefab with a SpriteRenderer. Draw it on the SAME " +
             "canvas size as the circle sprite, with the crescent along the TOP edge; it's scaled to " +
             "Radius and rotated toward the impact automatically.")]
    public GameObject impactArcPrefab;
    [Tooltip("Seconds the crescent takes to fade out completely.")]
    public float arcFadeSeconds = 0.9f;

    private ShipEnergySystem energy;
    private ShipIdentity identity;
    private Transform zoneRoot;
    private Collider2D zoneCollider;
    private bool shieldUp = true;
    private float rechargeResumeTime;
    private int topBase = 40;
    private int zoneAuthoredOrder;

    private Faction OwnFaction => identity != null ? identity.faction : Faction.Player;

    public bool IsUp => shieldUp && !IsDestroyed;

    public override float EnergyPerSecondEstimate => 0f;

    public float Reserve { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Shield;
        energyDelta = -Mathf.Abs(energyDelta);

        energy = GetComponentInParent<ShipEnergySystem>();
        identity = GetComponentInParent<ShipIdentity>();

        if (OwnFaction == Faction.Player && GameDataManager.Instance != null)
            capacity *= GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.ShieldEfficiency);

        Reserve = capacity;

        BuildZone();

        if (zoneRenderer != null)
        {
            zoneAuthoredOrder = zoneRenderer.sortingOrder;
            SetZoneAlpha(zoneAlpha);
        }

        ApplyRadius();
    }

    public void ApplyRadius()
    {
        if (zoneCollider is CircleCollider2D circle && zoneRoot != null && zoneRoot.parent != null)
            circle.radius = radius / Mathf.Max(0.0001f, zoneRoot.parent.lossyScale.x);

        if (zoneRenderer != null) FitToRadius(zoneRenderer.transform, zoneRenderer.sprite);
    }

    private void OnValidate() => ApplyRadius();

    private void OnDrawGizmosSelected()
    {
        var center = (visualRoot != null ? visualRoot : transform).position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(center, radius);
    }

    private void BuildZone()
    {
        var parent = visualRoot != null ? visualRoot : transform;

        var zoneObj = new GameObject("ShieldZone");
        zoneRoot = zoneObj.transform;
        zoneRoot.SetParent(parent, false);

        var circle = zoneObj.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        zoneCollider = circle;

        zoneObj.AddComponent<ShieldZoneRelay>().owner = this;
    }

    private void Update()
    {
        if (IsDestroyed) return;

        Recharge();

        if (!shieldUp && Reserve >= capacity * reactivateFraction) shieldUp = true;

        if (zoneRenderer != null)
        {
            float target = shieldUp ? zoneAlpha : 0f;
            float current = zoneRenderer.color.a;
            if (!Mathf.Approximately(current, target))
                SetZoneAlpha(Mathf.MoveTowards(current, target, zoneFadeSpeed * Time.deltaTime));
        }
    }

    private void Recharge()
    {
        float missing = capacity - Reserve;
        if (missing <= 0f || rechargePerSecond <= 0f || Time.time < rechargeResumeTime) return;

        float wanted = Mathf.Min(missing, rechargePerSecond * Time.deltaTime);
        if (energy != null)
        {
            wanted = Mathf.Min(wanted, energy.Current);
            if (wanted <= 0f || !energy.TrySpend(wanted, ShipEnergySystem.EnergyPriorityGroup.Defense)) return;
        }

        Reserve += wanted;
    }

    public void HandleIncoming(Collider2D other)
    {
        if (!IsUp) return;

        var hazard = other.GetComponentInParent<IShieldBlockable>();
        if (hazard == null || hazard.IsConsumed || !hazard.IsHostileTo(OwnFaction)) return;

        float damage = hazard.ImpactDamage;
        if (Reserve < damage)
        {
            Collapse();
            return;
        }

        float hitCost = Mathf.Abs(energyDelta);
        if (energy != null && hitCost > 0f && !energy.TrySpend(hitCost, ShipEnergySystem.EnergyPriorityGroup.Defense)) return;

        Reserve -= damage;
        if (Reserve <= 0.01f) Collapse();

        Vector2 center = zoneRoot.position;
        Vector2 impactDirection = (Vector2)other.transform.position - center;

        hazard.AbsorbedByShield();
        AudioManager.PlaySfx(absorbSound, absorbVolume);
        SpawnImpactArc(impactDirection);
    }

    private void Collapse()
    {
        shieldUp = false;
        rechargeResumeTime = Time.time + collapseCooldown;
    }

    private void SpawnImpactArc(Vector2 towardImpact)
    {
        if (impactArcPrefab == null) return;

        var arc = Instantiate(impactArcPrefab, zoneRoot);
        arc.transform.localPosition = Vector3.zero;

        float angle = Mathf.Atan2(towardImpact.y, towardImpact.x) * Mathf.Rad2Deg - 90f;
        arc.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var arcRenderer = arc.GetComponentInChildren<SpriteRenderer>();
        if (arcRenderer != null)
        {
            FitToRadius(arc.transform, arcRenderer.sprite);
            arcRenderer.sortingOrder = topBase + 1;
        }

        arc.AddComponent<ShieldImpactFade>().Begin(arcFadeSeconds);
    }

    private void FitToRadius(Transform target, Sprite sprite)
    {
        if (sprite == null || target.parent == null) return;

        float spriteDiameter = sprite.bounds.size.x;
        float parentScale = Mathf.Max(0.0001f, target.parent.lossyScale.x);
        target.localScale = Vector3.one * (2f * radius / Mathf.Max(0.0001f, spriteDiameter) / parentScale);
    }

    private void SetZoneAlpha(float alpha)
    {
        var c = zoneRenderer.color;
        c.a = alpha;
        zoneRenderer.color = c;
    }

    public override void ApplySortingOrders(int roofBase, int topBase)
    {
        base.ApplySortingOrders(roofBase, topBase);
        this.topBase = topBase;
        if (zoneRenderer != null) zoneRenderer.sortingOrder = topBase + zoneAuthoredOrder;
    }

    public override void PrepareAsGhost()
    {
        if (zoneRenderer != null) zoneRenderer.gameObject.SetActive(false);
    }

    protected override void OnModuleDestroyed()
    {
        if (zoneCollider != null) zoneCollider.enabled = false;
    }
}
