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
///  - The missile sprite on each Muzzle (the SpriteRenderer on the Muzzle object or its children) vanishes the
///    instant that muzzle launches and comes back Show Loaded Before Seconds before the next launch, so the
///    launcher looks reloaded. Loaded Missiles is only for a sprite that does NOT sit on the Muzzle itself.
/// </summary>
public class MissileLauncher : WeaponModule
{
    [Header("Missile launcher")]
    [Tooltip("The missile sitting on the launcher disappears the moment its muzzle launches and comes back this " +
             "long before the next launch (1 = one second before the cooldown ends). By default it is the sprite " +
             "of the Muzzle object itself (its SpriteRenderers, children included); if Loaded Missiles below has " +
             "an object for that muzzle, that object is used instead.")]
    [Min(0f)] public float showLoadedBeforeSeconds = 1f;
    [Tooltip("Optional: for a Muzzle (same order as the Muzzles list) whose missile sprite is NOT on the Muzzle " +
             "object itself — the object showing the loaded missile. Leave empty to use the Muzzle's own sprite.")]
    public List<GameObject> loadedMissiles = new();

    [Tooltip("Pause between one missile leaving and the next barrel launching (0.5 = half a second). The barrels launch one by one, not all at once.")]
    [Min(0f)] public float launchDelaySeconds = 0.5f;

    protected override float SalvoDelay => launchDelaySeconds;

    private MissileTargetPriority priority;
    private SpriteRenderer[][] muzzleSprites = System.Array.Empty<SpriteRenderer[]>();
    private bool[] loadedShown = System.Array.Empty<bool>();

    protected override void Awake()
    {
        base.Awake();
        priority = MissileTargetPriority.GetOrAdd(this);

        // Each muzzle's own sprite(s), so the loaded missile can be hidden without any extra setup.
        muzzleSprites = new SpriteRenderer[barrels.Count][];
        loadedShown = new bool[barrels.Count];
        for (int i = 0; i < barrels.Count; i++)
        {
            muzzleSprites[i] = barrels[i].GetComponentsInChildren<SpriteRenderer>(true);
            loadedShown[i] = true;
        }
    }

    protected override void Update()
    {
        TickCooldowns();
        UpdateLoadedMissiles();

        if (!CombatActive || IsDestroyed || !IsPoweredOn) return;
        if (!ValidateSetup()) return;

        // On an enemy ship every launcher fires by itself; for the player only Automatic ones do.
        if (fireControl != WeaponFireControl.Automatic && OwnFaction != Faction.Enemy) return;

        // Launches only when there is something to hit within range: a ship block (if ships are allowed targets) or a
        // meteor / torpedo among the Allowed Targets.
        bool shipTargets = allowedTargets.Contains(TargetKind.Ship) && MissileTargeting.AnyHostileWithin(OwnFaction, MuzzleCenter, range);
        if (!shipTargets && MissileTargeting.NearestTargetable(OwnFaction, allowedTargets, MuzzleCenter, range, true) == null) return;

        FireIfReady(null); // no aiming arc to check — the missile steers itself
    }

    protected override void Spawn(Transform tip)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);
        var instance = Instantiate(projectilePrefab, tip.position, rotation);

        if (instance.TryGetComponent<Missile>(out var missile))
            missile.Launch(OwnFaction, projectileDamage * EffectiveDamageMultiplier, projectileSpeed, priority.Order, weaponClass, allowedTargets);
        else
            Debug.LogWarning($"Missile launcher '{moduleId}': Projectile Prefab has no Missile component.", this);

        // The missile that was sitting on this barrel just left it — gone at once.
        int index = barrels.IndexOf(tip);
        if (index >= 0) SetLoadedShown(index, false);
    }

    private void UpdateLoadedMissiles()
    {
        for (int i = 0; i < barrels.Count && i < barrelCooldowns.Length; i++)
        {
            float remaining = barrelCooldowns[i];

            // Back on the launcher for the last stretch of the cooldown, and whenever it is ready. Right after a
            // launch the cooldown is full, so it stays hidden until only showLoadedBeforeSeconds remain.
            bool shown = remaining <= 0f || (remaining <= showLoadedBeforeSeconds && remaining < EffectiveCooldownSeconds - 0.0001f);
            shown &= !IsDestroyed;

            SetLoadedShown(i, shown);
        }
    }

    private void SetLoadedShown(int index, bool shown)
    {
        if (index >= loadedShown.Length || loadedShown[index] == shown) return;
        loadedShown[index] = shown;

        if (index < loadedMissiles.Count && loadedMissiles[index] != null)
        {
            loadedMissiles[index].SetActive(shown);
            return;
        }

        foreach (var sprite in muzzleSprites[index])
            if (sprite != null) sprite.enabled = shown;
    }
}
