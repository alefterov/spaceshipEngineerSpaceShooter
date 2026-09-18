using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// The ship's live energy pool — current charge vs max capacity. Max is the sum of every surviving
/// Generator's own Capacity (see GeneratorModule.capacity — a separate concept from Power Output,
/// which is the generation RATE that refills this pool over time). Losing a generator shrinks the
/// pool immediately, trimming any stored energy above the new max.
///
/// SETUP: put on the ship root, alongside ShipGrid/ShipIdentity. Whatever spends energy (ShipMovement
/// for now) calls TrySpend directly — this component only tracks the pool, it doesn't know who's
/// drawing from it.
/// </summary>
[RequireComponent(typeof(ShipGrid))]
public class ShipEnergySystem : MonoBehaviour
{
    public float Current { get; private set; }
    public float Max { get; private set; }

    /// <summary>Fired whenever Current or Max changes — UI gauges refresh on this.</summary>
    public event Action OnEnergyChanged;

    private ShipGrid grid;

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
    /// all-or-nothing convention as GameDataManager.SpendCredits/SpendCoins.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current < amount) return false;

        Current -= amount;
        OnEnergyChanged?.Invoke();
        return true;
    }
}
