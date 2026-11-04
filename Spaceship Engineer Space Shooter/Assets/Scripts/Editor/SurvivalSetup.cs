using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            if (!prefab.TryGetComponent<EnemyShip>(out var ship)) continue;

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

        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }

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
            if (module is WeaponModule weapon) dps += weapon.NominalDamagePerSecond;
        }

        return Mathf.Sqrt(hp * Mathf.Max(1f, dps));
    }
}
