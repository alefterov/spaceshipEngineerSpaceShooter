using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BuildPaletteUI : MonoBehaviour
{
    public BuildModeController controller;
    public BlockDatabase database;
    [Tooltip("Optional. If assigned, blocks gated behind an unresearched technology are hidden from " +
             "the palette instead of always being shown.")]
    public TechDatabase techDatabase;

    [Header("UI wiring")]
    [Tooltip("The scroll view's Content. It is made exactly as wide as the buttons in it (a row layout + a content size fitter are added if missing).")]
    public Transform buttonContainer;
    public GameObject buttonPrefab;

    [Tooltip("The block card: name, picture and numbers of the selected block (health, damage, energy, fire rate). Shown when a " +
             "button is selected, hidden when the selection is cleared. Optional — without it the name is written on the button as before.")]
    public BlockInfoCard infoCard;

    private BlockButtonView selectedButton;

    public void ShowForMode(BuildMode mode)
    {
        List<BlockDefinition> blocks = mode switch
        {
            BuildMode.Cockpit => database.GetByCategory(BlockCategory.Cockpit),
            BuildMode.Generators => database.GetByCategory(BlockCategory.Generator),
            BuildMode.Engines => database.GetByCategory(BlockCategory.Engine),
            BuildMode.Repair => database.GetByCategory(BlockCategory.Repair),
            BuildMode.Armor => database.GetByCategory(BlockCategory.Armor),
            _ => database.GetByCategory(BlockCategory.Weapon),
        };

        Populate(blocks);
    }

    public void ShowForCategory(BlockCategory category) => Populate(database.GetByCategory(category));

    public void ShowForWeaponClass(WeaponClass weaponClass) => Populate(database.GetWeaponsByClass(weaponClass));

    private void Populate(List<BlockDefinition> blocks)
    {
        EnsureElasticContent();

        foreach (Transform child in buttonContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        selectedButton = null;
        if (infoCard != null) infoCard.Hide();

        foreach (var block in blocks.Where(b => b != null).OrderBy(b => b.buildCost))
            if (!block.enemyOnly && IsUnlocked(block)) CreateButton(block);

        FitContent();
    }

    private void EnsureElasticContent()
    {
        if (buttonContainer is not RectTransform content) return;

        if (!content.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out _))
        {
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
        }

        if (!content.TryGetComponent<ContentSizeFitter>(out var fitter))
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        content.anchorMin = new Vector2(0f, content.anchorMin.y);
        content.anchorMax = new Vector2(0f, content.anchorMax.y);
        content.pivot = new Vector2(0f, content.pivot.y);
    }

    private void FitContent()
    {
        if (buttonContainer is not RectTransform content) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
    }

    private bool IsUnlocked(BlockDefinition block)
    {
        if (techDatabase == null) return true;
        var data = GameDataManager.Instance;
        return data == null || data.IsBlockUnlocked(block, techDatabase);
    }

    public void SetSelectedIconRotation(float degrees)
    {
        if (selectedButton != null && selectedButton.IconTransform != null)
            selectedButton.IconTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        if (infoCard != null) infoCard.SetSpriteRotation(degrees);
    }

    public void ClearSelection()
    {
        if (selectedButton != null) selectedButton.SetSelected(false);
        selectedButton = null;
        if (infoCard != null) infoCard.Hide();
    }

    public void ResetForExit()
    {
        if (selectedButton != null) selectedButton.SetSelected(false);
        selectedButton = null;
        if (infoCard != null) infoCard.HideImmediate();
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

        view.Block = block;
        if (view.icon != null && block.icon != null) view.icon.sprite = block.icon;
        view.SetSelected(false);

        view.SetPrice(block.buildCost);
        view.SetSize(block);
        if (view.costLabel == null)
        {
            var legacyLabel = buttonObj.GetComponentInChildren<Text>();
            if (legacyLabel != null) legacyLabel.text = NumberFormat.Compact(block.buildCost);
        }

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

        controller.SelectBlock(block);
        if (infoCard != null) infoCard.Show(block);
        if (selectedButton.IconTransform != null)
            selectedButton.IconTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * controller.CurrentRotationSteps);
        if (infoCard != null) infoCard.SetSpriteRotation(90f * controller.CurrentRotationSteps);
    }
}
