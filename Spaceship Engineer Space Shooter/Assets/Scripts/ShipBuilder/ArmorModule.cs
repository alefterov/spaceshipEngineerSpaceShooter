using UnityEngine;

/// <summary>
/// Armor plating. Structural just like Hull — same hullCells layer, same adjacency rule — but
/// tagged with its own ModuleType so it can be told apart from Hull at runtime. In particular,
/// HullOutlineRenderer only traces ModuleType.Hull cells, so armor prefabs must use THIS component
/// (not a bare ShipModule, which defaults to ModuleType.Hull) to be correctly excluded from the
/// ship's outline contour.
/// </summary>
public class ArmorModule : ShipModule
{
    private void Awake()
    {
        base.Awake();
        type = ModuleType.Armor;
    }
}
