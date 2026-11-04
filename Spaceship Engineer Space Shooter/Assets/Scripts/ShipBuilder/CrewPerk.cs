using UnityEngine;

public enum CrewPerkType
{
    None = 0,

    ExperienceGain = 10,
    CreditsGain = 11,
    CritChance = 12,
    ZeroDamageChance = 13,

    IonEnginePower = 14,
    JetEnginePower = 15,
    EvadeEnergyWeapons = 16,
    EvadeBallisticWeapons = 17,

    GeneratorEfficiency = 9,
    ArmorEfficiency = 18,
    ShieldEfficiency = 4,
    RepairEfficiency = 6,

    BallisticDamage = 19,
    MissileDamage = 20,
    LaserDamage = 21,
    PlasmaDamage = 22,
}

public static class CrewPerkTypeExtensions
{
    public static bool IsChance(this CrewPerkType type)
        => type == CrewPerkType.CritChance
        || type == CrewPerkType.ZeroDamageChance
        || type == CrewPerkType.EvadeEnergyWeapons
        || type == CrewPerkType.EvadeBallisticWeapons;
}
