using System;
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

    // Credits as they stood at the last save (or at BeginBuildSession, if nothing's been saved
    // since) — what RevertCredits() rolls back to on an unsaved exit from the builder.
    private int creditsAtSessionStart;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadOrCreate();
    }

    // ---------- Load / Save ----------

    /// <summary>Loads the save file, or creates a fresh empty GameData if none exists (first launch).</summary>
    public void LoadOrCreate()
    {
        Current = SaveSystem.Load() ?? new GameData();
        OnDataLoaded?.Invoke();
    }

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
        if (Current.playerShip == null || Current.playerShip.hull.Count == 0) return;
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

    public bool HasSavedShip => Current.playerShip != null && Current.playerShip.hull.Count > 0;

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
    // Each level permanently adds one row and one column to the ship's buildable grid (see
    // ShipGrid.SetHangarLevel). Paid with research points, same currency and immediate-persistence
    // semantics as researching a technology — not part of the revertible build-session state.

    [Header("Hangar upgrade cost")]
    [Tooltip("Research-point cost of the first hangar upgrade (level 0 -> 1).")]
    public int hangarUpgradeBaseCost = 200;
    [Tooltip("Extra research-point cost added per hangar level already owned — makes each successive upgrade pricier.")]
    public int hangarUpgradeCostPerLevel = 100;

    public int HangarLevel => Current.hangarLevel;

    public int GetHangarUpgradeCost() => hangarUpgradeBaseCost + Current.hangarLevel * hangarUpgradeCostPerLevel;

    public bool CanUpgradeHangar() => Current.researchPoints >= GetHangarUpgradeCost();

    /// <summary>Spends research points to add one row and one column to the given grid's buildable
    /// area. Returns false (and changes nothing) if there aren't enough points, firing
    /// OnInsufficientResearchPoints the same way TryResearch does.</summary>
    public bool TryUpgradeHangar(ShipGrid grid)
    {
        int cost = GetHangarUpgradeCost();
        if (Current.researchPoints < cost)
        {
            OnInsufficientResearchPoints?.Invoke();
            return false;
        }

        Current.researchPoints -= cost;
        Current.hangarLevel++;
        if (grid != null) grid.SetHangarLevel(Current.hangarLevel);
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
