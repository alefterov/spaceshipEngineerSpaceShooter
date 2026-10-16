using System;
using System.Collections.Generic;
using UnityEngine;

public enum Faction { Player, Enemy }

/// <summary>
/// Marks a ship root as Player or Enemy, tags its blocks accordingly (used by Projectile
/// for hit filtering), and tracks whether the ship as a whole is destroyed —
/// either a block flagged isCore dies, or every block is gone.
/// Same component drives both the player ship and every enemy ship.
///
/// Also ensures the root itself carries a Targetable (kind=Ship, faction synced to this component's own)
/// — WITHOUT one, this ship is simply invisible to WeaponModule.FindTarget (which only ever scans
/// Targetable.Active), so neither side could ever open fire on it. Added/kept in sync in ApplyTagToRoot,
/// the same moment faction is finalized, rather than here in Awake — a builder-assembled ship (ShipGrid.
/// BuildFromLayout/BuildFromDefinitions) sets faction AFTER this component's own Awake has already run,
/// and Unity doesn't guarantee sibling Awake order besides.
/// </summary>
public class ShipIdentity : MonoBehaviour
{
    public Faction faction = Faction.Player;

    public event Action<ShipIdentity> OnShipDestroyed;
    /// <summary>Fired once when the ship's last Cockpit or last power-generating block (Generator) is
    /// destroyed — see ShipGrid's block-destroyed handling, which is what actually notices this and
    /// calls NotifyDisabled. The ship can still visually exist with most of its blocks intact, but
    /// battle-outcome logic (BattleOutcomeController) should treat it the same as fully destroyed.</summary>
    public event Action<ShipIdentity> OnShipDisabled;

    private readonly List<ShipModule> blocks = new();
    private bool destroyed;
    public bool IsDisabled { get; private set; }

    /// <summary>Whether this ship is in battle. Weapons only fire while this is true, which is what
    /// keeps them silent in the main menu preview and the ship builder.</summary>
    public bool CombatActive { get; private set; }

    private static readonly List<ShipIdentity> all = new();

    /// <summary>How many of this ship's blocks are still alive (destroyed ones drop out of the list).</summary>
    public int LiveBlockCount => blocks.Count;

    /// <summary>This ship's living blocks (destroyed ones drop out of the list).</summary>
    public IReadOnlyList<ShipModule> LiveBlocks => blocks;

    /// <summary>Every ship currently in the scene.</summary>
    public static IReadOnlyList<ShipIdentity> All => all;

    /// <summary>True while any enemy ship in the scene still has at least one living block — wrecks included.
    /// A ship that lost its cockpit or generators is only "disabled": its remaining blocks still count.</summary>
    public static bool AnyEnemyBlocksAlive
    {
        get
        {
            foreach (var ship in all)
                if (ship != null && ship.faction == Faction.Enemy && ship.blocks.Count > 0) return true;
            return false;
        }
    }

    /// <summary>Position of the living block closest to `from`, or false if no block is left. Weapons aim at
    /// this rather than at the ship's middle: a damaged ship has holes, and the blocks that remain may be off to
    /// one side.</summary>
    public bool TryGetNearestBlockPosition(Vector2 from, out Vector2 position)
    {
        position = default;
        float bestSqr = float.MaxValue;
        bool found = false;

        foreach (var block in blocks)
        {
            if (block == null || block.IsDestroyed) continue;

            Vector2 p = block.transform.position;
            float sqr = (p - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            position = p;
            found = true;
        }

        return found;
    }

    private void Awake() => ApplyTagToRoot();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public void ApplyTagToRoot()
    {
        gameObject.tag = faction == Faction.Player ? "PlayerShip" : "EnemyShip";

        if (!TryGetComponent<Targetable>(out var targetable)) targetable = gameObject.AddComponent<Targetable>();
        targetable.kind = TargetKind.Ship;
        targetable.faction = faction;
    }

    /// <summary>Call once when a battle starts (and again with false when it ends) — arms or disarms
    /// every weapon on this ship. Weapons built or loaded afterwards read CombatActive themselves in
    /// Awake, so the state holds in both directions.</summary>
    public void SetCombatActive(bool active)
    {
        CombatActive = active;
        foreach (var weapon in GetComponentsInChildren<WeaponModule>(true))
            weapon.SetCombatActive(active);
    }

    /// <summary>Called by ShipGrid for every block it places.</summary>
    public void RegisterBlock(ShipModule block)
    {
        blocks.Add(block);
        block.OnDestroyed += HandleBlockDestroyed;
    }

    /// <summary>Called by ShipGrid when a block is removed WITHOUT being destroyed in combat (editor
    /// deletion, or the whole grid being cleared) — so it never counts toward "ship destroyed".</summary>
    public void UnregisterBlock(ShipModule block)
    {
        blocks.Remove(block);
        block.OnDestroyed -= HandleBlockDestroyed;
    }

    private void HandleBlockDestroyed(ShipModule block)
    {
        blocks.Remove(block);

        if (destroyed) return;

        if (block.isCore || blocks.Count == 0)
        {
            destroyed = true;
            OnShipDestroyed?.Invoke(this);
        }
    }

    /// <summary>Called by ShipGrid right after it notices the last Cockpit or last Generator on this
    /// ship is gone. Not called directly by anything else — ShipGrid owns the module bookkeeping needed
    /// to know whether one REALLY remains.</summary>
    public void NotifyDisabled()
    {
        if (IsDisabled || destroyed) return;
        IsDisabled = true;
        OnShipDisabled?.Invoke(this);
    }
}
