using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows a block's wear as a dark "damage mask" laid over its body: small geometric marks (shards,
/// slivers, triangles) in a dark color that pile up as HP drops — the more damage, the more of them.
/// Marks are added as damage is taken and never move; they're clipped to the block's own silhouette
/// (a SpriteMask cut from the body sprite), so nothing spills past a triangle's hypotenuse or a
/// block's edge. Optional sparks/fire objects still switch on at low HP.
///
/// Listens to the ShipModule it belongs to (same GameObject or a parent), so it reacts to ANY damage —
/// meteor, projectile, whatever. ShipModule adds this component automatically to any block prefab that
/// doesn't already have one, with the defaults below; add it by hand only to tune a specific block.
/// A disabled instance (checkbox off) does nothing.
///
/// The marks are made from small generated shapes by default; assign Mark Sprites to use your own
/// (white/gray shapes work best — they get tinted by Mark Color).
/// </summary>
public class BlockDamageVisual : MonoBehaviour
{
    [Header("Body")]
    [Tooltip("The block's body sprite the marks go on. Left empty, it's found automatically (the first " +
             "sprite under the module's Roof Root).")]
    public SpriteRenderer bodyRenderer;

    [Header("Damage marks")]
    [Tooltip("Your own mark shapes. Leave empty to use generated triangles and slivers.")]
    public Sprite[] markSprites;
    public Color markColor = new(0.04f, 0.04f, 0.06f, 0.8f);
    [Tooltip("How many marks a block shows at 0 HP. Marks appear in proportion to damage taken: " +
             "half the HP gone = about half of this many.")]
    [Min(1)] public int maxMarks = 16;
    [Tooltip("Size range of one mark, in cells (1 = one grid cell).")]
    public Vector2 markSize = new(0.15f, 0.4f);
    [Tooltip("Clip marks to the body sprite's silhouette so they never show outside the block.")]
    public bool clipToBlock = true;

    [Header("Effects (optional)")]
    [Tooltip("Shown at 50% HP or below.")]
    public GameObject sparksEffect;
    [Tooltip("Shown at 25% HP or below.")]
    public GameObject fireEffect;

    private ShipModule module;
    private Transform marksRoot;
    private readonly List<GameObject> marks = new();

    private void OnEnable()
    {
        module = GetComponentInParent<ShipModule>();
        if (module != null)
        {
            module.OnDamaged += HandleDamaged;
            module.OnHealed += HandleHealed;
        }
    }

    private void OnDisable()
    {
        if (module != null)
        {
            module.OnDamaged -= HandleDamaged;
            module.OnHealed -= HandleHealed;
        }
    }

    private void HandleDamaged(ShipModule damaged, float amount)
    {
        if (damaged.maxHP > 0f) SetHealthRatio(damaged.CurrentHP / damaged.maxHP);
    }

    private void HandleHealed(ShipModule healed, float amount)
    {
        if (healed.maxHP > 0f) SetHealthRatio(healed.CurrentHP / healed.maxHP);
    }

    /// <summary>0 = destroyed, 1 = untouched. Adds marks as HP drops and removes them as it recovers
    /// (e.g. a RepairModule healing it mid-fight) — either way it always settles on the count that
    /// matches the CURRENT ratio. Public so anything can drive it.</summary>
    public void SetHealthRatio(float ratio01)
    {
        ratio01 = Mathf.Clamp01(ratio01);

        int target = ratio01 >= 1f ? 0 : Mathf.Max(1, Mathf.CeilToInt((1f - ratio01) * maxMarks));
        while (marks.Count < target) AddMark();
        while (marks.Count > target) RemoveMark();

        if (sparksEffect != null) sparksEffect.SetActive(ratio01 <= 0.5f);
        if (fireEffect != null) fireEffect.SetActive(ratio01 <= 0.25f);
    }

    private SpriteRenderer ResolveBody()
    {
        if (bodyRenderer != null) return bodyRenderer;

        if (module != null && module.roofRoot != null)
            bodyRenderer = module.roofRoot.GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        return bodyRenderer;
    }

    private void EnsureMarksRoot(SpriteRenderer body)
    {
        if (marksRoot != null) return;

        var rootObj = new GameObject("DamageMarks");
        marksRoot = rootObj.transform;
        marksRoot.SetParent(body.transform, false); // follows the block's own rotation and scale

        if (!clipToBlock) return;

        var maskObj = new GameObject("DamageMask");
        maskObj.transform.SetParent(body.transform, false);
        var mask = maskObj.AddComponent<SpriteMask>();
        mask.sprite = body.sprite;
        mask.alphaCutoff = 0.2f;
    }

    /// <summary>Removes the most-recently-added mark — repairing wears off the newest scars first, which
    /// reads fine either way since marks are randomly placed/shaped and interchangeable.</summary>
    private void RemoveMark()
    {
        int last = marks.Count - 1;
        if (last < 0) return;

        if (marks[last] != null) Destroy(marks[last]);
        marks.RemoveAt(last);
    }

    private void AddMark()
    {
        var body = ResolveBody();
        if (body == null || body.sprite == null) { marks.Add(null); return; } // nothing to draw on — still counts, so this can't loop forever

        EnsureMarksRoot(body);

        var obj = new GameObject("DamageMark");
        obj.transform.SetParent(marksRoot, false);

        var sr = obj.AddComponent<SpriteRenderer>();
        Vector3 scale;
        if (markSprites != null && markSprites.Length > 0)
        {
            sr.sprite = markSprites[Random.Range(0, markSprites.Length)];
            float s = Random.Range(markSize.x, markSize.y);
            scale = new Vector3(s, s, 1f);
        }
        else
        {
            // Generated shapes come in 1-unit sprites: a right triangle, or a thin sliver of a rectangle.
            bool triangle = Random.value < 0.5f;
            sr.sprite = triangle ? GeneratedSprites.Triangle : GeneratedSprites.Square;
            float s = Random.Range(markSize.x, markSize.y);
            scale = triangle
                ? new Vector3(s, s * Random.Range(0.6f, 1.4f), 1f)
                : new Vector3(s, s * Random.Range(0.12f, 0.4f), 1f);
        }

        obj.transform.localScale = scale;
        obj.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // Kept fully inside the body's bounds (with room for its own size at any rotation), so a mark can
        // never overlap a neighboring block — the silhouette mask then trims what's left over.
        Bounds b = body.sprite.bounds;
        float half = 0.5f * Mathf.Max(scale.x, scale.y);
        float x = b.size.x > 2f * half ? Random.Range(b.min.x + half, b.max.x - half) : b.center.x;
        float y = b.size.y > 2f * half ? Random.Range(b.min.y + half, b.max.y - half) : b.center.y;
        obj.transform.localPosition = new Vector3(x, y, 0f);

        sr.color = markColor;
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + 1; // just above the body it sits on, still below a weapon's moving parts
        if (clipToBlock) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        marks.Add(obj);
    }

    /// <summary>The default mark shapes, generated once and shared by every block.</summary>
    private static class GeneratedSprites
    {
        private static Sprite square, triangle;

        public static Sprite Square => square != null ? square : square = Make(false);
        public static Sprite Triangle => triangle != null ? triangle : triangle = Make(true);

        private static Sprite Make(bool triangleShape)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = (!triangleShape || x <= y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);

            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
