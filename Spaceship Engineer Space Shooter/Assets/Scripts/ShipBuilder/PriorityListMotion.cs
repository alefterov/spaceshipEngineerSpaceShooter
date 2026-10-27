using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the rows of a priority list slide smoothly into their places whenever the list is rearranged, instead of jumping. It lives on the
/// layout-group container that holds the rows (added automatically by the first row that is dragged). Every change that moves rows
/// goes through Rearrange: it notes where each row is, lets the layout group put them all in their new places at once, then sends
/// each row back to where it was and lets it glide to its new place. Runs on unscaled time, so it works while the battle is paused.
/// </summary>
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

    /// <summary>Where a row is headed (its resting place), or where it is now if it is not gliding.</summary>
    public Vector2 RestingPosition(RectTransform row) => targets.TryGetValue(row, out var target) ? target : (Vector2)row.localPosition;

    /// <summary>Stops moving a row (while it is being dragged by hand).</summary>
    public void Release(RectTransform row) => targets.Remove(row);

    /// <summary>Runs `change` (which reorders / adds / removes rows) and glides every row that the layout moved.</summary>
    public void Rearrange(Action change)
    {
        if (parentRect == null) parentRect = (RectTransform)transform;

        var before = new Dictionary<RectTransform, Vector2>();
        for (int i = 0; i < parentRect.childCount; i++)
        {
            if (parentRect.GetChild(i) is RectTransform child) before[child] = child.localPosition;
        }

        change();
        LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect); // every row is now at its new place

        foreach (var pair in before)
        {
            var row = pair.Key;
            if (row == null || !row.gameObject.activeInHierarchy) { targets.Remove(row); continue; }

            Vector2 resting = row.localPosition;
            if ((resting - pair.Value).sqrMagnitude < 0.0001f) continue; // not moved by the layout

            targets[row] = resting;
            row.localPosition = new Vector3(pair.Value.x, pair.Value.y, row.localPosition.z); // start from where it was
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
        // Closed in the middle of a glide: put every row where the layout wants it.
        foreach (var pair in targets)
            if (pair.Key != null) pair.Key.localPosition = new Vector3(pair.Value.x, pair.Value.y, pair.Key.localPosition.z);
        targets.Clear();
    }
}
