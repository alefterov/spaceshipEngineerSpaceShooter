using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The order of block categories a kind of weapon goes for first (Cockpit, Weapon, Generator, Shield, Engine, Repair, Armor, Meteor;
/// Weapon means every kind of weapon), top = first. It picks the NEAREST block of the first category in this list that still has a
/// living block on the other side; if there is none it goes on to the next category, and if none of them exists any more it simply
/// takes the nearest thing. Two kinds exist: the missiles' (MissileTargetPriority) and the tower guns' (TowerTargetPriority).
///
/// On the PLAYER'S ship the order is remembered between battles and edited in the priorities window (ShipPrioritiesWindow). On an
/// enemy ship it is whatever is set in Default Order (add the component to the enemy prefab to choose; otherwise the built-in default
/// order is used).
/// </summary>
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

    /// <summary>The player's remembered order (ModuleType values), or null if it was never changed.</summary>
    protected abstract IReadOnlyList<int> LoadSaved();

    /// <summary>Remembers the player's order.</summary>
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

        // The player's choice persists; an enemy ship just uses its prefab's Default Order.
        var saved = IsPlayerShip && GameDataManager.Instance != null ? LoadSaved() : null;
        if (saved != null) order = Normalize(saved.Select(i => (ModuleType)i));
    }

    /// <summary>Replaces the order (the player's is saved too).</summary>
    public void SetOrder(IEnumerable<ModuleType> newOrder)
    {
        order = Normalize(newOrder);
        if (IsPlayerShip && GameDataManager.Instance != null) SaveOrder(order);
    }

    /// <summary>No repeats, only known categories, and every default category present (missing ones last).</summary>
    protected static List<ModuleType> Normalize(IEnumerable<ModuleType> source)
    {
        var result = new List<ModuleType>();
        foreach (var t in source)
            if (DefaultCategories.Contains(t) && !result.Contains(t)) result.Add(t);
        foreach (var t in DefaultCategories)
            if (!result.Contains(t)) result.Add(t);
        return result;
    }

    /// <summary>The priority component of kind T on the ship this object belongs to, added if the ship has none.</summary>
    public static T GetOrAdd<T>(Component fromShipPart) where T : TargetPriority
    {
        var existing = fromShipPart.GetComponentInParent<T>();
        if (existing != null) return existing;

        var identity = fromShipPart.GetComponentInParent<ShipIdentity>();
        var host = identity != null ? identity.gameObject : fromShipPart.gameObject;
        return host.AddComponent<T>();
    }
}
