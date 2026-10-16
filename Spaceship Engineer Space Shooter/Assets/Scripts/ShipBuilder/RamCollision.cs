using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ramming damage between an enemy ship and the player's ship. While a block of this (enemy) ship overlaps a
/// block of the player's ship, every Tick Seconds each of the two blocks hits the other for Damage Fraction
/// (default half) of its OWN current health — mirrored, so a sturdy block hurts more and gets hurt back by the
/// same rule. A contact that lasts keeps repeating the exchange. A block that is destroyed by the exchange
/// deals the rest of its health (the part of it that wasn't already dealt) to the block it died against as a
/// final blow — so a battering ram that dies still takes a bite out of what it hit. Destroyed blocks explode
/// like any other destroyed block (ShipModule.explosionEffectPrefab), on both sides.
///
/// Added to every EnemyShip automatically (add it yourself on the prefab to change the numbers). Works for
/// every kind of contact: a ship deliberately ramming (EnemyShip.ramsPlayer), a disabled wreck drifting into
/// the player, or the player simply flying into an enemy. Only enemy-versus-player pairs are considered.
/// </summary>
public class RamCollision : MonoBehaviour
{
    [Tooltip("Share of the block's own current health it deals to the block it is touching on every hit " +
             "(0.5 = half).")]
    [Range(0f, 1f)] public float damageFraction = 0.5f;
    [Tooltip("Seconds between hits while two blocks stay in contact.")]
    [Min(0.05f)] public float tickSeconds = 0.5f;

    private ShipGrid grid;
    private float nextTick;
    private readonly List<Collider2D> overlaps = new();
    private readonly List<(ShipModule enemy, ShipModule player)> pairs = new();
    private ContactFilter2D filter;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
    }

    private void Update()
    {
        if (grid == null || Time.time < nextTick) return;

        CollectPairs();
        if (pairs.Count == 0) return;

        nextTick = Time.time + tickSeconds;
        foreach (var (enemy, player) in pairs) Exchange(enemy, player);
    }

    /// <summary>Every (this ship's block, player's block) pair whose colliders overlap right now.</summary>
    private void CollectPairs()
    {
        pairs.Clear();

        // Ships move by setting their transform, so the physics world may still hold last step's positions.
        Physics2D.SyncTransforms();

        foreach (var block in grid.Blocks)
        {
            if (block == null || block.IsDestroyed) continue;
            if (!block.TryGetComponent<Collider2D>(out var collider) || !collider.enabled) continue;

            overlaps.Clear();
            Physics2D.OverlapCollider(collider, filter, overlaps);

            foreach (var other in overlaps)
            {
                if (other == null || !other.CompareTag("PlayerShip")) continue;

                var playerBlock = other.GetComponent<ShipModule>();
                if (playerBlock != null && !playerBlock.IsDestroyed) pairs.Add((block, playerBlock));
            }
        }
    }

    private void Exchange(ShipModule enemy, ShipModule player)
    {
        if (enemy == null || player == null || enemy.IsDestroyed || player.IsDestroyed) return;

        float enemyHp = enemy.CurrentHP;
        float playerHp = player.CurrentHP;

        // Both hits are worked out from the health before this exchange, so neither side gets the benefit
        // of hitting first.
        player.TakeDamage(enemyHp * damageFraction);
        enemy.TakeDamage(playerHp * damageFraction);

        // A block that died passes on the rest of its health to what killed it (unless both died).
        float rest = 1f - damageFraction;
        if (player.IsDestroyed && !enemy.IsDestroyed) enemy.TakeDamage(playerHp * rest);
        else if (enemy.IsDestroyed && !player.IsDestroyed) player.TakeDamage(enemyHp * rest);
    }
}
