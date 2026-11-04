using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class TargetPriority : MonoBehaviour
{
    [Tooltip("Top = hit first. Any category left out is appended at the end, so every block type is always covered.")]
    [SerializeField] protected List<ModuleType> defaultOrder = new()
    {
        ModuleType.Cockpit, ModuleType.Weapon, ModuleType.Generator, ModuleType.Shield,
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor, ModuleType.Meteor,
    };

    public static readonly ModuleType[] DefaultCategories =
    {
        ModuleType.Cockpit, ModuleType.Weapon, ModuleType.Generator, ModuleType.Shield,
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor, ModuleType.Meteor,
    };

    private List<ModuleType> order;

    protected abstract IReadOnlyList<int> LoadSaved();

    protected abstract void SaveOrder(IEnumerable<ModuleType> newOrder);

    public IReadOnlyList<ModuleType> Order
    {
        get
        {
            if (order == null) Load();
            return order;
        }
    }

    private bool IsPlayerShip
    {
        get
        {
            var identity = GetComponent<ShipIdentity>();
            return identity == null || identity.faction == Faction.Player;
        }
    }

    private void Load()
    {
        order = Normalize(defaultOrder);

        var saved = IsPlayerShip && GameDataManager.Instance != null ? LoadSaved() : null;
        if (saved != null) order = Normalize(saved.Select(i => (ModuleType)i));
    }

    public void SetOrder(IEnumerable<ModuleType> newOrder)
    {
        order = Normalize(newOrder);
        if (IsPlayerShip && GameDataManager.Instance != null) SaveOrder(order);
    }

    protected static List<ModuleType> Normalize(IEnumerable<ModuleType> source)
    {
        var result = new List<ModuleType>();
        foreach (var t in source)
            if (DefaultCategories.Contains(t) && !result.Contains(t)) result.Add(t);
        foreach (var t in DefaultCategories)
            if (!result.Contains(t)) result.Add(t);
        return result;
    }

    public static T GetOrAdd<T>(Component fromShipPart) where T : TargetPriority
    {
        var existing = fromShipPart.GetComponentInParent<T>();
        if (existing != null) return existing;

        var identity = fromShipPart.GetComponentInParent<ShipIdentity>();
        var host = identity != null ? identity.gameObject : fromShipPart.gameObject;
        return host.AddComponent<T>();
    }
}
