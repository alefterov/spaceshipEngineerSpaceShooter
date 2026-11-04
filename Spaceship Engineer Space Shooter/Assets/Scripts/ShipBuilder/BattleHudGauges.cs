using UnityEngine;

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
    private bool tracking;

    private void OnEnable()
    {
        if (energySystem != null) energySystem.OnEnergyChanged += RefreshEnergyGauge;
        RefreshEnergyGauge();
    }

    private void OnDisable()
    {
        if (shipGrid != null) shipGrid.OnShipChanged -= RefreshDamageGauges;
        if (energySystem != null) energySystem.OnEnergyChanged -= RefreshEnergyGauge;
        tracking = false;
    }

    public void BeginTracking()
    {
        if (shipGrid == null) return;

        startingHP = shipGrid.ComputeCurrentTotalHP();
        startingArmor = shipGrid.ComputeCurrentArmorHP();

        if (!tracking)
        {
            shipGrid.OnShipChanged += RefreshDamageGauges;
            tracking = true;
        }

        RefreshDamageGauges();
    }

    private void Update()
    {
        if (shipGrid != null && shieldGauge != null)
            shieldGauge.SetValue(shipGrid.ComputeShieldReserve(), shipGrid.ComputeShieldStrength());
    }

    private void RefreshDamageGauges()
    {
        if (shipGrid == null) return;
        if (hpGauge != null) hpGauge.SetValue(shipGrid.ComputeCurrentTotalHP(), startingHP);
        if (armorGauge != null) armorGauge.SetValue(shipGrid.ComputeCurrentArmorHP(), startingArmor);
    }

    private void RefreshEnergyGauge()
    {
        if (energyGauge != null && energySystem != null) energyGauge.SetValue(energySystem.Current, energySystem.Max);
    }
}
