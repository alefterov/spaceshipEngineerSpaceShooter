using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    [Tooltip("Bump this if the save format changes shape, to support migrations later.")]
    public int saveVersion = 1;

    [Tooltip("Soft currency — earned in-game, spent on building/repairing modules. Starting grant for a fresh save.")]
    public int credits = 3000;
    [Tooltip("Premium currency — purchasable with real money. Not spent on anything yet.")]
    public int coins;
    public List<ResourceEntry> resources = new();

    [Tooltip("Spent on researching technologies — earned separately from credits/coins. Starting grant for a fresh save.")]
    public int researchPoints = 10;
    [Tooltip("Ids of every TechDefinition researched so far. A List, not a Dictionary/HashSet — " +
             "JsonUtility can't serialize either of those.")]
    public List<string> researchedTechIds = new();

    [Tooltip("OLD single hangar level (one row AND one column per level). Only read once, to convert an old " +
             "save into hangarColumns / hangarRows — see GameDataManager.LoadOrCreate.")]
    public int hangarLevel;

    [Tooltip("Columns added to the ship's buildable area by hangar upgrades. Bought with research points, see " +
             "GameDataManager.TryUpgradeHangarColumn.")]
    public int hangarColumns;

    [Tooltip("Rows added to the ship's buildable area by hangar upgrades. Bought with research points, see " +
             "GameDataManager.TryUpgradeHangarRow.")]
    public int hangarRows;

    [Tooltip("The player's missile target priority: ModuleType values, first = hit first. Empty until changed in the missile priority window.")]
    public List<int> missileTargetPriority = new();

    [Tooltip("The player's tower-gun target priority: ModuleType values, first = shot first. Empty until changed in the priorities window.")]
    public List<int> towerTargetPriority = new();

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

    [Tooltip("Player's account level — separate from ship/hangar level, rises by earning battle " +
             "experience. Starts at 0 for a fresh save. See GameDataManager.AddExperience.")]
    public int playerLevel;
    [Tooltip("Experience earned toward the NEXT player level — resets to 0 (any overflow carried into " +
             "the next level) each time playerLevel increases.")]
    public int playerExperience;

    [Tooltip("Every crew member the player owns, assigned or not. A List, not a Dictionary — JsonUtility " +
             "can't serialize those. See GameDataManager's crew section.")]
    public List<CrewMemberData> crewMembers = new();
    [Tooltip("Which crew member stands in which of the 4 slots (by instanceId). A slot with no entry, or " +
             "an empty instanceId, is empty.")]
    public List<CrewSlotEntry> crewSlots = new();
    [Tooltip("Set once the starting crew (CrewDatabase.starterCrew) has been handed out, so it's never " +
             "granted twice.")]
    public bool crewStarterGranted;
    [Tooltip("Set once the starting technologies and ship (GameDataManager, New game) have been handed out, so it happens only at the very first start.")]
    public bool starterKitGranted;
    [Tooltip("The \"No ads\" purchase: when true the banner and the full-screen ads between battles are never shown (AdsManager). Rewarded videos stay available.")]
    public bool adsRemoved;
    [Tooltip("The premium purchase (shop): no ads, battle credits and experience x2.")]
    public bool premium;
    [Tooltip("Ids of the shop products bought once (premium...) — their buttons show \"bought\".")]
    public List<string> ownedShopProducts = new();
    [Tooltip("Transaction ids of the real-money purchases already given out, so a purchase the store sends again is never given twice.")]
    public List<string> processedOrders = new();

    [Tooltip("Ids of the tutorial steps already done (TutorialController).")]
    public List<string> tutorialDoneSteps = new();
    [Tooltip("The player ticked \"Don't show the tutorial again\".")]
    public bool tutorialOff;

    [Tooltip("Player levels whose reward has already been taken (rewards panel, level bar).")]
    public List<int> claimedLevelRewards = new();
    [Tooltip("Progress and claimed state of every task that has any (rewards panel). A List — JsonUtility cannot save a Dictionary.")]
    public List<TaskStateEntry> taskStates = new();
    [Tooltip("The best wave reached in survival mode so far (for the survival tasks).")]
    public int survivalBestWave;

    [Tooltip("OLD SAVES ONLY: the single ship before the fleet existed. Moved into Fleet (as its first ship) when loaded.")]
    public ShipLayout playerShip = new();

    [Tooltip("The player's ships (the fleet). Each has its own name, blocks and hangar upgrades.")]
    public List<FleetShipData> fleet = new();
    [Tooltip("Index in Fleet of the ship that goes into battle.")]
    public int selectedShip;
    [Tooltip("How many ships the fleet can hold.")]
    public int fleetCapacity = 3;
}

[System.Serializable]
public class FleetShipData
{
    public string name = "Ship";
    public ShipLayout layout = new();
    [Tooltip("Columns / rows bought for THIS ship's grid, on top of the base grid. Their prices grow with this ship's own count.")]
    public int hangarColumns;
    public int hangarRows;
}

[System.Serializable]
public class ResourceEntry
{
    public string id;
    public int amount;
}

[System.Serializable]
public class TaskStateEntry
{
    public string id;
    public int progress;
    public bool claimed;
}

[System.Serializable]
public class LevelStarEntry
{
    public string levelId;
    public int stars;
}

[System.Serializable]
public class CrewMemberData
{
    public string instanceId;
    public string definitionId;
    public int rank = 1;
    public int level = 1;
    public bool isNew;
}

[System.Serializable]
public class CrewSlotEntry
{
    public CrewRole role;
    public string instanceId;
}
