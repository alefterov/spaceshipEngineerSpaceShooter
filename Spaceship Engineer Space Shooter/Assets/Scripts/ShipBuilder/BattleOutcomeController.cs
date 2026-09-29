using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Decides how a battle ends and reacts to it:
///  - Player ship destroyed OR disabled (last Cockpit/Generator lost) -> loss.
///  - Every wave has finished spawning AND no enemy hazards remain (Targetable.Active) -> win. Stars
///    (1-3) come from how much of the ship's starting HP survived.
///
/// Either way, the SAME BattleResultPanel is told what to show — see that class for the win/loss
/// display differences (stars, message, Next Level availability, rewards).
///
/// REWARDS: BattleRewards.Credits/Experience is whatever killing enemies earned this battle (see
/// Meteor's OnDestroyed wiring) — paid in FULL on a win, no matter how many times this level has
/// already been cleared before. Losing halves BOTH credits and experience earned from kills. On a win, a
/// level-completion bonus is added on top — but only the DIFFERENCE between this run's star bonus and
/// whatever star bonus a previous best already paid out (see CreditsBonusForStars/
/// ExperienceBonusForStars), so re-clearing a level at the same star rating pays no bonus again, and
/// improving from e.g. 1 star to 3 only pays the 1->3 difference.
/// Credits (kills + bonus together) are then scaled by GameDataManager.GetCreditsMultiplier(), the
/// hangar-side battle-credits upgrade. Separately, the FIRST time a level is cleared with 3 stars it
/// also grants a flat research-point bonus (threeStarResearchBonus) — not tied to the credits/xp diff
/// bonus above, and not paid again on a repeat 3-star clear.
///
/// Deliberately does NOT pause the scene (no Time.timeScale) — whatever's left of the player's ship
/// just sits there un-piloted (SetCombatActive(false) stops it responding to the joystick and stops
/// its weapons firing) while everything else keeps going: existing enemies keep flying/attacking, and
/// any wreckage keeps drifting under whatever motion it already had, instead of the whole battle
/// freezing behind the result panel.
///
/// Wire BattleSequenceController to call BeginTracking() once its arrival/countdown intro finishes —
/// starting the starting-HP snapshot and enemy tracking any earlier would count intro-time as combat.
/// </summary>
public class BattleOutcomeController : MonoBehaviour
{
    [Header("References")]
    public ShipGrid playerShipGrid;
    public ShipIdentity playerShipIdentity;
    public EnemySpawner enemySpawner;
    public BattleResultPanel resultPanel;

    [Header("Level-completion bonus — credits")]
    [Tooltip("Paid on a win when this run's star rating BEATS the level's previous best (see class doc " +
             "comment) — these are the balance knobs, tune freely.")]
    public int oneStarCreditsBonus = 100;
    public int twoStarCreditsBonus = 150;
    public int threeStarCreditsBonus = 200;

    [Header("Level-completion bonus — experience")]
    public int oneStarExperienceBonus = 50;
    public int twoStarExperienceBonus = 75;
    public int threeStarExperienceBonus = 100;

    [Header("Level-completion bonus — research points")]
    [Tooltip("Research points granted the FIRST time this level is cleared with 3 stars — not paid " +
             "again on a repeat 3-star clear (same before/after-best rule as the credits/experience " +
             "bonuses above, just with only one tier since it's an all-or-nothing 3-star reward).")]
    public int threeStarResearchBonus = 1;

    [Tooltip("Scene loaded by the 'Home' button.")]
    public string homeSceneName = "SampleScene";

    private float startingHP;
    private bool outcomeDecided;

    public void BeginTracking()
    {
        BattleRewards.Reset();
        startingHP = playerShipGrid != null ? playerShipGrid.ComputeCurrentTotalHP() : 0f;

        if (playerShipIdentity != null)
        {
            playerShipIdentity.OnShipDestroyed += _ => Lose();
            playerShipIdentity.OnShipDisabled += _ => Lose();
        }

        if (enemySpawner != null) enemySpawner.OnAllWavesSpawned += HandleAllWavesSpawned;
    }

    private void HandleAllWavesSpawned() => StartCoroutine(WaitForEnemiesCleared());

    private IEnumerator WaitForEnemiesCleared()
    {
        // Targetable.Active is every hazard currently alive (meteors now, missiles/etc. later) —
        // "cleared" means none left, regardless of whether each one was shot down or just flew past.
        while (!outcomeDecided && Targetable.Active.Any(t => t.kind != TargetKind.Ship))
            yield return null;

        if (!outcomeDecided) Win();
    }

