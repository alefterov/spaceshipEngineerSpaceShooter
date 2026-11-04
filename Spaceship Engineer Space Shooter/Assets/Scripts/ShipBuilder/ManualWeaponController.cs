using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ManualWeaponGroup
{
    public string WeaponId { get; }

    public readonly List<WeaponModule> weapons = new();

    public ManualWeaponGroup(string weaponId) => WeaponId = weaponId;

    public int Count => weapons.Count;

    public float CooldownSeconds => weapons.Count > 0 ? weapons[0].cooldownSeconds : 0f;

    public float CooldownRemaining01 => weapons.Count == 0 ? 0f : weapons.Max(w => w.CooldownRemaining01);

    public bool IsReady => weapons.Any(w => w.IsReadyToFire);

    public void Fire()
    {
        foreach (var weapon in weapons)
            weapon.TryFire();
    }
}

[RequireComponent(typeof(ShipGrid))]
public class ManualWeaponController : MonoBehaviour
{
    private readonly List<ManualWeaponGroup> groups = new();
    private readonly List<WeaponModule> watched = new();

    public IReadOnlyList<ManualWeaponGroup> Groups => groups;

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
