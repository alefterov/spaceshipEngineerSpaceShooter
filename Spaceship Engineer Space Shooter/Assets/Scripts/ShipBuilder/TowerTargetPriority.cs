using System.Collections.Generic;
using UnityEngine;

public class TowerTargetPriority : TargetPriority
{
    protected override IReadOnlyList<int> LoadSaved() => GameDataManager.Instance.SavedTowerPriority;

    protected override void SaveOrder(IEnumerable<ModuleType> newOrder) => GameDataManager.Instance.SaveTowerPriority(newOrder);

    public static TowerTargetPriority GetOrAdd(Component fromShipPart) => GetOrAdd<TowerTargetPriority>(fromShipPart);
}
