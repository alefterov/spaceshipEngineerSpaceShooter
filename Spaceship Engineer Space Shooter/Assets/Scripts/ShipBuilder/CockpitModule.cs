using UnityEngine;

public class CockpitModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Cockpit;
    }
}
