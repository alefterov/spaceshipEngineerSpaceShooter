using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Registry of all TechDefinitions in the game — mirrors BlockDatabase. Used by tech-tree UI to
/// list/order each category's chain, and by BuildPaletteUI to look up whether a block is gated
/// behind an unresearched technology.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Tech Database", fileName = "TechDatabase")]
public class TechDatabase : ScriptableObject
{
    public List<TechDefinition> allTechs = new();

    private Dictionary<string, TechDefinition> lookup;

    private void EnsureLookup()
    {
        if (lookup != null) return;
        lookup = new Dictionary<string, TechDefinition>();
        foreach (var t in allTechs)
        {
            if (t == null) continue;
            if (!lookup.TryAdd(t.id, t))
                Debug.LogWarning($"Duplicate tech id '{t.id}' in TechDatabase.");
        }
    }

    public TechDefinition GetById(string id)
    {
        EnsureLookup();
        return lookup.GetValueOrDefault(id);
    }

    /// <summary>A category's chain, ordered from the first tech (no prerequisite) to the last.</summary>
    public List<TechDefinition> GetByCategory(TechCategory category)
        => allTechs.Where(t => t != null && t.category == category)
                   .OrderBy(ChainDepth)
                   .ToList();

    /// <summary>Position in its category's chain — 0 for the first tech (no prerequisite), 1 for the
    /// one after it, etc. Used to order a chain for display without a separate explicit index field.</summary>
    public int ChainDepth(TechDefinition tech)
    {
        int depth = 0;
        var current = tech;
        while (current != null && current.prerequisite != null)
        {
            depth++;
            current = current.prerequisite;
            if (depth > 1000)
            {
                Debug.LogError($"Tech chain starting at '{tech.id}' looks circular — prerequisite loop.");
                break;
            }
        }
        return depth;
    }

    /// <summary>Finds which tech (if any) unlocks the given block — used to gate the build palette.</summary>
    public TechDefinition GetTechForBlock(BlockDefinition block)
        => block == null ? null : allTechs.FirstOrDefault(t => t != null && t.unlockedBlock == block);
}
