using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Opens a battle: shows this level's background, flies the player's (already-built) ship in from
/// off-screen, counts down 3-2-1, then hands off to whatever spawns enemies (e.g. EnemySpawner).
///
/// Reads which level is being played from PendingBattle, set right before the battle scene loads —
/// see LevelButtonView.OnClicked.
/// </summary>
public class BattleSequenceController : MonoBehaviour
{
    [Header("Background")]
    public SpriteRenderer background;

    [Header("Ship")]
    [Tooltip("Loaded from the save file at Start — the battle scene doesn't come with the ship " +
             "pre-built, unlike the main menu scene which loads it in MainMenuFlowController.")]
    public ShipGrid playerShipGrid;
    public BlockDatabase database;

    [Header("Ship arrival")]
    public Transform playerShip;
    public ShipIdentity playerShipIdentity;
    [Tooltip("Where the ship starts, off-screen.")]
    public Transform shipStartPoint;
    [Tooltip("Where the ship's own built-ship CENTER should end up — not where its root transform " +
             "lands, since the root is rarely the ship's actual visual middle once you account for " +
             "whatever shape was actually built. Falls back to the battle camera's center if unset.")]
    public Transform shipArrivalPoint;
    public float arrivalDuration = 1.5f;

    [Header("Camera fit")]
    [Tooltip("Falls back to Camera.main if left unset.")]
    public Camera battleCamera;
    [Tooltip("Extra world-space breathing room kept around the ship if the camera has to zoom out to " +
             "fit it — only ever zooms OUT from whatever size you set on the camera, never in.")]
    public float shipFramePadding = 1f;

    [Header("Countdown")]
    public TMP_Text countdownLabel;
    public float secondsPerCount = 1f;

    [Header("Enemies")]
    [Tooltip("Started once the arrival + countdown intro finishes — assign it here rather than having " +
             "it start itself on its own Awake/Start, or enemies would appear before the player's ship " +
             "has even arrived.")]
    public EnemySpawner enemySpawner;

    [Header("Outcome")]
    [Tooltip("Starts tracking win/loss at the same moment enemies start spawning — any earlier and the " +
             "starting-HP snapshot would be taken before the ship is even done arriving.")]
    public BattleOutcomeController battleOutcome;
    [Tooltip("Same timing as Battle Outcome — snapshots the HUD gauges' starting HP/armor/shield here too.")]
    public BattleHudGauges hudGauges;

    /// <summary>Fired at the same moment EnemySpawner starts, for anything else that also needs to
    /// know the intro just finished (e.g. a HUD element revealing itself).</summary>
    public event Action OnBattleStart;

    private void Start()
    {
        if (background != null && PendingBattle.Level != null && PendingBattle.Level.background != null)
            background.sprite = PendingBattle.Level.background;

        if (playerShipGrid != null && database != null)
        {
            if (GameDataManager.Instance == null)
            {
                // Most likely cause: Play was pressed directly on this scene instead of from Bootstrap/
                // the main menu, so GameDataManager's own Awake (DontDestroyOnLoad) never ran at all —
                // see GameDataManager's class doc comment.
                Debug.LogError("GameDataManager.Instance is null — the battle scene needs it to already " +
                                "exist (DontDestroyOnLoad from the Bootstrap scene). Press Play from " +
                                "Bootstrap/the main menu instead of directly on this scene, or load " +
                                "Bootstrap additively alongside it for standalone testing.");
            }
            else
            {
                GameDataManager.Instance.LoadShip(playerShipGrid, database);
                playerShipGrid.SetViewMode(ShipViewMode.Preview); // closed look for battle, same as the menu preview
                FitCameraToShip();
            }
        }

        StartCoroutine(RunIntro());
    }

    private Camera GetBattleCamera() => battleCamera != null ? battleCamera : Camera.main;

    /// <summary>Zooms the battle camera out — never in — just enough that the ship's actual built size
    /// fits inside it with padding. A small, hand-built ship never touches the camera's own default
    /// size; a big, hangar-upgraded one won't get clipped off the edges.</summary>
    private void FitCameraToShip()
    {
        var cam = GetBattleCamera();
        if (cam == null || !cam.orthographic) return;

        Vector2 size = playerShipGrid.GetShipWorldSize();
        if (size == Vector2.zero) return; // nothing built yet — nothing to fit

        float sizeForHeight = size.y * 0.5f + shipFramePadding;
        float sizeForWidth = (size.x * 0.5f + shipFramePadding) / Mathf.Max(0.01f, cam.aspect);
        float required = Mathf.Max(sizeForHeight, sizeForWidth);

        if (required > cam.orthographicSize) cam.orthographicSize = required;
    }

    /// <summary>Where the ship's ROOT transform needs to end up so that its own built-ship CENTER (not
    /// the root itself) lands exactly on shipArrivalPoint — see ShipGrid.GetShipWorldCenter for why
    /// those two points usually aren't the same.</summary>
    private Vector3 ComputeShipEndPosition()
    {
        if (playerShipGrid == null) return playerShip != null ? playerShip.position : Vector3.zero;

        Vector3 rootToShipCenter = playerShipGrid.GetShipWorldCenter() - playerShipGrid.transform.position;

        Vector3 targetCenter = shipArrivalPoint != null ? shipArrivalPoint.position
            : GetBattleCamera() != null ? GetBattleCamera().transform.position
            : playerShip.position;
        targetCenter.z = playerShip.position.z; // never touch the ship's own depth

        return targetCenter - rootToShipCenter;
    }

    private IEnumerator RunIntro()
    {
        yield return StartCoroutine(FlyShipIn());
        yield return StartCoroutine(RunCountdown());

        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(true); // weapons stay silent until this fires
        if (battleOutcome != null) battleOutcome.BeginTracking(); // must start before enemies do, so the starting-HP snapshot is undamaged
        if (hudGauges != null) hudGauges.BeginTracking();
        if (enemySpawner != null) enemySpawner.StartWaves();
        OnBattleStart?.Invoke();
    }

    private IEnumerator FlyShipIn()
    {
        if (playerShip == null) yield break;

        Vector3 start = shipStartPoint != null ? shipStartPoint.position : playerShip.position;
        Vector3 end = ComputeShipEndPosition();
        playerShip.position = start;

        for (float t = 0f; t < arrivalDuration; t += Time.deltaTime)
        {
            playerShip.position = Vector3.Lerp(start, end, t / arrivalDuration);
            yield return null;
        }
        playerShip.position = end;
    }

    private IEnumerator RunCountdown()
    {
        if (countdownLabel == null) yield break;

        countdownLabel.gameObject.SetActive(true);
        for (int count = 3; count >= 1; count--)
        {
            countdownLabel.text = count.ToString();
            yield return new WaitForSeconds(secondsPerCount);
        }
        countdownLabel.gameObject.SetActive(false);
    }
}
