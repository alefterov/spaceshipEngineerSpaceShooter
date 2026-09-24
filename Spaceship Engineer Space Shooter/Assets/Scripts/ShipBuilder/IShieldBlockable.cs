/// <summary>
/// Anything a shield can stop before it reaches a block — meteors and enemy projectiles today, missiles
/// and the like later. ShieldModule's protected area looks for this on whatever enters it; implement it
/// on a hazard and shields will absorb it, no shield-specific code needed on the hazard's side.
/// </summary>
public interface IShieldBlockable
{
    /// <summary>Damage this would deal to a block — also what the shield's energy cost scales with.</summary>
    float ImpactDamage { get; }

    /// <summary>Whether a shield belonging to this faction should stop it (own shots pass through).</summary>
    bool IsHostileTo(Faction shieldFaction);

    /// <summary>True once a shield has taken it — stops a second, overlapping shield (or the block
    /// behind them) from also processing the same hazard in the same physics step.</summary>
    bool IsConsumed { get; }

    /// <summary>Called by the shield that absorbed it. The hazard must remove itself.</summary>
    void AbsorbedByShield();
}
