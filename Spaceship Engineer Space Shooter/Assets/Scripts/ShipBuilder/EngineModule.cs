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

    [Header("Thrust effect (optional)")]
    [Tooltip("Burn/exhaust particle system — plays continuously at Idle Emission Rate, scaled up " +
             "toward Full Thrust Emission Rate while ShipMovement reports the ship is actually moving. " +
             "Fed every frame via SetThrustStrength — this component doesn't poll movement itself.")]
    public ParticleSystem thrustEffect;
    public float idleEmissionRate = 5f;
    public float fullThrustEmissionRate = 30f;

    private ParticleSystem.EmissionModule emission;

    private void Awake()
    {
        base.Awake();
        type = ModuleType.Engine;
        energyDelta = -Mathf.Abs(energyDelta);

        if (thrustEffect != null)
        {
            emission = thrustEffect.emission;
            SetThrustStrength(0f); // start at idle rate rather than whatever the prefab's own default is
            thrustEffect.Play();
        }
    }

    /// <summary>Returns this engine's thrust contribution, or 0 if destroyed.</summary>
    public float GetThrust() => IsDestroyed ? 0f : thrustPower;

    /// <summary>Called every frame by ShipMovement with the ship's current joystick deflection (0 =
    /// idling, 1 = full thrust) — scales this engine's own burn effect between Idle and Full Thrust
    /// emission rates. A no-op once this engine is destroyed or has no effect assigned.</summary>
    public void SetThrustStrength(float strength01)
    {
        if (thrustEffect == null || IsDestroyed) return;

        var rate = emission.rateOverTime;
        rate.constant = Mathf.Lerp(idleEmissionRate, fullThrustEmissionRate, Mathf.Clamp01(strength01));
        emission.rateOverTime = rate;
    }

    protected override void OnModuleDestroyed()
    {
        if (thrustEffect != null) thrustEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
