using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom scrollable list of available blocks, filtered by the active BuildMode
/// (hull pieces in Hull mode, functional modules in Module mode).
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

        List<BlockDefinition> blocks = mode == BuildMode.Hull
            ? database.GetStructuralBlocks()
            : database.GetFunctionalBlocks();

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

        if (view.icon != null && block.icon != null) view.icon.sprite = block.icon;
        view.SetSelected(false);

        var label = buttonObj.GetComponentInChildren<Text>();
        if (label != null) label.text = block.displayName;

        var button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => Select(block, view));
    }

    private void Select(BlockDefinition block, BlockButtonView view)
    {
        if (selectedButton != null) selectedButton.SetSelected(false);

        selectedButton = view;
        selectedButton.SetSelected(true);
        if (selectedButton.IconTransform != null) selectedButton.IconTransform.localRotation = Quaternion.identity;

        controller.SelectBlock(block);
    }
}