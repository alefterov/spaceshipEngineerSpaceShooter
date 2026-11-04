using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RepairModule : ShipModule
{
    [Header("Repair")]
    [Tooltip("Total HP restored per second, at full crew bonus — split evenly across however many " +
             "blocks are being worked on at once (see Max Simultaneous Targets).")]
    public float repairPerSecond = 5f;
    [Tooltip("Energy spent per point of HP restored.")]
    public float energyPerHp = 1f;
    [Tooltip("How many different damaged blocks this repair block can work on AT THE SAME TIME — the " +
             "default, 1, finishes one block (by type priority, see class doc comment) before starting " +
             "the next. Raising it spreads the same total Repair Per Second across more blocks at once " +
             "rather than adding extra output.")]
    [Min(1)] public int maxSimultaneousTargets = 1;

    private static readonly ModuleType[] RepairPriorityOrder =
    {
        ModuleType.Cockpit,
        ModuleType.Generator,
        ModuleType.Engine,
        ModuleType.Weapon,
        ModuleType.Shield,
        ModuleType.Armor,
        ModuleType.Repair,
    };

    private ShipGrid grid;
    private ShipEnergySystem energy;
    private ShipIdentity identity;

    protected override void Awake()
    {
        base.Awake();
        type = ModuleType.Repair;
        energyDelta = -Mathf.Abs(repairPerSecond * energyPerHp);

        grid = GetComponentInParent<ShipGrid>();
        energy = GetComponentInParent<ShipEnergySystem>();
        identity = GetComponentInParent<ShipIdentity>();
    }

    private void Update()
    {
        if (IsDestroyed || grid == null) return;

        var targets = FindRepairTargets(Mathf.Max(1, maxSimultaneousTargets));
        if (targets.Count == 0) return;

        float rate = repairPerSecond;
        if (identity != null && identity.faction == Faction.Player && GameDataManager.Instance != null)
            rate *= GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.RepairEfficiency);

        float wantHpEach = (rate / targets.Count) * Time.deltaTime;
        float costEach = wantHpEach * energyPerHp;

        foreach (var target in targets)
        {
            if (energy != null && !energy.TrySpend(costEach, ShipEnergySystem.EnergyPriorityGroup.Repair))
                continue;

            target.Heal(wantHpEach);
        }
    }

    private List<ShipModule> FindRepairTargets(int count)
    {
        var targets = new List<ShipModule>(count);

        foreach (var priorityType in RepairPriorityOrder)
        {
            var damagedOfThisType = grid.Blocks
                .Where(b => b != null && !b.IsDestroyed && b.type == priorityType && b.maxHP > 0f && b.CurrentHP < b.maxHP)
                .OrderBy(b => b.CurrentHP / b.maxHP);

            foreach (var block in damagedOfThisType)
            {
                targets.Add(block);
                if (targets.Count >= count) return targets;
            }
        }

        return targets;
    }
}
