using UnityEngine;

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
