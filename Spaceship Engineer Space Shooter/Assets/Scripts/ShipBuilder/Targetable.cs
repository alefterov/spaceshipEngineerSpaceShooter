using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a Targetable is, from a weapon's point of view — each WeaponModule keeps its own list of
/// kinds it may engage (WeaponModule.allowedTargets), so e.g. enemy missiles can be limited to point-defence
/// turrets while torpedoes are open to cannons and launchers too. Projectile exists so "shots shooting
/// down other shots" is something a weapon has to be explicitly configured for, never implied by an
/// absent component. Values are explicit so serialized lists survive new kinds being added.</summary>
public enum TargetKind { Ship = 0, Missile = 1, Meteor = 2, Projectile = 3, Torpedo = 4 }

/// <summary>
/// Marks anything a weapon is allowed to shoot at, and carries the HP for it. Self-registers into a
/// scene-wide list while enabled, so weapons can scan candidates without a per-frame
/// FindObjectsOfType. Also exposes Velocity, which turrets use to lead their shots.
///
/// Attach to: enemy/player ship roots (kind = Ship), missiles, torpedoes, meteors. Ships take real damage
/// per-module through ShipModule instead, so a ship's Targetable is only an aiming marker — leave
/// its maxHP alone, nothing shoots the root collider.
/// </summary>
public class Targetable : MonoBehaviour
{
    public TargetKind kind = TargetKind.Ship;
    [Tooltip("Whose side this belongs to. Ignored for Meteor, which is hostile to everyone.")]
    public Faction faction = Faction.Enemy;

    [Header("HP (missiles/torpedoes/meteors — ships use per-module ShipModule HP instead)")]
    public float maxHP = 1f;

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    /// <summary>World-space velocity, for aiming ahead of a moving target. Read straight from a
    /// Rigidbody2D when there is one (exact); otherwise measured from how far the transform moved last
    /// frame (ships are moved by setting transform.position directly).</summary>
    public Vector2 Velocity => body != null && body.bodyType != RigidbodyType2D.Static ? body.linearVelocity : measuredVelocity;

    /// <summary>Fired once when HP reaches zero — hook explosion VFX, score, or a missile's own
    /// cleanup here. The GameObject is NOT destroyed automatically.</summary>
    public event Action<Targetable> OnDestroyed;

    private static readonly List<Targetable> active = new();

    /// <summary>Every Targetable currently alive in the scene — iterated by weapons looking for a target.</summary>
    public static IReadOnlyList<Targetable> Active => active;

    private Rigidbody2D body;
    private Vector2 lastPosition;
    private Vector2 measuredVelocity;

    private void Awake()
    {
        CurrentHP = maxHP;
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        active.Add(this);
        lastPosition = transform.position;
        measuredVelocity = Vector2.zero;
    }

    private void OnDisable() => active.Remove(this);

    private void LateUpdate()
    {
        if (body != null) return; // exact velocity already available from the Rigidbody2D

        float dt = Time.deltaTime;
        Vector2 position = transform.position;
        if (dt > 0f) measuredVelocity = (position - lastPosition) / dt;
        lastPosition = position;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (CurrentHP > 0f) return;

        IsDestroyed = true;
        OnDestroyed?.Invoke(this);
    }
}
