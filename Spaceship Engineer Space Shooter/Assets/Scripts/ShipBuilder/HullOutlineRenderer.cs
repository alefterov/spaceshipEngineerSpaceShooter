using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Draws a thick black outline (a stylized "wall cross-section") hugging the exterior boundary
/// of the ship's Hull cells specifically — Armor plating is deliberately excluded (see
/// ShipGrid.HullOnlyCellPositions), so an armor-plated cell reads as "not hull" to this outline
/// the same way empty space would, even though it's structurally on the same hullCells layer.
/// Purely cosmetic — listens to ShipGrid.OnHullChanged and redraws automatically whenever hull
/// pieces are placed or removed. Attach to the same GameObject as ShipGrid.
///
/// ALGORITHM: for every traced cell, each of its 4 sides that borders a cell NOT in the traced set
/// is a boundary edge. Each edge is emitted in a fixed direction per side (bottom: left-to-right,
/// right: bottom-to-top, top: right-to-left, left: top-to-bottom), which keeps the shape's interior
/// consistently on the LEFT of the direction of travel. That's what makes edges from different
/// cells chain head-to-tail into closed loops automatically — including multiple loops if the hull
/// has a hole in the middle, or splits into separate islands after a deletion.
/// </summary>
[RequireComponent(typeof(ShipGrid))]
public class HullOutlineRenderer : MonoBehaviour
{
    [Header("Outline (\"wall\")")]
    [Tooltip("Assign a simple unlit/sprite material (e.g. URP's Sprite-Unlit-Default). " +
             "LineRenderer renders as solid magenta without one.")]
    public Material lineMaterial;
    public Color outlineColor = Color.black;
    public float outlineWidth = 0.12f;
    public string sortingLayerName = "Default";
    [Tooltip("Draw order relative to the hull sprites — higher draws on top, so the outline reads as a border.")]
    public int sortingOrder = 5;

    private ShipGrid grid;
    private Transform outlineRoot;

    private void Awake() => grid = GetComponent<ShipGrid>();

    private void OnEnable()
    {
        grid.OnHullChanged += Regenerate;
        Regenerate(); // covers hull already built before this component was enabled (e.g. loaded ship)
    }

    private void OnDisable() => grid.OnHullChanged -= Regenerate;

    private void Regenerate()
    {
        if (outlineRoot != null) Destroy(outlineRoot.gameObject);
        outlineRoot = null;

        var loops = TraceBoundaryLoops(grid.HullOnlyCellPositions);
        if (loops.Count == 0) return;

        var rootObj = new GameObject("HullOutline");
        rootObj.transform.SetParent(transform, false);
        outlineRoot = rootObj.transform;

        int loopIndex = 0;
        foreach (var loop in loops)
        {
            // Each traced loop's last corner duplicates its first (closed) — drop it, LineRenderer.loop
            // takes care of connecting the last point back to the first.
            var worldPoints = loop.Take(loop.Count - 1).Select(c => grid.CornerToWorld(c)).ToList();
            if (worldPoints.Count < 3) continue;

            DrawOutlineLoop(worldPoints, loopIndex++);
        }
    }

    private void DrawOutlineLoop(List<Vector3> worldPoints, int index)
    {
        var lineObj = new GameObject($"OutlineLoop_{index}");
        lineObj.transform.SetParent(outlineRoot, false);

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = worldPoints.Count;
        lr.SetPositions(worldPoints.ToArray());
        lr.startWidth = lr.endWidth = outlineWidth;
        lr.numCornerVertices = 0; // sharp corners — matches the blocky grid silhouette
        lr.numCapVertices = 0;
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;
        if (lineMaterial != null) lr.material = lineMaterial;
        lr.startColor = lr.endColor = outlineColor;
    }

    /// <summary>
    /// Traces the boundary of a set of grid cells into one or more closed loops of grid-CORNER
    /// coordinates (each loop's last point duplicates its first). General-purpose: correctly
    /// handles a hull with a hole in it, or one that has split into disconnected islands.
    /// </summary>
    private static List<List<Vector2Int>> TraceBoundaryLoops(IEnumerable<Vector2Int> cells)
    {
        var cellSet = new HashSet<Vector2Int>(cells);
        var edgesFrom = new Dictionary<Vector2Int, List<Vector2Int>>();

        void AddEdge(Vector2Int start, Vector2Int end)
        {
            if (!edgesFrom.TryGetValue(start, out var list))
                edgesFrom[start] = list = new List<Vector2Int>();
            list.Add(end);
        }

        foreach (var c in cellSet)
        {
            if (!cellSet.Contains(c + Vector2Int.down))  AddEdge(new(c.x, c.y),         new(c.x + 1, c.y));     // bottom
            if (!cellSet.Contains(c + Vector2Int.right)) AddEdge(new(c.x + 1, c.y),     new(c.x + 1, c.y + 1)); // right
            if (!cellSet.Contains(c + Vector2Int.up))    AddEdge(new(c.x + 1, c.y + 1), new(c.x, c.y + 1));     // top
            if (!cellSet.Contains(c + Vector2Int.left))  AddEdge(new(c.x, c.y + 1),     new(c.x, c.y));         // left
        }

        var loops = new List<List<Vector2Int>>();
        var used = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (var kvp in edgesFrom)
        {
            var start = kvp.Key;
            foreach (var firstEnd in kvp.Value)
            {
                if (used.Contains((start, firstEnd))) continue;

                var loop = new List<Vector2Int> { start };
                Vector2Int current = start, next = firstEnd;

                while (true)
                {
                    used.Add((current, next));
                    loop.Add(next);
                    if (next == start) break;

                    Vector2Int? found = null;
                    foreach (var candidate in edgesFrom[next])
                    {
                        if (!used.Contains((next, candidate))) { found = candidate; break; }
                    }
                    if (found == null) break; // malformed input — bail out rather than loop forever

                    current = next;
                    next = found.Value;
                }

                loops.Add(loop);
            }
        }

        return loops;
    }
}
