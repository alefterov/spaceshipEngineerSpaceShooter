using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LevelMapScroller : MonoBehaviour
{
    public ScrollRect scrollRect;

    private void OnEnable()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        scrollRect.verticalNormalizedPosition = 0f;
        ScrollToActiveLevel();
    }

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
        Canvas.ForceUpdateCanvases();

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;
        if (contentHeight <= viewportHeight) return;

        Vector2 targetInContent = content.InverseTransformPoint(target.position);
        float targetFromBottom = targetInContent.y + contentHeight * content.pivot.y;

        float minCenter = viewportHeight * 0.5f;
        float maxCenter = contentHeight - viewportHeight * 0.5f;
        float clampedCenter = Mathf.Clamp(targetFromBottom, minCenter, maxCenter);

        scrollRect.verticalNormalizedPosition = (clampedCenter - minCenter) / (maxCenter - minCenter);
    }
}
