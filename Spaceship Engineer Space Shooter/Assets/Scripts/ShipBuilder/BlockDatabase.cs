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

    // Without this, adding a block in the Inspector after GetById has already run once this Editor
    // session (e.g. between Play sessions with "Reload Domain" off) leaves the cached dictionary
    // stale — the new block sits right there in allBlocks yet GetById reports it unknown. OnValidate
    // only ever fires in the Editor, so this is a no-op in a build.
    private void OnValidate() => lookup = null;

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

    /// <summary>Weapon blocks of one family — for the Weapons tab's sub-tabs.</summary>
    public List<BlockDefinition> GetWeaponsByClass(WeaponClass weaponClass)
    {
        var result = new List<BlockDefinition>();
        foreach (var b in allBlocks)
            if (b != null && b.category == BlockCategory.Weapon && b.weaponClass == weaponClass) result.Add(b);
        return result;
    }
}
