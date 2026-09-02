using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One-finger drag pans the build camera; two-finger pinch zooms it. Both are disabled while a
/// block is selected/being placed (GhostBlockController.IsPlacingBlock) or delete mode is active —
/// the camera has to stay put while the player is mid-decision on something.
///
/// Polls Touchscreen/Mouse directly every frame instead of using uGUI's OnBeginDrag/OnDrag/OnEndDrag
/// — with real multi-touch, those events aren't guaranteed to fire exactly once per finger (a lost
/// OnEndDrag leaves a phantom finger in a dictionary forever, permanently miscounting how many are
/// down and jamming pan/pinch into the wrong mode). Polling has no persistent add/remove bookkeeping
/// to desync: every frame re-derives "which fingers are down right now" from the input system itself.
///
/// SETUP: attach to the same full-screen UI Image that hosts GhostBlockController (the "input
/// catcher" behind the palette/UI panels) — a finger only counts for camera control if it FIRST
/// touched down inside that element's rect (checked once, when the finger appears), so gestures
/// starting on the palette/buttons/etc. are naturally ignored without needing uGUI raycasts here.
///
/// Panning is clamped so the camera can wander at most PanMarginCells past the ship's own Hull
/// footprint (ShipGrid.HullOnlyCellPositions — the same cells the outline traces), not the full
/// static grid, so the framing always tracks the ship you actually built rather than the board size.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BuildCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public ShipGrid grid;
    public GhostBlockController ghost;

    [Header("Pan")]
    [Tooltip("How many cells past the ship's own hull footprint the camera is allowed to pan.")]
    public float panMarginCells = 3f;

    [Header("Zoom (two-finger pinch)")]
    [Tooltip("Smallest orthographic size reachable by spreading fingers apart (most zoomed in).")]
    public float minOrthographicSize = 2f;
    [Tooltip("Largest orthographic size reachable by pinching fingers together (most zoomed out).")]
    public float maxOrthographicSize = 8f;
    [Tooltip("Orthographic size change per pixel of pinch-distance change.")]
    public float pinchZoomSpeed = 0.01f;

    private RectTransform rectTransform;

    // Ground truth, rebuilt from scratch every frame: id (touchId, or -1 for mouse) -> current
    // screen position, for every pointer currently down that qualifies for camera control.
    private readonly Dictionary<int, Vector2> tracked = new();

    private bool wasPinching;
    private float pinchStartDistance;
    private float pinchStartOrthoSize;

    private bool CanControlCamera => ghost != null && !ghost.IsPlacingBlock && !ghost.IsDeleteModeActive;

    private void Awake() => rectTransform = (RectTransform)transform;

    private void Update()
    {
        var current = GatherQualifyingPointers();

        if (!CanControlCamera || current.Count == 0)
        {
            wasPinching = false;
            SyncTracked(current);
            return;
        }

        if (current.Count >= 2)
        {
            var ids = current.Keys.Take(2).ToList();
            Vector2 a = current[ids[0]];
            Vector2 b = current[ids[1]];
            float distance = Vector2.Distance(a, b);

            if (!wasPinching)
            {
                pinchStartDistance = distance;
                pinchStartOrthoSize = targetCamera.orthographicSize;
                wasPinching = true;
            }
            else
            {
                // Fingers spreading apart (distance grows) -> zoom IN -> smaller orthographic size.
                float newSize = pinchStartOrthoSize - (distance - pinchStartDistance) * pinchZoomSpeed;
                targetCamera.orthographicSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
                MoveCameraClamped(targetCamera.transform.position); // re-clamp: zoom can push the center out of bounds
            }
        }
        else
        {
            wasPinching = false;
            int id = current.Keys.First();

            if (tracked.TryGetValue(id, out var previous))
                Pan(current[id] - previous);
        }

        SyncTracked(current);
    }

    /// <summary>Every pointer currently pressed, restricted to ones that either (a) already qualified
    /// last frame (so an in-progress gesture keeps being tracked even once the finger drifts outside
    /// the rect), or (b) just appeared this frame and started inside our rect.</summary>
    private Dictionary<int, Vector2> GatherQualifyingPointers()
    {
        var result = new Dictionary<int, Vector2>();

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;

                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();
                if (Qualifies(id, pos)) result[id] = pos;
            }
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            const int mouseId = -1;
            Vector2 pos = Mouse.current.position.ReadValue();
            if (Qualifies(mouseId, pos)) result[mouseId] = pos;
        }

        return result;
    }

    private bool Qualifies(int id, Vector2 screenPos)
        => tracked.ContainsKey(id) || RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, targetCamera);

    private void SyncTracked(Dictionary<int, Vector2> current)
    {
        tracked.Clear();
        foreach (var kvp in current) tracked[kvp.Key] = kvp.Value;
    }

    private void Pan(Vector2 screenDelta)
    {
        float worldPerPixel = targetCamera.orthographicSize * 2f / Screen.height;
        Vector3 worldDelta = new(-screenDelta.x * worldPerPixel, -screenDelta.y * worldPerPixel, 0f);
        MoveCameraClamped(targetCamera.transform.position + worldDelta);
    }

    private void MoveCameraClamped(Vector3 desired)
    {
        var (min, max) = GetPanBounds();

        desired.x = min.x <= max.x ? Mathf.Clamp(desired.x, min.x, max.x) : (min.x + max.x) * 0.5f;
        desired.y = min.y <= max.y ? Mathf.Clamp(desired.y, min.y, max.y) : (min.y + max.y) * 0.5f;
        desired.z = targetCamera.transform.position.z; // never touch camera depth

        targetCamera.transform.position = desired;
    }

    /// <summary>World-space min/max the camera's center is allowed to sit at — the ship's hull
    /// bounding box, expanded by panMarginCells on every side. Falls back to a single-cell box at
    /// the board's center if no hull has been placed yet (nothing to frame).</summary>
    private (Vector3 min, Vector3 max) GetPanBounds()
    {
        var cells = grid.HullOnlyCellPositions.ToList();

        Vector2Int minCell, maxCellExclusive;
        if (cells.Count == 0)
        {
            minCell = new Vector2Int(grid.width / 2, grid.height / 2);
            maxCellExclusive = minCell + Vector2Int.one;
        }
        else
        {
            minCell = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
            maxCellExclusive = new Vector2Int(cells.Max(c => c.x) + 1, cells.Max(c => c.y) + 1);
        }

        Vector3 margin = new(panMarginCells * grid.cellSize, panMarginCells * grid.cellSize, 0f);
        Vector3 worldMin = grid.CornerToWorld(minCell) - margin;
        Vector3 worldMax = grid.CornerToWorld(maxCellExclusive) + margin;
        return (worldMin, worldMax);
    }
}
