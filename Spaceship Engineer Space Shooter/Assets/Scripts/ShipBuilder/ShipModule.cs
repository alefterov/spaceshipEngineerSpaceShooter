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

    [Tooltip("The pivot/root cell this block was placed at. The GameObject's own transform always " +
             "sits exactly here and never rotates — only visualRoot (below) rotates/repositions.")]
    public Vector2Int anchorCell;

    [Tooltip("0-3, ×90° clockwise. Stored so a saved+reloaded ship reproduces the exact same footprint.")]
    public int rotationSteps;

    [Tooltip("If true, destroying this module destroys the whole ship (e.g. the cockpit/core hull piece).")]
    public bool isCore = false;

    [Header("View sprites")]
    [Tooltip("Shown in the main menu ship preview — the finished, closed-up look.")]
    public Sprite closedSprite;
    [Tooltip("Shown in the ship builder — exposed internals so the grid/wiring reads clearly.")]
    public Sprite openSprite;

    [Header("Visual (child object)")]
    [Tooltip("Child object holding the sprite(s). ShipGrid repositions/rotates THIS around the root — " +
             "the root itself never moves or rotates, so multi-cell shapes stay correctly anchored " +
             "no matter how many times the block is rotated or reloaded from a save.")]
    public Transform visualRoot;

    [Header("Idle animation (optional)")]
    [Tooltip("Animator for this block's idle/ambient animation (e.g. a weapon humming, a light " +
             "blinking). Deliberately NOT tied to ShipViewMode — it plays during the main menu ship " +
             "preview and, later, battle idle (a separate context ShipViewMode doesn't cover at all), " +
             "but stays off while actively building. Leave unassigned for blocks with no animation.")]
    public Animator idleAnimator;

    private readonly System.Collections.Generic.List<SpriteRenderer> spriteRenderers = new();

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

        spriteRenderers.Clear();
        spriteRenderers.AddRange(visualRoot.GetComponentsInChildren<SpriteRenderer>(true));

        SetIdleAnimationPlaying(false); // safe default until something explicitly turns it on
    }

    /// <summary>Swaps the visible sprite on every cell's child renderer, and — since the builder is
    /// the one context idle animation should NEVER play in — turns it off in Building mode and on in
    /// Preview. Called by ShipGrid.SetViewMode. (Battle idle, later, will call SetIdleAnimationPlaying
    /// directly instead — it isn't a ShipViewMode at all.)</summary>
    public void ApplyViewMode(ShipViewMode mode)
    {
        Sprite target = mode == ShipViewMode.Preview ? closedSprite : openSprite;
        if (target != null)
            foreach (var r in spriteRenderers)
                if (r != null) r.sprite = target;

        SetIdleAnimationPlaying(mode == ShipViewMode.Preview);
    }

    /// <summary>Turns this block's idle/ambient animation on or off. Safe to call even when no
    /// idleAnimator is assigned (most blocks won't have one) — a no-op in that case.</summary>
    public void SetIdleAnimationPlaying(bool playing)
    {
        if (idleAnimator != null) idleAnimator.enabled = playing;
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
    Generator
}

/// <summary>Preview = closed/finished look (main menu), Building = exposed internals (editor).</summary>
public enum ShipViewMode
{
    Preview,
    Building
}