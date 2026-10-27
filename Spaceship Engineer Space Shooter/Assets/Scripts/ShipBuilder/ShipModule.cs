using UnityEngine;
using System;

/// <summary>
/// Base component for any block of a ship (armor, weapon, engine, shield, generator, cockpit).
/// Attach this to every placeable block prefab. Every block owns its own HP and is destroyed
/// individually — that is the core of "точечный урон по модулям".
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShipModule : MonoBehaviour
{
    [Header("Module Definition")]
    public ModuleType type = ModuleType.Armor;
    public string moduleId = "block";

    /// <summary>Credits this block cost to build — set from BlockDefinition.buildCost at placement time (ShipGrid.PlaceBlock),
    /// the ONE place a price is entered. Not saved on the prefab (so a stale number there can never be mistaken for the
    /// price); read by GhostBlockController to compute the dismantle refund.</summary>
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

    /// <summary>Energy per second this block adds (+) or draws (−) on average — what the ship's stats panel and
    /// energy balance show. For most blocks that is Energy Delta itself; blocks whose Energy Delta means something
    /// else (a weapon's cost per shot, a shield's cost per hit) turn it into a per-second estimate.</summary>
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

    // Sorting orders authored in the prefab are kept as OFFSETS, so ApplySortingOrders can shift a
    // whole group onto its layer while preserving the relative order inside it. Cached so repeated
    // calls can't compound.
    private readonly System.Collections.Generic.List<(SpriteRenderer renderer, int authoredOrder)> roofRenderers = new();

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    // Fired when this module takes damage — UI/VFX can subscribe.
    public event Action<ShipModule, float> OnDamaged;
    // Fired when this module is healed (e.g. by a RepairModule) — BlockDamageVisual removes marks on this.
    public event Action<ShipModule, float> OnHealed;
    // Fired once, when HP hits zero.
    public event Action<ShipModule> OnDestroyed;

    private Collider2D bodyCollider;

    /// <summary>The block's footprint in world space (its collider's bounds; one cell around the block's position if
    /// the collider is unusable).</summary>
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

        // A block placed onto a ship that is already fighting starts with its idle animation off.
        var ownShip = GetComponentInParent<ShipIdentity>();
        if (ownShip != null) inCombat = ownShip.CombatActive;

        if (visualRoot == null) visualRoot = transform; // fallback for old single-cell prefabs without a child

        CacheRenderers(roofRoot, roofRenderers);

        SetIdleAnimationPlaying(false); // safe default until something explicitly turns it on

        // Every block shows wear: a prefab's own BlockDamageVisual keeps its tuned settings but is
        // switched on (the old sprite-tier version was left disabled on most prefabs); one with none gets
        // a default instance.
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

    /// <summary>
    /// Turns idle animation off in Building mode — the builder is the one context it should never play
    /// in — and on in Preview (the closed look shown in the main menu and in battle — see ShipGrid's own
    /// doc comment on CurrentViewMode). Called by ShipGrid.SetViewMode/PlaceBlock. Nothing is shown/hidden
    /// any more: a block's body is always visible. Virtual so EngineModule can also stop/start its burn
    /// effect here — it shouldn't so much as idle while sitting in the builder.
    /// </summary>
    public virtual void ApplyViewMode(ShipViewMode mode)
    {
        previewLook = mode == ShipViewMode.Preview;
        RefreshIdleAnimation();
    }

    private bool previewLook;

    /// <summary>The idle animation plays only outside battle: during a fight every block's animator is off (a turret's
    /// clip, for one, keys its rotation and would fight the code that aims it).</summary>
    protected virtual bool IdleAnimationAllowed => !inCombat;

    private bool inCombat;

    /// <summary>Called when the ship's battle starts or ends (ShipIdentity.SetCombatActive): the idle animation is switched
    /// off for the battle.</summary>
    public void SetInCombat(bool active)
    {
        inCombat = active;
        RefreshIdleAnimation();
    }

    /// <summary>Re-applies "idle animation plays" = preview look AND the subclass allows it. Call after
    /// whatever IdleAnimationAllowed depends on changes.</summary>
    protected void RefreshIdleAnimation() => SetIdleAnimationPlaying(previewLook && IdleAnimationAllowed);

    /// <summary>
    /// Puts this block's sprites on their render layer. Called by ShipGrid at placement time. Sorting
    /// orders authored in the prefab act as offsets from roofBase. Weapons override this to also place
    /// their separate moving parts (see WeaponModule.topRoot) on topBase, above every block body.
    /// </summary>
    public virtual void ApplySortingOrders(int roofBase, int topBase)
    {
        foreach (var (renderer, authoredOrder) in roofRenderers)
            if (renderer != null) renderer.sortingOrder = roofBase + authoredOrder;
    }

    /// <summary>Called on the build ghost right before its gameplay scripts are stripped — override to
    /// hide anything that only makes sense on a real placed block (e.g. a shield's coverage circle).</summary>
    public virtual void PrepareAsGhost() { }

    /// <summary>Turns this block's idle/ambient animation on or off. Safe to call even when no
    /// idleAnimator is assigned (most blocks won't have one) — a no-op in that case.</summary>
    public void SetIdleAnimationPlaying(bool playing)
    {
        if (idleAnimator != null) idleAnimator.enabled = playing;
    }

    /// <summary>Shape of a specific occupied cell — Square if this cell (or the whole block, for
    /// anything placed before triangular shapes existed) has no explicit entry.</summary>
    public CellShape GetCellShape(Vector2Int cell)
    {
        int index = occupiedCells.IndexOf(cell);
        return index >= 0 && index < cellShapes.Count ? cellShapes[index] : CellShape.Square;
    }

    /// <summary>
    /// Apply damage directly to THIS module only (called by the projectile/hit system
    /// after it resolves which module collider was actually struck).
    /// </summary>
    private bool nextHitIsCrit;

    /// <summary>A hit that may be critical: the damage number above the block turns red. The damage itself is whatever
    /// the caller passes in — the crit multiplier is applied by the caller.</summary>
    public void TakeDamage(float amount, bool crit)
    {
        nextHitIsCrit = crit;
        TakeDamage(amount);
        nextHitIsCrit = false;
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        // A block of an enemy ship is only vulnerable while it is in the battle zone (see BattleZone): inside the screen, or just
        // passing the top edge. The part of a ship that is still beyond the edge takes no damage.
        if (CompareTag("EnemyShip"))
        {
            var ownShip = GetComponentInParent<ShipIdentity>();
            if (ownShip != null && ownShip.faction == Faction.Enemy && !BattleZone.BlockInZone(WorldBounds)) return;
        }

        // Crew ZeroDamageChance perk — blocks of the player's ship only: the hit simply does nothing.
        if (CompareTag("PlayerShip") && GameDataManager.Instance != null && GameDataManager.Instance.RollCrewChance(CrewPerkType.ZeroDamageChance))
            return;

        float hpBefore = CurrentHP;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        OnDamaged?.Invoke(this, amount);
        DamageNumbers.Report(this, hpBefore - CurrentHP, nextHitIsCrit); // the floating number above the block (battle scene)

        if (CurrentHP <= 0f)
        {
            Destroy();
        }
    }

    /// <summary>Restores HP, e.g. from a RepairModule — symmetric to TakeDamage. A no-op once destroyed
    /// (a wreck isn't repaired back to life) or if it's already at full HP.</summary>
    public void Heal(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        float before = CurrentHP;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        if (CurrentHP > before) OnHealed?.Invoke(this, CurrentHP - before);
    }

    /// <summary>Scales maxHP (and tops CurrentHP back up to match, since this only ever runs once, right
    /// after a fresh block is placed) — e.g. the crew ArmorEfficiency perk, applied by ShipGrid.
    /// PlaceBlock for the player's own ships only. A no-op for a 1x multiplier.</summary>
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

        // Disable functional behaviour (weapon firing, engine thrust, shield, etc.)
        // Subclasses (WeaponModule, EngineModule...) should override OnModuleDestroyed().
        OnModuleDestroyed();

        BlastNeighbours();
        PayReward();
        AudioManager.PlaySfx(destroySound, destroyVolume);

        PlayDestructionEffect();
    }
    /// <summary>Destroying a block of an enemy ship earns the player its reward (see Reward Credits).</summary>
    private void PayReward()
    {
        if (!CompareTag("EnemyShip") || (rewardCredits <= 0 && rewardExperience <= 0)) return;
        BattleRewards.AddKill(rewardCredits, rewardExperience);
    }


    /// <summary>The explosion hurts the blocks next to this one (1 damage each by default) — every kind of block except
    /// armor. A neighbour that dies of it explodes in turn, so a chain can run along the ship.</summary>
    private void BlastNeighbours()
    {
        if (type == ModuleType.Armor || blastDamageToNeighbours <= 0f) return;

        // The list is made first: damage can destroy blocks, which changes the ship's own list of blocks.
        foreach (var neighbour in MissileTargeting.GetNeighbours(this))
            if (neighbour != null && !neighbour.IsDestroyed) neighbour.TakeDamage(blastDamageToNeighbours);
    }

    /// <summary>Override in subclasses to stop the module's function (e.g. WeaponModule stops firing).</summary>
    protected virtual void OnModuleDestroyed() { }

    /// <summary>Spawns the explosion VFX (if assigned) and removes the block immediately — it
    /// disappears rather than detaching as drifting/falling debris under gravity.</summary>
    protected virtual void PlayDestructionEffect()
    {
        if (explosionEffectPrefab != null)
        {
            var fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            if (explosionLifetime > 0f) UnityEngine.Object.Destroy(fx, explosionLifetime);
        }

        var wear = GetComponentInChildren<BlockDamageVisual>(true);
        if (wear != null) wear.SpawnShatter(); // the block bursts into shards that fly apart and fade

        UnityEngine.Object.Destroy(gameObject);
    }
}

// Explicit values keep already-serialized prefabs/assets pointing at the same entries now that Hull
// (was 0) no longer exists.
public enum ModuleType
{
    Armor = 1,
    Weapon = 2,
    Engine = 3,
    Shield = 4,
    Generator = 5,
    Cockpit = 6,
    Repair = 7,
    /// <summary>NOT a block: only used as a missile target category (MissileTargetPriority) meaning "meteors".</summary>
    Meteor = 8
}

/// <summary>Preview = closed/finished look (main menu), Building = exposed internals (editor).</summary>
public enum ShipViewMode
{
    Preview,
    Building
}