    private void Lose()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;

        if (enemySpawner != null) enemySpawner.StopWaves(); // no new waves against an already-lost player — existing enemies keep going regardless
        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(false); // stops responding to the joystick AND stops its weapons firing

        int killCredits = Half(BattleRewards.Credits); // losing halves both credits and experience earned from kills
        int killExperience = Half(BattleRewards.Experience);

        int credits = ScaleCredits(killCredits);
        var xpSteps = AwardRewards(credits, killExperience);

        if (resultPanel != null) resultPanel.ShowResult(won: false, earnedStars: 0, hasNextLevel: false, credits, xpSteps);
    }

    private void Win()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;

        float currentHP = playerShipGrid != null ? playerShipGrid.ComputeCurrentTotalHP() : 0f;
        float ratio = startingHP > 0f ? currentHP / startingHP : 0f;
        int stars = ratio > 0.95f ? 3 : ratio >= 0.5f ? 2 : 1;

        // Read the PREVIOUS best before SetLevelStars below overwrites it — the bonus-difference below
        // depends on knowing what it was BEFORE this run.
        int previousStars = PendingBattle.Level != null ? GameDataManager.Instance.GetStars(PendingBattle.Level) : 0;

        int killCredits = BattleRewards.Credits; // a win always pays kills in full, first clear or not
        int killExperience = BattleRewards.Experience;

        int bonusCredits = stars > previousStars ? CreditsBonusForStars(stars) - CreditsBonusForStars(previousStars) : 0;
        int bonusExperience = stars > previousStars ? ExperienceBonusForStars(stars) - ExperienceBonusForStars(previousStars) : 0;

        int credits = ScaleCredits(killCredits + bonusCredits);
        int experience = killExperience + bonusExperience;
        var xpSteps = AwardRewards(credits, experience);

        // previousStars was read further up, BEFORE SetLevelStars below can overwrite it — that's what
        // makes this "the first time" rather than every repeat 3-star clear.
        if (PendingBattle.Level != null && stars >= 3 && previousStars < 3 && threeStarResearchBonus > 0)
            GameDataManager.Instance.AddResearchPoints(threeStarResearchBonus);

        if (PendingBattle.Level != null) GameDataManager.Instance.SetLevelStars(PendingBattle.Level, stars);
        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(false); // nothing left to fight — same "player done" treatment as a loss

        bool hasNextLevel = PendingBattle.Level != null && PendingBattle.Level.nextLevel != null;
        if (resultPanel != null) resultPanel.ShowResult(won: true, earnedStars: stars, hasNextLevel: hasNextLevel, credits, xpSteps);
    }

    private static int Half(int amount) => Mathf.RoundToInt(amount * 0.5f);

    private int CreditsBonusForStars(int stars) => stars switch
    {
        1 => oneStarCreditsBonus,
        2 => twoStarCreditsBonus,
        3 => threeStarCreditsBonus,
        _ => 0,
    };

    private int ExperienceBonusForStars(int stars) => stars switch
    {
        1 => oneStarExperienceBonus,
        2 => twoStarExperienceBonus,
        3 => threeStarExperienceBonus,
        _ => 0,
    };

    private static int ScaleCredits(int amount) => Mathf.RoundToInt(amount * GameDataManager.Instance.GetCreditsMultiplier());

    /// <summary>Actually grants the credits/experience computed above and returns the experience-bar
    /// animation steps for the result panel. Experience is scaled by the crew Captain's own bonus here,
    /// in one place, so it applies identically whether the battle was won or lost.</summary>
    private static List<GameDataManager.ExperienceGainStep> AwardRewards(int credits, int experience)
    {
        if (credits > 0) GameDataManager.Instance.AddCredits(credits);

        int scaledExperience = Mathf.RoundToInt(experience * GameDataManager.Instance.GetCaptainExperienceMultiplier());
        return GameDataManager.Instance.AddExperience(scaledExperience);
    }

    /// <summary>Wire to the loss/win panel's "Try Again" button.</summary>
    public void RetryLevel() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    /// <summary>Wire to the loss/win panel's "Home" button.</summary>
    public void GoHome() => SceneManager.LoadScene(homeSceneName);

    /// <summary>Wire to the result panel's "Next Level" button — BattleResultPanel already disables it
    /// on a loss or when this level has no NextLevel set, so reaching here implies both are fine.</summary>
    public void NextLevel()
    {
        if (PendingBattle.Level == null || PendingBattle.Level.nextLevel == null) return;

        PendingBattle.Level = PendingBattle.Level.nextLevel;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
