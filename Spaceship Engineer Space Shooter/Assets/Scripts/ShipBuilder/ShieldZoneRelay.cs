using UnityEngine;

public class ShieldZoneRelay : MonoBehaviour
{
    public ShieldModule owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null) owner.HandleIncoming(other);
    }
}
