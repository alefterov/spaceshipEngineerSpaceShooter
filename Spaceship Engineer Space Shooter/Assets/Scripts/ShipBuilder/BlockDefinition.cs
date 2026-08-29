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

    [Header("Shape")]
    [Tooltip("Cell offsets relative to (0,0) — the anchor/pivot cell, which is always the root block. " +
             "Include (0,0) itself in the list. 1 entry = 1x1. Add more for 2/3/4-cell shapes.")]
    public List<Vector2Int> cells = new() { Vector2Int.zero };

    public bool IsStructural => category == BlockCategory.Hull || category == BlockCategory.Armor;

    /// <summary>
    /// Rotates a shape 90° clockwise `steps` times AROUND THE FIXED PIVOT (0,0) — the anchor/root cell.
    /// Deliberately does NOT re-normalize afterwards: the pivot must stay at the same logical point
    /// every time, otherwise reloading a saved rotated block would place it on different cells than
    /// when it was originally built, causing overlaps or a broken layout.
    /// </summary>
    public static List<Vector2Int> RotateCells(List<Vector2Int> cells, int steps)
    {
        var result = new List<Vector2Int>(cells);
        steps = ((steps % 4) + 4) % 4;

        for (int s = 0; s < steps; s++)
            for (int i = 0; i < result.Count; i++)
                result[i] = new Vector2Int(result[i].y, -result[i].x);

        return result;
    }
}

public enum BlockCategory
{
    Hull,
    Armor,
    Weapon,
    Engine,
    Generator,
    Shield
}