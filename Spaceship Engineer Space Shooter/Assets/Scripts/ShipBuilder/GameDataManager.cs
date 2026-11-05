using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public GameData Current { get; private set; }

    public event Action OnDataLoaded;
    public event Action OnCreditsChanged;
    public event Action OnCoinsChanged;
    public event Action<string> OnResourceChanged;
    public event Action OnInsufficientCredits;
    public event Action OnInsufficientCoins;
    public event Action OnResearchPointsChanged;
    public event Action OnInsufficientResearchPoints;
    public event Action<string> OnTechResearched;
    public event Action OnHangarLevelChanged;
    public event Action OnCreditsMultiplierLevelChanged;
    public event Action OnSurvivalStartLevelChanged;
    public event Action<string> OnLevelStarsChanged;
    public event Action OnExperienceChanged;

    private int creditsAtSessionStart;

    [Header("Number display")]
    [Tooltip("Prices and amounts of this size or more are shown shortened: 1000 -> 1k, 1 250 000 -> 1.25m, then b, t. " +
             "Shortened to hundredths for display only — the real amounts spent and earned are never rounded.")]
    public long compactNumbersFrom = 1000;

    private void OnValidate() => NumberFormat.Threshold = compactNumbersFrom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NumberFormat.Threshold = compactNumbersFrom;
        DontDestroyOnLoad(gameObject);

        LoadOrCreate();
    }

    public void LoadOrCreate()
    {
        Current = SaveSystem.Load() ?? new GameData();

        if (Current.hangarLevel > 0)
        {
            Current.hangarColumns += Current.hangarLevel;
            Current.hangarRows += Current.hangarLevel;
            Current.hangarLevel = 0;
        }

        if (Current.tutorialDoneSteps.Count == 0 && !Current.tutorialOff && Current.levelStars.Count > 0) Current.tutorialOff = true;

        GrantStarterKitIfNeeded();
        EnsureFleet();
        GrantStarterCrewIfNeeded();
        RebuildPerkBonuses();
        dailyCheckedDate = null;
        UpdateDailyVisit();
        OnDataLoaded?.Invoke();
    }

    [Header("New game")]
    [Tooltip("Ids of the technologies (TechDefinition.id) that are already researched in a fresh game.")]
    public List<string> starterTechIds = new()
    {
        "Cockpit_1x1", "generator_1x1", "engine_jet_1x1", "light_armor_1x1", "kinetic_static_gun1_1x1",
    };
    [Tooltip("The ship a fresh game starts with. Given only once, at the very first start (an existing ship is never replaced). " +
             "Build the ship in the builder, save it, then use this component's menu (⋮) → \"Use saved ship as starter ship\".")]
    public ShipLayout starterShip = DefaultStarterShip();
    [Tooltip("Hangar columns / rows a fresh game starts with, on top of the builder's base grid. 0 = the plain base grid.")]
    [Min(0)] public int starterHangarColumns = 0;
    [Min(0)] public int starterHangarRows = 0;
    [Tooltip("Size of the builder's base grid (the ShipGrid of the main scene, without hangar upgrades). \"Use saved ship as starter " +
             "ship\" moves the ship to the middle of this grid plus the starter hangar, so it fits a new player's grid.")]
    public Vector2Int builderBaseGridSize = new(5, 5);

    private void GrantStarterKitIfNeeded()
    {
        if (Current.starterKitGranted) return;
        Current.starterKitGranted = true;

        foreach (var id in starterTechIds)
            if (!string.IsNullOrEmpty(id) && !Current.researchedTechIds.Contains(id)) Current.researchedTechIds.Add(id);

        if ((Current.playerShip == null || Current.playerShip.TotalEntries == 0) && starterShip != null && starterShip.TotalEntries > 0)
        {
            Current.playerShip = CopyLayout(starterShip);
            Current.hangarColumns = Mathf.Max(Current.hangarColumns, starterHangarColumns);
            Current.hangarRows = Mathf.Max(Current.hangarRows, starterHangarRows);
        }

        Save();
    }

    private static ShipLayout CopyLayout(ShipLayout source)
    {
        var copy = new ShipLayout();
        foreach (var e in source.blocks)
            copy.blocks.Add(new ShipLayout.Entry { blockId = e.blockId, anchorX = e.anchorX, anchorY = e.anchorY, rotationSteps = e.rotationSteps });
        return copy;
    }

    private static ShipLayout DefaultStarterShip()
    {
        var layout = new ShipLayout();
        void Add(string id, int x, int y, int rotation = 0)
            => layout.blocks.Add(new ShipLayout.Entry { blockId = id, anchorX = x, anchorY = y, rotationSteps = rotation });

        Add("Cockpit_1x1", 2, 1);
        Add("generator_1x1", 2, 2);
        Add("engine_jet_1x1", 3, 1);
        Add("kinetic_static_gun1_1x1", 3, 2);
        Add("light_armor_1x1", 2, 3);
        Add("light_triangle_armor_1x1", 3, 3, 3);
        Add("light_triangle_armor_1x1", 1, 3);
        Add("engine_jet_1x1", 1, 1);
        return layout;
    }

