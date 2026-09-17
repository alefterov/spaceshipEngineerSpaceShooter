using UnityEngine;

/// <summary>
/// One node on the campaign's level-select map — one asset per level. Progression is a simple linear
/// chain via Prerequisite (same pattern as TechDefinition): a level with no prerequisite is unlocked
/// from the start, any other level unlocks once its prerequisite has been completed at least once
/// (see GameDataManager.IsLevelUnlocked). Star rating (0-3) is tracked separately per level in
/// GameData — see GameDataManager.GetStars/SetLevelStars.
///
/// No database/registry for these — nothing needs to enumerate or look one up by id (unlike blocks/
/// techs, which get resolved from saved string ids). LevelButtonView just holds a direct reference,
/// same as how you'll lay out the map and connecting lines by hand.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Level Definition", fileName = "NewLevel")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    public string id = "level_1";
    [Tooltip("Number shown on the level button.")]
    public int displayNumber = 1;

    [Header("Progression")]
    [Tooltip("The level that must be completed first. Leave empty for a level that's unlocked from the start.")]
    public LevelDefinition prerequisite;

    [Header("Battle")]
    [Tooltip("Backdrop shown in the battle scene. Every level in the same campaign typically shares " +
             "the same one — set per level rather than per campaign since there's no separate " +
             "campaign asset, just the level-select map's panels.")]
    public Sprite background;
}
