using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Spawns enemy ships built via ShipGrid.BuildFromLayout — the exact same
/// hull+module assembly code path the player's editor uses. This is what
/// makes enemies "настоящими" modular ships instead of a separate hardcoded type.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject shipGridPrefab;  // empty root with ShipGrid + ShipIdentity components
    public BlockDatabase database;
    public List<EnemyShipTemplate> templates = new();

    [Header("Spawn area")]
    public float spawnY = 8f;
    public float spawnXRange = 4f;

    public void SpawnOne()
    {
        var template = PickWeighted();
        if (template == null) return;

        Vector3 pos = new(Random.Range(-spawnXRange, spawnXRange), spawnY, 0f);
        var shipObj = Instantiate(shipGridPrefab, pos, Quaternion.identity);

        var grid = shipObj.GetComponent<ShipGrid>();
        var identity = shipObj.GetComponent<ShipIdentity>();

        grid.BuildFromLayout(template.layout, database, Faction.Enemy);

        var mover = shipObj.AddComponent<EnemyShipMover>();
        mover.speed = template.moveSpeed;

        identity.OnShipDestroyed += _ =>
        {
            // Hook up score/loot here, e.g. GameEvents.EnemyKilled(template.scoreValue);
            Destroy(shipObj, 0.1f);
        };
    }

    private EnemyShipTemplate PickWeighted()
    {
        if (templates.Count == 0) return null;
        float total = templates.Sum(t => t.spawnWeight);
        float roll = Random.Range(0f, total);

        foreach (var t in templates)
        {
            if (roll < t.spawnWeight) return t;
            roll -= t.spawnWeight;
        }
        return templates[^1];
    }
}

/// <summary>Minimal straight-line descent. Swap for real AI (weaving, targeting) later.</summary>
public class EnemyShipMover : MonoBehaviour
{
    public float speed = 2f;
    private void Update() => transform.position += Vector3.down * speed * Time.deltaTime;
}
