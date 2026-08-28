using UnityEngine;

/// <summary>
/// Engine. Contributes thrust to the ship's movement while alive.
/// ShipController should sum GetThrust() across all non-destroyed engines
/// on the ship each frame — losing an engine mid-fight immediately reduces speed.
/// </summary>
public class EngineModule : ShipModule
{
    [Header("Engine")]
    public float thrustPower = 5f;

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Engine;
        energyDelta = -Mathf.Abs(energyDelta);
    }

    /// <summary>Returns this engine's thrust contribution, or 0 if destroyed.</summary>
    public float GetThrust() => IsDestroyed ? 0f : thrustPower;
}
