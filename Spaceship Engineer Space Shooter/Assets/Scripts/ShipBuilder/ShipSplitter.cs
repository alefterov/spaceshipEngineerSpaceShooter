using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Breaks an enemy ship apart when destroyed blocks cut it in two. After blocks are lost, the remaining blocks
/// are sorted into groups that touch each other (cell edge to cell edge); the group with the cockpit stays the
/// ship (with no cockpit left, the biggest group does), and every other group tears loose as its own object — a
/// ShipFragment: a lifeless chunk that drifts toward the player's ship and slowly spins, can be shot piece by
/// piece (it counts as an enemy ship's blocks), rams the player's ship on contact and does not shoot.
///
/// Added to every EnemyShip by it (Splits Into Fragments); settings (drift speed, spin) live on EnemyShip.
/// </summary>
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

    // Subscribed only after the ship has been built (EnemyShip builds it in Awake) — a ship that was DESIGNED in
    // separate pieces would otherwise fall apart the moment it spawns.
    private void Start()
    {
        if (grid != null) grid.OnShipChanged += MarkDirty;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.OnShipChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    // Checked once per frame, after everything that destroyed blocks this frame has finished — not from inside
    // the destruction callbacks themselves.
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

        // The ship keeps the group with the cockpit, else the biggest one.
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

        // A cockpit cut off from every generator cannot work: it blows up (and the blast spreads to its neighbours).
        foreach (var cockpit in unpowered)
            if (cockpit != null && !cockpit.IsDestroyed) cockpit.TakeDamage(float.MaxValue);
    }

    /// <summary>Living blocks grouped by touching.</summary>
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
        // The root sits in the middle of its blocks, so it turns around them and aiming at it makes sense.
        Vector3 center = Vector3.zero;
        foreach (var block in blocks) center += block.transform.position;
        center /= blocks.Count;

        var go = new GameObject("EnemyFragment");
        go.transform.SetPositionAndRotation(center, transform.rotation);

        var identity = go.AddComponent<ShipIdentity>();
        identity.faction = Faction.Enemy;
        identity.ApplyTagToRoot(); // Awake already ran as Player when the component was added — set the real side

        foreach (var block in blocks)
        {
            block.transform.SetParent(go.transform, true); // stays exactly where it is in the world
            identity.RegisterBlock(block);
        }

        identity.SetCombatActive(false); // no crew: its weapons stay silent
        foreach (var engine in blocks.OfType<EngineModule>()) engine.SetEffectRunning(false);

        var fragment = go.AddComponent<ShipFragment>();
        fragment.Begin(enemy != null ? enemy.fragmentDriftSpeed : 0.5f, enemy != null ? enemy.fragmentMaxSpinDegrees : 30f,
                       enemy != null ? enemy.debrisDriftAngle : 20f, enemy != null ? enemy.debrisDespawnMargin : 2f);

        go.AddComponent<RamCollision>();
    }
}
