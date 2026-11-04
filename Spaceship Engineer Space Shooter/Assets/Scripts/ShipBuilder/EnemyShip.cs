using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class EnemyShipBlockPlacement
{
    public BlockDefinition block;
    [Tooltip("Anchor/pivot cell in this ship's own grid — same coordinate system the player's builder " +
             "uses, (0,0) at the bottom-left. This is the same cell the block's shape is defined relative to.")]
    public Vector2Int position;
    [Tooltip("0-3, ×90° clockwise — same rotation convention as the player's builder.")]
    public int rotationSteps;
}

[System.Serializable]
public class EnemyShipWaypoint
{
    public Transform point;
    [Tooltip("Seconds the ship pauses here before continuing to the next point.")]
    public float holdSeconds = 1f;
}

[RequireComponent(typeof(ShipGrid), typeof(ShipIdentity))]
public class EnemyShip : MonoBehaviour
{
    [Tooltip("Every block this ship is built from and where it sits.")]
    public List<EnemyShipBlockPlacement> blocks = new();

    [Header("Facing")]
    [Tooltip("Strictly vertical — never angled toward wherever the player's ship actually is. On: faces " +
             "straight down (the usual case — blocks are authored facing up, like the player's own ship, " +
             "so this points a ship spawned at the top of the screen down toward the player below it). " +
             "Off: faces straight up, unrotated, for a ship meant to fight from below/behind the player.")]
    public bool faceDown = true;

    [Header("Attack route (optional)")]
    [Tooltip("Ordered points this ship travels to and pauses at, in order — set here for a hand-placed " +
             "ship, or left for EnemySpawner to fill in from the wave entry that spawned it. Empty means " +
             "the default approach-then-maneuver behavior below is used instead.")]
    public List<EnemyShipWaypoint> route = new();

    [Header("Movement (computed like the player's own ship — see ShipMovement)")]
    [Tooltip("Multiplies thrust/mass into an actual world-units-per-second speed — same tunable as " +
             "ShipMovement.speedMultiplier. Drives approach, route travel AND the default maneuver below " +
             "— everything this ship does under its own power uses this one real speed.")]
    public float speedMultiplier = 1f;
    [Tooltip("How many times faster than its normal (thrust / mass) speed the ship flies while ENTERING the screen, before it starts " +
             "to maneuver. Still follows the engines and weight: a heavy ship enters slower than a light one, just twice as fast as it " +
             "would otherwise.")]
    [Min(0.1f)] public float approachSpeedMultiplier = 2f;
    [Tooltip("Energy spent per second while under way — same convention as ShipMovement." +
             "energyPerSecondAtFullThrust. Out of energy simply means the ship holds position, same " +
             "\"doesn't limp\" rule the player's own ship follows.")]
    public float energyPerSecondAtFullThrust = 5f;

    [Header("Ramming")]
    [Tooltip("A battering ram: instead of hovering at the top of the screen, this ship flies straight at the " +
             "player's ship and keeps pushing into it, trading collision damage block by block (see RamCollision). " +
             "With a Route set it first follows the route to its last point and then rams. Collision damage itself " +
             "happens for ANY enemy ship that touches the player's ship, rammer or not.")]
    public bool ramsPlayer;
    [Tooltip("When every weapon this ship started with has been destroyed, it gives up on maneuvering and goes to ram " +
             "the player's ship, exactly like a Rams Player ship. A ship built without any weapon is not affected.")]
    public bool ramWhenUnarmed = true;

    [Header("Default behavior (used only when Route is empty)")]
    [Tooltip("Falls back to Camera.main if left unset. EnemySpawner fills this in automatically for a " +
             "wave-spawned ship.")]
    public Camera battleCamera;
    [Tooltip("Extra distance traveled past the point where the ship first becomes fully visible, " +
             "randomized between 0 and this many multiples of the ship's own size (0.5 = up to half its " +
             "length) — pure variety, so ships don't all stop at exactly the same depth.")]
    public float extraApproachFraction = 0.5f;
    [Tooltip("Gap kept between the ship's own edge and the camera's edge — both where it enters and settles, and the " +
             "bounds it maneuvers within afterward — measured in cells of the ship grid (1 = one cell). The ship " +
             "therefore stays fully on the screen, near the left and right borders but never touching them.")]
    [Min(0f)] public float edgeMarginCells = 1f;
    [Tooltip("How much of the camera's visible HEIGHT, measured down from the top edge, this ship is " +
             "allowed to occupy while approaching or maneuvering (0.4 = the top 40% of the screen) — it " +
             "never advances further down toward the player while it's still able to maneuver. Ignored " +
             "entirely once disabled: the drift toward the player then goes wherever it needs to, all the " +
             "way down if that's where the player is.")]
    [Range(0.1f, 1f)] public float maneuverZoneHeightFraction = 0.4f;

