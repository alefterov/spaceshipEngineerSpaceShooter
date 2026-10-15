using UnityEngine;

/// <summary>
/// One perk (skill) as a reusable asset: what it improves, its icon and how strongly. Created once and then
/// dropped into the Perks list of as many crew members as needed (CrewMemberDefinition) — change the numbers
/// here and every character using it follows.
///
/// Every perk scales with the owner's single level: bonus = Base Bonus + Bonus Per Level x (level - 1). For
/// an ordinary perk the coefficient shown (and applied) is 1 + bonus; for a CHANCE perk the bonus IS the
/// chance (see CrewPerkType).
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Crew Perk", fileName = "NewCrewPerk")]
public class CrewPerkDefinition : ScriptableObject
{
    public CrewPerkType type = CrewPerkType.None;
    [Tooltip("Shown in the UI where a perk name fits (optional).")]
    public string displayName;
    public Sprite icon;
    [Tooltip("Bonus at level 1 — 0.1 means x1.10 (or a 10% chance, for a chance perk).")]
    public float baseBonus = 0.1f;
    [Tooltip("Extra bonus for every level above 1 — 0.05 means +0.05 per level.")]
    public float bonusPerLevel = 0.05f;

    public float GetBonus(int level) => baseBonus + bonusPerLevel * Mathf.Max(0, level - 1);
    public float GetCoefficient(int level) => 1f + GetBonus(level);
}
