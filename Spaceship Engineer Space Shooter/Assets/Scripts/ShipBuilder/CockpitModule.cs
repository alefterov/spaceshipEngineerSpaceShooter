using UnityEngine;

/// <summary>
/// Cockpit — a functional module like Engine/Generator/Shield/Weapon (sits on Hull cells,
/// moduleCells layer, no special structural treatment). Tagged with its own ModuleType so
/// ShipGrid.HasCockpit can find it (a bare ShipModule defaults to ModuleType.Hull and would
/// silently not count). The only thing that makes a cockpit special: the ship can't be saved
/// without at least one non-destroyed one — see ShipGrid.HasCockpit / MainMenuFlowController.
/// </summary>
public class CockpitModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Cockpit;
    }
}
