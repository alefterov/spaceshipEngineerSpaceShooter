using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom scrollable list of available blocks, filtered by the active BuildMode
/// (hull pieces in Hull mode, armor pieces in Armor mode, functional modules in Module mode).
/// buttonPrefab must have a BlockButtonView component (icon + selection highlight).
/// </summary>
public class BuildPaletteUI : MonoBehaviour
{
    public BuildModeController controller;
    public BlockDatabase database;

    [Header("UI wiring")]
    public Transform buttonContainer;   // horizontal layout group, e.g. bottom scroll rect content
    public GameObject buttonPrefab;     // prefab: Button + BlockButtonView (icon, selectionHighlight)

    private BlockButtonView selectedButton;

    public void ShowForMode(BuildMode mode)
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        selectedButton = null;

        List<BlockDefinition> blocks = mode switch
        {
            BuildMode.Hull => database.GetByCategory(BlockCategory.Hull),
            BuildMode.Armor => database.GetByCategory(BlockCategory.Armor),
            _ => database.GetFunctionalBlocks(),
        };

        foreach (var block in blocks)
            CreateButton(block);
    }

    /// <summary>Called by BuildModeController.RotateSelected() to spin the selected block's own icon in sync.</summary>
    public void SetSelectedIconRotation(float degrees)
    {
        if (selectedButton != null && selectedButton.IconTransform != null)
            selectedButton.IconTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    /// <summary>Deselects the currently highlighted button without rebuilding the whole list (e.g. entering delete mode).</summary>
    public void ClearSelection()
    {
        if (selectedButton != null) selectedButton.SetSelected(false);
        selectedButton = null;
    }

    private void CreateButton(BlockDefinition block)
    {
        var buttonObj = Instantiate(buttonPrefab, buttonContainer);
        buttonObj.name = $"Btn_{block.id}";

        var view = buttonObj.GetComponent<BlockButtonView>();
        if (view == null)
        {
            Debug.LogError($"Button prefab is missing a BlockButtonView component ('{block.id}').");
            return;
        }

        var dragHandle = buttonObj.GetComponent<BlockButtonDragHandle>();
        if (dragHandle == null)
        {
            Debug.LogError($"Button prefab is missing a BlockButtonDragHandle component ('{block.id}').");
            return;
        }

        if (view.icon != null && block.icon != null) view.icon.sprite = block.icon;
        view.SetSelected(false);

        var label = buttonObj.GetComponentInChildren<Text>();
        if (label != null) label.text = block.displayName;

        // Plain tap -> select only. Drag off the button -> select AND start dragging the ghost
        // onto the grid in the same continuous gesture (see BlockButtonDragHandle for why this
        // works even once the finger is no longer over the button).
        dragHandle.OnTap += () => Select(block, view);
        dragHandle.OnDragStarted += screenPos =>
        {
            Select(block, view);
            controller.BeginGridPlacement(screenPos);
        };
        dragHandle.OnDragMoved += controller.UpdateGridPlacement;
        dragHandle.OnDragReleased += controller.EndGridPlacement;
    }

    private void Select(BlockDefinition block, BlockButtonView view)
    {
        if (selectedButton != null) selectedButton.SetSelected(false);

        selectedButton = view;
        selectedButton.SetSelected(true);

        // Resolve selection first — re-selecting the same block (e.g. a drag-off gesture
        // starting) keeps its rotation now, so sync the icon FROM that result instead of
        // zeroing it out up front.
        controller.SelectBlock(block);
        if (selectedButton.IconTransform != null)
            selectedButton.IconTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * controller.CurrentRotationSteps);
    }
}