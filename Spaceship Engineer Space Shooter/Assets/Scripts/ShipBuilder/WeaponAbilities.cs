using UnityEngine;

/// <summary>The active abilities of the player's weapons, one per kind of weapon (see ActiveAbilitiesController).</summary>
public enum WeaponAbility
{
    /// <summary>Ballistic guns (fixed mounts only, not turrets) fire Strength times faster.</summary>
    RapidFire = 0,
    /// <summary>Every missile launch is repeated once, Delay seconds later.</summary>
    MissileDoubleSalvo = 1,
    /// <summary>Every plasma shot is repeated once, Delay seconds later.</summary>
    PlasmaDoubleSalvo = 2,
    /// <summary>Laser beams fired during the ability last Strength times longer.</summary>
    LongLaser = 3,
}

/// <summary>
/// Which active abilities are switched on right now and how strong they are. ActiveAbilitiesController starts and ends them; the
/// player's weapons read them (WeaponModule, LaserWeapon). Only the PLAYER's weapons use this — enemies never get abilities.
/// Based on scaled game time, so a paused battle freezes them too.
/// </summary>
public static class WeaponAbilities
{
    private static readonly float[] endTime = new float[4];
    private static readonly float[] strength = { 1f, 1f, 1f, 1f };
    private static readonly float[] delay = new float[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ClearAll();

    public static void Begin(WeaponAbility ability, float seconds, float strengthValue, float delaySeconds)
    {
        int i = (int)ability;
        endTime[i] = Time.time + seconds;
        strength[i] = strengthValue;
        delay[i] = delaySeconds;
    }

    public static void End(WeaponAbility ability) => endTime[(int)ability] = 0f;

    public static void ClearAll()
    {
        for (int i = 0; i < endTime.Length; i++) { endTime[i] = 0f; strength[i] = 1f; delay[i] = 0f; }
    }

    public static bool IsActive(WeaponAbility ability) => Time.time < endTime[(int)ability];

    /// <summary>How strong the ability is (1 when it is off).</summary>
    public static float Strength(WeaponAbility ability) => IsActive(ability) ? strength[(int)ability] : 1f;

    /// <summary>The pause between a shot and its repeat, for the double-salvo abilities.</summary>
    public static float Delay(WeaponAbility ability) => delay[(int)ability];
}
