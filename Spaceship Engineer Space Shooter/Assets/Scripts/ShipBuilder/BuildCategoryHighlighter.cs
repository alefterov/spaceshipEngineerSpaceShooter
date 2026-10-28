using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Highlights the active category buttons of the ship builder the same way the ship-settings window of the battle does: the icon of
/// the active button is fully opaque, the others are dimmed, and the change fades smoothly. Three groups, each on its own:
///  - Tabs (BuilderLeftPanel): Cockpit / Generators / Engines / Repair / Armor / Weapons — the active tab.
///  - Armor sub-tabs (ArmorButtonsRow): Physical armor / Shields — the one showing.
///  - Weapon sub-tabs (WeaponButtonsRow): one per weapon family — the one showing; while the Weapons tab lists EVERY weapon (right
///    after opening it), all of them count as active.
///
/// Unlike the battle window it switches nothing itself: the buttons keep calling BuildModeController as before, and this component
/// only follows BuildModeController's state (CurrentMode, ArmorSubTab, WeaponFilter), so the highlight is right however the tab changed — a button, a sub-tab
/// that opened its parent tab, or the builder opening on Cockpit.
///
/// SETUP: put on any object of the main scene (the builder screen, say), assign Build Mode Controller and fill the three lists: each
/// entry = the button's icon Image (the child Image inside the button, not the button's own background) and what it stands for.
/// </summary>
public class BuildCategoryHighlighter : MonoBehaviour
{
    [Serializable]
    public class TabEntry
    {
        public BuildMode mode;
        [Tooltip("The icon inside the button.")]
        public Image icon;
        [NonSerialized] public float alpha = -1f;
    }

    [Serializable]
    public class ArmorEntry
    {
        [Tooltip("Armor = the physical-armor button, Shield = the shields button.")]
        public BlockCategory category = BlockCategory.Armor;
        public Image icon;
        [NonSerialized] public float alpha = -1f;
    }

    [Serializable]
    public class WeaponEntry
    {
        public WeaponClass weaponClass;
        public Image icon;
        [NonSerialized] public float alpha = -1f;
    }

    public BuildModeController buildModeController;

    [Header("Buttons")]
    [Tooltip("BuilderLeftPanel: one entry per block-kind tab.")]
    public List<TabEntry> tabs = new();
    [Tooltip("ArmorButtonsRow: physical armor and shields.")]
    public List<ArmorEntry> armorSubTabs = new();
    [Tooltip("WeaponButtonsRow: one entry per weapon family.")]
    public List<WeaponEntry> weaponSubTabs = new();

    [Header("Look")]
    [Range(0f, 1f)] public float activeIconAlpha = 1f;
    [Range(0f, 1f)] public float inactiveIconAlpha = 0.5f;
    [Tooltip("Seconds for an icon to change between active and dimmed.")]
    [Min(0.01f)] public float fadeSeconds = 0.15f;

    private bool snapNext;

    // Opening the builder: the icons show the right state at once instead of fading from the last one.
    private void OnEnable() => snapNext = true;

    // LateUpdate, so a tab pressed this very frame (the button handlers run in the EventSystem's Update) is already counted.
    private void LateUpdate()
    {
        if (buildModeController == null) return;

        var mode = buildModeController.CurrentMode;
        var armor = buildModeController.ArmorSubTab;
        var weapon = buildModeController.WeaponFilter;

        float step = Mathf.Abs(activeIconAlpha - inactiveIconAlpha) * Time.unscaledDeltaTime / fadeSeconds;

        foreach (var entry in tabs)
            entry.alpha = Fade(entry.icon, entry.alpha, entry.mode == mode, step);

        foreach (var entry in armorSubTabs)
            entry.alpha = Fade(entry.icon, entry.alpha, entry.category == armor, step);

        foreach (var entry in weaponSubTabs)
            entry.alpha = Fade(entry.icon, entry.alpha, weapon == null || entry.weaponClass == weapon.Value, step);

        snapNext = false;
    }

    private float Fade(Image icon, float current, bool active, float step)
    {
        float target = active ? activeIconAlpha : inactiveIconAlpha;
        float next = current < 0f || snapNext ? target : Mathf.MoveTowards(current, target, step); // < 0 = never shown yet

        if (icon != null)
        {
            Color color = icon.color;
            color.a = next;
            icon.color = color;
        }

        return next;
    }
}
