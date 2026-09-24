using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Decides how a battle ends and reacts to it:
///  - Player ship destroyed OR disabled (last Cockpit/Generator lost) -> loss.
///  - Every wave has finished spawning AND no enemy hazards remain (Targetable.Active) -> win. Stars
///    (1-3) come from how much of the ship's starting HP survived; battle score converts to credits
///    either way, win or lose — points earned before dying still count.
/// Either way, the SAME BattleResultPanel is told what to show — see that class for the win/loss
/// display differences (stars, message, Next Level availability).
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

    [Tooltip("How many BattleScore points convert into 1 credit.")]
    public float pointsPerCredit = 1f;

    [Tooltip("Scene loaded by the 'Home' button.")]
    public string homeSceneName = "SampleScene";

    private float startingHP;
    private bool outcomeDecided;

    public void BeginTracking()
    {
        BattleScore.Reset();
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
        AwardCredits(); // points earned before dying still count

        if (resultPanel != null) resultPanel.ShowResult(won: false, earnedStars: 0, hasNextLevel: false);
    }

    private void Win()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;

        float currentHP = playerShipGrid != null ? playerShipGrid.ComputeCurrentTotalHP() : 0f;
        float ratio = startingHP > 0f ? currentHP / startingHP : 0f;
        int stars = ratio > 0.95f ? 3 : ratio >= 0.5f ? 2 : 1;

        if (PendingBattle.Level != null) GameDataManager.Instance.SetLevelStars(PendingBattle.Level, stars);
        AwardCredits();
        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(false); // nothing left to fight — same "player done" treatment as a loss

        bool hasNextLevel = PendingBattle.Level != null && PendingBattle.Level.nextLevel != null;
        if (resultPanel != null) resultPanel.ShowResult(won: true, earnedStars: stars, hasNextLevel: hasNextLevel);
    }

    private void AwardCredits()
    {
        int credits = Mathf.RoundToInt(BattleScore.Points / Mathf.Max(0.01f, pointsPerCredit));
        if (credits > 0) GameDataManager.Instance.AddCredits(credits);
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
