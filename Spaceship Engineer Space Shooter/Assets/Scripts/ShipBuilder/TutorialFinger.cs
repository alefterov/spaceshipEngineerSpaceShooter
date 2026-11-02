using UnityEngine;

/// <summary>
/// The pointing finger of the tutorial: a picture on the tutorial canvas that taps a button, drags from one point to another, or moves
/// like a thumb on a joystick. TutorialController tells it what to do; it only animates.
///
/// SETUP: an Image (your finger icon) on the tutorial canvas, Raycast Target off. Put its PIVOT on the fingertip — that point is what
/// touches the target.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialFinger : MonoBehaviour
{
    [Header("Tap")]
    [Tooltip("Taps per second.")]
    [Min(0.1f)] public float tapsPerSecond = 1.2f;
    [Tooltip("How much smaller the finger gets when it presses (0.15 = 15%).")]
    [Range(0f, 0.5f)] public float pressScale = 0.15f;

    [Header("Drag")]
    [Tooltip("Seconds one drag takes (plus a short pause before the next).")]
    [Min(0.1f)] public float dragSeconds = 1.3f;

    [Header("Joystick")]
    [Tooltip("Seconds for one round: right, left, up, down.")]
    [Min(0.5f)] public float joystickRoundSeconds = 3.2f;

    private RectTransform rect;
    private CanvasGroup group;
    private TutorialFingerMotion motion;
    private Vector2 from;
    private Vector2 to;
    private float radius;
    private float startTime;

    private void Awake()
    {
        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); // positions come measured from the middle of the tutorial canvas
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    /// <summary>Starts (or keeps) a motion. Positions are in the parent's local space; radius is the joystick swing.</summary>
    public void Play(TutorialFingerMotion newMotion, Vector2 start, Vector2 end, float swing)
    {
        if (rect == null) Awake();
        if (newMotion != motion) startTime = Time.unscaledTime;
        motion = newMotion;
        from = start;
        to = end;
        radius = swing;
        gameObject.SetActive(motion != TutorialFingerMotion.None);
    }

    public void Hide()
    {
        motion = TutorialFingerMotion.None;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        float t = Time.unscaledTime - startTime;
        switch (motion)
        {
            case TutorialFingerMotion.Tap:
            {
                float phase = Mathf.Repeat(t * tapsPerSecond, 1f);
                float press = phase < 0.5f ? Mathf.Sin(phase * 2f * Mathf.PI) : 0f; // a quick press, then a rest
                rect.anchoredPosition = from;
                rect.localScale = Vector3.one * (1f - pressScale * press);
                group.alpha = 1f;
                break;
            }
            case TutorialFingerMotion.Drag:
            {
                float cycle = dragSeconds + 0.6f;
                float phase = Mathf.Repeat(t, cycle);
                float k = Mathf.Clamp01(phase / dragSeconds);
                k = k * k * (3f - 2f * k); // smooth start and stop
                rect.anchoredPosition = Vector2.Lerp(from, to, k);
                rect.localScale = Vector3.one * (1f - pressScale * 0.6f); // pressed while dragging
                group.alpha = phase < dragSeconds ? 1f : 1f - (phase - dragSeconds) / 0.6f; // fades before starting over
                break;
            }
            case TutorialFingerMotion.Joystick:
            {
                // Four swings from the middle: right, left, up, down — each out and back.
                float quarter = joystickRoundSeconds / 4f;
                int leg = (int)(Mathf.Repeat(t, joystickRoundSeconds) / quarter);
                float k = Mathf.Sin(Mathf.Repeat(t, quarter) / quarter * Mathf.PI); // 0 -> 1 -> 0
                Vector2 dir = leg switch { 0 => Vector2.right, 1 => Vector2.left, 2 => Vector2.up, _ => Vector2.down };
                rect.anchoredPosition = from + dir * radius * k;
                rect.localScale = Vector3.one * (1f - pressScale * 0.6f);
                group.alpha = 1f;
                break;
            }
        }
    }
}
