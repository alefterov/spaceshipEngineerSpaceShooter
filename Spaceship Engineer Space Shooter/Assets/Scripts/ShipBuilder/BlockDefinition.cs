using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Block Definition", fileName = "NewBlock")]
public class BlockDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "armor_1x1";
    public string displayName = "Armor Block";
    public Sprite icon;
    public GameObject prefab;
    [Tooltip("Tick for blocks that only enemy ships use. They are never offered in the player's build palette " +
             "(the scroll view of the build panel), but enemy ships can still be built from them.")]
    public bool enemyOnly;

    [Header("Category — determines which build mode this block appears in")]
    public BlockCategory category = BlockCategory.Armor;
    [Tooltip("Only used when Category is Weapon — which sub-tab of the Weapons build tab this block appears in.")]
    public WeaponClass weaponClass = WeaponClass.Ballistic;

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

    public static List<Vector2Int> Offsets(List<BlockCell> cells) => cells.ConvertAll(c => c.offset);

    public static List<CellShape> Shapes(List<BlockCell> cells) => cells.ConvertAll(c => c.shape);

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

    public static CellSides GetSolidSides(CellShape shape) => shape switch
    {
        CellShape.TriangleBottomLeft => CellSides.Bottom | CellSides.Left,
        CellShape.TriangleBottomRight => CellSides.Bottom | CellSides.Right,
        CellShape.TriangleTopLeft => CellSides.Top | CellSides.Left,
        CellShape.TriangleTopRight => CellSides.Top | CellSides.Right,
        _ => CellSides.All,
    };
}

[System.Serializable]
public struct BlockCell
{
    public Vector2Int offset;
    public CellShape shape;
}

public enum WeaponClass
{
    Ballistic,
    Laser,
    Missile,
    Plasma
}

public enum BlockCategory
{
    Armor = 1,
    Weapon = 2,
    Engine = 3,
    Generator = 4,
    Shield = 5,
    Cockpit = 6,
    Repair = 7
}

public enum CellShape
{
    Square,
    TriangleBottomLeft,
    TriangleBottomRight,
    TriangleTopLeft,
    TriangleTopRight,
}

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
