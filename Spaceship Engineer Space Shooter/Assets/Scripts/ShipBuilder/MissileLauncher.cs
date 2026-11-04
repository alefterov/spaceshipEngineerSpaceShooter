using System.Collections.Generic;
using UnityEngine;

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

        if (fireControl != WeaponFireControl.Automatic && OwnFaction != Faction.Enemy) return;

        bool shipTargets = allowedTargets.Contains(TargetKind.Ship) && MissileTargeting.AnyHostileWithin(OwnFaction, MuzzleCenter, range);
        if (!shipTargets && MissileTargeting.NearestTargetable(OwnFaction, allowedTargets, MuzzleCenter, range, true) == null) return;

        FireIfReady(null);
    }

    protected override void Spawn(Transform tip)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, MuzzleForward);
        var instance = Instantiate(projectilePrefab, tip.position, rotation);

        if (instance.TryGetComponent<Missile>(out var missile))
            missile.Launch(OwnFaction, projectileDamage * EffectiveDamageMultiplier, projectileSpeed, priority.Order, weaponClass, allowedTargets);
        else
            Debug.LogWarning($"Missile launcher '{moduleId}': Projectile Prefab has no Missile component.", this);

        int index = barrels.IndexOf(tip);
        if (index >= 0) SetLoadedShown(index, false);
    }

    private void UpdateLoadedMissiles()
    {
        for (int i = 0; i < barrels.Count && i < barrelCooldowns.Length; i++)
        {
            float remaining = barrelCooldowns[i];

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
