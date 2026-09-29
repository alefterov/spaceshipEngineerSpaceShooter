using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The ship's live energy pool — current charge vs max capacity. Max is the sum of every surviving
/// Generator's own Capacity (see GeneratorModule.capacity — a separate concept from Power Output,
/// which is the generation RATE that refills this pool over time). Losing a generator shrinks the
/// pool immediately, trimming any stored energy above the new max.
///
/// PRIORITY: every spender (ShipMovement/EnemyShip, WeaponModule, ShieldModule, RepairModule) tags its
/// draw with an EnergyPriorityGroup and calls the TrySpend(amount, group) overload instead of the plain
/// one. Each frame, the pool is sliced into a per-group budget by PriorityOrder — the top group can draw
/// up to the WHOLE pool; each group below that gets whatever's left after subtracting how much every
/// group above it actually spent LAST frame. That's an approximation (this frame's higher-priority
/// demand isn't known yet when the slicing happens), but it self-corrects within a frame or two and
/// never lets a low-priority system starve a high-priority one for more than an instant. See
/// EnergyPriorityController for the in-battle UI that reorders PriorityOrder (Movement/Weapons/Defense/
/// Repair) via drag-and-drop while the battle is paused.
///
/// SETUP: put on the ship root, alongside ShipGrid/ShipIdentity.
/// </summary>
[RequireComponent(typeof(ShipGrid))]
public class ShipEnergySystem : MonoBehaviour
{
    /// <summary>The 4 systems energy can be prioritized between — see the in-battle priority window
    /// (EnergyPriorityController). Movement = engines. Weapons = every weapon type. Defense = shield
    /// generators (and, later, active armor). Repair = repair blocks.</summary>
    public enum EnergyPriorityGroup { Movement, Weapons, Defense, Repair }

    public float Current { get; private set; }
    public float Max { get; private set; }

    /// <summary>Fired whenever Current or Max changes — UI gauges refresh on this.</summary>
    public event Action OnEnergyChanged;

    private ShipGrid grid;

    // Default order matches the class doc comment's listing — EnergyPriorityController can reorder this
    // live (e.g. from the player's drag-and-drop priority window).
    private readonly List<EnergyPriorityGroup> priorityOrder = new()
        { EnergyPriorityGroup.Movement, EnergyPriorityGroup.Weapons, EnergyPriorityGroup.Defense, EnergyPriorityGroup.Repair };

    private readonly Dictionary<EnergyPriorityGroup, float> budgetRemaining = new();
    private readonly Dictionary<EnergyPriorityGroup, float> spentLastFrame = new();
    private readonly Dictionary<EnergyPriorityGroup, float> spentThisFrame = new();

    public IReadOnlyList<EnergyPriorityGroup> PriorityOrder => priorityOrder;

    /// <summary>Reorders which system gets first claim on the pool — must contain all 4 groups exactly
    /// once (silently ignored otherwise, so a malformed drag-drop result can't corrupt the ordering).</summary>
    public void SetPriorityOrder(IEnumerable<EnergyPriorityGroup> order)
    {
        var list = order.ToList();
        if (list.Count != 4 || list.Distinct().Count() != 4) return;

        priorityOrder.Clear();
        priorityOrder.AddRange(list);
    }

    private void Awake() => grid = GetComponent<ShipGrid>();

    private void OnEnable()
    {
        grid.OnShipChanged += RecalculateMax;
        RecalculateMax();
        Current = Max; // start full
    }

    private void OnDisable() => grid.OnShipChanged -= RecalculateMax;

    private void Update()
    {
        float generation = grid.ComputeEnergyGeneration(); // existing stat — sum of every generator's Power Output
        if (generation > 0f) Add(generation * Time.deltaTime);

        RecomputeBudgets();
    }

    /// <summary>Slices Current into a per-group budget for this frame — see class doc comment for the
    /// approximation this relies on.</summary>
    private void RecomputeBudgets()
    {
        budgetRemaining.Clear();

        float reservedByHigherGroups = 0f;
        foreach (var group in priorityOrder)
        {
            budgetRemaining[group] = Mathf.Max(0f, Current - reservedByHigherGroups);
            reservedByHigherGroups += spentLastFrame.GetValueOrDefault(group);
        }

        spentLastFrame.Clear();
        foreach (var kv in spentThisFrame) spentLastFrame[kv.Key] = kv.Value;
        spentThisFrame.Clear();
    }

    private void RecalculateMax()
    {
        Max = GetComponentsInChildren<GeneratorModule>()
            .Where(g => !g.IsDestroyed)
            .Sum(g => g.capacity);

        if (Current > Max) Current = Max; // a destroyed generator can shrink the pool below what was stored
        OnEnergyChanged?.Invoke();
    }

    private void Add(float amount)
    {
        if (amount <= 0f) return;
        float clamped = Mathf.Min(Max, Current + amount);
        if (clamped == Current) return; // already full — don't spam the event every frame
        Current = clamped;
        OnEnergyChanged?.Invoke();
    }

    /// <summary>Spends energy if there's enough; returns false (and spends nothing) otherwise — same
    /// all-or-nothing convention as GameDataManager.SpendCredits/SpendCoins. Draws straight from the pool,
    /// ignoring priority entirely — prefer the (amount, group) overload for anything that should respect
    /// the player's priority ordering.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current < amount) return false;

        Current -= amount;
        OnEnergyChanged?.Invoke();
        return true;
    }

    /// <summary>Priority-aware spend: succeeds only if the pool AND this group's per-frame budget (see
    /// RecomputeBudgets) both cover it. A ship with no priority window ever opened just uses the default
    /// order (Movement, Weapons, Defense, Repair) — this still behaves correctly, it simply never gets
    /// reordered.</summary>
    public bool TrySpend(float amount, EnergyPriorityGroup group)
    {
        if (amount <= 0f) return true;
        if (Current < amount) return false;
        if (budgetRemaining.TryGetValue(group, out float budget) && budget < amount) return false;

        Current -= amount;
        if (budgetRemaining.ContainsKey(group)) budgetRemaining[group] -= amount;
        spentThisFrame[group] = spentThisFrame.GetValueOrDefault(group) + amount;

        OnEnergyChanged?.Invoke();
        return true;
    }
}
