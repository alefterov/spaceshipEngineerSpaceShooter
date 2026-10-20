using UnityEngine;

/// <summary>
/// A chunk of an enemy ship that was cut loose by destroyed blocks (see ShipSplitter): a lifeless object that drifts
/// toward the player's ship at Drift Speed and slowly spins. Its blocks are still enemy blocks — they can be shot,
/// hit by missiles, and they ram the player's ship on contact (RamCollision). It never fires.
/// </summary>
public class ShipFragment : MonoBehaviour
{
    private ShipBody body;
    private Transform target;
    private float driftSpeed;
    private float spinDegreesPerSecond;

    /// <summary>Starts the fragment drifting. `maxSpin` = the most it turns per second, either way (random).</summary>
    public void Begin(Transform driftTarget, float speed, float maxSpin)
    {
        body = ShipBody.GetOrAdd(this);
        target = driftTarget;
        driftSpeed = speed;
        spinDegreesPerSecond = Random.Range(-maxSpin, maxSpin);
    }

    private void Update()
    {
        if (target == null)
        {
            var playerObj = GameObject.FindWithTag("PlayerShip");
            if (playerObj != null) target = playerObj.GetComponentInParent<ShipIdentity>()?.transform;
        }

        if (target != null)
        {
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.position += body.FilterMove((Vector3)(toTarget.normalized * driftSpeed * Time.deltaTime));
        }

        transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);
    }
}