#if UNITY_EDITOR
    [ContextMenu("Use saved ship as starter ship")]
    private void UseSavedShipAsStarter()
    {
        var saved = SaveSystem.Load();
        var savedLayout = saved == null ? null
            : saved.fleet != null && saved.fleet.Count > 0 ? saved.fleet[Mathf.Clamp(saved.selectedShip, 0, saved.fleet.Count - 1)].layout
            : saved.playerShip;
        if (savedLayout == null || savedLayout.TotalEntries == 0)
        {
            Debug.LogWarning("No saved ship found — build a ship in the builder and save it first.", this);
            return;
        }

        UnityEditor.Undo.RecordObject(this, "Use saved ship as starter ship");
        starterShip = CopyLayout(savedLayout);

        int minX = starterShip.blocks.Min(b => b.anchorX), maxX = starterShip.blocks.Max(b => b.anchorX);
        int minY = starterShip.blocks.Min(b => b.anchorY), maxY = starterShip.blocks.Max(b => b.anchorY);
        int gridW = builderBaseGridSize.x + starterHangarColumns, gridH = builderBaseGridSize.y + starterHangarRows;
        int shiftX = (gridW - (maxX - minX + 1)) / 2 - minX;
        int shiftY = (gridH - (maxY - minY + 1)) / 2 - minY;
        foreach (var b in starterShip.blocks) { b.anchorX += shiftX; b.anchorY += shiftY; }

        UnityEditor.EditorUtility.SetDirty(this);
        bool fits = maxX - minX + 1 <= gridW && maxY - minY + 1 <= gridH;
    }

    [ContextMenu("Delete save file (start as a new player)")]
    private void DeleteSaveFile()
    {
        SaveSystem.DeleteSave();
    }
