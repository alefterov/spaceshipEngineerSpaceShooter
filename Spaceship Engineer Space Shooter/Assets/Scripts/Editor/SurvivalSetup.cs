using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Ship Builder > Create Survival Definition: makes (or refreshes) Assets/Data/Survival/SurvivalDefinition.asset with every enemy
/// ship of Prefabs/Enemies except the bosses, sorted from the weakest to the strongest. Strength = the square root of (the HP of all its
/// blocks x the damage per second of all its weapons) — a rough measure: reorder the list by hand if a ship feels out of place (running the
/// menu again sorts it anew). If the battle scene is open, the definition is also handed to its EnemySpawner.
/// </summary>
public static class SurvivalSetup
{
    private const string Folder = "Assets/Data/Survival";
    private const string ShipsFolder = "Assets/Prefabs/Enemies";

    [MenuItem("Tools/Ship Builder/Create Survival Definition")]
    public static void CreateDefinition()
    {
        var ships = new List<(GameObject prefab, float strength)>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ShipsFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null || prefab.name.Contains("Boss")) continue;
            if (!prefab.TryGetComponent<EnemyShip>(out var ship)) continue; // meteors and the like

            ships.Add((prefab, Strength(ship)));
        }

        if (ships.Count == 0)
        {
            Debug.LogWarning($"SurvivalSetup: no enemy ships found in {ShipsFolder}.");
            return;
        }

        ApplyClassRanking(ships);

        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/SurvivalDefinition.asset";
        var definition = AssetDatabase.LoadAssetAtPath<SurvivalDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SurvivalDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        definition.ships = ships.OrderBy(s => s.strength).Select(s => s.prefab).ToList();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();

        var spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        if (spawner != null && spawner.survival != definition)
        {
            Undo.RecordObject(spawner, "Assign Survival Definition");
            spawner.survival = definition;
            EditorUtility.SetDirty(spawner);
        }

        Debug.Log("SurvivalSetup: survival ships, weakest first:\n" +
                  string.Join("\n", ships.OrderBy(s => s.strength).Select((s, i) => $"{i + 1}. {s.prefab.name}  (strength {s.strength:0})")));
        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }

    /// <summary>The specialised ships, weakest class first (by their prefab names): every ship of a class counts as stronger than every
    /// ship of the classes before it — the formula underrates plasma, whose lightning hits several blocks at once. Within a class the
    /// computed order stays.</summary>
    private static readonly string[] ClassOrder = { "RammingShip", "MissileShip", "LaserShip", "PlasmaShip" };

    private static void ApplyClassRanking(List<(GameObject prefab, float strength)> ships)
    {
        float floor = 0f;
        foreach (string shipClass in ClassOrder)
        {
            var members = Enumerable.Range(0, ships.Count)
                .Where(i => ships[i].prefab.name.Contains(shipClass))
                .OrderBy(i => ships[i].strength)
                .ToList();

            for (int rank = 0; rank < members.Count; rank++)
            {
                int i = members[rank];
                ships[i] = (ships[i].prefab, Mathf.Max(ships[i].strength, floor + 4f * (rank + 1)));
            }

            if (members.Count > 0) floor = members.Max(i => ships[i].strength);
        }
    }

    private static float Strength(EnemyShip ship)
    {
        float hp = 0f, dps = 0f;
        foreach (var placement in ship.blocks)
        {
            var module = placement.block != null && placement.block.prefab != null ? placement.block.prefab.GetComponent<ShipModule>() : null;
            if (module == null) continue;

            hp += module.maxHP;
            if (module is WeaponModule weapon) dps += weapon.NominalDamagePerSecond; // each weapon kind's own formula (beams, plasma balls...)
        }

        // How long it survives x how hard it hits, brought back to a readable scale.
        return Mathf.Sqrt(hp * Mathf.Max(1f, dps));
    }
}
