using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Survival Definition", fileName = "SurvivalDefinition")]
public class SurvivalDefinition : ScriptableObject
{
    [Tooltip("Every enemy ship model of survival, WEAKEST FIRST. No bosses.")]
    public List<GameObject> ships = new();

    [Header("Time between ships of a wave")]
    [Min(0f)] public float firstInterval = 10f;
    [Min(0f)] public float minInterval = 5f;
    [Tooltip("The wave from which the interval is Min Interval.")]
    [Min(1)] public int minIntervalWave = 50;

    [Header("Between waves")]
    [Tooltip("Seconds after a wave's last ship before the next wave starts (it starts at once if no enemy ship is left).")]
    [Min(0f)] public float wavePause = 30f;

    [Header("Reward")]
    [Tooltip("Coins for each wave that is over: this x the wave's number.")]
    [Min(0)] public int coinsPerWave = 10;

    [Header("Spawning")]
    public SpawnSide side = SpawnSide.Top;

    public List<GameObject> ShipsForWave(int wave)
    {
        var result = new List<GameObject>();
        var models = ships.FindAll(s => s != null);
        if (models.Count == 0 || wave < 1) return result;

        for (int model = 0; model < models.Count && model < wave; model++)
        {
            for (int c = 0; c < wave - model; c++) result.Add(models[model]);
        }

        return result;
    }

    public float IntervalForWave(int wave)
    {
        if (minIntervalWave <= 1) return minInterval;
        float t = Mathf.Clamp01((wave - 1f) / (minIntervalWave - 1f));
        return Mathf.Lerp(firstInterval, minInterval, t);
    }

    public int CoinsForWave(int wave) => coinsPerWave * Mathf.Max(0, wave);
}
