using UnityEngine;

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

    private Vector2 inputDirection;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        identity = GetComponent<ShipIdentity>();
        energy = GetComponent<ShipEnergySystem>();
    }

    private void OnEnable()
    {
        grid.OnShipChanged += RefreshEngineList;
        RefreshEngineList();
    }

    private void OnDisable() => grid.OnShipChanged -= RefreshEngineList;

    private void RefreshEngineList() => engines = GetComponentsInChildren<EngineModule>();

    public void SetInput(Vector2 direction) => inputDirection = direction;

    private float MoveEnergyPerSecond
    {
        get
        {
            float fromEngines = grid.ComputeMoveEnergyPerSecond();
            return fromEngines > 0f ? fromEngines : energyPerSecondAtFullThrust;
        }
    }

    private ShipBody body;

    private Vector2 velocity;

    public Vector2 Velocity => velocity;

    private void Update()
    {
        float dt = Time.deltaTime;

        float strength = identity.CombatActive ? Mathf.Clamp01(inputDirection.magnitude) : 0f;
        float maxSpeed = GetMaxSpeed();

        Vector2 desired = Vector2.zero;
        if (strength >= 0.01f)
        {
            float energyCost = MoveEnergyPerSecond * strength * dt;
            if (energy != null && !energy.TrySpend(energyCost, ShipEnergySystem.EnergyPriorityGroup.Movement))
                strength = 0f;
            else
                desired = inputDirection.normalized * maxSpeed * strength;
        }

        velocity = ShipInertia.Step(velocity, desired, maxSpeed, accelerationSeconds, brakeSeconds, turnBrakeAngle, dt);

        if (velocity.sqrMagnitude > 0.000001f && dt > 0f)
        {
            Vector3 step = (Vector3)(velocity * dt);
            if (body != null || TryGetComponent(out body)) step = body.FilterMove(step);

            Vector3 before = transform.position;
            transform.position = ClampToView(before + step);

            velocity = (Vector2)(transform.position - before) / dt;
        }

        foreach (var engine in engines)
        {
            if (engine == null) continue;
            if (identity.IsDisabled) engine.SetEffectRunning(false);
            else engine.SetThrustStrength(strength);
        }
    }

    public float GetMaxSpeed()
    {
        float mass = Mathf.Max(0.01f, grid.ComputeTotalMass());
        float baseSpeed = grid.ComputeEnginePower() / mass * speedMultiplier;
        return baseSpeed;
    }

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

        float clampedX = minX <= maxX ? Mathf.Clamp(desiredShipCenter.x, minX, maxX) : camCenter.x;
        float clampedY = minY <= maxY ? Mathf.Clamp(desiredShipCenter.y, minY, maxY) : camCenter.y;

        Vector3 clampedShipCenter = new(clampedX, clampedY, desiredShipCenter.z);
        return clampedShipCenter - rootToShipCenter;
    }
}
