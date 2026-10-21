using UnityEngine;

/// <summary>
/// A plasma thrower. Each shot launches Balls Per Shot plasma balls at once (PlasmaBall), side by side and all flying
/// the same way — along the barrel, straight ahead. The balls pass over enemy ships; what hurts is the lightning their
/// field throws at the blocks beneath (see PlasmaBall).
///
/// It is an ordinary weapon block (WeaponModule), so aiming (Fixed / Turret), Range, Cooldown Seconds, Muzzles,
/// Energy Delta (energy per shot), Allowed Targets and the crew's Plasma Damage perk all work as for a gun.
///
/// SETUP: Projectile Prefab = the plasma ball prefab (with a PlasmaBall component); Projectile Damage = the damage of
/// ONE lightning strike; Projectile Speed = how fast the balls fly; Range = how far they fly (and when the weapon
/// opens fire); Weapon Class = Plasma.
/// </summary>
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
        float lifetime = (range + 1f) / Mathf.Max(0.01f, projectileSpeed); // the balls end where the weapon's range ends

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
