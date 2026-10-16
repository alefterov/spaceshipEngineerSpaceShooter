using System.Collections;
using UnityEngine;

/// <summary>
/// An icon (any UI object) that fades in while a crew card is being dragged and fades out the moment the
/// finger lifts. Put it on the hint icon you place inside a crew slot or a merge window; it hides itself
/// at start (alpha 0) and never blocks touches.
///
/// By default it shows only where the dragged card can actually be dropped — it looks for the slot / merge
/// window it sits in (the nearest ICrewDropTarget among its parents) and asks it. Turn Only When Accepted
/// off to show it on every slot during any drag.
///
/// SETUP: add as a CHILD of the slot (CrewMemberView) or merge window (CrewMergeSlot), on the icon itself or
/// on a parent of the icons. The object must stay active in the scene (it fades with alpha, it is not
/// switched off). A CanvasGroup is added automatically.
/// </summary>
public class CrewDropHint : MonoBehaviour
{
    [Tooltip("Seconds to fade in when a drag starts.")]
    [Min(0f)] public float fadeInSeconds = 0.2f;
    [Tooltip("Seconds to fade out when the finger lifts.")]
    [Min(0f)] public float fadeOutSeconds = 0.2f;
    [Range(0f, 1f)] public float shownAlpha = 1f;
    [Tooltip("Show only if the dragged card can be dropped on the slot / window this icon belongs to.")]
    public bool onlyWhenAccepted = true;

    private CanvasGroup group;
    private Coroutine fade;

    private void Awake()
    {
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false; // never steals the drop from the slot underneath
        group.interactable = false;
    }

    private void OnEnable()
    {
        CrewDragHandle.DragStarted += OnDragStarted;
        CrewDragHandle.DragEnded += OnDragEnded;
        if (group != null) group.alpha = 0f;
    }

    private void OnDisable()
    {
        CrewDragHandle.DragStarted -= OnDragStarted;
        CrewDragHandle.DragEnded -= OnDragEnded;
        fade = null;
        if (group != null) group.alpha = 0f;
    }

    private void OnDragStarted(CrewMemberData member)
    {
        if (onlyWhenAccepted)
        {
            var target = GetComponentInParent<ICrewDropTarget>(true);
            if (target != null && !target.CanAccept(member)) return;
        }

        FadeTo(shownAlpha, fadeInSeconds);
    }

    private void OnDragEnded() => FadeTo(0f, fadeOutSeconds);

    private void FadeTo(float target, float seconds)
    {
        if (!isActiveAndEnabled) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(target, seconds));
    }

    private IEnumerator FadeRoutine(float target, float seconds)
    {
        float start = group.alpha;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }

        group.alpha = target;
        fade = null;
    }
}
