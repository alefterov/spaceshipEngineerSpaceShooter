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

    [Header("Stats")]
    public float maxHP = 20f;
    public float mass = 1f;
    [Tooltip("Energy this module produces (generators) or consumes (weapons/shields) per second.")]
    public float energyDelta = 0f;

    [Header("Grid placement (set by ShipGrid when placed)")]
    [Tooltip("All grid cells (in ship-local coordinates) this module occupies. " +
             "Supports arbitrary 1-4 cell shapes, not just rectangles.")]
    public System.Collections.Generic.List<Vector2Int> occupiedCells = new() { Vector2Int.zero };

    /// <summary>Bottom-left/first cell of the shape — used as the placement anchor.</summary>
    public Vector2Int AnchorCell => occupiedCells.Count > 0 ? occupiedCells[0] : Vector2Int.zero;

    [Tooltip("If true, destroying this module destroys the whole ship (e.g. the cockpit/core hull piece).")]
    public bool isCore = false;

    [Header("View sprites")]
    [Tooltip("Shown in the main menu ship preview — the finished, closed-up look.")]
    public Sprite closedSprite;
    [Tooltip("Shown in the ship builder — exposed internals so the grid/wiring reads clearly.")]
    public Sprite openSprite;

    private SpriteRenderer spriteRenderer;

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    // Fired when this module takes damage — UI/VFX can subscribe.
    public event Action<ShipModule, float> OnDamaged;
    // Fired once, when HP hits zero.
    public event Action<ShipModule> OnDestroyed;

    protected virtual void Awake()
    {
        CurrentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>Swaps the visible sprite. Called by ShipGrid.SetViewMode for every module at once.</summary>
    public void ApplyViewMode(ShipViewMode mode)
    {
        if (spriteRenderer == null) return;

        Sprite target = mode == ShipViewMode.Preview ? closedSprite : openSprite;
        if (target != null) spriteRenderer.sprite = target;
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
