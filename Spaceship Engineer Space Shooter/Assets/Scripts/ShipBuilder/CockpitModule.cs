using UnityEngine;

/// <summary>
/// Cockpit — a block like any other (own HP, same placement rules). Tagged with its own ModuleType so
/// ShipGrid.HasCockpit can find it. The only thing that makes a cockpit special: the ship can't be
/// saved without at least one non-destroyed one — see ShipGrid.HasCockpit / MainMenuFlowController —
/// and losing the last one disables the ship in battle.
/// </summary>
public class CockpitModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Cockpit;
    }
}
