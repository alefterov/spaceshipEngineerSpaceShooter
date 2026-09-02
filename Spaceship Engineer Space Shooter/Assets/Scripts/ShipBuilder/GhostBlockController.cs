using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives block placement on the build grid. Two distinct gestures on a palette block button
/// map to two distinct states here:
///  - Tap the button (finger never leaves the button's rect) -> SelectBlock(): the block becomes
///    the "current" block (so the Rotate button can spin it) but nothing is created yet.
///  - Press-and-drag the button, finger leaves its rect -> BeginGridDrag(): a translucent ghost
///    is spawned at whatever rotation was chosen and follows the finger, snapped to the grid,
///    until EndGridDrag() releases it.
///
/// BuildPaletteUI/BlockButtonDragHandle (on the button prefab) are what actually receive those
/// pointer events and call into this class — see BlockButtonDragHandle.cs for why the drag has
/// to start on the button itself rather than on this component.
///
/// Per-cell valid/invalid feedback is drawn by ShipGrid.ShowPlacementPreview (colored squares
/// under the footprint), not by tinting the ghost — the ghost itself just stays translucent.
///
/// SETUP: attach this to a full-screen, transparent UI Image inside your Canvas
/// (Raycast Target = ON, Color alpha = 0), positioned BEHIND the palette/UI panels
/// in the Hierarchy so taps on buttons never reach this catcher. It's only used for
/// delete-mode taps now (OnPointerClick) — placement dragging is driven externally.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public class GhostBlockController : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public ShipGrid grid;
    public Camera worldCamera;
    [Tooltip("Checkmark/cross confirmation popup shown when the player releases a drag over a valid cell. " +
             "If left unassigned, placement is confirmed immediately on release instead (useful while the " +
             "popup UI isn't built yet).")]
    public PlacementConfirmPopup confirmPopup;

    [Header("Appearance")]
    [Tooltip("Constant transparency of the dragged block itself — validity is shown by the grid cell highlight, not by tinting the ghost.")]
    [Range(0f, 1f)] public float ghostAlpha = 0.6f;
    public Color validColor = new(0.3f, 0.65f, 1f, 0.6f);
    public Color invalidColor = new(1f, 0.3f, 0.3f, 0.6f);

    [Header("Economy")]
    [Tooltip("Fraction of a block's build cost refunded when it's dismantled (0.8 = 80%).")]
    [Range(0f, 1f)] public float dismantleRefundFraction = 0.8f;

    private GameObject ghostInstance;
    private Transform ghostVisualRoot; // cached before ShipModule (and its visualRoot ref) gets stripped
    private readonly List<SpriteRenderer> ghostRenderers = new();
    private BlockDefinition currentBlock;
    private BuildMode currentMode;
    private int rotationSteps;

    private bool isSelected;      // a palette block is chosen (rotate is allowed), no ghost required
    private bool draggingOnGrid;  // finger is outside the palette button and actively moving the ghost

    private bool lastValid;
    private bool lastAffordable; // separate from lastValid so EndGridDrag can tell WHY it's invalid
    private Vector2Int lastAnchor;
    private List<Vector2Int> lastShape;

    /// <summary>When true, tapping anywhere on the field deletes whatever block is there instead of placing.</summary>
    public bool IsDeleteModeActive { get; private set; }

    /// <summary>Current rotation step (0-3, ×90°) — read by UI to spin the palette icon in sync.</summary>
    public int RotationSteps => rotationSteps;

    /// <summary>True only while a ghost is actively being dragged on the grid (not just selected in the palette).</summary>
    public bool IsDragging => draggingOnGrid;

    /// <summary>True from the moment a block is selected (tap in the palette) all the way through
    /// dragging and any pending confirm popup — i.e. whenever a placement is "in flight" and the
    /// camera shouldn't move out from under the player. False again once confirmed, cancelled, or
    /// deselected.</summary>
    public bool IsPlacingBlock => isSelected;

    // ---------- Delete mode ----------

    /// <summary>Wired to the Delete button. Turning it on cancels any pending selection/placement (mutually
    /// exclusive); toggling it either way also cancels a pending delete confirmation, so a stale popup
    /// can't outlive delete mode being switched off.</summary>
    public void SetDeleteMode(bool active)
    {
        IsDeleteModeActive = active;
        confirmPopup?.Hide();
        if (active) StopPlacing();
    }

    /// <summary>
    /// Tapping a cell in delete mode never deletes immediately — it finds whatever's there (a module
    /// takes priority over the hull underneath it, same as ShipGrid.FindDeletableAt/TryDeleteAt) and
    /// shows the same confirm/cancel popup used for placement. A cell with both only ever loses the
    /// module on this tap; a second, separate tap (and its own confirmation) is needed to then delete
    /// the hull piece once the module is gone.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsDeleteModeActive) return;

        Vector3 world = worldCamera.ScreenToWorldPoint(eventData.position);
        world.z = 0f;

        var target = grid.FindDeletableAt(world);
        if (target == null) return;

        if (confirmPopup != null)
            confirmPopup.Show(eventData.position, () => ConfirmDelete(target), () => { });
        else
            ConfirmDelete(target); // no popup wired up yet — fall back to deleting immediately
    }

    /// <summary>Refunds a fraction of the block's original build cost, then actually removes it.</summary>
    private void ConfirmDelete(ShipModule target)
    {
        int refund = Mathf.RoundToInt(target.buildCost * dismantleRefundFraction);
        if (refund > 0) GameDataManager.Instance.AddCredits(refund);

        grid.DeleteBlock(target);
    }

    // ---------- Selection (a tap on a palette button — see BlockButtonDragHandle.OnTap) ----------

    /// <summary>
    /// Call when the player taps a block in the bottom UI list without dragging it out. Also
    /// called again the moment a drag-off gesture starts (see BuildPaletteUI.CreateButton /
    /// OnDragStarted) to make sure the right button is highlighted — so re-selecting the SAME
    /// block keeps whatever rotation was already dialed in; only picking a genuinely different
    /// block resets it to 0.
    /// </summary>
    public void SelectBlock(BlockDefinition block, BuildMode mode)
    {
        bool sameBlock = isSelected && currentBlock == block && currentMode == mode;
        int keepRotation = sameBlock ? rotationSteps : 0;

        StopPlacing();
        IsDeleteModeActive = false; // selecting a block always cancels delete mode

        currentBlock = block;
        currentMode = mode;
        rotationSteps = keepRotation;
        isSelected = true;
    }

    /// <summary>Cancels selection and/or an in-progress drag (palette closed, mode switched, delete mode entered, screen exited).</summary>
    public void StopPlacing()
    {
        confirmPopup?.Hide();
        DestroyGhost();
        grid.ClearPlacementPreview();

        currentBlock = null;
        isSelected = false;
        draggingOnGrid = false;
    }

    /// <summary>
    /// Wired to the dedicated Rotate button in the UI. Only works while a block is selected but
    /// NOT currently being dragged on the grid — once the ghost exists and is following the
    /// finger, rotation locks until the drag ends, so the footprint can't change mid-drag.
    /// </summary>
    public void RotateGhost()
    {
        if (!isSelected || draggingOnGrid) return;
        rotationSteps = (rotationSteps + 1) % 4;
    }

    // ---------- Drag-to-place (driven by BlockButtonDragHandle on the palette button) ----------

    /// <summary>Finger left the palette button's rect while held down — spawn the ghost and start tracking it.</summary>
    public void BeginGridDrag(Vector2 screenPos)
    {
        if (!isSelected || IsDeleteModeActive) return;

        confirmPopup?.Hide();
        DestroyGhost();
        SpawnGhost();
        draggingOnGrid = true;
        UpdateGhost(screenPos);
    }

    /// <summary>Finger keeps moving with the ghost already spawned.</summary>
    public void UpdateGridDrag(Vector2 screenPos)
    {
        if (!draggingOnGrid) return;
        UpdateGhost(screenPos);
    }

    /// <summary>
    /// Finger released. Over a valid cell: show the confirm/cancel popup and wait for the player's
    /// decision. Over an invalid cell: there's nothing to confirm, so just discard the ghost.
    /// </summary>
    public void EndGridDrag(Vector2 screenPos)
    {
        if (!draggingOnGrid) return;

        UpdateGhost(screenPos);
        draggingOnGrid = false;

        if (!lastValid)
        {
            // Specifically failed on cost (as opposed to geometry) — this is the moment the player
            // actually "discovers" they can't afford it, so it's what drives the credits display's
            // insufficient-funds pulse, not GameDataManager.SpendCredits (which never even runs here).
            if (!lastAffordable) GameDataManager.Instance.NotifyInsufficientCredits();

            DestroyGhost();
            grid.ClearPlacementPreview();
            return;
        }

        if (confirmPopup != null)
            confirmPopup.Show(screenPos, ConfirmPlacement, CancelPlacement);
        else
            ConfirmPlacement(); // no popup wired up yet — fall back to placing immediately
    }

    private void ConfirmPlacement()
    {
        // Re-checked here rather than trusted from the drag-time validity check — credits could in
        // principle have changed between then and now. Spending happens before placing: if it fails
        // (shouldn't, given the drag already required affordability), treat it like an invalid drop.
        if (!GameDataManager.Instance.SpendCredits(currentBlock.buildCost))
        {
            CancelPlacement();
            return;
        }

        // Hull and Armor are both structural — same hullCells layer, same adjacency rule.
        // Only Modules sits on the separate module layer.
        if (currentMode == BuildMode.Modules)
            grid.PlaceModule(currentBlock, lastAnchor, lastShape, rotationSteps);
        else
            grid.PlaceHull(currentBlock, lastAnchor, lastShape, rotationSteps);

        DestroyGhost();
        grid.ClearPlacementPreview();
        // currentBlock/rotationSteps stay put — isSelected remains true so the same block can be
        // dragged out again immediately for fast multi-placement.
    }

    private void CancelPlacement()
    {
        DestroyGhost();
        grid.ClearPlacementPreview();
    }

    // ---------- Internals ----------

    private void SpawnGhost()
    {
        ghostInstance = Instantiate(currentBlock.prefab);
        ghostInstance.name = $"Ghost_{currentBlock.id}";

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
        foreach (var r in ghostRenderers) r.color = new Color(1f, 1f, 1f, ghostAlpha);
    }

    private void DestroyGhost()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        ghostInstance = null;
        ghostVisualRoot = null;
    }

    private void UpdateGhost(Vector2 screenPos)
    {
        Vector3 pointerWorld = worldCamera.ScreenToWorldPoint(screenPos);
        pointerWorld.z = 0f;

        Vector2Int anchor = grid.WorldToGrid(pointerWorld);
        var localShape = BlockDefinition.RotateCells(currentBlock.cells, rotationSteps);

        bool geometryValid = currentMode == BuildMode.Modules
            ? grid.CanPlaceModule(anchor, localShape)
            : grid.CanPlaceHull(anchor, localShape); // Hull and Armor share the same structural layer/rule

        // Affordability is part of validity too — can't afford it reads the same as "can't place it
        // here", cell goes red right along with any geometric reason. Tracked separately as well
        // (lastAffordable) so EndGridDrag can tell specifically WHY a drop failed.
        bool affordable = GameDataManager.Instance.Credits >= currentBlock.buildCost;
        bool valid = geometryValid && affordable;

        // Root sits exactly on the anchor cell and never rotates — matches how ShipGrid.Place()
        // will actually instantiate the real block, so the preview is a true WYSIWYG match.
        ghostInstance.transform.position = grid.AnchorToWorld(anchor);
        ghostInstance.transform.rotation = Quaternion.identity;

        var (centroidCells, _) = ShipGrid.ComputeLocalFootprint(localShape);
        ghostVisualRoot.localPosition = new Vector3(centroidCells.x * grid.cellSize, centroidCells.y * grid.cellSize, 0f);
        ghostVisualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationSteps);

        // Cell-by-cell blue/red validity feedback lives on the grid itself, not on the ghost.
        var absoluteCells = grid.GetOccupiedCells(anchor, localShape);
        grid.ShowPlacementPreview(absoluteCells, valid ? validColor : invalidColor);

        lastValid = valid;
        lastAffordable = affordable;
        lastAnchor = anchor;
        lastShape = localShape;
    }
}
