using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-level state for the build screen: which mode is active (Hull or Modules),
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

    /// <summary>Call from the "Корпус" / "Модули" tab buttons.</summary>
    public void SetHullBuildMode()
    {
        BuildMode mode = BuildMode.Hull;
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        palette.ShowForMode(mode);
    }

    public void SetModuleBuildMode()
    {
        BuildMode mode = BuildMode.Modules;
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        palette.ShowForMode(mode);
    }

    /// <summary>Call from a palette button when the player taps a block to place.</summary>
    public void SelectBlock(BlockDefinition block)
    {
        SetDeleteMode(false); // placing a new block always cancels delete mode
        ghost.BeginPlacing(block, CurrentMode);
        blockSelected = true;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
    }

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