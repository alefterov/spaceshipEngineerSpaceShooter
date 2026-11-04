using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShipSplitter : MonoBehaviour
{
    private ShipGrid grid;
    private EnemyShip enemy;
    private bool dirty;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        enemy = GetComponent<EnemyShip>();
    }

    private void Start()
    {
        if (grid != null) grid.OnShipChanged += MarkDirty;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.OnShipChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;

        Split();
    }

    private void Split()
    {
        var groups = FindGroups();
        if (groups.Count <= 1) return;

        var main = groups.FirstOrDefault(g => g.Any(b => b.type == ModuleType.Cockpit)) ?? groups.OrderByDescending(g => g.Count).First();

        var unpowered = groups
            .Where(g => !g.Any(b => b.type == ModuleType.Generator))
            .SelectMany(g => g.Where(b => b.type == ModuleType.Cockpit))
            .ToList();

        foreach (var group in groups)
        {
            if (group == main) continue;

            foreach (var block in group) grid.ReleaseBlock(block);
            CreateFragment(group);
        }

        grid.NotifyBlocksReleased();

        foreach (var cockpit in unpowered)
            if (cockpit != null && !cockpit.IsDestroyed) cockpit.TakeDamage(float.MaxValue);
    }

    private List<List<ShipModule>> FindGroups()
    {
        var alive = grid.Blocks.Where(b => b != null && !b.IsDestroyed).ToList();
        var visited = new HashSet<ShipModule>();
        var groups = new List<List<ShipModule>>();
        var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var start in alive)
        {
            if (visited.Contains(start)) continue;

            var group = new List<ShipModule>();
            var queue = new Queue<ShipModule>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var block = queue.Dequeue();
                group.Add(block);

                foreach (var cell in block.occupiedCells)
                {
                    foreach (var offset in offsets)
                    {
                        var neighbour = grid.GetBlockAt(cell + offset);
                        if (neighbour == null || neighbour.IsDestroyed || !visited.Add(neighbour)) continue;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private void CreateFragment(List<ShipModule> blocks)
    {
        Vector3 center = Vector3.zero;
        foreach (var block in blocks) center += block.transform.position;
        center /= blocks.Count;

        var go = new GameObject("EnemyFragment");
        go.transform.SetPositionAndRotation(center, transform.rotation);

        go.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = EnemyShip.FragmentSortingOrder;

        var identity = go.AddComponent<ShipIdentity>();
        identity.faction = Faction.Enemy;
        identity.ApplyTagToRoot();

        foreach (var block in blocks)
        {
            block.transform.SetParent(go.transform, true);
            identity.RegisterBlock(block);
        }

        identity.SetCombatActive(false);
        foreach (var engine in blocks.OfType<EngineModule>()) engine.SetEffectRunning(false);

        var fragment = go.AddComponent<ShipFragment>();
        fragment.Begin(enemy != null ? enemy.fragmentDriftSpeed : 0.5f, enemy != null ? enemy.fragmentMaxSpinDegrees : 30f,
                       enemy != null ? enemy.debrisDriftAngle : 20f, enemy != null ? enemy.debrisDespawnMargin : 2f);

        go.AddComponent<RamCollision>();
    }
}
