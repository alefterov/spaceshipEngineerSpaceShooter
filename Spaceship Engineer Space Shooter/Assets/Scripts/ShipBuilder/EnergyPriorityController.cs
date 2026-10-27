using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The energy-priority tab of the priorities window (ShipPrioritiesWindow): drag the 4 PriorityDragItem rows into whatever order you want
/// (see that class) — each drop reorders the player's ShipEnergySystem live via SetOrder, so the effect is visible at once. Opening the
/// window and switching between its tabs is the window's job (it also pauses the battle); this component only keeps its rows in step
/// with the energy system.
///
/// SETUP: put on the tab's root object (the one the window shows and hides — see ShipPrioritiesWindow, Categories). Assign Player Energy
/// (the player ship's ShipEnergySystem) and Priority Items Parent (the layout-group container holding the 4 PriorityDragItem rows).
/// </summary>
public class EnergyPriorityController : MonoBehaviour, IPriorityPanel
{
    [Tooltip("The player ship's ShipEnergySystem — whose PriorityOrder this window edits.")]
    public ShipEnergySystem playerEnergy;
    [Tooltip("The layout-group container holding the 4 PriorityDragItem rows, top = highest priority.")]
    public Transform priorityItemsParent;

    /// <summary>Shows this tab on its own (without the window) and lays its rows out. The priorities window does this itself.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RefreshPanel();
    }

    public void Close() => gameObject.SetActive(false);

    /// <summary>Lays the 4 rows out to match the energy system's CURRENT order — called when the tab is shown, so
    /// it always reflects reality rather than whatever order it was left in last time.</summary>
    public void RefreshPanel()
    {
        if (playerEnergy == null || priorityItemsParent == null) return;

        var order = playerEnergy.PriorityOrder;
        var itemsByGroup = new Dictionary<ShipEnergySystem.EnergyPriorityGroup, PriorityDragItem>();

        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<PriorityDragItem>(out var item))
                itemsByGroup[item.system] = item;

        for (int i = 0; i < order.Count; i++)
            if (itemsByGroup.TryGetValue(order[i], out var item))
                item.transform.SetSiblingIndex(i);
    }

    /// <summary>Called by a PriorityDragItem once it's dropped into a new slot — reads the current
    /// sibling order of Priority Items Parent and pushes it straight to the player's ShipEnergySystem.</summary>
    public void SetOrderFromSiblingOrder()
    {
        if (playerEnergy == null || priorityItemsParent == null) return;

        var order = new List<ShipEnergySystem.EnergyPriorityGroup>();
        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<PriorityDragItem>(out var item))
                order.Add(item.system);

        playerEnergy.SetPriorityOrder(order);
    }
}
