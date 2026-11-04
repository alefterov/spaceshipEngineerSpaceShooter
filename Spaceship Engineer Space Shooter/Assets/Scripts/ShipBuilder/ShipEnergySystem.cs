using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(ShipGrid))]
public class ShipEnergySystem : MonoBehaviour
{
    public enum EnergyPriorityGroup { Movement, Weapons, Defense, Repair }

    public float Current { get; private set; }
    public float Max { get; private set; }

    public event Action OnEnergyChanged;

    private ShipGrid grid;

    private readonly List<EnergyPriorityGroup> priorityOrder = new()
        { EnergyPriorityGroup.Movement, EnergyPriorityGroup.Weapons, EnergyPriorityGroup.Defense, EnergyPriorityGroup.Repair };

    private readonly Dictionary<EnergyPriorityGroup, float> budgetRemaining = new();
    private readonly Dictionary<EnergyPriorityGroup, float> spentLastFrame = new();
    private readonly Dictionary<EnergyPriorityGroup, float> spentThisFrame = new();

    public IReadOnlyList<EnergyPriorityGroup> PriorityOrder => priorityOrder;

    public void SetPriorityOrder(IEnumerable<EnergyPriorityGroup> order)
    {
        var list = order.ToList();
        if (list.Count != 4 || list.Distinct().Count() != 4) return;

        priorityOrder.Clear();
        priorityOrder.AddRange(list);
    }

    private ShipIdentity identity;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        identity = GetComponent<ShipIdentity>();
    }

    private void OnEnable()
    {
        grid.OnShipChanged += RecalculateMax;
        RecalculateMax();
        Current = Max;
    }

    private void OnDisable() => grid.OnShipChanged -= RecalculateMax;

    private void Update()
    {
        float generation = grid.ComputeEnergyGeneration();
        if (identity != null && identity.faction == Faction.Player && GameDataManager.Instance != null)
            generation *= GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.GeneratorEfficiency);
        if (generation > 0f) Add(generation * Time.deltaTime);

        RecomputeBudgets();
    }

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

        if (Current > Max) Current = Max;
        OnEnergyChanged?.Invoke();
    }

    private void Add(float amount)
    {
        if (amount <= 0f) return;
        float clamped = Mathf.Min(Max, Current + amount);
        if (clamped == Current) return;
        Current = clamped;
        OnEnergyChanged?.Invoke();
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current < amount) return false;

        Current -= amount;
        OnEnergyChanged?.Invoke();
        return true;
    }

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
