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
/// Flow: pointer down on the field -> ghost jumps under the finger and starts tracking
/// -> pointer move -> ghost follows -> pointer up -> if the cell is valid, place the block.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public class GhostBlockController : MonoBehaviour, IPointerDownHandler, IPointerMoveHandler, IPointerUpHandler
{
    [Header("References")]
    public ShipGrid grid;
    public Camera worldCamera;

    [Header("Appearance")]
    public Color validColor = new(0.3f, 0.65f, 1f, 0.5f);
    public Color invalidColor = new(1f, 0.3f, 0.3f, 0.5f);

    private GameObject ghostInstance;
    private readonly List<SpriteRenderer> ghostRenderers = new();
    private BlockDefinition currentBlock;
    private BuildMode currentMode;
    private int rotationSteps;
    private bool isPlacing;

    private int activePointerId = -1; // ignores extra fingers while one is already dragging
    private bool lastValid;
    private Vector2Int lastAnchor;
    private List<Vector2Int> lastShape;

    // ---------- Selection (called by BuildModeController / palette) ----------

    /// <summary>Call when the player taps a block in the bottom UI list.</summary>
    public void BeginPlacing(BlockDefinition block, BuildMode mode)
    {
        StopPlacing();

        currentBlock = block;
        currentMode = mode;
        rotationSteps = 0;
        isPlacing = true;

        ghostInstance = Instantiate(block.prefab);
        ghostInstance.name = $"Ghost_{block.id}";

        // Strip gameplay behaviour so the ghost never fires/collides/takes damage.
        foreach (var mb in ghostInstance.GetComponentsInChildren<MonoBehaviour>())
            Destroy(mb);
        foreach (var col in ghostInstance.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        ghostRenderers.Clear();
        ghostRenderers.AddRange(ghostInstance.GetComponentsInChildren<SpriteRenderer>());

        // Park it off-screen until the first pointer event places it under the finger/cursor.
        ghostInstance.SetActive(false);
    }

    /// <summary>Cancels placement (e.g. player closes the palette or switches build mode).</summary>
    public void StopPlacing()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        currentBlock = null;
        isPlacing = false;
        activePointerId = -1;
    }

    /// <summary>Wired to the dedicated Rotate button in the UI (mobile has no right-click).</summary>
    public void RotateGhost()
    {
        if (!isPlacing) return;
        rotationSteps = (rotationSteps + 1) % 4;
    }

    // ---------- Pointer events (mouse hover on desktop, touch drag on mobile) ----------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isPlacing) return;
        activePointerId = eventData.pointerId;
        ghostInstance.SetActive(true);
        UpdateGhost(eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isPlacing || ghostInstance == null) return;
        // On desktop this fires on hover even without a button held — that's fine, we want live preview.
        // On mobile it only fires while a finger is actually down and dragging.
        if (activePointerId != -1 && eventData.pointerId != activePointerId) return;

        if (!ghostInstance.activeSelf) ghostInstance.SetActive(true);
        UpdateGhost(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPlacing) return;
        if (activePointerId != -1 && eventData.pointerId != activePointerId) return;

        UpdateGhost(eventData.position);
        if (lastValid) Confirm();

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

        ghostInstance.transform.position = grid.GridToWorld(anchor, localShape);
        ghostInstance.transform.rotation = Quaternion.Euler(0f, 0f, -90f * rotationSteps);
        Tint(valid ? validColor : invalidColor);

        lastValid = valid;
        lastAnchor = anchor;
        lastShape = localShape;
    }

    private void Confirm()
    {
        var placed = currentMode == BuildMode.Hull
            ? grid.PlaceHull(currentBlock.prefab, lastAnchor, lastShape)
            : grid.PlaceModule(currentBlock.prefab, lastAnchor, lastShape);

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