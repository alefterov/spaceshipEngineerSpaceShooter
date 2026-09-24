using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-level state for the build screen: which palette tab is active (Cockpit, Generators, Engines,
/// Armor, Weapons), which block is currently selected. Wire the tab buttons and the bottom palette to
/// this. Armor has Armor/Shields sub-tabs and Weapons has one sub-tab per weapon family. The tab only
/// decides which blocks the palette lists — every block is placed on the same grid with the same rules
/// (see ShipGrid.CanPlace).
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

    [Tooltip("Row of Armor / Shields sub-tab buttons — shown only while the Armor tab is active.")]
    public GameObject armorSubCategoryPanel;
    [Tooltip("Row of Ballistic / Laser / Missile / Plasma sub-tab buttons — shown only while the Weapons tab is active.")]
    public GameObject weaponSubCategoryPanel;

    public BuildMode CurrentMode { get; private set; } = BuildMode.Cockpit;

    private bool blockSelected;

    private void Start()
    {
        rotateButton.onClick.AddListener(RotateSelected);
        deleteButton.onClick.AddListener(ToggleDeleteMode);
        deleteDefaultColor = deleteButton.image.color;

        // Deliberately NOT calling SetCockpitBuildMode() here. MainMenuFlowController.OnBuildShipPressed
        // already calls it explicitly every time the builder opens (including the first time) — if
        // this component lives under builderScreen (inactive at scene load), Start() is deferred by
        // Unity until later in the SAME frame builderScreen.SetActive(true) runs, which made this
        // fire a SECOND time back-to-back with that explicit call on the very first entry, tearing
        // down and rebuilding the palette buttons twice in one frame right as the player could start
        // interacting with them.
    }

    private void Update()
    {
        // Rotate is only allowed BEFORE dragging starts (requirement: rotate via UI only,
        // and only prior to touching/moving the block on the field).
        rotateButton.interactable = blockSelected && !ghost.IsDragging;
    }

    /// <summary>Call from the "Кокпит" tab button.</summary>
    public void SetCockpitBuildMode() => EnterMode(BuildMode.Cockpit);

    /// <summary>Call from the "Генераторы" tab button.</summary>
    public void SetGeneratorBuildMode() => EnterMode(BuildMode.Generators);

    /// <summary>Call from the "Двигатели" tab button.</summary>
    public void SetEngineBuildMode() => EnterMode(BuildMode.Engines);

    /// <summary>Call from the "Броня" tab button. Lists physical armor; the Shields sub-tab switches to shields.</summary>
    public void SetArmorBuildMode() => EnterMode(BuildMode.Armor);

    /// <summary>Call from the "Оружие" tab button. Lists every weapon; a sub-tab narrows it to one family.</summary>
    public void SetWeaponBuildMode() => EnterMode(BuildMode.Weapons);

    private void EnterMode(BuildMode mode)
    {
        CurrentMode = mode;
        ghost.StopPlacing();
        blockSelected = false;
        if (rotateButtonIcon != null) rotateButtonIcon.localRotation = Quaternion.identity;
        SetDeleteMode(false);
        grid.ShowGeneralGrid();
        if (armorSubCategoryPanel != null) armorSubCategoryPanel.SetActive(mode == BuildMode.Armor);
        if (weaponSubCategoryPanel != null) weaponSubCategoryPanel.SetActive(mode == BuildMode.Weapons);
        palette.ShowForMode(mode);
    }

    // ---------- Sub-tabs ----------
    // Each ensures its parent tab is active first (so clicking a sub-tab works even if the player
    // hasn't pressed the parent tab yet), then narrows the palette.

    public void ShowPhysicalArmor() { EnsureMode(BuildMode.Armor); palette.ShowForCategory(BlockCategory.Armor); }
    public void ShowShields() { EnsureMode(BuildMode.Armor); palette.ShowForCategory(BlockCategory.Shield); }

    public void ShowBallisticWeapons() => ShowWeaponClass(WeaponClass.Ballistic);
    public void ShowLaserWeapons() => ShowWeaponClass(WeaponClass.Laser);
    public void ShowMissileWeapons() => ShowWeaponClass(WeaponClass.Missile);
    public void ShowPlasmaWeapons() => ShowWeaponClass(WeaponClass.Plasma);

    private void ShowWeaponClass(WeaponClass weaponClass)
    {
        EnsureMode(BuildMode.Weapons);
        palette.ShowForWeaponClass(weaponClass);
    }

    private void EnsureMode(BuildMode mode)
    {
        if (CurrentMode != mode) EnterMode(mode);
    }

    /// <summary>Call from a palette button on a plain tap (BlockButtonDragHandle.OnTap) — selects the
    /// block so it can be rotated, but doesn't create anything on the grid yet. The ghost only appears
    /// once the player actually drags the block off the palette button (see BeginGridPlacement).</summary>
    public void SelectBlock(BlockDefinition block)
    {
        SetDeleteMode(false); // selecting a new block always cancels delete mode
        ghost.SelectBlock(block);
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
        if (armorSubCategoryPanel != null) armorSubCategoryPanel.SetActive(false);
        if (weaponSubCategoryPanel != null) weaponSubCategoryPanel.SetActive(false);
    }
}
