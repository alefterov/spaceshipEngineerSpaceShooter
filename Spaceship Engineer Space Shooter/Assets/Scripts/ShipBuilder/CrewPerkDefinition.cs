using UnityEngine;

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
