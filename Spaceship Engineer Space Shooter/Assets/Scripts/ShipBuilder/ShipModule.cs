using UnityEngine;
using System;

/// <summary>
/// Base component for any part of a ship (hull, weapon, engine, shield, armor, generator).
/// Attach this to every placeable module prefab. Handles per-module HP and destruction,
/// which is the core of "точечный урон по модулям".
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShipModule : MonoBehaviour
{
    [Header("Module Definition")]
    public ModuleType type = ModuleType.Hull;
    public string moduleId = "hull_basic";

    [Tooltip("Credits this block cost to build — always set from BlockDefinition.buildCost at " +
             "placement time (ShipGrid.Place), same as moduleId. Not meant to be hand-edited on the " +
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

    [Tooltip("Per-cell shape, same order/index as occupiedCells — set alongside it by ShipGrid.Place " +
             "from BlockDefinition.cellShapes (rotated to match). Used only by structural (Hull/Armor) " +
             "adjacency checks (ShipGrid.CanPlaceHull) to keep new blocks off a triangle's hypotenuse.")]
    public System.Collections.Generic.List<CellShape> cellShapes = new() { CellShape.Square };

    [Tooltip("The pivot/root cell this block was placed at. The GameObject's own transform always " +
             "sits exactly here and never rotates — only visualRoot (below) rotates/repositions.")]
    public Vector2Int anchorCell;

    [Tooltip("0-3, ×90° clockwise. Stored so a saved+reloaded ship reproduces the exact same footprint.")]
    public int rotationSteps;

    [Tooltip("If true, destroying this module destroys the whole ship (e.g. the cockpit/core hull piece).")]
    public bool isCore = false;

    [Header("Visual (child object)")]
    [Tooltip("Child object holding every visual part. ShipGrid repositions/rotates THIS around the root — " +
             "the root itself never moves or rotates, so multi-cell shapes stay correctly anchored " +
             "no matter how many times the block is rotated or reloaded from a save.")]
    public Transform visualRoot;

    [Header("Visual parts (separate child objects, toggled by view mode)")]
    [Tooltip("Everything that sits INSIDE the hull — e.g. a generator's machinery, a turret's mounting " +
             "base. ALWAYS active — never deactivated. Reads as hidden once the roof goes on purely " +
             "because roofRoot sits at a higher sorting order and is opaque over it, not because this " +
             "object is switched off.")]
    public Transform interiorRoot;
    [Tooltip("This module's own roof. Shown only while the ship is closed (menu preview and battle), " +
             "and drawn ABOVE the hull's own roof — see ShipGrid's roof sorting orders.")]
    public Transform roofRoot;
    [Tooltip("The functional part itself protruding above the roof — e.g. a turret's barrel. Unlike " +
             "interiorRoot/roofRoot this is ALWAYS visible (builder included), and always drawn above " +
             "every roof — see ShipGrid's top sorting order.")]
    public Transform topRoot;

    [Header("Idle animation (optional)")]
    [Tooltip("Animator for this block's idle/ambient animation (e.g. a weapon humming, a light " +
             "blinking). Deliberately NOT tied to ShipViewMode — it plays during the main menu ship " +
             "preview and, later, battle idle (a separate context ShipViewMode doesn't cover at all), " +
             "but stays off while actively building. Leave unassigned for blocks with no animation.")]
    public Animator idleAnimator;

    // Sorting orders authored in the prefab are kept as OFFSETS, so ApplySortingOrders can shift a
    // whole group onto its layer while preserving the relative order inside it (e.g. a barrel
    // authored one above its roof stays one above it). Cached so repeated calls can't compound.
    private readonly System.Collections.Generic.List<(SpriteRenderer renderer, int authoredOrder)> interiorRenderers = new();
    private readonly System.Collections.Generic.List<(SpriteRenderer renderer, int authoredOrder)> roofRenderers = new();
    private readonly System.Collections.Generic.List<(SpriteRenderer renderer, int authoredOrder)> topRenderers = new();

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    // Fired when this module takes damage — UI/VFX can subscribe.
    public event Action<ShipModule, float> OnDamaged;
    // Fired once, when HP hits zero.
    public event Action<ShipModule> OnDestroyed;

    protected virtual void Awake()
    {
        CurrentHP = maxHP;

        if (visualRoot == null) visualRoot = transform; // fallback for old single-cell prefabs without a child

        CacheRenderers(interiorRoot, interiorRenderers);
        CacheRenderers(roofRoot, roofRenderers);
        CacheRenderers(topRoot, topRenderers);

        SetIdleAnimationPlaying(false); // safe default until something explicitly turns it on
    }

    private static void CacheRenderers(
        Transform root, System.Collections.Generic.List<(SpriteRenderer, int)> into)
    {
        into.Clear();
        if (root == null) return;

        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            into.Add((renderer, renderer.sortingOrder));
    }

    /// <summary>
    /// Toggles the roof: shown while the ship is closed (menu preview, battle), hidden in the builder.
    /// interiorRoot is deliberately NEVER deactivated — it stays active always and is simply covered
    /// by the (higher-sorted, opaque) roof when one is showing, the same way a real wall hides a room
    /// instead of the room ceasing to exist. topRoot and anything parented directly under visualRoot
    /// rather than under either root are left alone too, so they stay visible in both.
    ///
    /// Also turns idle animation off in Building mode — the builder is the one context it should never
    /// play in. Called by ShipGrid.SetViewMode. (Battle idle, later, will call SetIdleAnimationPlaying
    /// directly instead — it isn't a ShipViewMode at all.)
    /// </summary>
    public void ApplyViewMode(ShipViewMode mode)
    {
        bool closed = mode == ShipViewMode.Preview;

        if (roofRoot != null) roofRoot.gameObject.SetActive(closed);

        SetIdleAnimationPlaying(closed);
    }

    /// <summary>
    /// Puts this module's parts on their render layers. Called by ShipGrid at placement time, which is
    /// the only thing that knows whether this block went onto the structural or the module layer —
    /// that distinction is what keeps a module's roof drawn above the hull's roof rather than under it.
    /// Sorting orders authored in the prefab act as offsets within each group.
    /// </summary>
    public void ApplySortingOrders(int interiorBase, int roofBase, int topBase)
    {
        foreach (var (renderer, authoredOrder) in interiorRenderers)
            if (renderer != null) renderer.sortingOrder = interiorBase + authoredOrder;

        foreach (var (renderer, authoredOrder) in roofRenderers)
            if (renderer != null) renderer.sortingOrder = roofBase + authoredOrder;

        foreach (var (renderer, authoredOrder) in topRenderers)
            if (renderer != null) renderer.sortingOrder = topBase + authoredOrder;
    }

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

    protected virtual void Destroy()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        OnDestroyed?.Invoke(this);

        // Disable functional behaviour (weapon firing, engine thrust, shield, etc.)
        // Subclasses (WeaponModule, EngineModule...) should override OnModuleDestroyed().
        OnModuleDestroyed();

        // Detach visually instead of instantly deleting: gives the satisfying "part falls off" feel.
        DetachAsDebris();
    }

    /// <summary>Override in subclasses to stop the module's function (e.g. WeaponModule stops firing).</summary>
    protected virtual void OnModuleDestroyed() { }

    /// <summary>Turns the destroyed module into free-falling debris instead of just vanishing.</summary>
    protected virtual void DetachAsDebris()
    {
        transform.SetParent(null);

        var rb = gameObject.GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.5f;
        rb.linearVelocity = UnityEngine.Random.insideUnitCircle * 2f;
        rb.angularVelocity = UnityEngine.Random.Range(-180f, 180f);

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false; // no longer blocks shots / triggers hits

        UnityEngine.Object.Destroy(gameObject, 3f); // cleanup after debris drifts off
    }
}

public enum ModuleType
{
    Hull,
    Armor,
    Weapon,
    Engine,
    Shield,
    Generator,
    Cockpit
}

/// <summary>Preview = closed/finished look (main menu), Building = exposed internals (editor).</summary>
public enum ShipViewMode
{
    Preview,
    Building
}