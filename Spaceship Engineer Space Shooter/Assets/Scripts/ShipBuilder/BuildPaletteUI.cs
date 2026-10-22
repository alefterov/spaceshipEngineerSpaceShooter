using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom scrollable list of available blocks, filtered by the active BuildMode
/// (Cockpit, Generators, Engines, Armor, Weapons — Armor and Weapons optionally narrowed further to one
/// sub-category via ShowForCategory / ShowForWeaponClass).
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
    [Tooltip("The scroll view's Content. It is made exactly as wide as the buttons in it (a row layout + a content size fitter are added if missing).")]
    public Transform buttonContainer;   // e.g. the bottom scroll rect content
    public GameObject buttonPrefab;     // prefab: Button + BlockButtonView (icon, selectionHighlight)

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
            BuildMode.Armor => database.GetByCategory(BlockCategory.Armor), // shields via the Shields sub-tab
            _ => database.GetByCategory(BlockCategory.Weapon),              // Weapons: every family until a sub-tab narrows it
        };

        Populate(blocks);
    }

    /// <summary>Narrows the palette to one category — for the Armor tab's Armor/Shields sub-tabs. Call
    /// after (or let BuildModeController do it) switching to the right build mode; this only changes
    /// which blocks the palette lists.</summary>
    public void ShowForCategory(BlockCategory category) => Populate(database.GetByCategory(category));

    /// <summary>Narrows the palette to one weapon family — for the Weapons tab's sub-tabs.</summary>
    public void ShowForWeaponClass(WeaponClass weaponClass) => Populate(database.GetWeaponsByClass(weaponClass));

    private void Populate(List<BlockDefinition> blocks)
    {
        EnsureElasticContent();

        foreach (Transform child in buttonContainer)
        {
            child.gameObject.SetActive(false); // Destroy is deferred — the layout must stop counting it right now
            Destroy(child.gameObject);
        }

        selectedButton = null;
        if (infoCard != null) infoCard.Hide(); // the selection is gone with the old list

        foreach (var block in blocks)
            if (!block.enemyOnly && IsUnlocked(block)) CreateButton(block); // enemy-only blocks never show for the player

        FitContent();
    }

    /// <summary>Makes the scroll view's content as wide as exactly the buttons in it: a row layout with a content-size
    /// fitter, pinned to the left edge of the viewport. Anything of that kind you already set up yourself is left alone.</summary>
    private void EnsureElasticContent()
    {
        if (buttonContainer is not RectTransform content) return;

        if (!content.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out _))
        {
            var row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = false;   // every button keeps the size of its own prefab
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
        }

        if (!content.TryGetComponent<ContentSizeFitter>(out var fitter))
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Pinned to the left edge so it grows to the right and scrolls from its start.
        content.anchorMin = new Vector2(0f, content.anchorMin.y);
        content.anchorMax = new Vector2(0f, content.anchorMax.y);
        content.pivot = new Vector2(0f, content.pivot.y);
    }

    /// <summary>Recomputes the content's width right away and scrolls back to the first button.</summary>
    private void FitContent()
    {
        if (buttonContainer is not RectTransform content) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.horizontalNormalizedPosition = 0f;
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
        if (infoCard != null) infoCard.SetSpriteRotation(degrees);
    }

    /// <summary>Deselects the currently highlighted button without rebuilding the whole list (e.g. entering delete mode).</summary>
    public void ClearSelection()
    {
        if (selectedButton != null) selectedButton.SetSelected(false);
        selectedButton = null;
        if (infoCard != null) infoCard.Hide();
    }

    /// <summary>Call when the build mode is closed: drops the selection and puts the block card away at once.</summary>
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

        if (view.icon != null && block.icon != null) view.icon.sprite = block.icon;
        view.SetSelected(false);

        // The button shows the build price, not the name (the name is on the block card).
        view.SetPrice(block.buildCost);
        if (view.costLabel == null)
        {
            var legacyLabel = buttonObj.GetComponentInChildren<Text>(); // an old-style Text label on the button gets the price instead
            if (legacyLabel != null) legacyLabel.text = NumberFormat.Compact(block.buildCost);
        }

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
        if (infoCard != null) infoCard.Show(block);
        if (selectedButton.IconTransform != null)
            selectedButton.IconTransform.localRotation = Quaternion.Euler(0f, 0f, 90f * controller.CurrentRotationSteps);
        if (infoCard != null) infoCard.SetSpriteRotation(90f * controller.CurrentRotationSteps);
    }
}