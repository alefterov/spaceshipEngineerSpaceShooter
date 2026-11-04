using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("The level the win screen's \"Next Level\" button leads to. Leave empty for the last level " +
             "of a campaign — that button is simply disabled then. Not derived from Prerequisite " +
             "automatically (no registry to search for \"whoever points back at me\") — set both ends by hand.")]
    public LevelDefinition nextLevel;

    [Header("Battle")]
    [Tooltip("Backdrop shown in the battle scene. Every level in the same campaign typically shares " +
             "the same one — set per level rather than per campaign since there's no separate " +
             "campaign asset, just the level-select map's panels.")]
    public Sprite background;

    [Tooltip("This level's own enemy waves — configured per level, not in the battle scene. " +
             "EnemySpawner just reads whichever level PendingBattle.Level points at.")]
    public List<Wave> waves = new();
}
