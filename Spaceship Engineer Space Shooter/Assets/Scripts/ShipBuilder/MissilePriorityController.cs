using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The missile target-priority window — works exactly like the energy-priority window
/// (EnergyPriorityController): opening it PAUSES the battle (Time.timeScale = 0), and you drag the
/// MissilePriorityDragItem rows into the order you want; each drop saves the order at once (the player's
/// order is remembered between battles). The top row is the block type missiles go for first.
///
/// SETUP: put on the panel's root (starts inactive). Assign Player Ship (the player's ship root — the object with
/// ShipIdentity; a MissileTargetPriority is added to it if it has none) and Priority Items Parent (the
/// layout-group container holding the 7 MissilePriorityDragItem rows: Cockpit, Weapon, Generator, Shield,
/// Engine, Repair, Armor). Wire your "open" button to Open()/Toggle() and the close button to Close().
/// </summary>
public class MissilePriorityController : MonoBehaviour
{
    [Tooltip("The player's ship root (the object with ShipIdentity).")]
    public ShipIdentity playerShip;
    [Tooltip("The layout-group container holding the MissilePriorityDragItem rows, top = hit first.")]
    public Transform priorityItemsParent;

    private float previousTimeScale = 1f;
    private MissileTargetPriority priority;

    public bool IsOpen { get; private set; }

    private MissileTargetPriority Priority
    {
        get
        {
            if (priority == null && playerShip != null) priority = MissileTargetPriority.GetOrAdd(playerShip);
            return priority;
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        gameObject.SetActive(true);
        RefreshItems();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        Time.timeScale = previousTimeScale;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>Lays the rows out to match the saved order — so the window always reflects reality.</summary>
    private void RefreshItems()
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
