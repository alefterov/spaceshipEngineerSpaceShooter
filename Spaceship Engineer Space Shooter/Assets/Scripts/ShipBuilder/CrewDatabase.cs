using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Crew Database", fileName = "CrewDatabase")]
public class CrewDatabase : ScriptableObject
{
    public List<CrewMemberDefinition> allMembers = new();

    [Tooltip("Copies a brand-new save starts with, each at rank 1 / level 1, sitting unassigned in the " +
             "crew collection. List a definition twice to start with two copies (handy for testing merges). " +
             "Granted once per save.")]
    public List<CrewMemberDefinition> starterCrew = new();

    private Dictionary<string, CrewMemberDefinition> lookup;

    private void OnValidate() => lookup = null;

    public CrewMemberDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (lookup == null)
        {
            lookup = new Dictionary<string, CrewMemberDefinition>();
            foreach (var m in allMembers)
            {
                if (m == null) continue;
                if (!lookup.TryAdd(m.id, m)) Debug.LogWarning($"Duplicate crew member id '{m.id}' in CrewDatabase.");
            }
        }

        return lookup.GetValueOrDefault(id);
    }
}
