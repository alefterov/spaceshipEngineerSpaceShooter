using UnityEngine;

/// <summary>
/// A pre-designed enemy ship: same ShipLayout format the player's editor exports.
/// This is what lets enemies be "настоящими" modular ships built from hull + modules,
/// destructible piece by piece exactly like the player's ship.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Enemy Ship Template", fileName = "NewEnemyShip")]
public class EnemyShipTemplate : ScriptableObject
{
    public string shipName = "Raider";
    public ShipLayout layout = new();

    [Header("Spawn tuning")]
    public float moveSpeed = 2f;
    [Tooltip("Relative chance this template is picked by the spawner.")]
    public float spawnWeight = 1f;
    public int scoreValue = 100;
}
