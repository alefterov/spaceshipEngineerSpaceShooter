using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MissileTargetPriority : TargetPriority
{
    protected override IReadOnlyList<int> LoadSaved() => GameDataManager.Instance.SavedMissilePriority;

    protected override void SaveOrder(IEnumerable<ModuleType> newOrder) => GameDataManager.Instance.SaveMissilePriority(newOrder);

    public static MissileTargetPriority GetOrAdd(Component fromShipPart) => GetOrAdd<MissileTargetPriority>(fromShipPart);
}

public static class MissileTargeting
{
    private static readonly List<ShipModule> scratch = new();

    public static void PickTarget(Faction owner, IReadOnlyList<ModuleType> order, IReadOnlyList<TargetKind> allowed,
        Vector2 from, out ShipModule block, out Targetable obj)
    {
        PickTarget(owner, order, allowed, from, true, out block, out obj);
        if (block == null && obj == null) PickTarget(owner, order, allowed, from, false, out block, out obj);
    }

    private static void PickTarget(Faction owner, IReadOnlyList<ModuleType> order, IReadOnlyList<TargetKind> allowed,
        Vector2 from, bool zoneOnly, out ShipModule block, out Targetable obj)
    {
        block = null;
        obj = null;

        bool ships = Contains(allowed, TargetKind.Ship);
        bool meteors = Contains(allowed, TargetKind.Meteor);

        if (ships) CollectHostileBlocks(owner, scratch, zoneOnly);
        else scratch.Clear();

        foreach (var category in order)
        {
            if (category == ModuleType.Meteor)
            {
                if (!meteors) continue;

                obj = NearestOfKind(owner, TargetKind.Meteor, from, zoneOnly);
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

        if (scratch.Count > 0)
        {
            block = scratch.OrderBy(b => ((Vector2)b.transform.position - from).sqrMagnitude).First();
            return;
        }

        obj = NearestTargetable(owner, allowed, from, float.MaxValue, zoneOnly);
    }

    private static bool Contains(IReadOnlyList<TargetKind> allowed, TargetKind kind)
    {
        foreach (var k in allowed) if (k == kind) return true;
        return false;
    }

    private static Targetable NearestOfKind(Faction owner, TargetKind kind, Vector2 from, bool zoneOnly)
    {
        Targetable best = null;
        float bestSqr = float.MaxValue;

        foreach (var candidate in Targetable.Active)
        {
            if (candidate == null || candidate.IsDestroyed || candidate.kind != kind) continue;
            if (kind != TargetKind.Meteor && candidate.faction == owner) continue;
            if (zoneOnly && !BattleZone.PointInZone(candidate.transform.position)) continue;

            float sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }

    public static bool AnyHostileWithin(Faction owner, Vector2 from, float range)
    {
        float rangeSqr = range * range;
        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == owner) continue;

            foreach (var block in ship.LiveBlocks)
                if (block != null && !block.IsDestroyed && ((Vector2)block.transform.position - from).sqrMagnitude <= rangeSqr
                    && BattleZone.BlockInZone(block.WorldBounds))
                    return true;
        }

        return false;
    }

    private static void CollectHostileBlocks(Faction owner, List<ShipModule> into, bool zoneOnly)
    {
        into.Clear();
        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction == owner) continue;

            foreach (var block in ship.LiveBlocks)
                if (block != null && !block.IsDestroyed && (!zoneOnly || BattleZone.BlockInZone(block.WorldBounds))) into.Add(block);
        }
    }

    public static bool IsValidObjectTarget(Targetable candidate, Faction owner, IReadOnlyList<TargetKind> allowed)
    {
        if (candidate == null || candidate.IsDestroyed || candidate.kind == TargetKind.Ship) return false;

        bool kindAllowed = false;
        foreach (var kind in allowed) if (kind == candidate.kind) { kindAllowed = true; break; }
        if (!kindAllowed) return false;

        return candidate.kind == TargetKind.Meteor || candidate.faction != owner;
    }

    public static Targetable NearestTargetable(Faction owner, IReadOnlyList<TargetKind> allowed, Vector2 from, float range, bool zoneOnly = false)
    {
        Targetable best = null;
        float bestSqr = range >= float.MaxValue ? float.MaxValue : range * range;

        foreach (var candidate in Targetable.Active)
        {
            if (!IsValidObjectTarget(candidate, owner, allowed)) continue;
            if (zoneOnly && !BattleZone.PointInZone(candidate.transform.position)) continue;

            float sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = candidate;
        }

        return best;
    }

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
