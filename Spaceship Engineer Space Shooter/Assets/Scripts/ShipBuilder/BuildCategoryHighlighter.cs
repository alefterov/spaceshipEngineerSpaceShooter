using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private void OnEnable() => snapNext = true;

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
        float next = current < 0f || snapNext ? target : Mathf.MoveTowards(current, target, step);

        if (icon != null)
        {
            Color color = icon.color;
            color.a = next;
            icon.color = color;
        }

        return next;
    }
}
