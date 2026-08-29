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

    private readonly List<ShipModule> hullPieces = new();
    private bool destroyed;

    private void Awake() => ApplyTagToRoot();

    public void ApplyTagToRoot()
        => gameObject.tag = faction == Faction.Player ? "PlayerShip" : "EnemyShip";

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
}
