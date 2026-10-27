using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which kind of block a ship's TOWER GUNS (heavy rotating ballistic guns — see WeaponModule.IsTowerGun) shoot at first. The same list
/// as the missiles' target priority (TargetPriority), kept separately, with its own tab in the priorities window. Added to the ship
/// automatically by its first tower gun.
/// </summary>
public class TowerTargetPriority : TargetPriority
{
    protected override IReadOnlyList<int> LoadSaved() => GameDataManager.Instance.SavedTowerPriority;

    protected override void SaveOrder(IEnumerable<ModuleType> newOrder) => GameDataManager.Instance.SaveTowerPriority(newOrder);

    public static TowerTargetPriority GetOrAdd(Component fromShipPart) => GetOrAdd<TowerTargetPriority>(fromShipPart);
}
