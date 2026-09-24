using TMPro;
using UnityEngine;

/// <summary>
/// Displays the ship's main stats — total HP, armor, shield, firepower, engine power, energy
/// generation and consumption — refreshed live from ShipGrid.OnShipChanged, so it stays accurate
/// while building (a block placed/removed updates it immediately), not just once when the panel opens.
///
/// SETUP: assign Grid, then drag each stat's TMP_Text label into its field. Any label left
/// unassigned is simply skipped — you don't need all of them wired to use the panel.
/// </summary>
public class ShipStatsPanel : MonoBehaviour
{
    [Header("References")]
    public ShipGrid grid;

    [Header("Labels")]
    [Tooltip("Total HP across every placed block — every block combined.")]
    public TMP_Text hpLabel;
    public TMP_Text armorLabel;
    public TMP_Text shieldLabel;
    public TMP_Text firepowerLabel;
    public TMP_Text enginePowerLabel;
    public TMP_Text energyGenerationLabel;
    public TMP_Text energyConsumptionLabel;

    [Tooltip("Number format applied to every stat, e.g. \"F0\" for whole numbers, \"F1\" for one decimal place.")]
    public string numberFormat = "F0";

    private void OnEnable()
    {
        if (grid != null) grid.OnShipChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (grid != null) grid.OnShipChanged -= Refresh;
    }

    public void Refresh()
    {
        if (grid == null) return;

        SetLabel(hpLabel, grid.ComputeTotalHP());
        SetLabel(armorLabel, grid.ComputeTotalArmor());
        SetLabel(shieldLabel, grid.ComputeShieldStrength());
        SetLabel(firepowerLabel, grid.ComputeFirepower());
        SetLabel(enginePowerLabel, grid.ComputeEnginePower());
        SetLabel(energyGenerationLabel, grid.ComputeEnergyGeneration());
        SetLabel(energyConsumptionLabel, grid.ComputeEnergyConsumption());
    }

    private void SetLabel(TMP_Text label, float value)
    {
        if (label != null) label.text = value.ToString(numberFormat);
    }
}
