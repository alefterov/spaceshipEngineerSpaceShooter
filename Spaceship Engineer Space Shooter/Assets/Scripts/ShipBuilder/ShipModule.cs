using UnityEngine;
using System;

[RequireComponent(typeof(Collider2D))]
public class ShipModule : MonoBehaviour
{
    [Header("Module Definition")]
    public ModuleType type = ModuleType.Armor;
    public string moduleId = "block";

    [System.NonSerialized] public int buildCost;

    [Header("Resistances")]
    [Tooltip("Share of LASER damage this block shrugs off, 0 to 1 — an energy armor block, say, 0.8 takes only a fifth of a beam's damage. 0 = none.")]
    [Range(0f, 1f)] public float laserResistance = 0f;

    [Header("Stats")]
    public float maxHP = 20f;
    public float mass = 1f;
    [Tooltip("Energy, with a sign: generators produce it (+, per second). Everything else spends it (−, enter the amount " +
             "as a number, the sign is fixed automatically) — and WHEN depends on the block: a weapon, per SHOT; a shield, " +
             "per HIT it absorbs; an engine, per second while the ship is MOVING.")]
    public float energyDelta = 0f;

    public virtual float EnergyPerSecondEstimate => energyDelta;

    [Header("Grid placement (set by ShipGrid when placed)")]
    [Tooltip("All grid cells (in absolute ship-grid coordinates) this module occupies. " +
             "Computed purely from block shape + anchor + rotation — independent from any transform, " +
             "so it never drifts out of sync after rotating or reloading a saved layout.")]
    public System.Collections.Generic.List<Vector2Int> occupiedCells = new() { Vector2Int.zero };

    [Tooltip("Per-cell shape, same order/index as occupiedCells — set alongside it by ShipGrid.PlaceBlock " +
             "from BlockDefinition.cells (rotated to match). Used by ShipGrid.CanPlace's adjacency check " +
             "to keep new blocks off a triangle's hypotenuse.")]
    public System.Collections.Generic.List<CellShape> cellShapes = new() { CellShape.Square };

    [Tooltip("The pivot/root cell this block was placed at. The GameObject's own transform always " +
             "sits exactly here and never rotates — only visualRoot (below) rotates/repositions.")]
    public Vector2Int anchorCell;

    [Tooltip("0-3, ×90° clockwise. Stored so a saved+reloaded ship reproduces the exact same footprint.")]
    public int rotationSteps;

    [Tooltip("If true, destroying this block destroys the whole ship.")]
    public bool isCore = false;

    [Header("Visual (child object)")]
    [Tooltip("Child object holding every visual part. ShipGrid repositions/rotates THIS around the root — " +
             "the root itself never moves or rotates, so multi-cell shapes stay correctly anchored " +
             "no matter how many times the block is rotated or reloaded from a save.")]
    public Transform visualRoot;

    [Header("Visual part")]
    [Tooltip("The block's own body sprite(s) — everything visible of this block. Always shown (builder, " +
             "menu preview and battle alike), drawn at ShipGrid's roof sorting order. Sorting orders " +
             "authored inside the prefab are kept as offsets from that base. Weapons additionally have " +
             "a separate Top Root for their moving parts — see WeaponModule.")]
    public Transform roofRoot;

    [Header("Idle animation (optional)")]
    [Tooltip("Animator for this block's idle/ambient animation (e.g. a weapon humming, a light " +
             "blinking). Deliberately NOT tied to ShipViewMode — it plays during the main menu ship " +
             "preview and, later, battle idle (a separate context ShipViewMode doesn't cover at all), " +
             "but stays off while actively building. Leave unassigned for blocks with no animation.")]
    public Animator idleAnimator;

