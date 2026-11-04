using System.Collections.Generic;
using UnityEngine;

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
    PlasmaWeapon,
    Repair
}
