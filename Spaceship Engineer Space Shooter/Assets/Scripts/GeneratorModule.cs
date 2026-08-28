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

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Generator;
        energyDelta = Mathf.Abs(powerOutput);
    }
}
