using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SlideOutPanel : MonoBehaviour
{
    [Tooltip("Seconds to slide fully open or fully closed.")]
    public float slideDuration = 0.3f;
    [Tooltip("How far (anchored-position units) the panel slides from its hidden position to reveal " +
             "itself. 0 = auto (the panel's own width). Flip the sign if it slides the wrong way — " +
             "which way is \"in\" depends on which edge of the screen you hid it against.")]
    public float showDistance = 0f;
    public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform rectTransform;
    private float hiddenX;
    private float shownX;
    private Coroutine slideRoutine;

    public bool IsShown { get; private set; }

    private bool initialized;

    private void Awake() => Init();

    private void Init()
    {
        if (initialized) return;
        initialized = true;

        rectTransform = (RectTransform)transform;
        hiddenX = rectTransform.anchoredPosition.x;

        if (showDistance == 0f) showDistance = rectTransform.rect.width;
        shownX = hiddenX + showDistance;
    }

    public void HideImmediate()
    {
        Init();
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = null;
        IsShown = false;
        SetX(hiddenX);
    }

    public void Show() => SlideTo(shownX, true);
    public void Hide() => SlideTo(hiddenX, false);
    public void Toggle()
    {
        if (IsShown) Hide();
        else Show();
    }

    private void SlideTo(float targetX, bool shown)
    {
        IsShown = shown;
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideRoutine(targetX));
    }

    private IEnumerator SlideRoutine(float targetX)
    {
        float startX = rectTransform.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = easing.Evaluate(Mathf.Clamp01(elapsed / slideDuration));
            SetX(Mathf.Lerp(startX, targetX, t));
            yield return null;
        }

        SetX(targetX);
        slideRoutine = null;
    }

    private void SetX(float x)
    {
        var pos = rectTransform.anchoredPosition;
        pos.x = x;
        rectTransform.anchoredPosition = pos;
    }
}
