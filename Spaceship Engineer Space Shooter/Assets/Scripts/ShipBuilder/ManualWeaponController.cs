using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Every manually-fired weapon of ONE type on the ship, driven by a single button. Two or more
/// identical launchers share one trigger and fire together, which also keeps their (identical)
/// cooldowns in lockstep.
/// </summary>
public class ManualWeaponGroup
{
    /// <summary>BlockDefinition id shared by every weapon in this group — ShipGrid.Place always
    /// stamps it from the definition, so it's a reliable "same weapon type" key.</summary>
    public string WeaponId { get; }

    public readonly List<WeaponModule> weapons = new();

    public ManualWeaponGroup(string weaponId) => WeaponId = weaponId;

    public int Count => weapons.Count;

    /// <summary>Seconds between shots for this weapon type — identical across the group.</summary>
    public float CooldownSeconds => weapons.Count > 0 ? weapons[0].cooldownSeconds : 0f;

    /// <summary>1 right after firing, easing to 0 as the group becomes ready — drives the button's
    /// radial cooldown ring. Takes the longest remaining cooldown in the group, so the ring can never
    /// read "ready" while some member still isn't.</summary>
    public float CooldownRemaining01 => weapons.Count == 0 ? 0f : weapons.Max(w => w.CooldownRemaining01);

    public bool IsReady => weapons.Any(w => w.IsReadyToFire);

    /// <summary>Fires every weapon in the group. Manual weapons shoot straight along their muzzle
    /// rather than needing a locked target — pressing the button means "shoot now".</summary>
    public void Fire()
    {
        foreach (var weapon in weapons)
            weapon.TryFire();
    }
}

/// <summary>
/// Collects the ship's manually-fired weapons into one group per weapon type and keeps that list in
/// sync as blocks are built, removed, or shot off. ManualWeaponBar turns each group into a button.
///
/// SETUP: put this on the player's ship root, next to ShipGrid / ShipIdentity.
/// </summary>
[RequireComponent(typeof(ShipGrid))]
public class ManualWeaponController : MonoBehaviour
{
    private readonly List<ManualWeaponGroup> groups = new();
    private readonly List<WeaponModule> watched = new();

    /// <summary>One entry per manually-fired weapon type currently on the ship, in build order.</summary>
    public IReadOnlyList<ManualWeaponGroup> Groups => groups;

    /// <summary>Fired whenever the set of groups changes — a new weapon type was built, or the last
    /// one of a type was removed/destroyed. ManualWeaponBar rebuilds its buttons on this.</summary>
    public event Action OnGroupsChanged;

    private ShipGrid grid;

    private void Awake() => grid = GetComponent<ShipGrid>();

    private void OnEnable()
    {
        grid.OnShipChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        grid.OnShipChanged -= Rebuild;
        UnwatchAll();
    }

    private void Rebuild()
    {
        UnwatchAll();
        groups.Clear();

        foreach (var weapon in GetComponentsInChildren<WeaponModule>())
        {
            if (weapon.IsDestroyed || weapon.fireControl != WeaponFireControl.Manual) continue;

            var group = groups.FirstOrDefault(g => g.WeaponId == weapon.moduleId);
            if (group == null)
            {
                group = new ManualWeaponGroup(weapon.moduleId);
                groups.Add(group);
            }

            group.weapons.Add(weapon);

            // A weapon shot off mid-battle has to leave its group — ShipGrid.OnShipChanged doesn't
            // cover combat destruction, only editor placement/removal.
            weapon.OnDestroyed += HandleWeaponDestroyed;
            watched.Add(weapon);
        }

        OnGroupsChanged?.Invoke();
    }

    private void HandleWeaponDestroyed(ShipModule module) => Rebuild();

    private void UnwatchAll()
    {
        foreach (var weapon in watched)
            if (weapon != null) weapon.OnDestroyed -= HandleWeaponDestroyed;
        watched.Clear();
    }
}
