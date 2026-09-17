using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BuildMode { Hull, Armor, Modules }

/// <summary>
/// Grid the ship is built on. Two independent layers:
///  - hullCells: structural pieces (Hull/Armor) — must always be adjacent to another hull piece.
///  - moduleCells: functional pieces (Weapon/Engine/Generator/Shield/Cockpit) — must sit entirely on
///    top of already-placed HULL cells specifically (Armor doesn't count — it protects the hull, it
///    isn't a mounting surface for modules). Cockpit is a module like any other here — the only thing
///    special about it is ShipGrid.HasCockpit, checked by MainMenuFlowController before allowing a save.
/// Losing a hull cell destroys any module sitting on it (cascade).
/// </summary>
[RequireComponent(typeof(ShipIdentity))]
public class ShipGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 1f;

    [Header("Hangar upgrades")]
    [Tooltip("Extra rows/columns granted by hangar upgrades — each level adds exactly one of each. " +
             "Set via SetHangarLevel; GameDataManager owns the persisted value and the research-point " +
             "cost (see GameDataManager.TryUpgradeHangar). AnchorToWorld/CornerToWorld always center on " +
             "the CURRENT effective size, so the grid stays centered as it grows — SetHangarLevel " +
             "repositions already-placed blocks to match instead of leaving them behind.")]
    [SerializeField] private int hangarLevel;

    public int HangarLevel => hangarLevel;
    /// <summary>Actual buildable size including hangar upgrades — e.g. for a "5x5" UI label.</summary>
    public int GridWidth => width + hangarLevel;
    public int GridHeight => height + hangarLevel;

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

    [Header("Module part render layers")]
    [Tooltip("Applied to each block's interiorRoot / roofRoot / topRoot at placement time (ShipModule." +
             "ApplySortingOrders). The order that actually matters: interiors at the bottom, then the " +
             "hull's roof, then module roofs, then topRoot parts (e.g. a turret's barrel) above " +
             "everything — a generator's or turret's roof has to cover the hull roof it sits on, not " +
             "disappear under it, and the part sticking out of the roof has to stay on top of that too. " +
             "Sorting orders authored inside a prefab are kept as offsets from these bases.")]
    public int hullInteriorSortingOrder = 0;
    public int moduleInteriorSortingOrder = 1;
    public int hullRoofSortingOrder = 20;
    public int moduleRoofSortingOrder = 30;
    public int topSortingOrder = 40;

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

    /// <summary>Fires on ANY change to the ship — hull, armor, or modules; placement or removal —
    /// broader than OnHullChanged (which is hull-cells-only, for the outline). ShipStatsPanel uses
    /// this to stay live while building, since stats depend on modules too (weapons, engines, etc).</summary>
    public event Action OnShipChanged;

    /// <summary>Read-only view of every structural cell (Hull AND Armor) — for systems that only need
    /// the footprint without depending on ShipGrid's placement API.</summary>
    public IEnumerable<Vector2Int> HullCellPositions => hullCells.Keys;

    /// <summary>Same as HullCellPositions, but Armor-type pieces excluded — HullOutlineRenderer uses
    /// this so the exterior contour only hugs the Hull, not armor plating bolted on top of it.</summary>
    public IEnumerable<Vector2Int> HullOnlyCellPositions
        => hullCells.Where(kv => kv.Value.type == ModuleType.Hull).Select(kv => kv.Key);

    /// <summary>Whether the ship has at least one non-destroyed Cockpit MODULE placed (moduleCells —
    /// a cockpit is a regular module like Engine/Generator, not a structural piece). A ship without
    /// one is not allowed to be saved — see MainMenuFlowController.OnSaveShipPressed.</summary>
    public bool HasCockpit => moduleCells.Values.Distinct().Any(m => m.type == ModuleType.Cockpit && !m.IsDestroyed);

    [Tooltip("Preview = closed look (main menu), Building = exposed internals (editor). " +
             "Applied to every module immediately on placement.")]
    public ShipViewMode CurrentViewMode { get; private set; } = ShipViewMode.Building;

    /// <summary>Fired whenever SetViewMode runs — e.g. HullOutlineRenderer uses this to hide the
    /// builder-only contour outside Building mode, without needing to also watch hull changes for that.</summary>
    public event Action OnViewModeChanged;

    private ShipIdentity identity;
    private void Awake() => identity = GetComponent<ShipIdentity>();

    // ---------- Coordinate helpers ----------

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.x < GridWidth && cell.y >= 0 && cell.y < GridHeight;

    /// <summary>Applies a hangar level directly — call once on load/start (GameDataManager.HangarLevel)
    /// and again whenever it changes via an upgrade. Since AnchorToWorld centers on the current
    /// effective size, growing it shifts where every anchor cell maps to in world space by a uniform
    /// amount — repositions every already-placed block to match, so the whole ship translates
    /// together and stays centered on the (now bigger) grid instead of drifting to one corner of it.
    /// Also redraws whichever grid overlay is currently shown.</summary>
    public void SetHangarLevel(int level)
    {
        if (hangarLevel == level) return;
        hangarLevel = level;

        foreach (var m in hullCells.Values.Distinct())
            if (m != null) m.transform.position = AnchorToWorld(m.anchorCell);
        foreach (var m in moduleCells.Values.Distinct())
            if (m != null) m.transform.position = AnchorToWorld(m.anchorCell);

        if (generalGridRoot != null) ShowGeneralGrid();
        if (moduleGridVisible) RedrawModuleGrid();
    }

    public Vector2Int WorldToGrid(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        int x = Mathf.FloorToInt(local.x / cellSize + GridWidth * 0.5f);
        int y = Mathf.FloorToInt(local.y / cellSize + GridHeight * 0.5f);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// World position of a SINGLE grid cell's center — this is where a block's root transform
    /// is placed (the anchor/pivot cell). Centers on the CURRENT effective grid size (base size plus
    /// any hangar upgrades), so the whole grid stays centered as it grows — see SetHangarLevel.
    /// </summary>
    public Vector3 AnchorToWorld(Vector2Int anchor)
    {
        float x = (anchor.x - GridWidth * 0.5f + 0.5f) * cellSize;
        float y = (anchor.y - GridHeight * 0.5f + 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    /// <summary>
    /// World position of a grid CORNER — e.g. corner (x,y) is the bottom-left corner of cell (x,y).
    /// Used to trace cell-boundary outlines (HullOutlineRenderer), as opposed to AnchorToWorld
    /// which gives a cell's center.
    /// </summary>
    public Vector3 CornerToWorld(Vector2Int corner)
    {
        float x = (corner.x - GridWidth * 0.5f) * cellSize;
        float y = (corner.y - GridHeight * 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    /// <summary>Min/max HULL cell coordinates actually built (corner space, max exclusive) — the
    /// ship's real footprint, independent of the grid's own (possibly much bigger, after hangar
    /// upgrades) size. False if nothing's been built yet.</summary>
    public bool TryGetHullCellBounds(out Vector2Int min, out Vector2Int maxExclusive)
    {
        var cells = HullOnlyCellPositions.ToList();
        if (cells.Count == 0) { min = maxExclusive = default; return false; }

        min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
        maxExclusive = new Vector2Int(cells.Max(c => c.x) + 1, cells.Max(c => c.y) + 1);
        return true;
    }

    /// <summary>World-space center of the ship's actual built hull footprint — NOT this transform's
    /// own position, and not the grid's own center either, since a smaller ship built off to one side
    /// of a (possibly hangar-upgraded) grid has a visual middle that's neither. Falls back to this
    /// object's own position if no hull exists yet.</summary>
    public Vector3 GetHullWorldCenter()
        => TryGetHullCellBounds(out var min, out var maxExclusive)
            ? (CornerToWorld(min) + CornerToWorld(maxExclusive)) * 0.5f
            : transform.position;

    /// <summary>World-space (width, height) of the ship's actual built hull footprint. Zero if
    /// nothing's been built yet.</summary>
    public Vector2 GetHullWorldSize()
    {
        if (!TryGetHullCellBounds(out var min, out var maxExclusive)) return Vector2.zero;
        Vector3 a = CornerToWorld(min);
        Vector3 b = CornerToWorld(maxExclusive);
        return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
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

    /// <summary>Structural rule (Hull and Armor both use this): cells must be free & in bounds, and
    /// adjacent to existing hull (unless ship is empty) via a genuine solid-to-solid edge — a
    /// triangular cell's hypotenuse side never counts, on either side of the join (see
    /// BlockDefinition.CellShape / GetSolidSides).</summary>
    public bool CanPlaceHull(Vector2Int anchor, List<BlockCell> rotatedCells)
    {
        var cells = Offset(anchor, BlockDefinition.Offsets(rotatedCells));

        foreach (var cell in cells)
        {
            if (!InBounds(cell)) return false;
            if (hullCells.ContainsKey(cell)) return false;
            if (IsEngineInColumnAbove(cell)) return false; // an engine's exhaust renders down its whole column — keep all of it permanently clear
        }

        if (hullCells.Count == 0) return true; // first block can go anywhere

        for (int i = 0; i < cells.Count; i++)
        {
            var mySolidSides = BlockDefinition.GetSolidSides(rotatedCells[i].shape);

            foreach (var (offset, mySide, neighborSide) in AdjacencyDirections)
            {
                if (!mySolidSides.HasFlag(mySide)) continue; // this side is a hypotenuse — nothing can attach through it

                if (!hullCells.TryGetValue(cells[i] + offset, out var neighborModule)) continue;

                var neighborSolidSides = BlockDefinition.GetSolidSides(neighborModule.GetCellShape(cells[i] + offset));
                if (neighborSolidSides.HasFlag(neighborSide)) return true; // genuine solid-to-solid contact
            }
        }

        return false;
    }

    /// <summary>Module mode rule: every target cell must already be covered by Hull specifically
    /// (Armor doesn't count — see HullOnlyCellPositions), and not already have a module. A Cockpit
    /// is additionally rejected outright once the ship already has one — see HasCockpit. An Engine is
    /// additionally rejected if anything already occupies its own column below it, all the way to the
    /// bottom of the grid — see IsColumnBelowOccupied — since that's exactly where its exhaust visual
    /// always renders.</summary>
    public bool CanPlaceModule(Vector2Int anchor, List<Vector2Int> localShape, BlockCategory category)
    {
        if (category == BlockCategory.Cockpit && HasCockpit) return false; // only one cockpit per ship

        var cells = Offset(anchor, localShape);

        foreach (var cell in cells)
        {
            if (!hullCells.TryGetValue(cell, out var hull) || hull.type != ModuleType.Hull) return false; // not hull (empty, or armor-only)
            if (moduleCells.ContainsKey(cell)) return false;  // cell already has a module
            if (IsEngineInColumnAbove(cell)) return false; // can't sit anywhere below an existing engine
        }

        if (category == BlockCategory.Engine)
            foreach (var cell in cells)
                if (IsColumnBelowOccupied(cell)) return false; // this engine's own exhaust column must stay entirely clear

        return true;
    }

    /// <summary>Whether any block — hull, armor, or module — currently occupies this absolute cell.</summary>
    private bool IsCellOccupied(Vector2Int cell) => hullCells.ContainsKey(cell) || moduleCells.ContainsKey(cell);

    /// <summary>Whether a non-destroyed Engine module currently occupies this absolute cell.</summary>
    private bool IsEngineAt(Vector2Int cell)
        => moduleCells.TryGetValue(cell, out var m) && m.type == ModuleType.Engine && !m.IsDestroyed;

    /// <summary>Whether an Engine occupies ANY cell directly above this one in the same column, all the
    /// way to the top of the grid — an engine's exhaust renders down its entire column, not just the
    /// one cell right below it.</summary>
    private bool IsEngineInColumnAbove(Vector2Int cell)
    {
        for (int y = cell.y + 1; y < GridHeight; y++)
            if (IsEngineAt(new Vector2Int(cell.x, y))) return true;
        return false;
    }

    /// <summary>Whether anything occupies ANY cell below this one in the same column, all the way to
    /// the bottom of the grid.</summary>
    private bool IsColumnBelowOccupied(Vector2Int cell)
    {
        for (int y = cell.y - 1; y >= 0; y--)
            if (IsCellOccupied(new Vector2Int(cell.x, y))) return true;
        return false;
    }

    /// <summary>The 4 grid directions, each paired with which CellSides flag they touch on the
    /// departing cell and on the neighboring cell — e.g. moving Up leaves via that cell's Top side
    /// and arrives at the neighbor's Bottom side. Used by CanPlaceHull's per-side adjacency check.</summary>
    private static readonly (Vector2Int offset, CellSides mySide, CellSides neighborSide)[] AdjacencyDirections =
    {
        (Vector2Int.up,    CellSides.Top,    CellSides.Bottom),
        (Vector2Int.down,  CellSides.Bottom, CellSides.Top),
        (Vector2Int.left,  CellSides.Left,   CellSides.Right),
        (Vector2Int.right, CellSides.Right,  CellSides.Left),
    };

    // ---------- Placement ----------

    public ShipModule PlaceHull(BlockDefinition definition, Vector2Int anchor, List<BlockCell> rotatedCells, int rotationSteps)
    {
        var module = Place(definition, anchor, rotatedCells, rotationSteps, hullCells, registerWithIdentity: true);
        if (moduleGridVisible) RedrawModuleGrid(); // keep the module grid in sync if hull changed while it's showing
        OnHullChanged?.Invoke();
        OnShipChanged?.Invoke();
        return module;
    }

    public ShipModule PlaceModule(BlockDefinition definition, Vector2Int anchor, List<BlockCell> rotatedCells, int rotationSteps)
    {
        var module = Place(definition, anchor, rotatedCells, rotationSteps, moduleCells, registerWithIdentity: false);
        if (moduleGridVisible) RedrawModuleGrid(); // the cell it just filled must stop showing as available
        OnShipChanged?.Invoke();
        return module;
    }

    private ShipModule Place(BlockDefinition definition, Vector2Int anchor, List<BlockCell> rotatedCells, int rotationSteps,
                              Dictionary<Vector2Int, ShipModule> layer, bool registerWithIdentity)
    {
        var localShape = BlockDefinition.Offsets(rotatedCells);
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
        module.cellShapes = BlockDefinition.Shapes(rotatedCells); // same index order as occupiedCells
        module.anchorCell = anchor;
        module.rotationSteps = rotationSteps;
        module.ApplyViewMode(CurrentViewMode);

        // Which layer this block landed on is only known here, and it's what decides whether its roof
        // draws above or below other roofs.
        bool structural = layer == hullCells;
        module.ApplySortingOrders(
            structural ? hullInteriorSortingOrder : moduleInteriorSortingOrder,
            structural ? hullRoofSortingOrder : moduleRoofSortingOrder,
            topSortingOrder);

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
            OnShipChanged?.Invoke();
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
        OnShipChanged?.Invoke();
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
        OnShipChanged?.Invoke();
    }

    /// <summary>
    /// Resolves a physical impact (e.g. a meteor) against whatever structural block occupies this
    /// cell, treating it and any module riding on it as ONE combined pool of HP — a module reinforces
    /// the hull cell it sits on against blunt impacts, on top of protecting its own separate HP from
    /// direct weapons fire. An Armor cell never has a module on it (see CanPlaceModule), so this
    /// naturally reduces to "just the armor's own HP" there without any special-casing.
    ///
    /// Lethal damage destroys the hull cell outright (RemoveHull already cascades to the module, if
    /// any). Otherwise damage drains the hull's own HP first — its sprite is what visually shows wear
    /// — and spills any overflow into the module's HP.
    ///
    /// Both the hull's own BlockDamageVisual AND the module's (if it has one) are pushed the same
    /// combined ratio — a module's roof sprite renders visibly on top of the hull once the ship is
    /// closed, so it needs to show the same wear the hull underneath it does.
    ///
    /// Returns true if this was lethal (block destroyed), false if it merely took damage. No-op
    /// (returns false) if there's no structural block at this cell at all.
    /// </summary>
    public bool ApplyCollisionDamage(Vector2Int cell, float damage)
    {
        if (!hullCells.TryGetValue(cell, out var hull)) return false;

        var module = moduleCells.TryGetValue(cell, out var m) ? m : null;
        float combinedCurrent = hull.CurrentHP + (module != null ? module.CurrentHP : 0f);

        if (damage >= combinedCurrent)
        {
            RemoveHull(hull);
            return true;
        }

        float hullDamage = Mathf.Min(damage, hull.CurrentHP);
        hull.TakeDamage(hullDamage);

        float overflow = damage - hullDamage;
        if (overflow > 0f && module != null) module.TakeDamage(overflow);

        float combinedMax = hull.maxHP + (module != null ? module.maxHP : 0f);
        float newCombinedCurrent = hull.CurrentHP + (module != null ? module.CurrentHP : 0f);
        if (combinedMax > 0f)
        {
            float ratio = newCombinedCurrent / combinedMax;
            if (hull.TryGetComponent<BlockDamageVisual>(out var hullVisual)) hullVisual.SetHealthRatio(ratio);
            if (module != null && module.TryGetComponent<BlockDamageVisual>(out var moduleVisual)) moduleVisual.SetHealthRatio(ratio);
        }

        return false;
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
    /// RemoveModule depending on its type (Hull/Armor share the structural layer, everything else,
    /// including Cockpit, is a module).</summary>
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

        OnViewModeChanged?.Invoke();
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

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
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

    /// <summary>Total HP capacity across every placed block — hull, armor, and every module combined
    /// (unlike ComputeTotalArmor/ComputeShieldStrength, which are per-category).</summary>
    public float ComputeTotalHP()
        => hullCells.Values.Distinct().Where(m => !m.IsDestroyed).Sum(m => m.maxHP)
         + moduleCells.Values.Distinct().Where(m => !m.IsDestroyed).Sum(m => m.maxHP);

    /// <summary>Net energy — positive means surplus, negative means the ship is over budget.</summary>
    public float ComputeEnergyBalance()
        => moduleCells.Values.Distinct().Where(m => !m.IsDestroyed).Sum(m => m.energyDelta);

    /// <summary>Total armor HP capacity — Armor-category structural blocks only (Hull itself excluded).</summary>
    public float ComputeTotalArmor()
        => hullCells.Values.Distinct().Where(m => m.type == ModuleType.Armor && !m.IsDestroyed).Sum(m => m.maxHP);

    /// <summary>Total shield HP capacity. Reads any placed block with type==Shield generically (via
    /// ShieldModule, which just tags itself with that type in Awake — same pattern as ArmorModule),
    /// so this method itself never needs touching when new shield block variants are added.</summary>
    public float ComputeShieldStrength()
        => moduleCells.Values.Distinct().Where(m => m.type == ModuleType.Shield && !m.IsDestroyed).Sum(m => m.maxHP);

    /// <summary>Total damage-per-second across every non-destroyed weapon.</summary>
    public float ComputeFirepower()
        => moduleCells.Values.Distinct().OfType<WeaponModule>().Where(w => !w.IsDestroyed).Sum(w => w.DamagePerSecond);

    /// <summary>Total thrust from every non-destroyed engine (EngineModule.GetThrust() already returns 0 when destroyed).</summary>
    public float ComputeEnginePower()
        => moduleCells.Values.Distinct().OfType<EngineModule>().Sum(e => e.GetThrust());

    /// <summary>Total energy output from generators — the positive half of ComputeEnergyBalance, split out.</summary>
    public float ComputeEnergyGeneration()
        => moduleCells.Values.Distinct().Where(m => !m.IsDestroyed && m.energyDelta > 0f).Sum(m => m.energyDelta);

    /// <summary>Total energy draw from weapons/engines/shields, as a positive number — the negative
    /// half of ComputeEnergyBalance, split out and sign-flipped so it reads as a plain "cost".</summary>
    public float ComputeEnergyConsumption()
        => moduleCells.Values.Distinct().Where(m => !m.IsDestroyed && m.energyDelta < 0f).Sum(m => -m.energyDelta);

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
        OnShipChanged?.Invoke();
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
            var rotatedCells = BlockDefinition.RotateCells(def.cells, entry.rotationSteps);
            PlaceHull(def, new Vector2Int(entry.anchorX, entry.anchorY), rotatedCells, entry.rotationSteps);
        }

        foreach (var entry in layout.modules)
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            var rotatedCells = BlockDefinition.RotateCells(def.cells, entry.rotationSteps);
            PlaceModule(def, new Vector2Int(entry.anchorX, entry.anchorY), rotatedCells, entry.rotationSteps);
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