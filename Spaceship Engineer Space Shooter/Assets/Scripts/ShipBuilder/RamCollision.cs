using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What happens when an enemy ship touches the player's ship — a collision with damage AND a physical response.
///
/// DAMAGE: while a block of this (enemy) ship overlaps a block of the player's ship, every Tick Seconds each of the
/// two blocks hits the other for Damage Fraction (default half) of its OWN current health — mirrored, so a sturdy
/// block hurts more and gets hurt back by the same rule. A contact that lasts keeps repeating the exchange. A block
/// destroyed by the exchange deals the rest of its health to the block it died against as a final blow. Destroyed
/// blocks explode like any other (ShipModule.explosionEffectPrefab).
///
/// PHYSICS: ships never pass through each other. Overlapping ships are pushed apart (the lighter one more), the
/// first moment of a touch stops both dead for Halt Seconds and knocks them away from each other a little
/// (Bounce Speed, heavier ships less), and afterwards neither can push further into the other (see ShipBody). The
/// same applies when the player rams an enemy and when an enemy rams the player.
///
/// Added to every EnemyShip automatically (add it yourself on the prefab to change the numbers). Works for every
/// kind of contact: a ship deliberately ramming (EnemyShip.ramsPlayer), a disabled wreck or a broken-off fragment
/// drifting into the player, or the player flying into an enemy. Only enemy-versus-player pairs are considered.
/// </summary>
public class RamCollision : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Share of the block's own current health it deals to the block it is touching on every hit " +
             "(0.5 = half).")]
    [Range(0f, 1f)] public float damageFraction = 0.5f;
    [Tooltip("Seconds between hits while two blocks stay in contact.")]
    [Min(0.05f)] public float tickSeconds = 0.5f;

    [Header("Impact")]
    [Tooltip("Seconds both ships are stopped dead after a fresh collision (their own engines can't move them).")]
    [Min(0f)] public float haltSeconds = 0.3f;
    [Tooltip("How hard the ships are thrown apart on impact, in world units per second (split between them by mass — " +
             "the lighter one flies off more).")]
    [Min(0f)] public float bounceSpeed = 3f;
    [Tooltip("How quickly the knock dies away, in units per second per second. Higher = a shorter bounce.")]
    [Min(0.1f)] public float bounceDecay = 8f;
    [Tooltip("How long a touch is remembered once the ships stop overlapping — within this time, touching again is not " +
             "a new impact.")]
    [Min(0.02f)] public float contactMemorySeconds = 0.15f;

    private ShipIdentity ship;
    private ShipBody body;
    private float nextTick;
    private readonly List<Collider2D> overlaps = new();
    private readonly List<(ShipModule enemy, ShipModule player)> pairs = new();
    private ContactFilter2D filter;

    private void Awake()
    {
        ship = GetComponent<ShipIdentity>();
        body = ShipBody.GetOrAdd(this);
        filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
    }

    private void Update()
    {
        if (ship == null) return;

        CollectPairs();
        if (pairs.Count == 0) return;

        Resolve();

        if (Time.time < nextTick) return;
        nextTick = Time.time + tickSeconds;
        foreach (var (enemy, player) in pairs) Exchange(enemy, player);
    }

    // ---------- Finding the touching blocks ----------

    /// <summary>Every (this ship's block, player's block) pair whose colliders overlap right now.</summary>
    private void CollectPairs()
    {
        pairs.Clear();

        if (!TryGetPlayerBounds(out Bounds playerBounds)) return;

        // Ships move by setting their transform, so the physics world may still hold last step's positions.
        Physics2D.SyncTransforms();

        foreach (var block in ship.LiveBlocks)
        {
            if (block == null || block.IsDestroyed) continue;
            if (!block.TryGetComponent<Collider2D>(out var collider) || !collider.enabled) continue;
            if (!collider.bounds.Intersects(playerBounds)) continue; // far from the player — skip the costly query

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

    /// <summary>A box around all of the player's living blocks, slightly grown — a cheap "could we be touching?" test.</summary>
    private static bool TryGetPlayerBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (var s in ShipIdentity.All)
        {
            if (s == null || s.faction != Faction.Player) continue;

            foreach (var block in s.LiveBlocks)
            {
                if (block == null || block.IsDestroyed || !block.TryGetComponent<Collider2D>(out var c) || !c.enabled) continue;

                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        if (any) bounds.Expand(0.2f);
        return any;
    }

    // ---------- Physical response ----------

    /// <summary>Pushes the two ships out of each other, and on a fresh touch stops and knocks them.</summary>
    private void Resolve()
    {
        var firstPlayerBlock = pairs[0].player;
        var playerShip = firstPlayerBlock.GetComponentInParent<ShipIdentity>();
        if (playerShip == null) return;

        // Where the touching is, and how deep: the direction away from the player's blocks, weighted by overlap.
        Vector2 sum = Vector2.zero;
        float deepest = 0f;

        foreach (var (enemy, player) in pairs)
        {
            if (!enemy.TryGetComponent<Collider2D>(out var ce) || !player.TryGetComponent<Collider2D>(out var cp)) continue;

            float depth = 0f;
            var distance = ce.Distance(cp);
            if (distance.isOverlapped) depth = -distance.distance;

            Vector2 away = (Vector2)ce.bounds.center - (Vector2)cp.bounds.center;
            if (away.sqrMagnitude < 0.0001f) continue;

            sum += away.normalized * Mathf.Max(depth, 0.02f);
            deepest = Mathf.Max(deepest, depth);
        }

        // Straight apart as a fallback (e.g. perfectly centered overlap).
        Vector2 normal = sum.sqrMagnitude > 0.000001f
            ? sum.normalized
            : ((Vector2)transform.position - (Vector2)playerShip.transform.position).normalized;
        if (normal.sqrMagnitude < 0.5f) normal = Vector2.up;

        var playerBody = ShipBody.GetOrAdd(playerShip);

        float myMass = body.Mass;
        float theirMass = playerBody.Mass;
        float myShare = theirMass / (myMass + theirMass);   // the lighter ship gives way more
        float theirShare = myMass / (myMass + theirMass);

        // 1) Out of each other, right now — but not quite: a hair of overlap is left so a ship that keeps pressing in
        //    stays "touching" and the damage exchange keeps repeating.
        float push = Mathf.Max(0f, deepest - 0.02f);
        transform.position += (Vector3)(normal * push * myShare);
        playerShip.transform.position -= (Vector3)(normal * push * theirShare);

        // 2) Remember the touch; a FRESH one (no contact a moment ago) is an impact: stop dead and get knocked back.
        bool impact = !body.InContact;

        body.RegisterContact(normal, contactMemorySeconds, impact, haltSeconds, normal * bounceSpeed * myShare, bounceDecay);
        playerBody.RegisterContact(-normal, contactMemorySeconds, impact, haltSeconds, -normal * bounceSpeed * theirShare, bounceDecay);
    }

    // ---------- Damage ----------

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
