using UnityEngine;

/// <summary>
/// Armor plating. A plain block whose only job is soaking up damage with its own HP pool — tagged with
/// its own ModuleType so ShipGrid.ComputeTotalArmor / ComputeCurrentArmorHP can find it. Armor prefabs
/// should use THIS component rather than a bare ShipModule.
/// </summary>
public class ArmorModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Armor;
    }
}
