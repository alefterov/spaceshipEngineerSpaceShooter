/// <summary>
/// Critical hits. Only the PLAYER's weapons can crit: the chance is the Captain's Crit Chance perk of the crew in the slots
/// (capped by GameDataManager.crewChanceCap), and a crit deals GameDataManager.critDamageMultiplier times the damage (2 by
/// default). The hit's damage number turns red (see DamageNumbers).
///
/// A weapon calls Roll once per hit — or once per shot for a beam and a missile — and passes the result on to
/// ShipModule.TakeDamage(amount, crit).
/// </summary>
public static class CriticalHit
{
    /// <summary>Rolls the dice for a hit by `owner`. Always false for the enemy side.</summary>
    public static bool Roll(Faction owner)
    {
        if (owner != Faction.Player) return false;

        var data = GameDataManager.Instance;
        return data != null && data.RollCrewChance(CrewPerkType.CritChance);
    }

    /// <summary>How much a critical hit multiplies the damage.</summary>
    public static float Multiplier
    {
        get
        {
            var data = GameDataManager.Instance;
            return data != null ? data.critDamageMultiplier : 2f;
        }
    }

    /// <summary>Rolls and applies in one go: returns the damage to deal (multiplied on a crit) and whether it was a crit.</summary>
    public static float Roll(Faction owner, float damage, out bool crit)
    {
        crit = damage > 0f && Roll(owner);
        return crit ? damage * Multiplier : damage;
    }
}
