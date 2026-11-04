public class BalistickPriorityController : TargetPriorityController
{
    protected override TargetPriority GetPriority(ShipIdentity ship) => TowerTargetPriority.GetOrAdd(ship);
}
