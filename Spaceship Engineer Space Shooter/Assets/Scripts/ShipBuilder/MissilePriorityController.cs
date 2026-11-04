public class MissilePriorityController : TargetPriorityController
{
    protected override TargetPriority GetPriority(ShipIdentity ship) => MissileTargetPriority.GetOrAdd(ship);
}
