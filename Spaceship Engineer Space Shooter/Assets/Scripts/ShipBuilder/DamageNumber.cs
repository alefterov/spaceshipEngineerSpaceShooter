using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One floating damage number (the prefab DamageNumbers spawns). It appears in the middle of the block that was hit, hops up
/// and sideways — randomly to the right or to the left — then falls under gravity while fading out, and removes itself.
///
/// PREFAB: any object with this component and a text on it or on a child: TextMeshPro (3D, or inside a world-space canvas)
/// — or a legacy UI Text / TextMesh. The text's colour is set by DamageNumbers (white / red), so its own colour does not
/// matter; its size, font and outline are yours. All the motion below is tuned here.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Seconds from appearing to vanishing.")]
    [Min(0.1f)] public float lifetime = 0.9f;
    [Tooltip("Sideways speed (world units/second), picked between these two; the direction is random, left or right.")]
    public Vector2 sideSpeed = new(0.6f, 1.4f);
    [Tooltip("Upward speed at the start (world units/second), picked between these two.")]
    public Vector2 upSpeed = new(2.5f, 3.5f);
    [Tooltip("How fast it falls back down.")]
    public float gravity = 9f;

    [Header("Look")]
    [Tooltip("The fade-out starts after this part of the lifetime (0.6 = the last 40% it becomes see-through).")]
    [Range(0f, 0.95f)] public float fadeStart = 0.6f;
    [Tooltip("It pops in from this fraction of its size...")]
    [Range(0.1f, 1f)] public float startScale = 0.6f;
    [Tooltip("...over this many seconds.")]
    public float popSeconds = 0.1f;
    [Tooltip("A critical hit is drawn this many times bigger.")]
    public float critScale = 1.35f;
    [Tooltip("Drawing order, so the number is above ships and effects.")]
    public int sortingOrder = 200;

    /// <summary>Called when the number is gone (used to count how many are on screen).</summary>
    public event Action OnFinished;

    private TMP_Text tmp;
    private TextMesh textMesh;
    private Text uiText;
    private Color baseColor = Color.white;
    private Vector2 velocity;
    private float age;
    private float sizeMultiplier = 1f;
    private Vector3 baseScale;

    private void Awake()
    {
        tmp = GetComponentInChildren<TMP_Text>(true);
        if (tmp == null) textMesh = GetComponentInChildren<TextMesh>(true);
        if (tmp == null && textMesh == null) uiText = GetComponentInChildren<Text>(true);

        baseScale = transform.localScale;

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
        else
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.sortingOrder = sortingOrder;
        }
    }

    public void Show(string text, Color color, bool crit)
    {
        baseColor = color;
        sizeMultiplier = crit ? critScale : 1f;

        if (tmp != null) tmp.text = text;
        else if (textMesh != null) textMesh.text = text;
        else if (uiText != null) uiText.text = text;

        SetColor(color);

        float side = UnityEngine.Random.Range(sideSpeed.x, sideSpeed.y) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        velocity = new Vector2(side, UnityEngine.Random.Range(upSpeed.x, upSpeed.y));
        ApplyScale(startScale);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        float pop = popSeconds > 0f ? Mathf.Clamp01(age / popSeconds) : 1f;
        ApplyScale(Mathf.Lerp(startScale, 1f, pop));

        float t = age / lifetime;
        if (t > fadeStart)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - (t - fadeStart) / (1f - fadeStart));
            SetColor(c);
        }
    }

    private void OnDestroy() => OnFinished?.Invoke();

    private void ApplyScale(float factor) => transform.localScale = baseScale * (factor * sizeMultiplier);

    private void SetColor(Color color)
    {
        if (tmp != null) tmp.color = color;
        else if (textMesh != null) textMesh.color = color;
        else if (uiText != null) uiText.color = color;
    }
}
