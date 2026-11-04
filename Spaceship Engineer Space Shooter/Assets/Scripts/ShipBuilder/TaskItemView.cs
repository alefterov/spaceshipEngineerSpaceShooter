using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskItemView : MonoBehaviour
{
    public TMP_Text titleLabel;
    public Image fillImage;
    public TMP_Text progressLabel;
    [Tooltip("The row of up to three rewards.")]
    public RewardRowView rewards;
    public Button claimButton;
    [Tooltip("Optional text on the claim button.")]
    public TMP_Text claimLabel;
    public string claimText = "Claim";
    public string claimedText = "Claimed";
    [Tooltip("Shown only once the reward has been taken.")]
    public GameObject claimedMark;
    [Range(0f, 1f)] public float claimedAlpha = 0.6f;

    public TaskDefinition Task { get; private set; }

    private Action<TaskDefinition> onClaim;
    private CanvasGroup group;

    private void Awake()
    {
        EnsureGroup();
    }

    private void EnsureGroup()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (claimButton != null) claimButton.onClick.AddListener(OnPressed);
    }

    private void OnDisable()
    {
        if (claimButton != null) claimButton.onClick.RemoveListener(OnPressed);
    }

    public void Setup(TaskDefinition task, RewardIcons icons, Action<TaskDefinition> claimCallback)
    {
        Task = task;
        onClaim = claimCallback;
        if (titleLabel != null) titleLabel.text = task.Title;
        if (rewards != null) rewards.Show(task.Rewards, icons);
    }

    public void Refresh(int progress, int target, bool claimed)
    {
        if (group == null) EnsureGroup();

        bool done = progress >= target;
        if (fillImage != null) fillImage.fillAmount = target > 0 ? Mathf.Clamp01((float)progress / target) : 0f;
        if (progressLabel != null) progressLabel.text = $"{NumberFormat.Compact(progress)} / {NumberFormat.Compact(target)}";

        if (claimButton != null) claimButton.interactable = done && !claimed;
        if (claimLabel != null) claimLabel.text = claimed ? claimedText : claimText;
        if (claimedMark != null) claimedMark.SetActive(claimed);
        group.alpha = claimed ? claimedAlpha : 1f;
    }

    private void OnPressed()
    {
        if (Task != null) onClaim?.Invoke(Task);
    }
}
