using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ghost preview shown while the player is placing a block. Driven entirely through
/// EventSystem pointer callbacks (works identically for mouse AND touch, old or new
/// Input System — no manual Input polling, so no InvalidOperationException either).
///
/// SETUP: attach this to a full-screen, transparent UI Image inside your Canvas
/// (Raycast Target = ON, Color alpha = 0), positioned BEHIND the palette/UI panels
/// in the Hierarchy so taps on buttons never reach this catcher.
/// The Canvas needs a GraphicRaycaster, and the scene needs an EventSystem
/// (Unity creates one automatically with the Input System UI Input Module).
///
/// IMPORTANT: uGUI only calls IPointerMoveHandler while the pointer moves WITHOUT a button
/// held (pure hover). The instant a button is held and the pointer moves past a small
/// threshold, the EventSystem switches to IBeginDragHandler/IDragHandler instead — so both
/// must be implemented, or movement while dragging silently stops being reported.
///
/// Flow: pointer down on the field -> ghost jumps under the finger and starts tracking
/// -> drag -> ghost follows -> pointer up -> if the cell is valid, place the block.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public class GhostBlockController : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    public ShipGrid grid;
    public Camera worldCamera;

    [Header("Appearance")]
    public Color validColor = new(0.3f, 0.65f, 1f, 0.5f);
    public Color invalidColor = new(1f, 0.3f, 0.3f, 0.5f);

    private GameObject ghostInstance;
    private Transform ghostVisualRoot; // cached before ShipModule (and its visualRoot ref) gets stripped
    private readonly List<SpriteRenderer> ghostRenderers = new();
    private BlockDefinition currentBlock;
    private BuildMode currentMode;
    private int rotationSteps;
    private bool isPlacing;

    private int activePointerId = -1; // ignores extra fingers while one is already dragging
    private bool lastValid;
    private Vector2Int lastAnchor;
    private List<Vector2Int> lastShape;

    /// <summary>When true, tapping anywhere on the field deletes whatever block is there instead of placing.</summary>
    public bool IsDeleteModeActive { get; private set; }

    /// <summary>Current rotation step (0-3, ×90°) — read by UI to spin the palette icon in sync.</summary>
    public int RotationSteps => rotationSteps;

    /// <summary>True while a finger/mouse button is actively dragging the ghost on the field.</summary>
    public bool IsDragging => activePointerId != -1;

    // ---------- Delete mode ----------

    /// <summary>Wired to the Delete button. Turning it on cancels any pending placement (mutually exclusive).</summary>
    public void SetDeleteMode(bool active)
    {
        IsDeleteModeActive = active;
        if (active) StopPlacing();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsDeleteModeActive) return;

        Vector3 world = worldCamera.ScreenToWorldPoint(eventData.position);
        world.z = 0f;
        grid.TryDeleteAt(world);
    }

    // ---------- Selection (called by BuildModeController / palette) ----------

    /// <summary>Call when the player taps a block in the bottom UI list.</summary>
    public void BeginPlacing(BlockDefinition block, BuildMode mode)
    {
        StopPlacing();
        IsDeleteModeActive = false; // starting a placement always cancels delete mode

        currentBlock = block;
        currentMode = mode;
        rotationSteps = 0;
        isPlacing = true;

        ghostInstance = Instantiate(block.prefab);
        ghostInstance.name = $"Ghost_{block.id}";

        // Cache the visual child BEFORE stripping scripts — ShipModule (which owns the
        // visualRoot reference) is about to be destroyed along with every other MonoBehaviour.
        var moduleComp = ghostInstance.GetComponent<ShipModule>();
        ghostVisualRoot = (moduleComp != null && moduleComp.visualRoot != null)
            ? moduleComp.visualRoot
            : ghostInstance.transform;

        // Strip gameplay behaviour so the ghost never fires/collides/takes damage.
        foreach (var mb in ghostInstance.GetComponentsInChildren<MonoBehaviour>())
            Destroy(mb);
        foreach (var col in ghostInstance.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        ghostRenderers.Clear();
        ghostRenderers.AddRange(ghostInstance.GetComponentsInChildren<SpriteRenderer>());

        // Hidden until the player actually touches/clicks the field — see requirement:
        // "movement only while the screen is being touched".
        ghostInstance.SetActive(false);
    }

    /// <summary>Cancels placement (e.g. player closes the palette, switches build mode, or enters delete mode).</summary>
    public void StopPlacing()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        ghostVisualRoot = null;
        currentBlock = null;
        isPlacing = false;
        activePointerId = -1;
    }

    /// <summary>
    /// Wired to the dedicated Rotate button in the UI. Only works BEFORE the player starts
    /// dragging the block on the field — once a finger/mouse button is down, rotation locks
    /// until they release, so the shape can't change mid-drag.
    /// </summary>
    public void RotateGhost()
    {
        if (!isPlacing || IsDragging) return;
        rotationSteps = (rotationSteps + 1) % 4;
    }

    // ---------- Pointer + drag events (mouse button held on desktop for testing, touch drag on mobile) ----------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isPlacing || IsDeleteModeActive) return;
        activePointerId = eventData.pointerId;
        ghostInstance.SetActive(true);
        UpdateGhost(eventData.position);
    }

    // uGUI treats "button held + moved" as a drag, not a plain pointer move — this is what
    // actually fires while the mouse button (or finger) is held down and moving.
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isPlacing || IsDeleteModeActive) return;
        if (activePointerId == -1 || eventData.pointerId != activePointerId) return;

        UpdateGhost(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPlacing || ghostInstance == null || IsDeleteModeActive) return;
        if (activePointerId == -1 || eventData.pointerId != activePointerId) return;

        UpdateGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isPlacing || IsDeleteModeActive) return;
        if (activePointerId == -1 || eventData.pointerId != activePointerId) return;

        UpdateGhost(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPlacing || IsDeleteModeActive) return;
        if (activePointerId == -1 || eventData.pointerId != activePointerId) return;

        UpdateGhost(eventData.position);

        if (lastValid) Confirm();
        else ghostInstance.SetActive(false); // invalid drop — discard the preview, wait for the next touch

        activePointerId = -1;
    }

    // ---------- Internals ----------

    private void UpdateGhost(Vector2 screenPos)
    {
        Vector3 pointerWorld = worldCamera.ScreenToWorldPoint(screenPos);
        pointerWorld.z = 0f;

        Vector2Int anchor = grid.WorldToGrid(pointerWorld);
        var localShape = BlockDefinition.RotateCells(currentBlock.cells, rotationSteps);

        bool valid = currentMode == BuildMode.Hull
            ? grid.CanPlaceHull(anchor, localShape)
            : grid.CanPlaceModule(anchor, localShape);

        // Root sits exactly on the anchor cell and never rotates — matches how ShipGrid.Place()
        // will actually instantiate the real block, so the preview is a true WYSIWYG match.
        ghostInstance.transform.position = grid.AnchorToWorld(anchor);
        ghostInstance.transform.rotation = Quaternion.identity;

        var (centroidCells, _) = ShipGrid.ComputeLocalFootprint(localShape);
        ghostVisualRoot.localPosition = new Vector3(centroidCells.x * grid.cellSize, centroidCells.y * grid.cellSize, 0f);
        ghostVisualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationSteps);

        Tint(valid ? validColor : invalidColor);

        lastValid = valid;
        lastAnchor = anchor;
        lastShape = localShape;
    }

    private void Confirm()
    {
        var placed = currentMode == BuildMode.Hull
            ? grid.PlaceHull(currentBlock.prefab, lastAnchor, lastShape, rotationSteps)
            : grid.PlaceModule(currentBlock.prefab, lastAnchor, lastShape, rotationSteps);

        if (placed == null) return;

        // Stay in placement mode with the same block selected (fast multi-placement).
        // Call StopPlacing() instead at the end here for one-shot-per-tap placement.
        BeginPlacing(currentBlock, currentMode);
    }

    private void Tint(Color c)
    {
        foreach (var r in ghostRenderers) r.color = c;
    }
}