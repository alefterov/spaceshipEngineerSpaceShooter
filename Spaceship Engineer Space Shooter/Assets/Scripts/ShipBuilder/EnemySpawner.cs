using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpawnSide { Top, Bottom, Left, Right }

[System.Serializable]
public class WaveEnemyEntry
{
    public GameObject enemyPrefab;
    [Tooltip("How many of this entry spawn in its wave.")]
    public int count = 1;
    [Tooltip("Seconds between each spawn of this entry. 0 = all at once (\"by two at a time\" etc.).")]
    public float spawnInterval = 1f;
    [Tooltip("Which edge of the camera's current view this entry spawns just outside of.")]
    public SpawnSide side = SpawnSide.Top;
    [Range(0f, 1f)]
    [Tooltip("Where along that edge — 0 is one corner, 1 the other, 0.5 the middle. Ignored if the " +
             "spawner's Randomize Position Along Edge is on.")]
    public float positionAlongEdge = 0.5f;

    [Header("Ship-only (ignored for anything but an EnemyShip prefab)")]
    [Tooltip("Optional attack route: an ordered list of hand-placed points in the battle scene this ship " +
             "travels to, and how long it pauses at each before continuing. Leave empty for the ship to " +
             "fall back to its own default approach-then-strafe behavior instead — see EnemyShip.")]
    public List<EnemyShipWaypoint> route = new();
}

[System.Serializable]
public class Wave
{
    public List<WaveEnemyEntry> enemies = new();
    [Tooltip("Seconds to wait after this wave's last enemy spawns before the next wave starts.")]
    public float delayAfterWave = 3f;
}

public class EnemySpawner : MonoBehaviour
{
    public ShipGrid targetShip;
    [Tooltip("Falls back to Camera.main if left unset.")]
    public Camera battleCamera;
    [Tooltip("World-space distance beyond the camera's edge enemies spawn at, so they appear just off-screen rather than exactly on its border.")]
    public float spawnMargin = 1f;
    [Tooltip("If on, ignores each entry's own Position Along Edge and rolls a random spot along the " +
             "edge for every individual spawn instead.")]
    public bool randomizePositionAlongEdge = true;

    [Header("Waiting between ships")]
    [Tooltip("On: when no enemy SHIP is left in the battle (all destroyed or disabled) while more ships are still to come, the next ship does " +
             "not wait for its spawn interval or the pause between waves - it appears at once. Meteors always keep their timing.")]
    public bool skipWaitWhenClear = true;
    [Tooltip("The short pause (seconds) kept before that next ship appears. 0 = immediately.")]
    [Min(0f)] public float clearScreenDelay = 0f;

    [Header("Survival (no campaign level chosen)")]
    [Tooltip("The survival mode's ships and timing. Used when the battle is started from the Survival button.")]
    public SurvivalDefinition survival;

    public bool IsSurvival => PendingBattle.Level == null && survival != null;
    public int CurrentWave { get; private set; }
    public int TotalWaves { get; private set; }
    public int WavesLeft => TotalWaves > 0 ? Mathf.Max(0, TotalWaves - CurrentWave + 1) : 0;
    public int SurvivalCoinsEarned { get; private set; }
    public event Action<int> OnSurvivalWaveStarted;
    public event Action<int, int> OnSurvivalWaveCompleted;

    public float WaitRemaining { get; private set; }
    public float WaitDuration { get; private set; }

    public event Action OnAllWavesSpawned;

    private Coroutine wavesRoutine;

    public void StartWaves() => wavesRoutine = StartCoroutine(IsSurvival ? RunSurvival() : RunWaves());
    public void StopWaves() { if (wavesRoutine != null) StopCoroutine(wavesRoutine); }

    private IEnumerator RunWaves()
    {
        var waves = PendingBattle.Level != null ? PendingBattle.Level.waves : null;
        if (waves != null)
        {
            var steps = new List<(WaveEnemyEntry entry, float wait, int wave)>();
            for (int w = 0; w < waves.Count; w++)
            {
                var wave = waves[w];
                foreach (var entry in wave.enemies)
                {
                    for (int i = 0; i < entry.count; i++)
                        steps.Add((entry, Mathf.Max(0f, entry.spawnInterval), w + 1));
                }

                if (w < waves.Count - 1 && steps.Count > 0)
                {
                    var last = steps[steps.Count - 1];
                    steps[steps.Count - 1] = (last.entry, last.wait + Mathf.Max(0f, wave.delayAfterWave), last.wave);
                }
            }

            TotalWaves = waves.Count;
            for (int k = 0; k < steps.Count; k++)
            {
                CurrentWave = steps[k].wave;
                SpawnEnemy(steps[k].entry);

                if (k + 1 >= steps.Count || steps[k].wait <= 0f) continue;

                yield return WaitForNext(steps[k].wait, IsShip(steps[k + 1].entry), skipWaitWhenClear);
            }
        }

        OnAllWavesSpawned?.Invoke();
    }

