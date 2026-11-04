using UnityEngine;

public class PlasmaWeapon : WeaponModule
{
    [Header("Plasma")]
    [Tooltip("How many balls one shot launches at once.")]
    [Min(1)] public int ballsPerShot = 1;
    [Tooltip("Distance between neighbouring balls, side to side, in world units. The group is centred on the muzzle.")]
    [Min(0f)] public float ballSpacing = 0.8f;

    public override float NominalDamagePerSecond
        => projectileDamage * Mathf.Max(1, ballsPerShot) * NominalBarrels / Mathf.Max(0.01f, cooldownSeconds);

    public override float DamagePerSecond
        => projectileDamage * EffectiveDamageMultiplier * BarrelCount * Mathf.Max(1, ballsPerShot)
           / Mathf.Max(0.01f, EffectiveCooldownSeconds);

    protected override void Spawn(Transform tip)
    {
        Vector2 forward = MuzzleForward;
        Vector2 side = new(-forward.y, forward.x);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, forward);

        int count = Mathf.Max(1, ballsPerShot);
        float lifetime = (range + 1f) / Mathf.Max(0.01f, projectileSpeed);

        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) * 0.5f) * ballSpacing;
            Vector3 position = tip.position + (Vector3)(side * offset);

            var instance = Instantiate(projectilePrefab, position, rotation);
            if (instance.TryGetComponent<PlasmaBall>(out var ball))
                ball.Launch(OwnFaction, projectileDamage * EffectiveDamageMultiplier, projectileSpeed, lifetime);
            else
                Debug.LogWarning($"Plasma weapon '{moduleId}': Projectile Prefab has no PlasmaBall component.", this);
        }
    }
}
