using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A missile launcher block. It launches on its own, every Cooldown Seconds per launcher/barrel, as long as
/// a block of the other side is within Range — no aiming, the missile does the steering (see Missile). It
/// reuses everything a gun has: energy per launch (Energy Delta x Cooldown, per barrel), a reload per barrel,
/// the crew's Missile Damage perk, manual or automatic fire control.
///
/// SETUP (a prefab like any weapon block):
///  - Projectile Prefab = the missile prefab (a Missile component); Muzzles = where each missile leaves
///    (one entry per launcher barrel/rail); Cooldown Seconds = time between launches; Projectile Damage =
///    the missile's damage; Projectile Speed = its flight speed (slower than a shot, set it here); Range =
///    how close an enemy block must be before it launches.
///  - Weapon Class = Missile. Aiming stays Fixed — the launcher never turns; the missile curves after launch.
///  - Loaded Missiles (optional) = for each muzzle, in the same order, the sprite object of the missile
///    sitting on the launcher. It vanishes the instant that muzzle launches and comes back
///    Show Loaded Before Seconds before the next launch, so the launcher looks reloaded.
/// </summary>
public class MissileLauncher : WeaponModule
{
    [Header("Missile launcher")]
    [Tooltip("Optional: for every Muzzle (same order), the object showing the missile sitting on the launcher. " +
             "It disappears at once when that muzzle launches and reappears this many seconds before the next " +
             "launch.")]
    public List<GameObject> loadedMissiles = new();
    [Tooltip("The loaded missile shows again this long before the next launch (1 = one second before the cooldown ends).")]
    [Min(0f)] public float showLoadedBeforeSeconds = 1f;

    private MissileTargetPriority priority;

    protected override void Awake()
    {
        base.Awake();
        priority = MissileTargetPriority.GetOrAdd(this);
    }

    protected override void Update()
    {
        TickCooldowns();
        UpdateLoadedMissiles();

        if (!CombatActive || IsDestroyed || !IsPoweredOn) return;
        if (!ValidateSetup()) return;

        // On an enemy ship every launcher fires by itself; for the player only Automatic ones do.
        if (fireControl != WeaponFireControl.Automatic && OwnFaction != Faction.Enemy) return;

        // Launches only when there is something to hit within range.
        if (!MissileTargeting.AnyHostileWithin(OwnFaction, MuzzleCenter, range)) return;

        FireIfReady(null); // no aiming arc to check — the missile steers itself
    }

    protected override void Spawn(Transform tip)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);
        var instance = Instantiate(projectilePrefab, tip.position, rotation);

        if (instance.TryGetComponent<Missile>(out var missile))
            missile.Launch(OwnFaction, projectileDamage * EffectiveDamageMultiplier, projectileSpeed, priority.Order, weaponClass);
        else
            Debug.LogWarning($"Missile launcher '{moduleId}': Projectile Prefab has no Missile component.", this);

        // The missile that was sitting on this barrel just left it.
        int index = barrels.IndexOf(tip);
        if (index >= 0 && index < loadedMissiles.Count && loadedMissiles[index] != null)
            loadedMissiles[index].SetActive(false);
    }

    private void UpdateLoadedMissiles()
    {
        for (int i = 0; i < loadedMissiles.Count && i < barrelCooldowns.Length; i++)
        {
            var visual = loadedMissiles[i];
            if (visual == null) continue;

            float remaining = barrelCooldowns[i];

            // Back on the launcher for the last stretch of the cooldown, and whenever it is ready. Right after a
            // launch the cooldown is full, so it stays hidden until only showLoadedBeforeSeconds remain.
            bool shown = remaining <= 0f || (remaining <= showLoadedBeforeSeconds && remaining < EffectiveCooldownSeconds - 0.0001f);
            shown &= !IsDestroyed;
            if (visual.activeSelf != shown) visual.SetActive(shown);
        }
    }
}
