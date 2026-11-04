using System;

public static class BattleRewards
{
    public static int Credits { get; private set; }
    public static int Experience { get; private set; }

    public static event Action OnKillRewardChanged;

    public static void Reset()
    {
        Credits = 0;
        Experience = 0;
    }

    public static void AddKill(int credits, int experience)
    {
        Credits += credits;
        Experience += experience;
        OnKillRewardChanged?.Invoke();
    }
}
