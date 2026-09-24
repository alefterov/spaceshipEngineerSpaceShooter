using UnityEngine;

/// <summary>
/// Shows a block's wear as its own HP drops: 5 sprite tiers plus sparks/fire VFX at the low end. Listens
/// to the ShipModule it belongs to (same GameObject or a parent), so it reacts to ANY damage — meteor,
/// projectile, whatever — without anything having to push a ratio into it. SetHealthRatio(0..1) is still
/// public for anything that wants to drive it manually.
///
/// SETUP: put on each block prefab (on the root or on the child holding its body sprite) and assign the
/// sprite tiers you have art for. Every block tracks only its OWN health.
/// </summary>
public class BlockDamageVisual : MonoBehaviour
{
    [Header("Sprites (assign whichever tiers you have art for)")]
    public SpriteRenderer bodyRenderer;
    [Tooltip("100% HP.")]
    public Sprite pristineSprite;
    [Tooltip("Over 75% and up to 100%.")]
    public Sprite lightDamageSprite;
    [Tooltip("Over 50% and up to 75%.")]
    public Sprite moderateDamageSprite;
    [Tooltip("Over 25% and up to 50%.")]
    public Sprite heavyDamageSprite;
    [Tooltip("Up to 25%.")]
    public Sprite criticalDamageSprite;

    [Header("Effects")]
    [Tooltip("Shown at 50% HP or below.")]
    public GameObject sparksEffect;
    [Tooltip("Shown at 25% HP or below.")]
    public GameObject fireEffect;

    private ShipModule module;

    private void OnEnable()
    {
        module = GetComponentInParent<ShipModule>();
        if (module != null) module.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (module != null) module.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(ShipModule damaged, float amount)
    {
        if (damaged.maxHP > 0f) SetHealthRatio(damaged.CurrentHP / damaged.maxHP);
    }

    public void SetHealthRatio(float ratio01)
    {
        ratio01 = Mathf.Clamp01(ratio01);

        Sprite target = ratio01 >= 1f ? pristineSprite
            : ratio01 > 0.75f ? lightDamageSprite
            : ratio01 > 0.5f ? moderateDamageSprite
            : ratio01 > 0.25f ? heavyDamageSprite
            : criticalDamageSprite;

        if (bodyRenderer != null && target != null) bodyRenderer.sprite = target;

        if (sparksEffect != null) sparksEffect.SetActive(ratio01 <= 0.5f);
        if (fireEffect != null) fireEffect.SetActive(ratio01 <= 0.25f);
    }
}
