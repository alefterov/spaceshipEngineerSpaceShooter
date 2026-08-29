using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghost preview shown while the player is placing a block: follows the pointer snapped
/// to the grid, tinted blue (valid) or red (invalid), semi-transparent until confirmed.
/// Validation rule depends on the active BuildMode (Hull vs Modules) — see ShipGrid.
/// </summary>
public class GhostBlockController : MonoBehaviour
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

    /// <summary>Call when the player picks a block from the bottom UI list.</summary>
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
    }

    /// <summary>Cancels placement (e.g. player closes the palette or picks a different block).</summary>
    public void StopPlacing()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        currentBlock = null;
        isPlacing = false;
    }

    public void RotateGhost()
    {
        if (!isPlacing) return;
        rotationSteps = (rotationSteps + 1) % 4;
    }

    private void Update()
    {
        if (!isPlacing || currentBlock == null) return;

        Vector3 pointerWorld = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        pointerWorld.z = 0f;

        Vector2Int anchor = grid.WorldToGrid(pointerWorld);
        var localShape = BlockDefinition.RotateCells(currentBlock.cells, rotationSteps);

        bool valid = currentMode == BuildMode.Hull
            ? grid.CanPlaceHull(anchor, localShape)
            : grid.CanPlaceModule(anchor, localShape);

        ghostInstance.transform.position = grid.GridToWorld(anchor, localShape);
        ghostInstance.transform.rotation = Quaternion.Euler(0f, 0f, -90f * rotationSteps);
        Tint(valid ? validColor : invalidColor);

        // NOTE: swap Input.mousePosition / GetMouseButtonDown for touch input (Input.touches)
        // when targeting mobile; also gate this behind "pointer is not over UI" via EventSystem.
        if (Input.GetMouseButtonDown(1)) RotateGhost();
        if (Input.GetMouseButtonDown(0) && valid) Confirm(anchor, localShape);
    }

    private void Confirm(Vector2Int anchor, List<Vector2Int> localShape)
    {
        var placed = currentMode == BuildMode.Hull
            ? grid.PlaceHull(currentBlock.prefab, anchor, localShape)
            : grid.PlaceModule(currentBlock.prefab, anchor, localShape);

        if (placed == null) return;

        // Stay in placement mode with the same block selected (fast multi-placement),
        // matching how most grid builders behave. Call StopPlacing() instead for one-shot placement.
        BeginPlacing(currentBlock, currentMode);
    }

    private void Tint(Color c)
    {
        foreach (var r in ghostRenderers) r.color = c;
    }
}
