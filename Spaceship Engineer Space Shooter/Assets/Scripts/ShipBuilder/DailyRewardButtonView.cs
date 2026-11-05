using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyRewardButtonView : MonoBehaviour
{
    [Tooltip("The button that takes the gift. The rewards are shown on it.")]
    public Button button;
    [Tooltip("\"Day 5\".")]
    public TMP_Text dayLabel;
    public string dayFormat = "Day {0}";
    [Tooltip("The row of up to three rewards on the button.")]
    public RewardRowView rewards;

    [Header("States")]
    [Tooltip("Optional: shown while the gift can be taken (a glow, a \"Claim\" text...).")]
    public GameObject claimableMark;
    [Tooltip("Optional: shown once the gift has been taken (a check mark...).")]
    public GameObject claimedMark;
    [Tooltip("Optional: shown on today's day of the series.")]
    public GameObject todayMark;
    [Tooltip("Optional: shown on every fifth (special) day, if the same prefab is used for all days.")]
    public GameObject specialMark;
    [Tooltip("The whole button's opacity on a day not reached yet / a day whose gift is taken.")]
    [Range(0f, 1f)] public float lockedAlpha = 0.5f;
    [Range(0f, 1f)] public float claimedAlpha = 0.7f;

    public int Day { get; private set; }

    private Action<int> onClaim;
    private CanvasGroup group;

    private void Awake() => EnsureGroup();

    private void EnsureGroup()
    {
        if (group != null) return;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnPressed);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);
    }

    public void Setup(int day, bool special, IReadOnlyList<RewardData> data, RewardIcons icons, Action<int> claimCallback)
    {
        Day = day;
        onClaim = claimCallback;
        if (dayLabel != null) dayLabel.text = string.Format(dayFormat, day);
        if (specialMark != null) specialMark.SetActive(special);
        if (rewards != null) rewards.Show(data, icons);
    }

    public void Refresh(bool reached, bool claimed, bool today)
    {
        EnsureGroup();

        bool claimable = reached && !claimed;
        if (button != null) button.interactable = claimable;
        if (claimableMark != null) claimableMark.SetActive(claimable);
        if (claimedMark != null) claimedMark.SetActive(claimed);
        if (todayMark != null) todayMark.SetActive(today);
        group.alpha = claimed ? claimedAlpha : reached ? 1f : lockedAlpha;
    }

    private void OnPressed() => onClaim?.Invoke(Day);
}
