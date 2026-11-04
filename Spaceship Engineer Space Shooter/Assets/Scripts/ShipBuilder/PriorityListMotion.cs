using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PriorityListMotion : MonoBehaviour
{
    [Tooltip("How quickly a row glides to its new place (bigger = snappier). About 14 settles in a fifth of a second.")]
    [Min(1f)] public float glideSpeed = 14f;

    private readonly Dictionary<RectTransform, Vector2> targets = new();
    private readonly List<RectTransform> done = new();
    private RectTransform parentRect;

    public static PriorityListMotion For(Transform parent)
    {
        var motion = parent.GetComponent<PriorityListMotion>();
        if (motion == null) motion = parent.gameObject.AddComponent<PriorityListMotion>();
        motion.parentRect = (RectTransform)parent;
        return motion;
    }

    public Vector2 RestingPosition(RectTransform row) => targets.TryGetValue(row, out var target) ? target : (Vector2)row.localPosition;

    public void Release(RectTransform row) => targets.Remove(row);

    public void Rearrange(Action change)
    {
        if (parentRect == null) parentRect = (RectTransform)transform;

        var before = new Dictionary<RectTransform, Vector2>();
        for (int i = 0; i < parentRect.childCount; i++)
        {
            if (parentRect.GetChild(i) is RectTransform child) before[child] = child.localPosition;
        }

        change();
        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        foreach (var pair in before)
        {
            var row = pair.Key;
            if (row == null || !row.gameObject.activeInHierarchy) { targets.Remove(row); continue; }

            Vector2 resting = row.localPosition;
            if ((resting - pair.Value).sqrMagnitude < 0.0001f) continue;

            targets[row] = resting;
            row.localPosition = new Vector3(pair.Value.x, pair.Value.y, row.localPosition.z);
        }
    }

    private void Update()
    {
        if (targets.Count == 0) return;

        float blend = 1f - Mathf.Exp(-glideSpeed * Time.unscaledDeltaTime);
        done.Clear();

        foreach (var pair in targets)
        {
            var row = pair.Key;
            if (row == null) { done.Add(row); continue; }

            Vector2 next = Vector2.Lerp(row.localPosition, pair.Value, blend);
            if ((next - pair.Value).sqrMagnitude < 0.01f) { next = pair.Value; done.Add(row); }
            row.localPosition = new Vector3(next.x, next.y, row.localPosition.z);
        }

        foreach (var row in done) targets.Remove(row);
    }

    private void OnDisable()
    {
        foreach (var pair in targets)
            if (pair.Key != null) pair.Key.localPosition = new Vector3(pair.Value.x, pair.Value.y, pair.Key.localPosition.z);
        targets.Clear();
    }
}
