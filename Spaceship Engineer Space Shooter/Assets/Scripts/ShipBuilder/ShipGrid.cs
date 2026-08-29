using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BuildMode { Hull, Modules }

/// <summary>
/// Grid the ship is built on. Two independent layers:
///  - hullCells: structural pieces (Hull/Armor) — must always be adjacent to another hull piece.
///  - moduleCells: functional pieces (Weapon/Engine/Generator/Shield) — must sit entirely
///    on top of already-placed hull cells (so a module can never stick out past the hull's shape).
/// Losing a hull cell destroys any module sitting on it (cascade).
/// </summary>
[RequireComponent(typeof(ShipIdentity))]
public class ShipGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 1f;

    private readonly Dictionary<Vector2Int, ShipModule> hullCells = new();
    private readonly Dictionary<Vector2Int, ShipModule> moduleCells = new();

    [Tooltip("Preview = closed look (main menu), Building = exposed internals (editor). " +
             "Applied to every module immediately on placement.")]
    public ShipViewMode CurrentViewMode { get; private set; } = ShipViewMode.Building;

    private ShipIdentity identity;
    private void Awake() => identity = GetComponent<ShipIdentity>();

    // ---------- Coordinate helpers ----------

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

    public Vector2Int WorldToGrid(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        int x = Mathf.FloorToInt(local.x / cellSize + width * 0.5f);
        int y = Mathf.FloorToInt(local.y / cellSize + height * 0.5f);
        return new Vector2Int(x, y);
    }

    /// <summary>World-space center point of a (possibly multi-cell) shape anchored at `anchor`.</summary>
    public Vector3 GridToWorld(Vector2Int anchor, List<Vector2Int> shapeCells)
    {
        var bounds = ShapeBounds(shapeCells);
        Vector2 center = (Vector2)anchor + (Vector2)(bounds.min + bounds.max + Vector2Int.one) * 0.5f;
        float x = (center.x - width * 0.5f) * cellSize;
        float y = (center.y - height * 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    private static RectInt ShapeBounds(List<Vector2Int> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    private static List<Vector2Int> Offset(Vector2Int anchor, List<Vector2Int> local)
        => local.Select(c => anchor + c).ToList();

    // ---------- Validation ----------

    /// <summary>Hull mode rule: cells must be free & in bounds, and adjacent to existing hull (unless ship is empty).</summary>
    public bool CanPlaceHull(Vector2Int anchor, List<Vector2Int> localShape)
    {
        var cells = Offset(anchor, localShape);

        foreach (var cell in cells)
        {
            if (!InBounds(cell)) return false;
            if (hullCells.ContainsKey(cell)) return false;
        }

        if (hullCells.Count == 0) return true; // first block can go anywhere

        foreach (var cell in cells)
            foreach (var n in Neighbors(cell))
                if (hullCells.ContainsKey(n)) return true;

        return false;
    }

    /// <summary>Module mode rule: every target cell must already be covered by hull, and not already have a module.</summary>
    public bool CanPlaceModule(Vector2Int anchor, List<Vector2Int> localShape)
    {
        var cells = Offset(anchor, localShape);

        foreach (var cell in cells)
        {
            if (!hullCells.ContainsKey(cell)) return false;   // would stick out past the hull
            if (moduleCells.ContainsKey(cell)) return false;  // cell already has a module
        }
        return true;
    }

    private static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return c + Vector2Int.up;
        yield return c + Vector2Int.down;
        yield return c + Vector2Int.left;
        yield return c + Vector2Int.right;
    }

    // ---------- Placement ----------

    public ShipModule PlaceHull(GameObject prefab, Vector2Int anchor, List<Vector2Int> localShape)
        => Place(prefab, anchor, localShape, hullCells, registerWithIdentity: true);

    public ShipModule PlaceModule(GameObject prefab, Vector2Int anchor, List<Vector2Int> localShape)
        => Place(prefab, anchor, localShape, moduleCells, registerWithIdentity: false);

    private ShipModule Place(GameObject prefab, Vector2Int anchor, List<Vector2Int> localShape,
                              Dictionary<Vector2Int, ShipModule> layer, bool registerWithIdentity)
    {
        var cells = Offset(anchor, localShape);
        Vector3 worldPos = GridToWorld(anchor, localShape);

        var instance = Instantiate(prefab, worldPos, Quaternion.identity, transform);
        instance.tag = identity.faction == Faction.Player ? "PlayerShip" : "EnemyShip";

        var module = instance.GetComponent<ShipModule>();
        if (module == null)
        {
            Debug.LogError($"Block prefab '{prefab.name}' has no ShipModule component.");
            Destroy(instance);
            return null;
        }

        module.occupiedCells = cells;
        module.ApplyViewMode(CurrentViewMode);
        foreach (var cell in cells) layer[cell] = module;

        if (registerWithIdentity) identity.RegisterHull(module);
        else module.OnDestroyed += _ => moduleCells.Keys
                .Where(k => moduleCells[k] == module).ToList()
                .ForEach(k => moduleCells.Remove(k));

        return module;
    }

    /// <summary>Removes a hull piece and cascades: any module(s) sitting on its cells are destroyed too.</summary>
    public void RemoveHull(ShipModule hull)
    {
        var affectedModules = hull.occupiedCells
            .Where(moduleCells.ContainsKey)
            .Select(c => moduleCells[c])
            .Distinct()
            .ToList();

        foreach (var m in affectedModules)
            if (!m.IsDestroyed) m.TakeDamage(999999f); // force-destroy: hull under it is gone

        foreach (var cell in hull.occupiedCells) hullCells.Remove(cell);
        Destroy(hull.gameObject);
    }

    // ---------- View mode (menu preview vs builder) ----------

    /// <summary>Switches every currently placed block between the closed preview look and the exposed builder look.</summary>
    public void SetViewMode(ShipViewMode mode)
    {
        CurrentViewMode = mode;

        foreach (var m in hullCells.Values.Distinct()) m.ApplyViewMode(mode);
        foreach (var m in moduleCells.Values.Distinct()) m.ApplyViewMode(mode);
    }

    // ---------- Aggregate stats ----------

    public float ComputeTotalMass()
        => hullCells.Values.Distinct().Sum(m => m.mass) + moduleCells.Values.Distinct().Sum(m => m.mass);

    public float ComputeEnergyBalance()
        => moduleCells.Values.Distinct().Where(m => !m.IsDestroyed).Sum(m => m.energyDelta);

    // ---------- Save / load / procedural spawn (shared by player editor & enemy spawner) ----------

    public ShipLayout ExportLayout()
    {
        var layout = new ShipLayout();
        foreach (var m in hullCells.Values.Distinct())
            layout.hull.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.AnchorCell.x, anchorY = m.AnchorCell.y });
        foreach (var m in moduleCells.Values.Distinct())
            layout.modules.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.AnchorCell.x, anchorY = m.AnchorCell.y });
        return layout;
    }

    /// <summary>Destroys every placed block and resets the grid — call before loading a saved layout.</summary>
    public void Clear()
    {
        foreach (var m in hullCells.Values.Distinct().ToList())
            if (m != null) Destroy(m.gameObject);
        foreach (var m in moduleCells.Values.Distinct().ToList())
            if (m != null) Destroy(m.gameObject);

        hullCells.Clear();
        moduleCells.Clear();
    }

    /// <summary>Builds a full ship (hull + modules) from a saved layout with no player input — used for enemy ships and for restoring a saved player ship.</summary>
    public void BuildFromLayout(ShipLayout layout, BlockDatabase db, Faction faction)
    {
        Clear();

        identity.faction = faction;
        identity.ApplyTagToRoot();

        foreach (var entry in layout.hull)
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            PlaceHull(def.prefab, new Vector2Int(entry.anchorX, entry.anchorY), def.cells);
        }

        foreach (var entry in layout.modules)
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            PlaceModule(def.prefab, new Vector2Int(entry.anchorX, entry.anchorY), def.cells);
        }
    }
}

[System.Serializable]
public class ShipLayout
{
    [System.Serializable]
    public class Entry
    {
        public string blockId;
        public int anchorX, anchorY;
    }

    public List<Entry> hull = new();
    public List<Entry> modules = new();
}
