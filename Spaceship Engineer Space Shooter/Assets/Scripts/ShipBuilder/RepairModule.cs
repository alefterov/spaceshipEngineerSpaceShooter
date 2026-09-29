using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Repair block. A functional module like Generator/Shield — same placement rules, same per-cell HP —
/// but while alive it continuously heals damaged blocks elsewhere on the ship, spending energy from
/// ShipEnergySystem to do it. Never targets a destroyed block (that's gone — a repair block can't
/// rebuild it) and never overheals past a block's own maxHP.
///
/// TARGETING is by BLOCK TYPE priority, not "whoever is most hurt" — see RepairPriorityOrder: Cockpit
/// first, then Generator, then Engine, then Weapon, then Shield, then Armor, and Repair blocks (itself
/// included) dead last. Within one tier, the most-damaged block of that type goes first. Max Simultaneous
/// Targets (default 1) controls how many DIFFERENT blocks this one repair block can work on at once —
/// Repair Per Second is split evenly across however many it's actively working on this frame, so raising
/// this doesn't give extra total output for free, just spreads the same crew thinner across more patients.
///
/// Its own separate research branch (Category = Repair on both the BlockDefinition and its unlocking
/// TechDefinition) — set that up in the tech tree like any other block. The crew Engineer's own repair
/// bonus (GameDataManager.GetEngineerRepairMultiplier) scales Repair Per Second for the player's ships
/// only, same as the Engineer's shield/max-HP bonuses elsewhere.
/// </summary>
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

    // Which BLOCK TYPE gets fixed first when several different ones are damaged — see class doc comment.
    // Repair itself is always last, on purpose (a repair crew fixes everything else before its own bay).
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
        energyDelta = -Mathf.Abs(repairPerSecond * energyPerHp); // stats-panel estimate at full rate

        grid = GetComponentInParent<ShipGrid>();
        energy = GetComponentInParent<ShipEnergySystem>();
        identity = GetComponentInParent<ShipIdentity>();
    }

    private void Update()
    {
        if (IsDestroyed || grid == null) return;

        var targets = FindRepairTargets(Mathf.Max(1, maxSimultaneousTargets));
        if (targets.Count == 0) return; // nothing to do — every surviving block is already full

        float rate = repairPerSecond;
        if (identity != null && identity.faction == Faction.Player && GameDataManager.Instance != null)
            rate *= GameDataManager.Instance.GetEngineerRepairMultiplier();

        // Shared across whichever blocks actually got picked this frame — fewer targets than the max
        // (e.g. only 1 damaged block exists) means that one gets the FULL rate, not a diluted share.
        float wantHpEach = (rate / targets.Count) * Time.deltaTime;
        float costEach = wantHpEach * energyPerHp;

        foreach (var target in targets)
        {
            if (energy != null && !energy.TrySpend(costEach, ShipEnergySystem.EnergyPriorityGroup.Repair))
                continue; // no power for this one right now — the others still get theirs

            target.Heal(wantHpEach);
        }
    }

    /// <summary>Up to `count` damaged (not destroyed) blocks, filled in BLOCK-TYPE priority order — every
    /// eligible Cockpit before any Generator, every Generator before any Weapon, and so on (see
    /// RepairPriorityOrder). Within one type, the most-damaged block goes first.</summary>
    private List<ShipModule> FindRepairTargets(int count)
    {
        var targets = new List<ShipModule>(count);

        foreach (var priorityType in RepairPriorityOrder)
        {
            var damagedOfThisType = grid.Blocks
                .Where(b => b != null && !b.IsDestroyed && b.type == priorityType && b.maxHP > 0f && b.CurrentHP < b.maxHP)
                .OrderBy(b => b.CurrentHP / b.maxHP); // most-damaged (lowest ratio) first within this tier

            foreach (var block in damagedOfThisType)
            {
                targets.Add(block);
                if (targets.Count >= count) return targets;
            }
        }

        return targets;
    }
}
