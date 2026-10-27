using UnityEngine;

/// <summary>
/// One draggable row of a target-priority list (the missiles' or the tower guns' — the same row serves both). Drag it up or down: the other
/// rows glide aside to make room and the row settles into the free place on release (the dragging itself is PriorityDragItemBase). The
/// container of the rows must be a layout group; the order of the rows inside it is the priority.
///
/// SETUP: put on each row, set Target Type to the block type this row stands for, and assign Controller to the controller of the list this
/// row belongs to — a MissilePriorityController (missiles) or a BalistickPriorityController (tower guns). A CanvasGroup and a
/// LayoutElement are required on the same object (added automatically if missing).
/// </summary>
public class MissilePriorityDragItem : PriorityDragItemBase
{
    [Tooltip("Cockpit, Weapon (every kind of weapon), Generator, Shield, Engine, Repair, Armor or Meteor. Other types are ignored.")]
    public ModuleType targetType = ModuleType.Weapon;
    [Tooltip("The list this row belongs to: a MissilePriorityController or a BalistickPriorityController.")]
    public TargetPriorityController controller;

    protected override void OnOrderChanged()
    {
        if (controller != null) controller.SetOrderFromSiblingOrder();
    }
}
