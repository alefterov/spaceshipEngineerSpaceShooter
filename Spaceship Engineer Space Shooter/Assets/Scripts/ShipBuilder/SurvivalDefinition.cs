using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything survival mode is made of. Survival is played in the battle scene (PendingBattle.Level == null) and never ends until the
/// player's ship is lost: wave after wave, each one bigger.
///  - Wave N: N of the weakest ship, N-1 of the next one, N-2 of the next... down to 1 — wave 1 = 1 weakest; wave 2 = 2 weakest + 1 of
///    the next; wave 3 = 3 + 2 + 1. Every wave a new, stronger model joins (until all of Ships are in). Inside a wave they come weakest first.
///  - Between two ships of a wave: First Interval at wave 1, falling evenly to Min Interval at Min Interval Wave, then staying there.
///  - After a wave's last ship has come: the next wave starts after Wave Pause seconds — or at once, when no enemy ship is left.
///  - Each wave that is over pays Coins Per Wave x its number in coins (wave 7 = 70 with the default 10). Kills pay credits and
///    experience as in the campaign.
/// The starting wave is GameDataManager.GetSurvivalStartLevel() (the hangar upgrade).
/// Made by Tools > Ship Builder > Create Survival Definition (ships sorted by strength — reorder them by hand if you like); assign it
/// to the battle scene's EnemySpawner.
/// </summary>
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

    /// <summary>The ships of wave `wave` (1-based), weakest first.</summary>
    public List<GameObject> ShipsForWave(int wave)
    {
        var result = new List<GameObject>();
        var models = ships.FindAll(s => s != null);
        if (models.Count == 0 || wave < 1) return result;

        // Model k (0 = the weakest) comes wave - k times: wave 3 = 3 of the first, 2 of the second, 1 of the third.
        for (int model = 0; model < models.Count && model < wave; model++)
        {
            for (int c = 0; c < wave - model; c++) result.Add(models[model]);
        }

        return result;
    }

    /// <summary>Seconds between two ships of wave `wave`.</summary>
    public float IntervalForWave(int wave)
    {
        if (minIntervalWave <= 1) return minInterval;
        float t = Mathf.Clamp01((wave - 1f) / (minIntervalWave - 1f));
        return Mathf.Lerp(firstInterval, minInterval, t);
    }

    /// <summary>Coins paid when wave `wave` is over.</summary>
    public int CoinsForWave(int wave) => coinsPerWave * Mathf.Max(0, wave);
}
