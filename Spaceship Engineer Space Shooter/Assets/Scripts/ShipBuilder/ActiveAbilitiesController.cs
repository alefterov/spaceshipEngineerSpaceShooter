using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One button of the battle's active abilities and all its settings.</summary>
[Serializable]
public class AbilitySlot
{
    [Tooltip("What this button does. Rapid Fire: ballistic guns (fixed mounts, not turrets) fire Strength times faster. Missile / " +
             "Plasma Double Salvo: every launch is repeated once, Salvo Delay Seconds later. Long Laser: beams last Strength times longer.")]
    public WeaponAbility ability = WeaponAbility.RapidFire;
    public Button button;

    [Header("Numbers")]
    [Tooltip("How long the ability works once pressed, in seconds.")]
    [Min(0.1f)] public float durationSeconds = 8f;
    [Tooltip("Pause after the ability has ended before it can be pressed again, in seconds.")]
    [Min(0f)] public float cooldownSeconds = 25f;
    [Tooltip("Rapid Fire: the fire-rate multiplier (2 = twice as fast). Long Laser: the beam-length-in-time multiplier (2 = twice as long). Not used by the double salvos.")]
    [Min(1f)] public float strength = 2f;
    [Tooltip("Double salvos only: the pause between a shot and its repeat (0.5 = half a second).")]
    [Min(0f)] public float salvoDelaySeconds = 0.5f;

    [Header("Look (all optional)")]
    [Tooltip("A RadialGaugeView, the same component the HP gauge uses (set up its Fill Image yourself). During the cooldown the ring " +
             "fills up; once the ability is ready it is full.")]
    public RadialGaugeView gauge;
    [Tooltip("While the ability works the ring drains from full to empty over its duration, then fills up again during the cooldown. " +
             "Off: it stays empty while the ability works.")]
    public bool drainWhileActive = true;
    [Tooltip("Shows the seconds left, while it works and while it cools down.")]
    public TMP_Text timerLabel;
    [Tooltip("Switched on only while the ability works (a glow, a frame).")]
    public GameObject activeIndicator;
    [Tooltip("Hide the button when the player's ship has no weapon this ability works on. Off = it only becomes unpressable.")]
    public bool hideWithoutWeapons = true;

    [Header("Sound")]
    public AudioClip activateSound;
    [Range(0f, 1f)] public float activateVolume = 1f;

    // runtime
    [NonSerialized] public float activeEnd;
    [NonSerialized] public float cooldownEnd;
    [NonSerialized] public bool hasWeapons = true;

    public bool IsActive => Time.time < activeEnd;
    public bool IsCooling => !IsActive && Time.time < cooldownEnd;
    public bool IsReady => !IsActive && !IsCooling;
}

/// <summary>
/// The battle's active ability buttons: one for each kind of weapon (ballistic guns, missiles, plasma, lasers). Pressing a button
/// switches its ability on for Duration Seconds; the moment it ends its Cooldown Seconds starts, and only after that can it be used
/// again. Every ability has its own timers.
///
/// SETUP: put on any object of the BATTLE scene, add one entry to Slots per button (the four defaults appear when the component is
/// first added) and assign each entry's Button. Everything — duration, cooldown, strength, delay, the look of the timer — is set per
/// entry here. The buttons stand wherever you put them on the canvas. A button is pressable only while its ability is ready and the
/// player's ship still has a weapon of that kind.
/// </summary>
public class ActiveAbilitiesController : MonoBehaviour
{
    public List<AbilitySlot> slots = new();

    private float nextWeaponScan;

    private void Reset()
    {
        slots = new List<AbilitySlot>
        {
            new() { ability = WeaponAbility.RapidFire, durationSeconds = 8f, cooldownSeconds = 25f, strength = 2f },
            new() { ability = WeaponAbility.MissileDoubleSalvo, durationSeconds = 10f, cooldownSeconds = 30f, salvoDelaySeconds = 0.5f },
            new() { ability = WeaponAbility.PlasmaDoubleSalvo, durationSeconds = 10f, cooldownSeconds = 30f, salvoDelaySeconds = 0.5f },
            new() { ability = WeaponAbility.LongLaser, durationSeconds = 10f, cooldownSeconds = 30f, strength = 2f },
        };
    }

    private readonly List<UnityEngine.Events.UnityAction> handlers = new();

    private void OnEnable()
    {
        WeaponAbilities.ClearAll();
        handlers.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            int index = i;
            var slot = slots[i];
            slot.activeEnd = 0f;
            slot.cooldownEnd = 0f;

            UnityEngine.Events.UnityAction handler = () => Activate(index);
            handlers.Add(handler);
            if (slot.button != null) slot.button.onClick.AddListener(handler);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < slots.Count && i < handlers.Count; i++)
            if (slots[i].button != null) slots[i].button.onClick.RemoveListener(handlers[i]);

        handlers.Clear();
        WeaponAbilities.ClearAll();
    }

    /// <summary>Presses slot `index`, if it is ready (also callable from your own UI).</summary>
    public void Activate(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        var slot = slots[index];
        if (!slot.IsReady || !slot.hasWeapons) return;

        slot.activeEnd = Time.time + slot.durationSeconds;
        slot.cooldownEnd = slot.activeEnd + slot.cooldownSeconds; // the cooldown starts when the ability ends
        WeaponAbilities.Begin(slot.ability, slot.durationSeconds, slot.strength, slot.salvoDelaySeconds);
        AudioManager.PlaySfx(slot.activateSound, slot.activateVolume);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextWeaponScan)
        {
            nextWeaponScan = Time.unscaledTime + 0.25f;
            ScanPlayerWeapons();
        }

        foreach (var slot in slots)
        {
            if (slot.IsActive == false && WeaponAbilities.IsActive(slot.ability)) WeaponAbilities.End(slot.ability); // safety

            if (slot.button != null)
            {
                if (slot.hideWithoutWeapons && slot.button.gameObject.activeSelf != slot.hasWeapons)
                    slot.button.gameObject.SetActive(slot.hasWeapons);
                slot.button.interactable = slot.IsReady && slot.hasWeapons;
            }

            if (slot.activeIndicator != null && slot.activeIndicator.activeSelf != slot.IsActive)
                slot.activeIndicator.SetActive(slot.IsActive);

            float fraction = 1f, remaining = 0f;
            if (slot.IsActive)
            {
                remaining = slot.activeEnd - Time.time;
                fraction = remaining / Mathf.Max(0.01f, slot.durationSeconds);                    // draining
            }
            else if (slot.IsCooling)
            {
                remaining = slot.cooldownEnd - Time.time;
                fraction = 1f - remaining / Mathf.Max(0.01f, slot.cooldownSeconds);               // filling back up
            }

            if (slot.IsActive && !slot.drainWhileActive) fraction = 0f;
            if (slot.gauge != null) slot.gauge.SetValue(Mathf.Clamp01(fraction), 1f);
            if (slot.timerLabel != null) slot.timerLabel.text = remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : "";
        }
    }

    /// <summary>Which abilities have at least one living weapon of the player's ship to work on.</summary>
    private void ScanPlayerWeapons()
    {
        foreach (var slot in slots) slot.hasWeapons = false;

        foreach (var ship in ShipIdentity.All)
        {
            if (ship == null || ship.faction != Faction.Player) continue;

            foreach (var block in ship.LiveBlocks)
            {
                if (block is not WeaponModule weapon || weapon.IsDestroyed) continue;
                foreach (var slot in slots)
                    if (weapon.SupportsAbility(slot.ability)) slot.hasWeapons = true;
            }
        }
    }
}