    [Header("Destruction")]
    [Tooltip("Spawned at this block's position when it's destroyed — an explosion VFX (particle " +
             "system, animated sprite, whatever). Optional; leave unassigned for no effect.")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Seconds until the spawned explosion is removed. Make it at least as long as the effect. 0 = never removed by this script.")]
    public float explosionLifetime = 3f;
    [Tooltip("When this block is destroyed it blows up and deals this much damage to every block touching it. Armor never " +
             "does (it does not explode). 0 = no blast damage.")]
    public float blastDamageToNeighbours = 1f;
    [Tooltip("Sound of this block being destroyed.")]
    public AudioClip destroySound;
    [Range(0f, 1f)] public float destroyVolume = 1f;

    [Header("Reward")]
    [Tooltip("Credits the player earns when this block is destroyed while it belongs to an ENEMY ship — so every block of a " +
             "destroyed enemy ship pays out. (Nothing for the player's own blocks.) Scaled by the usual battle multipliers.")]
    public int rewardCredits = 5;
    [Tooltip("Experience the player earns for the same.")]
    public int rewardExperience = 0;

    private readonly System.Collections.Generic.List<(SpriteRenderer renderer, int authoredOrder)> roofRenderers = new();

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    public event Action<ShipModule, float> OnDamaged;
    public event Action<ShipModule, float> OnHealed;
    public event Action<ShipModule> OnDestroyed;

    private Collider2D bodyCollider;

    public Bounds WorldBounds
    {
        get
        {
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider != null)
            {
                Bounds b = bodyCollider.bounds;
                if (b.size.x > 0.001f && b.size.y > 0.001f) return b;
            }

            return new Bounds(transform.position, new Vector3(1f, 1f, 0f));
        }
    }

    protected virtual void Awake()
    {
        CurrentHP = maxHP;

        var ownShip = GetComponentInParent<ShipIdentity>();
        if (ownShip != null) inCombat = ownShip.CombatActive;

        if (visualRoot == null || visualRoot == transform)
        {
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            visualRoot = pivot;
        }

        poseParts = CapturePoseParts();

        CacheRenderers(roofRoot, roofRenderers);

        SetIdleAnimationPlaying(false);

        var wear = GetComponentInChildren<BlockDamageVisual>(true);
        if (wear == null) gameObject.AddComponent<BlockDamageVisual>();
        else wear.enabled = true;
    }

    protected static void CacheRenderers(
        Transform root, System.Collections.Generic.List<(SpriteRenderer, int)> into)
    {
        into.Clear();
        if (root == null) return;

        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            into.Add((renderer, renderer.sortingOrder));
    }

    public struct PosePart
    {
        public Transform part;
        public Vector3 position;
        public Quaternion rotation;
    }

    private System.Collections.Generic.List<PosePart> poseParts;

    protected virtual System.Collections.Generic.IEnumerable<Transform> VisualParts
    {
        get { if (roofRoot != null) yield return roofRoot; }
    }

    public System.Collections.Generic.List<PosePart> CapturePoseParts()
    {
        var list = new System.Collections.Generic.List<PosePart>();
        var parts = new System.Collections.Generic.List<Transform>();
        foreach (var part in VisualParts)
            if (part != null && part != transform && !parts.Contains(part)) parts.Add(part);

        foreach (var part in parts)
        {
            if (visualRoot != null && visualRoot != transform && part.IsChildOf(visualRoot)) continue;
            bool insideAnother = false;
            foreach (var other in parts)
                if (other != part && part.IsChildOf(other)) insideAnother = true;
            if (insideAnother) continue;

            list.Add(new PosePart { part = part, position = part.localPosition, rotation = part.localRotation });
        }

        return list;
    }

    public static void PoseParts(System.Collections.Generic.List<PosePart> parts, Vector3 center, Vector3 unrotatedCenter, int rotationSteps)
    {
        if (parts == null) return;

        var rotation = Quaternion.Euler(0f, 0f, 90f * rotationSteps);
        foreach (var p in parts)
        {
            if (p.part == null) continue;
            p.part.localPosition = center + rotation * (p.position - unrotatedCenter);
            p.part.localRotation = rotation * p.rotation;
        }
    }

