using System;
using System.Collections;
using TMPro;
using UnityEngine;

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

    public event Action OnBattleStart;

    private void Start()
    {
        if (background != null && PendingBattle.Level != null && PendingBattle.Level.background != null)
        {
            background.sprite = PendingBattle.Level.background;

            if (!background.enabled || !background.gameObject.activeInHierarchy)
                Debug.LogWarning($"Battle background: '{background.name}' is disabled, so the level's background can't be seen. " +
                                 "Assign the SpriteRenderer that actually draws the backdrop to Background.", this);
        }

        if (playerShipGrid != null && database != null)
        {
            if (GameDataManager.Instance == null)
            {
                Debug.LogError("GameDataManager.Instance is null — the battle scene needs it to already " +
                                "exist (DontDestroyOnLoad from the Bootstrap scene). Press Play from " +
                                "Bootstrap/the main menu instead of directly on this scene, or load " +
                                "Bootstrap additively alongside it for standalone testing.");
            }
            else
            {
                GameDataManager.Instance.LoadShip(playerShipGrid, database);
                playerShipGrid.SetViewMode(ShipViewMode.Preview);
                FitCameraToShip();
            }
        }

        StartCoroutine(RunIntro());
    }

    private Camera GetBattleCamera() => battleCamera != null ? battleCamera : Camera.main;

    private void FitCameraToShip()
    {
        var cam = GetBattleCamera();
        if (cam == null || !cam.orthographic) return;

        Vector2 size = playerShipGrid.GetShipWorldSize();
        if (size == Vector2.zero) return;

        float sizeForHeight = size.y * 0.5f + shipFramePadding;
        float sizeForWidth = (size.x * 0.5f + shipFramePadding) / Mathf.Max(0.01f, cam.aspect);
        float required = Mathf.Max(sizeForHeight, sizeForWidth);

        if (required > cam.orthographicSize) cam.orthographicSize = required;
    }

    private Vector3 ComputeShipEndPosition()
    {
        if (playerShipGrid == null) return playerShip != null ? playerShip.position : Vector3.zero;

        Vector3 rootToShipCenter = playerShipGrid.GetShipWorldCenter() - playerShipGrid.transform.position;

        Vector3 targetCenter = shipArrivalPoint != null ? shipArrivalPoint.position
            : GetBattleCamera() != null ? GetBattleCamera().transform.position
            : playerShip.position;
        targetCenter.z = playerShip.position.z;

        return targetCenter - rootToShipCenter;
    }

    private IEnumerator RunIntro()
    {
        yield return StartCoroutine(FlyShipIn());
        yield return StartCoroutine(RunCountdown());

        if (playerShipIdentity != null) playerShipIdentity.SetCombatActive(true);
        if (battleOutcome != null) battleOutcome.BeginTracking();
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
