/// <summary>
/// The missile target-priority tab of the priorities window (ShipPrioritiesWindow). You drag the MissilePriorityDragItem rows into the
/// order you want; each drop saves the order at once (remembered between battles). The top row is the block type missiles go for first.
/// Opening the window and switching between its tabs is the window's job (it also pauses the battle).
///
/// SETUP: put on the tab's root object (the one the window shows and hides — see ShipPrioritiesWindow, Categories). Assign Player Ship
/// (the player's ship root — the object with ShipIdentity; a MissileTargetPriority is added to it if it has none) and Priority Items
/// Parent (the layout-group container holding the rows: Cockpit, Weapon, Generator, Shield, Engine, Repair, Armor, Meteor). Each row's
/// Controller is this component.
/// </summary>
public class MissilePriorityController : TargetPriorityController
{
    protected override TargetPriority GetPriority(ShipIdentity ship) => MissileTargetPriority.GetOrAdd(ship);
}
