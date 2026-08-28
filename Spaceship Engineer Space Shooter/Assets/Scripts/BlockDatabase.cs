using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry of all BlockDefinitions in the game. Used by the bottom UI palette
/// (filtered by category) and by ShipGrid.BuildFromLayout (id lookup for enemy ships).
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Block Database", fileName = "BlockDatabase")]
public class BlockDatabase : ScriptableObject
{
    public List<BlockDefinition> allBlocks = new();

    private Dictionary<string, BlockDefinition> lookup;

    private void EnsureLookup()
    {
        if (lookup != null) return;
        lookup = new Dictionary<string, BlockDefinition>();
        foreach (var b in allBlocks)
        {
            if (b == null) continue;
            if (!lookup.TryAdd(b.id, b))
                Debug.LogWarning($"Duplicate block id '{b.id}' in BlockDatabase.");
        }
    }

    public BlockDefinition GetById(string id)
    {
        EnsureLookup();
        return lookup.GetValueOrDefault(id);
    }

    public List<BlockDefinition> GetByCategory(BlockCategory category)
    {
        var result = new List<BlockDefinition>();
        foreach (var b in allBlocks)
            if (b != null && b.category == category) result.Add(b);
        return result;
    }

    /// <summary>All blocks belonging to the "Hull build mode" (Hull + Armor).</summary>
    public List<BlockDefinition> GetStructuralBlocks()
    {
        var result = new List<BlockDefinition>();
        foreach (var b in allBlocks)
            if (b != null && b.IsStructural) result.Add(b);
        return result;
    }

    /// <summary>All functional blocks belonging to the "Module build mode".</summary>
    public List<BlockDefinition> GetFunctionalBlocks()
    {
        var result = new List<BlockDefinition>();
        foreach (var b in allBlocks)
            if (b != null && !b.IsStructural) result.Add(b);
        return result;
    }
}
