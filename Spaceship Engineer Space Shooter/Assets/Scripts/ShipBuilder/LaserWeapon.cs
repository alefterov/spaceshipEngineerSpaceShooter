using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A laser. Instead of a shot it fires a BEAM: a glowing line from the muzzle straight along the barrel (for a
/// fixed gun "straight up" the ship's way; for a turret wherever the turret points), Beam Length long. For
/// Beam Duration seconds it deals Damage Per Second to EVERY enemy block under it (it pierces), and to
/// meteors and the like among the Allowed Targets. When the time is up the damage stops and the beam fades away
/// gradually over Fade Out Seconds. The Cooldown starts once the beam has ended.
///
/// Blocks with Laser Resistance (energy armor) take only the remaining share of the damage. Against the player,
/// the Navigator's laser/plasma evasion perk can make a whole beam miss (rolled once per beam).
///
/// SETUP (a normal weapon block prefab): same as a gun — Aiming = Fixed or Turret (with Turret Pivot and its
/// rotation speed — a laser TURRET aims the beam), Muzzles = beam origin(s), Allowed Targets, Range (when it
/// starts firing), Cooldown Seconds, Energy Delta (= the energy one beam costs, paid when it fires). No Projectile Prefab needed. Weapon Class = Laser. Set the
/// beam's damage, duration, length, width, colour and fade here. Each Muzzle fires its own beam, in the order of
/// the list, with its own cooldown.
/// </summary>
public class LaserWeapon : WeaponModule
{
    [Header("Laser beam")]
    [Tooltip("Damage per second to every block (or meteor) the beam passes through.")]
    public float beamDamagePerSecond = 10f;
    [Tooltip("How long the beam stays on, in seconds.")]
    public float beamDuration = 2f;
    [Tooltip("Length of the beam, in world units. 0 = the weapon's Range.")]
    public float beamLength = 0f;
    [Tooltip("Thickness of the glowing core, in world units. Blocks within this width are hit.")]
    public float beamWidth = 0.15f;
    [Tooltip("Seconds the beam takes to fade away after its time is up (damage has already stopped).")]
    public float fadeOutSeconds = 0.6f;
    [Tooltip("The beam hurts everything under it in steps this many seconds apart: the first at the moment it fires, then " +
             "one every step. Each step deals Damage Per Second x this many seconds, so the total over the beam is the same " +
             "whatever the step (1 = once a second).")]
    [Min(0.02f)] public float damageTickSeconds = 1f;

    [Header("Look")]
    [Tooltip("The beam's colour — green by default. The halo around it uses the same colour.")]
    public Color beamColor = new(0.2f, 1f, 0.3f, 1f);
    [Tooltip("The glow around the core is this many times wider than the core.")]
    public float haloWidthMultiplier = 3.5f;
    [Range(0f, 1f), Tooltip("Opacity of the halo (the glow) while the beam is at full strength.")]
    public float haloAlpha = 0.35f;
    [Tooltip("Optional material for the beam (e.g. an additive one for a stronger glow). Empty = the plain sprite material.")]
    public Material beamMaterial;
    public int beamSortingOrder = 20;

    [Header("Animation")]
    [Tooltip("Bright capsules of light that run along the beam, from the muzzle to its far end, over and over.")]
    [Min(0)] public int capsuleCount = 6;
    [Tooltip("Length of each capsule, in world units.")]
    public float capsuleLength = 0.7f;
    [Tooltip("How fast the capsules travel along the beam, in world units per second.")]
    public float capsuleSpeed = 14f;
    [Tooltip("A capsule is this many times thicker than the core of the beam.")]
    public float capsuleWidthMultiplier = 1.7f;
    [Range(0f, 0.8f), Tooltip("How much the glow and the core swell and flicker (0 = steady).")]
    public float shimmerAmount = 0.25f;
    [Tooltip("How fast the glow flickers, in beats per second.")]
    public float shimmerSpeed = 16f;

    protected override bool RequiresProjectilePrefab => false;

    protected override float ReloadAfterShot => EffectiveCooldownSeconds + beamDuration; // the cooldown runs from the beam's end
    protected override float ShotCycleSeconds => EffectiveCooldownSeconds + beamDuration;

    /// <summary>Sustained damage output for the stats panel: the damage of one beam spread over beam time + cooldown.</summary>
    public override float NominalDamagePerShot => beamDamagePerSecond * beamDuration;

    public override float NominalDamagePerSecond
        => beamDamagePerSecond * beamDuration * NominalBarrels / Mathf.Max(0.01f, cooldownSeconds + beamDuration);

    public override float DamagePerSecond
        => beamDamagePerSecond * EffectiveDamageMultiplier * beamDuration * BarrelCount
           / Mathf.Max(0.01f, EffectiveCooldownSeconds + beamDuration);

    private float Length => beamLength > 0f ? beamLength : range;

    private class Beam
    {
        public LineRenderer core;
        public LineRenderer halo;
        public readonly List<LineRenderer> capsules = new();
        public float phase;        // how far the capsules have travelled (0..1 of the beam length, wraps)
        public float alpha = 1f;
        public float activeLeft;   // seconds of damage still to come
        public float fadeLeft;     // seconds of fading still to come (after the damage)
        public float tickTimer;
        public bool evaded;        // dodged by the player's perk — draws, but deals nothing
        public bool crit;          // this whole beam is a critical hit (rolled once per shot)
        public Transform tip;
        public bool Visible => activeLeft > 0f || fadeLeft > 0f;
    }

    private readonly List<Beam> beams = new();
    private readonly List<ShipModule> hitModules = new();
    private readonly List<Targetable> hitObjects = new();

    protected override void Awake()
    {
        base.Awake();

        foreach (var tip in barrels)
            beams.Add(CreateBeam(tip));
    }

    protected override void Update()
    {
        base.Update(); // targeting, aiming the turret, firing (which starts a beam through Spawn)
        UpdateBeams();
    }

    // ---------- Firing ----------

    /// <summary>Called by the weapon when a barrel fires: switches that barrel's beam on.</summary>
    protected override void Spawn(Transform tip)
    {
        int index = barrels.IndexOf(tip);
        if (index < 0 || index >= beams.Count) return;

        var beam = beams[index];
        beam.activeLeft = beamDuration;
        beam.fadeLeft = 0f;
        beam.tickTimer = damageTickSeconds; // full: the first pulse of damage comes on the very next frame

        // The Navigator's laser/plasma evasion perk: one roll per beam fired at the player.
        var data = GameDataManager.Instance;
        beam.evaded = OwnFaction == Faction.Enemy && data != null && data.RollCrewChance(CrewPerkType.EvadeEnergyWeapons);
        beam.crit = CriticalHit.Roll(OwnFaction); // the Captain's crit perk: one roll per beam, the whole beam hits harder

        SetBeamVisible(beam, true);
        SetAlpha(beam, 1f);
        UpdateBeamShape(beam);
    }

    // ---------- Beams ----------

    private void UpdateBeams()
    {
        for (int i = 0; i < beams.Count; i++)
        {
            var beam = beams[i];
            if (!beam.Visible) continue;

            if (beam.activeLeft > 0f)
            {
                UpdateBeamShape(beam);

                // One pulse of damage to every block under the beam each Damage Tick Seconds — the first right as it fires
                // (the timer starts full). A pulse covers the next step; the last one only the time that is left.
                beam.tickTimer += Time.deltaTime;
                if (beam.tickTimer >= damageTickSeconds)
                {
                    float covered = Mathf.Min(damageTickSeconds, beam.activeLeft);
                    if (covered > 0.05f) DealDamage(beam, covered);
                    beam.tickTimer -= damageTickSeconds;
                }

                beam.activeLeft -= Time.deltaTime;

                if (beam.activeLeft <= 0f) beam.fadeLeft = Mathf.Max(0.0001f, fadeOutSeconds); // damage over — start fading
            }
            else
            {
                // Fading out: it keeps following the barrel, losing opacity, and deals nothing.
                UpdateBeamShape(beam);
                beam.fadeLeft -= Time.deltaTime;

                float alpha = fadeOutSeconds > 0f ? Mathf.Clamp01(beam.fadeLeft / fadeOutSeconds) : 0f;
                SetAlpha(beam, alpha);
                if (beam.fadeLeft <= 0f) SetBeamVisible(beam, false);
            }
        }
    }

    private void DealDamage(Beam beam, float seconds)
    {
        if (beam.evaded || beam.tip == null) return;

        Vector2 origin = beam.tip.position;
        Vector2 direction = MuzzleForward;
        float radius = Mathf.Max(0.01f, beamWidth * 0.5f);

        Physics2D.SyncTransforms(); // ships move by setting their transform
        var hits = Physics2D.CircleCastAll(origin, radius, direction, Length);

        hitModules.Clear();
        hitObjects.Clear();

        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (collider == null) continue;

            var block = collider.GetComponent<ShipModule>();
            if (block != null)
            {
                bool hostile = OwnFaction == Faction.Enemy ? collider.CompareTag("PlayerShip") : collider.CompareTag("EnemyShip");
                if (hostile && !block.IsDestroyed && !hitModules.Contains(block)) hitModules.Add(block);
                continue;
            }

            var thing = collider.GetComponent<Targetable>();
            if (thing != null && thing.kind != TargetKind.Ship && !thing.IsDestroyed && allowedTargets.Contains(thing.kind)
                && (thing.kind == TargetKind.Meteor || thing.faction != OwnFaction) && !hitObjects.Contains(thing))
                hitObjects.Add(thing);
        }

        float damage = beamDamagePerSecond * EffectiveDamageMultiplier * seconds;

        foreach (var block in hitModules)
            if (block != null) block.TakeDamage(damage * (1f - block.laserResistance) * (beam.crit ? CriticalHit.Multiplier : 1f), beam.crit);

        foreach (var thing in hitObjects)
            if (thing != null) thing.TakeDamage(damage);
    }

    // ---------- Drawing ----------

    private static Material fallbackMaterial;
    private static Material FallbackMaterial
    {
        get
        {
            if (fallbackMaterial == null) fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
            return fallbackMaterial;
        }
    }

    private Beam CreateBeam(Transform tip)
    {
        var beam = new Beam { tip = tip };

        beam.halo = CreateLine("LaserHalo", tip, beamSortingOrder);
        beam.core = CreateLine("LaserCore", tip, beamSortingOrder + 1);
        for (int i = 0; i < capsuleCount; i++) beam.capsules.Add(CreateLine("LaserCapsule", tip, beamSortingOrder + 2));
        beam.phase = Random.value;

        SetBeamVisible(beam, false);
        return beam;
    }

    private LineRenderer CreateLine(string objectName, Transform parent, int sortingOrder)
    {
        var go = new GameObject(objectName, typeof(LineRenderer));
        go.transform.SetParent(parent, false);

        var line = go.GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 6;
        line.alignment = LineAlignment.View;
        line.sortingOrder = sortingOrder;
        line.material = beamMaterial != null ? beamMaterial : FallbackMaterial;
        return line;
    }

    private void UpdateBeamShape(Beam beam)
    {
        if (beam.tip == null) return;

        float length = Length;
        Vector3 forward = MuzzleForward;
        Vector3 start = beam.tip.position;
        Vector3 end = start + forward * length;

        foreach (var line in new[] { beam.core, beam.halo })
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        // Shimmer: the glow swells and the core flickers, each on its own beat so it never looks regular.
        float t = Time.time * shimmerSpeed;
        float haloBeat = 1f + shimmerAmount * Mathf.Sin(t + beam.phase * 6f);
        float coreBeat = 1f + shimmerAmount * 0.5f * Mathf.Sin(t * 1.7f + 1.3f);
        beam.core.startWidth = beam.core.endWidth = beamWidth * coreBeat;
        beam.halo.startWidth = beam.halo.endWidth = beamWidth * haloWidthMultiplier * haloBeat;

        AnimateCapsules(beam, start, forward, length);
    }

    /// <summary>The capsules of light run from the muzzle to the end of the beam and start over; each fades in and out
    /// at the two ends so they seem to come out of the gun and melt into the distance.</summary>
    private void AnimateCapsules(Beam beam, Vector3 start, Vector3 forward, float length)
    {
        if (beam.capsules.Count == 0 || length <= 0.01f) return;

        beam.phase = Mathf.Repeat(beam.phase + capsuleSpeed * Time.deltaTime / length, 1f);

        Color bright = Color.Lerp(beamColor, Color.white, 0.85f);
        float width = beamWidth * capsuleWidthMultiplier;

        for (int i = 0; i < beam.capsules.Count; i++)
        {
            var capsule = beam.capsules[i];
            float t = Mathf.Repeat(beam.phase + i / (float)beam.capsules.Count, 1f); // 0..1 along the beam
            float centre = t * length;
            float from = Mathf.Max(0f, centre - capsuleLength * 0.5f);
            float to = Mathf.Min(length, centre + capsuleLength * 0.5f);

            capsule.SetPosition(0, start + forward * from);
            capsule.SetPosition(1, start + forward * to);
            capsule.startWidth = capsule.endWidth = width;

            float ends = Mathf.Sin(t * Mathf.PI); // 0 at both ends of the beam, 1 in the middle
            Color color = bright;
            color.a = beam.alpha * Mathf.Clamp01(ends * 1.6f);
            capsule.startColor = capsule.endColor = color;
        }
    }

    private void SetAlpha(Beam beam, float alpha)
    {
        beam.alpha = alpha;
        Color core = Color.Lerp(beamColor, Color.white, 0.6f); // a hot, brighter centre
        core.a = alpha;
        beam.core.startColor = beam.core.endColor = core;

        Color halo = beamColor;
        halo.a = haloAlpha * alpha;
        beam.halo.startColor = beam.halo.endColor = halo;
    }

    private static void SetBeamVisible(Beam beam, bool visible)
    {
        beam.core.enabled = visible;
        beam.halo.enabled = visible;
        foreach (var capsule in beam.capsules) capsule.enabled = visible;
    }

    protected override void OnModuleDestroyed()
    {
        base.OnModuleDestroyed();

        // A destroyed laser stops at once — no more damage, and the beam is let go (it fades with the wreck).
        foreach (var beam in beams)
        {
            beam.activeLeft = 0f;
            beam.fadeLeft = 0f;
            SetBeamVisible(beam, false);
        }
    }
}
