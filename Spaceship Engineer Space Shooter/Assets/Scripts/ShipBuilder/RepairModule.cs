using UnityEngine;

/// <summary>
/// Repair block. A functional module like Generator/Shield — same placement rules, same per-cell HP —
/// but while alive it continuously heals the single most-damaged surviving block on the WHOLE ship
/// (itself included), spending energy from ShipEnergySystem to do it. Never targets a destroyed block
/// (that's gone — a repair block can't rebuild it) and never overheals past a block's own maxHP.
///
/// Its own separate research branch (Category = Repair on both the BlockDefinition and its unlocking
/// TechDefinition) — set that up in the tech tree like any other block. The crew Engineer's own repair
/// bonus (GameDataManager.GetEngineerRepairMultiplier) scales Repair Per Second for the player's ships
/// only, same as the Engineer's shield/max-HP bonuses elsewhere.
/// </summary>
public class RepairModule : ShipModule
{
    [Header("Repair")]
    [Tooltip("HP restored per second to the most-damaged block on the ship, at full crew bonus.")]
    public float repairPerSecond = 5f;
    [Tooltip("Energy spent per point of HP restored.")]
    public float energyPerHp = 1f;

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

        var target = FindMostDamagedBlock();
        if (target == null) return; // nothing to do — every surviving block is already full

        float rate = repairPerSecond;
        if (identity != null && identity.faction == Faction.Player && GameDataManager.Instance != null)
            rate *= GameDataManager.Instance.GetEngineerRepairMultiplier();

        float wantHp = rate * Time.deltaTime;
        float cost = wantHp * energyPerHp;

        if (energy != null && !energy.TrySpend(cost, ShipEnergySystem.EnergyPriorityGroup.Repair))
            return; // no power for it this frame — tries again next frame

        target.Heal(wantHp);
    }

    /// <summary>The single most-damaged (but not destroyed) block on the whole ship, or null if every
    /// surviving block is already at full HP.</summary>
    private ShipModule FindMostDamagedBlock()
    {
        ShipModule worst = null;
        float worstRatio = 1f;

        foreach (var block in grid.Blocks)
        {
            if (block == null || block.IsDestroyed || block.maxHP <= 0f) continue;

            float ratio = block.CurrentHP / block.maxHP;
            if (ratio >= 1f) continue;

            if (ratio < worstRatio)
            {
                worstRatio = ratio;
                worst = block;
            }
        }

        return worst;
    }
}
