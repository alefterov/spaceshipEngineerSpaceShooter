using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("The picture on the delete button — it turns red as well while delete mode is ON. Empty = the first child Image of the button.")]
    public Graphic deleteIcon;
    [Tooltip("Icon colour while delete mode is ON.")]
    public Color deleteIconActiveColor = new(1f, 0.35f, 0.35f);
    private Color deleteIconDefaultColor = Color.white;

    [Tooltip("Row of Armor / Shields sub-tab buttons — shown only while the Armor tab is active.")]
    public GameObject armorSubCategoryPanel;
    [Tooltip("Row of Ballistic / Laser / Missile / Plasma sub-tab buttons — shown only while the Weapons tab is active.")]
    public GameObject weaponSubCategoryPanel;

    public BuildMode CurrentMode { get; private set; } = BuildMode.Cockpit;

    public BlockCategory ArmorSubTab { get; private set; } = BlockCategory.Armor;
    public WeaponClass? WeaponFilter { get; private set; }

    private bool blockSelected;

    private void Start()
    {
        rotateButton.onClick.AddListener(RotateSelected);
        deleteButton.onClick.AddListener(ToggleDeleteMode);
        deleteDefaultColor = deleteButton.image.color;

        if (deleteIcon == null)
        {
            foreach (var graphic in deleteButton.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.gameObject == deleteButton.gameObject) continue;
                deleteIcon = graphic;
                break;
            }
        }

        if (deleteIcon != null) deleteIconDefaultColor = deleteIcon.color;
    }

    private void Update()
    {
        rotateButton.interactable = blockSelected && !ghost.IsDragging;
    }

    public void SetCockpitBuildMode() => EnterMode(BuildMode.Cockpit);

    public void SetGeneratorBuildMode() => EnterMode(BuildMode.Generators);

    public void SetEngineBuildMode() => EnterMode(BuildMode.Engines);

    public void SetRepairBuildMode() => EnterMode(BuildMode.Repair);

    public void SetArmorBuildMode() => EnterMode(BuildMode.Armor);

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
        ArmorSubTab = BlockCategory.Armor;
        WeaponFilter = null;
    }

    public void ShowPhysicalArmor() => ShowArmorSubTab(BlockCategory.Armor);
    public void ShowShields() => ShowArmorSubTab(BlockCategory.Shield);

    private void ShowArmorSubTab(BlockCategory category)
    {
        EnsureMode(BuildMode.Armor);
        palette.ShowForCategory(category);
        ArmorSubTab = category;
    }

    public void ShowBallisticWeapons() => ShowWeaponClass(WeaponClass.Ballistic);
    public void ShowLaserWeapons() => ShowWeaponClass(WeaponClass.Laser);
    public void ShowMissileWeapons() => ShowWeaponClass(WeaponClass.Missile);
    public void ShowPlasmaWeapons() => ShowWeaponClass(WeaponClass.Plasma);

    private void ShowWeaponClass(WeaponClass weaponClass)
    {
        EnsureMode(BuildMode.Weapons);
        palette.ShowForWeaponClass(weaponClass);
        WeaponFilter = weaponClass;
    }

    private void EnsureMode(BuildMode mode)
    {
        if (CurrentMode != mode) EnterMode(mode);
    }

    public void SelectBlock(BlockDefinition block)
    {
        SetDeleteMode(false);
        ghost.SelectBlock(block);
        blockSelected = true;
        if (rotateButtonIcon != null)
            rotateButtonIcon.localRotation = Quaternion.Euler(0f, 0f, 90f * ghost.RotationSteps);
    }

    public int CurrentRotationSteps => ghost.RotationSteps;

    public void BeginGridPlacement(Vector2 screenPos) => ghost.BeginGridDrag(screenPos);

    public void UpdateGridPlacement(Vector2 screenPos) => ghost.UpdateGridDrag(screenPos);

    public void EndGridPlacement(Vector2 screenPos) => ghost.EndGridDrag(screenPos);

    public void RotateSelected()
    {
        ghost.RotateGhost();
        if (rotateButtonIcon != null)
            rotateButtonIcon.localRotation = Quaternion.Euler(0f, 0f, 90f * ghost.RotationSteps);
        palette.SetSelectedIconRotation(90f * ghost.RotationSteps);
    }

    public void ToggleDeleteMode() => SetDeleteMode(!ghost.IsDeleteModeActive);

    public void SetDeleteMode(bool active)
    {
        ghost.SetDeleteMode(active);
        blockSelected = false;
        if (active) palette.ClearSelection();
        deleteButton.image.color = active ? deleteActiveColor : deleteDefaultColor;
        if (deleteIcon != null) deleteIcon.color = active ? deleteIconActiveColor : deleteIconDefaultColor;
    }

    public void ResetForExit()
    {
        ghost.StopPlacing();
        SetDeleteMode(false);
        if (armorSubCategoryPanel != null) armorSubCategoryPanel.SetActive(false);
        if (weaponSubCategoryPanel != null) weaponSubCategoryPanel.SetActive(false);
    }
}
