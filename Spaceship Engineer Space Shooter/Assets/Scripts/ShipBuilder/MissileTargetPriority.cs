using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Which kind of block a ship's missiles go for first — the order of the ModuleType categories, top = first. A
/// missile picks the NEAREST block of the first category in this list that still has a living block on the
/// other side; if there is none, it goes on to the next category. Same idea as the energy priority
/// (ShipEnergySystem), with its own window (MissilePriorityController).
///
/// On the PLAYER'S ship the order is remembered between battles (GameData.missileTargetPriority) and edited in
/// the window. On an enemy ship it is whatever is set in Default Order here (add the component to the enemy
/// prefab to choose; otherwise the built-in default order is used). Added to a ship automatically by its first
/// missile launcher.
/// </summary>
public class MissileTargetPriority : MonoBehaviour
{
    [Tooltip("Top = hit first. Any category left out is appended at the end, so every block type is always covered.")]
    [SerializeField] private List<ModuleType> defaultOrder = new()
    {
        ModuleType.Cockpit, ModuleType.Weapon, ModuleType.Generator, ModuleType.Shield,
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor,
    };

    public static readonly ModuleType[] DefaultCategories =
    {
        ModuleType.Cockpit, ModuleType.Weapon, ModuleType.Generator, ModuleType.Shield,
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor,
    };

    private List<ModuleType> order;

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
        var saved = IsPlayerShip && GameDataManager.Instance != null ? GameDataManager.Instance.SavedMissilePriority : null;
        if (saved != null) order = Normalize(saved.Select(i => (ModuleType)i));
    }

    /// <summary>Replaces the order (the player's is saved too).</summary>
    public void SetOrder(IEnumerable<ModuleType> newOrder)
    {
        order = Normalize(newOrder);
        if (IsPlayerShip && GameDataManager.Instance != null) GameDataManager.Instance.SaveMissilePriority(order);
    }

    /// <summary>No repeats, only known categories, and every default category present (missing ones last).</summary>
    private static List<ModuleType> Normalize(IEnumerable<ModuleType> source)
    {
        var result = new List<ModuleType>();
        foreach (var t in source)
            if (DefaultCategories.Contains(t) && !result.Contains(t)) result.Add(t);
        foreach (var t in DefaultCategories)
            if (!result.Contains(t)) result.Add(t);
        return result;
    }

    /// <summary>The priority component of the ship this object belongs to, added if the ship has none.</summary>
    public static MissileTargetPriority GetOrAdd(Component fromShipPart)
    {
        var existing = fromShipPart.GetComponentInParent<MissileTargetPriority>();
        if (existing != null) return existing;

        var identity = fromShipPart.GetComponentInParent<ShipIdentity>();
        var host = identity != null ? identity.gameObject : fromShipPart.gameObject;
        return host.AddComponent<MissileTargetPriority>();
    }
}

/// <summary>Finding blocks for missiles to aim at.</summary>
public static class MissileTargeting
{
    private static readonly List<ShipModule> scratch = new();

    /// <summary>The block a missile of `owner` should chase from `from`: the nearest living block of the first
    /// category in `order` that exists on the other side. Null if the other side has no blocks at all.</summary>
    public static ShipModule PickTarget(Faction owner, IReadOnlyList<ModuleType> order, Vector2 from)
    {
        CollectHostileBlocks(owner, scratch);
        if (scratch.Count == 0) return null;

        foreach (var category in order)
        {
            ShipModule best = null;
            float bestSqr = float.MaxValue;

            foreach (var block in scratch)
            {
                if (block.type != category) continue;

                float sqr = ((Vector2)block.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = block;
            }

            if (best != null) return best;
        }

        // A block whose type isn't in the list at all (shouldn't happen) — still better than nothing.
        return scratch.OrderBy(b => ((Vector2)b.transform.position - from).sqrMagnitude).First();
    }

    /// <summary>Whether any living block of the other side is within `range` of `from`.</summary>
    public static bool AnyHostileWithin(Faction owner, Vector2 from, float range)
    {
        float rangeSqr = range * range;
        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == owner) continue;

            foreach (var block in ship.LiveBlocks)
                if (block != null && !block.IsDestroyed && ((Vector2)block.transform.position - from).sqrMagnitude <= rangeSqr)
                    return true;
        }

        return false;
    }

    private static void CollectHostileBlocks(Faction owner, List<ShipModule> into)
    {
        into.Clear();
        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == owner) continue;

            foreach (var block in ship.LiveBlocks)
                if (block != null && !block.IsDestroyed) into.Add(block);
        }
    }

    /// <summary>The blocks touching `block` (sharing a cell edge), not counting the block itself.</summary>
    public static List<ShipModule> GetNeighbours(ShipModule block)
    {
        var result = new List<ShipModule>();
        var grid = block.GetComponentInParent<ShipGrid>();
        if (grid == null) return result;

        var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var cell in block.occupiedCells)
        {
            foreach (var offset in offsets)
            {
                var neighbour = grid.GetBlockAt(cell + offset);
                if (neighbour != null && neighbour != block && !neighbour.IsDestroyed && !result.Contains(neighbour))
                    result.Add(neighbour);
            }
        }

        return result;
    }
}
