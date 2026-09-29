using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Which palette tab is active in the builder, in tab order. Armor also covers Shields and
/// Weapons covers every weapon family via sub-tabs. Purely a palette filter — every block, whatever its
/// tab, is placed on the same grid with the same rules (see ShipGrid.CanPlace).</summary>
public enum BuildMode { Cockpit, Generators, Engines, Repair, Armor, Weapons }

/// <summary>
/// Grid the ship is built on. ONE layer: every block (armor, weapon, engine, generator, shield,
/// cockpit) occupies its own cells, owns its own HP, and is destroyed on its own — there is no hull
/// underneath anything and no cascade when a neighbor dies. The only structural rule is contact: a new
/// block must touch an existing one through solid sides (the first block can go anywhere).
/// Cockpit is a block like any other here — the only thing special about it is ShipGrid.HasCockpit,
/// checked by MainMenuFlowController before allowing a save.
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
    [Tooltip("Small square sprite, one instance per grid cell — used for the build grid and the " +
             "per-cell placement validity highlight under a dragged block. Assign a plain white 1x1 " +
             "sprite sized to one cell. Left unassigned, both visuals are skipped.")]
    public GameObject cellVisualPrefab;
    [Tooltip("Faint tint for the build grid covering the whole board, shown throughout Building view mode.")]
    public Color buildGridColor = new(1f, 1f, 1f, 0.06f);

    [Header("Block render layers")]
    [Tooltip("Applied to each block at placement time (ShipModule.ApplySortingOrders): block bodies " +
             "(roofRoot) at Roof Sorting Order, a weapon's separate moving parts (topRoot — e.g. a " +
             "turret's tower and barrel) at Top Sorting Order, above every body. Sorting orders " +
             "authored inside a prefab are kept as offsets from these bases.")]
    [FormerlySerializedAs("hullRoofSortingOrder")] public int roofSortingOrder = 20;
    public int topSortingOrder = 40;

    private Transform previewRoot;
    private readonly List<GameObject> previewPool = new();
    private Transform generalGridRoot;

    private readonly Dictionary<Vector2Int, ShipModule> cells = new();

    /// <summary>Fires on ANY change to the ship — a block placed, removed, destroyed, or the whole grid
    /// cleared/reloaded. ShipStatsPanel listens to this to stay live while building.</summary>
    public event Action OnShipChanged;

    /// <summary>Read-only view of every occupied cell — for systems that only need the ship's footprint
    /// without depending on ShipGrid's placement API.</summary>
    public IEnumerable<Vector2Int> OccupiedCellPositions => cells.Keys;

    /// <summary>Every distinct block currently on the grid (multi-cell blocks appear once).</summary>
    /// <summary>Every distinct block currently on the grid (multi-cell blocks appear once) — e.g. for a
    /// RepairModule scanning for the most damaged one.</summary>
    public IEnumerable<ShipModule> Blocks => cells.Values.Distinct();

    /// <summary>Whether the ship has at least one non-destroyed Cockpit placed. A ship without one is
    /// not allowed to be saved — see MainMenuFlowController.OnSaveShipPressed.</summary>
    public bool HasCockpit => Blocks.Any(m => m.type == ModuleType.Cockpit && !m.IsDestroyed);

    /// <summary>Preview = main menu / battle look (idle animations play), Building = editor (they don't).
    /// Applied to every block immediately on placement.</summary>
    public ShipViewMode CurrentViewMode { get; private set; } = ShipViewMode.Building;

    /// <summary>Fired whenever SetViewMode runs — for anything that should only exist while building.</summary>
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
    /// Also redraws the grid overlay if it's currently shown.</summary>
    public void SetHangarLevel(int level)
    {
        if (hangarLevel == level) return;
        hangarLevel = level;

        foreach (var m in Blocks)
            if (m != null) m.transform.position = AnchorToWorld(m.anchorCell);

        if (generalGridRoot != null) ShowGeneralGrid();
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
    /// Used to trace cell corners (e.g. ship bounds), as opposed to AnchorToWorld
    /// which gives a cell's center.
    /// </summary>
    public Vector3 CornerToWorld(Vector2Int corner)
    {
        float x = (corner.x - GridWidth * 0.5f) * cellSize;
        float y = (corner.y - GridHeight * 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    /// <summary>Min/max cell coordinates actually built (corner space, max exclusive) — the ship's
    /// real footprint, independent of the grid's own (possibly much bigger, after hangar upgrades)
    /// size. False if nothing's been built yet.</summary>
    public bool TryGetShipCellBounds(out Vector2Int min, out Vector2Int maxExclusive)
    {
        if (cells.Count == 0) { min = maxExclusive = default; return false; }

        min = new Vector2Int(cells.Keys.Min(c => c.x), cells.Keys.Min(c => c.y));
        maxExclusive = new Vector2Int(cells.Keys.Max(c => c.x) + 1, cells.Keys.Max(c => c.y) + 1);
        return true;
    }

    /// <summary>World-space center of the ship's actual built footprint — NOT this transform's own
    /// position, and not the grid's own center either, since a smaller ship built off to one side of a
    /// (possibly hangar-upgraded) grid has a visual middle that's neither. Falls back to this object's
    /// own position if nothing is built yet.</summary>
    public Vector3 GetShipWorldCenter()
        => TryGetShipCellBounds(out var min, out var maxExclusive)
            ? (CornerToWorld(min) + CornerToWorld(maxExclusive)) * 0.5f
            : transform.position;

    /// <summary>World-space (width, height) of the ship's actual built footprint. Zero if nothing's
    /// been built yet.</summary>
    public Vector2 GetShipWorldSize()
    {
        if (!TryGetShipCellBounds(out var min, out var maxExclusive)) return Vector2.zero;
        Vector3 a = CornerToWorld(min);
        Vector3 b = CornerToWorld(maxExclusive);
        return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

    /// <summary>
    /// Centroid (in local cell units, relative to the anchor) and cell-count size of a shape.
    /// Used to position/size the block's visual child and its collider — completely separate
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

    /// <summary>
    /// The single placement rule for every block: cells must be free and in bounds, and (unless the
    /// ship is still empty) at least one cell must touch an existing block through a genuine
    /// solid-to-solid edge — a triangular cell's hypotenuse side never counts, on either side of the
    /// join (see BlockDefinition.CellShape / GetSolidSides).
    ///
    /// Extra category rules: only one Cockpit per ship (see HasCockpit); an Engine's exhaust renders down
    /// its whole column, so nothing may already sit anywhere below an engine being placed, and nothing
    /// may ever be placed anywhere below an existing engine (see IsEngineInColumnAbove /
    /// IsColumnBelowOccupied).
    /// </summary>
    public bool CanPlace(Vector2Int anchor, List<BlockCell> rotatedCells, BlockCategory category)
    {
        if (category == BlockCategory.Cockpit && HasCockpit) return false; // only one cockpit per ship

        var targets = Offset(anchor, BlockDefinition.Offsets(rotatedCells));

        foreach (var cell in targets)
        {
            if (!InBounds(cell)) return false;
            if (cells.ContainsKey(cell)) return false;
            if (IsEngineInColumnAbove(cell)) return false; // can't sit anywhere below an existing engine
        }

        if (category == BlockCategory.Engine)
            foreach (var cell in targets)
                if (IsColumnBelowOccupied(cell)) return false; // this engine's own exhaust column must stay entirely clear

        if (cells.Count == 0) return true; // first block can go anywhere

        for (int i = 0; i < targets.Count; i++)
        {
            var mySolidSides = BlockDefinition.GetSolidSides(rotatedCells[i].shape);

            foreach (var (offset, mySide, neighborSide) in AdjacencyDirections)
            {
                if (!mySolidSides.HasFlag(mySide)) continue; // this side is a hypotenuse — nothing can attach through it

                if (!cells.TryGetValue(targets[i] + offset, out var neighbor)) continue;

                var neighborSolidSides = BlockDefinition.GetSolidSides(neighbor.GetCellShape(targets[i] + offset));
                if (neighborSolidSides.HasFlag(neighborSide)) return true; // genuine solid-to-solid contact
            }
        }

        return false;
    }

    /// <summary>Whether a non-destroyed Engine currently occupies this absolute cell.</summary>
    private bool IsEngineAt(Vector2Int cell)
        => cells.TryGetValue(cell, out var m) && m.type == ModuleType.Engine && !m.IsDestroyed;

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
            if (cells.ContainsKey(new Vector2Int(cell.x, y))) return true;
        return false;
    }

    /// <summary>The 4 grid directions, each paired with which CellSides flag they touch on the
    /// departing cell and on the neighboring cell — e.g. moving Up leaves via that cell's Top side
    /// and arrives at the neighbor's Bottom side. Used by CanPlace's per-side adjacency check.</summary>
    private static readonly (Vector2Int offset, CellSides mySide, CellSides neighborSide)[] AdjacencyDirections =
    {
        (Vector2Int.up,    CellSides.Top,    CellSides.Bottom),
        (Vector2Int.down,  CellSides.Bottom, CellSides.Top),
        (Vector2Int.left,  CellSides.Left,   CellSides.Right),
        (Vector2Int.right, CellSides.Right,  CellSides.Left),
    };

    // ---------- Placement ----------

    public ShipModule PlaceBlock(BlockDefinition definition, Vector2Int anchor, List<BlockCell> rotatedCells, int rotationSteps)
    {
        var localShape = BlockDefinition.Offsets(rotatedCells);
        var targets = Offset(anchor, localShape);
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
        // BlockDefinition it belongs to is exactly what broke save/load for some blocks once already.
        module.moduleId = definition.id;
        module.buildCost = definition.buildCost;
        module.occupiedCells = targets;     // pure grid bookkeeping — independent of any transform
        module.cellShapes = BlockDefinition.Shapes(rotatedCells); // same index order as occupiedCells
        module.anchorCell = anchor;
        module.rotationSteps = rotationSteps;
        module.ApplyViewMode(CurrentViewMode);
        module.ApplySortingOrders(roofSortingOrder, topSortingOrder);

        // Crew Engineer bonus — player ships only, applied once here so it's baked into a freshly
        // placed block's maxHP/CurrentHP rather than recomputed live (matches how the Gunner/Helmsman/
        // Captain bonuses are read fresh each use elsewhere, just simpler to bake for a static stat).
        if (identity.faction == Faction.Player && GameDataManager.Instance != null)
            module.ApplyMaxHpMultiplier(GameDataManager.Instance.GetEngineerMaxHpMultiplier());

        // Only the VISUAL child rotates/repositions — around its own point, computed fresh from
        // the already-rotated local shape, so it always lines up with occupiedCells exactly.
        var (centroidCells, sizeCells) = ComputeLocalFootprint(localShape);
        // Never the block's own root — the root stays exactly on its anchor cell (AnchorToWorld above),
        // so a prefab whose Visual Root is unassigned/destroyed (Awake then falls back to the root itself)
        // must not have this offset applied to it, or the block would jump to the ship's origin.
        if (module.visualRoot != null && module.visualRoot != instance.transform)
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

        foreach (var cell in targets) cells[cell] = module;

        // Identity first, grid second: when the LAST block dies, the identity must already know the
        // ship is destroyed by the time HandleBlockDestroyed runs, so CheckShipDisabled can't also
        // report the same ship as merely "disabled".
        identity.RegisterBlock(module);
        module.OnDestroyed += HandleBlockDestroyed;

        OnShipChanged?.Invoke();
        return module;
    }

    /// <summary>A block died in combat (HP hit zero): frees its cells and re-checks whether the ship can
    /// still function. Nothing else is affected — neighbors are independent blocks with their own HP.</summary>
    private void HandleBlockDestroyed(ShipModule module)
    {
        FreeCells(module);
        OnShipChanged?.Invoke();

        if (module.type == ModuleType.Cockpit || module.type == ModuleType.Generator)
            CheckShipDisabled();
    }

    /// <summary>Losing the last Cockpit or last power-generating block leaves a ship unable to function
    /// even with most of its blocks intact — see ShipIdentity.NotifyDisabled/OnShipDisabled.</summary>
    private void CheckShipDisabled()
    {
        bool hasGenerator = Blocks.Any(m => m.type == ModuleType.Generator && !m.IsDestroyed);
        if (!HasCockpit || !hasGenerator) identity.NotifyDisabled();
    }

    private void FreeCells(ShipModule module)
    {
        foreach (var cell in module.occupiedCells)
            if (cells.TryGetValue(cell, out var occupant) && occupant == module) cells.Remove(cell);
    }

    /// <summary>
    /// Removes a block cleanly (editor deletion, not combat destruction — no explosion, no
    /// TakeDamage/OnDestroyed event).
    /// </summary>
    public void RemoveBlock(ShipModule module)
    {
        if (module == null) return;

        identity.UnregisterBlock(module);
        module.OnDestroyed -= HandleBlockDestroyed;
        FreeCells(module);
        Destroy(module.gameObject);

        OnShipChanged?.Invoke();
    }

    /// <summary>The block occupying a given grid cell, or null if it's empty.</summary>
    public ShipModule GetBlockAt(Vector2Int cell) => cells.GetValueOrDefault(cell);

    /// <summary>
    /// Finds whatever block sits at a world position WITHOUT deleting it. Used by the editor's Delete
    /// mode to know what a confirmation popup would be deleting before the player commits to it.
    /// Returns null if the cell is empty.
    /// </summary>
    public ShipModule FindDeletableAt(Vector3 worldPosition) => GetBlockAt(WorldToGrid(worldPosition));

    /// <summary>Deletes a specific block found via FindDeletableAt.</summary>
    public void DeleteBlock(ShipModule target) => RemoveBlock(target);

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

        foreach (var m in Blocks) m.ApplyViewMode(mode);

        // Safety net only: the grid overlay may not survive a trip back to the menu preview. Showing
        // it while building is owned by BuildModeController.
        if (mode != ShipViewMode.Building) HideGeneralGrid();

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

    // ---------- Build grid (whole board) ----------

    /// <summary>Call when entering the builder or switching palette tab.</summary>
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

    /// <summary>Call when leaving the builder.</summary>
    public void HideGeneralGrid()
    {
        if (generalGridRoot != null) Destroy(generalGridRoot.gameObject);
        generalGridRoot = null;
    }

    // ---------- Aggregate stats ----------

    public float ComputeTotalMass() => Blocks.Sum(m => m.mass);

    /// <summary>Total HP capacity across every placed block (unlike ComputeTotalArmor/
    /// ComputeShieldStrength, which are per-category).</summary>
    public float ComputeTotalHP() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.maxHP);

    /// <summary>Sum of CURRENT (not max) HP across every surviving block. Unlike ComputeTotalHP (a
    /// capacity stat for the builder's stats panel), this reflects actual damage taken — e.g. for
    /// star-rating a battle by how much HP survived it, see BattleOutcomeController.</summary>
    public float ComputeCurrentTotalHP() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.CurrentHP);

    /// <summary>Net energy — positive means surplus, negative means the ship is over budget.</summary>
    public float ComputeEnergyBalance() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.energyDelta);

    /// <summary>Total armor HP capacity — Armor-category blocks only.</summary>
    public float ComputeTotalArmor()
        => Blocks.Where(m => m.type == ModuleType.Armor && !m.IsDestroyed).Sum(m => m.maxHP);

    /// <summary>Total shield capacity — the sum of every surviving shield's own Capacity (how much damage
    /// they can absorb from full), not the shield blocks' own HP.</summary>
    public float ComputeShieldStrength()
        => Blocks.OfType<ShieldModule>().Where(s => !s.IsDestroyed).Sum(s => s.capacity);

    /// <summary>Current (not max) HP across surviving Armor blocks — e.g. for a battle HUD gauge
    /// showing remaining armor rather than total armor capacity.</summary>
    public float ComputeCurrentArmorHP()
        => Blocks.Where(m => m.type == ModuleType.Armor && !m.IsDestroyed).Sum(m => m.CurrentHP);

    /// <summary>Damage all surviving shields can still absorb right now — the live counterpart of
    /// ComputeShieldStrength, for the HUD's shield gauge.</summary>
    public float ComputeShieldReserve()
        => Blocks.OfType<ShieldModule>().Where(s => !s.IsDestroyed).Sum(s => s.Reserve);

    /// <summary>Total damage-per-second across every non-destroyed weapon.</summary>
    public float ComputeFirepower()
        => Blocks.OfType<WeaponModule>().Where(w => !w.IsDestroyed).Sum(w => w.DamagePerSecond);

    /// <summary>Total thrust from every non-destroyed engine (EngineModule.GetThrust() already returns 0 when destroyed).</summary>
    public float ComputeEnginePower() => Blocks.OfType<EngineModule>().Sum(e => e.GetThrust());

    /// <summary>Total energy output from generators — the positive half of ComputeEnergyBalance, split out.</summary>
    public float ComputeEnergyGeneration()
        => Blocks.Where(m => !m.IsDestroyed && m.energyDelta > 0f).Sum(m => m.energyDelta);

    /// <summary>Total energy draw from weapons/engines/shields, as a positive number — the negative
    /// half of ComputeEnergyBalance, split out and sign-flipped so it reads as a plain "cost".</summary>
    public float ComputeEnergyConsumption()
        => Blocks.Where(m => !m.IsDestroyed && m.energyDelta < 0f).Sum(m => -m.energyDelta);

    // ---------- Save / load / procedural spawn (shared by player editor & enemy spawner) ----------

    public ShipLayout ExportLayout()
    {
        var layout = new ShipLayout();
        foreach (var m in Blocks)
            layout.blocks.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.anchorCell.x, anchorY = m.anchorCell.y, rotationSteps = m.rotationSteps });
        return layout;
    }

    /// <summary>Destroys every placed block and resets the grid — call before loading a saved layout.</summary>
    public void Clear()
    {
        foreach (var m in Blocks.ToList())
        {
            if (m == null) continue;
            identity.UnregisterBlock(m);
            m.OnDestroyed -= HandleBlockDestroyed;
            Destroy(m.gameObject);
        }

        cells.Clear();
        OnShipChanged?.Invoke();
    }

    /// <summary>Builds a full ship from a saved layout with no player input — used for restoring a saved
    /// player ship. Placement rules are NOT re-checked: a saved layout is trusted.</summary>
    public void BuildFromLayout(ShipLayout layout, BlockDatabase db, Faction faction)
    {
        Clear();

        identity.faction = faction;
        identity.ApplyTagToRoot();

        if (layout == null) return;

        // Legacy saves kept hull/armor and modules in two separate lists; both simply load as plain
        // blocks now (hull block ids no longer exist and are skipped with a warning).
        foreach (var entry in layout.hull.Concat(layout.modules).Concat(layout.blocks))
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            PlaceFromDefinition(def, new Vector2Int(entry.anchorX, entry.anchorY), entry.rotationSteps);
        }
    }

    /// <summary>Builds a full ship with each block's BlockDefinition given DIRECTLY rather than looked up
    /// by id — used for enemy ships (see EnemyShip), whose blocks don't need to
    /// be registered in any BlockDatabase at all. Placement rules are NOT re-checked, same as
    /// BuildFromLayout: a hand-authored ship is trusted. Entries with no block assigned are skipped.</summary>
    public void BuildFromDefinitions(IEnumerable<(BlockDefinition block, Vector2Int anchor, int rotationSteps)> placements, Faction faction)
    {
        Clear();

        identity.faction = faction;
        identity.ApplyTagToRoot();

        if (placements == null) return;

        foreach (var (block, anchor, rotationSteps) in placements)
        {
            if (block == null) continue;
            PlaceFromDefinition(block, anchor, rotationSteps);
        }
    }

    private void PlaceFromDefinition(BlockDefinition def, Vector2Int anchor, int rotationSteps)
    {
        var rotatedCells = BlockDefinition.RotateCells(def.cells, rotationSteps);
        PlaceBlock(def, anchor, rotatedCells, rotationSteps);
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

    /// <summary>Every block of the ship.</summary>
    public List<Entry> blocks = new();

    // Legacy: saves and enemy templates made before hull was removed stored structure and modules in
    // two separate lists. Still READ by ShipGrid.BuildFromLayout, never written anymore.
    public List<Entry> hull = new();
    public List<Entry> modules = new();

    public int TotalEntries => blocks.Count + hull.Count + modules.Count;
}
