using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Tooltip("The battle stops (time scale 0) the moment the result panel comes up. The panel itself runs on unscaled time.")]
    public bool stopBattleOnResult = true;

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
        while (!outcomeDecided && (Targetable.Active.Any(t => t.kind != TargetKind.Ship) || ShipIdentity.AnyEnemyShipOperational))
            yield return null;

        if (!outcomeDecided) Win();
    }

    private void Lose()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;
        StopBattle();
        if (AdsManager.Instance != null) AdsManager.Instance.NotifyBattleFinished();

        if (enemySpawner != null) enemySpawner.StopWaves();
        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(false);

        if (enemySpawner != null && enemySpawner.IsSurvival)
        {
            int survivalCredits = ScaleCredits(BattleRewards.Credits);
            var survivalSteps = AwardRewards(survivalCredits, BattleRewards.Experience);
            if (resultPanel != null) resultPanel.ShowSurvivalResult(enemySpawner.CurrentWave, enemySpawner.SurvivalCoinsEarned, survivalCredits, survivalSteps);
            return;
        }

        int killCredits = Half(BattleRewards.Credits);
        int killExperience = Half(BattleRewards.Experience);

        int credits = ScaleCredits(killCredits);
        var xpSteps = AwardRewards(credits, killExperience);

        if (resultPanel != null) resultPanel.ShowResult(won: false, earnedStars: 0, hasNextLevel: false, credits, xpSteps);
    }

    private void Win()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;
        StopBattle();
        if (AdsManager.Instance != null) AdsManager.Instance.NotifyBattleFinished();

        float currentHP = playerShipGrid != null ? playerShipGrid.ComputeCurrentTotalHP() : 0f;
        float ratio = startingHP > 0f ? currentHP / startingHP : 0f;
        int stars = ratio > 0.95f ? 3 : ratio >= 0.5f ? 2 : 1;

        int previousStars = PendingBattle.Level != null ? GameDataManager.Instance.GetStars(PendingBattle.Level) : 0;

        int killCredits = BattleRewards.Credits;
        int killExperience = BattleRewards.Experience;

        int bonusCredits = stars > previousStars ? CreditsBonusForStars(stars) - CreditsBonusForStars(previousStars) : 0;
        int bonusExperience = stars > previousStars ? ExperienceBonusForStars(stars) - ExperienceBonusForStars(previousStars) : 0;

        int credits = ScaleCredits(killCredits + bonusCredits);
        int experience = killExperience + bonusExperience;
        var xpSteps = AwardRewards(credits, experience);

        if (PendingBattle.Level != null && stars >= 3 && previousStars < 3 && threeStarResearchBonus > 0)
            GameDataManager.Instance.AddResearchPoints(threeStarResearchBonus);

        if (PendingBattle.Level != null) GameDataManager.Instance.SetLevelStars(PendingBattle.Level, stars);
        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(false);

        bool hasNextLevel = PendingBattle.Level != null && PendingBattle.Level.nextLevel != null;
        if (resultPanel != null) resultPanel.ShowResult(won: true, earnedStars: stars, hasNextLevel: hasNextLevel, credits, xpSteps);
    }

    private void StopBattle()
    {
        if (stopBattleOnResult) Time.timeScale = 0f;
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

    private static int ScaleCredits(int amount)
        => Mathf.RoundToInt(amount * GameDataManager.Instance.GetCreditsMultiplier() * GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.CreditsGain)
                            * GameDataManager.Instance.PremiumRewardMultiplier);

    private static List<GameDataManager.ExperienceGainStep> AwardRewards(int credits, int experience)
    {
        if (credits > 0) GameDataManager.Instance.AddCredits(credits);

        int scaledExperience = Mathf.RoundToInt(experience * GameDataManager.Instance.GetCrewPerkMultiplier(CrewPerkType.ExperienceGain)
                                                * GameDataManager.Instance.PremiumRewardMultiplier);
        return GameDataManager.Instance.AddExperience(scaledExperience);
    }

    public void RetryLevel() => SceneLoader.Load(SceneManager.GetActiveScene().name);

    public void GoHome() => SceneLoader.Load(homeSceneName);

    public void NextLevel()
    {
        if (PendingBattle.Level == null || PendingBattle.Level.nextLevel == null) return;

        PendingBattle.Level = PendingBattle.Level.nextLevel;
        SceneLoader.Load(SceneManager.GetActiveScene().name);
    }
}
