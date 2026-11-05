using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("Sound")]
    [Tooltip("Played when a block is built on the ship.")]
    public AudioClip placeSound;
    [Range(0f, 1f)] public float placeVolume = 1f;
    [Tooltip("Played when a block is dismantled.")]
    public AudioClip dismantleSound;
    [Range(0f, 1f)] public float dismantleVolume = 1f;

    private GameObject ghostInstance;
    private Transform ghostVisualRoot;
    private List<ShipModule.PosePart> ghostParts;
    private readonly List<SpriteRenderer> ghostRenderers = new();
    private BlockDefinition currentBlock;
    private int rotationSteps;

    private bool isSelected;
    private bool draggingOnGrid;

    private bool lastValid;
    private bool lastAffordable;
    private Vector2Int lastAnchor;
    private List<BlockCell> lastShape;

    public bool IsDeleteModeActive { get; private set; }

    public int RotationSteps => rotationSteps;

    public bool IsDragging => draggingOnGrid;

    public void SetDeleteMode(bool active)
    {
        IsDeleteModeActive = active;
        confirmPopup?.Hide();
        if (active) StopPlacing();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsDeleteModeActive) return;

        Vector3 world = worldCamera.ScreenToWorldPoint(eventData.position);
        world.z = 0f;

        var target = grid.FindDeletableAt(world);
        if (target == null) return;

        if (confirmPopup != null)
            confirmPopup.Show(() => ConfirmDelete(target), () => { });
        else
            ConfirmDelete(target);
    }

    private void ConfirmDelete(ShipModule target)
    {
        int refund = Mathf.RoundToInt(target.buildCost * dismantleRefundFraction);
        if (refund > 0) GameDataManager.Instance.AddCredits(refund);

        grid.DeleteBlock(target);
        AudioManager.PlaySfx(dismantleSound, dismantleVolume);
    }

    public void SelectBlock(BlockDefinition block)
    {
        bool sameBlock = isSelected && currentBlock == block;
        int keepRotation = sameBlock ? rotationSteps : 0;

        StopPlacing();
        IsDeleteModeActive = false;

        currentBlock = block;
        rotationSteps = keepRotation;
        isSelected = true;
    }

    public void StopPlacing()
    {
        confirmPopup?.Hide();
        DestroyGhost();
        grid.ClearPlacementPreview();

        currentBlock = null;
        isSelected = false;
        draggingOnGrid = false;
    }

    public void RotateGhost()
    {
        if (!isSelected || draggingOnGrid) return;
        rotationSteps = (rotationSteps + 1) % 4;
    }

    public void BeginGridDrag(Vector2 screenPos)
    {
        if (!isSelected || IsDeleteModeActive) return;

        confirmPopup?.Hide();
        DestroyGhost();
        SpawnGhost();
        draggingOnGrid = true;
        UpdateGhost(screenPos);
    }

    public void UpdateGridDrag(Vector2 screenPos)
    {
        if (!draggingOnGrid) return;
        UpdateGhost(screenPos);
    }

    public void EndGridDrag(Vector2 screenPos)
    {
        if (!draggingOnGrid) return;

        UpdateGhost(screenPos);
        draggingOnGrid = false;

        if (!lastValid)
        {
            if (!lastAffordable) GameDataManager.Instance.NotifyInsufficientCredits();

            DestroyGhost();
            grid.ClearPlacementPreview();
            return;
        }

        if (confirmPopup != null)
            confirmPopup.Show(ConfirmPlacement, CancelPlacement);
        else
            ConfirmPlacement();
    }

    private void ConfirmPlacement()
    {
        if (!GameDataManager.Instance.SpendCredits(currentBlock.buildCost))
        {
            CancelPlacement();
            return;
        }

        grid.PlaceBlock(currentBlock, lastAnchor, lastShape, rotationSteps);
        AudioManager.PlaySfx(placeSound, placeVolume);

        DestroyGhost();
        grid.ClearPlacementPreview();
    }

    private void CancelPlacement()
    {
        DestroyGhost();
        grid.ClearPlacementPreview();
    }

    private void SpawnGhost()
    {
        ghostInstance = Instantiate(currentBlock.prefab);
        ghostInstance.name = $"Ghost_{currentBlock.id}";

        var moduleComp = ghostInstance.GetComponent<ShipModule>();
        ghostVisualRoot = (moduleComp != null && moduleComp.visualRoot != null)
            ? moduleComp.visualRoot
            : ghostInstance.transform;

        if (moduleComp != null)
        {
            moduleComp.ApplyViewMode(ShipViewMode.Building);
            moduleComp.PrepareAsGhost();

            moduleComp.ApplySortingOrders(grid.roofSortingOrder, grid.topSortingOrder);
            ghostParts = moduleComp.CapturePoseParts();
        }

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
        ghostParts = null;
        ghostVisualRoot = null;
    }

    private void UpdateGhost(Vector2 screenPos)
    {
        Vector3 pointerWorld = worldCamera.ScreenToWorldPoint(screenPos);
        pointerWorld.z = 0f;

        Vector2Int anchor = grid.WorldToGrid(pointerWorld);
        var rotatedCells = BlockDefinition.RotateCells(currentBlock.cells, rotationSteps);
        var localOffsets = BlockDefinition.Offsets(rotatedCells);

        bool geometryValid = grid.CanPlace(anchor, rotatedCells, currentBlock.category);

        bool affordable = GameDataManager.Instance.Credits >= currentBlock.buildCost;
        bool valid = geometryValid && affordable;

        ghostInstance.transform.position = grid.AnchorToWorld(anchor);
        ghostInstance.transform.rotation = Quaternion.identity;

        var (centroidCells, _) = ShipGrid.ComputeLocalFootprint(localOffsets);
        var center = new Vector3(centroidCells.x * grid.cellSize, centroidCells.y * grid.cellSize, 0f);
        if (ghostVisualRoot != ghostInstance.transform)
        {
            ghostVisualRoot.localPosition = center;
            ghostVisualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationSteps);
        }

        var (unrotated, _) = ShipGrid.ComputeLocalFootprint(BlockDefinition.Offsets(BlockDefinition.RotateCells(currentBlock.cells, 0)));
        ShipModule.PoseParts(ghostParts, center, new Vector3(unrotated.x * grid.cellSize, unrotated.y * grid.cellSize, 0f), rotationSteps);

        var absoluteCells = grid.GetOccupiedCells(anchor, localOffsets);
        grid.ShowPlacementPreview(absoluteCells, valid ? validColor : invalidColor);

        lastValid = valid;
        lastAffordable = affordable;
        lastAnchor = anchor;
        lastShape = rotatedCells;
    }
}
