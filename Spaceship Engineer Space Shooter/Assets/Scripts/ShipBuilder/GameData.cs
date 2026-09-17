using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single root object for the whole save file — everything the player has
/// (ship build, credits, resources) lives here so there's exactly one JSON file
/// to read/write. Uses JsonUtility, so every field must be public and every
/// nested type must be [System.Serializable] (no Dictionary — see ResourceEntry).
/// </summary>
[System.Serializable]
public class GameData
{
    [Tooltip("Bump this if the save format changes shape, to support migrations later.")]
    public int saveVersion = 1;

    [Tooltip("Soft currency — earned in-game, spent on building/repairing modules. Starting grant for a fresh save.")]
    public int credits = 1000;
    [Tooltip("Premium currency — purchasable with real money. Not spent on anything yet.")]
    public int coins;
    public List<ResourceEntry> resources = new();

    [Tooltip("Spent on researching technologies — earned separately from credits/coins. Starting grant for a fresh save.")]
    public int researchPoints = 10;
    [Tooltip("Ids of every TechDefinition researched so far. A List, not a Dictionary/HashSet — " +
             "JsonUtility can't serialize either of those.")]
    public List<string> researchedTechIds = new();

    [Tooltip("Hangar (build grid) upgrade level — each level adds one row and one column to the " +
             "ship's buildable area. Bought with research points, see GameDataManager.TryUpgradeHangar.")]
    public int hangarLevel;

    [Tooltip("Battle credits-reward multiplier upgrade level. Bought with research points, see " +
             "GameDataManager.TryUpgradeCreditsMultiplier.")]
    public int creditsMultiplierLevel;

    [Tooltip("Survival-mode starting-level upgrade count. Bought with research points, see " +
             "GameDataManager.TryUpgradeSurvivalStartLevel.")]
    public int survivalStartLevelUpgrades;

    [Tooltip("Best star rating (0-3) earned per campaign level so far. A List, not a Dictionary — " +
             "JsonUtility can't serialize those — so it's only ever grown/updated via GameDataManager, " +
             "never indexed directly.")]
    public List<LevelStarEntry> levelStars = new();

    public ShipLayout playerShip = new();
}

[System.Serializable]
public class ResourceEntry
{
    public string id;
    public int amount;
}

[System.Serializable]
public class LevelStarEntry
{
    public string levelId;
    public int stars;
}
