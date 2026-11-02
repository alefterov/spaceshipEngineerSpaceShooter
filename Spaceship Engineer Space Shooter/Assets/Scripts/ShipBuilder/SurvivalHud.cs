using TMPro;
using UnityEngine;

/// <summary>
/// The waves part of the battle HUD.
///  - SURVIVAL: a RadialGauge counting down the time until the next enemy ship (between the ships of a wave, and the pause before the
///    next wave), the wave number, and optionally the seconds left and the coins earned so far.
///  - CAMPAIGN: only the Wave Label, showing how many waves are left (Campaign Wave Format); the gauge, seconds and coins are hidden.
///
/// SETUP: put on the battle HUD. Assign the Enemy Spawner, the Gauge (a RadialGaugeView — its fill is full when a wait starts and runs
/// out as it ends; tick Show Used on it for the other way round) and the Wave Label. Root (the object holding these parts) is optional.
/// </summary>
public class SurvivalHud : MonoBehaviour
{
    public EnemySpawner enemySpawner;
    [Tooltip("Optional: holds these HUD parts; switched off only if the scene has no EnemySpawner.")]
    public GameObject root;
    [Tooltip("Counts down the time until the next enemy ship.")]
    public RadialGaugeView gauge;
    [Tooltip("Optional: the seconds left, e.g. \"7\".")]
    public TMP_Text secondsLabel;
    [Tooltip("Optional: the wave number.")]
    public TMP_Text waveLabel;
    [Tooltip("Survival: {0} = the wave number.")]
    public string waveFormat = "Волна {0}";
    [Tooltip("Campaign: {0} = waves left (the current one included), {1} = the current wave, {2} = all the waves of the level.")]
    public string campaignWaveFormat = "Осталось волн: {0}";
    [Tooltip("Optional: the coins paid for waves this battle.")]
    public TMP_Text coinsLabel;

    private bool survival;

    private void Start()
    {
        if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();
        if (enemySpawner == null)
        {
            if (root != null) root.SetActive(false);
            enabled = false;
            return;
        }

        // A campaign level shows only the waves left.
        survival = enemySpawner.IsSurvival;
        if (gauge != null) gauge.gameObject.SetActive(survival);
        if (secondsLabel != null) secondsLabel.gameObject.SetActive(survival);
        if (coinsLabel != null) coinsLabel.gameObject.SetActive(survival);
    }

    private void Update()
    {
        if (!survival)
        {
            if (waveLabel != null)
                waveLabel.text = enemySpawner.TotalWaves > 0
                    ? string.Format(campaignWaveFormat, enemySpawner.WavesLeft, enemySpawner.CurrentWave, enemySpawner.TotalWaves)
                    : "";
            return;
        }

        float remaining = enemySpawner.WaitRemaining;
        float duration = enemySpawner.WaitDuration;

        if (gauge != null) gauge.SetValue(remaining, duration);
        if (secondsLabel != null) secondsLabel.text = remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : "";
        if (waveLabel != null) waveLabel.text = enemySpawner.CurrentWave > 0 ? string.Format(waveFormat, enemySpawner.CurrentWave) : "";
        if (coinsLabel != null) coinsLabel.text = NumberFormat.Compact(enemySpawner.SurvivalCoinsEarned);
    }
}
