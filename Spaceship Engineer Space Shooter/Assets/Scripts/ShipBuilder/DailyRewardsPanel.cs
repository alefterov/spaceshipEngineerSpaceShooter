using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyRewardsPanel : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The button that closes the panel. The button that OPENS it calls DailyRewardsPanel.Open() in its OnClick.")]
    public Button closeButton;
    [Tooltip("Seconds of the fade-in when the panel opens.")]
    [Min(0f)] public float fadeInSeconds = 0.15f;

    [Header("Days")]
    [Tooltip("The scroll view of the days (horizontal or vertical).")]
    public ScrollRect scroll;
    [Tooltip("The scroll's content, with a Horizontal / Vertical / Grid Layout Group. The day buttons are created in it.")]
    public RectTransform content;
    [Tooltip("The button of an ordinary day.")]
    public DailyRewardButtonView dayPrefab;
    [Tooltip("Optional: the button of every fifth (special) day. Empty = the ordinary one, with its Special Mark shown.")]
    public DailyRewardButtonView specialDayPrefab;

    [Header("Labels (optional)")]
    [Tooltip("\"Day 7 of 30\".")]
    public TMP_Text seriesLabel;
    public string seriesFormat = "Day {0} of {1}";

    [Header("Icons")]
    public RewardIcons icons = new();

    private readonly List<DailyRewardButtonView> buttons = new();
    private bool subscribed;
    private CanvasGroup group;

    public bool IsOpen => gameObject.activeSelf;

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void OnEnable()
    {
        var data = GameDataManager.Instance;
        if (data != null)
        {
            data.UpdateDailyVisit();
            if (!subscribed)
            {
                data.OnRewardsChanged += Refresh;
                subscribed = true;
            }
        }

        Build();
        Refresh();
        StartCoroutine(OpenRoutine());
    }

    private void OnDisable()
    {
        var data = GameDataManager.Instance;
        if (data != null && subscribed) data.OnRewardsChanged -= Refresh;
        subscribed = false;
    }

    private void Build()
    {
        if (content == null || dayPrefab == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            child.SetActive(false);
            if (IsTemplate(child)) continue;
            Destroy(child);
        }

        buttons.Clear();

        var data = GameDataManager.Instance;
        if (data == null || data.dailyRewards == null) return;

        for (int day = 1; day <= data.DailyDaysCount; day++)
        {
            bool special = data.dailyRewards.IsSpecialDay(day);
            var prefab = special && specialDayPrefab != null ? specialDayPrefab : dayPrefab;
            var view = Instantiate(prefab, content);
            view.gameObject.SetActive(true);
            view.name = $"DailyReward_{day}";
            view.Setup(day, special, data.GetDailyRewards(day), icons, OnDayPressed);
            buttons.Add(view);
        }
    }

    private bool IsTemplate(GameObject child)
        => (dayPrefab != null && child == dayPrefab.gameObject) || (specialDayPrefab != null && child == specialDayPrefab.gameObject);

    private void Refresh()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        foreach (var button in buttons)
            button.Refresh(data.IsDailyRewardReached(button.Day), data.IsDailyRewardClaimed(button.Day), button.Day == data.DailyDay);

        if (seriesLabel != null) seriesLabel.text = string.Format(seriesFormat, data.DailyDay, data.DailyDaysCount);
    }

    private void OnDayPressed(int day) => GameDataManager.Instance?.TryClaimDailyReward(day);

    private IEnumerator OpenRoutine()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        yield return null;

        ScrollToFocusDay();

        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        group.alpha = 1f;
    }

    private void ScrollToFocusDay()
    {
        var data = GameDataManager.Instance;
        if (scroll == null || data == null || buttons.Count == 0) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        int focus = Mathf.Clamp(data.DailyDay, 1, buttons.Count);
        for (int day = 1; day <= Mathf.Min(data.DailyDay, buttons.Count); day++)
        {
            if (data.CanClaimDailyReward(day)) { focus = day; break; }
        }

        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        var item = (RectTransform)buttons[focus - 1].transform;
        Vector3 center = content.InverseTransformPoint(item.TransformPoint(item.rect.center));

        if (scroll.horizontal)
        {
            float scrollable = content.rect.width - viewport.rect.width;
            if (scrollable > 0f)
            {
                float fromLeft = center.x - content.rect.xMin;
                scroll.horizontalNormalizedPosition = Mathf.Clamp01((fromLeft - viewport.rect.width * 0.5f) / scrollable);
            }
        }

        if (scroll.vertical)
        {
            float scrollable = content.rect.height - viewport.rect.height;
            if (scrollable > 0f)
            {
                float fromTop = content.rect.yMax - center.y;
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((fromTop - viewport.rect.height * 0.5f) / scrollable);
            }
        }
    }
}
