using System;

/// <summary>
/// Credits and experience earned by DESTROYING ENEMIES so far this battle (kills only — the
/// level-completion bonus is separate, see BattleOutcomeController). A plain static accumulator, reset
/// at the start of each battle (BattleOutcomeController.BeginTracking) — same pattern as PendingBattle.
///
/// Ticks up here immediately, one kill at a time — BattleRewardCounterView (the HUD's live "coins
/// earned" counter) watches OnKillRewardChanged and animates its own displayed number toward
/// Credits/Experience rather than snapping, so a burst of kills reads as one smooth climb.
/// </summary>
public static class BattleRewards
{
    public static int Credits { get; private set; }
    public static int Experience { get; private set; }

    /// <summary>Fired every time a kill adds credits/experience.</summary>
    public static event Action OnKillRewardChanged;

    public static void Reset()
    {
        Credits = 0;
        Experience = 0;
    }

    /// <summary>Call when an enemy is actually destroyed (shot down, or stopped by a shield) — NOT when
    /// it merely hits the ship or flies off-screen; see e.g. Meteor's own OnDestroyed wiring.</summary>
    public static void AddKill(int credits, int experience)
    {
        Credits += credits;
        Experience += experience;
        OnKillRewardChanged?.Invoke();
    }
}
