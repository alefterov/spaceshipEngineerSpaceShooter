using System;
using System.Collections.Generic;
using UnityEngine;

public enum Faction { Player, Enemy }

/// <summary>
/// Marks a ship root as Player or Enemy, tags its modules accordingly (used by Projectile
/// for hit filtering), and tracks whether the ship as a whole is destroyed —
/// either its core hull piece dies, or every hull piece is gone.
/// Same component drives both the player ship and every enemy ship.
/// </summary>
public class ShipIdentity : MonoBehaviour
{
    public Faction faction = Faction.Player;

    public event Action<ShipIdentity> OnShipDestroyed;
    /// <summary>Fired once when the ship's last Cockpit or last power-generating module (Generator) is
    /// destroyed — see ShipGrid's module-destroyed handling, which is what actually notices this and
    /// calls NotifyDisabled. The ship can still visually exist with most of its hull intact, but
    /// battle-outcome logic (BattleOutcomeController) should treat it the same as fully destroyed.</summary>
    public event Action<ShipIdentity> OnShipDisabled;

    private readonly List<ShipModule> hullPieces = new();
    private bool destroyed;
    public bool IsDisabled { get; private set; }

    /// <summary>Whether this ship is in battle. Weapons only fire while this is true, which is what
    /// keeps them silent in the main menu preview and the ship builder.</summary>
    public bool CombatActive { get; private set; }

    private void Awake() => ApplyTagToRoot();

    public void ApplyTagToRoot()
        => gameObject.tag = faction == Faction.Player ? "PlayerShip" : "EnemyShip";

    /// <summary>Call once when a battle starts (and again with false when it ends) — arms or disarms
    /// every weapon on this ship. Weapons built or loaded afterwards read CombatActive themselves in
    /// Awake, so the state holds in both directions.</summary>
    public void SetCombatActive(bool active)
    {
        CombatActive = active;
        foreach (var weapon in GetComponentsInChildren<WeaponModule>(true))
            weapon.SetCombatActive(active);
    }

    public void RegisterHull(ShipModule hull)
    {
        hullPieces.Add(hull);
        hull.OnDestroyed += HandleHullPieceDestroyed;
    }

    private void HandleHullPieceDestroyed(ShipModule hull)
    {
        hullPieces.Remove(hull);

        if (destroyed) return;

        bool coreLost = hull.isCore;
        bool allHullGone = hullPieces.TrueForAll(h => h.IsDestroyed) && hullPieces.Count == 0;

        if (coreLost || allHullGone)
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