    private static bool IsShip(WaveEnemyEntry entry) => entry.enemyPrefab != null && entry.enemyPrefab.TryGetComponent<EnemyShip>(out _);

    private IEnumerator WaitForNext(float seconds, bool nextIsShip, bool skipWhenClear)
    {
        WaitDuration = seconds;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            WaitRemaining = seconds - t;
            if (skipWhenClear && nextIsShip && !ShipIdentity.AnyEnemyShipOperational)
            {
                if (clearScreenDelay > 0f) yield return new WaitForSeconds(clearScreenDelay);
                break;
            }

            yield return null;
        }

        WaitRemaining = 0f;
    }

    private IEnumerator RunSurvival()
    {
        var data = GameDataManager.Instance;
        int wave = data != null ? Mathf.Max(1, data.GetSurvivalStartLevel()) : 1;
        SurvivalCoinsEarned = 0;

        if (survival.ShipsForWave(1).Count == 0)
        {
            Debug.LogWarning("EnemySpawner: the Survival definition has no ships.", this);
            yield break;
        }

        while (true)
        {
            CurrentWave = wave;
            if (data != null) data.RecordSurvivalWave(wave);
            OnSurvivalWaveStarted?.Invoke(wave);

            var ships = survival.ShipsForWave(wave);
            float interval = survival.IntervalForWave(wave);
            for (int i = 0; i < ships.Count; i++)
            {
                SpawnEnemy(new WaveEnemyEntry { enemyPrefab = ships[i], count = 1, side = survival.side });
                if (i + 1 < ships.Count) yield return WaitForNext(interval, true, skipWaitWhenClear);
            }

            yield return WaitForNext(survival.wavePause, true, true);

            int coins = survival.CoinsForWave(wave);
            if (coins > 0 && data != null) data.AddCoins(coins);
            SurvivalCoinsEarned += coins;
            OnSurvivalWaveCompleted?.Invoke(wave, coins);
            wave++;
        }
    }

    private void SpawnEnemy(WaveEnemyEntry entry)
    {
        if (entry.enemyPrefab == null || targetShip == null) return;

        if (entry.enemyPrefab.TryGetComponent<Meteor>(out _))
        {
            ComputeMeteorTrajectory(out Vector3 start, out Vector2 direction);
            var meteorObj = Instantiate(entry.enemyPrefab, start, Quaternion.identity);
            var meteor = meteorObj.GetComponent<Meteor>();
            meteor.targetShip = targetShip;
            meteor.Launch(direction);
            return;
        }

        var instance = Instantiate(entry.enemyPrefab, ComputeSpawnPoint(entry), Quaternion.identity);
        if (instance.TryGetComponent<EnemyShip>(out var ship))
        {
            ship.battleCamera = battleCamera;
            if (entry.route.Count > 0) ship.route = entry.route;
        }
    }

    private void ComputeMeteorTrajectory(out Vector3 start, out Vector2 direction)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            start = targetShip.transform.position + Vector3.up * 10f;
            direction = Vector2.down;
            return;
        }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 center = cam.transform.position;

        float topX = center.x + UnityEngine.Random.Range(-halfWidth, halfWidth);
        float bottomX = center.x + UnityEngine.Random.Range(-halfWidth, halfWidth);

        start = new Vector3(topX, center.y + halfHeight + spawnMargin, 0f);
        var end = new Vector2(bottomX, center.y - halfHeight - spawnMargin);
        direction = (end - (Vector2)start).normalized;
    }

    private Vector3 ComputeSpawnPoint(WaveEnemyEntry entry)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) return targetShip.transform.position;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 center = cam.transform.position;
        float t = randomizePositionAlongEdge ? UnityEngine.Random.value : entry.positionAlongEdge;

        return entry.side switch
        {
            SpawnSide.Top => new Vector3(center.x + Mathf.Lerp(-halfWidth, halfWidth, t), center.y + halfHeight + spawnMargin, 0f),
            SpawnSide.Bottom => new Vector3(center.x + Mathf.Lerp(-halfWidth, halfWidth, t), center.y - halfHeight - spawnMargin, 0f),
            SpawnSide.Left => new Vector3(center.x - halfWidth - spawnMargin, center.y + Mathf.Lerp(-halfHeight, halfHeight, t), 0f),
            SpawnSide.Right => new Vector3(center.x + halfWidth + spawnMargin, center.y + Mathf.Lerp(-halfHeight, halfHeight, t), 0f),
            _ => center,
        };
    }
}
