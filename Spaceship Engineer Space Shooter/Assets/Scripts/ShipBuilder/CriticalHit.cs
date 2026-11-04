public static class CriticalHit
{
    public static bool Roll(Faction owner)
    {
        if (owner != Faction.Player) return false;

        var data = GameDataManager.Instance;
        return data != null && data.RollCrewChance(CrewPerkType.CritChance);
    }

    public static float Multiplier
    {
        get
        {
            var data = GameDataManager.Instance;
            return data != null ? data.critDamageMultiplier : 2f;
        }
    }

    public static float Roll(Faction owner, float damage, out bool crit)
    {
        crit = damage > 0f && Roll(owner);
        return crit ? damage * Multiplier : damage;
    }
}
