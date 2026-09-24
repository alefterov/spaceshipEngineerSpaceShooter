using UnityEngine;

/// <summary>
/// Moves the ship toward wherever ShipJoystickController points, at a speed derived from total
/// engine thrust vs total ship mass (see ShipGrid.ComputeEnginePower/ComputeTotalMass) — a heavier
/// ship, or one that's lost engines mid-fight, moves slower. Consumes energy from ShipEnergySystem
/// while thrusting; simply stops (doesn't limp along at reduced speed) if the pool runs dry.
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
    [Tooltip("Energy spent per second while thrusting at full joystick deflection — scales down with a lighter push.")]
    public float energyPerSecondAtFullThrust = 5f;

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

    private void Update()
    {
        // Cached per-frame rather than re-derived inside the early-returns below, so every engine's
        // burn effect correctly drops back to idle the instant the player lets go or runs out of
        // energy — not just while actually moving.
        float strength = identity.CombatActive ? Mathf.Clamp01(inputDirection.magnitude) : 0f;

        if (strength >= 0.01f)
        {
            float energyCost = energyPerSecondAtFullThrust * strength * Time.deltaTime;
            if (energy != null && !energy.TrySpend(energyCost))
            {
                strength = 0f; // out of power — hold position, engines ease back to idle
            }
            else
            {
                float speed = GetMaxSpeed() * strength;
                Vector3 delta = (Vector3)(inputDirection.normalized * speed * Time.deltaTime);
                transform.position = ClampToView(transform.position + delta);
            }
        }

        foreach (var engine in engines)
            if (engine != null) engine.SetThrustStrength(strength);
    }

    /// <summary>Top speed at full joystick deflection, in world units/second.</summary>
    public float GetMaxSpeed()
    {
        float mass = Mathf.Max(0.01f, grid.ComputeTotalMass());
        return grid.ComputeEnginePower() / mass * speedMultiplier;
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
