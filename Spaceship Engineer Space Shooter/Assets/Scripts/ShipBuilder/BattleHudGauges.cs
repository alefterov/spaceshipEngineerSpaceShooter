using UnityEngine;

/// <summary>
/// Feeds the battle HUD's radial gauges (energy, HP, shield, armor) from the live ship state.
///
/// Energy and shield vs their (live) max: a generator or shield lost mid-fight immediately shrinks the
/// ceiling, so these always reflect the CURRENT capacity — that's just what a pool means. Shield is the
/// combined reserve of every surviving shield (see ShieldModule) over their combined capacity.
///
/// HP/armor vs their STARTING totals (snapshotted once here, same idea as BattleOutcomeController's
/// own starting-HP capture): the point of these gauges is "how much of what I brought into this fight
/// is left", which a live-recalculating max would mask — it would keep reading close to 100% even after
/// losing half the ship, since destroyed blocks stop counting toward the max the moment they die.
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
    private bool tracking;

    private void OnEnable()
    {
        // Deliberately NOT snapshotting starting HP/armor here — Unity doesn't guarantee this runs
        // AFTER BattleSequenceController.Start() has actually loaded the ship, so it could snapshot an
        // empty, just-instantiated grid (0 HP forever, gauges stuck reading empty even once the ship
        // exists). See BeginTracking, called once loading is genuinely done.
        if (energySystem != null) energySystem.OnEnergyChanged += RefreshEnergyGauge;
        RefreshEnergyGauge(); // energy has no "starting" snapshot to race — safe to show live immediately
    }

    private void OnDisable()
    {
        if (shipGrid != null) shipGrid.OnShipChanged -= RefreshDamageGauges;
        if (energySystem != null) energySystem.OnEnergyChanged -= RefreshEnergyGauge;
        tracking = false;
    }

    /// <summary>Snapshots starting HP/armor and starts tracking live changes against them. Call once the
    /// ship is actually loaded and about to enter combat — BattleSequenceController calls this at the
    /// same moment as BattleOutcomeController.BeginTracking().</summary>
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
        // Shield reserve changes continuously (drained by hits, refilled every frame) without any
        // ship-changed event, so it's simply polled — a sum over a handful of shield blocks.
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
