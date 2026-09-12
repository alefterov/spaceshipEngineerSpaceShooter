using UnityEngine;

/// <summary>
/// Shield generator. A functional module (moduleCells layer, must sit on Hull), tagged with its own
/// ModuleType so ShipGrid.ComputeShieldStrength() can find it — a bare ShipModule defaults to
/// ModuleType.Hull and would silently be counted as armor/hull instead, not show up as shield
/// strength at all. Consumes energy like a weapon; maxHP is what ComputeShieldStrength reports.
/// </summary>
public class ShieldModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Shield;
        energyDelta = -Mathf.Abs(energyDelta); // shields draw power, same convention as WeaponModule
    }
}