    public void ApplyGridPose(Vector3 center, Vector3 unrotatedCenter, int steps)
    {
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localPosition = center;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * steps);
        }

        PoseParts(poseParts, center, unrotatedCenter, steps);
    }

    public virtual void ApplyViewMode(ShipViewMode mode)
    {
        previewLook = mode == ShipViewMode.Preview;
        RefreshIdleAnimation();
    }

    private bool previewLook;

    protected virtual bool IdleAnimationAllowed => !inCombat;

    private bool inCombat;

    public void SetInCombat(bool active)
    {
        inCombat = active;
        RefreshIdleAnimation();
    }

    protected void RefreshIdleAnimation() => SetIdleAnimationPlaying(previewLook && IdleAnimationAllowed);

    public virtual void ApplySortingOrders(int roofBase, int topBase)
    {
        foreach (var (renderer, authoredOrder) in roofRenderers)
            if (renderer != null) renderer.sortingOrder = roofBase + authoredOrder;
    }

    public virtual void PrepareAsGhost() { }

    public void SetIdleAnimationPlaying(bool playing)
    {
        if (idleAnimator != null) idleAnimator.enabled = playing;
    }

    public CellShape GetCellShape(Vector2Int cell)
    {
        int index = occupiedCells.IndexOf(cell);
        return index >= 0 && index < cellShapes.Count ? cellShapes[index] : CellShape.Square;
    }

    private bool nextHitIsCrit;

    public void TakeDamage(float amount, bool crit)
    {
        nextHitIsCrit = crit;
        TakeDamage(amount);
        nextHitIsCrit = false;
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        if (CompareTag("EnemyShip"))
        {
            var ownShip = GetComponentInParent<ShipIdentity>();
            if (ownShip != null && ownShip.faction == Faction.Enemy && !BattleZone.BlockInZone(WorldBounds)) return;
        }

        if (CompareTag("PlayerShip") && GameDataManager.Instance != null && GameDataManager.Instance.RollCrewChance(CrewPerkType.ZeroDamageChance))
            return;

        float hpBefore = CurrentHP;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        OnDamaged?.Invoke(this, amount);
        DamageNumbers.Report(this, hpBefore - CurrentHP, nextHitIsCrit);

        if (CurrentHP <= 0f)
        {
            Destroy();
        }
    }

    public void Heal(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        float before = CurrentHP;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        if (CurrentHP > before) OnHealed?.Invoke(this, CurrentHP - before);
    }

    public void ApplyMaxHpMultiplier(float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f) || multiplier <= 0f) return;
        maxHP *= multiplier;
        CurrentHP = maxHP;
    }

    protected virtual void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        OnDestroyed?.Invoke(this);

        OnModuleDestroyed();

        BlastNeighbours();
        PayReward();
        AudioManager.PlaySfx(destroySound, destroyVolume);

        PlayDestructionEffect();
    }
    private void PayReward()
    {
        if (!CompareTag("EnemyShip")) return;

        if (GameDataManager.Instance != null) GameDataManager.Instance.RecordBlockDestroyed(type);

        if (rewardCredits <= 0 && rewardExperience <= 0) return;
        BattleRewards.AddKill(rewardCredits, rewardExperience);
    }

    private void BlastNeighbours()
    {
        if (type == ModuleType.Armor || blastDamageToNeighbours <= 0f) return;

        foreach (var neighbour in MissileTargeting.GetNeighbours(this))
            if (neighbour != null && !neighbour.IsDestroyed) neighbour.TakeDamage(blastDamageToNeighbours);
    }

    protected virtual void OnModuleDestroyed() { }

    protected virtual void PlayDestructionEffect()
    {
        if (explosionEffectPrefab != null)
        {
            var fx = EffectSorting.Apply(Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity));
            if (explosionLifetime > 0f) UnityEngine.Object.Destroy(fx, explosionLifetime);
        }

        var wear = GetComponentInChildren<BlockDamageVisual>(true);
        if (wear != null) wear.SpawnShatter();

        UnityEngine.Object.Destroy(gameObject);
    }
}

public enum ModuleType
{
    Armor = 1,
    Weapon = 2,
    Engine = 3,
    Shield = 4,
    Generator = 5,
    Cockpit = 6,
    Repair = 7,
    Meteor = 8
}

public enum ShipViewMode
{
    Preview,
    Building
}
