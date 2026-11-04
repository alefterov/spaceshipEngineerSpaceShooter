using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

            var captured = tab;
            tab.onClick.AddListener(() => SetActiveTab(captured));
        }
    }

    public void SetActiveTab(Button tab)
    {
        if (activeTab != null && activeTab.image != null && defaultColors.TryGetValue(activeTab, out var prevColor))
            activeTab.image.color = prevColor;

        activeTab = tab;
        if (activeTab != null && activeTab.image != null)
            activeTab.image.color = activeColor;
    }

    public void ClearActiveTab()
    {
        if (activeTab != null && activeTab.image != null && defaultColors.TryGetValue(activeTab, out var prevColor))
            activeTab.image.color = prevColor;
        activeTab = null;
    }
}
