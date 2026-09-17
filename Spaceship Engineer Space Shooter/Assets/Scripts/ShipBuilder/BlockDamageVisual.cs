using UnityEngine;

/// <summary>
/// Shows a block's wear as its combined HP (hull + whatever module rides on it — see
/// ShipGrid.ApplyCollisionDamage) drops: 5 sprite tiers plus sparks/fire VFX at the low end. Purely
/// reactive — SetHealthRatio(0..1) is the only thing that drives it, so it knows nothing about combat,
/// modules, or the grid itself.
///
/// SETUP: goes on BOTH sides of a hit — the hull/armor block's own GameObject (its body sprite) AND
/// the module riding on it, if any (its roof sprite, visible on top once the ship is closed). Both get
/// pushed the SAME combined ratio, so a generator's roof shows the same wear as the hull under it.
/// </summary>
public class BlockDamageVisual : MonoBehaviour
{
    [Header("Sprites (assign whichever tiers you have art for)")]
    public SpriteRenderer bodyRenderer;
    [Tooltip("100% combined HP.")]
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
    [Tooltip("Shown at 50% combined HP or below.")]
    public GameObject sparksEffect;
    [Tooltip("Shown at 25% combined HP or below.")]
    public GameObject fireEffect;

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
