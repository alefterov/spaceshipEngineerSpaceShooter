using System;
using System.Collections.Generic;
using UnityEngine;

public enum Faction { Player, Enemy }

public class ShipIdentity : MonoBehaviour
{
    public Faction faction = Faction.Player;

    public event Action<ShipIdentity> OnShipDestroyed;
    public event Action<ShipIdentity> OnShipDisabled;

    private readonly List<ShipModule> blocks = new();
    private bool destroyed;
    public bool IsDisabled { get; private set; }

    public bool CombatActive { get; private set; }

    private static readonly List<ShipIdentity> all = new();

    public int LiveBlockCount => blocks.Count;

    public IReadOnlyList<ShipModule> LiveBlocks => blocks;

    public static IReadOnlyList<ShipIdentity> All => all;

    private int viewCheckFrame = -1;
    private bool viewCheckResult;

    public bool IsFullyInView(Camera cam = null)
    {
        if (viewCheckFrame == Time.frameCount) return viewCheckResult;
        viewCheckFrame = Time.frameCount;

        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic || blocks.Count == 0) return viewCheckResult = true;

        bool any = false;
        Bounds total = default;
        foreach (var block in blocks)
        {
            if (block == null || block.IsDestroyed) continue;

            Bounds b = block.WorldBounds;
            if (!any) { total = b; any = true; }
            else total.Encapsulate(b);
        }

        if (!any) return viewCheckResult = true;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 c = cam.transform.position;
        const float tolerance = 0.02f;

        bool xOk = total.size.x > halfWidth * 2f || (total.min.x >= c.x - halfWidth - tolerance && total.max.x <= c.x + halfWidth + tolerance);
        bool yOk = total.size.y > halfHeight * 2f || (total.min.y >= c.y - halfHeight - tolerance && total.max.y <= c.y + halfHeight + tolerance);
        return viewCheckResult = xOk && yOk;
    }

    private int zoneCheckFrame = -1;
    private bool zoneCheckResult;

    public bool AnyBlockInZone()
    {
        if (zoneCheckFrame == Time.frameCount) return zoneCheckResult;
        zoneCheckFrame = Time.frameCount;

        zoneCheckResult = false;
        foreach (var block in blocks)
        {
            if (block == null || block.IsDestroyed || !BattleZone.BlockInZone(block.WorldBounds)) continue;
            zoneCheckResult = true;
            break;
        }

        return zoneCheckResult;
    }

    public static bool AnyEnemyShipOperational
    {
        get
        {
            foreach (var ship in all)
            {
                if (ship == null || ship.faction != Faction.Enemy) continue;

                bool cockpit = false, generator = false;
                foreach (var block in ship.blocks)
                {
                    if (block == null || block.IsDestroyed) continue;
                    if (block.type == ModuleType.Cockpit) cockpit = true;
                    else if (block.type == ModuleType.Generator) generator = true;
                }

                if (cockpit && generator) return true;
            }

            return false;
        }
    }

    public static bool AnyEnemyBlocksAlive
    {
        get
        {
            foreach (var ship in all)
                if (ship != null && ship.faction == Faction.Enemy && ship.blocks.Count > 0) return true;
            return false;
        }
    }

    public bool TryGetNearestBlockPosition(Vector2 from, out Vector2 position)
    {
        position = default;
        float bestSqr = float.MaxValue;
        bool found = false;

        foreach (var block in blocks)
        {
            if (block == null || block.IsDestroyed) continue;
            if (faction == Faction.Enemy && !BattleZone.BlockInZone(block.WorldBounds)) continue;

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

    public void SetCombatActive(bool active)
    {
        CombatActive = active;
        foreach (var module in GetComponentsInChildren<ShipModule>(true))
            module.SetInCombat(active);
        foreach (var weapon in GetComponentsInChildren<WeaponModule>(true))
            weapon.SetCombatActive(active);
    }

    public void RegisterBlock(ShipModule block)
    {
        blocks.Add(block);
        block.OnDestroyed += HandleBlockDestroyed;
    }

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

    public void NotifyDisabled()
    {
        if (IsDisabled || destroyed) return;
        IsDisabled = true;
        OnShipDisabled?.Invoke(this);
    }
}