#endif

    public void Save() => SaveSystem.Save(Current);

    public void SaveShip(ShipGrid grid)
    {
        FocusedShip.layout = grid.ExportLayout();
        Current.selectedShip = FocusedShipIndex;
        Save();
        OnFleetChanged?.Invoke();
        creditsAtSessionStart = Current.credits;
    }

    public void LoadShip(ShipGrid grid, BlockDatabase database)
    {
        var layout = SelectedShip.layout;
        if (layout == null || layout.TotalEntries == 0) return;
        grid.BuildFromLayout(layout, database, Faction.Player);
    }

    public void BeginBuildSession()
    {
        creditsAtSessionStart = Current.credits;
    }

    public void RevertShip(ShipGrid grid, BlockDatabase database)
    {
        grid.BuildFromLayout(FocusedShip.layout, database, Faction.Player);
    }

    public void RevertCredits()
    {
        Current.credits = creditsAtSessionStart;
        OnCreditsChanged?.Invoke();
    }

    public bool HasSavedShip => SelectedShip.layout != null && SelectedShip.layout.TotalEntries > 0;

    [Header("Fleet")]
    [Tooltip("What a new hangar (an extra ship) is paid with: Credits, Coins or Research Points.")]
    public RewardType fleetShipCurrency = RewardType.Credits;
    [Tooltip("Price of every new hangar (every new ship).")]
    [Min(0)] public int fleetShipCost = 10000;
    [Tooltip("Name of a new ship; {0} = its number in the fleet.")]
    public string fleetShipNameFormat = "Ship {0}";
    [Tooltip("How many ships the fleet can hold (the starting ship included). Older saves with a smaller limit get this one.")]
    [Min(1)] public int maxFleetShips = 5;

    public event Action OnFleetChanged;

    public IReadOnlyList<FleetShipData> Fleet => Current.fleet;
    public int FleetCapacity => Mathf.Max(1, Mathf.Max(Current.fleetCapacity, maxFleetShips));
    public bool CanAddFleetShip => Current.fleet.Count < FleetCapacity;

    public int SelectedShipIndex => Mathf.Clamp(Current.selectedShip, 0, Mathf.Max(0, Current.fleet.Count - 1));
    public FleetShipData SelectedShip => Current.fleet[SelectedShipIndex];

    public int FocusedShipIndex { get; private set; }
    public FleetShipData FocusedShip => Current.fleet[Mathf.Clamp(FocusedShipIndex, 0, Current.fleet.Count - 1)];

    public int NextFleetShipCost => fleetShipCost;

    private void EnsureFleet()
    {
        if (Current.fleet.Count > 0)
        {
            FocusedShipIndex = SelectedShipIndex;
            return;
        }

        Current.fleet.Add(new FleetShipData
        {
            name = string.Format(fleetShipNameFormat, 1),
            layout = Current.playerShip != null ? CopyLayout(Current.playerShip) : new ShipLayout(),
            hangarColumns = Current.hangarColumns,
            hangarRows = Current.hangarRows,
        });
        Current.selectedShip = 0;
        FocusedShipIndex = 0;
        Save();
    }

    public void FocusShip(int index)
    {
        FocusedShipIndex = Mathf.Clamp(index, 0, Current.fleet.Count - 1);
        OnFleetChanged?.Invoke();
        OnHangarLevelChanged?.Invoke();
    }

    public void ResetFocus() => FocusShip(SelectedShipIndex);

    public void SelectShip(int index)
    {
        Current.selectedShip = Mathf.Clamp(index, 0, Current.fleet.Count - 1);
        Save();
        OnFleetChanged?.Invoke();
    }

    public void RenameShip(int index, string newName)
    {
        if (index < 0 || index >= Current.fleet.Count) return;
        newName = string.IsNullOrWhiteSpace(newName) ? string.Format(fleetShipNameFormat, index + 1) : newName.Trim();
        if (Current.fleet[index].name == newName) return;

        Current.fleet[index].name = newName;
        Save();
        OnFleetChanged?.Invoke();
    }

    public bool TryBuyFleetShip(out int newIndex)
    {
        newIndex = -1;
        if (!CanAddFleetShip) return false;

        int cost = NextFleetShipCost;
        switch (fleetShipCurrency)
        {
            case RewardType.Coins:
                if (!SpendCoins(cost)) return false;
                break;
            case RewardType.ResearchPoints:
                if (Current.researchPoints < cost) { OnInsufficientResearchPoints?.Invoke(); return false; }
                Current.researchPoints -= cost;
                OnResearchPointsChanged?.Invoke();
                break;
            default:
                if (!SpendCredits(cost)) return false;
                creditsAtSessionStart = Current.credits;
                break;
        }

        Current.fleet.Add(new FleetShipData
        {
            name = string.Format(fleetShipNameFormat, Current.fleet.Count + 1),
            layout = starterShip != null ? CopyLayout(starterShip) : new ShipLayout(),
            hangarColumns = starterHangarColumns,
            hangarRows = starterHangarRows,
        });
        newIndex = Current.fleet.Count - 1;
        Save();
        OnFleetChanged?.Invoke();
        return true;
    }

    public void ShowFleetShip(ShipGrid grid, BlockDatabase database, int index)
    {
        if (grid == null || index < 0 || index >= Current.fleet.Count) return;
        var ship = Current.fleet[index];
        grid.SetHangarSize(ship.hangarColumns, ship.hangarRows);
        grid.BuildFromLayout(ship.layout, database, Faction.Player);
    }

    public int Credits => Current.credits;

    public void AddCredits(int amount)
    {
        Current.credits += amount;
        OnCreditsChanged?.Invoke();
    }

    public bool SpendCredits(int amount)
    {
        if (Current.credits < amount) { NotifyInsufficientCredits(); return false; }
        Current.credits -= amount;
        OnCreditsChanged?.Invoke();
        return true;
    }

    public void NotifyInsufficientCredits() => OnInsufficientCredits?.Invoke();

    public int Coins => Current.coins;

    public void AddCoins(int amount)
    {
        Current.coins += amount;
        OnCoinsChanged?.Invoke();
        Save();
    }

    public bool SpendCoins(int amount)
    {
        if (Current.coins < amount) { NotifyInsufficientCoins(); return false; }
        Current.coins -= amount;
        OnCoinsChanged?.Invoke();
        Save();
        return true;
    }

    public void NotifyInsufficientCoins() => OnInsufficientCoins?.Invoke();

    public int ResearchPoints => Current.researchPoints;

    public void AddResearchPoints(int amount)
    {
        Current.researchPoints += amount;
        OnResearchPointsChanged?.Invoke();
        Save();
    }

    public bool IsTechResearched(TechDefinition tech) => tech != null && Current.researchedTechIds.Contains(tech.id);

    public bool CanResearch(TechDefinition tech)
    {
        if (tech == null || IsTechResearched(tech)) return false;
        if (tech.prerequisite != null && !IsTechResearched(tech.prerequisite)) return false;
        return Current.researchPoints >= tech.researchCost;
    }

    [Header("Research")]
    [Tooltip("Played when a technology has been researched.")]
    public AudioClip researchSound;
    [Range(0f, 1f)] public float researchVolume = 1f;

    public bool TryResearch(TechDefinition tech)
    {
        if (tech == null || IsTechResearched(tech)) return false;
        if (tech.prerequisite != null && !IsTechResearched(tech.prerequisite)) return false;

        if (Current.researchPoints < tech.researchCost)
        {
            OnInsufficientResearchPoints?.Invoke();
            return false;
        }

        Current.researchPoints -= tech.researchCost;
        Current.researchedTechIds.Add(tech.id);
        AudioManager.PlaySfx(researchSound, researchVolume);
        OnResearchPointsChanged?.Invoke();
        OnTechResearched?.Invoke(tech.id);
        Save();
        return true;
    }

    public bool IsBlockUnlocked(BlockDefinition block, TechDatabase techDatabase)
    {
        var tech = techDatabase != null ? techDatabase.GetTechForBlock(block) : null;
        return tech == null || IsTechResearched(tech);
    }

    [Header("Hangar upgrade cost — columns")]
    [Tooltip("Research-point cost of the first column upgrade.")]
    public int hangarColumnBaseCost = 200;
    [Tooltip("Extra research-point cost added per column already bought.")]
    public int hangarColumnCostPerLevel = 100;

    [Header("Hangar upgrade cost — rows")]
    [Tooltip("Research-point cost of the first row upgrade.")]
    public int hangarRowBaseCost = 200;
    [Tooltip("Extra research-point cost added per row already bought.")]
    public int hangarRowCostPerLevel = 100;

    public int HangarColumns => FocusedShip.hangarColumns;
    public int HangarRows => FocusedShip.hangarRows;

    public int GetHangarColumnUpgradeCost() => hangarColumnBaseCost + FocusedShip.hangarColumns * hangarColumnCostPerLevel;

    public int GetHangarRowUpgradeCost() => hangarRowBaseCost + FocusedShip.hangarRows * hangarRowCostPerLevel;

    public bool CanUpgradeHangarColumn() => Current.researchPoints >= GetHangarColumnUpgradeCost();
    public bool CanUpgradeHangarRow() => Current.researchPoints >= GetHangarRowUpgradeCost();

    public bool TryUpgradeHangarColumn(ShipGrid grid) => TryUpgradeHangar(grid, addColumn: true);

    public bool TryUpgradeHangarRow(ShipGrid grid) => TryUpgradeHangar(grid, addColumn: false);

    private bool TryUpgradeHangar(ShipGrid grid, bool addColumn)
    {
        int cost = addColumn ? GetHangarColumnUpgradeCost() : GetHangarRowUpgradeCost();
        if (Current.researchPoints < cost)
        {
            OnInsufficientResearchPoints?.Invoke();
            return false;
        }

        Current.researchPoints -= cost;
        var ship = FocusedShip;
        if (addColumn) ship.hangarColumns++;
        else ship.hangarRows++;

        if (grid != null) grid.SetHangarSize(ship.hangarColumns, ship.hangarRows);
        OnResearchPointsChanged?.Invoke();
        OnHangarLevelChanged?.Invoke();
        Save();
        return true;
    }

    [Header("Battle credits multiplier upgrade")]
    public int creditsMultiplierUpgradeBaseCost = 200;
    public int creditsMultiplierUpgradeCostPerLevel = 100;
    [Tooltip("Multiplier bonus granted per level — 0.1 means +10% battle credits per level.")]
    public float creditsMultiplierPerLevel = 0.1f;

    public int CreditsMultiplierLevel => Current.creditsMultiplierLevel;
    public float GetCreditsMultiplier() => 1f + Current.creditsMultiplierLevel * creditsMultiplierPerLevel;
    public int GetCreditsMultiplierUpgradeCost() => creditsMultiplierUpgradeBaseCost + Current.creditsMultiplierLevel * creditsMultiplierUpgradeCostPerLevel;
    public bool CanUpgradeCreditsMultiplier() => Current.researchPoints >= GetCreditsMultiplierUpgradeCost();

    public bool TryUpgradeCreditsMultiplier()
    {
        int cost = GetCreditsMultiplierUpgradeCost();
        if (Current.researchPoints < cost) { OnInsufficientResearchPoints?.Invoke(); return false; }

        Current.researchPoints -= cost;
        Current.creditsMultiplierLevel++;
        OnResearchPointsChanged?.Invoke();
        OnCreditsMultiplierLevelChanged?.Invoke();
        Save();
        return true;
    }

    [Header("Survival start level upgrade")]
    public int survivalStartLevelUpgradeBaseCost = 200;
    public int survivalStartLevelUpgradeCostPerLevel = 100;

    public int SurvivalStartLevelUpgrades => Current.survivalStartLevelUpgrades;
    public int GetSurvivalStartLevel() => 1 + Current.survivalStartLevelUpgrades;
    public int GetSurvivalStartLevelUpgradeCost() => survivalStartLevelUpgradeBaseCost + Current.survivalStartLevelUpgrades * survivalStartLevelUpgradeCostPerLevel;
    public bool CanUpgradeSurvivalStartLevel() => Current.researchPoints >= GetSurvivalStartLevelUpgradeCost();

    public bool TryUpgradeSurvivalStartLevel()
    {
        int cost = GetSurvivalStartLevelUpgradeCost();
        if (Current.researchPoints < cost) { OnInsufficientResearchPoints?.Invoke(); return false; }

        Current.researchPoints -= cost;
        Current.survivalStartLevelUpgrades++;
        OnResearchPointsChanged?.Invoke();
        OnSurvivalStartLevelChanged?.Invoke();
        Save();
        return true;
    }

    public int GetStars(LevelDefinition level)
    {
        if (level == null) return 0;
        return Current.levelStars.FirstOrDefault(e => e.levelId == level.id)?.stars ?? 0;
    }

    public bool IsLevelCompleted(LevelDefinition level)
        => level != null && Current.levelStars.Any(e => e.levelId == level.id);

    public bool IsLevelUnlocked(LevelDefinition level)
        => level != null && (level.prerequisite == null || IsLevelCompleted(level.prerequisite));

    public void SetLevelStars(LevelDefinition level, int stars)
    {
        if (level == null) return;

        var entry = Current.levelStars.FirstOrDefault(e => e.levelId == level.id);
        if (entry == null)
        {
            Current.levelStars.Add(new LevelStarEntry { levelId = level.id, stars = stars });
        }
        else
        {
            if (stars <= entry.stars) return;
            entry.stars = stars;
        }

        OnLevelStarsChanged?.Invoke(level.id);
        Save();
    }

    [Header("Player level curve")]
    [Tooltip("Every player starts at level 0. This is the experience required to advance from level 0 to level 1.")]
    public int baseExperiencePerLevel = 100;
    [Tooltip("Multiplies the experience required for each level after the first — 1.2 means each level " +
             "needs 20% more than the last.")]
    public float experienceGrowthPerLevel = 1.2f;

    [Header("Research points per level-up")]
    [Tooltip("Research points granted every time the player's account level goes up (on top of whatever " +
             "credits/experience the battle itself awarded) — multiplied by however many levels a single " +
             "gain crosses, if it's big enough to cross more than one. See AddExperience.")]
    public int researchPointsPerLevel = 20;

    public int PlayerLevel => Current.playerLevel;
    public int PlayerExperience => Current.playerExperience;

    public int GetExperienceRequiredForLevel(int level)
        => Mathf.Max(1, Mathf.RoundToInt(baseExperiencePerLevel * Mathf.Pow(experienceGrowthPerLevel, Mathf.Max(0, level))));

    public struct ExperienceGainStep
    {
        public int level;
        public int startXp, endXp, xpRequired;
    }

    public List<ExperienceGainStep> AddExperience(int amount)
    {
        var steps = new List<ExperienceGainStep>();
        int remaining = Mathf.Max(0, amount);
        bool changed = remaining > 0;
        int levelsGained = 0;

        do
        {
            int level = Current.playerLevel;
            int required = GetExperienceRequiredForLevel(level);
            int startXp = Current.playerExperience;

            int add = Mathf.Min(remaining, Mathf.Max(0, required - startXp));
            Current.playerExperience += add;
            remaining -= add;

            steps.Add(new ExperienceGainStep { level = level, startXp = startXp, endXp = Current.playerExperience, xpRequired = required });

            if (Current.playerExperience < required) break;

            Current.playerExperience = 0;
            Current.playerLevel++;
            levelsGained++;
        } while (remaining > 0);

        if (levelsGained > 0) Current.researchPoints += researchPointsPerLevel * levelsGained;

        if (changed)
        {
            OnExperienceChanged?.Invoke();
            if (levelsGained > 0) OnResearchPointsChanged?.Invoke();
            Save();
        }
        return steps;
    }

    public event Action OnCrewChanged;

    [Header("Crew")]
    [Tooltip("Resolves saved crew members back to their definitions, and holds the starting crew. Required.")]
    public CrewDatabase crewDatabase;
    [Tooltip("Level-up price = (the member's Level Up Base Cost x its current level + current level x this) x its rank — rank 2 pays double and rank 3 triple, since more perks level up at once.")]
    public int crewLevelCostExtraPerLevel = 30;
    [Range(0f, 1f), Tooltip("Upper limit for every CHANCE perk (zero damage, evasion...) after all crew bonuses are added — 0.75 = never above 75%.")]
    public float crewChanceCap = 0.75f;
    [Min(1f), Tooltip("How many times a critical hit multiplies the damage (the Captain's Crit Chance perk decides how often it happens).")]
    public float critDamageMultiplier = 2f;
    [Tooltip("Credits a rank-up (merge) costs, by the rank the member has NOW: element 0 = merging a rank 1 member into rank 2, element 1 = rank 2 into rank 3.")]
    public int[] crewMergeCosts = { 2000, 4000 };
    [Tooltip("Played whenever a crew member is put into a slot or taken out of one (dragging it in or out, the button on its card) — " +
             "only when something actually changes.")]
    public AudioClip crewSlotSound;
    [Range(0f, 1f)] public float crewSlotVolume = 1f;

    private readonly float[] perkBonus = new float[32];

    public IReadOnlyList<CrewMemberData> CrewMembers => Current.crewMembers;

    public CrewMemberData GetCrewMember(string instanceId)
        => string.IsNullOrEmpty(instanceId) ? null : Current.crewMembers.FirstOrDefault(m => m.instanceId == instanceId);

    public CrewMemberDefinition GetCrewDefinition(CrewMemberData member)
        => member == null || crewDatabase == null ? null : crewDatabase.GetById(member.definitionId);

    public CrewMemberData GetAssignedMember(CrewRole slot)
    {
        var entry = Current.crewSlots.FirstOrDefault(s => s.role == slot);
        return entry == null ? null : GetCrewMember(entry.instanceId);
    }

    public bool IsCrewAssigned(string instanceId)
        => !string.IsNullOrEmpty(instanceId) && Current.crewSlots.Any(s => s.instanceId == instanceId);

    public string AddCrewMember(CrewMemberDefinition definition, int rank = 1)
    {
        if (definition == null) return null;

        var member = NewCrewMember(definition);
        member.rank = Mathf.Clamp(rank, 1, CrewMemberDefinition.MaxRank);
        member.isNew = true;
        Current.crewMembers.Add(member);
        CrewChanged();
        return member.instanceId;
    }

    private static CrewMemberData NewCrewMember(CrewMemberDefinition definition)
        => new() { instanceId = Guid.NewGuid().ToString("N"), definitionId = definition.id, rank = 1, level = 1 };

    public bool TryAssignToSlot(string instanceId, CrewRole slot)
    {
        var member = GetCrewMember(instanceId);
        var definition = GetCrewDefinition(member);
        if (member == null || definition == null || definition.role != slot) return false;

        var entry = Current.crewSlots.FirstOrDefault(s => s.role == slot);
        if (entry == null)
        {
            entry = new CrewSlotEntry { role = slot };
            Current.crewSlots.Add(entry);
        }

        if (entry.instanceId == instanceId) return true;

        entry.instanceId = instanceId;
        AudioManager.PlaySfx(crewSlotSound, crewSlotVolume);
        CrewChanged();
        return true;
    }

    public void UnassignCrew(string instanceId)
    {
        if (!ClearSlotsHolding(instanceId)) return;
        AudioManager.PlaySfx(crewSlotSound, crewSlotVolume);
        CrewChanged();
    }

    private bool ClearSlotsHolding(string instanceId)
    {
        bool changed = false;
        foreach (var slot in Current.crewSlots)
        {
            if (slot.instanceId != instanceId) continue;
            slot.instanceId = "";
            changed = true;
        }
        return changed;
    }

    public int GetCrewLevelUpCost(CrewMemberData member)
    {
        var definition = GetCrewDefinition(member);
        if (definition == null) return 0;
        return (definition.levelUpBaseCost * member.level + member.level * crewLevelCostExtraPerLevel) * Mathf.Max(1, member.rank);
    }

    public bool CanAffordCrewLevelUp(CrewMemberData member) => member != null && Current.credits >= GetCrewLevelUpCost(member);

    public bool TryLevelUpCrew(string instanceId)
    {
        var member = GetCrewMember(instanceId);
        if (member == null || !IsCrewAssigned(instanceId)) return false;

        int cost = GetCrewLevelUpCost(member);
        if (Current.credits < cost)
        {
            NotifyInsufficientCredits();
            return false;
        }

        Current.credits -= cost;
        member.level++;

        OnCreditsChanged?.Invoke();
        CrewChanged();
        return true;
    }

    public bool CanRankUpCrew(CrewMemberData member) => member != null && member.rank < CrewMemberDefinition.MaxRank;

    public bool CanBeMergeFodder(CrewMemberData target, CrewMemberData candidate)
        => target != null && candidate != null
           && candidate.instanceId != target.instanceId
           && candidate.definitionId == target.definitionId
           && candidate.rank == target.rank
           && !IsCrewAssigned(candidate.instanceId);

    public List<CrewMemberData> GetMergeCandidates(CrewMemberData target)
        => Current.crewMembers.Where(m => CanBeMergeFodder(target, m)).OrderBy(m => m.level).ToList();

    public bool CanMergeCrew(CrewMemberData member)
        => CanRankUpCrew(member) && IsCrewAssigned(member.instanceId) && GetMergeCandidates(member).Count >= 2;

    public int GetCrewMergeCost(CrewMemberData member)
    {
        if (member == null || crewMergeCosts == null || crewMergeCosts.Length == 0) return 0;
        return Mathf.Max(0, crewMergeCosts[Mathf.Clamp(member.rank - 1, 0, crewMergeCosts.Length - 1)]);
    }

    public bool CanAffordCrewMerge(CrewMemberData member) => member != null && Current.credits >= GetCrewMergeCost(member);

    public bool TryMergeCrew(string targetId, string fodderAId, string fodderBId)
    {
        var target = GetCrewMember(targetId);
        var fodderA = GetCrewMember(fodderAId);
        var fodderB = GetCrewMember(fodderBId);

        if (target == null || fodderA == null || fodderB == null || fodderA == fodderB) return false;
        if (!IsCrewAssigned(targetId) || !CanRankUpCrew(target)) return false;
        if (!CanBeMergeFodder(target, fodderA) || !CanBeMergeFodder(target, fodderB)) return false;

        int cost = GetCrewMergeCost(target);
        if (Current.credits < cost)
        {
            NotifyInsufficientCredits();
            return false;
        }

        Current.credits -= cost;
        OnCreditsChanged?.Invoke();

        Current.crewMembers.Remove(fodderA);
        Current.crewMembers.Remove(fodderB);

        target.rank++;
        target.level = 1;

        CrewChanged();
        return true;
    }

    public IReadOnlyList<int> SavedMissilePriority
        => Current.missileTargetPriority != null && Current.missileTargetPriority.Count > 0 ? Current.missileTargetPriority : null;

    public void SaveMissilePriority(IEnumerable<ModuleType> order)
    {
        Current.missileTargetPriority = order.Select(t => (int)t).ToList();
        Save();
    }

    public IReadOnlyList<int> SavedTowerPriority
        => Current.towerTargetPriority != null && Current.towerTargetPriority.Count > 0 ? Current.towerTargetPriority : null;

    public void SaveTowerPriority(IEnumerable<ModuleType> order)
    {
        Current.towerTargetPriority = order.Select(t => (int)t).ToList();
        Save();
    }

    [Header("Rewards and tasks")]
    [Tooltip("The reward for each player level (the level bar of the rewards panel).")]
    public PlayerLevelRewards playerLevelRewards;
    [Tooltip("All the tasks of the rewards panel.")]
    public TaskDatabase taskDatabase;
    [Tooltip("Played when the player takes a reward (a level's or a task's).")]
    public AudioClip rewardClaimSound;
    [Range(0f, 1f)] public float rewardClaimVolume = 1f;

    public event Action OnRewardsChanged;

    public void GrantReward(RewardData reward)
    {
        if (reward.amount <= 0) return;

        switch (reward.type)
        {
            case RewardType.Credits: AddCredits(reward.amount); Save(); break;
            case RewardType.Coins: AddCoins(reward.amount); break;
            case RewardType.Experience: AddExperience(reward.amount); break;
            case RewardType.ResearchPoints: AddResearchPoints(reward.amount); break;
            case RewardType.RandomCrewCard: GrantRandomCrewCards(reward.amount, reward.CrewRank); break;
        }
    }

    public void GrantRewards(IReadOnlyList<RewardData> rewards)
    {
        if (rewards == null) return;
        foreach (var reward in rewards) GrantReward(reward);
    }

    public void GrantRandomCrewCards(int count, int rank = 1)
    {
        var members = crewDatabase != null ? crewDatabase.allMembers : null;
        if (members == null || members.Count == 0)
        {
            Debug.LogWarning("GameDataManager: no Crew Database (or it is empty) - the crew card reward cannot be given.", this);
            return;
        }

        var received = new List<CrewMemberData>();
        for (int i = 0; i < count; i++)
        {
            var definition = members[UnityEngine.Random.Range(0, members.Count)];
            if (definition == null) continue;

            var member = GetCrewMember(AddCrewMember(definition, rank));
            if (member != null) received.Add(member);
        }

        if (received.Count > 0) OnCrewCardsGranted?.Invoke(received);
    }

    public event Action<IReadOnlyList<CrewMemberData>> OnCrewCardsGranted;

    public IReadOnlyList<RewardData> GetLevelRewards(int level)
        => playerLevelRewards != null ? playerLevelRewards.GetRewards(level) : System.Array.Empty<RewardData>();

    public bool IsLevelRewardClaimed(int level) => Current.claimedLevelRewards.Contains(level);

    public bool CanClaimLevelReward(int level)
        => level >= 1 && Current.playerLevel >= level && !IsLevelRewardClaimed(level) && GetLevelRewards(level).Any(r => r.amount > 0);

    public bool TryClaimLevelReward(int level)
    {
        if (!CanClaimLevelReward(level)) return false;

        Current.claimedLevelRewards.Add(level);
        GrantRewards(GetLevelRewards(level));
        AudioManager.PlaySfx(rewardClaimSound, rewardClaimVolume);
        Save();
        OnRewardsChanged?.Invoke();
        return true;
    }

    [Header("Daily gifts")]
    [Tooltip("The gifts of the 30 days of daily visits. A missed day starts the series again from day 1, and so does the day after the last one.")]
    public DailyRewards dailyRewards;

    private string dailyCheckedDate;

    public int DailyDay => Current != null ? Current.dailyDay : 0;
    public int DailyDaysCount => dailyRewards != null ? dailyRewards.Count : 0;

    public IReadOnlyList<RewardData> GetDailyRewards(int day)
        => dailyRewards != null ? dailyRewards.GetRewards(day) : System.Array.Empty<RewardData>();

    public void UpdateDailyVisit()
    {
        if (Current == null || DailyDaysCount == 0) return;

        var today = DateTime.Now.Date;
        string key = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (key == dailyCheckedDate) return;
        dailyCheckedDate = key;
        if (Current.dailyLastVisit == key) return;

        bool hasLast = DateTime.TryParseExact(Current.dailyLastVisit, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var last);
        if (hasLast && last > today) return;

        bool nextDay = hasLast && (today - last).Days == 1;
        if (nextDay && Current.dailyDay >= 1 && Current.dailyDay < DailyDaysCount)
        {
            Current.dailyDay++;
        }
        else
        {
            Current.dailyDay = 1;
            Current.dailyClaimedDays.Clear();
        }

        Current.dailyLastVisit = key;
        Save();
        OnRewardsChanged?.Invoke();
    }

    public void RecheckDailyVisit()
    {
        dailyCheckedDate = null;
        UpdateDailyVisit();
    }

    public bool IsDailyRewardClaimed(int day) => Current != null && Current.dailyClaimedDays.Contains(day);

    public bool IsDailyRewardReached(int day) => Current != null && day >= 1 && day <= Current.dailyDay;

    public bool CanClaimDailyReward(int day)
        => IsDailyRewardReached(day) && !IsDailyRewardClaimed(day) && GetDailyRewards(day).Any(r => r.amount > 0);

    public bool TryClaimDailyReward(int day)
    {
        UpdateDailyVisit();
        if (!CanClaimDailyReward(day)) return false;

        Current.dailyClaimedDays.Add(day);
        GrantRewards(GetDailyRewards(day));
        AudioManager.PlaySfx(rewardClaimSound, rewardClaimVolume);
        Save();
        OnRewardsChanged?.Invoke();
        return true;
    }

    public bool HasClaimableDailyReward()
    {
        if (Current == null) return false;
        UpdateDailyVisit();

        for (int day = 1; day <= Mathf.Min(Current.dailyDay, DailyDaysCount); day++)
        {
            if (CanClaimDailyReward(day)) return true;
        }

        return false;
    }

    private TaskStateEntry GetTaskState(string id, bool create)
    {
        var state = Current.taskStates.FirstOrDefault(s => s.id == id);
        if (state == null && create)
        {
            state = new TaskStateEntry { id = id };
            Current.taskStates.Add(state);
        }

        return state;
    }

    public int GetTaskProgress(TaskDefinition task)
    {
        if (task == null) return 0;

        int value = task.kind switch
        {
            TaskKind.DestroyBlocks => GetTaskState(task.id, false)?.progress ?? 0,
            TaskKind.CompleteCampaignLevel => task.level != null && GetStars(task.level) > 0 ? 1 : 0,
            TaskKind.SurvivalWave => Current.survivalBestWave,
            _ => 0,
        };

        return Mathf.Clamp(value, 0, task.TargetValue);
    }

    public bool IsTaskComplete(TaskDefinition task) => task != null && GetTaskProgress(task) >= task.TargetValue;

    public bool IsTaskClaimed(TaskDefinition task) => task != null && (GetTaskState(task.id, false)?.claimed ?? false);

    public bool TryClaimTask(TaskDefinition task)
    {
        if (task == null || !IsTaskComplete(task) || IsTaskClaimed(task)) return false;

        GetTaskState(task.id, true).claimed = true;
        GrantRewards(task.Rewards);
        AudioManager.PlaySfx(rewardClaimSound, rewardClaimVolume);
        Save();
        OnRewardsChanged?.Invoke();
        return true;
    }

    public void RecordBlockDestroyed(ModuleType type)
    {
        if (taskDatabase == null) return;

        bool changed = false;
        foreach (var task in taskDatabase.tasks)
        {
            if (task == null || task.kind != TaskKind.DestroyBlocks) continue;
            if (!task.anyBlockType && task.blockType != type) continue;

            var state = GetTaskState(task.id, true);
            if (state.claimed || state.progress >= task.TargetValue) continue;

            state.progress++;
            changed = true;
        }

        if (changed) OnRewardsChanged?.Invoke();
    }

    public void RecordSurvivalWave(int wave)
    {
        if (wave <= Current.survivalBestWave) return;

        Current.survivalBestWave = wave;
        Save();
        OnRewardsChanged?.Invoke();
    }

    public bool HasClaimableRewards()
    {
        if (Current == null) return false;

        int levels = playerLevelRewards != null ? Mathf.Min(Current.playerLevel, playerLevelRewards.Count) : 0;
        for (int level = 1; level <= levels; level++)
        {
            if (CanClaimLevelReward(level)) return true;
        }

        if (taskDatabase != null)
        {
            foreach (var task in taskDatabase.tasks)
            {
                if (task != null && IsTaskComplete(task) && !IsTaskClaimed(task)) return true;
            }
        }

        return false;
    }

    public bool CanResearchAny(TechDatabase techDatabase)
        => Current != null && techDatabase != null && techDatabase.allTechs.Any(CanResearch);

    public bool HasNewCrew => Current != null && Current.crewMembers.Any(m => m != null && m.isNew);

    public void MarkCrewSeen()
    {
        if (!HasNewCrew) return;

        foreach (var member in Current.crewMembers)
        {
            if (member != null) member.isNew = false;
        }

        Save();
    }

    public bool AdsRemoved => Current != null && Current.adsRemoved;

    public void SetAdsRemoved(bool removed)
    {
        Current.adsRemoved = removed;
        Save();
    }

    [Header("Shop")]
    [Tooltip("How many times the premium multiplies the credits and experience of every battle.")]
    [Min(1f)] public float premiumRewardMultiplier = 2f;
    [Tooltip("Played when a shop purchase has been given out.")]
    public AudioClip shopPurchaseSound;
    [Range(0f, 1f)] public float shopPurchaseVolume = 1f;

    public event Action OnShopChanged;

    public bool IsPremium => Current != null && Current.premium;

    public float PremiumRewardMultiplier => IsPremium ? premiumRewardMultiplier : 1f;

    public bool IsShopProductOwned(ShopProduct product)
        => product != null && product.IsOneTime && Current != null && Current.ownedShopProducts.Contains(product.id);

    public bool TryBuyWithCoins(ShopProduct product)
    {
        if (product == null || product.IsRealMoney || IsShopProductOwned(product)) return false;
        if (!SpendCoins(product.coinPrice)) return false;

        GrantShopProduct(product);
        return true;
    }

    public void GrantShopProduct(ShopProduct product)
    {
        if (product == null) return;

        GrantRewards(product.rewards);
        ApplyShopEntitlement(product);
        AudioManager.PlaySfx(shopPurchaseSound, shopPurchaseVolume);
        Save();
        OnShopChanged?.Invoke();
    }

    public void RestoreShopProduct(ShopProduct product)
    {
        if (product == null || !product.IsOneTime || IsShopProductOwned(product)) return;

        ApplyShopEntitlement(product);
        Save();
        OnShopChanged?.Invoke();
    }

    private void ApplyShopEntitlement(ShopProduct product)
    {
        if (product.grantsPremium)
        {
            Current.premium = true;
            Current.adsRemoved = true;
        }

        if (product.IsOneTime && !Current.ownedShopProducts.Contains(product.id)) Current.ownedShopProducts.Add(product.id);
    }

    public bool IsOrderProcessed(string transactionId)
        => !string.IsNullOrEmpty(transactionId) && Current != null && Current.processedOrders.Contains(transactionId);

    public void MarkOrderProcessed(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId) || IsOrderProcessed(transactionId)) return;

        Current.processedOrders.Add(transactionId);
        if (Current.processedOrders.Count > 200) Current.processedOrders.RemoveAt(0);
        Save();
    }

    public bool TutorialOff => Current != null && Current.tutorialOff;

    public void SetTutorialOff(bool off)
    {
        Current.tutorialOff = off;
        Save();
    }

    [ContextMenu("Reset tutorial")]
    public void ResetTutorial()
    {
        if (Current == null) return;
        Current.tutorialDoneSteps.Clear();
        Current.tutorialOff = false;
        Save();
    }

    public bool IsTutorialStepDone(string id) => Current != null && Current.tutorialDoneSteps.Contains(id);

    public void MarkTutorialStepDone(string id)
    {
        if (string.IsNullOrEmpty(id) || IsTutorialStepDone(id)) return;
        Current.tutorialDoneSteps.Add(id);
        Save();
    }

    public float GetCrewPerkMultiplier(CrewPerkType type)
    {
        int index = (int)type;
        return index > 0 && index < perkBonus.Length ? 1f + perkBonus[index] : 1f;
    }

    public float GetCrewChance(CrewPerkType type)
    {
        int index = (int)type;
        float chance = index > 0 && index < perkBonus.Length ? perkBonus[index] : 0f;
        return Mathf.Clamp(chance, 0f, crewChanceCap);
    }

    public bool RollCrewChance(CrewPerkType type)
    {
        float chance = GetCrewChance(type);
        return chance > 0f && UnityEngine.Random.value < chance;
    }

    private void RebuildPerkBonuses()
    {
        Array.Clear(perkBonus, 0, perkBonus.Length);
        if (Current == null) return;

        foreach (var slot in Current.crewSlots)
        {
            var member = GetCrewMember(slot.instanceId);
            var definition = GetCrewDefinition(member);
            if (member == null || definition == null) continue;

            int unlocked = Mathf.Min(member.rank, definition.perks.Length);
            for (int i = 0; i < unlocked; i++)
            {
                var perk = definition.perks[i];
                int index = perk != null ? (int)perk.type : 0;
                if (index <= 0 || index >= perkBonus.Length) continue;

                perkBonus[index] += perk.GetBonus(member.level);
            }
        }
    }

    private void CrewChanged()
    {
        RebuildPerkBonuses();
        OnCrewChanged?.Invoke();
        Save();
    }

    private void GrantStarterCrewIfNeeded()
    {
        if (Current.crewStarterGranted || crewDatabase == null) return;

        Current.crewStarterGranted = true;
        foreach (var definition in crewDatabase.starterCrew)
            if (definition != null) Current.crewMembers.Add(NewCrewMember(definition));

        Save();
    }

    public int GetResource(string id) => Current.resources.FirstOrDefault(r => r.id == id)?.amount ?? 0;

    public void AddResource(string id, int amount)
    {
        var entry = Current.resources.FirstOrDefault(r => r.id == id);
        if (entry == null)
        {
            entry = new ResourceEntry { id = id, amount = 0 };
            Current.resources.Add(entry);
        }
        entry.amount += amount;
        OnResourceChanged?.Invoke(id);
        Save();
    }

    public bool SpendResource(string id, int amount)
    {
        var entry = Current.resources.FirstOrDefault(r => r.id == id);
        if (entry == null || entry.amount < amount) return false;

        entry.amount -= amount;
        OnResourceChanged?.Invoke(id);
        Save();
        return true;
    }
}
