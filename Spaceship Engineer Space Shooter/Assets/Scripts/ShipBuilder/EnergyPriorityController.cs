using System.Collections.Generic;
using UnityEngine;

public class EnergyPriorityController : MonoBehaviour, IPriorityPanel
{
    [Tooltip("The player ship's ShipEnergySystem — whose PriorityOrder this window edits.")]
    public ShipEnergySystem playerEnergy;
    [Tooltip("The layout-group container holding the 4 PriorityDragItem rows, top = highest priority.")]
    public Transform priorityItemsParent;

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshPanel();
    }

    public void Close() => gameObject.SetActive(false);

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
