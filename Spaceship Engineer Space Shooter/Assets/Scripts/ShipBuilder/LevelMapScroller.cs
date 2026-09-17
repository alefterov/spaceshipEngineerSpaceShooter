using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolls the level-select map to the player's current "active" level every time this popup opens.
/// Resets to the very bottom first (clears whatever scroll position was left over from last time),
/// then scrolls up to reveal how far the player has progressed.
///
/// SETUP: put this anywhere on the level-select popup and assign Scroll Rect. Works regardless of how
/// you've laid the level buttons out inside Content (a plain vertical list, a staggered map with
/// hand-placed connecting lines, whatever) — it reads each LevelButtonView's actual RectTransform
/// position rather than assuming a particular layout.
/// </summary>
public class LevelMapScroller : MonoBehaviour
{
    public ScrollRect scrollRect;

    private void OnEnable()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        scrollRect.verticalNormalizedPosition = 0f; // bottom — clears whatever position was left from last time
        ScrollToActiveLevel();
    }

    /// <summary>Finds the furthest unlocked level among this map's buttons and scrolls it into view.
    /// "Furthest unlocked" = unlocked but not completed yet; if every unlocked level is already
    /// cleared, falls back to the highest-numbered unlocked one.</summary>
    public void ScrollToActiveLevel()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        var unlocked = scrollRect.content.GetComponentsInChildren<LevelButtonView>(true)
            .Where(b => b.level != null && data.IsLevelUnlocked(b.level))
            .ToList();
        if (unlocked.Count == 0) return;

        var active = unlocked.FirstOrDefault(b => !data.IsLevelCompleted(b.level))
                     ?? unlocked.OrderByDescending(b => b.level.displayNumber).First();

        ScrollToTarget((RectTransform)active.transform);
    }

    private void ScrollToTarget(RectTransform target)
    {
        Canvas.ForceUpdateCanvases(); // layout must be current before reading rects/positions

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight) return; // everything already fits — nothing to scroll

        // Target's Y measured from content's OWN bottom edge — independent of whatever pivot/anchor
        // Content happens to use, so this works whether it's top-pivoted (typical Vertical Layout
        // Group) or something else entirely for a hand-placed map.
        Vector2 targetInContent = content.InverseTransformPoint(target.position);
        float targetFromBottom = targetInContent.y + contentHeight * content.pivot.y;

        // Clamp so the viewport's CENTER lands on the target, without scrolling past either end.
        float minCenter = viewportHeight * 0.5f;
        float maxCenter = contentHeight - viewportHeight * 0.5f;
        float clampedCenter = Mathf.Clamp(targetFromBottom, minCenter, maxCenter);

        scrollRect.verticalNormalizedPosition = (clampedCenter - minCenter) / (maxCenter - minCenter);
    }
}
