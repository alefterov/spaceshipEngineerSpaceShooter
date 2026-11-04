using UnityEngine;

public class ArmorModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Armor;
    }
}
