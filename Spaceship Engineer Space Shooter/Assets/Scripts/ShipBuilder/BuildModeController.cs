using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-level state for the build screen: which mode is active (Hull, Armor, or Modules),
/// which block is currently selected. Wire the mode-toggle buttons and the
/// bottom palette to this.
/// </summary>
public class BuildModeController : MonoBehaviour
{
    public ShipGrid grid;
    public GhostBlockController ghost;
    public BuildPaletteUI palette;

    [Header("UI")]
    [Tooltip("Rotate button in the bottom panel. Disabled until a block is tapped/selected.")]
    public Button rotateButton;
    [Tooltip("Optional: the arrow/icon inside the rotate button, spun 90° per press for visual feedback.")]
    public RectTransform rotateButtonIcon;
    [Tooltip("Toggle button: while active, tapping any placed block deletes it.")]
    public Button deleteButton;
    [Tooltip("Button color while delete mode is ON.")]
    public Color deleteActiveColor = new(1f, 0.35f, 0.35f);
    private Color deleteDefaultColor;

    public BuildMode CurrentMode { get; private set; } = BuildMode.Hull;

    private bool blockSelected;

    private void Start()
    {
        rotateButton.onClick.AddListener(RotateSelected);
        deleteButton.onClick.AddListener(ToggleDeleteMode);
        deleteDefaultColor = deleteButton.image.color;
        SetHullBuildMode();
    }

    private void Update()
    {
        // Rotate is only allowed BEFORE dragging starts (requirement: rotate via UI only,
        // and only prior to touching/moving the block on the field).
        rotateButton.interactable = blockSelected && !ghost.IsDragging;
    }

    /// <summary>Call from the "Корпус" tab button.</summary>
    public void SetHullBuildMode()
    {
        BuildMode mode = BuildMode.Hull;
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        grid.ShowGeneralGrid();
        grid.HideModuleGrid(); // the module-placement grid only makes sense in Module mode
        palette.ShowForMode(mode);
    }

    /// <summary>Call from the "Броня" tab button.</summary>
    public void SetArmorBuildMode()
    {
        BuildMode mode = BuildMode.Armor;
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        grid.ShowGeneralGrid();
        grid.HideModuleGrid();
        palette.ShowForMode(mode);
    }

    /// <summary>Call from the "Модули" tab button.</summary>
    public void SetModuleBuildMode()
    {
        BuildMode mode = BuildMode.Modules;
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        grid.HideGeneralGrid(); // modules can only sit on hull cells — replace the general grid, don't stack on it
        grid.ShowModuleGrid();
        palette.ShowForMode(mode);
    }

    /// <summary>Call from a palette button on a plain tap (BlockButtonDragHandle.OnTap) — selects the
    /// block so it can be rotated, but doesn't create anything on the grid yet. The ghost only appears
    /// once the player actually drags the block off the palette button (see BeginGridPlacement).</summary>
    public void SelectBlock(BlockDefinition block)
    {
        SetDeleteMode(false); // selecting a new block always cancels delete mode
        ghost.SelectBlock(block, CurrentMode);
        blockSelected = true;
        // Sync from ghost.RotationSteps rather than resetting to identity — re-selecting the
        // same block (e.g. when a drag-off gesture starts) now keeps its rotation, and the icon
        // needs to reflect that instead of snapping back to 0.
        if (rotateButtonIcon != null)
            rotateButtonIcon.localRotation = Quaternion.Euler(0f, 0f, 90f * ghost.RotationSteps);
    }

    /// <summary>Current rotation of whatever block is selected — read by BuildPaletteUI to keep the palette icon in sync.</summary>
    public int CurrentRotationSteps => ghost.RotationSteps;

    /// <summary>Call from a palette button's BlockButtonDragHandle.OnDragStarted — the finger just left
    /// the button's rect, so the ghost should spawn and start following it onto the grid.</summary>
    public void BeginGridPlacement(Vector2 screenPos) => ghost.BeginGridDrag(screenPos);

    /// <summary>Call from BlockButtonDragHandle.OnDragMoved while the finger keeps moving on the grid.</summary>
    public void UpdateGridPlacement(Vector2 screenPos) => ghost.UpdateGridDrag(screenPos);

    /// <summary>Call from BlockButtonDragHandle.OnDragReleased — shows the confirm popup (or discards
    /// the ghost if the drop cell isn't valid).</summary>
    public void EndGridPlacement(Vector2 screenPos) => ghost.EndGridDrag(screenPos);

    /// <summary>Wired to the rotate button (and the desktop R-key shortcut inside GhostBlockController).</summary>
    public void RotateSelected()
    {
        ghost.RotateGhost();
        if (rotateButtonIcon != null)
            rotateButtonIcon.localRotation = Quaternion.Euler(0f, 0f, 90f * ghost.RotationSteps);
        palette.SetSelectedIconRotation(90f * ghost.RotationSteps);
    }

    /// <summary>Wired to the delete button — toggles delete mode on/off.</summary>
    public void ToggleDeleteMode() => SetDeleteMode(!ghost.IsDeleteModeActive);

    public void SetDeleteMode(bool active)
    {
        ghost.SetDeleteMode(active);
        blockSelected = false; // selecting-a-block state and delete mode are mutually exclusive
        if (active) palette.ClearSelection();
        deleteButton.image.color = active ? deleteActiveColor : deleteDefaultColor;
    }

    /// <summary>
    /// Call when leaving the builder screen entirely (back to main menu). Clears every transient
    /// build state so the player can't keep placing/deleting blocks once they're back in the menu.
    /// </summary>
    public void ResetForExit()
    {
        ghost.StopPlacing();
        SetDeleteMode(false);
    }
}