using UnityEngine;

public enum ProjectileOwner { Player, Enemy }

/// <summary>
/// Simple projectile that damages exactly the module collider it hits —
/// this is the core piece that makes "точечный урон по модулям" work.
/// Requires each module's Collider2D to be on its own GameObject (not a shared
/// composite collider) so OnTriggerEnter2D resolves to the specific part hit.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    public float damage = 5f;
    public float speed = 12f;
    public ProjectileOwner owner = ProjectileOwner.Player;
    public float lifeTime = 4f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifeTime);
    }

    private void Start()
    {
        rb.linearVelocity = transform.up * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore friendly fire based on layer/tag setup you define per project.
        var hitModule = other.GetComponent<ShipModule>();
        if (hitModule == null) return;

        bool isEnemyModule = other.CompareTag("EnemyShip");
        bool isPlayerModule = other.CompareTag("PlayerShip");

        if (owner == ProjectileOwner.Player && !isEnemyModule) return;
        if (owner == ProjectileOwner.Enemy && !isPlayerModule) return;

        // This is the key line: damage goes to the SPECIFIC module collider hit,
        // not to some abstract whole-ship HP bar.
        hitModule.TakeDamage(damage);

        Destroy(gameObject);
    }
}
