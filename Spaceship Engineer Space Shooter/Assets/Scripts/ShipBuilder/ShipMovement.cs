using UnityEngine;

/// <summary>
/// Moves the ship toward wherever ShipJoystickController points, at a speed derived from total
/// engine thrust vs total ship mass (see ShipGrid.ComputeEnginePower/ComputeTotalMass) — a heavier
/// ship, or one that's lost engines mid-fight, moves slower. Consumes energy from ShipEnergySystem
/// while thrusting; loses thrust (and coasts to a stop) if the pool runs dry. Speed changes gradually — see ShipInertia.
///
/// Only moves while ShipIdentity.CombatActive is true, so the player can't drive the ship during the
/// arrival/countdown intro — same gate WeaponModule already uses for firing.
///
/// Position is set directly (no Rigidbody2D), same style BattleSequenceController's own arrival
/// animation already uses for this same transform — nothing about ship movement in this project is
/// physics-driven.
///
/// SETUP: put on the ship root, alongside ShipGrid/ShipIdentity/ShipEnergySystem.
/// </summary>
[RequireComponent(typeof(ShipGrid), typeof(ShipIdentity))]
public class ShipMovement : MonoBehaviour
{
    [Tooltip("Multiplies thrust/mass into an actual world-units-per-second speed — tune to taste.")]
    public float speedMultiplier = 1f;
    [Tooltip("FALLBACK only: energy per second at full thrust, used when none of the ship's engines has an Energy Delta set. Normally the engines' own Energy Delta (added up) is what moving costs.")]
    public float energyPerSecondAtFullThrust = 5f;

    [Header("Inertia")]
    [Tooltip("Seconds to get from a standstill to full speed. Higher = a more sluggish, heavier feel.")]
    public float accelerationSeconds = 1.2f;
    [Tooltip("Seconds to come to a stop from full speed (letting go of the stick, or turning around).")]
    public float brakeSeconds = 0.8f;
    [Tooltip("When the stick points more than this many degrees away from the way the ship is flying, the ship first " +
             "brakes down to a standstill and only then accelerates the new way. A smaller change is followed gradually.")]
    [Range(5f, 170f)] public float turnBrakeAngle = 40f;

    [Tooltip("Falls back to Camera.main if left unset.")]
    public Camera battleCamera;
    [Tooltip("World-space gap kept between the ship's own edge and the camera's edge.")]
    public float screenMargin = 0.5f;

    private ShipGrid grid;
    private ShipIdentity identity;
    private ShipEnergySystem energy;
    private EngineModule[] engines = System.Array.Empty<EngineModule>();

