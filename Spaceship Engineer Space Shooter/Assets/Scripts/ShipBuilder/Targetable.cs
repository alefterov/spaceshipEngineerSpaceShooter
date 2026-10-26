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

    /// <summary>World-space velocity, for aiming ahead of a moving target. Works for every kind of target:
    /// a Rigidbody2D that is really moving under physics (meteors, missiles) reports its own exact velocity;
    /// anything else — ships, whose position is set directly through the transform, and which may even carry a
    /// kinematic Rigidbody2D that always reports zero — gets the velocity measured from how far it moved,
    /// lightly smoothed so the aim doesn't twitch.</summary>
    public Vector2 Velocity
    {
        get
        {
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) return body.linearVelocity;
            if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f) return body.linearVelocity;
            return measuredVelocity;
        }
    }

    /// <summary>False for a ship whose every block is gone (an empty hull is nothing to shoot at). Anything
    /// that is not a ship is always shootable. A ship with blocks left — even a disabled wreck — stays a target.</summary>
    public bool HasLiveBlocks => shipIdentity == null || shipIdentity.LiveBlockCount > 0;

    /// <summary>False for a ship that is not completely on the screen (see ShipIdentity.IsFullyInView): it cannot be hurt
    /// yet, so nothing aims at it. Everything that is not a ship is always true.</summary>
    public bool IsFullyInView => shipIdentity == null || shipIdentity.IsFullyInView();

    /// <summary>Where a weapon at `from` should aim. For a ship: its NEAREST living block, so a damaged or
    /// disabled wreck is shot block by block instead of through a hole in its middle. Anything else, and a ship
    /// with no block left: AimPoint.</summary>
    public Vector2 AimPointFrom(Vector2 from)
        => shipIdentity != null && shipIdentity.TryGetNearestBlockPosition(from, out var block) ? block : AimPoint;

    /// <summary>The middle of the whole ship for a ship (its root point can sit
    /// off to one side), the object's position for everything else.</summary>
    public Vector2 AimPoint => shipGrid != null ? (Vector2)shipGrid.GetShipWorldCenter() : (Vector2)transform.position;

    /// <summary>Fired once when HP reaches zero — hook explosion VFX, score, or a missile's own
    /// cleanup here. The GameObject is NOT destroyed automatically.</summary>
    public event Action<Targetable> OnDestroyed;

    private static readonly List<Targetable> active = new();

    /// <summary>Every Targetable currently alive in the scene — iterated by weapons looking for a target.</summary>
    public static IReadOnlyList<Targetable> Active => active;

    private Rigidbody2D body;
    private ShipGrid shipGrid;
    private ShipIdentity shipIdentity;
    private Vector2 lastPosition;
    private Vector2 measuredVelocity;

    private void Awake()
    {
        CurrentHP = maxHP;
        body = GetComponent<Rigidbody2D>();
        shipGrid = GetComponent<ShipGrid>();
        shipIdentity = GetComponent<ShipIdentity>();
    }

    private void OnEnable()
    {
        active.Add(this);
        lastPosition = transform.position;
        measuredVelocity = Vector2.zero;
    }

    private void OnDisable() => active.Remove(this);

    // Measured for every target, bodied or not (see Velocity for which one is used).
    private const float MaxPlausibleSpeed = 200f; // a bigger jump in one frame is a teleport (spawn, arrival), not movement
    private const float VelocitySmoothing = 20f;  // per second — about 50 ms to follow a change

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector2 position = transform.position;

        if (dt > 0f)
        {
            Vector2 raw = (position - lastPosition) / dt;
            if (raw.magnitude <= MaxPlausibleSpeed)
                measuredVelocity = Vector2.Lerp(measuredVelocity, raw, 1f - Mathf.Exp(-VelocitySmoothing * dt));
        }

        lastPosition = position;
    }

    /// <summary>Sets kind, side and HP in one go, for objects that create their Targetable in code (a missile).
    /// HP is refilled.</summary>
    public void Configure(TargetKind newKind, Faction newFaction, float hp)
    {
        kind = newKind;
        faction = newFaction;
        maxHP = hp;
        CurrentHP = hp;
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
