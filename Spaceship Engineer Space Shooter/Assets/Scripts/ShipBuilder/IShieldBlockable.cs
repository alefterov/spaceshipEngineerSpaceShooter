public interface IShieldBlockable
{
    float ImpactDamage { get; }

    bool IsHostileTo(Faction shieldFaction);

    bool IsConsumed { get; }

    void AbsorbedByShield();
}
