using System;
using System.Collections.Generic;
using UnityEngine;

public enum TargetKind { Ship = 0, Missile = 1, Meteor = 2, Projectile = 3, Torpedo = 4 }

public class Targetable : MonoBehaviour
{
    public TargetKind kind = TargetKind.Ship;
    [Tooltip("Whose side this belongs to. Ignored for Meteor, which is hostile to everyone.")]
    public Faction faction = Faction.Enemy;

    [Header("HP (missiles/torpedoes/meteors — ships use per-module ShipModule HP instead)")]
    public float maxHP = 1f;

    public float CurrentHP { get; private set; }
    public bool IsDestroyed { get; private set; }

    public Vector2 Velocity
    {
        get
        {
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic) return body.linearVelocity;
            if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f) return body.linearVelocity;
            return measuredVelocity;
        }
    }

    public bool HasLiveBlocks => shipIdentity == null || shipIdentity.LiveBlockCount > 0;

    public bool HasBlockInZone => shipIdentity == null || shipIdentity.AnyBlockInZone();

    public Vector2 AimPointFrom(Vector2 from)
        => shipIdentity != null && shipIdentity.TryGetNearestBlockPosition(from, out var block) ? block : AimPoint;

    public Vector2 AimPoint => shipGrid != null ? (Vector2)shipGrid.GetShipWorldCenter() : (Vector2)transform.position;

    public event Action<Targetable> OnDestroyed;

    private static readonly List<Targetable> active = new();

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

    private const float MaxPlausibleSpeed = 200f;
    private const float VelocitySmoothing = 20f;

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
