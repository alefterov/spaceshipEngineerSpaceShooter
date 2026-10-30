using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rewards panel of the main menu. Two parts:
///  - TOP: the player-level bar — a horizontal scroll with one button per level (1 to Max Level), each showing its reward; the bar fills as
///    the player's level (and the experience toward the next one) grows, and a button turns on when its level is reached. Pressing it takes
///    the reward (PlayerLevelRewards).
///  - BOTTOM: the tasks — a vertical scroll list, one TaskItemView per task of the TaskDatabase: its text, a progress bar, the reward and a
///    button that takes it once the task is done. Ready-to-claim tasks are listed first, taken ones last.
/// Taking a reward plays GameDataManager's reward sound. The panel opens and closes like the other popups (Open / Close / Toggle).
///
/// SETUP: the Rewards Panel Setup menu builds all of it (Tools → Ship Builder → Create Rewards Panel). By hand: put this on the panel root,
/// assign the scrolls, the bar's Track / Fill, both item prefabs and the icons; wire the main menu's reward button to Open() and the close
/// button to Close(). The scene may hold one sample item in each scroll — they are replaced by real ones when the panel opens.
/// </summary>
public class RewardsPanel : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The button that closes the panel. The button that OPENS it (the main menu's reward button) calls Open() in its OnClick.")]
    public Button closeButton;
    [Tooltip("Seconds of the fade-in when the panel opens.")]
    [Min(0f)] public float fadeInSeconds = 0.15f;

    [Header("Player level bar (top)")]
    public ScrollRect levelScroll;
    [Tooltip("The scroll's content: a Horizontal Layout Group. The Track and Fill below are its children that ignore the layout.")]
    public RectTransform levelContent;
    public LevelRewardButtonView levelRewardPrefab;
    [Tooltip("The thin line behind the buttons (the empty part of the bar).")]
    public Image barTrack;
    [Tooltip("The same line, filled up to the player's level. Anchored to the left of the content; its width is set from code.")]
    public RectTransform barFill;
    [Tooltip("Optional: \"Уровень 12\".")]
    public TMP_Text currentLevelLabel;
    public string levelFormat = "Уровень {0}";
    [Min(1)] public int maxLevel = 100;

    [Header("Tasks (bottom)")]
    public ScrollRect taskScroll;
    public RectTransform taskContent;
    public TaskItemView taskItemPrefab;

    [Header("Icons")]
    public RewardIcons icons = new();

    private readonly List<LevelRewardButtonView> levelButtons = new();
    private readonly List<TaskItemView> taskItems = new();
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
        if (data != null && !subscribed)
        {
            data.OnRewardsChanged += Refresh;
            data.OnExperienceChanged += Refresh;
            data.OnLevelStarsChanged += OnStarsChanged;
            subscribed = true;
        }

        BuildLevelBar();
        BuildTasks();
        Refresh();

        StartCoroutine(OpenRoutine());
    }

    private void OnDisable()
    {
        var data = GameDataManager.Instance;
        if (data != null && subscribed)
        {
            data.OnRewardsChanged -= Refresh;
            data.OnExperienceChanged -= Refresh;
            data.OnLevelStarsChanged -= OnStarsChanged;
        }

        subscribed = false;
    }

    private void OnStarsChanged(string _) => Refresh();

    // =====================================================================================================
    //  Building
    // =====================================================================================================

    private static void ClearChildren(RectTransform content, params Transform[] keep)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (keep.Contains(child)) continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void BuildLevelBar()
    {
        if (levelContent == null || levelRewardPrefab == null) return;

        // The sample button in the scene (and the previous opening's buttons) go; the track and the fill stay.
        ClearChildren(levelContent, barTrack != null ? barTrack.transform : null, barFill != null ? barFill.transform : null);
        levelButtons.Clear();

        var data = GameDataManager.Instance;
        for (int level = 1; level <= maxLevel; level++)
        {
            var view = Instantiate(levelRewardPrefab, levelContent);
            view.gameObject.SetActive(true);
            view.name = $"LevelReward_{level}";
            view.Setup(level, data != null ? data.GetLevelRewards(level) : null, icons, OnLevelRewardPressed);
            levelButtons.Add(view);
        }

        if (barTrack != null) barTrack.transform.SetAsFirstSibling();
        if (barFill != null) barFill.transform.SetSiblingIndex(1);
    }

    private void BuildTasks()
    {
        if (taskContent == null || taskItemPrefab == null) return;

        ClearChildren(taskContent);
        taskItems.Clear();

        var data = GameDataManager.Instance;
        if (data == null || data.taskDatabase == null) return;

        // Ready to take first, then the unfinished (the furthest along first), the taken ones last.
        var ordered = data.taskDatabase.tasks.Where(t => t != null)
            .OrderBy(t => data.IsTaskClaimed(t) ? 2 : data.IsTaskComplete(t) ? 0 : 1)
            .ThenByDescending(t => (float)data.GetTaskProgress(t) / t.TargetValue)
            .ToList();

        foreach (var task in ordered)
        {
            var item = Instantiate(taskItemPrefab, taskContent);
            item.gameObject.SetActive(true);
            item.name = "Task_" + task.id;
            item.Setup(task, icons, OnTaskPressed);
            taskItems.Add(item);
        }
    }

    // =====================================================================================================
    //  Showing the state
    // =====================================================================================================

    private void Refresh()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        foreach (var button in levelButtons)
            button.Refresh(data.PlayerLevel >= button.Level, data.IsLevelRewardClaimed(button.Level));

        foreach (var item in taskItems)
            item.Refresh(data.GetTaskProgress(item.Task), item.Task.TargetValue, data.IsTaskClaimed(item.Task));

        if (currentLevelLabel != null) currentLevelLabel.text = string.Format(levelFormat, data.PlayerLevel);
        UpdateBarFill();
    }

    /// <summary>The bar reaches the middle of the current level's button, and runs on toward the next one by the share of experience earned.</summary>
    private void UpdateBarFill()
    {
        var data = GameDataManager.Instance;
        if (barFill == null || data == null || levelButtons.Count == 0) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(levelContent);

        int level = Mathf.Clamp(data.PlayerLevel, 0, maxLevel);
        float from = level <= 0 ? 0f : CenterX(level);
        float to = level >= maxLevel ? from : CenterX(level + 1);

        int required = data.GetExperienceRequiredForLevel(level);
        float share = level >= maxLevel || required <= 0 ? 0f : Mathf.Clamp01((float)data.PlayerExperience / required);

        barFill.sizeDelta = new Vector2(Mathf.Lerp(from, to, share), barFill.sizeDelta.y);
    }

    private float CenterX(int level) => ((RectTransform)levelButtons[level - 1].transform).localPosition.x - LeftEdge();

    // The content's own left edge in the coordinates its children use (the content's pivot is not necessarily at the left).
    private float LeftEdge() => -levelContent.rect.width * levelContent.pivot.x;

    // =====================================================================================================
    //  Taking rewards
    // =====================================================================================================

    private void OnLevelRewardPressed(int level) => GameDataManager.Instance?.TryClaimLevelReward(level);

    private void OnTaskPressed(TaskDefinition task) => GameDataManager.Instance?.TryClaimTask(task);

    // =====================================================================================================
    //  Opening
    // =====================================================================================================

    private IEnumerator OpenRoutine()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;

        yield return null; // the layout of the freshly built items settles in this frame

        UpdateBarFill();
        ScrollToCurrentLevel();
        if (taskScroll != null) taskScroll.verticalNormalizedPosition = 1f; // the top of the list

        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        group.alpha = 1f;
    }

    private void ScrollToCurrentLevel()
    {
        var data = GameDataManager.Instance;
        if (levelScroll == null || data == null || levelButtons.Count == 0 || levelScroll.viewport == null) return;

        // Show the next reward waiting to be taken, or else the player's current level, in the middle of the bar.
        int focus = Mathf.Clamp(data.PlayerLevel, 1, maxLevel);
        for (int level = 1; level <= Mathf.Min(data.PlayerLevel, maxLevel); level++)
        {
            if (data.CanClaimLevelReward(level)) { focus = level; break; }
        }

        float viewport = levelScroll.viewport.rect.width;
        float scrollable = levelContent.rect.width - viewport;
        if (scrollable <= 0f) return;

        float target = (CenterX(focus) - viewport * 0.5f) / scrollable;
        levelScroll.horizontalNormalizedPosition = Mathf.Clamp01(target);
    }
}
