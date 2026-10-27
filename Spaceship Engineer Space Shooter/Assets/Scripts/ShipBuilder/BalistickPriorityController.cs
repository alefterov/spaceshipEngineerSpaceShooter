/// <summary>
/// The tower-gun target-priority tab of the priorities window (ShipPrioritiesWindow) — exactly like the missiles' tab
/// (MissilePriorityController), but its order is used ONLY by the tower guns: the KineticTurretGun blocks (1x1 and 2x2; see
/// WeaponModule.IsTowerGun). Fixed guns, point-defence turrets and every other weapon ignore it. The order is remembered between battles.
///
/// SETUP: put on the tab's root object (BalistickPriorityContainer). Assign Player Ship and Priority Items Parent (its own row list); each
/// row there is a MissilePriorityDragItem whose Controller is this component.
/// </summary>
public class BalistickPriorityController : TargetPriorityController
{
    protected override TargetPriority GetPriority(ShipIdentity ship) => TowerTargetPriority.GetOrAdd(ship);
}
