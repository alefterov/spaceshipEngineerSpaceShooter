using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Which kind of block a ship's missiles go for first — the order of the block categories (Cockpit, Weapon,
/// Generator, Shield, Engine, Repair, Armor, Meteor; Weapon means every kind of weapon), top = first. A
/// missile picks the NEAREST block of the first category in this list that still has a living block on the
/// other side; if there is none, it goes on to the next category, and if none of them exists any more (only
/// block types missing from the list) it simply chases the nearest block. Same idea as the energy priority
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
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor, ModuleType.Meteor,
    };

    public static readonly ModuleType[] DefaultCategories =
    {
        ModuleType.Cockpit, ModuleType.Weapon, ModuleType.Generator, ModuleType.Shield,
        ModuleType.Engine, ModuleType.Repair, ModuleType.Armor, ModuleType.Meteor,
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

    /// <summary>What a missile of `owner` should chase from `from`. Goes through `order` top to bottom: a block
    /// category picks the NEAREST living block of that type on the other side (only if ships are among the
    /// allowed targets), the Meteor category the nearest hostile meteor (only if meteors are allowed). The first
    /// category with something to hit wins. If none has, the nearest block of any type, else the nearest
    /// allowed object. Exactly one of the outputs is set (or neither, when there is nothing at all).</summary>
    public static void PickTarget(Faction owner, IReadOnlyList<ModuleType> order, IReadOnlyList<TargetKind> allowed,
        Vector2 from, out ShipModule block, out Targetable obj)
    {
        block = null;
        obj = null;

        bool ships = Contains(allowed, TargetKind.Ship);
        bool meteors = Contains(allowed, TargetKind.Meteor);

        if (ships) CollectHostileBlocks(owner, scratch);
        else scratch.Clear();

        foreach (var category in order)
        {
            if (category == ModuleType.Meteor)
            {
                if (!meteors) continue;

                obj = NearestOfKind(owner, TargetKind.Meteor, from);
                if (obj != null) return;
                continue;
            }

            ShipModule best = null;
            float bestSqr = float.MaxValue;

            foreach (var candidate in scratch)
            {
                if (candidate.type != category) continue;

                float sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = candidate;
            }

            if (best != null)
            {
                block = best;
                return;
            }
        }

        // Nothing in the list applies (or no category matched a block that exists): the nearest block, else the
        // nearest object of an allowed kind.
        if (scratch.Count > 0)
        {
            block = scratch.OrderBy(b => ((Vector2)b.transform.position - from).sqrMagnitude).First();
            return;
        }

        obj = NearestTargetable(owner, allowed, from, float.MaxValue);
    }

    private static bool Contains(IReadOnlyList<TargetKind> allowed, TargetKind kind)
    {
        foreach (var k in allowed) if (k == kind) return true;
        return false;
    }

    private static Targetable NearestOfKind(Faction owner, TargetKind kind, Vector2 from)
    {
        Targetable best = null;
        float bestSqr = float.MaxValue;

        foreach (var candidate in Targetable.Active)
        {
            if (candidate == null || candidate.IsDestroyed || candidate.kind != kind) continue;
            if (kind != TargetKind.Meteor && candidate.faction == owner) continue;

            float sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
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

    /// <summary>Whether a missile of `owner` that may engage `allowed` kinds may hit / chase this object: not a
    /// ship (ships are hit block by block), not already destroyed, of an allowed kind, and hostile — meteors are
    /// hostile to everyone, everything else only when it belongs to the other side.</summary>
    public static bool IsValidObjectTarget(Targetable candidate, Faction owner, IReadOnlyList<TargetKind> allowed)
    {
        if (candidate == null || candidate.IsDestroyed || candidate.kind == TargetKind.Ship) return false;

        bool kindAllowed = false;
        foreach (var kind in allowed) if (kind == candidate.kind) { kindAllowed = true; break; }
        if (!kindAllowed) return false;

        return candidate.kind == TargetKind.Meteor || candidate.faction != owner;
    }

    /// <summary>The nearest meteor / torpedo / etc. (never a ship) a missile of `owner` may engage within `range`.</summary>
    public static Targetable NearestTargetable(Faction owner, IReadOnlyList<TargetKind> allowed, Vector2 from, float range)
    {
        Targetable best = null;
        float bestSqr = range >= float.MaxValue ? float.MaxValue : range * range;

        foreach (var candidate in Targetable.Active)
        {
            if (!IsValidObjectTarget(candidate, owner, allowed)) continue;

            float sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }

    /// <summary>The living blocks touching `block` (sharing a cell edge) on its ship or fragment, not counting the
    /// block itself. Works from the blocks' own cells, so it needs no grid — a broken-off fragment has none.</summary>
    public static List<ShipModule> GetNeighbours(ShipModule block)
    {
        var result = new List<ShipModule>();
        var ship = block.GetComponentInParent<ShipIdentity>();
        if (ship == null) return result;

        var cells = new HashSet<Vector2Int>(block.occupiedCells);
        var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var other in ship.LiveBlocks)
        {
            if (other == null || other == block || other.IsDestroyed) continue;

            bool touching = false;
            foreach (var cell in other.occupiedCells)
            {
                foreach (var offset in offsets)
                    if (cells.Contains(cell + offset)) { touching = true; break; }
                if (touching) break;
            }

            if (touching) result.Add(other);
        }

        return result;
    }
}
