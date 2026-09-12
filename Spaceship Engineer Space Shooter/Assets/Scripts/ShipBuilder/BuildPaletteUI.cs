using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom scrollable list of available blocks, filtered by the active BuildMode
/// (hull pieces in Hull mode, armor pieces in Armor mode, functional modules — including the
/// cockpit — in Module mode, optionally narrowed further to one sub-category via ShowForCategory).
/// buttonPrefab must have a BlockButtonView component (icon + selection highlight).
/// </summary>
public class BuildPaletteUI : MonoBehaviour
{
    public BuildModeController controller;
    public BlockDatabase database;
    [Tooltip("Optional. If assigned, blocks gated behind an unresearched technology are hidden from " +
             "the palette instead of always being shown.")]
    public TechDatabase techDatabase;

    [Header("UI wiring")]
    public Transform buttonContainer;   // horizontal layout group, e.g. bottom scroll rect content
    public GameObject buttonPrefab;     // prefab: Button + BlockButtonView (icon, selectionHighlight)

    private BlockButtonView selectedButton;

    public void ShowForMode(BuildMode mode)
    {
        List<BlockDefinition> blocks = mode switch
        {
            BuildMode.Hull => database.GetByCategory(BlockCategory.Hull),
            BuildMode.Armor => database.GetByCategory(BlockCategory.Armor),
            _ => database.GetFunctionalBlocks(), // Modules — includes Cockpit, see BlockDefinition.IsStructural
        };

        Populate(blocks);
    }

    /// <summary>Narrows the palette to one specific functional sub-category (Weapon/Engine/Generator/
    /// Shield/Cockpit) — for the Module mode sub-tabs. Call ShowForMode(BuildMode.Modules) first (or
    /// let BuildModeController.ShowWeaponModules() etc. do it for you) to actually switch build mode;
    /// this only changes which blocks the palette lists.</summary>
    public void ShowForCategory(BlockCategory category) => Populate(database.GetByCategory(category));

    private void Populate(List<BlockDefinition> blocks)
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        selectedButton = null;

        foreach (var block in blocks)
            if (IsUnlocked(block)) CreateButton(block);
    }

    private bool IsUnlocked(BlockDefinition block)
    {
        if (techDatabase == null) return true; // no tech-gating configured — show everything
        var data = GameDataManager.Instance;
        return data == null || data.IsBlockUnlocked(block, techDatabase);
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