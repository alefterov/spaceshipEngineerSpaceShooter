using UnityEngine;

/// <summary>
/// What a crew perk (skill) improves on the player's ship. Every value is applied somewhere in gameplay —
/// see each line for the exact place. The perks are grouped here by the specialty they were designed for,
/// but nothing stops a character from using any of them. Values are explicit so already-configured
/// CrewMemberDefinition assets survive new perks being added.
///
/// Perks marked CHANCE are probabilities, not coefficients: their bonus is the chance itself (0.05 = 5%),
/// capped by GameDataManager.Crew Chance Cap, and the UI shows them as a percentage rather than "x1.05".
/// </summary>
public enum CrewPerkType
{
    None = 0,

    // ----- Captain -----
    /// <summary>Player experience earned from battle (BattleOutcomeController).</summary>
    ExperienceGain = 10,
    /// <summary>Credits earned from battle (BattleOutcomeController).</summary>
    CreditsGain = 11,
    /// <summary>CHANCE of a critical hit. The mechanic itself isn't built yet — the perk is stored and shown,
    /// nothing reads it so far.</summary>
    CritChance = 12,
    /// <summary>CHANCE that a hit on one of the player's blocks deals no damage at all (ShipModule.TakeDamage).</summary>
    ZeroDamageChance = 13,

    // ----- Navigator -----
    /// <summary>Thrust of engines whose Kind is Ion (EngineModule.GetThrust).</summary>
    IonEnginePower = 14,
    /// <summary>Thrust of engines whose Kind is Jet (EngineModule.GetThrust).</summary>
    JetEnginePower = 15,
    /// <summary>CHANCE to dodge laser and plasma shots (Projectile).</summary>
    EvadeEnergyWeapons = 16,
    /// <summary>CHANCE to dodge ballistic shots and missiles (Projectile).</summary>
    EvadeBallisticWeapons = 17,

    // ----- Engineer -----
    /// <summary>Energy generation of the generators (ShipEnergySystem).</summary>
    GeneratorEfficiency = 9,
    /// <summary>Max HP of armor blocks (ShipGrid.PlaceBlock).</summary>
    ArmorEfficiency = 18,
    /// <summary>Shield capacity (ShieldModule).</summary>
    ShieldEfficiency = 4,
    /// <summary>Healing rate of repair blocks (RepairModule).</summary>
    RepairEfficiency = 6,

    // ----- Gunner -----
    /// <summary>Damage of ballistic shots — kinetic cannons and the like (WeaponModule).</summary>
    BallisticDamage = 19,
    /// <summary>Damage of missiles (WeaponModule).</summary>
    MissileDamage = 20,
    /// <summary>Damage of lasers (WeaponModule).</summary>
    LaserDamage = 21,
    /// <summary>Damage of plasma (WeaponModule).</summary>
    PlasmaDamage = 22,
}

public static class CrewPerkTypeExtensions
{
    /// <summary>Whether the perk is a probability (shown as a percentage, capped) rather than a coefficient.</summary>
    public static bool IsChance(this CrewPerkType type)
        => type == CrewPerkType.CritChance
        || type == CrewPerkType.ZeroDamageChance
        || type == CrewPerkType.EvadeEnergyWeapons
        || type == CrewPerkType.EvadeBallisticWeapons;
}
