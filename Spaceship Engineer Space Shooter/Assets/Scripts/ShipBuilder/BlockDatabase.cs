using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Block Database", fileName = "BlockDatabase")]
public class BlockDatabase : ScriptableObject
{
    public List<BlockDefinition> allBlocks = new();

    private Dictionary<string, BlockDefinition> lookup;

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

    public List<BlockDefinition> GetWeaponsByClass(WeaponClass weaponClass)
    {
        var result = new List<BlockDefinition>();
        foreach (var b in allBlocks)
            if (b != null && b.category == BlockCategory.Weapon && b.weaponClass == weaponClass) result.Add(b);
        return result;
    }
}
