using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Single point of access to the player's save data. Loads GameData once at game start
/// (Awake, before any scene logic needs it), keeps it in memory, and persists to disk
/// through SaveSystem whenever something changes.
/// Put this on a bootstrap object in your very first scene, marked DontDestroyOnLoad.
/// </summary>
public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public GameData Current { get; private set; }

    public event Action OnDataLoaded;
    public event Action OnCreditsChanged;
    public event Action OnCoinsChanged;
    public event Action<string> OnResourceChanged;
    // Fired on a failed spend attempt (not enough of that currency) — UI (CurrencyDisplay) uses
    // these to trigger its "can't afford it" pulse; distinct from OnCreditsChanged/OnCoinsChanged,
    // which fire on every successful change, not on rejections.
    public event Action OnInsufficientCredits;
    public event Action OnInsufficientCoins;
    public event Action OnResearchPointsChanged;
    public event Action OnInsufficientResearchPoints;
    /// <summary>Fired with the researched tech's id right after TryResearch succeeds — e.g. so a
    /// TechNodeView can refresh itself when a DIFFERENT node's research just unlocked it.</summary>
    public event Action<string> OnTechResearched;
    public event Action OnHangarLevelChanged;
    public event Action OnCreditsMultiplierLevelChanged;
    public event Action OnSurvivalStartLevelChanged;
    /// <summary>Fired with the level's id right after SetLevelStars actually changes something — e.g.
    /// so a LevelButtonView can refresh itself, or a neighboring level's lock/unlock state if it was
    /// gated on this one being completed.</summary>
    public event Action<string> OnLevelStarsChanged;
    /// <summary>Fired whenever AddExperience actually changes playerExperience/playerLevel (a zero-xp
    /// call is a no-op and doesn't fire this).</summary>
    public event Action OnExperienceChanged;

    // Credits as they stood at the last save (or at BeginBuildSession, if nothing's been saved
    // since) — what RevertCredits() rolls back to on an unsaved exit from the builder.
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

    // ---------- Load / Save ----------

    /// <summary>Loads the save file, or creates a fresh empty GameData if none exists (first launch).</summary>
    public void LoadOrCreate()
    {
        Current = SaveSystem.Load() ?? new GameData();

        // An old save counted one combined hangar level (a row AND a column each): convert it once.
        if (Current.hangarLevel > 0)
        {
            Current.hangarColumns += Current.hangarLevel;
            Current.hangarRows += Current.hangarLevel;
            Current.hangarLevel = 0;
        }

        GrantStarterKitIfNeeded();
        GrantStarterCrewIfNeeded();
        RebuildPerkBonuses();
        OnDataLoaded?.Invoke();
    }

    // ---------- New game: starting technologies and ship ----------

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

    /// <summary>Hands out the starting technologies and the starting ship, once per save. The ship only goes to a save that has
    /// none yet, so an old save keeps the player's own ship.</summary>
    private void GrantStarterKitIfNeeded()
    {
        if (Current.starterKitGranted) return;
        Current.starterKitGranted = true;

        foreach (var id in starterTechIds)
            if (!string.IsNullOrEmpty(id) && !Current.researchedTechIds.Contains(id)) Current.researchedTechIds.Add(id);

        if ((Current.playerShip == null || Current.playerShip.TotalEntries == 0) && starterShip != null && starterShip.TotalEntries > 0)
        {
            Current.playerShip = CopyLayout(starterShip);
            Current.hangarColumns = Mathf.Max(Current.hangarColumns, starterHangarColumns); // room for the starter ship
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

    /// <summary>The ship built in the editor and saved on 28.10 (CyberForgeStudio save) — the default starting ship. A 3 x 3 ship,
    /// moved to the middle of the builder's 5 x 5 base grid.</summary>
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
        Add("kinetic_static_gun1_1x1", 1, 2);
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
        if (saved == null || saved.playerShip == null || saved.playerShip.TotalEntries == 0)
        {
            Debug.LogWarning("No saved ship found — build a ship in the builder and save it first.", this);
            return;
        }

        UnityEditor.Undo.RecordObject(this, "Use saved ship as starter ship");
        starterShip = CopyLayout(saved.playerShip);

        // The save was built in the player's (possibly upgraded) grid: move the ship to the middle of a NEW player's grid.
        int minX = starterShip.blocks.Min(b => b.anchorX), maxX = starterShip.blocks.Max(b => b.anchorX);
        int minY = starterShip.blocks.Min(b => b.anchorY), maxY = starterShip.blocks.Max(b => b.anchorY);
        int gridW = builderBaseGridSize.x + starterHangarColumns, gridH = builderBaseGridSize.y + starterHangarRows;
        int shiftX = (gridW - (maxX - minX + 1)) / 2 - minX;
        int shiftY = (gridH - (maxY - minY + 1)) / 2 - minY;
        foreach (var b in starterShip.blocks) { b.anchorX += shiftX; b.anchorY += shiftY; }

        UnityEditor.EditorUtility.SetDirty(this);
        bool fits = maxX - minX + 1 <= gridW && maxY - minY + 1 <= gridH;
        Debug.Log($"Starter ship set from the save: {starterShip.blocks.Count} blocks, {maxX - minX + 1} x {maxY - minY + 1} cells" +
                  (fits ? "" : $" — DOES NOT FIT the new player's {gridW} x {gridH} grid, raise Starter Hangar Columns / Rows") +
                  ". Save the scene to keep it.", this);
    }

    [ContextMenu("Delete save file (start as a new player)")]
    private void DeleteSaveFile()
    {
        SaveSystem.DeleteSave();
        Debug.Log("Save file deleted — the next Play starts a fresh game with the starter technologies and ship.", this);
    }
#endif

    public void Save() => SaveSystem.Save(Current);

    // ---------- Ship build ----------

    /// <summary>Call from the ship editor's "Save" / "Confirm build" button. Also becomes the new
    /// baseline RevertCredits() rolls back to — saving locks in whatever was spent/refunded so far
    /// this session as the new "safe" point.</summary>
    public void SaveShip(ShipGrid grid)
    {
        Current.playerShip = grid.ExportLayout();
        Save();
        creditsAtSessionStart = Current.credits;
    }

    /// <summary>
    /// Call once when the editor scene opens (or at game start if the ship
    /// is built directly in the bootstrap flow) to restore the saved build.
    /// Does nothing if the player has never saved a ship yet.
    /// </summary>
    public void LoadShip(ShipGrid grid, BlockDatabase database)
    {
        if (Current.playerShip == null || Current.playerShip.TotalEntries == 0) return;
        grid.BuildFromLayout(Current.playerShip, database, Faction.Player);
    }

    /// <summary>Call when entering the ship builder — remembers the current credits as the point
    /// RevertCredits() rolls back to if the player leaves without saving. Also call once at game
    /// start alongside LoadShip, so the very first build session has a correct baseline.</summary>
    public void BeginBuildSession()
    {
        creditsAtSessionStart = Current.credits;
    }

    /// <summary>
    /// Call from the builder's "Close"/"Exit" button — throws away whatever was placed/deleted
    /// this session by rebuilding the grid from the last SAVED layout, discarding anything since.
    /// Unlike LoadShip, this always rebuilds — including clearing the grid back to empty if the
    /// player has never saved a ship yet, so a first-time, never-saved build gets fully discarded too.
    /// </summary>
    public void RevertShip(ShipGrid grid, BlockDatabase database)
    {
        grid.BuildFromLayout(Current.playerShip, database, Faction.Player);
    }

    /// <summary>Call alongside RevertShip when leaving the builder without saving — undoes any
    /// credits spent on building or refunded from dismantling this session, back to whatever they
    /// were at BeginBuildSession (or the last SaveShip, whichever is more recent).</summary>
    public void RevertCredits()
    {
        Current.credits = creditsAtSessionStart;
        OnCreditsChanged?.Invoke();
    }

    public bool HasSavedShip => Current.playerShip != null && Current.playerShip.TotalEntries > 0;

    // ---------- Credits ----------
    // Soft currency spent on building/repairing. Deliberately NOT auto-saved on every change —
    // building/dismantling happens continuously during a session and must stay revertible
    // (RevertCredits) until the player explicitly saves (SaveShip persists it to disk).

    public int Credits => Current.credits;

    public void AddCredits(int amount)
    {
        Current.credits += amount;
        OnCreditsChanged?.Invoke();
    }

    /// <summary>Returns false (and spends nothing) if the player can't afford it.</summary>
    public bool SpendCredits(int amount)
    {
        if (Current.credits < amount) { NotifyInsufficientCredits(); return false; }
        Current.credits -= amount;
        OnCreditsChanged?.Invoke();
        return true;
    }

    /// <summary>Fires OnInsufficientCredits — e.g. CurrencyDisplay's "can't afford it" pulse. C# events
    /// can only be raised from inside their declaring class, so callers that detect an affordability
    /// failure themselves (GhostBlockController, when a drag-drop is rejected on cost) go through this
    /// instead of SpendCredits, which they never actually call in that case.</summary>
    public void NotifyInsufficientCredits() => OnInsufficientCredits?.Invoke();

    // ---------- Coins (premium currency, purchased with real money) ----------
    // Unlike credits, these ARE saved immediately — a real-money purchase should never be lost by
    // exiting the builder without saving, so they're not part of the build-session revert at all.

    public int Coins => Current.coins;

    public void AddCoins(int amount)
    {
        Current.coins += amount;
        OnCoinsChanged?.Invoke();
        Save();
    }

    /// <summary>Returns false (and spends nothing) if the player doesn't have enough coins.</summary>
    public bool SpendCoins(int amount)
    {
        if (Current.coins < amount) { NotifyInsufficientCoins(); return false; }
        Current.coins -= amount;
        OnCoinsChanged?.Invoke();
        Save();
        return true;
    }

    /// <summary>Fires OnInsufficientCoins — see NotifyInsufficientCredits for why this wrapper exists.</summary>
    public void NotifyInsufficientCoins() => OnInsufficientCoins?.Invoke();

    // ---------- Research points & technologies ----------
    // Research is meta-progression, not tied to any one build session — spending is saved
    // immediately (like coins), never reverted by exiting the builder without saving.

    public int ResearchPoints => Current.researchPoints;

    public void AddResearchPoints(int amount)
    {
        Current.researchPoints += amount;
        OnResearchPointsChanged?.Invoke();
        Save();
    }

    public bool IsTechResearched(TechDefinition tech) => tech != null && Current.researchedTechIds.Contains(tech.id);

    /// <summary>True if this tech isn't researched yet, its prerequisite (if any) is, and the player
    /// currently has enough points — i.e. TryResearch would succeed right now.</summary>
    public bool CanResearch(TechDefinition tech)
    {
        if (tech == null || IsTechResearched(tech)) return false;
        if (tech.prerequisite != null && !IsTechResearched(tech.prerequisite)) return false;
        return Current.researchPoints >= tech.researchCost;
    }

    /// <summary>Spends points and marks the tech researched. Returns false and changes nothing if
    /// it's already researched, its prerequisite isn't met, or there aren't enough points (which
    /// also fires OnInsufficientResearchPoints in that last case specifically).</summary>
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
        OnResearchPointsChanged?.Invoke();
        OnTechResearched?.Invoke(tech.id);
        Save();
        return true;
    }

    /// <summary>Whether a block can currently be built — true if no tech gates it, or that tech has
    /// been researched. BuildPaletteUI uses this (via its optional TechDatabase field) to hide
    /// blocks the player hasn't unlocked yet.</summary>
    public bool IsBlockUnlocked(BlockDefinition block, TechDatabase techDatabase)
    {
        var tech = techDatabase != null ? techDatabase.GetTechForBlock(block) : null;
        return tech == null || IsTechResearched(tech);
    }

    // ---------- Hangar (build grid) upgrades ----------
    // Two separate purchases with their OWN prices: one adds a COLUMN to the ship's buildable grid, the other
    // a ROW (see ShipGrid.SetHangarSize). Each has its own base cost and its own growth per purchase, and
    // each price depends only on how many upgrades of THAT kind were bought. Paid with research points, same
    // currency and immediate-persistence semantics as researching a technology — not part of the revertible
    // build-session state.

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

    public int HangarColumns => Current.hangarColumns;
    public int HangarRows => Current.hangarRows;

    /// <summary>Price of the next column.</summary>
    public int GetHangarColumnUpgradeCost() => hangarColumnBaseCost + Current.hangarColumns * hangarColumnCostPerLevel;

    /// <summary>Price of the next row.</summary>
    public int GetHangarRowUpgradeCost() => hangarRowBaseCost + Current.hangarRows * hangarRowCostPerLevel;

    public bool CanUpgradeHangarColumn() => Current.researchPoints >= GetHangarColumnUpgradeCost();
    public bool CanUpgradeHangarRow() => Current.researchPoints >= GetHangarRowUpgradeCost();

    /// <summary>Spends research points to add one column to the given grid's buildable area. Returns false
    /// (and changes nothing) if there aren't enough points, firing OnInsufficientResearchPoints the same way
    /// TryResearch does.</summary>
    public bool TryUpgradeHangarColumn(ShipGrid grid) => TryUpgradeHangar(grid, addColumn: true);

    /// <summary>Same as TryUpgradeHangarColumn, but adds a row.</summary>
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
        if (addColumn) Current.hangarColumns++;
        else Current.hangarRows++;

        if (grid != null) grid.SetHangarSize(Current.hangarColumns, Current.hangarRows);
        OnResearchPointsChanged?.Invoke();
        OnHangarLevelChanged?.Invoke();
        Save();
        return true;
    }

    // ---------- Battle credits-reward multiplier upgrade ----------
    // Multiplies whatever credits a battle would award — the battle-reward code (not written yet)
    // should call GetCreditsMultiplier() and scale its base reward by it.

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

    // ---------- Survival-mode starting-level upgrade ----------
    // Raises the level survival mode (not written yet) begins at — that mode's start-up code should
    // call GetSurvivalStartLevel() for its initial difficulty level.

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

    // ---------- Campaign levels ----------
    // A level is unlocked once its prerequisite (if any) has been completed — see LevelDefinition.
    // Star rating (0-3) is tracked separately and only ever improves; a repeat clear with fewer stars
    // than a previous best doesn't overwrite it.

    public int GetStars(LevelDefinition level)
    {
        if (level == null) return 0;
        return Current.levelStars.FirstOrDefault(e => e.levelId == level.id)?.stars ?? 0;
    }

    public bool IsLevelCompleted(LevelDefinition level)
        => level != null && Current.levelStars.Any(e => e.levelId == level.id);

    public bool IsLevelUnlocked(LevelDefinition level)
        => level != null && (level.prerequisite == null || IsLevelCompleted(level.prerequisite));

    /// <summary>Records a level's result. Call once a battle (not implemented yet) resolves — the
    /// battle-end code should pass however many of 3 stars the player earned. A no-op if that's not
    /// better than the level's existing best.</summary>
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
            if (stars <= entry.stars) return; // not an improvement — nothing to save or notify
            entry.stars = stars;
        }

        OnLevelStarsChanged?.Invoke(level.id);
        Save();
    }

    // ---------- Player level & experience ----------
    // Meta-progression separate from the ship: earned from battle rewards (BattleOutcomeController) and
    // shown on the result panel's level-up bar (ExperienceBarView). Persisted immediately, like coins/
    // research — never part of the build-session revert.

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

    /// <summary>Experience needed to advance FROM this level to the next.</summary>
    public int GetExperienceRequiredForLevel(int level)
        => Mathf.Max(1, Mathf.RoundToInt(baseExperiencePerLevel * Mathf.Pow(experienceGrowthPerLevel, Mathf.Max(0, level))));

    /// <summary>One level's worth of an experience gain — how full the bar was before and after, and how
    /// much that level needed in total. A single big gain can span several of these (one per level it
    /// rolled past) — see ExperienceBarView.PlaySteps, which animates through them in order.</summary>
    public struct ExperienceGainStep
    {
        public int level;
        public int startXp, endXp, xpRequired;
    }

    /// <summary>
    /// Adds experience, leveling up (possibly more than once) if it fills the current level's bar — each
    /// level-up also grants researchPointsPerLevel research points. Returns the sequence of per-level
    /// steps the gain passed through — ALWAYS at least one entry (a zero-length step at the player's
    /// current standing) even when amount is 0 or negative, so a caller can use the same result to just
    /// show the current position with nothing to animate.
    /// </summary>
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

            if (Current.playerExperience < required) break; // didn't fill this level — nothing left to carry over anyway

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

    // ---------- Ship's crew ----------
    // The player owns crew MEMBERS (CrewMemberData — a copy of some CrewMemberDefinition with its own rank
    // and level). Up to 4 of them stand in the 4 slots, one per CrewRole. ONLY members standing in a slot
    // count: their unlocked perks (the Nth perk in the definition unlocks at rank N) feed the player's ship through
    // GetCrewPerkMultiplier, scaled by the member's level. A member can be levelled up with credits only
    // while standing in a slot; two copies of the same definition and rank can be merged into a higher
    // rank (TryMergeCrew) — which resets the level and unlocks the next perk; the ranked-up member stays in its slot.

    /// <summary>Fired after ANY crew change — a member added/removed, moved between slot and collection,
    /// levelled up or ranked up. Crew UI rebuilds itself on this.</summary>
    public event Action OnCrewChanged;

    [Header("Crew")]
    [Tooltip("Resolves saved crew members back to their definitions, and holds the starting crew. Required.")]
    public CrewDatabase crewDatabase;
    [Tooltip("Level-up price = (the member's Level Up Base Cost x its current level + current level x this) x its rank — rank 2 pays double and rank 3 triple, since more perks level up at once.")]
    public int crewLevelCostExtraPerLevel = 15;
    [Range(0f, 1f), Tooltip("Upper limit for every CHANCE perk (zero damage, evasion...) after all crew bonuses are added — 0.75 = never above 75%.")]
    public float crewChanceCap = 0.75f;
    [Min(1f), Tooltip("How many times a critical hit multiplies the damage (the Captain's Crit Chance perk decides how often it happens).")]
    public float critDamageMultiplier = 2f;
    [Tooltip("Credits a rank-up (merge) costs, by the rank the member has NOW: element 0 = merging a rank 1 member into rank 2, element 1 = rank 2 into rank 3.")]
    public int[] crewMergeCosts = { 1000, 2000 };
    [Tooltip("Played whenever a crew member is put into a slot or taken out of one (dragging it in or out, the button on its card) — " +
             "only when something actually changes.")]
    public AudioClip crewSlotSound;
    [Range(0f, 1f)] public float crewSlotVolume = 1f;

    // Total bonus per CrewPerkType from the members in the slots, rebuilt whenever crew changes. Cached
    // because gameplay reads it every frame (weapons, movement) and recomputing from the save list there
    // would mean list scans per weapon per frame.
    private readonly float[] perkBonus = new float[32];

    public IReadOnlyList<CrewMemberData> CrewMembers => Current.crewMembers;

    public CrewMemberData GetCrewMember(string instanceId)
        => string.IsNullOrEmpty(instanceId) ? null : Current.crewMembers.FirstOrDefault(m => m.instanceId == instanceId);

    public CrewMemberDefinition GetCrewDefinition(CrewMemberData member)
        => member == null || crewDatabase == null ? null : crewDatabase.GetById(member.definitionId);

    /// <summary>The member currently standing in this slot, or null if it's empty.</summary>
    public CrewMemberData GetAssignedMember(CrewRole slot)
    {
        var entry = Current.crewSlots.FirstOrDefault(s => s.role == slot);
        return entry == null ? null : GetCrewMember(entry.instanceId);
    }

    public bool IsCrewAssigned(string instanceId)
        => !string.IsNullOrEmpty(instanceId) && Current.crewSlots.Any(s => s.instanceId == instanceId);

    /// <summary>Gives the player a fresh copy (rank 1, level 1) of this crew member, unassigned. Call from
    /// whatever hands crew out — shop, loot, rewards. Returns the new copy's instanceId.</summary>
    public string AddCrewMember(CrewMemberDefinition definition)
    {
        if (definition == null) return null;

        var member = NewCrewMember(definition);
        Current.crewMembers.Add(member);
        CrewChanged();
        return member.instanceId;
    }

    private static CrewMemberData NewCrewMember(CrewMemberDefinition definition)
        => new() { instanceId = Guid.NewGuid().ToString("N"), definitionId = definition.id, rank = 1, level = 1 };

    /// <summary>Puts a member into the slot of its own specialty, replacing whoever stood there (who simply
    /// goes back to the collection). False if the member doesn't exist or belongs to a different specialty.</summary>
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

        if (entry.instanceId == instanceId) return true; // already there — nothing to change

        entry.instanceId = instanceId;
        AudioManager.PlaySfx(crewSlotSound, crewSlotVolume);
        CrewChanged();
        return true;
    }

    /// <summary>Takes a member out of its slot, back to the collection. No-op if it wasn't in one.</summary>
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

    // ----- Level-up -----

    /// <summary>Price of this member's NEXT level: (Level Up Base Cost x current level, plus current level
    /// x Crew Level Cost Extra Per Level) x the member's rank.</summary>
    public int GetCrewLevelUpCost(CrewMemberData member)
    {
        var definition = GetCrewDefinition(member);
        if (definition == null) return 0;
        return (definition.levelUpBaseCost * member.level + member.level * crewLevelCostExtraPerLevel) * Mathf.Max(1, member.rank);
    }

    public bool CanAffordCrewLevelUp(CrewMemberData member) => member != null && Current.credits >= GetCrewLevelUpCost(member);

    /// <summary>Spends credits to raise a member's level by one. Only members standing in a slot can be
    /// levelled. Returns false (spending nothing, and firing OnInsufficientCredits when it was the money
    /// that was missing) if it can't.</summary>
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

    // ----- Merge (rank-up) -----
    // Only a member standing in a slot can be ranked up. It is fed two spare copies from the collection — the
    // exact same kind at the exact same rank — and STAYS in its slot with the new rank.

    public bool CanRankUpCrew(CrewMemberData member) => member != null && member.rank < CrewMemberDefinition.MaxRank;

    /// <summary>Whether `candidate` may be fed to `target` in a merge: a different copy of the exact same
    /// definition (same kind, therefore same specialty) at the exact same rank, sitting in the collection
    /// (not in a slot).</summary>
    public bool CanBeMergeFodder(CrewMemberData target, CrewMemberData candidate)
        => target != null && candidate != null
           && candidate.instanceId != target.instanceId
           && candidate.definitionId == target.definitionId
           && candidate.rank == target.rank
           && !IsCrewAssigned(candidate.instanceId);

    /// <summary>Every spare copy that could be fed to `target`, lowest level first (the ones that cost the
    /// least to lose).</summary>
    public List<CrewMemberData> GetMergeCandidates(CrewMemberData target)
        => Current.crewMembers.Where(m => CanBeMergeFodder(target, m)).OrderBy(m => m.level).ToList();

    /// <summary>Whether the rank-up button should be usable: the member stands in a slot, isn't at the top
    /// rank, and at least two spare copies exist.</summary>
    public bool CanMergeCrew(CrewMemberData member)
        => CanRankUpCrew(member) && IsCrewAssigned(member.instanceId) && GetMergeCandidates(member).Count >= 2;

    /// <summary>Credits it costs to rank this member up (from Crew Merge Costs, by its current rank). The
    /// last entry is reused if the list is shorter than the number of ranks.</summary>
    public int GetCrewMergeCost(CrewMemberData member)
    {
        if (member == null || crewMergeCosts == null || crewMergeCosts.Length == 0) return 0;
        return Mathf.Max(0, crewMergeCosts[Mathf.Clamp(member.rank - 1, 0, crewMergeCosts.Length - 1)]);
    }

    public bool CanAffordCrewMerge(CrewMemberData member) => member != null && Current.credits >= GetCrewMergeCost(member);

    /// <summary>
    /// "Crossing": consumes the two fodder copies and raises the target one rank — its level resets to 1
    /// (the levels bought so far are lost), the next perk unlocks, and its portrait swaps to the new rank's.
    /// The target STAYS in its slot. False, changing nothing, if the target isn't in a slot, is already max
    /// rank, either fodder doesn't match, or the credits (GetCrewMergeCost) are missing — in that last case
    /// OnInsufficientCredits fires. Spends the credits on success.
    /// </summary>
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

    // ----- Missile target priority (player) -----

    /// <summary>The saved missile target order (ModuleType values), or null if never changed.</summary>
    public IReadOnlyList<int> SavedMissilePriority
        => Current.missileTargetPriority != null && Current.missileTargetPriority.Count > 0 ? Current.missileTargetPriority : null;

    /// <summary>Remembers the player's missile target order and saves it right away.</summary>
    public void SaveMissilePriority(IEnumerable<ModuleType> order)
    {
        Current.missileTargetPriority = order.Select(t => (int)t).ToList();
        Save();
    }

    // ----- Tower-gun target priority (player) -----

    /// <summary>The saved tower-gun target order (ModuleType values), or null if never changed.</summary>
    public IReadOnlyList<int> SavedTowerPriority
        => Current.towerTargetPriority != null && Current.towerTargetPriority.Count > 0 ? Current.towerTargetPriority : null;

    /// <summary>Remembers the player's tower-gun target order and saves it right away.</summary>
    public void SaveTowerPriority(IEnumerable<ModuleType> order)
    {
        Current.towerTargetPriority = order.Select(t => (int)t).ToList();
        Save();
    }

    // ----- Perks -----

    /// <summary>The coefficient (1 = no change) for one kind of ordinary perk, from every member standing
    /// in a slot whose unlocked perks include it — bonuses from different members add together. For a
    /// CHANCE perk use GetCrewChance instead.</summary>
    public float GetCrewPerkMultiplier(CrewPerkType type)
    {
        int index = (int)type;
        return index > 0 && index < perkBonus.Length ? 1f + perkBonus[index] : 1f;
    }

    /// <summary>The probability (0..Crew Chance Cap) of a CHANCE perk, summed over the crew in the slots.</summary>
    public float GetCrewChance(CrewPerkType type)
    {
        int index = (int)type;
        float chance = index > 0 && index < perkBonus.Length ? perkBonus[index] : 0f;
        return Mathf.Clamp(chance, 0f, crewChanceCap);
    }

    /// <summary>Rolls a CHANCE perk once: true with the perk's probability.</summary>
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

            // The Nth perk in the list belongs to rank N: a rank-2 member has the first two.
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

    /// <summary>Hands out CrewDatabase.starterCrew once per save. Skipped (and retried next launch) while
    /// no database is assigned, so forgetting to assign it never burns the one-time grant.</summary>
    private void GrantStarterCrewIfNeeded()
    {
        if (Current.crewStarterGranted || crewDatabase == null) return;

        Current.crewStarterGranted = true;
        foreach (var definition in crewDatabase.starterCrew)
            if (definition != null) Current.crewMembers.Add(NewCrewMember(definition));

        Save();
    }

    // ---------- Resources (generic key/value, e.g. "scrap", "alloy", "energy_cores") ----------

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

    /// <summary>Returns false (and spends nothing) if there isn't enough of that resource.</summary>
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
