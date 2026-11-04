using UnityEngine;

public class PriorityDragItem : PriorityDragItemBase
{
    public ShipEnergySystem.EnergyPriorityGroup system;
    public EnergyPriorityController controller;

    protected override void OnOrderChanged()
    {
        if (controller != null) controller.SetOrderFromSiblingOrder();
    }
}
