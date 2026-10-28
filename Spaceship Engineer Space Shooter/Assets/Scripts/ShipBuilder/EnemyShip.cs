using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One block of this enemy ship: which BlockDefinition it is and where it sits. Referencing the
/// BlockDefinition asset directly (drag it in) rather than typing an id string means enemy-only blocks
/// never need registering in any BlockDatabase — create them (ShipBuilder/Block Definition) and
/// reference them here only.</summary>
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

/// <summary>One stop on an enemy ship's attack route — a hand-placed point in the battle scene it travels
/// to, and how long it lingers there before continuing to the next one. See EnemyShip.route /
/// WaveEnemyEntry.route.</summary>
[System.Serializable]
public class EnemyShipWaypoint
{
    public Transform point;
    [Tooltip("Seconds the ship pauses here before continuing to the next point.")]
    public float holdSeconds = 1f;
}

/// <summary>
/// A hand-built enemy ship prefab. THE RECIPE: put this component, ShipGrid and ShipIdentity all on the
/// same prefab root, and list every block and its grid position in Blocks (ShipIdentity's own Faction
/// field can be left at its default — this overwrites it to Enemy at Awake). Then drop the finished
/// prefab straight into a wave's WaveEnemyEntry — same as MeteorType1; EnemySpawner already Instantiates
/// whatever isn't a Meteor at the wave's spawn point, and hands this component its Route (if that entry
/// set one) and its battle camera reference, with no other wiring needed. Make as many of these prefabs
/// as you like, one per enemy design — they all follow this same recipe.
///
/// On spawn it assembles itself from Blocks via ShipGrid.BuildFromDefinitions — the exact same per-block
/// HP and damage reactions the player's own ship uses, so enemies are real modular ships, destructible
/// piece by piece. It's given a fixed, strictly VERTICAL facing (see Face Down — never angled toward
/// wherever the player happens to be), starts fighting immediately (every WeaponModule on it fires on its
/// own — see WeaponModule), and then moves according to ONE of two behaviors:
///  - ROUTE SET (WaveEnemyEntry.route, or hand-authored directly on the prefab): travels from point to
///    point in order, pausing at each for its own Hold Seconds, then holds at the last one.
///  - NO ROUTE (the default): advances straight ahead (whichever way it's facing) until it's FULLY inside
///    the camera's view, continues a random extra distance up to Extra Approach Fraction of its own size
///    (so ships don't all stop at exactly the same depth), then maneuvers side to side for the rest of
///    the fight — bouncing off the edges of the visible screen rather than wandering off it.
///
/// Either way, it keeps fighting until DISABLED: its last Generator destroyed, or its Cockpit destroyed
/// (ShipGrid.CheckShipDisabled — the exact same rule the player's own ship uses). That immediately
/// overrides whatever it was doing above — it goes quiet (stops firing) and drifts slowly toward the
/// player's ship like a dead hulk instead. It can still be shot apart block by block, it just never
/// fights back or moves under its own power again.
/// </summary>
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

    // Default-behavior state
    private bool fullyInViewReached;
    private float extraApproachRemaining;
    private int maneuverDirection = 1; // +1 or -1 along transform.right

    // Route-following state
    private int routeIndex;
    private float holdTimer;

    private void Awake()
    {
        grid = GetComponent<ShipGrid>();
        identity = GetComponent<ShipIdentity>();

        grid.BuildFromDefinitions(blocks.Select(p => (p.block, p.position, p.rotationSteps)), Faction.Enemy);

        // ShipGrid.CurrentViewMode defaults to Building — fine for the player's ship, which
        // BattleSequenceController explicitly switches to Preview once it's loaded into the battle scene,
        // but nothing does that for an enemy ship otherwise. Left at Building, EngineModule.ApplyViewMode
        // keeps every thrust effect permanently stopped (idle animations stay off too) — this is the
        // "closed/fighting" look, same as the player's own ship in battle.
        grid.SetViewMode(ShipViewMode.Preview);

        // WeaponModule.EnergyCostPerShot (and this ship's own movement, below) check ShipEnergySystem.
        // TrySpend — without one, energy is simply never checked, so this ship needs a real pool for the
        // fight to be properly balanced, whether or not the prefab happens to already have this component.
        energy = GetComponent<ShipEnergySystem>();
        if (energy == null) energy = gameObject.AddComponent<ShipEnergySystem>();

        // Collision damage against the player's ship — see RamCollision. Add one to the prefab yourself to tune it.
        if (!TryGetComponent<RamCollision>(out _)) gameObject.AddComponent<RamCollision>();
        if (splitsIntoFragments && !TryGetComponent<ShipSplitter>(out _)) gameObject.AddComponent<ShipSplitter>();
    }

    private void OnEnable()
    {
        identity.OnShipDisabled += HandleDisabled;
        grid.OnShipChanged += HandleShipChanged; // a destroyed engine should stop contributing to speed/thrust immediately; losing the last weapon sends the ship ramming
        HandleShipChanged(); // also records whether the ship starts out armed
    }

    private void OnDisable()
    {
        identity.OnShipDisabled -= HandleDisabled;
        grid.OnShipChanged -= HandleShipChanged;
    }

    private void RefreshEngineList() => engines = GetComponentsInChildren<EngineModule>();

    private ShipBody body;

    /// <summary>Runs every self-propelled step through the ship's ShipBody, so a ship that has just collided with the
    /// player's ship stops, and can't push on into it.</summary>
    private Vector3 Filter(Vector3 step)
    {
        if (body == null) body = ShipBody.GetOrAdd(this);
        return body.FilterMove(step);
    }

    private Vector2 velocity;

    /// <summary>Current velocity of the ship, in world units per second.</summary>
    public Vector2 Velocity => velocity;

    /// <summary>Moves the ship one step toward `desired` velocity WITH inertia (see ShipInertia): it speeds up and slows
    /// down gradually, and shedding the old speed comes before accelerating a new way. `accelReference` is the speed
    /// the Acceleration / Brake times are measured against (default: the ship's top speed). `clampToZone` keeps it
    /// inside its play zone; `hitSide` says it ran into a side wall. Returns how far it actually moved.</summary>
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

        // What really happened is the new velocity — a wall or another ship takes the speed away.
        velocity = (Vector2)(target - before) / dt;
        return target - before;
    }

    private bool hadWeapons;

    private void HandleShipChanged()
    {
        RefreshEngineList();

        // Armed at some point, and every weapon is gone now: stop maneuvering and ram.
        bool hasWeapons = grid.Blocks.Any(m => m is WeaponModule && !m.IsDestroyed);
        if (hasWeapons) hadWeapons = true;
        else if (hadWeapons && ramWhenUnarmed && !disabled && state != State.Ramming) state = State.Ramming;
    }

    // ---------- Drawing order: every enemy ship on a layer of its own ----------

    /// <summary>Order in Layer of broken-off fragments: all of them share this one layer, under every ship.</summary>
    public const int FragmentSortingOrder = 0;

    private static readonly List<EnemyShip> living = new();
    private UnityEngine.Rendering.SortingGroup sortingGroup;

    /// <summary>Puts the whole ship into a SortingGroup, so its blocks are drawn together and never mix with another ship's.
    /// A ship that arrives later is drawn above the ones already there: its Order in Layer is one more than the highest of the
    /// ships still alive (1 for the first, never negative), so the numbers start again from 1 once the screen is clear.</summary>
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

        // Strictly vertical — blocks are authored facing up (local +Y), same convention the player's own
        // ship uses, so a Faction.Enemy ship needs Euler(0,0,180) to have that same "front" point DOWN,
        // toward a player who's typically below it. Deliberately NOT aimed at the player's actual
        // position (that used to rotate the whole ship toward wherever they were standing) — see Face Down.
        transform.rotation = faceDown ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;

        bool hasRoute = route.Count > 0 && route.Any(w => w.point != null);
        state = hasRoute ? State.FollowingRoute : ramsPlayer ? State.Ramming : State.Approaching;
        if (state == State.Approaching) PlaceAtEntry();

        grid.RefreshWeaponLayers(); // the ship now faces down: "higher on the screen" is the other way round than when it was built

        identity.SetCombatActive(true); // starts fighting the instant it appears — no arrival intro like the player gets
    }

    private Vector2 driftDirection = Vector2.down;
    private float driftSpin;

    private void HandleDisabled(ShipIdentity _)
    {
        disabled = true;

        // From now on it is debris: one random direction roughly down the screen and a spin of its own, kept for good.
        driftDirection = ShipFragment.RandomDownDirection(debrisDriftAngle);
        driftSpin = Random.Range(-fragmentMaxSpinDegrees, fragmentMaxSpinDegrees);
        identity.SetCombatActive(false); // no crew left to run the guns
        StopEngines();
    }

    /// <summary>Top speed right now, in world units/second — computed exactly like the player's own ship
    /// (ShipMovement.GetMaxSpeed): total engine thrust over total mass, so a heavier hull or one that's
    /// lost engines moves slower. Drives every kind of movement this ship does under its own power
    /// (approach, route travel, the default maneuver) — not the disabled drift, which is a powerless
    /// hulk, not a piloted move.</summary>
    public float GetMaxSpeed()
    {
        float mass = Mathf.Max(0.01f, grid.ComputeTotalMass());
        return grid.ComputeEnginePower() / mass * speedMultiplier;
    }

    /// <summary>The speed to actually move at THIS frame: GetMaxSpeed(), or 0 if the ship can't afford
    /// Energy Per Second At Full Thrust worth of energy from its pool right now — same "doesn't limp,
    /// just stops" rule ShipMovement follows. Also drives the engines' burn effect, same as ShipMovement.</summary>
    private float ConsumeMoveSpeed()
    {
        float speed = GetMaxSpeed();
        bool moving = speed > 0.001f;

        float perSecond = grid.ComputeMoveEnergyPerSecond(); // the living engines' Energy Delta; falls back to the field below
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

    /// <summary>Fully stops every engine's burn effect (not merely idling — see EngineModule.
    /// SetEffectRunning) once the ship is disabled. No crew left to even idle them.</summary>
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

    // ---------- Disabled: drifts toward Target, ignoring everything else ----------

    private void UpdateDisabledDrift()
    {
        // A powerless hulk: keeps whatever speed it had, and settles into a steady drift in its own direction
        // (roughly down the screen) while turning slowly — it does not steer toward anything.
        Drive(driftDirection * disabledDriftSpeed, accelReference: Mathf.Max(0.05f, disabledDriftSpeed));
        transform.RotateAround(grid.GetShipWorldCenter(), Vector3.forward, driftSpin * Time.deltaTime); // spins about its own middle: the middle flies straight

        if (ShipFragment.HasLeftScreen(identity, debrisDespawnMargin)) Destroy(gameObject); // gone off the screen: stop counting as an enemy
    }

    // ---------- Entry: always from above, always inside the allowed width ----------

    /// <summary>A ship on the default behavior advances straight along its facing (down) and never steers
    /// sideways while approaching — so it is only ever visible if it STARTS inside the allowed width. The
    /// spawner may put it anywhere along an edge (or on a side edge); this pulls it to a legal entry point: its
    /// whole width inside the play bounds (screen width minus the margin — computed from the CURRENT camera,
    /// so a bigger view after hangar upgrades widens the range automatically) and, if it isn't already
    /// above the screen, just beyond the top edge, from where it flies in.</summary>
    private void PlaceAtEntry()
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic || !TryGetPlayBounds(out var min, out var max)) return;

        Vector2 half = grid.GetShipWorldSize() * 0.5f;
        Vector3 centerOffset = grid.GetShipWorldCenter() - transform.position;
        Vector3 center = transform.position + centerOffset;

        float minX = min.x + half.x;
        float maxX = max.x - half.x;
        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : (min.x + max.x) * 0.5f; // wider than the zone: centered

        float aboveTop = cam.transform.position.y + cam.orthographicSize + half.y + 0.5f;
        if (center.y < aboveTop) center.y = aboveTop;

        transform.position = center - centerOffset;
    }

    // ---------- Ramming: charges the player's nearest block and keeps pushing ----------

    private ShipIdentity targetIdentity;

    /// <summary>Flies at real (engine-computed) speed toward the player's nearest living block — NOT held
    /// inside the maneuver zone and never turning aside. Once it arrives it keeps pressing into the target, so
    /// the collision (RamCollision) repeats until one side's blocks are gone. Holds position if there is
    /// nothing left to hit.</summary>
    private void UpdateRamming()
    {
        if (targetIdentity == null && target != null) targetIdentity = target.GetComponentInParent<ShipIdentity>();

        Vector2 from = grid.GetShipWorldCenter();
        Vector2 aim;
        if (targetIdentity != null && targetIdentity.TryGetNearestBlockPosition(from, out var block)) aim = block;
        else if (target != null && targetIdentity == null) aim = target.position;
        else { ConsumeMoveSpeed(); Drive(Vector2.zero); return; } // the target has no blocks left — nothing to ram

        float speed = ConsumeMoveSpeed();
        Vector2 toAim = aim - from;
        float distance = toAim.magnitude;

        // Full speed at the target — a ram does not slow down.
        Drive(speed > 0.001f && distance >= 0.01f ? toAim / distance * speed : Vector2.zero);
    }

    // ---------- Default behavior: advance into view, then maneuver ----------

    private void UpdateApproaching()
    {
        float speed = ConsumeMoveSpeed();
        Vector3 moved = Drive((Vector2)transform.up * speed); // "up" is the fixed facing set in Start

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
        transform.position = ClampToView(transform.position); // wherever Approaching left it, settle within the safe zone right away
    }

    /// <summary>The area this ship is allowed to occupy while APPROACHING or MANEUVERING (never while
    /// disabled — UpdateDisabledDrift ignores this entirely): the full camera width, inset by Edge
    /// Margin Cells (ship grid cells) on every side, but only the TOP Maneuver Zone Height Fraction of the
    /// camera's height — so it settles and maneuvers up near the top of the screen and never advances
    /// further down toward the player while it's still able to maneuver under its own power. False (with
    /// min/max left at zero) if there's no camera to measure against.</summary>
    private bool TryGetPlayBounds(out Vector2 min, out Vector2 max)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) { min = max = Vector2.zero; return false; }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float margin = edgeMarginCells * grid.cellSize; // a whole number of ship cells, kept on every side
        Vector3 camCenter = cam.transform.position;
        float top = camCenter.y + halfHeight;

        min = new Vector2(camCenter.x - halfWidth + margin, top - maneuverZoneHeightFraction * (halfHeight * 2f));
        max = new Vector2(camCenter.x + halfWidth - margin, top - margin);
        return true;
    }

    /// <summary>Whether the ship's whole footprint currently sits inside its allowed play bounds (see
    /// TryGetPlayBounds) — gates when Approaching hands off to Maneuvering.</summary>
    private bool IsFullyInView()
    {
        if (!TryGetPlayBounds(out var min, out var max)) return true; // nothing to measure against — don't block forever

        Vector2 center = grid.GetShipWorldCenter();
        Vector2 half = grid.GetShipWorldSize() * 0.5f;

        return center.x - half.x >= min.x && center.x + half.x <= max.x
            && center.y - half.y >= min.y && center.y + half.y <= max.y;
    }

    /// <summary>Side-to-side maneuvering: moves along the ship's own right/left at real (engine-computed)
    /// speed, bouncing off the edges of its allowed play bounds instead of wandering past them.</summary>
    private void UpdateManeuvering()
    {
        float speed = ConsumeMoveSpeed();
        Drive((Vector2)transform.right * maneuverDirection * speed, clampToZone: true, out bool hitSide);

        // Ran into the side wall: turn around — it brakes first (it has no speed left along X anyway), then sets off back.
        if (hitSide) maneuverDirection = -maneuverDirection;
    }

    /// <summary>Clamps a candidate root position so the ship's actual built bounds stay fully inside its
    /// allowed play bounds (see TryGetPlayBounds) — same clamp-a-box-within-a-box approach ShipMovement.
    /// ClampToView uses to keep the player's own ship on screen, just against a smaller box here.</summary>
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

        // If the ship is bigger than the zone on an axis (min > max once inset), hold it centered on
        // that axis instead of jittering between two invalid clamp bounds.
        float clampedX = minX <= maxX ? Mathf.Clamp(desiredShipCenter.x, minX, maxX) : (zoneMin.x + zoneMax.x) * 0.5f;
        float clampedY = minY <= maxY ? Mathf.Clamp(desiredShipCenter.y, minY, maxY) : (zoneMin.y + zoneMax.y) * 0.5f;

        Vector3 clampedShipCenter = new(clampedX, clampedY, desiredShipCenter.z);
        return clampedShipCenter - rootToShipCenter;
    }

    // ---------- Route: travels point to point, pausing Hold Seconds at each ----------

    private void UpdateRoute()
    {
        if (routeIndex >= route.Count)
        {
            if (ramsPlayer) state = State.Ramming; // route finished: charge
            else Drive(Vector2.zero);              // otherwise it comes to a stop at the last point
            return;
        }

        var waypoint = route[routeIndex];
        if (waypoint.point == null) { routeIndex++; return; }

        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            Drive(Vector2.zero); // waiting: it brakes and holds
            return;
        }

        float speed = ConsumeMoveSpeed();

        Vector2 toPoint = (Vector2)waypoint.point.position - (Vector2)transform.position;
        float distance = toPoint.magnitude;

        // Arrived: close enough, and slow enough that it won't just carry on past the point.
        if (distance <= 0.2f && velocity.magnitude < Mathf.Max(0.3f, GetMaxSpeed() * 0.15f))
        {
            holdTimer = waypoint.holdSeconds;
            routeIndex++;
            return;
        }

        // Out of power: no thrust (it coasts down). Otherwise head for the point, easing off early enough to stop on it:
        // the speed from which it can still brake to a halt within the distance left.
        float desiredSpeed = speed;
        float brakeAcceleration = Mathf.Max(0.01f, GetMaxSpeed()) / Mathf.Max(0.05f, brakeSeconds);
        desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Sqrt(2f * brakeAcceleration * distance));

        Drive(speed > 0.001f && distance > 0.0001f ? toPoint / distance * desiredSpeed : Vector2.zero);
    }
}
