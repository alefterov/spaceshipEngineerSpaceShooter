using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BuildMode { Hull, Armor, Modules }

/// <summary>
/// Grid the ship is built on. Two independent layers:
///  - hullCells: structural pieces (Hull/Armor) — must always be adjacent to another hull piece.
///  - moduleCells: functional pieces (Weapon/Engine/Generator/Shield) — must sit entirely on top of
///    already-placed HULL cells specifically (Armor doesn't count — it protects the hull, it isn't
///    a mounting surface for modules).
/// Losing a hull cell destroys any module sitting on it (cascade).
/// </summary>
[RequireComponent(typeof(ShipIdentity))]
public class ShipGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 1f;

    [Header("Placement visuals")]
    [Tooltip("Small square sprite, one instance per grid cell — used for the general build grid and the " +
             "per-cell placement validity highlight under a dragged block. Assign a plain white 1x1 " +
             "sprite sized to one cell. Left unassigned, both visuals are skipped.")]
    public GameObject cellVisualPrefab;
    [Tooltip("Cell visual for the module-mode grid specifically (drawn over the hull's own cells in " +
             "Module build mode) — lets it look different from the general grid (e.g. a distinct border " +
             "style). Falls back to Cell Visual Prefab above if left unassigned.")]
    public GameObject moduleCellVisualPrefab;
    [Tooltip("Faint tint for the general grid covering the whole board, shown throughout Building view mode.")]
    public Color buildGridColor = new(1f, 1f, 1f, 0.06f);
    [Tooltip("Tint for the grid highlighting exactly the hull's own cells — shown only in Module build mode, on top of the general grid.")]
    public Color moduleGridColor = new(0.3f, 0.65f, 1f, 0.12f);
    [Tooltip("Sorting order applied to the module-mode grid's cell sprites. Needs to be higher than the " +
             "hull sprites' own sorting order, or the highlight renders hidden underneath the hull art " +
             "instead of visibly on top of it.")]
    public int moduleGridSortingOrder = 10;

    private Transform previewRoot;
    private readonly List<GameObject> previewPool = new();
    private Transform generalGridRoot;
    private Transform moduleGridRoot;
    private bool moduleGridVisible;

    private readonly Dictionary<Vector2Int, ShipModule> hullCells = new();
    private readonly Dictionary<Vector2Int, ShipModule> moduleCells = new();

    /// <summary>Fires whenever a hull cell is added or removed (placement, deletion, load, or clear) —
    /// e.g. HullOutlineRenderer listens to this to redraw the ship's outline.</summary>
    public event Action OnHullChanged;

    /// <summary>Read-only view of every structural cell (Hull AND Armor) — for systems that only need
    /// the footprint without depending on ShipGrid's placement API.</summary>
    public IEnumerable<Vector2Int> HullCellPositions => hullCells.Keys;

    /// <summary>Same as HullCellPositions, but Armor-type pieces excluded — HullOutlineRenderer uses
    /// this so the exterior contour only hugs the Hull, not armor plating bolted on top of it.</summary>
    public IEnumerable<Vector2Int> HullOnlyCellPositions
        => hullCells.Where(kv => kv.Value.type == ModuleType.Hull).Select(kv => kv.Key);

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

    /// <summary>
    /// World position of a SINGLE grid cell's center — this is where a block's root transform
    /// is placed (the anchor/pivot cell). The root never moves again after this, even on rotation.
    /// </summary>
    public Vector3 AnchorToWorld(Vector2Int anchor)
    {
        float x = (anchor.x - width * 0.5f + 0.5f) * cellSize;
        float y = (anchor.y - height * 0.5f + 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    /// <summary>
    /// World position of a grid CORNER — e.g. corner (x,y) is the bottom-left corner of cell (x,y).
    /// Used to trace cell-boundary outlines (HullOutlineRenderer), as opposed to AnchorToWorld
    /// which gives a cell's center.
    /// </summary>
    public Vector3 CornerToWorld(Vector2Int corner)
    {
        float x = (corner.x - width * 0.5f) * cellSize;
        float y = (corner.y - height * 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    /// <summary>
    /// Centroid (in local cell units, relative to the anchor) and cell-count size of a shape.
    /// Used to position/size the module's visual child and its collider — completely separate
    /// from `occupiedCells`, which is pure grid-cell bookkeeping. Keeping these two calculations
    /// independent is what prevents rotated/reloaded blocks from visually drifting or overlapping.
    /// </summary>
    public static (Vector2 centroidCells, Vector2Int sizeCells) ComputeLocalFootprint(List<Vector2Int> localShape)
    {
        var b = ShapeBounds(localShape);
        Vector2 centroid = new(b.xMin + b.width * 0.5f, b.yMin + b.height * 0.5f);
        Vector2Int size = new(b.width + 1, b.height + 1);
        return (centroid, size);
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

    /// <summary>Absolute grid cells a shape would occupy at the given anchor — used by GhostBlockController
    /// to know which cells to highlight, independent of whether the placement is actually valid.</summary>
    public List<Vector2Int> GetOccupiedCells(Vector2Int anchor, List<Vector2Int> localShape) => Offset(anchor, localShape);

    // ---------- Validation ----------

    /// <summary>Structural rule (Hull and Armor both use this): cells must be free & in bounds, and adjacent to existing hull (unless ship is empty).</summary>
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

    /// <summary>Module mode rule: every target cell must already be covered by Hull specifically
    /// (Armor doesn't count — see HullOnlyCellPositions), and not already have a module.</summary>
    public bool CanPlaceModule(Vector2Int anchor, List<Vector2Int> localShape)
    {
        var cells = Offset(anchor, localShape);

        foreach (var cell in cells)
        {
            if (!hullCells.TryGetValue(cell, out var hull) || hull.type != ModuleType.Hull) return false; // not hull (empty, or armor-only)
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

    public ShipModule PlaceHull(BlockDefinition definition, Vector2Int anchor, List<Vector2Int> localShape, int rotationSteps)
    {
        var module = Place(definition, anchor, localShape, rotationSteps, hullCells, registerWithIdentity: true);
        if (moduleGridVisible) RedrawModuleGrid(); // keep the module grid in sync if hull changed while it's showing
        OnHullChanged?.Invoke();
        return module;
    }

    public ShipModule PlaceModule(BlockDefinition definition, Vector2Int anchor, List<Vector2Int> localShape, int rotationSteps)
    {
        var module = Place(definition, anchor, localShape, rotationSteps, moduleCells, registerWithIdentity: false);
        if (moduleGridVisible) RedrawModuleGrid(); // the cell it just filled must stop showing as available
        return module;
    }

    private ShipModule Place(BlockDefinition definition, Vector2Int anchor, List<Vector2Int> localShape, int rotationSteps,
                              Dictionary<Vector2Int, ShipModule> layer, bool registerWithIdentity)
    {
        var cells = Offset(anchor, localShape);
        Vector3 rootWorldPos = AnchorToWorld(anchor);

        // Root is instantiated at the anchor cell with IDENTITY rotation — it never moves or spins.
        var instance = Instantiate(definition.prefab, rootWorldPos, Quaternion.identity, transform);
        instance.tag = identity.faction == Faction.Player ? "PlayerShip" : "EnemyShip";

        var module = instance.GetComponent<ShipModule>();
        if (module == null)
        {
            Debug.LogError($"Block prefab '{definition.prefab.name}' has no ShipModule component.");
            Destroy(instance);
            return null;
        }

        // Always sourced from the BlockDefinition, never trusted from whatever the prefab's own
        // moduleId field happens to say — a hand-edited prefab drifting out of sync with the
        // BlockDefinition it belongs to is exactly what broke save/load for every non-hull block.
        module.moduleId = definition.id;
        module.buildCost = definition.buildCost;
        module.occupiedCells = cells;       // pure grid bookkeeping — independent of any transform
        module.anchorCell = anchor;
        module.rotationSteps = rotationSteps;
        module.ApplyViewMode(CurrentViewMode);

        // Only the VISUAL child rotates/repositions — around its own point, computed fresh from
        // the already-rotated local shape, so it always lines up with occupiedCells exactly.
        var (centroidCells, sizeCells) = ComputeLocalFootprint(localShape);
        if (module.visualRoot != null)
        {
            module.visualRoot.localPosition = new Vector3(centroidCells.x * cellSize, centroidCells.y * cellSize, 0f);
            module.visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationSteps);
        }

        // Resize the (root) collider to cover the whole footprint's bounding box.
        // 90°-multiple rotations keep the box axis-aligned, so no collider rotation is needed.
        if (instance.TryGetComponent<BoxCollider2D>(out var box))
        {
            box.offset = new Vector2(centroidCells.x * cellSize, centroidCells.y * cellSize);
            box.size = new Vector2(sizeCells.x * cellSize, sizeCells.y * cellSize);
        }

        foreach (var cell in cells) layer[cell] = module;

        if (registerWithIdentity) identity.RegisterHull(module);
        else module.OnDestroyed += _ =>
        {
            moduleCells.Keys.Where(k => moduleCells[k] == module).ToList().ForEach(k => moduleCells.Remove(k));
            if (moduleGridVisible) RedrawModuleGrid(); // freed cell should be able to show as available again
        };

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

        if (moduleGridVisible) RedrawModuleGrid();
        OnHullChanged?.Invoke();
    }

    /// <summary>
    /// Removes a functional module cleanly (editor deletion, not combat destruction — no debris,
    /// no TakeDamage/OnDestroyed event). Use RemoveHull for structural pieces instead.
    /// </summary>
    public void RemoveModule(ShipModule module)
    {
        foreach (var cell in module.occupiedCells) moduleCells.Remove(cell);
        Destroy(module.gameObject);

        if (moduleGridVisible) RedrawModuleGrid(); // freed cell should be able to show as available again
    }

    /// <summary>
    /// Finds whatever block sits at a world position WITHOUT deleting it — modules take priority
    /// over hull (a cell with both reports its module first; the hull under it only shows up once
    /// the module is gone). Used by the editor's Delete mode to know what a confirmation popup
    /// would be deleting before the player commits to it. Returns null if the cell is empty.
    /// </summary>
    public ShipModule FindDeletableAt(Vector3 worldPosition)
    {
        Vector2Int cell = WorldToGrid(worldPosition);

        if (moduleCells.TryGetValue(cell, out var functionalModule)) return functionalModule;
        if (hullCells.TryGetValue(cell, out var hullModule)) return hullModule;
        return null;
    }

    /// <summary>Deletes a specific block found via FindDeletableAt — dispatches to RemoveHull or
    /// RemoveModule depending on its type (Hull/Armor share the structural layer, everything else
    /// is a module).</summary>
    public void DeleteBlock(ShipModule target)
    {
        if (target == null) return;

        if (target.type == ModuleType.Hull || target.type == ModuleType.Armor) RemoveHull(target);
        else RemoveModule(target);
    }

    /// <summary>Convenience one-shot: finds and immediately deletes whatever is at a world position,
    /// no confirmation. Prefer FindDeletableAt + a confirm popup + DeleteBlock for player-facing
    /// deletion (see GhostBlockController) — this is for callers that don't need to ask first.
    /// Returns true if something was actually removed.</summary>
    public bool TryDeleteAt(Vector3 worldPosition)
    {
        var target = FindDeletableAt(worldPosition);
        if (target == null) return false;
        DeleteBlock(target);
        return true;
    }

    // ---------- View mode (menu preview vs builder) ----------

    /// <summary>Switches every currently placed block between the closed preview look and the exposed builder look.</summary>
    public void SetViewMode(ShipViewMode mode)
    {
        CurrentViewMode = mode;

        foreach (var m in hullCells.Values.Distinct()) m.ApplyViewMode(mode);
        foreach (var m in moduleCells.Values.Distinct()) m.ApplyViewMode(mode);

        // Safety net only: neither grid overlay may survive a trip back to the menu preview.
        // Showing the right one for the right build sub-mode (Hull/Armor -> general grid,
        // Modules -> hull-footprint grid) is owned by BuildModeController, not here — it's the
        // only thing that actually knows which sub-mode is active.
        if (mode != ShipViewMode.Building)
        {
            HideGeneralGrid();
            HideModuleGrid();
        }
    }

    // ---------- Placement preview (per-cell valid/invalid highlight under a dragged block) ----------

    /// <summary>Highlights exactly the given cells in the given color (typically GhostBlockController's
    /// valid/invalid color) — pooled, so dragging a block around doesn't spam Instantiate/Destroy.</summary>
    public void ShowPlacementPreview(List<Vector2Int> cells, Color color)
    {
        if (cellVisualPrefab == null) return; // no placeholder art assigned yet — skip silently

        EnsurePreviewPool(cells.Count);

        for (int i = 0; i < previewPool.Count; i++)
        {
            bool used = i < cells.Count;
            previewPool[i].SetActive(used);
            if (!used) continue;

            previewPool[i].transform.position = AnchorToWorld(cells[i]);
            if (previewPool[i].TryGetComponent<SpriteRenderer>(out var sr)) sr.color = color;
        }
    }

    public void ClearPlacementPreview()
    {
        foreach (var go in previewPool) go.SetActive(false);
    }

    private void EnsurePreviewPool(int count)
    {
        if (previewRoot == null)
        {
            var rootObj = new GameObject("PlacementPreview");
            rootObj.transform.SetParent(transform, false);
            previewRoot = rootObj.transform;
        }

        while (previewPool.Count < count)
            previewPool.Add(Instantiate(cellVisualPrefab, previewRoot));
    }

    // ---------- General build grid (whole board — Hull/Armor mode) ----------

    /// <summary>Call when entering Hull or Armor build mode.</summary>
    public void ShowGeneralGrid()
    {
        HideGeneralGrid();
        if (cellVisualPrefab == null) return;

        var rootObj = new GameObject("BuildGridVisual");
        rootObj.transform.SetParent(transform, false);
        generalGridRoot = rootObj.transform;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cellObj = Instantiate(cellVisualPrefab, generalGridRoot);
                cellObj.transform.position = AnchorToWorld(new Vector2Int(x, y));
                if (cellObj.TryGetComponent<SpriteRenderer>(out var sr)) sr.color = buildGridColor;
            }
        }
    }

    /// <summary>Call when leaving Hull/Armor mode (switching to Module mode, or leaving the builder).</summary>
    public void HideGeneralGrid()
    {
        if (generalGridRoot != null) Destroy(generalGridRoot.gameObject);
        generalGridRoot = null;
    }

    // ---------- Module-mode grid highlight (drawn over the hull's own cells, on top of the general grid) ----------

    /// <summary>Draws a highlight over every Hull cell that's still free to build on — a cell already
    /// carrying a module is skipped, so it stops reading as "available" the instant a module fills it.
    /// Call when entering Module build mode; automatically redraws itself if the hull or modules change
    /// while it's showing, via PlaceHull/RemoveHull/PlaceModule/RemoveModule.</summary>
    public void ShowModuleGrid()
    {
        moduleGridVisible = true;
        RedrawModuleGrid();
    }

    /// <summary>Call when leaving Module build mode (switching to Hull/Armor mode, or leaving the builder).</summary>
    public void HideModuleGrid()
    {
        moduleGridVisible = false;
        if (moduleGridRoot != null) Destroy(moduleGridRoot.gameObject);
        moduleGridRoot = null;
    }

    private void RedrawModuleGrid()
    {
        if (moduleGridRoot != null) Destroy(moduleGridRoot.gameObject);
        moduleGridRoot = null;

        var prefab = moduleCellVisualPrefab != null ? moduleCellVisualPrefab : cellVisualPrefab;
        if (prefab == null) return;

        var rootObj = new GameObject("ModuleGridVisual");
        rootObj.transform.SetParent(transform, false);
        moduleGridRoot = rootObj.transform;

        // Only cells that are actually still buildable — Hull, and not already carrying a module.
        foreach (var cell in HullOnlyCellPositions.Where(c => !moduleCells.ContainsKey(c)))
        {
            var cellObj = Instantiate(prefab, moduleGridRoot);
            cellObj.transform.position = AnchorToWorld(cell);
            if (cellObj.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.color = moduleGridColor;
                sr.sortingOrder = moduleGridSortingOrder; // draw above the hull sprites, not hidden behind them
            }
        }
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
            layout.hull.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.anchorCell.x, anchorY = m.anchorCell.y, rotationSteps = m.rotationSteps });
        foreach (var m in moduleCells.Values.Distinct())
            layout.modules.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.anchorCell.x, anchorY = m.anchorCell.y, rotationSteps = m.rotationSteps });
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
        OnHullChanged?.Invoke();
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
            var rotatedShape = BlockDefinition.RotateCells(def.cells, entry.rotationSteps);
            PlaceHull(def, new Vector2Int(entry.anchorX, entry.anchorY), rotatedShape, entry.rotationSteps);
        }

        foreach (var entry in layout.modules)
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            var rotatedShape = BlockDefinition.RotateCells(def.cells, entry.rotationSteps);
            PlaceModule(def, new Vector2Int(entry.anchorX, entry.anchorY), rotatedShape, entry.rotationSteps);
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
        public int rotationSteps;
    }

    public List<Entry> hull = new();
    public List<Entry> modules = new();
}