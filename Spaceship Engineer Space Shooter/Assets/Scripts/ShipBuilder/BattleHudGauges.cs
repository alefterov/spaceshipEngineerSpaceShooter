using UnityEngine;

/// <summary>
/// Feeds the battle HUD's radial gauges (energy, HP, shield, armor) from the live ship state.
///
/// Energy vs its (live) max: a generator lost mid-fight immediately shrinks Max, so this always
/// reflects the CURRENT ceiling, not what you started with — that's just what an energy pool means.
///
/// HP/shield/armor vs their STARTING totals (snapshotted once here, same idea as
/// BattleOutcomeController's own starting-HP capture): the point of these gauges is "how much of what
/// I brought into this fight is left", which a live-recalculating max would mask — it would keep
/// reading close to 100% even after losing half the ship, since destroyed blocks stop counting
/// toward the max the moment they die.
///
/// SETUP: put anywhere in the battle HUD; assign whichever gauges exist.
/// </summary>
public class BattleHudGauges : MonoBehaviour
{
    [Header("References")]
    public ShipGrid shipGrid;
    public ShipEnergySystem energySystem;

    [Header("Gauges (assign whichever exist)")]
    public RadialGaugeView energyGauge;
    public RadialGaugeView hpGauge;
    public RadialGaugeView shieldGauge;
    public RadialGaugeView armorGauge;

    private float startingHP;
    private float startingArmor;
    private float startingShield;

    private void OnEnable()
    {
        if (shipGrid != null)
        {
            startingHP = shipGrid.ComputeCurrentTotalHP();
            startingArmor = shipGrid.ComputeCurrentArmorHP();
            startingShield = shipGrid.ComputeCurrentShieldHP();
            shipGrid.OnShipChanged += RefreshDamageGauges;
        }

        if (energySystem != null) energySystem.OnEnergyChanged += RefreshEnergyGauge;

        RefreshDamageGauges();
        RefreshEnergyGauge();
    }

    private void OnDisable()
    {
        if (shipGrid != null) shipGrid.OnShipChanged -= RefreshDamageGauges;
        if (energySystem != null) energySystem.OnEnergyChanged -= RefreshEnergyGauge;
    }

    private void RefreshDamageGauges()
    {
        if (shipGrid == null) return;
        if (hpGauge != null) hpGauge.SetValue(shipGrid.ComputeCurrentTotalHP(), startingHP);
        if (armorGauge != null) armorGauge.SetValue(shipGrid.ComputeCurrentArmorHP(), startingArmor);
        if (shieldGauge != null) shieldGauge.SetValue(shipGrid.ComputeCurrentShieldHP(), startingShield);
    }

    private void RefreshEnergyGauge()
    {
        if (energyGauge != null && energySystem != null) energyGauge.SetValue(energySystem.Current, energySystem.Max);
    }
}
