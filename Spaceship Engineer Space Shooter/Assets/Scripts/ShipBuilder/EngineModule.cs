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
             "toward Full Thrust Emission Rate while ShipMovement/EnemyShip reports the ship is actually " +
             "moving. Fed every frame via SetThrustStrength — this component doesn't poll movement " +
             "itself. Fully stopped rather than merely idling while building (ApplyViewMode) or once the " +
             "whole ship has no power to run anything (SetEffectRunning — called by ShipMovement/EnemyShip " +
             "once the ship is disabled).")]
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
            thrustEffect.Play(); // ApplyViewMode (called right after placement) immediately corrects this if the ship is being built
        }
    }

    /// <summary>Fully stops the effect while building — an engine shouldn't so much as idle-glow while
    /// you're still assembling the ship — and resumes it (at whatever SetThrustStrength last set) for
    /// every other view (menu preview, battle).</summary>
    public override void ApplyViewMode(ShipViewMode mode)
    {
        base.ApplyViewMode(mode);
        SetEffectRunning(mode != ShipViewMode.Building);
    }

    /// <summary>Returns this engine's thrust contribution, or 0 if destroyed.</summary>
    public float GetThrust() => IsDestroyed ? 0f : thrustPower;

    /// <summary>Called every frame by ShipMovement/EnemyShip with the ship's current thrust deflection
    /// (0 = idling, 1 = full thrust) — scales this engine's own burn effect between Idle and Full Thrust
    /// emission rates. A no-op once this engine is destroyed or has no effect assigned. NOT the same as
    /// "off" — see SetEffectRunning for that; this always leaves at least the idle rate running.</summary>
    public void SetThrustStrength(float strength01)
    {
        if (thrustEffect == null || IsDestroyed) return;

        var rate = emission.rateOverTime;
        rate.constant = Mathf.Lerp(idleEmissionRate, fullThrustEmissionRate, Mathf.Clamp01(strength01));
        emission.rateOverTime = rate;
    }

    /// <summary>Fully starts or stops the burn effect — no emission at all while stopped, not even Idle
    /// Emission Rate. Used both by ApplyViewMode (off while building) and by ShipMovement/EnemyShip once
    /// the whole ship is disabled (no crew left to run anything, not merely "not thrusting right now" —
    /// see SetThrustStrength for that softer case).</summary>
    public void SetEffectRunning(bool running)
    {
        if (thrustEffect == null || IsDestroyed) return;

        if (running) { if (!thrustEffect.isPlaying) thrustEffect.Play(); }
        else thrustEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    protected override void OnModuleDestroyed()
    {
        if (thrustEffect != null) thrustEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
