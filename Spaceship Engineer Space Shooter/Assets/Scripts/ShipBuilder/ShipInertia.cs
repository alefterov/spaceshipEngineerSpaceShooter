using UnityEngine;

/// <summary>
/// The inertia of a ship under its own power. A ship has a velocity that changes only gradually:
///  - It speeds up toward the speed it is asked for over Acceleration Seconds (from standstill to full speed).
///  - Ask for nothing, or for less speed, and it slows down over Brake Seconds — it coasts to a stop instead of
///    halting on the spot.
///  - Ask for a clearly DIFFERENT direction (more than Turn Brake Angle away from where it is going) and it first
///    sheds the speed it has — all the way down to a standstill — and only then accelerates the new way. A smaller
///    change of heading is just followed gradually.
/// Because both times are measured against the ship's top speed (which engine thrust and mass decide), a heavy, weak
/// ship is as sluggish to turn as it is slow.
/// </summary>
public static class ShipInertia
{
    /// <summary>One step of the velocity. `desired` is the velocity the ship is asked for (zero = let go of the controls).</summary>
    public static Vector2 Step(Vector2 velocity, Vector2 desired, float maxSpeed, float accelerationSeconds,
                               float brakeSeconds, float turnBrakeAngle, float deltaTime)
    {
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        float accelerate = maxSpeed / Mathf.Max(0.05f, accelerationSeconds);
        float brake = maxSpeed / Mathf.Max(0.05f, brakeSeconds);

        // Nothing asked for: slow to a stop.
        if (desired.sqrMagnitude < 0.000001f)
            return Vector2.MoveTowards(velocity, Vector2.zero, brake * deltaTime);

        // Going one way, asked to go clearly another: get rid of the old speed first.
        if (velocity.sqrMagnitude > 0.0004f && Vector2.Angle(velocity, desired) > turnBrakeAngle)
            return Vector2.MoveTowards(velocity, Vector2.zero, brake * deltaTime);

        // Same general direction: speed up (or slow down, if asked for less than it has).
        float rate = desired.sqrMagnitude >= velocity.sqrMagnitude ? accelerate : brake;
        return Vector2.MoveTowards(velocity, desired, rate * deltaTime);
    }
}
