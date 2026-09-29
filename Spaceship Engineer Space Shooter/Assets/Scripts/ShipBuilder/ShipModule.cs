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

    [Tooltip("Credits this block cost to build — always set from BlockDefinition.buildCost at " +
             "placement time (ShipGrid.PlaceBlock), same as moduleId. Not meant to be hand-edited on the " +
             "prefab; read by GhostBlockController to compute the dismantle refund.")]
    public int buildCost;

    [Header("Stats")]
    public float maxHP = 20f;
    public float mass = 1f;
    [Tooltip("Energy this module produces (generators) or consumes (weapons/shields) per second.")]
    public float energyDelta = 0f;

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

    protected virtual void Awake()
    {
        CurrentHP = maxHP;

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

    /// <summary>Subclasses can veto the idle animation even in the Preview look — e.g. a turret in battle,
    /// where an animation keyed on its rotation would fight the code that aims it.</summary>
    protected virtual bool IdleAnimationAllowed => true;

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
    public virtual void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        OnDamaged?.Invoke(this, amount);

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
    /// after a fresh block is placed) — e.g. the crew Engineer's max-HP bonus, applied by ShipGrid.
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

        PlayDestructionEffect();
    }

    /// <summary>Override in subclasses to stop the module's function (e.g. WeaponModule stops firing).</summary>
    protected virtual void OnModuleDestroyed() { }

    /// <summary>Spawns the explosion VFX (if assigned) and removes the block immediately — it
    /// disappears rather than detaching as drifting/falling debris under gravity.</summary>
    protected virtual void PlayDestructionEffect()
    {
        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

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
    Repair = 7
}

/// <summary>Preview = closed/finished look (main menu), Building = exposed internals (editor).</summary>
public enum ShipViewMode
{
    Preview,
    Building
}