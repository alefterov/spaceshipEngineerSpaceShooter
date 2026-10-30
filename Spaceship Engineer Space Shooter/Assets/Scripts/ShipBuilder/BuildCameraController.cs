using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// One-finger drag pans the build camera; two-finger pinch zooms it. Both are disabled while a block
/// is actively being dragged onto the grid (GhostBlockController.IsDragging) or delete mode is active
/// — the camera has to stay put while the player's finger is mid-gesture on the grid itself.
///
/// Polls Touchscreen/Mouse directly every frame instead of using uGUI's OnBeginDrag/OnDrag/OnEndDrag
/// — with real multi-touch, those events aren't guaranteed to fire exactly once per finger (a lost
/// OnEndDrag leaves a phantom finger in a dictionary forever, permanently miscounting how many are
/// down and jamming pan/pinch into the wrong mode). Polling has no persistent add/remove bookkeeping
/// to desync: every frame re-derives "which fingers are down right now" from the input system itself.
///
/// SETUP: attach to the same full-screen UI Image that hosts GhostBlockController (the "input
/// catcher" behind the palette/UI panels) — a finger only counts for camera control if it FIRST
/// touched down on that element itself (checked once, when the finger appears): inside its rect AND with no other UI on top of it at
/// that point. So a swipe starting on the builder panel, the palette, buttons etc. never moves the ship — only one starting on the
/// open area where the ship is.
///
/// Panning is clamped so the camera can wander at most PanMarginCells past the ship's own
/// footprint (ShipGrid.OccupiedCellPositions — the same cells the outline traces), not the full
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
    [Tooltip("How many cells past the ship's own footprint the camera is allowed to pan.")]
    public float panMarginCells = 3f;

    [Header("Zoom (two-finger pinch)")]
    [Tooltip("Smallest orthographic size reachable by spreading fingers apart (most zoomed in).")]
    public float minOrthographicSize = 2f;
    [Tooltip("Largest orthographic size reachable by pinching fingers together (most zoomed out).")]
    public float maxOrthographicSize = 8f;
    [Tooltip("Orthographic size change per pixel of pinch-distance change.")]
    public float pinchZoomSpeed = 0.01f;

    [Header("Frame ship (menu preview)")]
    [Tooltip("Empty space left around the ship, in cells, when FrameShip() fits it to the screen.")]
    public float framePaddingCells = 1f;

    private RectTransform rectTransform;

    // Ground truth, rebuilt from scratch every frame: id (touchId, or -1 for mouse) -> current
    // screen position, for every pointer currently down that qualifies for camera control.
    private readonly Dictionary<int, Vector2> tracked = new();
    // Fingers that went down on some other UI (a panel, a button): ignored until lifted, even if they slide onto the ship's area.
    private readonly HashSet<int> rejected = new();
    private readonly List<RaycastResult> uiHits = new();
    private PointerEventData pointerData;

    private bool wasPinching;
    private float pinchStartDistance;
    private float pinchStartOrthoSize;

    private bool CanControlCamera => ghost != null && !ghost.IsDragging && !ghost.IsDeleteModeActive;

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
        var pressed = new HashSet<int>();

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;

                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();
                pressed.Add(id);
                if (Qualifies(id, pos)) result[id] = pos;
            }
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            const int mouseId = -1;
            Vector2 pos = Mouse.current.position.ReadValue();
            pressed.Add(mouseId);
            if (Qualifies(mouseId, pos)) result[mouseId] = pos;
        }

        rejected.RemoveWhere(id => !pressed.Contains(id)); // lifted: the next touch with that id is judged afresh
        return result;
    }

    private bool Qualifies(int id, Vector2 screenPos)
    {
        if (tracked.ContainsKey(id)) return true;
        if (rejected.Contains(id)) return false;

        if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, targetCamera) && !IsOtherUIOnTop(screenPos)) return true;

        rejected.Add(id);
        return false;
    }

    /// <summary>Whether the topmost UI element under this screen point is something other than this input catcher (or its children) —
    /// a panel, a button, the palette.</summary>
    private bool IsOtherUIOnTop(Vector2 screenPos)
    {
        var events = EventSystem.current;
        if (events == null) return false;

        pointerData ??= new PointerEventData(events);
        pointerData.position = screenPos;
        uiHits.Clear();
        events.RaycastAll(pointerData, uiHits); // sorted topmost first

        return uiHits.Count > 0 && !uiHits[0].gameObject.transform.IsChildOf(transform);
    }

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

    /// <summary>Min/max occupied-cell coordinates (corner space, max exclusive) — the ship's own footprint,
    /// with no padding. Falls back to a single cell at the board's center if nothing is built yet.</summary>
    private (Vector2Int min, Vector2Int maxExclusive) GetShipCellBounds()
    {
        if (grid.TryGetShipCellBounds(out var min, out var maxExclusive)) return (min, maxExclusive);

        var center = new Vector2Int(grid.GridWidth / 2, grid.GridHeight / 2);
        return (center, center + Vector2Int.one);
    }

    /// <summary>World-space min/max the camera's center is allowed to sit at — the ship's
    /// bounding box, expanded by panMarginCells on every side.</summary>
    private (Vector3 min, Vector3 max) GetPanBounds()
    {
        var (minCell, maxCellExclusive) = GetShipCellBounds();

        Vector3 margin = new(panMarginCells * grid.cellSize, panMarginCells * grid.cellSize, 0f);
        Vector3 worldMin = grid.CornerToWorld(minCell) - margin;
        Vector3 worldMax = grid.CornerToWorld(maxCellExclusive) + margin;
        return (worldMin, worldMax);
    }

    /// <summary>Centers the camera on the ship and zooms to fit the whole ship on screen (plus
    /// framePaddingCells of breathing room). Call whenever entering the menu preview — including right
    /// after leaving the builder, since exiting always routes through that same preview transition
    /// (MainMenuFlowController.ShowMainMenu).</summary>
    public void FrameShip()
    {
        var (minCell, maxCellExclusive) = GetShipCellBounds();
        Vector3 worldMin = grid.CornerToWorld(minCell);
        Vector3 worldMax = grid.CornerToWorld(maxCellExclusive);

        Vector3 center = (worldMin + worldMax) * 0.5f;
        center.z = targetCamera.transform.position.z; // never touch camera depth
        targetCamera.transform.position = center;

        float padding = framePaddingCells * 2f * grid.cellSize;
        float width = (worldMax.x - worldMin.x) + padding;
        float height = (worldMax.y - worldMin.y) + padding;

        float sizeForHeight = height * 0.5f;
        float sizeForWidth = width * 0.5f / Mathf.Max(0.01f, targetCamera.aspect);
        float requiredSize = Mathf.Max(sizeForHeight, sizeForWidth);

        targetCamera.orthographicSize = Mathf.Clamp(requiredSize, minOrthographicSize, maxOrthographicSize);
    }
}
