using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button driving a single progression upgrade — hangar size, battle credits multiplier, or
/// survival mode starting level. All three share the same research-point economy and linear
/// cost-scaling pattern in GameDataManager; this just picks which one via UpgradeType and shows its
/// current value + next level's cost, mirroring CurrencyDisplay's Currency enum switch.
/// </summary>
public class UpgradeButtonView : MonoBehaviour
{
    public enum UpgradeType { Hangar, BattleCreditsMultiplier, SurvivalStartLevel }

    [Header("Upgrade")]
    public UpgradeType upgradeType = UpgradeType.Hangar;
    [Tooltip("Required only for UpgradeType.Hangar — the grid the upgrade applies to.")]
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
            case UpgradeType.Hangar: data.OnHangarLevelChanged += Refresh; break;
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
            case UpgradeType.Hangar: data.OnHangarLevelChanged -= Refresh; break;
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
            case UpgradeType.Hangar:
                level = data.HangarLevel;
                value = grid != null ? $"{grid.GridWidth}x{grid.GridHeight}" : "-";
                cost = data.GetHangarUpgradeCost();
                canUpgrade = data.CanUpgradeHangar();
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
        if (costLabel != null) costLabel.text = cost.ToString();
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
    }

    private void OnUpgradeClicked()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        switch (upgradeType)
        {
            case UpgradeType.Hangar: data.TryUpgradeHangar(grid); break;
            case UpgradeType.BattleCreditsMultiplier: data.TryUpgradeCreditsMultiplier(); break;
            case UpgradeType.SurvivalStartLevel: data.TryUpgradeSurvivalStartLevel(); break;
        }
    }
}
