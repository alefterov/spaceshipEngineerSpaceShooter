using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom scrollable list of available blocks, filtered by the active BuildMode
/// (hull pieces in Hull mode, functional modules in Module mode).
/// Assumes buttonPrefab is a UI Button with an Image (icon) as a child.
/// </summary>
public class BuildPaletteUI : MonoBehaviour
{
    public BuildModeController controller;
    public BlockDatabase database;

    [Header("UI wiring")]
    public Transform buttonContainer;   // horizontal layout group, e.g. bottom scroll rect content
    public GameObject buttonPrefab;     // prefab: Button + child Image for the icon

    public void ShowForMode(BuildMode mode)
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        List<BlockDefinition> blocks = mode == BuildMode.Hull
            ? database.GetStructuralBlocks()
            : database.GetFunctionalBlocks();

        foreach (var block in blocks)
            CreateButton(block);
    }

    private void CreateButton(BlockDefinition block)
    {
        var buttonObj = Instantiate(buttonPrefab, buttonContainer);
        buttonObj.name = $"Btn_{block.id}";

        var icon = buttonObj.GetComponentInChildren<Image>();
        if (icon != null && block.icon != null) icon.sprite = block.icon;

        var label = buttonObj.GetComponentInChildren<Text>();
        if (label != null) label.text = block.displayName;

        var button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => controller.SelectBlock(block));
    }
}
