using UnityEngine;

public static class ShipInertia
{
    public static Vector2 Step(Vector2 velocity, Vector2 desired, float maxSpeed, float accelerationSeconds,
                               float brakeSeconds, float turnBrakeAngle, float deltaTime)
    {
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        float accelerate = maxSpeed / Mathf.Max(0.05f, accelerationSeconds);
        float brake = maxSpeed / Mathf.Max(0.05f, brakeSeconds);

        if (desired.sqrMagnitude < 0.000001f)
            return Vector2.MoveTowards(velocity, Vector2.zero, brake * deltaTime);

        if (velocity.sqrMagnitude > 0.0004f && Vector2.Angle(velocity, desired) > turnBrakeAngle)
            return Vector2.MoveTowards(velocity, Vector2.zero, brake * deltaTime);

        float rate = desired.sqrMagnitude >= velocity.sqrMagnitude ? accelerate : brake;
        return Vector2.MoveTowards(velocity, desired, rate * deltaTime);
    }
}
