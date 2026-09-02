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
