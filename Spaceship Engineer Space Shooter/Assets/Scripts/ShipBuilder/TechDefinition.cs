using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One node in a technology tree — one asset per tech. Belongs to a TechCategory (its own
/// independent linear chain) and optionally points at the tech that must be researched before it
/// (Prerequisite) — leave that empty for the first tech in a category's chain, which is
/// researchable from the start. Researching it grants access to every block in UnlockedBlocks in the
/// build palette (see BuildPaletteUI / GameDataManager.IsBlockUnlocked).
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Tech Definition", fileName = "NewTech")]
public class TechDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "tech_hull_1";
    public string displayName = "Hull I";
    public Sprite icon;

    [Header("Tech tree placement")]
    public TechCategory category = TechCategory.Hull;
    [Tooltip("The tech that must be researched first, in the same category's chain. Leave empty for " +
             "the first tech in the chain.")]
    public TechDefinition prerequisite;

    [Header("Cost")]
    public int researchCost = 100;

    [Header("Unlocks")]
    [Tooltip("The block(s) this technology grants access to once researched — usually just one, but " +
             "some techs unlock two related blocks at the same tier (e.g. two rotations/variants of " +
             "the same part) at once.")]
    public List<BlockDefinition> unlockedBlocks = new();
}

/// <summary>One independent linear research chain per value — a weapon TYPE, not the generic
/// BlockCategory.Weapon, since Ballistic/Laser/Missile/Plasma each need their own progression.</summary>
public enum TechCategory
{
    Hull,
    Armor,
    Cockpit,
    Generator,
    Engine,
    Shield,
    BallisticWeapon,
    LaserWeapon,
    Missile,
    PlasmaWeapon
}
