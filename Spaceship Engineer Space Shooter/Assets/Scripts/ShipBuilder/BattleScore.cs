/// <summary>
/// Points earned so far this battle — destroying a meteor (or later, an enemy ship's block) adds to
/// it. Read once at battle-end to convert into credits (see BattleOutcomeController). A plain static
/// accumulator, reset at the start of each battle, same pattern as PendingBattle.
/// </summary>
public static class BattleScore
{
    public static int Points;

    public static void Reset() => Points = 0;
    public static void Add(int amount) => Points += amount;
}
