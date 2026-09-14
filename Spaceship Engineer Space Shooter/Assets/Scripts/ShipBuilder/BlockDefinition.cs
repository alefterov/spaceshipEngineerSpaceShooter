using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-only definition of a placeable block (hull piece or functional module).
/// One asset per block type — drives both the bottom UI palette and the ghost preview.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Block Definition", fileName = "NewBlock")]
public class BlockDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "hull_1x1";
    public string displayName = "Hull Block";
    public Sprite icon;                 // shown in the bottom UI list
    public GameObject prefab;           // must have a ShipModule (or subclass) component

    [Header("Category — determines which build mode this block appears in")]
    public BlockCategory category = BlockCategory.Hull;

    [Header("Economy")]
    [Tooltip("Credits required to build one of this block. Dismantling refunds a fraction of this " +
             "(see GhostBlockController.dismantleRefundFraction) — not the full amount.")]
    public int buildCost = 10;

    [Header("Shape")]
    [Tooltip("Cells this block occupies. Offset is relative to (0,0) — the anchor/pivot cell, which " +
             "is always the root block and must be included. Shape is Square for a normal cell, or a " +
             "triangle named by where its right angle sits (see CellShape) — its hypotenuse then faces " +
             "the two OTHER sides, and nothing can attach to the block through those. 1 entry = 1x1.")]
    public List<BlockCell> cells = new() { new BlockCell { offset = Vector2Int.zero, shape = CellShape.Square } };

    // Cockpit deliberately excluded — it's a functional module (sits on hull, like Engine/Generator),
    // not a structural piece. GetFunctionalBlocks() relies on that to include it in the Modules list.
    public bool IsStructural => category == BlockCategory.Hull || category == BlockCategory.Armor;

    /// <summary>Just the offsets, e.g. for code that only needs geometry (bounding box, grid occupancy)
    /// and doesn't care about shape.</summary>
    public static List<Vector2Int> Offsets(List<BlockCell> cells) => cells.ConvertAll(c => c.offset);

    /// <summary>Just the shapes, same index order as `cells` — e.g. to hand off to ShipModule.cellShapes.</summary>
    public static List<CellShape> Shapes(List<BlockCell> cells) => cells.ConvertAll(c => c.shape);

    /// <summary>
    /// Rotates a shape 90° clockwise `steps` times AROUND THE FIXED PIVOT (0,0) — the anchor/root cell.
    /// Deliberately does NOT re-normalize afterwards: the pivot must stay at the same logical point
    /// every time, otherwise reloading a saved rotated block would place it on different cells than
    /// when it was originally built, causing overlaps or a broken layout. Rotates each cell's shape
    /// right along with its offset, so a triangle's hypotenuse keeps facing the correct side.
    /// </summary>
    public static List<BlockCell> RotateCells(List<BlockCell> cells, int steps)
    {
        steps = ((steps % 4) + 4) % 4;

        var result = new List<BlockCell>(cells.Count);
        foreach (var cell in cells)
        {
            var offset = cell.offset;
            for (int s = 0; s < steps; s++) offset = new Vector2Int(offset.y, -offset.x);
            result.Add(new BlockCell { offset = offset, shape = RotateCellShape(cell.shape, steps) });
        }
        return result;
    }

    // Cycle order per rotation step: BottomLeft -> BottomRight -> TopRight -> TopLeft -> BottomLeft.
    // Direction confirmed empirically against the actual visual rotation (Quaternion.Euler(0,0,90*steps)
    // on the sprite) — it runs opposite to the grid-offset rotation formula above, which only rotates
    // CELL POSITIONS, not sprites, so there's no reason the two need to share a sense of direction.
    private static readonly CellShape[] TriangleRotationOrder =
    {
        CellShape.TriangleBottomLeft, CellShape.TriangleBottomRight, CellShape.TriangleTopRight, CellShape.TriangleTopLeft
    };

    public static CellShape RotateCellShape(CellShape shape, int steps)
    {
        if (shape == CellShape.Square) return CellShape.Square;

        steps = ((steps % 4) + 4) % 4;
        int index = System.Array.IndexOf(TriangleRotationOrder, shape);
        return TriangleRotationOrder[(index + steps) % 4];
    }

    /// <summary>Which of a cell's 4 sides are solid material a neighboring block can actually attach
    /// to — all 4 for Square, only the two legs for a triangle (its hypotenuse is never attachable,
    /// see ShipGrid.CanPlaceHull).</summary>
    public static CellSides GetSolidSides(CellShape shape) => shape switch
    {
        CellShape.TriangleBottomLeft => CellSides.Bottom | CellSides.Left,
        CellShape.TriangleBottomRight => CellSides.Bottom | CellSides.Right,
        CellShape.TriangleTopLeft => CellSides.Top | CellSides.Left,
        CellShape.TriangleTopRight => CellSides.Top | CellSides.Right,
        _ => CellSides.All,
    };
}

/// <summary>One cell of a BlockDefinition's shape — where it sits (relative to the block's anchor) and
/// what silhouette it has there.</summary>
[System.Serializable]
public struct BlockCell
{
    public Vector2Int offset;
    public CellShape shape;
}

public enum BlockCategory
{
    Hull,
    Armor,
    Weapon,
    Engine,
    Generator,
    Shield,
    Cockpit
}

/// <summary>A grid cell's silhouette. Triangle orientations are named by which corner holds the right
/// angle — that corner's two edges are exactly the two solid/attachable sides (see GetSolidSides); the
/// hypotenuse runs between the other two corners and is never attachable.</summary>
public enum CellShape
{
    Square,
    TriangleBottomLeft,
    TriangleBottomRight,
    TriangleTopLeft,
    TriangleTopRight,
}

/// <summary>The 4 cardinal sides of a grid cell, in ship-local space (Bottom/Top = -Y/+Y before the
/// block's own rotation is applied — rotation is handled by rotating the CellShape itself, see
/// BlockDefinition.RotateCellShape).</summary>
[System.Flags]
public enum CellSides
{
    None = 0,
    Bottom = 1,
    Right = 2,
    Top = 4,
    Left = 8,
    All = Bottom | Right | Top | Left,
}