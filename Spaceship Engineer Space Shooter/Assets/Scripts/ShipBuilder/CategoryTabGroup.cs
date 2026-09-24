using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A row of mutually-exclusive tab/category buttons — clicking one tints it (ActiveColor, red by
/// default) and resets whichever tab was previously active back to its normal color. Purely visual:
/// each button's own OnClick (wired separately in the Inspector, e.g. to BuildModeController.
/// SetArmorBuildMode / ShowShields) still does the actual mode switch — this component just
/// listens to the SAME buttons automatically (no extra OnClick entries needed) and manages which one
/// looks "active".
///
/// Use one instance per row — e.g. one for the Cockpit/Generators/Engines/Armor/Weapons tabs, a separate one for each
/// sub-tab row (Armor/Shields, Ballistic/Laser/Missile/Plasma). Assumes each button's own Image is its background (the
/// same Image assigned to Selectable.Image, not just Target Graphic — see BuildModeController's
/// deleteButton for the same requirement).
/// </summary>
public class CategoryTabGroup : MonoBehaviour
{
    public List<Button> tabs = new();
    public Color activeColor = Color.red;

    private readonly Dictionary<Button, Color> defaultColors = new();
    private Button activeTab;

    private void Awake()
    {
        foreach (var tab in tabs)
        {
            if (tab == null) continue;

            defaultColors[tab] = tab.image != null ? tab.image.color : Color.white;

            var captured = tab; // local copy — avoids the classic "closes over the loop variable" bug
            tab.onClick.AddListener(() => SetActiveTab(captured));
        }
    }

    /// <summary>Tints this tab active and restores whichever one was active before. Safe to call
    /// with a tab not in the list (just won't have a stored default color to restore later).</summary>
    public void SetActiveTab(Button tab)
    {
        if (activeTab != null && activeTab.image != null && defaultColors.TryGetValue(activeTab, out var prevColor))
            activeTab.image.color = prevColor;

        activeTab = tab;
        if (activeTab != null && activeTab.image != null)
            activeTab.image.color = activeColor;
    }

    /// <summary>Clears the highlight without picking a new active tab — e.g. leaving this section entirely.</summary>
    public void ClearActiveTab()
    {
        if (activeTab != null && activeTab.image != null && defaultColors.TryGetValue(activeTab, out var prevColor))
            activeTab.image.color = prevColor;
        activeTab = null;
    }
}
