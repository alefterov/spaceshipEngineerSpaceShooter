using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a target-priority tab of the priorities window (ShipPrioritiesWindow) does, whichever weapons it is for: keeps its rows
/// (MissilePriorityDragItem) in the saved order when it is shown, and saves the new order whenever a row is dropped. The concrete tabs are
/// MissilePriorityController (missiles) and BalistickPriorityController (tower guns).
/// </summary>
public abstract class TargetPriorityController : MonoBehaviour, IPriorityPanel
{
    [Tooltip("The player's ship root (the object with ShipIdentity).")]
    public ShipIdentity playerShip;
    [Tooltip("The layout-group container holding the MissilePriorityDragItem rows, top = hit first.")]
    public Transform priorityItemsParent;

    private TargetPriority priority;

    /// <summary>The priority list of the player's ship this tab edits (added to the ship if it has none).</summary>
    protected abstract TargetPriority GetPriority(ShipIdentity ship);

    private TargetPriority Priority
    {
        get
        {
            if (priority == null && playerShip != null) priority = GetPriority(playerShip);
            return priority;
        }
    }

    /// <summary>Shows this tab on its own (without the window) and lays its rows out. The priorities window does this itself.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RefreshPanel();
    }

    public void Close() => gameObject.SetActive(false);

    /// <summary>Lays the rows out to match the saved order — so the tab always reflects reality.</summary>
    public void RefreshPanel()
    {
        var current = Priority;
        if (current == null || priorityItemsParent == null) return;

        var itemsByType = new Dictionary<ModuleType, MissilePriorityDragItem>();
        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<MissilePriorityDragItem>(out var item))
                itemsByType[item.targetType] = item;

        var order = current.Order;
        for (int i = 0; i < order.Count; i++)
            if (itemsByType.TryGetValue(order[i], out var item))
                item.transform.SetSiblingIndex(i);
    }

    /// <summary>Called by a row once it is dropped into a new slot — reads the rows' current order and saves it.</summary>
    public void SetOrderFromSiblingOrder()
    {
        var current = Priority;
        if (current == null || priorityItemsParent == null) return;

        var order = new List<ModuleType>();
        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<MissilePriorityDragItem>(out var item))
                order.Add(item.targetType);

        current.SetOrder(order);
    }
}
