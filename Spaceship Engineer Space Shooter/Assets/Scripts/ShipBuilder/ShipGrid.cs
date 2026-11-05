using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public enum BuildMode { Cockpit, Generators, Engines, Repair, Armor, Weapons }

[RequireComponent(typeof(ShipIdentity))]
public class ShipGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 1f;

    [Header("Hangar upgrades")]
    [Tooltip("Extra columns / rows granted by hangar upgrades — bought separately. Set via SetHangarSize; " +
             "GameDataManager owns the persisted values and the research-point cost (see " +
             "GameDataManager.TryUpgradeHangarColumn / TryUpgradeHangarRow). AnchorToWorld/CornerToWorld always " +
             "center on the CURRENT effective size, so the grid stays centered as it grows — SetHangarSize " +
             "repositions already-placed blocks to match instead of leaving them behind.")]
    [SerializeField] private int hangarColumns;
    [SerializeField] private int hangarRows;

    public int HangarColumns => hangarColumns;
    public int HangarRows => hangarRows;
    public int GridWidth => width + hangarColumns;
    public int GridHeight => height + hangarRows;

    [Header("Destruction")]
    [Tooltip("Explosion effect for any block of this ship whose own prefab has none assigned (Explosion Effect " +
             "Prefab on the block), so every destroyed block — the player's and the enemy's alike — explodes. Optional.")]
    public GameObject fallbackExplosionEffect;

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
    [Tooltip("Weapons' moving parts overlap those of the weapons next to them. The higher a weapon is on the screen, the LOWER its layer, so " +
             "the ones further down are drawn over the ones above. Neighbouring weapons are this many sorting orders apart — keep it larger " +
             "than the spread of orders inside one weapon prefab.")]
    public int weaponLayerStep = 10;

    private Transform previewRoot;
    private readonly List<GameObject> previewPool = new();
    private Transform generalGridRoot;

    private readonly Dictionary<Vector2Int, ShipModule> cells = new();

    public event Action OnShipChanged;

    public IEnumerable<Vector2Int> OccupiedCellPositions => cells.Keys;

    public IEnumerable<ShipModule> Blocks => cells.Values.Distinct();

    public bool HasCockpit => Blocks.Any(m => m.type == ModuleType.Cockpit && !m.IsDestroyed);

    public bool AreAllBlocksConnected()
    {
        var all = Blocks.Where(b => b != null && !b.IsDestroyed).ToList();
        if (all.Count <= 1) return true;

        var visited = new HashSet<ShipModule> { all[0] };
        var queue = new Queue<ShipModule>();
        queue.Enqueue(all[0]);
        var offsets = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            foreach (var cell in block.occupiedCells)
            {
                foreach (var offset in offsets)
                {
                    if (cells.TryGetValue(cell + offset, out var other) && other != null && !other.IsDestroyed && visited.Add(other))
                        queue.Enqueue(other);
                }
            }
        }

        return visited.Count == all.Count;
    }

    public bool CanBeSaved => HasCockpit && AreAllBlocksConnected();

    public ShipViewMode CurrentViewMode { get; private set; } = ShipViewMode.Building;

    public event Action OnViewModeChanged;

    private ShipIdentity identity;
    private void Awake() => identity = GetComponent<ShipIdentity>();

    public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.x < GridWidth && cell.y >= 0 && cell.y < GridHeight;

    public void SetHangarSize(int columns, int rows)
    {
        if (hangarColumns == columns && hangarRows == rows) return;
        hangarColumns = columns;
        hangarRows = rows;

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

    public Vector3 AnchorToWorld(Vector2Int anchor)
    {
        float x = (anchor.x - GridWidth * 0.5f + 0.5f) * cellSize;
        float y = (anchor.y - GridHeight * 0.5f + 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    public Vector3 CornerToWorld(Vector2Int corner)
    {
        float x = (corner.x - GridWidth * 0.5f) * cellSize;
        float y = (corner.y - GridHeight * 0.5f) * cellSize;
        return transform.TransformPoint(new Vector3(x, y, 0f));
    }

    public bool TryGetShipCellBounds(out Vector2Int min, out Vector2Int maxExclusive)
    {
        if (cells.Count == 0) { min = maxExclusive = default; return false; }

        min = new Vector2Int(cells.Keys.Min(c => c.x), cells.Keys.Min(c => c.y));
        maxExclusive = new Vector2Int(cells.Keys.Max(c => c.x) + 1, cells.Keys.Max(c => c.y) + 1);
        return true;
    }

    public Vector3 GetShipWorldCenter()
        => TryGetShipCellBounds(out var min, out var maxExclusive)
            ? (CornerToWorld(min) + CornerToWorld(maxExclusive)) * 0.5f
            : transform.position;

    public Vector2 GetShipWorldSize()
    {
        if (!TryGetShipCellBounds(out var min, out var maxExclusive)) return Vector2.zero;
        Vector3 a = CornerToWorld(min);
        Vector3 b = CornerToWorld(maxExclusive);
        return new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

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

    public List<Vector2Int> GetOccupiedCells(Vector2Int anchor, List<Vector2Int> localShape) => Offset(anchor, localShape);

    public bool CanPlace(Vector2Int anchor, List<BlockCell> rotatedCells, BlockCategory category)
    {
        if (category == BlockCategory.Cockpit && HasCockpit) return false;

        var targets = Offset(anchor, BlockDefinition.Offsets(rotatedCells));

        foreach (var cell in targets)
        {
            if (!InBounds(cell)) return false;
            if (cells.ContainsKey(cell)) return false;
            if (IsEngineInColumnAbove(cell)) return false;
        }

        if (category == BlockCategory.Engine)
            foreach (var cell in targets)
                if (IsColumnBelowOccupied(cell)) return false;

        if (cells.Count == 0) return true;

        for (int i = 0; i < targets.Count; i++)
        {
            var mySolidSides = BlockDefinition.GetSolidSides(rotatedCells[i].shape);

            foreach (var (offset, mySide, neighborSide) in AdjacencyDirections)
            {
                if (!mySolidSides.HasFlag(mySide)) continue;

                if (!cells.TryGetValue(targets[i] + offset, out var neighbor)) continue;

                var neighborSolidSides = BlockDefinition.GetSolidSides(neighbor.GetCellShape(targets[i] + offset));
                if (neighborSolidSides.HasFlag(neighborSide)) return true;
            }
        }

        return false;
    }

    private bool IsEngineAt(Vector2Int cell)
        => cells.TryGetValue(cell, out var m) && m.type == ModuleType.Engine && !m.IsDestroyed;

    private bool IsEngineInColumnAbove(Vector2Int cell)
    {
        for (int y = cell.y + 1; y < GridHeight; y++)
            if (IsEngineAt(new Vector2Int(cell.x, y))) return true;
        return false;
    }

    private bool IsColumnBelowOccupied(Vector2Int cell)
    {
        for (int y = cell.y - 1; y >= 0; y--)
            if (cells.ContainsKey(new Vector2Int(cell.x, y))) return true;
        return false;
    }

    private static readonly (Vector2Int offset, CellSides mySide, CellSides neighborSide)[] AdjacencyDirections =
    {
        (Vector2Int.up,    CellSides.Top,    CellSides.Bottom),
        (Vector2Int.down,  CellSides.Bottom, CellSides.Top),
        (Vector2Int.left,  CellSides.Left,   CellSides.Right),
        (Vector2Int.right, CellSides.Right,  CellSides.Left),
    };

    private static float CenterY(ShipModule block) => (block.visualRoot != null ? block.visualRoot.position : block.transform.position).y;

    public void RefreshWeaponLayers()
    {
        var weapons = Blocks.OfType<WeaponModule>().Where(w => w != null && !w.IsDestroyed)
            .OrderByDescending(w => CenterY(w)).ToList();

        int rank = -1;
        float lastY = float.MaxValue;
        foreach (var weapon in weapons)
        {
            float y = CenterY(weapon);
            if (lastY - y > cellSize * 0.25f || rank < 0) rank++;
            lastY = y;
            weapon.SetLayerRank(rank, weaponLayerStep);
        }
    }

    public ShipModule PlaceBlock(BlockDefinition definition, Vector2Int anchor, List<BlockCell> rotatedCells, int rotationSteps)
    {
        var localShape = BlockDefinition.Offsets(rotatedCells);
        var targets = Offset(anchor, localShape);
        Vector3 rootWorldPos = AnchorToWorld(anchor);

        var instance = Instantiate(definition.prefab, rootWorldPos, Quaternion.identity, transform);
        instance.tag = identity.faction == Faction.Player ? "PlayerShip" : "EnemyShip";

        var module = instance.GetComponent<ShipModule>();
        if (module == null)
        {
            Debug.LogError($"Block prefab '{definition.prefab.name}' has no ShipModule component.");
            Destroy(instance);
            return null;
        }

        module.moduleId = definition.id;
        module.buildCost = definition.buildCost;
        module.occupiedCells = targets;
        module.cellShapes = BlockDefinition.Shapes(rotatedCells);
        module.anchorCell = anchor;
        module.rotationSteps = rotationSteps;
        module.ApplyViewMode(CurrentViewMode);
        module.ApplySortingOrders(roofSortingOrder, topSortingOrder);
        if (module.explosionEffectPrefab == null) module.explosionEffectPrefab = fallbackExplosionEffect;

        if (module is WeaponModule weapon) weapon.weaponClass = definition.weaponClass;

        if (module.type == ModuleType.Armor && identity.faction == Faction.Player && GameDataManager.Instance != null)
            module.ApplyMaxHpMultiplier(GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.ArmorEfficiency));

        var (centroidCells, sizeCells) = ComputeLocalFootprint(localShape);
        var (unrotatedCentroid, _) = ComputeLocalFootprint(BlockDefinition.Offsets(BlockDefinition.RotateCells(definition.cells, 0)));
        module.ApplyGridPose(new Vector3(centroidCells.x * cellSize, centroidCells.y * cellSize, 0f),
            new Vector3(unrotatedCentroid.x * cellSize, unrotatedCentroid.y * cellSize, 0f), rotationSteps);

        if (instance.TryGetComponent<BoxCollider2D>(out var box))
        {
            box.offset = new Vector2(centroidCells.x * cellSize, centroidCells.y * cellSize);
            box.size = new Vector2(sizeCells.x * cellSize, sizeCells.y * cellSize);
        }

        foreach (var cell in targets) cells[cell] = module;

        identity.RegisterBlock(module);
        module.OnDestroyed += HandleBlockDestroyed;

        RefreshWeaponLayers();
        OnShipChanged?.Invoke();
        return module;
    }

    private void HandleBlockDestroyed(ShipModule module)
    {
        FreeCells(module);
        OnShipChanged?.Invoke();

        if (module.type == ModuleType.Cockpit || module.type == ModuleType.Generator)
            CheckShipDisabled();
    }

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

    public void RemoveBlock(ShipModule module)
    {
        if (module == null) return;

        identity.UnregisterBlock(module);
        module.OnDestroyed -= HandleBlockDestroyed;
        FreeCells(module);
        Destroy(module.gameObject);

        OnShipChanged?.Invoke();
    }

    public void ReleaseBlock(ShipModule module)
    {
        if (module == null) return;

        identity.UnregisterBlock(module);
        module.OnDestroyed -= HandleBlockDestroyed;
        FreeCells(module);
    }

    public void NotifyBlocksReleased()
    {
        OnShipChanged?.Invoke();
        CheckShipDisabled();
    }

    public ShipModule GetBlockAt(Vector2Int cell) => cells.GetValueOrDefault(cell);

    public ShipModule FindDeletableAt(Vector3 worldPosition) => GetBlockAt(WorldToGrid(worldPosition));

    public void DeleteBlock(ShipModule target) => RemoveBlock(target);

    public bool TryDeleteAt(Vector3 worldPosition)
    {
        var target = FindDeletableAt(worldPosition);
        if (target == null) return false;
        DeleteBlock(target);
        return true;
    }

    public void SetViewMode(ShipViewMode mode)
    {
        CurrentViewMode = mode;

        foreach (var m in Blocks) m.ApplyViewMode(mode);

        if (mode != ShipViewMode.Building) HideGeneralGrid();

        OnViewModeChanged?.Invoke();
    }

    public void ShowPlacementPreview(List<Vector2Int> cells, Color color)
    {
        if (cellVisualPrefab == null) return;

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

    public void HideGeneralGrid()
    {
        if (generalGridRoot != null) Destroy(generalGridRoot.gameObject);
        generalGridRoot = null;
    }

    public float ComputeTotalMass() => Blocks.Sum(m => m.mass);

    public float ComputeTotalHP() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.maxHP);

    public float ComputeCurrentTotalHP() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.CurrentHP);

    public float ComputeEnergyBalance() => Blocks.Where(m => !m.IsDestroyed).Sum(m => m.EnergyPerSecondEstimate);

    public float ComputeTotalArmor()
        => Blocks.Where(m => m.type == ModuleType.Armor && !m.IsDestroyed).Sum(m => m.maxHP);

    public float ComputeShieldStrength()
        => Blocks.OfType<ShieldModule>().Where(s => !s.IsDestroyed).Sum(s => s.capacity);

    public float ComputeCurrentArmorHP()
        => Blocks.Where(m => m.type == ModuleType.Armor && !m.IsDestroyed).Sum(m => m.CurrentHP);

    public float ComputeShieldReserve()
        => Blocks.OfType<ShieldModule>().Where(s => !s.IsDestroyed).Sum(s => s.Reserve);

    public float ComputeFirepower()
        => Blocks.OfType<WeaponModule>().Where(w => !w.IsDestroyed).Sum(w => w.DamagePerSecond);

    public float ComputeEnginePower() => Blocks.OfType<EngineModule>().Sum(e => e.GetThrust());

    public float ComputeEnergyGeneration()
        => Blocks.Where(m => !m.IsDestroyed && m.EnergyPerSecondEstimate > 0f).Sum(m => m.EnergyPerSecondEstimate);

    public float ComputeEnergyConsumption()
        => Blocks.Where(m => !m.IsDestroyed && m.EnergyPerSecondEstimate < 0f).Sum(m => -m.EnergyPerSecondEstimate);
    public float ComputeMoveEnergyPerSecond()
        => Blocks.OfType<EngineModule>().Where(e => !e.IsDestroyed).Sum(e => Mathf.Abs(e.energyDelta));

    public ShipLayout ExportLayout()
    {
        var layout = new ShipLayout();
        foreach (var m in Blocks)
            layout.blocks.Add(new ShipLayout.Entry { blockId = m.moduleId, anchorX = m.anchorCell.x, anchorY = m.anchorCell.y, rotationSteps = m.rotationSteps });
        return layout;
    }

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

    public void BuildFromLayout(ShipLayout layout, BlockDatabase db, Faction faction)
    {
        Clear();

        identity.faction = faction;
        identity.ApplyTagToRoot();

        if (layout == null) return;

        foreach (var entry in layout.hull.Concat(layout.modules).Concat(layout.blocks))
        {
            var def = db.GetById(entry.blockId);
            if (def == null) { Debug.LogWarning($"Unknown block id '{entry.blockId}'"); continue; }
            PlaceFromDefinition(def, new Vector2Int(entry.anchorX, entry.anchorY), entry.rotationSteps);
        }
    }

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

    public List<Entry> blocks = new();

    public List<Entry> hull = new();
    public List<Entry> modules = new();

    public int TotalEntries => blocks.Count + hull.Count + modules.Count;
}
