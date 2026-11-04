using System.Collections.Generic;
using UnityEngine;

public abstract class TargetPriorityController : MonoBehaviour, IPriorityPanel
{
    [Tooltip("The player's ship root (the object with ShipIdentity).")]
    public ShipIdentity playerShip;
    [Tooltip("The layout-group container holding the MissilePriorityDragItem rows, top = hit first.")]
    public Transform priorityItemsParent;

    private TargetPriority priority;

    protected abstract TargetPriority GetPriority(ShipIdentity ship);

    private TargetPriority Priority
    {
        get
        {
            if (priority == null && playerShip != null) priority = GetPriority(playerShip);
            return priority;
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshPanel();
    }

    public void Close() => gameObject.SetActive(false);

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
