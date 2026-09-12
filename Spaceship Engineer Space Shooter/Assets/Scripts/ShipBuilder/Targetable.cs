using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a Targetable is, from a weapon's point of view — see WeaponModule.CanEngage for the
/// per-role rules. Projectile exists so the "defensive turrets may NOT intercept enemy shots" rule is
/// written out explicitly rather than being implied by an absent component.</summary>
public enum TargetKind { Ship, Missile, Meteor, Projectile }

/// <summary>
/// Marks anything a weapon is allowed to shoot at, and carries the HP for it. Self-registers into a
/// scene-wide list while enabled, so weapons can scan candidates without a per-frame
/// FindObjectsOfType.
///
/// Attach to: enemy/player ship roots (kind = Ship), missiles, meteors. Ships take real damage
/// per-module through ShipModule instead, so a ship's Targetable is only an aiming marker — leave
/// its maxHP alone, nothing shoots the root collider.
/// </summary>
public class Targetable : MonoBehaviour
{
    public TargetKind kind = TargetKind.Ship;
    [Tooltip("Whose side this belongs to. Ignored for Meteor, which is hostile to everyone.")]
    public Faction faction = Faction.Enemy;

    [Header("HP (missiles/meteors — ships use per-module ShipModule HP instead)")]
    public float maxHP = 1f;

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    /// <summary>Fired once when HP reaches zero — hook explosion VFX, score, or a missile's own
    /// cleanup here. The GameObject is NOT destroyed automatically.</summary>
    public event Action<Targetable> OnDestroyed;

    private static readonly List<Targetable> active = new();

    /// <summary>Every Targetable currently alive in the scene — iterated by weapons looking for a target.</summary>
    public static IReadOnlyList<Targetable> Active => active;

    private void Awake() => CurrentHP = maxHP;
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (CurrentHP > 0f) return;

        IsDestroyed = true;
        OnDestroyed?.Invoke(this);
    }
}
