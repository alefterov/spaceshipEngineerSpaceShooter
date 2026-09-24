using UnityEngine;

/// <summary>Sits on ShieldModule's generated trigger child and forwards whatever enters the protected
/// area to the module — OnTriggerEnter2D only fires on the object that owns the collider. Added by
/// ShieldModule itself; never attach by hand.</summary>
public class ShieldZoneRelay : MonoBehaviour
{
    public ShieldModule owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null) owner.HandleIncoming(other);
    }
}
