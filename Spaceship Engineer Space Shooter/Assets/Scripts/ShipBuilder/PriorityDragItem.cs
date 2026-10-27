using UnityEngine;

/// <summary>
/// One draggable row of the energy-priority list — drag it up or down: the other rows glide aside to make room and the row settles into
/// the free place on release (the dragging itself is PriorityDragItemBase). The container of the 4 rows must be a layout group (e.g. a
/// Vertical Layout Group); the order of the rows inside it is the priority.
///
/// SETUP: put on each of the 4 priority rows (Movement/Weapons/Defense/Repair), set System to whichever one this row represents, and
/// assign Controller to the scene's EnergyPriorityController. A CanvasGroup and a LayoutElement are required on the same object (added
/// automatically if missing).
/// </summary>
public class PriorityDragItem : PriorityDragItemBase
{
    public ShipEnergySystem.EnergyPriorityGroup system;
    public EnergyPriorityController controller;

    protected override void OnOrderChanged()
    {
        if (controller != null) controller.SetOrderFromSiblingOrder();
    }
}
