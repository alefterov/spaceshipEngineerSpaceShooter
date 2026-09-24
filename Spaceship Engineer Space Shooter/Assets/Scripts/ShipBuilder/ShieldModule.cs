using UnityEngine;

/// <summary>
/// Shield generator. Projects a protected circular area around itself: any hostile hazard
/// (IShieldBlockable — meteors, enemy projectiles) that enters it is stopped before it can touch a
/// block. Each absorbed hit flashes a crescent on the side it came from, which then fades out slowly.
///
/// The shield has its own reserve, Capacity, in energy units. Every absorbed hit drains it by exactly
/// the damage the hazard would have dealt (1:1). The reserve refills at Recharge Per Second, and that
/// refill is paid for out of the ship's energy pool (ShipEnergySystem), also 1:1 — so the ship ends up
/// spending as much energy as the enemy dealt damage, just a little later. A ship with no
/// ShipEnergySystem (an enemy ship) refills for free.
///
/// When the reserve can't cover a hit the shield collapses: the area goes fully transparent, that hit and
/// every following one are ignored (they fly through and damage blocks as if there were no shield), and
/// it only comes back once the reserve has refilled to Reactivate Fraction of Capacity.
///
/// The protected area's trigger collider is generated in Awake — the prefab only needs the optional
/// sprites below. The area is centered on the block's own visual center and works for any block shape.
/// The block's own maxHP is separate from all of this: destroying the block switches the field off.
/// </summary>
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
    private int topBase = 40;
    private int zoneAuthoredOrder;

    private Faction OwnFaction => identity != null ? identity.faction : Faction.Player;

    /// <summary>Whether the field is currently protecting — false while collapsed for lack of reserve.</summary>
    public bool IsUp => shieldUp && !IsDestroyed;

    /// <summary>Damage the shield can still absorb right now.</summary>
    public float Reserve { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Shield;
        energyDelta = -Mathf.Abs(energyDelta); // shields draw power, same convention as WeaponModule

        energy = GetComponentInParent<ShipEnergySystem>();
        identity = GetComponentInParent<ShipIdentity>();
        Reserve = capacity; // starts full

        BuildZone();

        if (zoneRenderer != null)
        {
            zoneAuthoredOrder = zoneRenderer.sortingOrder;
            SetZoneAlpha(zoneAlpha);
        }

        ApplyRadius();
    }

    /// <summary>Pushes Radius into the collider and the circle sprite. Runs on Awake, whenever the value
    /// changes in the Inspector, and can be called at runtime after changing radius from code (e.g. a
    /// shield upgrade). Impact crescents pick up the new radius when they spawn.</summary>
    public void ApplyRadius()
    {
        if (zoneCollider is CircleCollider2D circle && zoneRoot != null && zoneRoot.parent != null)
            circle.radius = radius / Mathf.Max(0.0001f, zoneRoot.parent.lossyScale.x); // collider radius is in local units

        if (zoneRenderer != null) FitToRadius(zoneRenderer.transform, zoneRenderer.sprite);
    }

    // Live preview while editing: resizes the circle sprite as Radius is dragged, and draws the real
    // radius as a gizmo so it can be judged even with no sprite assigned.
    private void OnValidate() => ApplyRadius();

    private void OnDrawGizmosSelected()
    {
        var center = (visualRoot != null ? visualRoot : transform).position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(center, radius);
    }

    /// <summary>Creates the trigger child that detects incoming hazards. Parented under the visual root so
    /// it always sits on the block's own center, whatever its footprint or rotation.</summary>
    private void BuildZone()
    {
        var parent = visualRoot != null ? visualRoot : transform;

        var zoneObj = new GameObject("ShieldZone");
        zoneRoot = zoneObj.transform;
        zoneRoot.SetParent(parent, false);

        var circle = zoneObj.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        zoneCollider = circle; // radius is set by ApplyRadius, right after this in Awake

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

    /// <summary>Refills the reserve, paying for it from the ship's energy — 1:1, and never more than the
    /// ship actually has.</summary>
    private void Recharge()
    {
        float missing = capacity - Reserve;
        if (missing <= 0f || rechargePerSecond <= 0f) return;

        float wanted = Mathf.Min(missing, rechargePerSecond * Time.deltaTime);
        if (energy != null)
        {
            wanted = Mathf.Min(wanted, energy.Current);
            if (wanted <= 0f || !energy.TrySpend(wanted)) return;
        }

        Reserve += wanted;
    }

    /// <summary>Called by ShieldZoneRelay for anything entering the protected area.</summary>
    public void HandleIncoming(Collider2D other)
    {
        if (!IsUp) return; // collapsed or destroyed — the hazard flies through untouched

        var hazard = other.GetComponentInParent<IShieldBlockable>();
        if (hazard == null || hazard.IsConsumed || !hazard.IsHostileTo(OwnFaction)) return;

        float damage = hazard.ImpactDamage;
        if (Reserve < damage)
        {
            shieldUp = false; // not enough reserve — the field collapses and this hit goes through
            return;
        }

        Reserve -= damage;
        if (Reserve <= 0.01f) shieldUp = false; // drained to nothing by this very hit

        Vector2 center = zoneRoot.position;
        Vector2 impactDirection = (Vector2)other.transform.position - center;

        hazard.AbsorbedByShield();
        SpawnImpactArc(impactDirection);
    }

    private void SpawnImpactArc(Vector2 towardImpact)
    {
        if (impactArcPrefab == null) return;

        var arc = Instantiate(impactArcPrefab, zoneRoot);
        arc.transform.localPosition = Vector3.zero;

        // World rotation, not local — the block's visual root rotates with its build orientation, but
        // the crescent has to face wherever the hit actually came from. Drawn facing up (+Y).
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

    /// <summary>Scales a transform so the given sprite's full width spans the shield's diameter.</summary>
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

    /// <summary>The field draws above every block body — same layer as a weapon's moving parts.</summary>
    public override void ApplySortingOrders(int roofBase, int topBase)
    {
        base.ApplySortingOrders(roofBase, topBase);
        this.topBase = topBase;
        if (zoneRenderer != null) zoneRenderer.sortingOrder = topBase + zoneAuthoredOrder;
    }

    /// <summary>The build ghost shouldn't drag a big translucent circle around with it.</summary>
    public override void PrepareAsGhost()
    {
        if (zoneRenderer != null) zoneRenderer.gameObject.SetActive(false);
    }

    protected override void OnModuleDestroyed()
    {
        if (zoneCollider != null) zoneCollider.enabled = false;
    }
}