    [Header("Inertia")]
    [Tooltip("Seconds to get from a standstill to full speed. Higher = a heavier, more sluggish ship.")]
    public float accelerationSeconds = 1.2f;
    [Tooltip("Seconds to come to a stop from full speed.")]
    public float brakeSeconds = 0.8f;
    [Tooltip("When it has to go more than this many degrees away from where it is heading, it first brakes to a standstill, " +
             "then accelerates the new way. A smaller change is followed gradually.")]
    [Range(5f, 170f)] public float turnBrakeAngle = 40f;

    [Header("Breaking apart")]
    [Tooltip("When destroyed blocks cut the ship in two, the part without the cockpit tears loose and becomes a " +
             "separate lifeless object (see ShipSplitter / ShipFragment).")]
    public bool splitsIntoFragments = true;
    [Tooltip("World units per second a broken-off part drifts toward the player's ship.")]
    public float fragmentDriftSpeed = 0.5f;
    [Tooltip("The most a broken-off part turns per second (degrees); each one gets a random spin up to this, either way.")]
    public float fragmentMaxSpinDegrees = 30f;
    [Tooltip("A broken-off part and a disabled ship drift in a random direction within this many degrees of straight down " +
             "the screen, each at its own spin (up to Fragment Max Spin Degrees) — they do not home in on the player.")]
    public float debrisDriftAngle = 20f;
    [Tooltip("Debris (a disabled ship or a broken-off part) is removed once it is this many world units beyond the bottom or a side of the screen.")]
    public float debrisDespawnMargin = 2f;

    [Header("Once disabled")]
    [Tooltip("World units per second this ship drifts once its last Generator or its " +
             "Cockpit is destroyed — overrides Route/the default behavior above entirely. A heavier hull " +
             "can be given a slower drift.")]
    public float disabledDriftSpeed = 0.5f;
    [Tooltip("What it drifts toward once disabled — normally the player's ship. Falls back to searching " +
             "for it by tag if left unset.")]
    public Transform target;

    private enum State { Approaching, Maneuvering, FollowingRoute, Ramming }
    private State state;

    private ShipGrid grid;
    private ShipIdentity identity;
    private ShipEnergySystem energy;
    private EngineModule[] engines = System.Array.Empty<EngineModule>();
    private bool disabled;

    private bool fullyInViewReached;
    private float extraApproachRemaining;
    private int maneuverDirection = 1;

    private int routeIndex;
    private float holdTimer;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        identity = GetComponent<ShipIdentity>();

        grid.BuildFromDefinitions(blocks.Select(p => (p.block, p.position, p.rotationSteps)), Faction.Enemy);

        grid.SetViewMode(ShipViewMode.Preview);

        energy = GetComponent<ShipEnergySystem>();
        if (energy == null) energy = gameObject.AddComponent<ShipEnergySystem>();

