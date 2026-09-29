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

    [Header("Default behavior (used only when Route is empty)")]
    [Tooltip("Falls back to Camera.main if left unset. EnemySpawner fills this in automatically for a " +
             "wave-spawned ship.")]
    public Camera battleCamera;
    [Tooltip("Extra distance traveled past the point where the ship first becomes fully visible, " +
             "randomized between 0 and this many multiples of the ship's own size (0.5 = up to half its " +
             "length) — pure variety, so ships don't all stop at exactly the same depth.")]
    public float extraApproachFraction = 0.5f;
    [Tooltip("Margin kept between the ship's own edge and the camera's edge — both where it settles " +
             "once it stops approaching, and the bounds it maneuvers within afterward — as a fraction of " +
             "the camera's visible WIDTH (0.05 = 5%), so it scales automatically with screen size/zoom " +
             "instead of a fixed world-unit gap.")]
    [Range(0f, 0.4f)] public float screenMarginFraction = 0.05f;
    [Tooltip("How much of the camera's visible HEIGHT, measured down from the top edge, this ship is " +
             "allowed to occupy while approaching or maneuvering (0.4 = the top 40% of the screen) — it " +
             "never advances further down toward the player while it's still able to maneuver. Ignored " +
             "entirely once disabled: the drift toward the player then goes wherever it needs to, all the " +
             "way down if that's where the player is.")]
    [Range(0.1f, 1f)] public float maneuverZoneHeightFraction = 0.4f;

    [Header("Once disabled")]
    [Tooltip("World units per second this ship drifts toward Target once its last Generator or its " +
             "Cockpit is destroyed — overrides Route/the default behavior above entirely. A heavier hull " +
             "can be given a slower drift.")]
    public float disabledDriftSpeed = 0.5f;
    [Tooltip("What it drifts toward once disabled — normally the player's ship. Falls back to searching " +
             "for it by tag if left unset.")]
    public Transform target;

    private enum State { Approaching, Maneuvering, FollowingRoute }
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
    }

    private void OnEnable()
    {
        identity.OnShipDisabled += HandleDisabled;
        grid.OnShipChanged += RefreshEngineList; // a destroyed engine should stop contributing to speed/thrust immediately
        RefreshEngineList();
    }

    private void OnDisable()
    {
        identity.OnShipDisabled -= HandleDisabled;
        grid.OnShipChanged -= RefreshEngineList;
    }

    private void RefreshEngineList() => engines = GetComponentsInChildren<EngineModule>();

    private void Start()
    {
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

        state = route.Count > 0 && route.Any(w => w.point != null) ? State.FollowingRoute : State.Approaching;

        identity.SetCombatActive(true); // starts fighting the instant it appears — no arrival intro like the player gets
    }

    private void HandleDisabled(ShipIdentity _)
    {
        disabled = true;
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

        if (moving && energy != null && !energy.TrySpend(energyPerSecondAtFullThrust * Time.deltaTime, ShipEnergySystem.EnergyPriorityGroup.Movement))
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
        }
    }

    // ---------- Disabled: drifts toward Target, ignoring everything else ----------

    private void UpdateDisabledDrift()
    {
        if (target == null) return;

        Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return; // already there — nothing left to close

        transform.position += (Vector3)(toTarget.normalized * disabledDriftSpeed * Time.deltaTime);
    }

    // ---------- Default behavior: advance into view, then maneuver ----------

    private void UpdateApproaching()
    {
        float speed = ConsumeMoveSpeed();
        transform.position += transform.up * speed * Time.deltaTime; // "up" is the fixed facing set in Start

        if (!fullyInViewReached)
        {
            if (!IsFullyInView()) return;

            fullyInViewReached = true;
            float shipLength = Mathf.Max(grid.GetShipWorldSize().x, grid.GetShipWorldSize().y);
            extraApproachRemaining = Random.Range(0f, shipLength * extraApproachFraction);
            return;
        }

        extraApproachRemaining -= speed * Time.deltaTime;
        if (extraApproachRemaining > 0f) return;

        state = State.Maneuvering;
        maneuverDirection = Random.value < 0.5f ? 1 : -1;
        transform.position = ClampToView(transform.position); // wherever Approaching left it, settle within the safe zone right away
    }

    /// <summary>The area this ship is allowed to occupy while APPROACHING or MANEUVERING (never while
    /// disabled — UpdateDisabledDrift ignores this entirely): the full camera width, inset by Screen
    /// Margin Fraction of that width on every side, but only the TOP Maneuver Zone Height Fraction of the
    /// camera's height — so it settles and maneuvers up near the top of the screen and never advances
    /// further down toward the player while it's still able to maneuver under its own power. False (with
    /// min/max left at zero) if there's no camera to measure against.</summary>
    private bool TryGetPlayBounds(out Vector2 min, out Vector2 max)
    {
        var cam = battleCamera != null ? battleCamera : Camera.main;
        if (cam == null || !cam.orthographic) { min = max = Vector2.zero; return false; }

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        float margin = screenMarginFraction * (halfWidth * 2f); // fraction of the screen's WIDTH, kept as the margin on every side
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
        Vector3 desired = transform.position + transform.right * maneuverDirection * speed * Time.deltaTime;
        Vector3 clamped = ClampToView(desired);

        // Got clamped along X — that's an edge, so turn around for next frame instead of pressing into it.
        if (Mathf.Abs(clamped.x - desired.x) > 0.0001f) maneuverDirection = -maneuverDirection;

        transform.position = clamped;
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
        if (routeIndex >= route.Count) return; // reached the end — holds position here

        var waypoint = route[routeIndex];
        if (waypoint.point == null) { routeIndex++; return; }

        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            return;
        }

        float speed = ConsumeMoveSpeed();
        if (speed <= 0.001f) return; // out of power this frame — holds position, tries again next frame

        Vector2 toPoint = (Vector2)waypoint.point.position - (Vector2)transform.position;
        float distance = toPoint.magnitude;

        if (distance <= speed * Time.deltaTime)
        {
            transform.position = waypoint.point.position;
            holdTimer = waypoint.holdSeconds;
            routeIndex++;
            return;
        }

        transform.position += (Vector3)(toPoint / distance * speed * Time.deltaTime);
    }
}
