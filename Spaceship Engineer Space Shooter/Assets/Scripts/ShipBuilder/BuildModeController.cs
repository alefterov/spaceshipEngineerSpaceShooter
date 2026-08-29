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

    public BuildMode CurrentMode { get; private set; } = BuildMode.Hull;

    private void Start()
    {
        rotateButton.onClick.AddListener(RotateSelected);
        SetHullBildMode();
    }

    /// <summary>Call from the "Корпус" / "Модули" tab buttons.</summary>
    public void SetHullBildMode()
    {
        BuildMode mode = BuildMode.Hull;
        CurrentMode = mode;
        ghost.StopPlacing();
        rotateButton.interactable = false;
        palette.ShowForMode(mode);
    }
    public void SetModuleBildMode()
    {
        BuildMode mode = BuildMode.Modules;
        CurrentMode = mode;
        ghost.StopPlacing();
        rotateButton.interactable = false;
        palette.ShowForMode(mode);
    }


    /// <summary>Call from a palette button when the player taps a block to place.</summary>
    public void SelectBlock(BlockDefinition block)
    {
        ghost.BeginPlacing(block, CurrentMode);
        rotateButton.interactable = true;
    }

    /// <summary>Wired to the rotate button — same effect as the desktop right-click shortcut.</summary>
    public void RotateSelected() => ghost.RotateGhost();
}
