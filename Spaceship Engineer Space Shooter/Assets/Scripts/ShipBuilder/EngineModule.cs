using UnityEngine;

public enum EngineKind { Jet, Ion }

public class EngineModule : ShipModule
{
    [Header("Engine")]
    public float thrustPower = 5f;
    [Tooltip("Ion or Jet — which crew perk (IonEnginePower / JetEnginePower) boosts this engine.")]
    public EngineKind kind = EngineKind.Jet;

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
            SetThrustStrength(0f);
            thrustEffect.Play();
        }
    }

    public override void ApplyViewMode(ShipViewMode mode)
    {
        base.ApplyViewMode(mode);
        SetEffectRunning(mode != ShipViewMode.Building);
    }

    public float GetThrust() => IsDestroyed ? 0f : thrustPower * CrewThrustMultiplier;

    private ShipIdentity identity;
    private float CrewThrustMultiplier
    {
        get
        {
            if (identity == null) identity = GetComponentInParent<ShipIdentity>();
            if (identity == null || identity.faction != Faction.Player || GameDataManager.Instance == null) return 1f;
            return GameDataManager.Instance.GetCrewPerkMultiplier(kind == EngineKind.Ion ? CrewPerkType.IonEnginePower : CrewPerkType.JetEnginePower);
        }
    }

    public void SetThrustStrength(float strength01)
    {
        if (thrustEffect == null || IsDestroyed) return;

        var rate = emission.rateOverTime;
        rate.constant = Mathf.Lerp(idleEmissionRate, fullThrustEmissionRate, Mathf.Clamp01(strength01));
        emission.rateOverTime = rate;
    }

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
