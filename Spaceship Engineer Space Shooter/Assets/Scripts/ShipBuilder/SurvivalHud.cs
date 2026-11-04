using TMPro;
using UnityEngine;

public class SurvivalHud : MonoBehaviour
{
    public enum CampaignGauge { NextEnemyCountdown = 0, WavesProgress = 1, Hidden = 2 }

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
    [Tooltip("Campaign: what the gauge shows — the time until the next enemy, how many of the level's waves have started, or nothing.")]
    public CampaignGauge campaignGauge = CampaignGauge.NextEnemyCountdown;
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

        survival = enemySpawner.IsSurvival;
        if (gauge != null) gauge.gameObject.SetActive(survival || campaignGauge != CampaignGauge.Hidden);
        if (secondsLabel != null) secondsLabel.gameObject.SetActive(survival);
        if (coinsLabel != null) coinsLabel.gameObject.SetActive(survival);
    }

    private void Update()
    {
        if (!survival)
        {
            if (gauge != null)
            {
                if (campaignGauge == CampaignGauge.NextEnemyCountdown) gauge.SetValue(enemySpawner.WaitRemaining, enemySpawner.WaitDuration);
                else if (campaignGauge == CampaignGauge.WavesProgress) gauge.SetValue(enemySpawner.CurrentWave, enemySpawner.TotalWaves);
            }

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
