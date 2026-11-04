using UnityEngine;

public enum WeaponAbility
{
    RapidFire = 0,
    MissileDoubleSalvo = 1,
    PlasmaDoubleSalvo = 2,
    LongLaser = 3,
}

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

    public static float Strength(WeaponAbility ability) => IsActive(ability) ? strength[(int)ability] : 1f;

    public static float Delay(WeaponAbility ability) => delay[(int)ability];
}
