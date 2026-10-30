using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which edge of the camera's current view an entry spawns just outside of.</summary>
public enum SpawnSide { Top, Bottom, Left, Right }

/// <summary>
/// One kind of enemy within a wave — how many, how far apart, and from which side.
/// </summary>
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

/// <summary>One wave: every entry's enemies spawn in list order, then the wave pauses before the next.</summary>
[System.Serializable]
public class Wave
{
    public List<WaveEnemyEntry> enemies = new();
    [Tooltip("Seconds to wait after this wave's last enemy spawns before the next wave starts.")]
    public float delayAfterWave = 3f;
}

/// <summary>
/// Spawns enemies/hazards (meteors for now) in waves. Waves are configured PER LEVEL (see
/// LevelDefinition.waves), not on this component — this just reads whichever level PendingBattle.Level
/// points at, so every level plays out differently without touching the battle scene itself.
///
/// Spawn positions are computed from the CAMERA's current view (orthographic size + aspect), not
/// fixed scene Transforms — so "just off the top edge" stays correct regardless of screen size/aspect
/// ratio or however far BattleSequenceController.FitCameraToShip zoomed out to fit a big ship.
///
/// Deliberately simple for this first pass beyond that: entries within a wave spawn strictly in list
/// order, one edge per entry. Multi-side simultaneous patterns / per-wave formations are meant to grow
/// from here once there's more than one enemy type to actually need it for.
///
/// Meteors are the exception to the per-entry side: each flies a random top-edge-to-bottom-edge line.
///
/// Wire BattleSequenceController.OnBattleStart to StartWaves() so nothing spawns before the
/// arrival/countdown intro finishes.
/// </summary>
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

    /// <summary>Fired once every wave has finished spawning everything it has. Doesn't by itself mean
    /// the battle is won — BattleOutcomeController still waits for the last spawned enemy to actually
    /// be cleared (Targetable.Active) before declaring victory.</summary>
    public event Action OnAllWavesSpawned;

    private Coroutine wavesRoutine;

    public void StartWaves() => wavesRoutine = StartCoroutine(RunWaves());
    public void StopWaves() { if (wavesRoutine != null) StopCoroutine(wavesRoutine); }

    private IEnumerator RunWaves()
    {
        var waves = PendingBattle.Level != null ? PendingBattle.Level.waves : null;
        if (waves != null)
        {
            // Every spawn in order, with the time to wait after it: the entry's interval, plus the pause between waves after a wave's last spawn.
            var steps = new List<(WaveEnemyEntry entry, float wait)>();
            for (int w = 0; w < waves.Count; w++)
            {
                var wave = waves[w];
                foreach (var entry in wave.enemies)
                {
                    for (int i = 0; i < entry.count; i++)
                        steps.Add((entry, Mathf.Max(0f, entry.spawnInterval)));
                }

                if (w < waves.Count - 1 && steps.Count > 0)
                {
                    var last = steps[steps.Count - 1];
                    steps[steps.Count - 1] = (last.entry, last.wait + Mathf.Max(0f, wave.delayAfterWave));
                }
            }

            for (int k = 0; k < steps.Count; k++)
            {
                SpawnEnemy(steps[k].entry);

                // Nothing more to spawn: nothing to wait for - the end-of-battle check starts right away.
                if (k + 1 >= steps.Count || steps[k].wait <= 0f) continue;

                yield return WaitForNext(steps[k].wait, IsShip(steps[k + 1].entry));
            }
        }

        OnAllWavesSpawned?.Invoke();
    }

    private static bool IsShip(WaveEnemyEntry entry) => entry.enemyPrefab != null && entry.enemyPrefab.TryGetComponent<EnemyShip>(out _);

    /// <summary>Waits `seconds` - but if the next spawn is a ship and no enemy ship is left in the battle, stops waiting at once.</summary>
    private IEnumerator WaitForNext(float seconds, bool nextIsShip)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (skipWaitWhenClear && nextIsShip && !ShipIdentity.AnyEnemyShipOperational)
            {
                if (clearScreenDelay > 0f) yield return new WaitForSeconds(clearScreenDelay);
                yield break;
            }

            yield return null;
        }
    }

    private void SpawnEnemy(WaveEnemyEntry entry)
    {
        if (entry.enemyPrefab == null || targetShip == null) return;

        // Meteors ignore the entry's side/position: each flies a straight line from a random point on the
        // top edge of the screen to a random point on the bottom edge.
        if (entry.enemyPrefab.TryGetComponent<Meteor>(out _))
        {
            ComputeMeteorTrajectory(out Vector3 start, out Vector2 direction);
            var meteorObj = Instantiate(entry.enemyPrefab, start, Quaternion.identity);
            var meteor = meteorObj.GetComponent<Meteor>();
            meteor.targetShip = targetShip;
            meteor.Launch(direction);
            return;
        }

        // Only other enemy types would go here — add more type checks as new ones join.
        var instance = Instantiate(entry.enemyPrefab, ComputeSpawnPoint(entry), Quaternion.identity);
        if (instance.TryGetComponent<EnemyShip>(out var ship))
        {
            ship.battleCamera = battleCamera;
            if (entry.route.Count > 0) ship.route = entry.route; // otherwise keep whatever the prefab itself was authored with, if any
        }
    }

    /// <summary>A random point just above the top edge of the camera's view and a random point just
    /// below its bottom edge; the meteor flies the straight line between them. Both points are chosen
    /// independently across the FULL width, so trajectories run from vertical to steeply diagonal.</summary>
    private void ComputeMeteorTrajectory(out Vector3 start, out Vector2 direction)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            start = targetShip.transform.position + Vector3.up * 10f; // no camera to measure against — still fly downward
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
        // No camera to measure against — spawning at the ship is at least visible, unlike nowhere.
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