    /// <summary>Set every frame by ShipJoystickController — magnitude 0..1, direction is where the stick points.</summary>
    private Vector2 inputDirection;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        identity = GetComponent<ShipIdentity>();
        energy = GetComponent<ShipEnergySystem>();
    }

    private void OnEnable()
    {
        grid.OnShipChanged += RefreshEngineList; // a built/destroyed engine should (dis)appear from the effect update immediately
        RefreshEngineList();
    }

    private void OnDisable() => grid.OnShipChanged -= RefreshEngineList;

    private void RefreshEngineList() => engines = GetComponentsInChildren<EngineModule>();

    public void SetInput(Vector2 direction) => inputDirection = direction;

    /// <summary>Energy per second while moving at full thrust: the living engines' Energy Delta added up (so
    /// a lost engine also costs less). If no engine has one set, the old Energy Per Second At Full Thrust is used.</summary>
    private float MoveEnergyPerSecond
    {
        get
        {
            float fromEngines = grid.ComputeMoveEnergyPerSecond();
            return fromEngines > 0f ? fromEngines : energyPerSecondAtFullThrust;
        }
    }

    private ShipBody body; // added by RamCollision the first time an enemy ship touches ours

    private Vector2 velocity;

    /// <summary>Current velocity of the ship, in world units per second.</summary>
    public Vector2 Velocity => velocity;

    private void Update()
    {
        float dt = Time.deltaTime;

        // Cached per-frame rather than re-derived inside the early-returns below, so every engine's
        // burn effect correctly drops back to idle the instant the player lets go or runs out of
        // energy — not just while actually moving.
        float strength = identity.CombatActive ? Mathf.Clamp01(inputDirection.magnitude) : 0f;
        float maxSpeed = GetMaxSpeed();

        // What the stick asks for. Without energy there is no thrust (no speed asked for) — the ship just slows down.
        Vector2 desired = Vector2.zero;
        if (strength >= 0.01f)
        {
            float energyCost = MoveEnergyPerSecond * strength * dt;
            if (energy != null && !energy.TrySpend(energyCost, ShipEnergySystem.EnergyPriorityGroup.Movement))
                strength = 0f; // out of power — no thrust, engines ease back to idle
            else
                desired = inputDirection.normalized * maxSpeed * strength;
        }

        // Inertia: the velocity follows the stick gradually — see ShipInertia.
        velocity = ShipInertia.Step(velocity, desired, maxSpeed, accelerationSeconds, brakeSeconds, turnBrakeAngle, dt);

        if (velocity.sqrMagnitude > 0.000001f && dt > 0f)
        {
            Vector3 step = (Vector3)(velocity * dt);
            if (body != null || TryGetComponent(out body)) step = body.FilterMove(step); // a collision with an enemy ship stops us / blocks the way into it

            Vector3 before = transform.position;
            transform.position = ClampToView(before + step);

            // What really happened is the new velocity — a screen edge or another ship takes the speed away.
            velocity = (Vector2)(transform.position - before) / dt;
        }

        // Disabled (last Cockpit or last Generator gone) means no crew left to run anything — engines go
        // fully dark rather than just easing to idle, same treatment EnemyShip gives a disabled enemy.
        foreach (var engine in engines)
        {
            if (engine == null) continue;
            if (identity.IsDisabled) engine.SetEffectRunning(false);
            else engine.SetThrustStrength(strength);
        }
    }

    /// <summary>Top speed at full joystick deflection, in world units/second. Includes the crew Ion/Jet engine
    /// power perks (via ShipGrid.ComputeEnginePower).</summary>
    public float GetMaxSpeed()
    {
        float mass = Mathf.Max(0.01f, grid.ComputeTotalMass());
        float baseSpeed = grid.ComputeEnginePower() / mass * speedMultiplier;
        return baseSpeed;
    }

    /// <summary>Clamps a candidate root position so the ship's actual built SHIP BOUNDS (not just its
    /// root point) stay fully inside the camera's view — reuses the same ship-bounds math
    /// BattleSequenceController's arrival positioning already relies on.</summary>
    private Vector3 ClampToView(Vector3 desiredRootPosition)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) return desiredRootPosition;

        Vector3 rootToShipCenter = grid.GetShipWorldCenter() - transform.position;
        Vector3 desiredShipCenter = desiredRootPosition + rootToShipCenter;

        Vector2 halfShipSize = grid.GetShipWorldSize() * 0.5f;
        float halfCamHeight = cam.orthographicSize;
        float halfCamWidth = halfCamHeight * cam.aspect;
        Vector3 camCenter = cam.transform.position;

        float minX = camCenter.x - halfCamWidth + halfShipSize.x + screenMargin;
        float maxX = camCenter.x + halfCamWidth - halfShipSize.x - screenMargin;
        float minY = camCenter.y - halfCamHeight + halfShipSize.y + screenMargin;
        float maxY = camCenter.y + halfCamHeight - halfShipSize.y - screenMargin;

        // If the ship is bigger than the screen on an axis (min > max once inset), hold it centered
        // on that axis instead of jittering between two invalid clamp bounds.
        float clampedX = minX <= maxX ? Mathf.Clamp(desiredShipCenter.x, minX, maxX) : camCenter.x;
        float clampedY = minY <= maxY ? Mathf.Clamp(desiredShipCenter.y, minY, maxY) : camCenter.y;

        Vector3 clampedShipCenter = new(clampedX, clampedY, desiredShipCenter.z);
        return clampedShipCenter - rootToShipCenter;
    }
}