        if (!TryGetComponent<RamCollision>(out _)) gameObject.AddComponent<RamCollision>();
        if (splitsIntoFragments && !TryGetComponent<ShipSplitter>(out _)) gameObject.AddComponent<ShipSplitter>();
    }

    private void OnEnable()
    {
        identity.OnShipDisabled += HandleDisabled;
        grid.OnShipChanged += HandleShipChanged;
        HandleShipChanged();
    }

    private void OnDisable()
    {
        identity.OnShipDisabled -= HandleDisabled;
        grid.OnShipChanged -= HandleShipChanged;
    }

    private void RefreshEngineList() => engines = GetComponentsInChildren<EngineModule>();

    private ShipBody body;

    private Vector3 Filter(Vector3 step)
    {
        if (body == null) body = ShipBody.GetOrAdd(this);
        return body.FilterMove(step);
    }

    private Vector2 velocity;

    public Vector2 Velocity => velocity;

    private Vector3 Drive(Vector2 desired, bool clampToZone = false, float accelReference = -1f)
        => Drive(desired, clampToZone, out _, accelReference);

    private Vector3 Drive(Vector2 desired, bool clampToZone, out bool hitSide, float accelReference = -1f)
    {
        hitSide = false;
        float dt = Time.deltaTime;
        if (dt <= 0f) return Vector3.zero;

        float reference = accelReference > 0f ? accelReference : GetMaxSpeed();
        velocity = ShipInertia.Step(velocity, desired, reference, accelerationSeconds, brakeSeconds, turnBrakeAngle, dt);

        Vector3 before = transform.position;
        Vector3 target = before + Filter((Vector3)(velocity * dt));

        if (clampToZone)
        {
            Vector3 clamped = ClampToView(target);
            hitSide = Mathf.Abs(clamped.x - target.x) > 0.0001f;
            target = clamped;
        }

        transform.position = target;

        velocity = (Vector2)(target - before) / dt;
        return target - before;
    }

    private bool hadWeapons;

    private void HandleShipChanged()
    {
        RefreshEngineList();

        bool hasWeapons = grid.Blocks.Any(m => m is WeaponModule && !m.IsDestroyed);
        if (hasWeapons) hadWeapons = true;
        else if (hadWeapons && ramWhenUnarmed && !disabled && state != State.Ramming) state = State.Ramming;
    }

    public const int FragmentSortingOrder = 0;

    private static readonly List<EnemyShip> living = new();
    private UnityEngine.Rendering.SortingGroup sortingGroup;

    private void AssignSortingLayer()
    {
        sortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sortingGroup == null) sortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();

        int highest = FragmentSortingOrder;
        foreach (var ship in living)
            if (ship != null && ship != this && ship.sortingGroup != null) highest = Mathf.Max(highest, ship.sortingGroup.sortingOrder);

        sortingGroup.sortingOrder = highest + 1;
        if (!living.Contains(this)) living.Add(this);
    }

    private void OnDestroy() => living.Remove(this);
    private void Start()
    {
        AssignSortingLayer();

        if (target == null)
        {
            var playerObj = GameObject.FindWithTag("PlayerShip");
            if (playerObj != null) target = playerObj.GetComponentInParent<ShipGrid>()?.transform;
        }

        transform.rotation = faceDown ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;

        bool hasRoute = route.Count > 0 && route.Any(w => w.point != null);
        state = hasRoute ? State.FollowingRoute : ramsPlayer ? State.Ramming : State.Approaching;
        if (state == State.Approaching) PlaceAtEntry();

        grid.RefreshWeaponLayers();

        identity.SetCombatActive(true);
    }

    private Vector2 driftDirection = Vector2.down;
    private float driftSpin;

    private void HandleDisabled(ShipIdentity _)
    {
        disabled = true;

        driftDirection = ShipFragment.RandomDownDirection(debrisDriftAngle);
        driftSpin = Random.Range(-fragmentMaxSpinDegrees, fragmentMaxSpinDegrees);
        identity.SetCombatActive(false);
        StopEngines();
    }

    public float GetMaxSpeed()
    {
        float mass = Mathf.Max(0.01f, grid.ComputeTotalMass());
        return grid.ComputeEnginePower() / mass * speedMultiplier;
    }

    private float ConsumeMoveSpeed()
    {
        float speed = GetMaxSpeed();
        bool moving = speed > 0.001f;

        float perSecond = grid.ComputeMoveEnergyPerSecond();
        if (perSecond <= 0f) perSecond = energyPerSecondAtFullThrust;
        if (moving && energy != null && !energy.TrySpend(perSecond * Time.deltaTime, ShipEnergySystem.EnergyPriorityGroup.Movement))
        {
            speed = 0f;
            moving = false;
        }

        foreach (var engine in engines)
            if (engine != null) engine.SetThrustStrength(moving ? 1f : 0f);

        return speed;
    }

    private void StopEngines()
    {
        foreach (var engine in engines)
            if (engine != null) engine.SetEffectRunning(false);
    }

    private void Update()
    {
        if (disabled) { UpdateDisabledDrift(); return; }

        switch (state)
        {
            case State.FollowingRoute: UpdateRoute(); break;
            case State.Approaching: UpdateApproaching(); break;
            case State.Maneuvering: UpdateManeuvering(); break;
            case State.Ramming: UpdateRamming(); break;
        }
    }

    private void UpdateDisabledDrift()
    {
        Drive(driftDirection * disabledDriftSpeed, accelReference: Mathf.Max(0.05f, disabledDriftSpeed));
        transform.RotateAround(grid.GetShipWorldCenter(), Vector3.forward, driftSpin * Time.deltaTime);

        if (ShipFragment.HasLeftScreen(identity, debrisDespawnMargin)) Destroy(gameObject);
    }

    private void PlaceAtEntry()
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic || !TryGetPlayBounds(out var min, out var max)) return;

        Vector2 half = grid.GetShipWorldSize() * 0.5f;
        Vector3 centerOffset = grid.GetShipWorldCenter() - transform.position;
        Vector3 center = transform.position + centerOffset;

        float minX = min.x + half.x;
        float maxX = max.x - half.x;
        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : (min.x + max.x) * 0.5f;

        float aboveTop = cam.transform.position.y + cam.orthographicSize + half.y + 0.5f;
        if (center.y < aboveTop) center.y = aboveTop;

        transform.position = center - centerOffset;
    }

    private ShipIdentity targetIdentity;

    private void UpdateRamming()
    {
        if (targetIdentity == null && target != null) targetIdentity = target.GetComponentInParent<ShipIdentity>();

        Vector2 from = grid.GetShipWorldCenter();
        Vector2 aim;
        if (targetIdentity != null && targetIdentity.TryGetNearestBlockPosition(from, out var block)) aim = block;
        else if (target != null && targetIdentity == null) aim = target.position;
        else { ConsumeMoveSpeed(); Drive(Vector2.zero); return; }

        float speed = ConsumeMoveSpeed();
        Vector2 toAim = aim - from;
        float distance = toAim.magnitude;

        Drive(speed > 0.001f && distance >= 0.01f ? toAim / distance * speed : Vector2.zero);
    }

    private void UpdateApproaching()
    {
        float speed = ConsumeMoveSpeed() * approachSpeedMultiplier;
        Vector3 moved = Drive((Vector2)transform.up * speed, accelReference: Mathf.Max(0.05f, speed));

        if (!fullyInViewReached)
        {
            if (!IsFullyInView()) return;

            fullyInViewReached = true;
            float shipLength = Mathf.Max(grid.GetShipWorldSize().x, grid.GetShipWorldSize().y);
            extraApproachRemaining = Random.Range(0f, shipLength * extraApproachFraction);
            return;
        }

        extraApproachRemaining -= moved.magnitude;
        if (extraApproachRemaining > 0f) return;

        state = State.Maneuvering;
        maneuverDirection = Random.value < 0.5f ? 1 : -1;
        transform.position = ClampToView(transform.position);
    }

    private bool TryGetPlayBounds(out Vector2 min, out Vector2 max)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) { min = max = Vector2.zero; return false; }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float margin = edgeMarginCells * grid.cellSize;
        Vector3 camCenter = cam.transform.position;
        float top = camCenter.y + halfHeight;

        min = new Vector2(camCenter.x - halfWidth + margin, top - maneuverZoneHeightFraction * (halfHeight * 2f));
        max = new Vector2(camCenter.x + halfWidth - margin, top - margin);
        return true;
    }

    private bool IsFullyInView()
    {
        if (!TryGetPlayBounds(out var min, out var max)) return true;

        Vector2 center = grid.GetShipWorldCenter();
        Vector2 half = grid.GetShipWorldSize() * 0.5f;

        return center.x - half.x >= min.x && center.x + half.x <= max.x
            && center.y - half.y >= min.y && center.y + half.y <= max.y;
    }

    private void UpdateManeuvering()
    {
        float speed = ConsumeMoveSpeed();
        Drive((Vector2)transform.right * maneuverDirection * speed, clampToZone: true, out bool hitSide);

        if (hitSide) maneuverDirection = -maneuverDirection;
    }

    private Vector3 ClampToView(Vector3 desiredRootPosition)
    {
        if (!TryGetPlayBounds(out var zoneMin, out var zoneMax)) return desiredRootPosition;

        Vector3 rootToShipCenter = grid.GetShipWorldCenter() - transform.position;
        Vector3 desiredShipCenter = desiredRootPosition + rootToShipCenter;
        Vector2 halfShipSize = grid.GetShipWorldSize() * 0.5f;

        float minX = zoneMin.x + halfShipSize.x;
        float maxX = zoneMax.x - halfShipSize.x;
        float minY = zoneMin.y + halfShipSize.y;
        float maxY = zoneMax.y - halfShipSize.y;

        float clampedX = minX <= maxX ? Mathf.Clamp(desiredShipCenter.x, minX, maxX) : (zoneMin.x + zoneMax.x) * 0.5f;
        float clampedY = minY <= maxY ? Mathf.Clamp(desiredShipCenter.y, minY, maxY) : (zoneMin.y + zoneMax.y) * 0.5f;

        Vector3 clampedShipCenter = new(clampedX, clampedY, desiredShipCenter.z);
        return clampedShipCenter - rootToShipCenter;
    }

    private void UpdateRoute()
    {
        if (routeIndex >= route.Count)
        {
            if (ramsPlayer) state = State.Ramming;
            else Drive(Vector2.zero);
            return;
        }

        var waypoint = route[routeIndex];
        if (waypoint.point == null) { routeIndex++; return; }

        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            Drive(Vector2.zero);
            return;
        }

        float speed = ConsumeMoveSpeed();

        Vector2 toPoint = (Vector2)waypoint.point.position - (Vector2)transform.position;
        float distance = toPoint.magnitude;

        if (distance <= 0.2f && velocity.magnitude < Mathf.Max(0.3f, GetMaxSpeed() * 0.15f))
        {
            holdTimer = waypoint.holdSeconds;
            routeIndex++;
            return;
        }

        float desiredSpeed = speed;
        float brakeAcceleration = Mathf.Max(0.01f, GetMaxSpeed()) / Mathf.Max(0.05f, brakeSeconds);
        desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Sqrt(2f * brakeAcceleration * distance));

        Drive(speed > 0.001f && distance > 0.0001f ? toPoint / distance * desiredSpeed : Vector2.zero);
    }
}
