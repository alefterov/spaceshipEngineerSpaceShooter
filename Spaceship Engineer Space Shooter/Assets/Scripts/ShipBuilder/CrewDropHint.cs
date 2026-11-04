using System.Collections;
using UnityEngine;

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
        group.blocksRaycasts = false;
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
