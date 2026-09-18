using UnityEngine;

/// <summary>
/// Power generator. Contributes positive energyDelta to the ship while alive;
/// stops contributing the instant it's destroyed (ShipGrid.ComputeEnergyBalance
/// only sums non-destroyed modules).
/// </summary>
public class GeneratorModule : ShipModule
{
    [Header("Generator")]
    public float powerOutput = 10f;
    [Tooltip("How much this generator contributes to the ship's MAX energy capacity — a separate " +
             "concept from Power Output (generation rate per second). See ShipEnergySystem.")]
    public float capacity = 20f;

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Generator;
        energyDelta = Mathf.Abs(powerOutput);
    }
}
