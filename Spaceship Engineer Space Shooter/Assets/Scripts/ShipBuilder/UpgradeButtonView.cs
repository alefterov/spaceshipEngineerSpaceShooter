using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button driving a single progression upgrade — a hangar column or a hangar row (two separate buttons,
/// each with its own price), battle credits multiplier, or
/// survival mode starting level. All three share the same research-point economy and linear
/// cost-scaling pattern in GameDataManager; this just picks which one via UpgradeType and shows its
/// current value + next level's cost, mirroring CurrencyDisplay's Currency enum switch.
/// </summary>
public class UpgradeButtonView : MonoBehaviour
{
    // Explicit values keep already-serialized buttons on the same entry: the old single "Hangar" button (0)
    // is now the column button. Add a second button set to HangarRows for rows.
    public enum UpgradeType { HangarColumns = 0, BattleCreditsMultiplier = 1, SurvivalStartLevel = 2, HangarRows = 3 }

    [Header("Upgrade")]
    public UpgradeType upgradeType = UpgradeType.HangarColumns;
    [Tooltip("Required only for the hangar types (HangarColumns / HangarRows) — the grid the upgrade applies to.")]
    public ShipGrid grid;

    [Header("UI (assign whichever exist on this prefab)")]
    [Tooltip("Current upgrade level, formatted like \"1 LV\".")]
    public TMP_Text levelLabel;
    [Tooltip("Current value — e.g. \"5x5\" for hangar size, \"x1.1\" for the credits multiplier, or " +
             "\"1 WV\" for survival start level.")]
    public TMP_Text valueLabel;
    [Tooltip("Research-point cost of the next level.")]
    public TMP_Text costLabel;
    public Button upgradeButton;

    private bool subscribed;

    private void Awake()
    {
        // Same defensive Z-reset as TechNodeView — see that file's Awake for why this matters.
        var rt = (RectTransform)transform;
        var pos = rt.localPosition;
        if (pos.z != 0f) rt.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        TrySubscribe();
    }

    // Same Start()-fallback pattern as TechNodeView/CurrencyDisplay — GameDataManager.Instance may
    // not be set yet during this object's own OnEnable if Awake order puts it first.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnResearchPointsChanged += Refresh;
        switch (upgradeType)
        {
            case UpgradeType.HangarColumns:
            case UpgradeType.HangarRows: data.OnHangarLevelChanged += Refresh; break;
            case UpgradeType.BattleCreditsMultiplier: data.OnCreditsMultiplierLevelChanged += Refresh; break;
            case UpgradeType.SurvivalStartLevel: data.OnSurvivalStartLevelChanged += Refresh; break;
        }

        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnResearchPointsChanged -= Refresh;
        switch (upgradeType)
        {
            case UpgradeType.HangarColumns:
            case UpgradeType.HangarRows: data.OnHangarLevelChanged -= Refresh; break;
            case UpgradeType.BattleCreditsMultiplier: data.OnCreditsMultiplierLevelChanged -= Refresh; break;
            case UpgradeType.SurvivalStartLevel: data.OnSurvivalStartLevelChanged -= Refresh; break;
        }
    }

    public void Refresh()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        int level;
        string value;
        int cost;
        bool canUpgrade;

        switch (upgradeType)
        {
            case UpgradeType.HangarColumns:
            case UpgradeType.HangarRows:
                level = upgradeType == UpgradeType.HangarColumns ? data.HangarColumns : data.HangarRows;
                value = grid != null ? $"{grid.GridWidth}x{grid.GridHeight}" : "-";
                cost = upgradeType == UpgradeType.HangarColumns ? data.GetHangarColumnUpgradeCost() : data.GetHangarRowUpgradeCost();
                canUpgrade = upgradeType == UpgradeType.HangarColumns ? data.CanUpgradeHangarColumn() : data.CanUpgradeHangarRow();
                break;
            case UpgradeType.BattleCreditsMultiplier:
                level = data.CreditsMultiplierLevel;
                value = $"x{data.GetCreditsMultiplier():0.0}"; // fixed one decimal — x1.0, x1.1, x1.2...
                cost = data.GetCreditsMultiplierUpgradeCost();
                canUpgrade = data.CanUpgradeCreditsMultiplier();
                break;
            default: // SurvivalStartLevel
                level = data.SurvivalStartLevelUpgrades;
                value = $"{data.GetSurvivalStartLevel()} WV"; // e.g. "1 WV", "2 WV" — starting wave/level
                cost = data.GetSurvivalStartLevelUpgradeCost();
                canUpgrade = data.CanUpgradeSurvivalStartLevel();
                break;
        }

        if (levelLabel != null) levelLabel.text = $"{level} LV";
        if (valueLabel != null) valueLabel.text = value;
        if (costLabel != null) costLabel.text = NumberFormat.Compact(cost);
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
    }

    private void OnUpgradeClicked()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        switch (upgradeType)
        {
            case UpgradeType.HangarColumns: data.TryUpgradeHangarColumn(grid); break;
            case UpgradeType.HangarRows: data.TryUpgradeHangarRow(grid); break;
            case UpgradeType.BattleCreditsMultiplier: data.TryUpgradeCreditsMultiplier(); break;
            case UpgradeType.SurvivalStartLevel: data.TryUpgradeSurvivalStartLevel(); break;
        }
    }
}
